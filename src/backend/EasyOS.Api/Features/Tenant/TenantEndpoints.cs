using EasyOS.Api.Platform;
using EasyOS.Infrastructure.MultiTenancy;
using EasyOS.Infrastructure.Storage;

namespace EasyOS.Api.Features.Tenant;

public sealed record BrandingResponse(
    string Name,
    string PrimaryColor,
    string HeadingColor,
    string OtherPlaguesLabel,
    string? LogoUrl,
    string? LoginBackgroundUrl,
    string? FaviconUrl);

/// <summary>Identidade visual do tenant do domínio acessado. Pública: a tela de login precisa dela.</summary>
public static class TenantEndpoints
{
    public static void MapTenantEndpoints(this IEndpointRouteBuilder app)
    {
        // Primeira chamada do frontend: diz se o domínio é de um cliente ou do painel EasyOS.
        app.MapGet("/api/context", (HttpContext http) =>
            Results.Ok(new { kind = MultiTenancy.RequestKinds.IsPlatformRequest(http) ? "platform" : "tenant" }))
            .WithTags("Tenant").AllowAnonymous();

        var group = app.MapGroup("/api/tenant").WithTags("Tenant").AllowAnonymous();

        group.MapGet("/branding", (ITenantContext ctx) =>
        {
            var t = ctx.Tenant;
            // O sufixo de versão força o navegador a buscar a imagem nova após troca no painel.
            string? Url(string asset, string? key) => key is null ? null : $"/api/tenant/assets/{asset}?v={(uint)key.GetHashCode()}";
            return new BrandingResponse(
                t.Name,
                t.Branding.PrimaryColor,
                t.Branding.HeadingColor,
                t.Branding.OtherPlaguesLabel,
                Url(BrandingAssets.Logo, t.Branding.LogoKey),
                Url(BrandingAssets.LoginBackground, t.Branding.LoginBackgroundKey),
                Url(BrandingAssets.Favicon, t.Branding.FaviconKey));
        });

        group.MapGet("/assets/{asset}", async (string asset, ITenantContext ctx, IObjectStorage storage, HttpContext http) =>
        {
            var key = BrandingAssets.GetKey(ctx.Tenant.Branding, asset);
            var content = key is null ? null : await storage.GetAsync(key, http.RequestAborted);
            if (content is null) return Results.NotFound();
            AssetHeaders.Apply(http);
            return Results.Stream(content.Content, content.ContentType);
        });
    }
}
