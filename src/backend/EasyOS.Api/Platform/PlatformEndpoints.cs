using System.Security.Claims;
using System.Text.RegularExpressions;
using EasyOS.Api.Auth;
using EasyOS.Api.Common;
using EasyOS.Api.MultiTenancy;
using EasyOS.Domain.Platform;
using EasyOS.Infrastructure.MultiTenancy;
using EasyOS.Infrastructure.Persistence;
using EasyOS.Infrastructure.Security;
using EasyOS.Infrastructure.Storage;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EasyOS.Api.Platform;

public sealed record PlatformLoginRequest(string Email, string Password);
public sealed record PlatformMeResponse(Guid Id, string Name, string Email);

public sealed record TenantListItem(Guid Id, string Slug, string Name, bool Active, string? PrimaryHost, IReadOnlyList<string> Domains, DateTime UpdatedAt);

public sealed record TenantDomainDto(string Host, bool IsPrimary);

public sealed record SmtpDto(string? Host, int Port, bool EnableSsl, string? UserName, string? FromAddress, string? FromName, string? Subject, bool HasPassword);

public sealed record TenantSettingsDto(
    bool OrderSequenceByCustomer, bool CertificateDateOnVisit, bool DayVisitsOnly, bool TravelTime, bool LockTime,
    SmtpDto Smtp, IReadOnlyList<TenantFeature> Features);

public sealed record TenantBrandingDto(
    string PrimaryColor, string HeadingColor, string OtherPlaguesLabel,
    bool HasLogo, bool HasLoginBackground, bool HasFavicon, bool HasReportLogo);

public sealed record TenantDetail(
    Guid Id, string Slug, string Name, bool Active, string DatabaseSummary,
    IReadOnlyList<TenantDomainDto> Domains, TenantBrandingDto Branding, TenantSettingsDto Settings);

public sealed record TenantUpsertRequest(
    string Slug, string Name, bool Active,
    // Obrigatória na criação; na edição, vazio mantém a atual.
    string? ConnectionString,
    IReadOnlyList<TenantDomainDto> Domains,
    string PrimaryColor, string HeadingColor, string OtherPlaguesLabel,
    bool OrderSequenceByCustomer, bool CertificateDateOnVisit, bool DayVisitsOnly, bool TravelTime, bool LockTime,
    SmtpDto Smtp,
    // null mantém a senha SMTP atual; string vazia remove.
    string? SmtpPassword,
    IReadOnlyList<TenantFeature>? Features);

public sealed record TestConnectionRequest(string? ConnectionString);
public sealed record TestConnectionResponse(bool Ok, string Message);

public sealed record PlatformUserDto(Guid Id, string Name, string Email, bool Active, DateTime CreatedAt);
public sealed record PlatformUserCreateRequest(string Name, string Email, string Password);
public sealed record PlatformUserUpdateRequest(string Name, bool Active, string? Password);

public static partial class PlatformEndpoints
{
    public static void MapPlatformEndpoints(this IEndpointRouteBuilder app)
    {
        var root = app.MapGroup("/api/platform").WithTags("Painel EasyOS")
            .AddEndpointFilter(async (ctx, next) =>
                ctx.HttpContext.IsPlatformRequest() ? await next(ctx) : Results.NotFound());

        root.MapPost("/auth/login", Login).AllowAnonymous().RequireRateLimiting("login");
        root.MapPost("/auth/logout", async (HttpContext http) =>
        {
            await http.SignOutAsync(AuthSchemes.Platform);
            return Results.NoContent();
        }).AllowAnonymous();
        root.MapGet("/auth/me", (ClaimsPrincipal user) => new PlatformMeResponse(
            Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!),
            user.FindFirstValue(ClaimTypes.Name)!,
            user.FindFirstValue(ClaimTypes.Email)!)).RequireAuthorization(Policies.Platform);

        var tenants = root.MapGroup("/tenants").RequireAuthorization(Policies.Platform);
        tenants.MapGet("/", ListTenants);
        tenants.MapGet("/{id:guid}", GetTenant);
        tenants.MapPost("/", CreateTenant);
        tenants.MapPut("/{id:guid}", UpdateTenant);
        tenants.MapPost("/test-connection", TestNewConnection);
        tenants.MapPost("/{id:guid}/test-connection", TestTenantConnection);
        tenants.MapPut("/{id:guid}/branding/{asset}", UploadBrandingAsset).DisableAntiforgery();
        tenants.MapDelete("/{id:guid}/branding/{asset}", DeleteBrandingAsset);
        tenants.MapGet("/{id:guid}/branding/{asset}", GetBrandingAsset);

        var users = root.MapGroup("/users").RequireAuthorization(Policies.Platform);
        users.MapGet("/", async (CatalogDbContext db) =>
            await db.PlatformUsers.AsNoTracking().OrderBy(u => u.Name)
                .Select(u => new PlatformUserDto(u.Id, u.Name, u.Email, u.Active, u.CreatedAt)).ToListAsync());
        users.MapPost("/", CreatePlatformUser);
        users.MapPut("/{id:guid}", UpdatePlatformUser);
    }

    private static async Task<IResult> Login(PlatformLoginRequest req, HttpContext http, CatalogDbContext db)
    {
        var email = (req.Email ?? "").Trim().ToLowerInvariant();
        var user = await db.PlatformUsers.FirstOrDefaultAsync(u => u.Email == email && u.Active);
        var hasher = new PasswordHasher<PlatformUser>();
        if (user is null || hasher.VerifyHashedPassword(user, user.PasswordHash, req.Password ?? "") == PasswordVerificationResult.Failed)
            throw new BusinessException("Usuário ou senha inválidos.");

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Name),
            new(ClaimTypes.Email, user.Email),
        };
        await http.SignInAsync(AuthSchemes.Platform, new ClaimsPrincipal(new ClaimsIdentity(claims, AuthSchemes.Platform)));
        return Results.Ok(new PlatformMeResponse(user.Id, user.Name, user.Email));
    }

    private static async Task<List<TenantListItem>> ListTenants(CatalogDbContext db)
    {
        var tenants = await db.Tenants.AsNoTracking().Include(t => t.Domains).OrderBy(t => t.Name).ToListAsync();
        return tenants.Select(t => new TenantListItem(
            t.Id, t.Slug, t.Name, t.Active,
            t.Domains.OrderByDescending(d => d.IsPrimary).Select(d => d.Host).FirstOrDefault(),
            t.Domains.Select(d => d.Host).OrderBy(h => h).ToList(),
            t.UpdatedAt)).ToList();
    }

    private static async Task<TenantDetail> GetTenant(Guid id, CatalogDbContext db, ISecretProtector protector)
    {
        var t = await db.Tenants.AsNoTracking().Include(x => x.Domains).FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException();
        return ToDetail(t, protector);
    }

    private static async Task<IResult> CreateTenant(
        TenantUpsertRequest req, CatalogDbContext db, ISecretProtector protector, TenantDatabaseFactory tenantDb, ITenantStore store)
    {
        if (string.IsNullOrWhiteSpace(req.ConnectionString))
            throw new BusinessException("Informe a connection string do banco do cliente.");

        var (ok, message) = await tenantDb.TestAsync(req.ConnectionString);
        if (!ok) throw new BusinessException(message);

        var tenant = new Tenant();
        await Apply(tenant, req, db, protector, isNew: true);
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
        store.Invalidate(tenant.Id);
        return Results.Created($"/api/platform/tenants/{tenant.Id}", ToDetail(tenant, protector));
    }

    private static async Task<TenantDetail> UpdateTenant(
        Guid id, TenantUpsertRequest req, CatalogDbContext db, ISecretProtector protector, TenantDatabaseFactory tenantDb, ITenantStore store)
    {
        var tenant = await db.Tenants.Include(t => t.Domains).FirstOrDefaultAsync(t => t.Id == id) ?? throw new NotFoundException();
        if (!string.IsNullOrWhiteSpace(req.ConnectionString))
        {
            var (ok, message) = await tenantDb.TestAsync(req.ConnectionString);
            if (!ok) throw new BusinessException(message);
        }

        await Apply(tenant, req, db, protector, isNew: false);
        await db.SaveChangesAsync();
        store.Invalidate(tenant.Id);
        return ToDetail(tenant, protector);
    }

    private static async Task Apply(Tenant tenant, TenantUpsertRequest req, CatalogDbContext db, ISecretProtector protector, bool isNew)
    {
        var slug = (req.Slug ?? "").Trim().ToLowerInvariant();
        if (!SlugRegex().IsMatch(slug))
            throw new BusinessException("Identificador inválido: use letras minúsculas, números e hífen (3 a 60 caracteres).");
        if (!isNew && slug != tenant.Slug)
            throw new BusinessException("O identificador não pode ser alterado (ele define a pasta de arquivos do cliente).");
        if (isNew && await db.Tenants.AnyAsync(t => t.Slug == slug))
            throw new BusinessException("Já existe um cliente com este identificador.");
        if (string.IsNullOrWhiteSpace(req.Name)) throw new BusinessException("Informe o nome do cliente.");

        var hosts = (req.Domains ?? []).Select(d => d with { Host = CatalogTenantStore.NormalizeHost(d.Host ?? "") })
            .Where(d => d.Host.Length > 0).DistinctBy(d => d.Host).ToList();
        if (hosts.Count == 0) throw new BusinessException("Informe ao menos um domínio.");
        if (hosts.Any(h => !HostRegex().IsMatch(h.Host))) throw new BusinessException("Há domínios em formato inválido.");
        var hostNames = hosts.Select(h => h.Host).ToList();
        var taken = await db.TenantDomains.Where(d => hostNames.Contains(d.Host) && d.TenantId != tenant.Id).Select(d => d.Host).ToListAsync();
        if (taken.Count > 0) throw new BusinessException($"Domínio já usado por outro cliente: {string.Join(", ", taken)}.");

        tenant.Slug = slug;
        tenant.Name = req.Name.Trim();
        tenant.Active = req.Active;
        if (!string.IsNullOrWhiteSpace(req.ConnectionString))
            tenant.ConnectionStringProtected = protector.Protect(req.ConnectionString.Trim());

        tenant.Domains.RemoveAll(d => !hostNames.Contains(d.Host));
        foreach (var h in hosts)
        {
            var existing = tenant.Domains.FirstOrDefault(d => d.Host == h.Host);
            if (existing is null) tenant.Domains.Add(new TenantDomain { Host = h.Host, IsPrimary = h.IsPrimary });
            else existing.IsPrimary = h.IsPrimary;
        }
        if (!tenant.Domains.Any(d => d.IsPrimary)) tenant.Domains[0].IsPrimary = true;

        tenant.Branding.PrimaryColor = ValidColor(req.PrimaryColor, "#37ab8c");
        tenant.Branding.HeadingColor = ValidColor(req.HeadingColor, "#333333");
        tenant.Branding.OtherPlaguesLabel = string.IsNullOrWhiteSpace(req.OtherPlaguesLabel) ? "Outros" : req.OtherPlaguesLabel.Trim();

        var s = tenant.Settings;
        s.OrderSequenceByCustomer = req.OrderSequenceByCustomer;
        s.CertificateDateOnVisit = req.CertificateDateOnVisit;
        s.DayVisitsOnly = req.DayVisitsOnly;
        s.TravelTime = req.TravelTime;
        s.LockTime = req.LockTime;
        s.Smtp.Host = NullIfEmpty(req.Smtp?.Host);
        s.Smtp.Port = req.Smtp?.Port is > 0 ? req.Smtp.Port : 587;
        s.Smtp.EnableSsl = req.Smtp?.EnableSsl ?? true;
        s.Smtp.UserName = NullIfEmpty(req.Smtp?.UserName);
        s.Smtp.FromAddress = NullIfEmpty(req.Smtp?.FromAddress);
        s.Smtp.FromName = NullIfEmpty(req.Smtp?.FromName);
        s.Smtp.Subject = NullIfEmpty(req.Smtp?.Subject);
        if (req.SmtpPassword is not null)
            s.Smtp.PasswordProtected = req.SmtpPassword.Length == 0 ? null : protector.Protect(req.SmtpPassword);
        s.Features = (req.Features ?? [])
            .Where(f => !string.IsNullOrWhiteSpace(f.Key))
            .Select(f => new TenantFeature
            {
                Key = f.Key.Trim().ToUpperInvariant() is var k && k.StartsWith("CFG_") ? k : "CFG_" + k,
                Value = f.Value ?? "",
            })
            .DistinctBy(f => f.Key).ToList();

        tenant.UpdatedAt = DateTime.UtcNow;
    }

    private static async Task<TestConnectionResponse> TestNewConnection(TestConnectionRequest req, TenantDatabaseFactory tenantDb)
    {
        if (string.IsNullOrWhiteSpace(req.ConnectionString)) throw new BusinessException("Informe a connection string.");
        var (ok, message) = await tenantDb.TestAsync(req.ConnectionString);
        return new TestConnectionResponse(ok, message);
    }

    private static async Task<TestConnectionResponse> TestTenantConnection(Guid id, CatalogDbContext db, ISecretProtector protector, TenantDatabaseFactory tenantDb)
    {
        var tenant = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id) ?? throw new NotFoundException();
        var (ok, message) = await tenantDb.TestAsync(protector.Unprotect(tenant.ConnectionStringProtected));
        return new TestConnectionResponse(ok, message);
    }

    private static async Task<IResult> UploadBrandingAsset(
        Guid id, string asset, IFormFile file, CatalogDbContext db, IObjectStorage storage, ITenantStore store)
    {
        if (!BrandingAssets.All.Contains(asset)) throw new NotFoundException();
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!BrandingAssets.AllowedExtensions.Contains(ext)) throw new BusinessException("Formato de imagem não suportado.");
        if (file.Length is 0 or > BrandingAssets.MaxBytes) throw new BusinessException("A imagem deve ter até 2 MB.");
        if (asset == BrandingAssets.ReportLogo && ext is not (".png" or ".jpg" or ".jpeg"))
            throw new BusinessException("O logo dos relatórios em PDF deve ser PNG ou JPG.");

        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == id) ?? throw new NotFoundException();
        var oldKey = BrandingAssets.GetKey(tenant.Branding, asset);
        var key = BrandingAssets.KeyFor(tenant.Slug, asset, ext);

        await using (var stream = file.OpenReadStream())
            await storage.PutAsync(key, stream, StorageKeys.ContentTypeFor(file.FileName));
        if (oldKey is not null && oldKey != key) await storage.DeleteAsync(oldKey);

        BrandingAssets.SetKey(tenant.Branding, asset, key);
        tenant.UpdatedAt = DateTime.UtcNow;
        // O EF não detecta mudança dentro do JSON owned por referência; marca explicitamente.
        db.Entry(tenant).Reference(t => t.Branding).TargetEntry!.State = EntityState.Modified;
        await db.SaveChangesAsync();
        store.Invalidate(tenant.Id);
        return Results.NoContent();
    }

    private static async Task<IResult> DeleteBrandingAsset(Guid id, string asset, CatalogDbContext db, IObjectStorage storage, ITenantStore store)
    {
        if (!BrandingAssets.All.Contains(asset)) throw new NotFoundException();
        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == id) ?? throw new NotFoundException();
        var key = BrandingAssets.GetKey(tenant.Branding, asset);
        if (key is not null) await storage.DeleteAsync(key);
        BrandingAssets.SetKey(tenant.Branding, asset, null);
        tenant.UpdatedAt = DateTime.UtcNow;
        db.Entry(tenant).Reference(t => t.Branding).TargetEntry!.State = EntityState.Modified;
        await db.SaveChangesAsync();
        store.Invalidate(tenant.Id);
        return Results.NoContent();
    }

    private static async Task<IResult> GetBrandingAsset(Guid id, string asset, CatalogDbContext db, IObjectStorage storage, HttpContext http)
    {
        var tenant = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id) ?? throw new NotFoundException();
        var key = BrandingAssets.GetKey(tenant.Branding, asset);
        var content = key is null ? null : await storage.GetAsync(key);
        if (content is null) return Results.NotFound();
        AssetHeaders.Apply(http);
        return Results.Stream(content.Content, content.ContentType);
    }

    private static async Task<IResult> CreatePlatformUser(PlatformUserCreateRequest req, CatalogDbContext db)
    {
        var email = (req.Email ?? "").Trim().ToLowerInvariant();
        if (!email.Contains('@')) throw new BusinessException("E-mail inválido.");
        if (string.IsNullOrWhiteSpace(req.Name)) throw new BusinessException("Informe o nome.");
        if ((req.Password ?? "").Length < 10) throw new BusinessException("A senha deve ter pelo menos 10 caracteres.");
        if (await db.PlatformUsers.AnyAsync(u => u.Email == email)) throw new BusinessException("Já existe um usuário com este e-mail.");

        var user = new PlatformUser { Email = email, Name = req.Name.Trim() };
        user.PasswordHash = new PasswordHasher<PlatformUser>().HashPassword(user, req.Password!);
        db.PlatformUsers.Add(user);
        await db.SaveChangesAsync();
        return Results.Created($"/api/platform/users/{user.Id}", new PlatformUserDto(user.Id, user.Name, user.Email, user.Active, user.CreatedAt));
    }

    private static async Task<PlatformUserDto> UpdatePlatformUser(Guid id, PlatformUserUpdateRequest req, CatalogDbContext db, ClaimsPrincipal me)
    {
        var user = await db.PlatformUsers.FirstOrDefaultAsync(u => u.Id == id) ?? throw new NotFoundException();
        if (!req.Active && me.FindFirstValue(ClaimTypes.NameIdentifier) == id.ToString())
            throw new BusinessException("Você não pode desativar o próprio usuário.");
        if (string.IsNullOrWhiteSpace(req.Name)) throw new BusinessException("Informe o nome.");

        user.Name = req.Name.Trim();
        user.Active = req.Active;
        if (!string.IsNullOrEmpty(req.Password))
        {
            if (req.Password.Length < 10) throw new BusinessException("A senha deve ter pelo menos 10 caracteres.");
            user.PasswordHash = new PasswordHasher<PlatformUser>().HashPassword(user, req.Password);
        }
        await db.SaveChangesAsync();
        return new PlatformUserDto(user.Id, user.Name, user.Email, user.Active, user.CreatedAt);
    }

    private static TenantDetail ToDetail(Tenant t, ISecretProtector protector)
    {
        var smtp = t.Settings.Smtp;
        return new TenantDetail(
            t.Id, t.Slug, t.Name, t.Active, DescribeDatabase(t, protector),
            t.Domains.OrderByDescending(d => d.IsPrimary).ThenBy(d => d.Host).Select(d => new TenantDomainDto(d.Host, d.IsPrimary)).ToList(),
            new TenantBrandingDto(t.Branding.PrimaryColor, t.Branding.HeadingColor, t.Branding.OtherPlaguesLabel,
                t.Branding.LogoKey is not null, t.Branding.LoginBackgroundKey is not null, t.Branding.FaviconKey is not null, t.Branding.ReportLogoKey is not null),
            new TenantSettingsDto(t.Settings.OrderSequenceByCustomer, t.Settings.CertificateDateOnVisit, t.Settings.DayVisitsOnly, t.Settings.TravelTime, t.Settings.LockTime,
                new SmtpDto(smtp.Host, smtp.Port, smtp.EnableSsl, smtp.UserName, smtp.FromAddress, smtp.FromName, smtp.Subject, smtp.PasswordProtected is not null),
                t.Settings.Features));
    }

    /// <summary>Mostra servidor e banco sem expor usuário e senha.</summary>
    private static string DescribeDatabase(Tenant t, ISecretProtector protector)
    {
        try
        {
            var csb = new System.Data.Common.DbConnectionStringBuilder { ConnectionString = protector.Unprotect(t.ConnectionStringProtected) };
            string? Get(params string[] keys) => keys.Select(k => csb.TryGetValue(k, out var v) ? v?.ToString() : null).FirstOrDefault(v => v is not null);
            var server = Get("Server", "Data Source", "Address", "Addr") ?? "?";
            var database = Get("Initial Catalog", "Database");
            return database is null ? server : $"{server} / {database}";
        }
        catch
        {
            return "(não foi possível ler a connection string)";
        }
    }

    private static string ValidColor(string? color, string fallback) =>
        color is not null && ColorRegex().IsMatch(color.Trim()) ? color.Trim().ToLowerInvariant() : fallback;

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    [GeneratedRegex("^[a-z0-9](?:[a-z0-9-]{1,58}[a-z0-9])$")]
    private static partial Regex SlugRegex();

    [GeneratedRegex(@"^(?=.{1,253}$)(?:[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?\.)*[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?$")]
    private static partial Regex HostRegex();

    [GeneratedRegex("^#[0-9a-fA-F]{6}$")]
    private static partial Regex ColorRegex();
}

public static class AssetHeaders
{
    /// <summary>Impede que um SVG enviado como logo execute script se aberto diretamente.</summary>
    public static void Apply(HttpContext http)
    {
        http.Response.Headers.ContentSecurityPolicy = "default-src 'none'; style-src 'unsafe-inline'; sandbox";
        http.Response.Headers.XContentTypeOptions = "nosniff";
        http.Response.Headers.CacheControl = "public, max-age=300";
    }
}
