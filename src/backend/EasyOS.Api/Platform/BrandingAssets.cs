using EasyOS.Domain.Platform;

namespace EasyOS.Api.Platform;

/// <summary>Arquivos de identidade visual de cada tenant, guardados em tenants/{slug}/branding/.</summary>
public static class BrandingAssets
{
    public const string Logo = "logo";
    public const string LoginBackground = "login-background";
    public const string Favicon = "favicon";
    public const string ReportLogo = "report-logo";

    public static readonly string[] All = [Logo, LoginBackground, Favicon, ReportLogo];

    public static readonly string[] AllowedExtensions = [".png", ".jpg", ".jpeg", ".svg", ".ico", ".webp", ".gif"];

    public const long MaxBytes = 2 * 1024 * 1024;

    public static string KeyFor(string slug, string asset, string extension) => $"tenants/{slug}/branding/{asset}{extension}";

    public static string? GetKey(TenantBranding branding, string asset) => asset switch
    {
        Logo => branding.LogoKey,
        LoginBackground => branding.LoginBackgroundKey,
        Favicon => branding.FaviconKey,
        ReportLogo => branding.ReportLogoKey,
        _ => null,
    };

    public static void SetKey(TenantBranding branding, string asset, string? key)
    {
        switch (asset)
        {
            case Logo: branding.LogoKey = key; break;
            case LoginBackground: branding.LoginBackgroundKey = key; break;
            case Favicon: branding.FaviconKey = key; break;
            case ReportLogo: branding.ReportLogoKey = key; break;
            default: throw new ArgumentOutOfRangeException(nameof(asset));
        }
    }
}
