using System.Net.Http.Json;
using EasyOS.Api.Common;
using EasyOS.Domain.Entities;
using EasyOS.Domain.Platform;
using EasyOS.Infrastructure.Persistence;
using EasyOS.Infrastructure.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace EasyOS.Tests.Infrastructure;

/// <summary>
/// Sobe a API inteira em memória com catálogo e bancos de tenant em SQLite (arquivos temporários).
/// Cria dois tenants (A e B) para verificar o isolamento.
/// </summary>
public sealed class TestApp : WebApplicationFactory<Program>
{
    public const string HostA = "tenant-a.test";
    public const string HostB = "tenant-b.test";
    public const string PlatformHost = "admin.test";
    public const string Password = "senha123";
    public const string PlatformPassword = "plataforma-123";

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "easyos-tests", Guid.NewGuid().ToString("N"));

    public string TenantAConnection => $"Data Source={Path.Combine(_dir, "tenant-a.db")}";
    public string TenantBConnection => $"Data Source={Path.Combine(_dir, "tenant-b.db")}";

    public Guid TenantAId { get; private set; }
    public Guid TenantBId { get; private set; }

    public TestApp()
    {
        Directory.CreateDirectory(_dir);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Catalog", $"Data Source={Path.Combine(_dir, "catalog.db")}");
        builder.UseSetting("DataProtection:KeysPath", Path.Combine(_dir, "keys"));
        builder.UseSetting("Storage:LocalRoot", Path.Combine(_dir, "storage"));
        builder.UseSetting("MultiTenancy:TenantDatabaseProvider", TenantDatabaseFactory.Sqlite);
        builder.UseSetting("MultiTenancy:PlatformHosts:0", PlatformHost);
        builder.UseSetting("Platform:BootstrapAdmin:Email", "admin@easyos.com.br");
        builder.UseSetting("Platform:BootstrapAdmin:Password", PlatformPassword);
        builder.UseSetting("Security:LoginAttemptsPerMinute", "1000");
    }

    /// <summary>Cria a aplicação e registra os dois tenants de teste.</summary>
    public async Task InitializeAsync()
    {
        _ = Server; // força o build do host (migra o catálogo)
        using var scope = Services.CreateScope();
        var catalog = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var protector = scope.ServiceProvider.GetRequiredService<ISecretProtector>();
        var factory = scope.ServiceProvider.GetRequiredService<TenantDatabaseFactory>();

        TenantAId = await AddTenant(catalog, protector, "tenant-a", "Tenant A", HostA, TenantAConnection);
        TenantBId = await AddTenant(catalog, protector, "tenant-b", "Tenant B", HostB, TenantBConnection);

        await SeedTenantDb(factory, TenantAConnection, "A");
        await SeedTenantDb(factory, TenantBConnection, "B");
    }

    public HttpClient ClientFor(string host)
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri($"http://{host}"), HandleCookies = true });
        client.DefaultRequestHeaders.Add(CsrfHeaderMiddleware.HeaderName, CsrfHeaderMiddleware.HeaderValue);
        return client;
    }

    public async Task<HttpClient> LoginAsync(string host, string email, string password = Password)
    {
        var client = ClientFor(host);
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        response.EnsureSuccessStatusCode();
        return client;
    }

    public TenantDbContext OpenTenantDb(string connectionString) =>
        Services.GetRequiredService<TenantDatabaseFactory>().Create(connectionString);

    private static async Task<Guid> AddTenant(CatalogDbContext catalog, ISecretProtector protector, string slug, string name, string host, string cs)
    {
        var tenant = new Tenant
        {
            Slug = slug,
            Name = name,
            ConnectionStringProtected = protector.Protect(cs),
            Domains = [new TenantDomain { Host = host, IsPrimary = true }],
        };
        catalog.Tenants.Add(tenant);
        await catalog.SaveChangesAsync();
        return tenant.Id;
    }

    /// <summary>
    /// Mesmos e-mails nos dois bancos, com nomes diferentes, para provar que cada domínio só enxerga o próprio banco.
    /// </summary>
    private static async Task SeedTenantDb(TenantDatabaseFactory factory, string cs, string label)
    {
        await using var db = factory.Create(cs);
        await db.Database.EnsureCreatedAsync();
        var now = DateTime.UtcNow;

        // Dados de referência iguais aos scripts do legado (insertEASYOSInicial.sql).
        string[] plagues = ["Roedores", "Baratas", "Moscas", "Formigas", "Percevejos", "Mosquitos", "Cupins", "Pragas de produtos armazenados"];
        db.Plagues.AddRange(plagues.Select((name, i) => new Plague { Name = name, PlagueType = i == 0 ? "Roedor" : "Inseto", LastUpdate = now }));
        string[] statuses = ["Individuos vivos", "Individuos mortos", "Fragmentos", "Fezes", "Marcas de gordura", "Roeduras"];
        db.PlagueStatuses.AddRange(statuses.Select(s => new PlagueStatus { Description = s }));
        db.ProductTypes.AddRange(
            new ProductType { Type = "INSETICIDA LIQUIDO", Unity = "ml" },
            new ProductType { Type = "INSETICIDA SÓLIDO", Unity = "g" },
            new ProductType { Type = "RODENTICIDA", Unity = "g" },
            new ProductType { Type = "INSUMOS", Unity = "un" });
        await db.SaveChangesAsync();

        var company = new Company
        {
            TradingName = $"Dedetizadora {label}", CompanyName = $"Dedetizadora {label} LTDA", Address = "Rua A", Number = 1,
            Complement = "", Neighborhood = "Centro", City = "São Paulo", State = "SP", Zipcode = "01000-000", Phone = "1199999999",
            ContactName = "Contato", Active = true, CreatedDate = now, LastUpdate = now, Charter = "123", CharterExpiration = now.AddYears(1),
        };
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        db.CompanyPlagues.AddRange(db.Plagues.Local.Select(p => new CompanyPlague { CompanyId = company.Id, PlagueId = p.Id, Active = true, LastUpdate = now }));

        db.CompanyUsers.AddRange(
            new CompanyUser { Name = $"Admin {label}", Email = "admin@cliente.com", Password = LegacyPasswordHasher.Hash(Password), Active = true, Department = "Adm", ProfileLevel = CompanyProfile.Administrator, CompanyId = company.Id, CreatedDate = now, LastUpdate = now },
            new CompanyUser { Name = $"Backoffice {label}", Email = "backoffice@cliente.com", Password = LegacyPasswordHasher.Hash(Password), Active = true, Department = "Adm", ProfileLevel = CompanyProfile.BackOffice, CompanyId = company.Id, CreatedDate = now, LastUpdate = now },
            new CompanyUser { Name = $"Tecnico {label}", Email = "tecnico@cliente.com", Password = LegacyPasswordHasher.Hash(Password), Active = true, Department = "Campo", ProfileLevel = CompanyProfile.Technician, CompanyId = company.Id, CreatedDate = now, LastUpdate = now },
            new CompanyUser { Name = $"Inativo {label}", Email = "inativo@cliente.com", Password = LegacyPasswordHasher.Hash(Password), Active = false, Department = "Adm", ProfileLevel = CompanyProfile.Administrator, CompanyId = company.Id, CreatedDate = now, LastUpdate = now });

        var customer = new Customer
        {
            TradingName = $"Restaurante {label}", CompanyName = $"Restaurante {label} LTDA", Address = "Rua B", Number = 2, Complement = "",
            Neighborhood = "Centro", City = "São Paulo", State = "SP", Zipcode = "01000-000", Phone = "1188888888", ContactName = "Gerente",
            Active = true, CompanyId = company.Id, CreatedDate = now, LastUpdate = now, CertificateDate = 10,
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        db.CustomerUsers.Add(new CustomerUser
        {
            Name = $"Cliente {label}", Email = "cliente@restaurante.com", Password = LegacyPasswordHasher.Hash(Password), Active = true,
            ProfileLevel = CustomerProfile.BackOffice, CustomerId = customer.Id, CreatedDate = now,
        });
        await db.SaveChangesAsync();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }
}

[CollectionDefinition(nameof(TestAppCollection))]
public sealed class TestAppCollection : ICollectionFixture<TestAppFixture>;

public sealed class TestAppFixture : IAsyncLifetime
{
    public TestApp App { get; } = new();

    public Task InitializeAsync() => App.InitializeAsync();

    public Task DisposeAsync()
    {
        App.Dispose();
        return Task.CompletedTask;
    }
}
