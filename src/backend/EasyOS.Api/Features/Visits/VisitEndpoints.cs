using EasyOS.Api.Auth;
using EasyOS.Api.Common;
using EasyOS.Domain.Entities;
using EasyOS.Infrastructure;
using EasyOS.Infrastructure.MultiTenancy;
using EasyOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EasyOS.Api.Features.Visits;

public static class VisitStatus
{
    public const string Pending = "pendente";
    public const string Unconfirmed = "não confirmada";
    public const string Confirmed = "confirmada";
    public const string Done = "realizada";
    public const string NotDone = "não realizada";
    public const string Canceled = "cancelada";

    /// <summary>Mesma regra de getStatusVisit do legado, comparando no mesmo referencial (horário gravado × UTC).</summary>
    public static string Of(bool active, DateTime storedVisitDate, int? executionUserId, DateTime? checkinDate, bool serviceExecuted, int orderId, DateTime utcNow)
    {
        if (active)
        {
            if (storedVisitDate < utcNow) return Pending;
            return executionUserId is null ? Unconfirmed : Confirmed;
        }
        if (checkinDate is null) return Canceled;
        return serviceExecuted || orderId == 0 ? Done : NotDone;
    }
}

public sealed record VisitListItem(
    int Id, int CustomerId, string CustomerName,
    DateTime VisitDate, DateTime? CheckinDate, int OffsetMinutes,
    int? TechnicianId, string? TechnicianName, string? TechnicianPhone,
    int ResponsibleId, string? ResponsibleName,
    string? Description, string? TreatedAreas, IReadOnlyList<string> AuxiliarTechnicians, int? CertificateDate,
    bool Active, bool HasConfirmed, bool IsLate, string Status,
    int OrderId, long? OrderSequence,
    string Address, int Number, string Complement, string Neighborhood, string City, string State, string Zipcode);

public sealed record CalendarItem(int Id, DateTime VisitDate, string CustomerName, string? TechnicianName, bool Done);

public sealed record VisitRequest(
    int CustomerId,
    int? TechnicianId,
    int ResponsibleId,
    // Horário local (de parede), sem fuso; o offset em minutos vem do navegador (ex.: −180).
    DateTime VisitDate,
    int? OffsetMinutes,
    string? Description,
    int? CertificateDate,
    // Check-in manual (apenas visitas passadas). Ao informar, a visita é encerrada como no legado.
    DateTime? CheckinDate);

public static class VisitEndpoints
{
    public static void MapVisitEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/visits").WithTags("Visitas");

        group.MapGet("/calendar", Calendar).RequireAuthorization(Policies.Company);
        group.MapGet("/", List).RequireAuthorization(Policies.TenantUser);
        group.MapGet("/upcoming", Upcoming).RequireAuthorization(Policies.TenantUser);
        group.MapGet("/pending-for-order", PendingForOrder).RequireAuthorization(Policies.Company);
        group.MapGet("/{id:int}", Get).RequireAuthorization(Policies.TenantUser);
        group.MapPost("/", Create).RequireAuthorization(Policies.Company);
        group.MapPut("/{id:int}", Update).RequireAuthorization(Policies.Company);
        group.MapDelete("/{id:int}", Delete).RequireAuthorization(Policies.Company);
    }

    /// <summary>Visitas do ano para o calendário (exceto canceladas).</summary>
    private static async Task<List<CalendarItem>> Calendar(int? year, Access access, IClock clock, CancellationToken ct)
    {
        var y = year is >= 2015 and <= 2100 ? year.Value : clock.Now.Year;
        var (from, to) = StoredRange(new DateTime(y, 1, 1), new DateTime(y, 12, 31));

        var rows = await access.Visits()
            .Where(v => !(v.CheckinDate == null && !v.Active) && v.VisitDate >= from && v.VisitDate < to)
            .Select(v => new { v.Id, v.VisitDate, v.OffsetDate, Customer = v.Customer!.TradingName, Technician = v.ExecutionUser!.Name, v.Active })
            .ToListAsync(ct);

        return rows
            .Select(r => new CalendarItem(r.Id, LegacyTime.ToLocal(r.VisitDate, r.OffsetDate ?? Visit.DefaultOffsetMinutes), r.Customer, r.Technician, !r.Active))
            .Where(r => r.VisitDate.Year == y)
            .OrderBy(r => r.VisitDate)
            .ToList();
    }

    /// <summary>
    /// Lista de visitas por período (datas locais, inclusivas). Para usuário de cliente, só as dele.
    /// Canceladas ficam de fora, como no legado.
    /// </summary>
    private static async Task<PagedResult<VisitListItem>> List(
        DateOnly? from, DateOnly? to, int? customerId, int? page, int? pageSize,
        Access access, TenantDbContext db, IClock clock, CancellationToken ct)
    {
        var start = from ?? new DateOnly(clock.Now.Year, 1, 1);
        var end = to ?? DateOnly.FromDateTime(clock.Now.AddMonths(12));
        if (end < start) throw new BusinessException("A data final deve ser maior que a inicial.");

        var query = access.Visits();
        if (customerId is not null)
        {
            await access.EnsureCustomerAsync(customerId.Value, ct);
            query = query.Where(v => v.CustomerId == customerId);
        }
        var (storedFrom, storedTo) = StoredRange(start.ToDateTime(TimeOnly.MinValue), end.ToDateTime(TimeOnly.MinValue));
        query = query.Where(v => !(v.CheckinDate == null && !v.Active) && v.VisitDate >= storedFrom && v.VisitDate < storedTo)
            .OrderBy(v => v.VisitDate);

        var paged = await Project(query, db).ToPagedAsync(page, pageSize ?? 50, ct);
        var items = await Finish(paged.Items, db, clock, ct);
        return new PagedResult<VisitListItem>(items, paged.Page, paged.PageSize, paged.TotalItems);
    }

    /// <summary>Próximas visitas ativas (dashboards). Usuário de cliente vê só as dele.</summary>
    private static async Task<List<VisitListItem>> Upcoming(int? customerId, int? take, Access access, TenantDbContext db, IClock clock, CancellationToken ct)
    {
        var query = access.Visits().Where(v => v.Active && v.VisitDate >= clock.UtcNow.Date);
        if (customerId is not null)
        {
            await access.EnsureCustomerAsync(customerId.Value, ct);
            query = query.Where(v => v.CustomerId == customerId);
        }
        var rows = await Project(query.OrderBy(v => v.VisitDate), db).Take(Math.Clamp(take ?? 10, 1, 50)).ToListAsync(ct);
        return await Finish(rows, db, clock, ct);
    }

    /// <summary>Visitas do cliente ainda sem OS e sem check-in (lançamento manual de OS pela web).</summary>
    private static async Task<List<VisitListItem>> PendingForOrder(int customerId, Access access, TenantDbContext db, IClock clock, CancellationToken ct)
    {
        await access.EnsureCustomerAsync(customerId, ct);
        var query = access.Visits()
            .Where(v => v.CustomerId == customerId && v.Active && v.CheckinDate == null && v.ExecutionUserId != null &&
                        !db.Orders.Any(o => o.VisitId == v.Id))
            .OrderBy(v => v.VisitDate);
        return await Finish(await Project(query, db).ToListAsync(ct), db, clock, ct);
    }

    private static async Task<VisitListItem> Get(int id, Access access, TenantDbContext db, IClock clock, CancellationToken ct)
    {
        var row = await Project(access.Visits().Where(v => v.Id == id), db).FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Visita não encontrada.");
        return (await Finish([row], db, clock, ct))[0];
    }

    private static async Task<IResult> Create(VisitRequest req, Access access, TenantDbContext db, ITenantContext tenant, IClock clock, CancellationToken ct)
    {
        var customer = await access.CustomerAsync(req.CustomerId, ct);
        if (!customer.Active) throw new BusinessException("Não é possível agendar visita para um cliente inativo.");
        await ValidateUsers(req, access, ct);
        ValidateCertificateDate(req, tenant);

        var offset = LegacyTime.NormalizeOffset(req.OffsetMinutes);
        var now = clock.Now;
        var visit = new Visit
        {
            CompanyId = customer.CompanyId,
            CustomerId = customer.Id,
            ExecutionUserId = req.TechnicianId,
            ResponsibleUserId = req.ResponsibleId,
            VisitDate = LegacyTime.ToStored(req.VisitDate, offset),
            OffsetDate = offset,
            Active = true,
            HasConfirmed = req.TechnicianId is not null,
            CreatedDate = now,
            LastUpdate = now,
            Description = CleanDescription(req.Description),
            CertificateDate = req.CertificateDate,
        };
        db.Visits.Add(visit);
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/visits/{visit.Id}", new { visit.Id });
    }

    private static async Task<IResult> Update(int id, VisitRequest req, Access access, TenantDbContext db, ITenantContext tenant, IClock clock, CancellationToken ct)
    {
        if (!await access.Visits().AnyAsync(v => v.Id == id, ct)) throw new NotFoundException("Visita não encontrada.");
        var visit = await db.Visits.FirstAsync(v => v.Id == id, ct);
        if (!visit.Active) throw new BusinessException("Visitas encerradas ou canceladas não podem ser alteradas.");
        if (visit.CustomerId != req.CustomerId) throw new BusinessException("O cliente da visita não pode ser alterado.");
        await ValidateUsers(req, access, ct);
        ValidateCertificateDate(req, tenant);

        var offset = LegacyTime.NormalizeOffset(req.OffsetMinutes);
        visit.ExecutionUserId = req.TechnicianId;
        visit.ResponsibleUserId = req.ResponsibleId;
        visit.HasConfirmed = req.TechnicianId is not null;
        visit.VisitDate = LegacyTime.ToStored(req.VisitDate, offset);
        visit.OffsetDate = offset;
        visit.Description = CleanDescription(req.Description);
        if (req.CertificateDate is not null) visit.CertificateDate = req.CertificateDate;
        visit.LastUpdate = clock.Now;

        if (req.CheckinDate is not null)
        {
            if (req.CheckinDate.Value > clock.UtcNow.AddMinutes(offset).AddMinutes(5))
                throw new BusinessException("A data de check-in não pode estar no futuro.");
            if (req.CheckinDate.Value.Date < req.VisitDate.Date)
                throw new BusinessException("A data de check-in não pode ser anterior à data da visita.");
            // Mesmo referencial do check-in feito pelo app (horário gravado = local − offset).
            visit.CheckinDate = LegacyTime.ToStored(req.CheckinDate.Value, offset);
            visit.Active = false;
        }

        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    /// <summary>Exclusão lógica (active = 0), só para visitas sem check-in.</summary>
    private static async Task<IResult> Delete(int id, Access access, TenantDbContext db, IClock clock, CancellationToken ct)
    {
        if (!await access.Visits().AnyAsync(v => v.Id == id, ct)) throw new NotFoundException("Visita não encontrada.");
        var visit = await db.Visits.FirstAsync(v => v.Id == id, ct);
        if (visit.CheckinDate is not null) throw new BusinessException("Visitas com check-in não podem ser excluídas.");
        if (await db.Orders.AnyAsync(o => o.VisitId == id, ct)) throw new BusinessException("Esta visita já possui ordem de serviço.");
        visit.Active = false;
        visit.LastUpdate = clock.Now;
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task ValidateUsers(VisitRequest req, Access access, CancellationToken ct)
    {
        if (!await access.CompanyUsers().AnyAsync(u => u.Id == req.ResponsibleId, ct))
            throw new BusinessException("Informe o responsável pelo agendamento.");
        if (req.TechnicianId is not null && !await access.CompanyUsers().AnyAsync(u => u.Id == req.TechnicianId && u.Active, ct))
            throw new BusinessException("Técnico inválido.");
    }

    private static void ValidateCertificateDate(VisitRequest req, ITenantContext tenant)
    {
        if (tenant.Tenant.Settings.CertificateDateOnVisit && req.CertificateDate is null)
            throw new BusinessException("Data do Certificado não foi informada.");
    }

    private static string CleanDescription(string? description)
    {
        var d = description?.Trim() ?? "";
        if (d.Length > 8000) throw new BusinessException("A descrição deve ter até 8000 caracteres.");
        return d;
    }

    /// <summary>Converte um intervalo de datas locais (inclusivo) em intervalo gravado, com folga para qualquer fuso brasileiro.</summary>
    private static (DateTime From, DateTime To) StoredRange(DateTime localFrom, DateTime localToInclusive) =>
        (localFrom.Date.AddHours(-1).AddMinutes(-Visit.DefaultOffsetMinutes), localToInclusive.Date.AddDays(1).AddHours(2).AddMinutes(-Visit.DefaultOffsetMinutes));

    private sealed record Row(
        int Id, int CustomerId, string CustomerName, DateTime VisitDate, DateTime? CheckinDate, int? OffsetDate,
        int? TechnicianId, string? TechnicianName, string? TechnicianPhone, int ResponsibleId, string? ResponsibleName,
        string? Description, string? TreatedAreas, string? AuxiliarTechnicians, int? CertificateDate,
        bool Active, bool HasConfirmed, int OrderId, long? OrderSequence, bool ServiceExecuted,
        string Address, int Number, string Complement, string Neighborhood, string City, string State, string Zipcode);

    private static IQueryable<Row> Project(IQueryable<Visit> query, TenantDbContext db) =>
        query.Select(v => new Row(
            v.Id, v.CustomerId, v.Customer!.TradingName, v.VisitDate, v.CheckinDate, v.OffsetDate,
            v.ExecutionUserId, v.ExecutionUser!.Name, v.ExecutionUser.Phone, v.ResponsibleUserId, v.ResponsibleUser!.Name,
            v.Description, v.TreatedAreas, v.AuxiliarTechnicians, v.CertificateDate,
            v.Active, v.HasConfirmed,
            db.Orders.Where(o => o.VisitId == v.Id).OrderBy(o => o.Id).Select(o => o.Id).FirstOrDefault(),
            db.Orders.Where(o => o.VisitId == v.Id).OrderBy(o => o.Id).Select(o => o.OrderSequence).FirstOrDefault(),
            db.Orders.Where(o => o.VisitId == v.Id).OrderBy(o => o.Id).Select(o => o.ServiceExecuted).FirstOrDefault(),
            v.Customer.Address, v.Customer.Number, v.Customer.Complement, v.Customer.Neighborhood, v.Customer.City, v.Customer.State, v.Customer.Zipcode));

    /// <summary>Aplica offset, calcula status e resolve os nomes dos técnicos auxiliares (gravados como ids "1;2;3").</summary>
    private static async Task<List<VisitListItem>> Finish(IReadOnlyList<Row> rows, TenantDbContext db, IClock clock, CancellationToken ct)
    {
        var auxIds = rows.SelectMany(r => ParseIds(r.AuxiliarTechnicians)).Distinct().ToList();
        var auxNames = auxIds.Count == 0
            ? new Dictionary<int, string>()
            : await db.CompanyUsers.AsNoTracking().Where(u => auxIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Name, ct);
        var utcNow = clock.UtcNow;

        return rows.Select(r =>
        {
            var offset = r.OffsetDate ?? Visit.DefaultOffsetMinutes;
            return new VisitListItem(
                r.Id, r.CustomerId, r.CustomerName,
                LegacyTime.ToLocal(r.VisitDate, offset), LegacyTime.ToLocal(r.CheckinDate, offset), offset,
                r.TechnicianId, r.TechnicianName, r.TechnicianPhone, r.ResponsibleId, r.ResponsibleName,
                r.Description, r.TreatedAreas,
                ParseIds(r.AuxiliarTechnicians).Select(id => auxNames.GetValueOrDefault(id)).OfType<string>().ToList(),
                r.CertificateDate, r.Active, r.HasConfirmed,
                IsLate: r.CheckinDate is null && r.VisitDate < utcNow,
                VisitStatus.Of(r.Active, r.VisitDate, r.TechnicianId, r.CheckinDate, r.ServiceExecuted, r.OrderId, utcNow),
                r.OrderId, r.OrderSequence,
                r.Address, r.Number, r.Complement, r.Neighborhood, r.City, r.State, r.Zipcode);
        }).ToList();
    }

    private static IEnumerable<int> ParseIds(string? ids) =>
        (ids ?? "").Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => int.TryParse(s, out var id) ? id : 0).Where(id => id > 0);
}
