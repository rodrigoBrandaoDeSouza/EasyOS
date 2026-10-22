using System.Globalization;
using System.Security.Claims;
using EasyOS.Api.MultiTenancy;
using EasyOS.Domain.Entities;
using EasyOS.Infrastructure;
using EasyOS.Infrastructure.MultiTenancy;
using EasyOS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

namespace EasyOS.Api.Auth;

public static class AuthSchemes
{
    public const string Tenant = "EasyOS.Tenant";
    public const string Platform = "EasyOS.Platform";
}

public static class AuthSetup
{
    public static IServiceCollection AddEasyOSAuth(this IServiceCollection services, IHostEnvironment env)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<CurrentUser>();
        services.AddScoped<TenantCookieEvents>();
        services.AddScoped<PlatformCookieEvents>();

        services.AddAuthentication(AuthSchemes.Tenant)
            .AddCookie(AuthSchemes.Tenant, o =>
            {
                o.Cookie.Name = "easyos_session";
                ConfigureCookie(o, env);
                // Mesmo comportamento do legado: sessão expira após 2h sem uso.
                o.ExpireTimeSpan = TimeSpan.FromHours(2);
                o.EventsType = typeof(TenantCookieEvents);
            })
            .AddCookie(AuthSchemes.Platform, o =>
            {
                o.Cookie.Name = "easyos_platform";
                ConfigureCookie(o, env);
                o.ExpireTimeSpan = TimeSpan.FromHours(8);
                o.EventsType = typeof(PlatformCookieEvents);
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(Policies.TenantUser, p => p
                .AddAuthenticationSchemes(AuthSchemes.Tenant)
                .RequireAuthenticatedUser())
            .AddPolicy(Policies.Company, p => p
                .AddAuthenticationSchemes(AuthSchemes.Tenant)
                .RequireClaim(EasyClaims.UserType, UserTypes.Company))
            .AddPolicy(Policies.CompanyAdmin, p => p
                .AddAuthenticationSchemes(AuthSchemes.Tenant)
                .RequireClaim(EasyClaims.UserType, UserTypes.Company)
                .RequireClaim(EasyClaims.Profile, ((int)CompanyProfile.Administrator).ToString(CultureInfo.InvariantCulture)))
            .AddPolicy(Policies.CustomerAdmin, p => p
                .AddAuthenticationSchemes(AuthSchemes.Tenant)
                .RequireAssertion(c =>
                    c.User.HasClaim(EasyClaims.UserType, UserTypes.Company) ||
                    (c.User.HasClaim(EasyClaims.UserType, UserTypes.Customer) &&
                     c.User.HasClaim(EasyClaims.Profile, ((int)CustomerProfile.BackOffice).ToString(CultureInfo.InvariantCulture)))))
            .AddPolicy(Policies.Platform, p => p
                .AddAuthenticationSchemes(AuthSchemes.Platform)
                .RequireAuthenticatedUser());

        return services;
    }

    private static void ConfigureCookie(CookieAuthenticationOptions o, IHostEnvironment env)
    {
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Strict;
        // Em produção o Caddy termina o TLS e repassa X-Forwarded-Proto: o cookie sai sempre Secure.
        o.Cookie.SecurePolicy = env.IsProduction() ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
        o.SlidingExpiration = true;
    }

    public static string PasswordStamp(string passwordHash) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(passwordHash.Trim().ToUpperInvariant())))[..16];
}

/// <summary>
/// Garante que o cookie só vale no tenant que o emitiu e revalida periodicamente o usuário no banco
/// (desativado ou senha trocada encerra a sessão).
/// </summary>
public sealed class TenantCookieEvents(ITenantContext tenantContext, IServiceProvider services, IClock clock) : ApiCookieEvents
{
    private static readonly TimeSpan RevalidateEvery = TimeSpan.FromMinutes(5);

    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var principal = context.Principal!;
        var tenant = tenantContext.Current;
        if (tenant is null || context.HttpContext.IsPlatformRequest() ||
            principal.FindFirstValue(EasyClaims.TenantId) != tenant.Id.ToString())
        {
            await Reject(context);
            return;
        }

        var validatedAt = long.TryParse(principal.FindFirstValue(EasyClaims.ValidatedAt), out var ticks) ? new DateTime(ticks, DateTimeKind.Utc) : DateTime.MinValue;
        if (clock.UtcNow - validatedAt < RevalidateEvery) return;

        // Resolvido só aqui: o DbContext do tenant exige um tenant definido.
        var db = services.GetRequiredService<TenantDbContext>();
        var userId = int.Parse(principal.FindFirstValue(EasyClaims.UserId)!);
        var stamp = principal.FindFirstValue(EasyClaims.PasswordStamp);
        var stillValid = principal.FindFirstValue(EasyClaims.UserType) == UserTypes.Company
            ? await db.CompanyUsers.AsNoTracking().Where(u => u.Id == userId && u.Active).Select(u => u.Password).FirstOrDefaultAsync() is { } p1 && AuthSetup.PasswordStamp(p1) == stamp
            : await db.CustomerUsers.AsNoTracking().Where(u => u.Id == userId && u.Active && u.Customer!.Active).Select(u => u.Password).FirstOrDefaultAsync() is { } p2 && AuthSetup.PasswordStamp(p2) == stamp;

        if (!stillValid)
        {
            await Reject(context);
            return;
        }

        var identity = (ClaimsIdentity)principal.Identity!;
        identity.RemoveClaim(identity.FindFirst(EasyClaims.ValidatedAt));
        identity.AddClaim(new Claim(EasyClaims.ValidatedAt, clock.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture)));
        context.ShouldRenew = true;
    }

    private static async Task Reject(CookieValidatePrincipalContext context)
    {
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(AuthSchemes.Tenant);
    }
}

/// <summary>API: responde 401/403 em vez de redirecionar para uma página de login.</summary>
public abstract class ApiCookieEvents : CookieAuthenticationEvents
{
    public override Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    }

    public override Task RedirectToAccessDenied(RedirectContext<CookieAuthenticationOptions> context)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    }
}

/// <summary>O cookie do painel só é aceito nos hosts do painel.</summary>
public sealed class PlatformCookieEvents : ApiCookieEvents
{
    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        if (!context.HttpContext.IsPlatformRequest())
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(AuthSchemes.Platform);
        }
    }
}
