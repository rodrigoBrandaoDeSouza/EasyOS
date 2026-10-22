using EasyOS.Api.Common;
using EasyOS.Api.Features.Catalog;
using EasyOS.Api.Features.Companies;
using EasyOS.Api.Features.Customers;
using EasyOS.Api.Features.Tenant;
using EasyOS.Api.Features.Users;
using EasyOS.Api.Features.Visits;
using EasyOS.Api.Features.Orders;
using EasyOS.Api.Features.Reports;
using EasyOS.Api.Features.MonthlyComments;
using EasyOS.Api.Features.Files;
using EasyOS.Api.Features.Inea;

namespace EasyOS.Api;

/// <summary>Registro dos módulos funcionais (um arquivo de endpoints por área do sistema).</summary>
public static class FeatureRegistration
{
    public static IServiceCollection AddEasyOSFeatures(this IServiceCollection services)
    {
        services.AddScoped<Access>();
        services.AddScoped<OrderQueries>();
        services.AddScoped<OrderCreation>();
        services.AddScoped<ReportService>();
        return services;
    }

    public static void MapEasyOSFeatures(this IEndpointRouteBuilder app)
    {
        app.MapTenantEndpoints();
        app.MapCompanyEndpoints();
        app.MapCompanyUserEndpoints();
        app.MapCustomerEndpoints();
        app.MapCustomerUserEndpoints();
        app.MapCatalogEndpoints();
        app.MapVisitEndpoints();
        app.MapOrderEndpoints();
        app.MapReportEndpoints();
        app.MapMonthlyCommentEndpoints();
        app.MapFileEndpoints();
        app.MapIneaEndpoints();
    }
}
