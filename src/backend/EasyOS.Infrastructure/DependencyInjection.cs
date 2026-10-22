using EasyOS.Infrastructure.Email;
using EasyOS.Infrastructure.Geocoding;
using EasyOS.Infrastructure.MultiTenancy;
using EasyOS.Infrastructure.Persistence;
using EasyOS.Infrastructure.Security;
using EasyOS.Infrastructure.Storage;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EasyOS.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddEasyOSInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        // Chaves do Data Protection: cifram cookies de sessão e os segredos do catálogo.
        // Precisam ficar em volume persistente e entrar no backup junto com o catálogo.
        var keysPath = config["DataProtection:KeysPath"] ?? "data/keys";
        services.AddDataProtection()
            .SetApplicationName("EasyOS")
            .PersistKeysToFileSystem(new DirectoryInfo(keysPath));
        services.AddSingleton<ISecretProtector, SecretProtector>();

        // Catálogo central em SQLite: custo zero e suficiente para algumas centenas de tenants.
        var catalogCs = config.GetConnectionString("Catalog") ?? "Data Source=data/catalog.db";
        services.AddDbContextFactory<CatalogDbContext>(o => o.UseSqlite(catalogCs));

        services.AddMemoryCache();
        services.AddSingleton<ITenantStore, CatalogTenantStore>();
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());

        // Banco do tenant: a connection string vem do tenant resolvido na requisição.
        // Sem pooling de DbContext, pois as opções variam por tenant. O pool de conexões do
        // SqlClient continua funcionando (um pool por connection string).
        var tenantDb = new TenantDatabaseFactory(config["MultiTenancy:TenantDatabaseProvider"] ?? TenantDatabaseFactory.SqlServer);
        services.AddSingleton(tenantDb);
        services.AddDbContext<TenantDbContext>((sp, o) =>
            tenantDb.Configure(o, sp.GetRequiredService<ITenantContext>().Tenant.ConnectionString));

        var storage = config.GetSection("Storage").Get<StorageOptions>() ?? new StorageOptions();
        services.AddSingleton<IObjectStorage>(storage.Provider.Equals("S3", StringComparison.OrdinalIgnoreCase)
            ? new S3ObjectStorage(storage)
            : new LocalObjectStorage(storage.LocalRoot));
        services.AddScoped<TenantFiles>();

        services.Configure<DefaultSmtpOptions>(config.GetSection("Email:DefaultSmtp"));
        services.AddScoped<IEmailSender, TenantEmailSender>();

        services.Configure<GeocodingOptions>(config.GetSection("Geocoding"));
        services.AddHttpClient<IGeocoder, Geocoder>(c => c.Timeout = TimeSpan.FromSeconds(8));

        var tzId = config["App:StorageTimeZone"] ?? "UTC";
        services.AddSingleton<IClock>(new SystemClock(TimeZoneInfo.FindSystemTimeZoneById(tzId)));

        return services;
    }
}
