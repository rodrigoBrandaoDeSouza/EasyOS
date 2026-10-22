using EasyOS.Api.Common;
using EasyOS.Domain.Entities;
using EasyOS.Infrastructure.MultiTenancy;
using EasyOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EasyOS.Api.Features.Orders;

/// <summary>Montagem da OS completa e do certificado (usada pela tela, pelos PDFs e pelo e-mail).</summary>
public sealed class OrderQueries(TenantDbContext db, Access access, ITenantContext tenant)
{
    public async Task<OrderDetailDto> GetDetailAsync(int orderId, CancellationToken ct)
    {
        var order = await access.Orders().Where(o => o.Id == orderId)
            .Include(o => o.Customer)
            .Include(o => o.ExecutionUser)

            .Include(o => o.NonConformities)
            .AsSplitQuery()
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException("Ordem de serviço não encontrada.");

        var customer = order.Customer!;
        var visit = order.VisitId == 0 ? null : await db.Visits.AsNoTracking().FirstOrDefaultAsync(v => v.Id == order.VisitId, ct);
        var company = await db.Companies.AsNoTracking().FirstAsync(c => c.Id == customer.CompanyId, ct);
        var technicalManager = await TechnicalManagerFor(customer, ct);

        var detail = await db.OrderDetails.AsNoTracking().Where(d => d.OrderId == orderId)
            .Include(d => d.Plagues).ThenInclude(p => p.Plague)
            .Include(d => d.Plagues).ThenInclude(p => p.PlagueStatus)
            .Include(d => d.Baitings)
            .Include(d => d.PlaquesOfGlue)
            .Include(d => d.Pheromones)
            .Include(d => d.Luminous).ThenInclude(l => l.Plagues)
            .Include(d => d.Consumptions).ThenInclude(c => c.PlagueProduct!).ThenInclude(pp => pp.Plague)
            .Include(d => d.Consumptions).ThenInclude(c => c.PlagueProduct!).ThenInclude(pp => pp.Product!).ThenInclude(p => p.ProductType)
            .Include(d => d.Consumptions).ThenInclude(c => c.ProductApplication)
            .AsSplitQuery()
            .FirstOrDefaultAsync(ct);

        var evidenceIds = await db.OrderEvidences.AsNoTracking().Where(e => e.OrderId == orderId).OrderBy(e => e.Id).Select(e => e.Id).ToListAsync(ct);
        var hasResponsibleSignature = await db.Orders.AsNoTracking()
            .Where(o => o.Id == orderId).Select(o => o.ResponsibleSignature != null).FirstAsync(ct);
        var hasTechnicianSignature = !string.IsNullOrEmpty(order.ExecutionUser?.Signature);

        var offset = order.OffsetMinutes;
        var visitOffset = visit?.OffsetMinutes ?? offset;

        return new OrderDetailDto(
            order.Id, order.OrderSequence ?? 0, order.Active,
            order.OrderDate, LegacyTime.ToLocal(order.TimeStart, offset), LegacyTime.ToLocal(order.TimeEnd, offset),
            LegacyTime.ToLocal(visit?.CheckinDate, visitOffset), offset,
            order.VisitId, visit?.Description, visit?.TreatedAreas,
            order.ServiceExecuted, order.ReasonOfNonExecution, order.Observations,
            order.ResponsibleName, order.ResponsibleBusiness, hasResponsibleSignature,
            order.ExecutionUserId, order.ExecutionUser?.Name ?? "", hasTechnicianSignature, technicalManager?.Name,
            ToCustomerInfo(customer), ToCompanyInfo(company),
            detail is null
                ? new OrderDetailsFlags(false, false, false, false, false, false, false, false)
                : new OrderDetailsFlags(detail.HadEvidenceOfPlague, detail.HadEquipmentRevision, detail.HadBaiting, detail.HadPlaquesOfGlue,
                    detail.HadLuminousTrap, detail.CustomerNeedPlagueAccount, detail.HadPheromoneTrap, detail.HadProductConsumption),
            (detail?.Plagues ?? []).Select(p => new PlagueFoundItem(p.PlagueId, p.Plague?.Name ?? "", p.PlagueStatusId, p.PlagueStatus?.Description ?? "",
                p.PlagueLocationDescription, p.Quantity)).ToList(),
            (detail?.Baitings ?? []).Select(b => new TrapItem(b.Type, b.Point, b.Quantity)).ToList(),
            (detail?.PlaquesOfGlue ?? []).Select(g => new GlueTrapItem(g.Type, g.Point, g.Quantity, g.RodentQuantity)).ToList(),
            (detail?.Pheromones ?? []).Select(p => new TrapItem(p.Type, p.Point, p.Quantity)).ToList(),
            (detail?.Luminous ?? []).Select(l => new LuminousItem(l.Point, l.Quantity,
                l.Plagues.Select(p => new LuminousPlagueCount(p.PlagueId, LuminousInsects.Label(p.PlagueId, tenant.Tenant.Branding.OtherPlaguesLabel), p.Quantity)).ToList())).ToList(),
            (detail?.Consumptions ?? []).Select(c => new ConsumptionItem(
                c.PlagueProductId, c.PlagueProduct?.Plague?.Name ?? "", c.PlagueProduct?.Product?.Name ?? "",
                c.PlagueProduct?.Product?.ProductType?.Unity ?? "", c.PlagueProduct?.Concentration ?? 0,
                c.ProductApplicationId, c.ProductApplication?.Description ?? "", c.Quantity, c.Lot, c.ExpirationDate)).ToList(),
            order.NonConformities.OrderBy(n => n.Id).Select(ToNonConformity).ToList(),
            evidenceIds);
    }

    public async Task<CertificateDto> GetCertificateAsync(int orderId, CancellationToken ct)
    {
        var order = await access.Orders().Where(o => o.Id == orderId)
            .Include(o => o.Customer).Include(o => o.ExecutionUser)
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException("Ordem de serviço não encontrada.");
        var customer = order.Customer!;
        var visit = order.VisitId == 0 ? null : await db.Visits.AsNoTracking().FirstOrDefaultAsync(v => v.Id == order.VisitId, ct);
        var company = await db.Companies.AsNoTracking().FirstAsync(c => c.Id == customer.CompanyId, ct);
        var tm = await TechnicalManagerFor(customer, ct);
        var days = ValidityDays(visit?.CertificateDate, customer.CertificateDate);

        // Produtos efetivamente usados na OS, com as pragas-alvo de cada um (como no legado).
        var productIds = await db.OrderConsumptions.AsNoTracking()
            .Where(c => db.OrderDetails.Any(d => d.Id == c.OrderDetailId && d.OrderId == orderId))
            .Select(c => c.PlagueProduct!.ProductId).Distinct().ToListAsync(ct);
        var products = await db.Products.AsNoTracking().Where(p => productIds.Contains(p.Id))
            .Include(p => p.ProductType)
            .Include(p => p.PlagueProducts.Where(pp => pp.Active)).ThenInclude(pp => pp.Plague)
            .AsSplitQuery()
            .OrderBy(p => p.Name).ToListAsync(ct);

        var plaguesFound = await db.OrderDetailsPlagues.AsNoTracking()
            .Where(p => db.OrderDetails.Any(d => d.Id == p.OrderDetailId && d.OrderId == orderId))
            .Select(p => p.Plague!.Name).Distinct().OrderBy(n => n).ToListAsync(ct);

        var offset = order.OffsetMinutes;
        return new CertificateDto(
            order.Id, order.OrderSequence ?? 0, order.OrderDate,
            LegacyTime.ToLocal(order.TimeStart, offset), LegacyTime.ToLocal(order.TimeEnd, offset),
            order.OrderDate.Date, order.OrderDate.Date.AddDays(days), days,
            order.ResponsibleName, order.ResponsibleBusiness,
            ToCustomerInfo(customer), ToCompanyInfo(company),
            order.ExecutionUser?.Name ?? "",
            tm is null ? null : new CertificateTechnicalManager(tm.State.Trim(), tm.Name, tm.Register, HasSignature: false),
            visit?.TreatedAreas,
            products.Select(p => new CertificateProduct(p.Id, p.Name, p.ChemicalGroup, p.ActiveIngredient, p.RegistrationNumber,
                p.Symptoms, p.Antidotes, p.ProductType?.Unity ?? "",
                string.Join(", ", p.PlagueProducts.Select(pp => pp.Plague?.Name).OfType<string>().OrderBy(n => n)))).ToList(),
            plaguesFound);
    }

    public async Task<List<CertificateListItem>> ListCertificatesAsync(int customerId, CancellationToken ct)
    {
        var customer = await access.CustomerAsync(customerId, ct);
        var rows = await db.Orders.AsNoTracking()
            .Where(o => o.CustomerId == customerId && o.Active && o.ServiceExecuted)
            .OrderByDescending(o => o.OrderDate)
            .Select(o => new { o.Id, o.OrderSequence, o.OrderDate, VisitDays = db.Visits.Where(v => v.Id == o.VisitId).Select(v => v.CertificateDate).FirstOrDefault() })
            .ToListAsync(ct);
        return rows.Select(r =>
        {
            var days = ValidityDays(r.VisitDays, customer.CertificateDate);
            return new CertificateListItem(r.Id, r.OrderSequence ?? 0, r.OrderDate.Date, r.OrderDate.Date.AddDays(days));
        }).ToList();
    }

    /// <summary>Validade do certificado: a da visita, se informada; senão a do cadastro do cliente.</summary>
    public static int ValidityDays(int? visitDays, int customerDays) => visitDays is > 0 ? visitDays.Value : customerDays;

    private Task<TechnicalManager?> TechnicalManagerFor(Customer customer, CancellationToken ct)
    {
        var state = customer.State.Trim();
        return db.TechnicalManagers.AsNoTracking()
            .Where(t => t.CompanyId == customer.CompanyId && t.State.Trim() == state)
            .FirstOrDefaultAsync(ct);
    }

    public static OrderCustomerInfo ToCustomerInfo(Customer c) => new(
        c.Id, c.TradingName, c.CompanyName, c.CpfCnpj, c.Address, c.Number, c.Complement, c.Neighborhood, c.City, c.State, c.Zipcode,
        c.ActivityType, c.CertificateDate);

    public static OrderCompanyInfo ToCompanyInfo(Company c) => new(
        c.Id, c.CompanyName, c.TradingName, c.CpfCnpj, c.Charter, c.CharterExpiration,
        c.Address, c.Number, c.Neighborhood, c.City, c.State, c.Zipcode, c.Phone);

    public static NonConformityItem ToNonConformity(OrderNonConformity n) =>
        new(n.Id, n.Place, n.Description, n.Suggestion, n.Responsible, n.Action, n.DueDate, n.ClosingDate, n.Status, n.IsValid);
}
