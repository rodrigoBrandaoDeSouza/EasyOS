using EasyOS.Api.Auth;

namespace EasyOS.Api.Features.Reports;

public static class ReportEndpoints
{
    public static void MapReportEndpoints(this IEndpointRouteBuilder app)
    {
        var customer = app.MapGroup("/api/customers/{customerId:int}").WithTags("Relatórios").RequireAuthorization(Policies.TenantUser);

        customer.MapGet("/dashboard", async (int customerId, DateOnly? from, DateOnly? to, ReportService reports, CancellationToken ct) =>
        {
            var (start, end) = reports.DefaultRange(from, to);
            return await reports.CustomerDashboardAsync(customerId, start, end, ct);
        });

        customer.MapGet("/reports/non-conformities", async (int customerId, DateOnly? from, DateOnly? to, ReportService reports, CancellationToken ct) =>
        {
            var (start, end) = reports.DefaultRange(from, to);
            return await reports.NonConformitiesAsync(customerId, start, end, ct);
        });

        customer.MapGet("/reports/{type}", async (int customerId, string type, DateOnly? from, DateOnly? to, ReportService reports, CancellationToken ct) =>
        {
            var (start, end) = reports.DefaultRange(from, to);
            return await reports.CustomerReportAsync(customerId, type, start, end, ct);
        });

        var company = app.MapGroup("/api").WithTags("Relatórios").RequireAuthorization(Policies.Company);

        company.MapGet("/dashboard/company", (DateOnly? from, DateOnly? to, ReportService reports, CancellationToken ct) =>
            reports.CompanyDashboardAsync(from, to, ct));

        company.MapGet("/reports/productivity", async (DateOnly? from, DateOnly? to, ReportService reports, Infrastructure.IClock clock, CancellationToken ct) =>
        {
            // Padrão do legado: do início do mês até hoje.
            var today = DateOnly.FromDateTime(clock.Now);
            var (start, end) = reports.DefaultRange(from ?? new DateOnly(today.Year, today.Month, 1), to ?? today);
            return await reports.ProductivityAsync(start, end, ct);
        });
    }
}
