using EasyOS.Api.Auth;
using EasyOS.Api.Common;
using EasyOS.Domain.Entities;
using EasyOS.Infrastructure;
using EasyOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EasyOS.Api.Features.MonthlyComments;

public sealed record MonthComment(int Month, string? Comment, string? Author, DateTime? CreatedAt);
public sealed record MonthlyCommentsResponse(int Year, IReadOnlyList<MonthComment> Months);
public sealed record MonthlyCommentRequest(int Year, int Month, string Comment);

/// <summary>Comentário mensal da empresa para o cliente (um por mês, só para meses encerrados, sem edição).</summary>
public static class MonthlyCommentEndpoints
{
    public static void MapMonthlyCommentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/customers/{customerId:int}/monthly-comments").WithTags("Comentários mensais");
        group.MapGet("/", List).RequireAuthorization(Policies.TenantUser);
        group.MapPost("/", Create).RequireAuthorization(Policies.Company);
    }

    private static async Task<MonthlyCommentsResponse> List(int customerId, int? year, Access access, TenantDbContext db, IClock clock, CancellationToken ct)
    {
        await access.EnsureCustomerAsync(customerId, ct);
        var y = year is >= 2015 && year <= clock.Now.Year ? year.Value : clock.Now.Year;
        var rows = await db.MonthlyComments.AsNoTracking()
            .Where(m => m.CustomerId == customerId && m.ReferenceYear == y)
            .Select(m => new { m.ReferenceMonth, m.Comment, Author = m.CompanyUser!.Name, m.CreatedDate })
            .ToListAsync(ct);

        var months = Enumerable.Range(1, 12).Select(month =>
        {
            var row = rows.FirstOrDefault(r => r.ReferenceMonth == month);
            return new MonthComment(month, row?.Comment, row?.Author, row?.CreatedDate);
        }).ToList();
        return new MonthlyCommentsResponse(y, months);
    }

    private static async Task<IResult> Create(int customerId, MonthlyCommentRequest req, Access access, TenantDbContext db, IClock clock, CancellationToken ct)
    {
        await access.EnsureCustomerAsync(customerId, ct);
        var comment = Text.Required(req.Comment, "o comentário");
        if (req.Month is < 1 or > 12) throw new BusinessException("Mês inválido.");
        var now = clock.Now;
        if (req.Year > now.Year || (req.Year == now.Year && req.Month >= now.Month))
            throw new BusinessException("Só é possível comentar meses já encerrados.");
        if (await db.MonthlyComments.AnyAsync(m => m.CustomerId == customerId && m.ReferenceYear == req.Year && m.ReferenceMonth == req.Month, ct))
            throw new BusinessException("Já existe um comentário para este mês.");

        db.MonthlyComments.Add(new MonthlyComment
        {
            CustomerId = customerId,
            CompanyUserId = access.User.Id,
            ReferenceYear = req.Year,
            ReferenceMonth = req.Month,
            Comment = comment,
            CreatedDate = now,
        });
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }
}
