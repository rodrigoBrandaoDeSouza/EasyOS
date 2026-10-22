using EasyOS.Domain.Platform;
using EasyOS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EasyOS.Api.Platform;

public sealed class PlatformOptions
{
    /// <summary>Primeiro usuário do painel, criado apenas se o catálogo não tiver nenhum.</summary>
    public BootstrapAdminOptions? BootstrapAdmin { get; set; }
}

public sealed class BootstrapAdminOptions
{
    public string? Email { get; set; }
    public string? Password { get; set; }
    public string Name { get; set; } = "Administrador EasyOS";
}

public static class CatalogInitializer
{
    public static async Task InitializeCatalogAsync(this IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<CatalogDbContext>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<CatalogDbContext>>();
        await using var db = await factory.CreateDbContextAsync();

        var dataSource = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(db.Database.GetConnectionString()).DataSource;
        var dir = Path.GetDirectoryName(Path.GetFullPath(dataSource));
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        await db.Database.MigrateAsync();

        var bootstrap = scope.ServiceProvider.GetRequiredService<IOptions<PlatformOptions>>().Value.BootstrapAdmin;
        if (await db.PlatformUsers.AnyAsync() || string.IsNullOrWhiteSpace(bootstrap?.Email) || string.IsNullOrEmpty(bootstrap.Password))
            return;

        var user = new PlatformUser { Email = bootstrap.Email.Trim().ToLowerInvariant(), Name = bootstrap.Name };
        user.PasswordHash = new PasswordHasher<PlatformUser>().HashPassword(user, bootstrap.Password);
        db.PlatformUsers.Add(user);
        await db.SaveChangesAsync();
        logger.LogWarning("Usuário inicial do painel criado: {Email}. Troque a senha e remova Platform:BootstrapAdmin da configuração.", user.Email);
    }
}
