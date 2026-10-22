using EasyOS.Api.MultiTenancy;
using EasyOS.Infrastructure.MultiTenancy;
using Microsoft.Extensions.Options;

namespace EasyOS.Api.Platform;

public static class InternalEndpoints
{
    public static void MapInternalEndpoints(this IEndpointRouteBuilder app)
    {
        // Chamado pelo Caddy (on-demand TLS) antes de emitir certificado para um domínio:
        // só domínios cadastrados recebem certificado. O Caddyfile bloqueia /internal/ externamente.
        app.MapGet("/internal/tls-check", async (string? domain, ITenantStore store, IOptions<MultiTenancyOptions> options) =>
        {
            if (string.IsNullOrWhiteSpace(domain)) return Results.BadRequest();
            var host = CatalogTenantStore.NormalizeHost(domain);
            if (options.Value.PlatformHosts.Contains(host, StringComparer.OrdinalIgnoreCase)) return Results.Ok();
            var tenant = await store.FindByHostAsync(host);
            return tenant is not null ? Results.Ok() : Results.NotFound();
        }).AllowAnonymous().ExcludeFromDescription();
    }
}
