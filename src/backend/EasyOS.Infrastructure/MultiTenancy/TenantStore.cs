using EasyOS.Domain.Platform;
using EasyOS.Infrastructure.Persistence;
using EasyOS.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace EasyOS.Infrastructure.MultiTenancy;

public interface ITenantStore
{
    Task<TenantInfo?> FindByHostAsync(string host, CancellationToken ct = default);
    Task<TenantInfo?> FindByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Descarta o cache de um tenant após alteração no painel.</summary>
    void Invalidate(Guid tenantId);
}

/// <summary>Lê tenants do catálogo com cache em memória (50 tenants cabem folgadamente).</summary>
public sealed class CatalogTenantStore(
    IDbContextFactory<CatalogDbContext> catalogFactory,
    IMemoryCache cache,
    ISecretProtector protector) : ITenantStore
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);

    // Versão global do cache: Invalidate() a incrementa e as entradas antigas deixam de ser usadas.
    private static long _generation;

    public async Task<TenantInfo?> FindByHostAsync(string host, CancellationToken ct = default)
    {
        var normalized = NormalizeHost(host);
        if (normalized.Length == 0) return null;

        var key = $"tenant:host:{Interlocked.Read(ref _generation)}:{normalized}";
        if (cache.TryGetValue(key, out TenantInfo? cached)) return cached;

        await using var db = await catalogFactory.CreateDbContextAsync(ct);
        var tenantId = await db.TenantDomains
            .Where(d => d.Host == normalized)
            .Select(d => (Guid?)d.TenantId)
            .FirstOrDefaultAsync(ct);

        var info = tenantId is null ? null : await LoadAsync(db, tenantId.Value, ct);
        // Hosts desconhecidos também ficam em cache (por menos tempo) para não martelar o catálogo.
        cache.Set(key, info, info is null ? TimeSpan.FromSeconds(30) : Ttl);
        return info;
    }

    public async Task<TenantInfo?> FindByIdAsync(Guid id, CancellationToken ct = default)
    {
        var key = $"tenant:id:{Interlocked.Read(ref _generation)}:{id}";
        if (cache.TryGetValue(key, out TenantInfo? cached)) return cached;

        await using var db = await catalogFactory.CreateDbContextAsync(ct);
        var info = await LoadAsync(db, id, ct);
        if (info is not null) cache.Set(key, info, Ttl);
        return info;
    }

    public void Invalidate(Guid tenantId) => Interlocked.Increment(ref _generation);

    public static string NormalizeHost(string host)
    {
        var h = host.Trim().ToLowerInvariant();
        var colon = h.LastIndexOf(':');
        if (colon > 0 && !h.EndsWith(']')) h = h[..colon];
        return h.TrimEnd('.');
    }

    private async Task<TenantInfo?> LoadAsync(CatalogDbContext db, Guid id, CancellationToken ct)
    {
        var tenant = await db.Tenants.AsNoTracking()
            .Include(t => t.Domains)
            .FirstOrDefaultAsync(t => t.Id == id, ct);
        if (tenant is null) return null;

        return new TenantInfo(
            tenant.Id,
            tenant.Slug,
            tenant.Name,
            tenant.Active,
            protector.Unprotect(tenant.ConnectionStringProtected),
            tenant.Domains.OrderByDescending(d => d.IsPrimary).Select(d => d.Host).FirstOrDefault(),
            tenant.Branding,
            tenant.Settings);
    }
}
