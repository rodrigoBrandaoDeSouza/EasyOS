namespace EasyOS.Infrastructure.Storage;

/// <summary>Storage em disco, para desenvolvimento e testes.</summary>
public sealed class LocalObjectStorage(string root) : IObjectStorage
{
    private readonly string _root = Path.GetFullPath(root);

    public async Task PutAsync(string key, Stream content, string contentType, CancellationToken ct = default)
    {
        var path = Resolve(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var file = File.Create(path);
        await content.CopyToAsync(file, ct);
    }

    public Task<ObjectContent?> GetAsync(string key, CancellationToken ct = default)
    {
        var path = Resolve(key);
        if (!File.Exists(path)) return Task.FromResult<ObjectContent?>(null);
        var stream = File.OpenRead(path);
        return Task.FromResult<ObjectContent?>(new ObjectContent(stream, StorageKeys.ContentTypeFor(path), stream.Length));
    }

    public Task DeleteAsync(string key, CancellationToken ct = default)
    {
        var path = Resolve(key);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<StoredObject>> ListAsync(string prefix, CancellationToken ct = default)
    {
        var dir = Resolve(prefix);
        if (!Directory.Exists(dir)) return Task.FromResult<IReadOnlyList<StoredObject>>([]);

        var items = Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)
            .Select(f => new FileInfo(f))
            .Select(f => new StoredObject(
                Path.GetRelativePath(_root, f.FullName).Replace('\\', '/'),
                f.Length,
                f.LastWriteTimeUtc))
            .ToList();
        return Task.FromResult<IReadOnlyList<StoredObject>>(items);
    }

    private string Resolve(string key)
    {
        var full = Path.GetFullPath(Path.Combine(_root, StorageKeys.SanitizeRelativePath(key)));
        if (!full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new ArgumentException("Chave fora do diretório de storage.", nameof(key));
        return full;
    }
}
