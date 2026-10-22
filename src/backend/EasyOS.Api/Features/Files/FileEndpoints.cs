using EasyOS.Api.Auth;
using EasyOS.Api.Common;
using EasyOS.Infrastructure.Storage;
using Microsoft.AspNetCore.Mvc;

namespace EasyOS.Api.Features.Files;

public sealed record FolderItem(string Name, string Path);
public sealed record FileItem(string Name, string Path, long Size, DateTime LastModifiedUtc);
public sealed record FolderListing(string Path, IReadOnlyList<FolderItem> Folders, IReadOnlyList<FileItem> Files);
public sealed record CreateFolderRequest(string? Path, string Name);

/// <summary>
/// Área de arquivos do cliente EasyOS (antigo "Downloads"/customer_files): documentos da empresa
/// visíveis a todos os usuários do tenant. Antes os arquivos eram enviados por FTP; agora o
/// administrador da empresa envia e organiza pela própria tela.
/// </summary>
public static class FileEndpoints
{
    private const string Root = TenantFiles.CustomerFilesFolder;
    // Objeto vazio que representa uma pasta criada sem arquivos (object storage não tem pastas).
    private const string FolderMarker = ".pasta";
    private const long MaxFileBytes = 50L * 1024 * 1024;

    public static void MapFileEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/files").WithTags("Arquivos");
        group.MapGet("/", List).RequireAuthorization(Policies.TenantUser);
        group.MapGet("/download", Download).RequireAuthorization(Policies.TenantUser);
        group.MapPost("/upload", Upload).RequireAuthorization(Policies.CompanyAdmin).DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(MaxFileBytes * 5));
        group.MapPost("/folder", CreateFolder).RequireAuthorization(Policies.CompanyAdmin);
        group.MapDelete("/", Delete).RequireAuthorization(Policies.CompanyAdmin);
    }

    private static async Task<FolderListing> List(string? path, TenantFiles files, CancellationToken ct)
    {
        var folder = Normalize(path);
        var prefix = Combine(Root, folder);
        var all = await files.ListAsync(prefix, ct);

        var folders = new SortedDictionary<string, FolderItem>(StringComparer.CurrentCultureIgnoreCase);
        var fileItems = new List<FileItem>();
        foreach (var item in all)
        {
            var relative = item.Key[(prefix.Length + 1)..];
            var slash = relative.IndexOf('/');
            if (slash >= 0)
            {
                var name = relative[..slash];
                folders.TryAdd(name, new FolderItem(name, Combine(folder, name)));
            }
            else if (relative != FolderMarker)
            {
                fileItems.Add(new FileItem(relative, Combine(folder, relative), item.Size, item.LastModifiedUtc));
            }
        }
        return new FolderListing(folder, folders.Values.ToList(), fileItems.OrderBy(f => f.Name, StringComparer.CurrentCultureIgnoreCase).ToList());
    }

    private static async Task<IResult> Download(string path, TenantFiles files, CancellationToken ct)
    {
        var relative = Normalize(path);
        if (relative.Length == 0) throw new NotFoundException("Arquivo não encontrado.");
        var content = await files.GetAsync(Combine(Root, relative), ct) ?? throw new NotFoundException("Arquivo não encontrado.");
        var name = relative[(relative.LastIndexOf('/') + 1)..];
        // Sempre como anexo: impede que HTML/SVG enviados sejam renderizados no domínio do sistema.
        return Results.File(content.Content, "application/octet-stream", name, enableRangeProcessing: true);
    }

    private static async Task<IResult> Upload(string? path, IFormFileCollection uploaded, TenantFiles files, CancellationToken ct)
    {
        if (uploaded.Count == 0) throw new BusinessException("Selecione ao menos um arquivo.");
        var folder = Normalize(path);
        foreach (var file in uploaded)
        {
            if (file.Length > MaxFileBytes) throw new BusinessException($"O arquivo {file.FileName} passa de 50 MB.");
            var name = SafeName(file.FileName);
            await using var stream = file.OpenReadStream();
            await files.PutAsync(Combine(Root, folder, name), stream, StorageKeys.ContentTypeFor(name), ct);
        }
        return Results.NoContent();
    }

    private static async Task<IResult> CreateFolder(CreateFolderRequest req, TenantFiles files, CancellationToken ct)
    {
        var name = SafeName(req.Name);
        await files.PutAsync(Combine(Root, Normalize(req.Path), name, FolderMarker), Stream.Null, "application/octet-stream", ct);
        return Results.NoContent();
    }

    /// <summary>Remove um arquivo ou uma pasta inteira.</summary>
    private static async Task<IResult> Delete(string path, TenantFiles files, CancellationToken ct)
    {
        var relative = Normalize(path);
        if (relative.Length == 0) throw new BusinessException("Não é possível remover a pasta raiz.");
        var key = Combine(Root, relative);
        var inside = await files.ListAsync(key, ct);
        foreach (var item in inside) await files.DeleteAsync(item.Key, ct);
        await files.DeleteAsync(key, ct);
        return Results.NoContent();
    }

    private static string Normalize(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return "";
        try
        {
            return StorageKeys.SanitizeRelativePath(path);
        }
        catch (ArgumentException)
        {
            throw new BusinessException("Caminho inválido.");
        }
    }

    private static string SafeName(string? name)
    {
        var clean = Path.GetFileName((name ?? "").Replace('\\', '/')).Trim();
        if (clean.Length == 0 || clean is "." or ".." || clean == FolderMarker || clean.Any(c => char.IsControl(c) || Path.GetInvalidFileNameChars().Contains(c)))
            throw new BusinessException("Nome inválido.");
        return clean.Length > 200 ? clean[..200] : clean;
    }

    private static string Combine(params string[] parts) => string.Join('/', parts.Where(p => p.Length > 0));
}
