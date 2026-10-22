using EasyOS.Api.Auth;
using EasyOS.Api.Common;
using EasyOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EasyOS.Api.Features.Orders;

public static class OrderEndpoints
{
    public static void MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/orders").WithTags("Ordens de serviço");

        group.MapGet("/", List).RequireAuthorization(Policies.TenantUser);
        group.MapGet("/{id:int}", (int id, OrderQueries q, CancellationToken ct) => q.GetDetailAsync(id, ct)).RequireAuthorization(Policies.TenantUser);
        group.MapGet("/{id:int}/responsible-signature", ResponsibleSignature).RequireAuthorization(Policies.TenantUser);
        group.MapGet("/{id:int}/technician-signature", TechnicianSignature).RequireAuthorization(Policies.TenantUser);
        group.MapGet("/{id:int}/evidences/{evidenceId:int}", Evidence).RequireAuthorization(Policies.TenantUser);
        group.MapGet("/{id:int}/certificate", (int id, OrderQueries q, CancellationToken ct) => q.GetCertificateAsync(id, ct)).RequireAuthorization(Policies.TenantUser);
        group.MapPost("/", Create).RequireAuthorization(Policies.Company);
        group.MapDelete("/{id:int}", Cancel).RequireAuthorization(Policies.Company);

        app.MapGet("/api/customers/{customerId:int}/certificates", (int customerId, OrderQueries q, CancellationToken ct) => q.ListCertificatesAsync(customerId, ct))
            .WithTags("Ordens de serviço").RequireAuthorization(Policies.TenantUser);
    }

    /// <summary>
    /// Lista de OS por período (data da OS), mais recentes primeiro. Inclui as canceladas (marcadas como inativas),
    /// como no legado. Usuário de cliente vê só as do próprio cliente.
    /// </summary>
    private static async Task<PagedResult<OrderListItem>> List(
        DateOnly? from, DateOnly? to, int? customerId, int? technicianId, int? page, int? pageSize,
        Access access, CancellationToken ct)
    {
        var query = access.Orders();
        if (from is not null) query = query.Where(o => o.OrderDate >= from.Value.ToDateTime(TimeOnly.MinValue));
        if (to is not null) query = query.Where(o => o.OrderDate < to.Value.AddDays(1).ToDateTime(TimeOnly.MinValue));
        if (customerId is not null)
        {
            await access.EnsureCustomerAsync(customerId.Value, ct);
            query = query.Where(o => o.CustomerId == customerId);
        }
        if (technicianId is not null && access.User.IsCompanyUser) query = query.Where(o => o.ExecutionUserId == technicianId);

        var paged = await query.OrderByDescending(o => o.TimeStart)
            .Select(o => new
            {
                o.Id, o.OrderSequence, o.OrderDate, o.TimeStart, o.TimeEnd, o.OffsetDate, o.CustomerId,
                CustomerName = o.Customer!.TradingName, o.ExecutionUserId, TechnicianName = o.ExecutionUser!.Name,
                o.ServiceExecuted, o.Active, o.Observations,
            })
            .ToPagedAsync(page, pageSize, ct);

        var items = paged.Items.Select(o =>
        {
            var offset = o.OffsetDate ?? Domain.Entities.Visit.DefaultOffsetMinutes;
            return new OrderListItem(o.Id, o.OrderSequence ?? 0, o.OrderDate, LegacyTime.ToLocal(o.TimeStart, offset), LegacyTime.ToLocal(o.TimeEnd, offset),
                o.CustomerId, o.CustomerName, o.ExecutionUserId, o.TechnicianName, o.ServiceExecuted, o.Active, o.Observations);
        }).ToList();
        return new PagedResult<OrderListItem>(items, paged.Page, paged.PageSize, paged.TotalItems);
    }

    private static async Task<ResponsibleSignatureDto> ResponsibleSignature(int id, Access access, CancellationToken ct) =>
        await access.Orders().Where(o => o.Id == id)
            .Select(o => new ResponsibleSignatureDto(o.ResponsibleName, o.ResponsibleBusiness, o.ResponsibleSignature))
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException("Ordem de serviço não encontrada.");

    private static async Task<IResult> TechnicianSignature(int id, Access access, HttpContext http, CancellationToken ct)
    {
        var signature = await access.Orders().Where(o => o.Id == id).Select(o => o.ExecutionUser!.Signature).FirstOrDefaultAsync(ct);
        return DataUrlImage.ToResult(signature, http);
    }

    /// <summary>Evidência (foto) da OS. Guardada no banco como data URL; servida como imagem para usar direto em &lt;img&gt;.</summary>
    private static async Task<IResult> Evidence(int id, int evidenceId, Access access, TenantDbContext db, HttpContext http, CancellationToken ct)
    {
        if (!await access.Orders().AnyAsync(o => o.Id == id, ct)) throw new NotFoundException("Ordem de serviço não encontrada.");
        var data = await db.OrderEvidences.AsNoTracking().Where(e => e.Id == evidenceId && e.OrderId == id).Select(e => e.Data).FirstOrDefaultAsync(ct);
        return DataUrlImage.ToResult(data, http, cacheSeconds: 86400);
    }

    private static async Task<IResult> Create(OrderCreateRequest req, OrderCreation creation, CancellationToken ct)
    {
        var order = await creation.CreateAsync(req, ct);
        return Results.Created($"/api/orders/{order.Id}", new { order.Id, Sequence = order.OrderSequence ?? 0 });
    }

    /// <summary>Cancelamento (active = 0), equivalente ao "delete" do legado.</summary>
    private static async Task<IResult> Cancel(int id, Access access, TenantDbContext db, CancellationToken ct)
    {
        if (!await access.Orders().AnyAsync(o => o.Id == id, ct)) throw new NotFoundException("Ordem de serviço não encontrada.");
        var order = await db.Orders.FirstAsync(o => o.Id == id, ct);
        order.Active = false;
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }
}

/// <summary>Converte imagens gravadas como data URL (base64) em resposta binária.</summary>
public static class DataUrlImage
{
    public static IResult ToResult(string? dataUrl, HttpContext http, int cacheSeconds = 300)
    {
        if (!TryDecode(dataUrl, out var bytes, out var contentType)) return Results.NotFound();
        http.Response.Headers.CacheControl = $"private, max-age={cacheSeconds}";
        http.Response.Headers.XContentTypeOptions = "nosniff";
        return Results.File(bytes, contentType);
    }

    public static bool TryDecode(string? dataUrl, out byte[] bytes, out string contentType)
    {
        bytes = [];
        contentType = "image/png";
        if (string.IsNullOrWhiteSpace(dataUrl)) return false;

        var base64 = dataUrl;
        var comma = dataUrl.IndexOf(',');
        if (dataUrl.StartsWith("data:", StringComparison.Ordinal) && comma > 0)
        {
            var header = dataUrl[5..comma];
            var type = header.Split(';')[0];
            // Só tipos de imagem rasterizados: SVG poderia carregar script.
            contentType = type is "image/png" or "image/jpeg" or "image/jpg" or "image/gif" or "image/webp" ? type.Replace("jpg", "jpeg") : "image/png";
            base64 = dataUrl[(comma + 1)..];
        }
        try
        {
            bytes = Convert.FromBase64String(base64.Trim());
            return bytes.Length > 0;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
