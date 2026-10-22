namespace EasyOS.Infrastructure.Storage;

public sealed record StoredObject(string Key, long Size, DateTime LastModifiedUtc);

public sealed record ObjectContent(Stream Content, string ContentType, long Length);

/// <summary>Armazenamento de arquivos (S3-compatível em produção: Cloudflare R2 / Backblaze B2).</summary>
public interface IObjectStorage
{
    Task PutAsync(string key, Stream content, string contentType, CancellationToken ct = default);
    Task<ObjectContent?> GetAsync(string key, CancellationToken ct = default);
    Task DeleteAsync(string key, CancellationToken ct = default);
    Task<IReadOnlyList<StoredObject>> ListAsync(string prefix, CancellationToken ct = default);
}

public sealed class StorageOptions
{
    /// <summary>"S3" ou "Local".</summary>
    public string Provider { get; set; } = "Local";

    public string LocalRoot { get; set; } = "data/storage";

    public string? ServiceUrl { get; set; }
    public string? Region { get; set; } = "auto";
    public string? Bucket { get; set; }
    public string? AccessKey { get; set; }
    public string? SecretKey { get; set; }
}

public static class StorageKeys
{
    /// <summary>Rejeita caminhos que tentem sair do prefixo (.., barras iniciais, caracteres de controle).</summary>
    public static string SanitizeRelativePath(string path)
    {
        var parts = path.Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .Where(p => p.Length > 0)
            .ToArray();

        if (parts.Any(p => p is "." or ".." || p.Any(char.IsControl)))
            throw new ArgumentException("Caminho de arquivo inválido.", nameof(path));

        return string.Join('/', parts);
    }

    public static string ContentTypeFor(string fileName) =>
        Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".svg" => "image/svg+xml",
            ".ico" => "image/x-icon",
            ".webp" => "image/webp",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".txt" => "text/plain",
            ".csv" => "text/csv",
            ".zip" => "application/zip",
            _ => "application/octet-stream",
        };
}
