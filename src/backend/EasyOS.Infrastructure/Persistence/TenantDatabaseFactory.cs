using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace EasyOS.Infrastructure.Persistence;

/// <summary>
/// Configura e cria o <see cref="TenantDbContext"/> para uma connection string.
/// Produção usa SQL Server; "Sqlite" existe para testes automatizados e demonstração local.
/// </summary>
public sealed class TenantDatabaseFactory(string provider)
{
    public const string SqlServer = "SqlServer";
    public const string Sqlite = "Sqlite";

    public string Provider { get; } = provider;

    public void Configure(DbContextOptionsBuilder options, string connectionString)
    {
        if (Provider.Equals(Sqlite, StringComparison.OrdinalIgnoreCase))
        {
            options.UseSqlite(connectionString);
            return;
        }

        options.UseSqlServer(connectionString, sql =>
        {
            sql.EnableRetryOnFailure(maxRetryCount: 3);
            sql.CommandTimeout(60);
            // Bancos legados podem estar em compatibility level antigo (sem OPENJSON).
            sql.UseCompatibilityLevel(120);
        });
    }

    public TenantDbContext Create(string connectionString)
    {
        var builder = new DbContextOptionsBuilder<TenantDbContext>();
        Configure(builder, connectionString);
        return new TenantDbContext(builder.Options);
    }

    /// <summary>Confere se a connection string abre e aponta para um banco EasyOS (tabela Companies).</summary>
    public async Task<(bool Ok, string Message)> TestAsync(string connectionString, CancellationToken ct = default)
    {
        try
        {
            if (!Provider.Equals(Sqlite, StringComparison.OrdinalIgnoreCase))
            {
                // Falha rápido em vez de esperar o timeout padrão de 15s com retry.
                var csb = new SqlConnectionStringBuilder(connectionString) { ConnectTimeout = 10 };
                connectionString = csb.ConnectionString;
            }
            await using var db = Create(connectionString);
            var missing = await MissingColumnsAsync(db, ct);
            if (missing.Count > 0)
                return (false, "Conectou, mas o esquema do banco está incompleto. Faltam: " + string.Join(", ", missing.Take(30)) + (missing.Count > 30 ? "…" : ""));

            var companies = await db.Companies.CountAsync(ct);
            var users = await db.CompanyUsers.CountAsync(ct);
            return (true, $"Conexão OK: {companies} empresa(s) e {users} usuário(s) encontrados.");
        }
        catch (Exception e)
        {
            return (false, $"Falha ao conectar: {e.GetBaseException().Message}");
        }
    }

    /// <summary>
    /// Compara as tabelas/colunas mapeadas com as que existem no banco (SQL Server). Evita colocar no ar
    /// um cliente cujo banco esteja com o esquema atrasado em relação aos demais.
    /// </summary>
    public static async Task<List<string>> MissingColumnsAsync(TenantDbContext db, CancellationToken ct = default)
    {
        if (!db.Database.IsSqlServer()) return [];

        var existing = (await db.Database
                .SqlQueryRaw<SchemaColumn>("SELECT TABLE_NAME AS TableName, COLUMN_NAME AS ColumnName FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'dbo'")
                .ToListAsync(ct))
            .Select(c => $"{c.TableName}.{c.ColumnName}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missing = new List<string>();
        foreach (var entity in db.Model.GetEntityTypes())
        {
            var table = entity.GetTableName();
            if (table is null) continue;
            var store = Microsoft.EntityFrameworkCore.Metadata.StoreObjectIdentifier.Table(table, entity.GetSchema());
            foreach (var property in entity.GetProperties())
            {
                var column = property.GetColumnName(store);
                if (column is not null && !existing.Contains($"{table}.{column}")) missing.Add($"{table}.{column}");
            }
        }
        return missing;
    }

    private sealed record SchemaColumn(string TableName, string ColumnName);
}
