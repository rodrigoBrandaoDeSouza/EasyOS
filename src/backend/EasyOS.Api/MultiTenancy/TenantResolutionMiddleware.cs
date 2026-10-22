using EasyOS.Infrastructure.MultiTenancy;
using Microsoft.Extensions.Options;

namespace EasyOS.Api.MultiTenancy;

public sealed class MultiTenancyOptions
{
    /// <summary>Hosts do painel central da EasyOS (sem tenant).</summary>
    public List<string> PlatformHosts { get; set; } = [];

    /// <summary>Mapeia um host para outro antes da busca (ex.: localhost → demo.easyos.local em desenvolvimento).</summary>
    public Dictionary<string, string> HostOverrides { get; set; } = [];
}

public static class RequestKinds
{
    public const string ItemKey = "easyos.request-kind";
    public const string Platform = "platform";
    public const string Tenant = "tenant";

    public static bool IsPlatformRequest(this HttpContext http) =>
        http.Items.TryGetValue(ItemKey, out var kind) && (string?)kind == Platform;
}

/// <summary>
/// Identifica o tenant pelo domínio da requisição e o grava no contexto da requisição.
/// Toda requisição que não é do painel central nem infraestrutura precisa pertencer a um tenant ativo.
/// </summary>
public sealed class TenantResolutionMiddleware(RequestDelegate next, IOptions<MultiTenancyOptions> options)
{
    private static readonly string[] ExemptPrefixes = ["/health", "/internal/"];

    public async Task InvokeAsync(HttpContext http, ITenantStore store, TenantContext tenantContext)
    {
        if (ExemptPrefixes.Any(p => http.Request.Path.StartsWithSegments(p.TrimEnd('/'))))
        {
            await next(http);
            return;
        }

        var host = CatalogTenantStore.NormalizeHost(http.Request.Host.Host);
        if (options.Value.HostOverrides.TryGetValue(host, out var mapped)) host = mapped;

        if (options.Value.PlatformHosts.Contains(host, StringComparer.OrdinalIgnoreCase))
        {
            http.Items[RequestKinds.ItemKey] = RequestKinds.Platform;
            await next(http);
            return;
        }

        var tenant = await store.FindByHostAsync(host, http.RequestAborted);
        if (tenant is null)
        {
            await WriteProblem(http, StatusCodes.Status404NotFound, "Domínio não cadastrado",
                "Este endereço não está associado a nenhum cliente EasyOS.");
            return;
        }
        if (!tenant.Active)
        {
            await WriteProblem(http, StatusCodes.Status403Forbidden, "Acesso suspenso",
                "O acesso deste cliente está temporariamente suspenso. Entre em contato com a EasyOS.");
            return;
        }

        tenantContext.Set(tenant);
        http.Items[RequestKinds.ItemKey] = RequestKinds.Tenant;
        await next(http);
    }

    private static Task WriteProblem(HttpContext http, int status, string title, string detail)
    {
        // Para navegação (SPA) devolvemos uma página simples; para a API, ProblemDetails.
        http.Response.StatusCode = status;
        if (!http.Request.Path.StartsWithSegments("/api"))
        {
            http.Response.ContentType = "text/html; charset=utf-8";
            return http.Response.WriteAsync(
                $"<!doctype html><meta charset=utf-8><title>{title}</title>" +
                $"<body style=\"font-family:system-ui;padding:48px;color:#333\"><h1>{title}</h1><p>{detail}</p></body>");
        }
        return Results.Problem(title: title, detail: detail, statusCode: status).ExecuteAsync(http);
    }
}
