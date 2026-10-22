using EasyOS.Infrastructure.MultiTenancy;

namespace EasyOS.Infrastructure.Storage;

/// <summary>
/// Acesso ao storage restrito ao prefixo do tenant atual (tenants/{slug}/...).
/// Nenhuma chave consegue sair do prefixo do tenant.
/// </summary>
public sealed class TenantFiles(IObjectStorage storage, ITenantContext tenantContext)
{
    public const string CustomerFilesFolder = "customer_files";
    public const string BrandingFolder = "branding";

    private string Root => $"tenants/{tenantContext.Tenant.Slug}";

    public string KeyFor(string relativePath) => $"{Root}/{StorageKeys.SanitizeRelativePath(relativePath)}";

    /// <summary>Converte uma chave absoluta do storage em caminho relativo ao tenant.</summary>
    public string ToRelative(string key) => key.StartsWith(Root + "/", StringComparison.Ordinal) ? key[(Root.Length + 1)..] : key;

    public Task PutAsync(string relativePath, Stream content, string contentType, CancellationToken ct = default) =>
        storage.PutAsync(KeyFor(relativePath), content, contentType, ct);

    public Task<ObjectContent?> GetAsync(string relativePath, CancellationToken ct = default) =>
        storage.GetAsync(KeyFor(relativePath), ct);

    public Task DeleteAsync(string relativePath, CancellationToken ct = default) =>
        storage.DeleteAsync(KeyFor(relativePath), ct);

    public async Task<IReadOnlyList<StoredObject>> ListAsync(string relativeFolder, CancellationToken ct = default)
    {
        var items = await storage.ListAsync(KeyFor(relativeFolder), ct);
        return items.Select(i => i with { Key = ToRelative(i.Key) }).ToList();
    }
}
