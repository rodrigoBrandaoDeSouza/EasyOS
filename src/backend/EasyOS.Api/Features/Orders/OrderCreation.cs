using EasyOS.Api.Common;
using EasyOS.Domain.Entities;
using EasyOS.Infrastructure;
using EasyOS.Infrastructure.MultiTenancy;
using EasyOS.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace EasyOS.Api.Features.Orders;

/// <summary>
/// Lançamento de OS (equivale ao OrderBLL.create do legado): numeração sequencial por cliente
/// ou por empresa, proteção contra duplicidade, check-in da visita e gravação de todos os detalhes.
/// </summary>
public sealed class OrderCreation(TenantDbContext db, Access access, ITenantContext tenant, IClock clock)
{
    private const int MaxAttempts = 5;
    private const int MaxEvidenceLength = 3 * 1024 * 1024;

    public async Task<Order> CreateAsync(OrderCreateRequest req, CancellationToken ct)
    {
        var customer = await access.CustomerAsync(req.CustomerId, ct);
        if (!await access.CompanyUsers().AnyAsync(u => u.Id == req.TechnicianId, ct))
            throw new BusinessException("Informe o técnico que executou o serviço.");
        if (req.Details is null) throw new BusinessException("Ordem de serviço com dados incompletos.");
        if (req.VisitId != 0 && !await access.Visits().AnyAsync(v => v.Id == req.VisitId && v.CustomerId == customer.Id, ct))
            throw new BusinessException("Visita inválida para este cliente.");
        Validate(req);
        await ValidateReferencesAsync(req, ct);

        var offset = LegacyTime.NormalizeOffset(req.OffsetMinutes);
        var timeStart = LegacyTime.ToStored(req.TimeStart, offset);
        var timeEnd = LegacyTime.ToStored(req.TimeEnd, offset);
        if (timeStart > timeEnd) timeEnd = timeEnd.AddDays(1);
        // Mesmo valor gravado pelo legado: a data ao meio-dia (local), deslocada pelo offset.
        var orderDate = LegacyTime.ToStored(req.OrderDate.ToDateTime(new TimeOnly(12, 0)), offset);

        // Reenvio da mesma OS (duplo clique, retry de rede): devolve a existente.
        var existing = await db.Orders.AsNoTracking().FirstOrDefaultAsync(o =>
            o.CustomerId == customer.Id && o.ExecutionUserId == req.TechnicianId && o.VisitId == req.VisitId &&
            o.OrderDate == orderDate && o.TimeStart == timeStart && o.TimeEnd == timeEnd, ct);
        if (existing is not null) return existing;

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await InsertAsync(req, customer, offset, orderDate, timeStart, timeEnd, ct);
            }
            catch (DbUpdateException e) when (IsUniqueViolation(e) && attempt < MaxAttempts)
            {
                // Outra OS pegou o mesmo número ao mesmo tempo: tenta de novo com o próximo.
                db.ChangeTracker.Clear();
            }
        }
    }

    private async Task<Order> InsertAsync(OrderCreateRequest req, Customer customer, int offset, DateTime orderDate,
        DateTime timeStart, DateTime timeEnd, CancellationToken ct)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            var now = clock.Now;

            var order = new Order
            {
                CustomerId = customer.Id,
                VisitId = req.VisitId,
                ExecutionUserId = req.TechnicianId,
                OrderSequence = await NextSequenceAsync(customer, ct),
                OrderDate = orderDate,
                TimeStart = timeStart,
                TimeEnd = timeEnd,
                OffsetDate = offset,
                ServiceExecuted = req.ServiceExecuted,
                ReasonOfNonExecution = Limit(req.ReasonOfNonExecution, 500),
                ResponsibleName = Limit(req.ResponsibleName, 500),
                ResponsibleBusiness = Limit(req.ResponsibleBusiness, 500),
                ResponsibleSignature = Text.Clean(req.ResponsibleSignature),
                Observations = req.Observations?.Trim() ?? "",
                Active = true,
                CreatedDate = now,
            };
            db.Orders.Add(order);
            await db.SaveChangesAsync(ct);

            // Visita pendente vira "realizada" com check-in no início da OS (como no legado).
            if (req.VisitId != 0)
            {
                var visit = await db.Visits.FirstOrDefaultAsync(v => v.Id == req.VisitId && v.Active && v.CheckinDate == null, ct);
                if (visit is not null)
                {
                    visit.CheckinDate = timeStart;
                    visit.Active = false;
                    visit.LastUpdate = now;
                }
            }

            foreach (var data in req.Evidences ?? [])
                db.OrderEvidences.Add(new OrderEvidence { OrderId = order.Id, Data = data, CreatedDate = now });

            foreach (var n in req.NonConformities ?? [])
                db.OrderNonConformities.Add(new OrderNonConformity
                {
                    OrderId = order.Id, Place = n.Place?.Trim(), Description = n.Description?.Trim(), Suggestion = n.Suggestion?.Trim(), CreatedDate = now,
                });

            var f = req.Details;
            var detail = new OrderDetail
            {
                OrderId = order.Id,
                HadEvidenceOfPlague = f.HadEvidenceOfPlague,
                HadEquipmentRevision = f.HadEquipmentRevision,
                HadBaiting = f.HadBaiting,
                HadPlaquesOfGlue = f.HadPlaquesOfGlue,
                HadLuminousTrap = f.HadLuminousTrap,
                CustomerNeedPlagueAccount = f.CustomerNeedPlagueAccount,
                HadPheromoneTrap = f.HadPheromoneTrap,
                HadProductConsumption = f.HadProductConsumption,
                CreatedDate = now,
            };
            foreach (var g in req.PlaquesOfGlue ?? [])
                detail.PlaquesOfGlue.Add(new OrderDetailsPlaquesOfGlue { Type = g.Type, Point = g.Point, Quantity = g.Quantity, RodentQuantity = g.RodentQuantity });
            foreach (var p in req.Pheromones ?? [])
                detail.Pheromones.Add(new OrderDetailsPheromone { Type = p.Type ?? "", Point = p.Point ?? "", Quantity = p.Quantity });
            foreach (var l in req.Luminous ?? [])
            {
                var lum = new OrderDetailsLuminous { Point = l.Point ?? "", Quantity = l.Quantity };
                foreach (var c in l.PlagueCounts ?? [])
                    lum.Plagues.Add(new LuminousPlaquePlague { PlagueId = c.PlagueId, Quantity = c.Quantity });
                detail.Luminous.Add(lum);
            }
            foreach (var p in req.Plagues ?? [])
                detail.Plagues.Add(new OrderDetailsPlague
                {
                    PlagueId = p.PlagueId, PlagueStatusId = p.PlagueStatusId, PlagueLocationDescription = p.Location, Quantity = p.Quantity,
                });
            foreach (var b in req.Baitings ?? [])
                detail.Baitings.Add(new OrderDetailsBaiting { Type = b.Type ?? "", Point = b.Point ?? "", Quantity = b.Quantity });
            foreach (var c in req.Consumptions ?? [])
                detail.Consumptions.Add(new OrderConsumption
                {
                    PlagueProductId = c.PlagueProductId, ProductApplicationId = c.ProductApplicationId, Quantity = c.Quantity,
                    Lot = c.Lot?.Trim() ?? "", ExpirationDate = Limit(c.ExpirationDate, 10), CreatedDate = now,
                });
            db.OrderDetails.Add(detail);

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return order;
        });
    }

    /// <summary>Próximo número da OS: por cliente ou por empresa, conforme a configuração do tenant.</summary>
    private async Task<long> NextSequenceAsync(Customer customer, CancellationToken ct)
    {
        var byCustomer = tenant.Tenant.Settings.OrderSequenceByCustomer;
        if (db.Database.IsSqlServer())
        {
            // UPDLOCK/HOLDLOCK serializa o cálculo entre requisições simultâneas.
            var sql = byCustomer
                ? "SELECT ISNULL(MAX(order_sequence), 0) AS [Value] FROM Orders WITH (UPDLOCK, HOLDLOCK) WHERE idcustomer = {0}"
                : "SELECT ISNULL(MAX(o.order_sequence), 0) AS [Value] FROM Orders o WITH (UPDLOCK, HOLDLOCK) JOIN Customers c ON c.idcustomer = o.idcustomer WHERE c.idcompany = {0}";
            var max = await db.Database.SqlQueryRaw<long>(sql, byCustomer ? customer.Id : customer.CompanyId).SingleAsync(ct);
            return max + 1;
        }

        var query = byCustomer
            ? db.Orders.Where(o => o.CustomerId == customer.Id)
            : db.Orders.Where(o => o.Customer!.CompanyId == customer.CompanyId);
        return (await query.MaxAsync(o => o.OrderSequence, ct) ?? 0) + 1;
    }

    private static void Validate(OrderCreateRequest req)
    {
        if (!req.ServiceExecuted && string.IsNullOrWhiteSpace(req.ReasonOfNonExecution))
            throw new BusinessException("Informe o motivo da não execução do serviço.");
        if (req.TimeEnd == req.TimeStart) throw new BusinessException("Informe os horários de início e término.");
        if ((req.Evidences ?? []).Any(e => !e.StartsWith("data:image/", StringComparison.Ordinal) || e.Length > MaxEvidenceLength))
            throw new BusinessException("As evidências devem ser imagens de até 2 MB.");
        if ((req.Consumptions ?? []).Any(c => c.Quantity < 0)) throw new BusinessException("Quantidade de produto inválida.");
    }

    /// <summary>Confere pragas, status, produtos e aplicações referenciados (evita erro de chave estrangeira).</summary>
    private async Task ValidateReferencesAsync(OrderCreateRequest req, CancellationToken ct)
    {
        var plagueIds = (req.Plagues ?? []).Select(p => p.PlagueId)
            .Concat((req.Luminous ?? []).SelectMany(l => l.PlagueCounts ?? []).Select(c => c.PlagueId))
            .Distinct().ToList();
        if (plagueIds.Count > 0 && await db.Plagues.CountAsync(p => plagueIds.Contains(p.Id), ct) != plagueIds.Count)
            throw new BusinessException("Há pragas inválidas na OS.");

        var statusIds = (req.Plagues ?? []).Select(p => p.PlagueStatusId).Distinct().ToList();
        if (statusIds.Count > 0 && await db.PlagueStatuses.CountAsync(s => statusIds.Contains(s.Id), ct) != statusIds.Count)
            throw new BusinessException("Há status de praga inválidos na OS.");

        var plagueProductIds = (req.Consumptions ?? []).Select(c => c.PlagueProductId).Distinct().ToList();
        if (plagueProductIds.Count > 0 && await db.PlagueProducts.CountAsync(p => plagueProductIds.Contains(p.Id), ct) != plagueProductIds.Count)
            throw new BusinessException("Há produtos inválidos no consumo.");

        var applicationIds = (req.Consumptions ?? []).Select(c => c.ProductApplicationId).Distinct().ToList();
        if (applicationIds.Count > 0 && await db.ProductApplications.CountAsync(a => applicationIds.Contains(a.Id), ct) != applicationIds.Count)
            throw new BusinessException("Há formas de aplicação inválidas no consumo.");
    }

    private static string? Limit(string? value, int max)
    {
        var v = Text.Clean(value);
        return v is null || v.Length <= max ? v : v[..max];
    }

    private static bool IsUniqueViolation(DbUpdateException e) =>
        e.GetBaseException() is SqlException { Number: 2627 or 2601 };
}
