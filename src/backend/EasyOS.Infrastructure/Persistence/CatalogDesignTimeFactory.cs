using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace EasyOS.Infrastructure.Persistence;

/// <summary>Usado apenas pelo dotnet-ef para gerar migrations do catálogo.</summary>
public sealed class CatalogDesignTimeFactory : IDesignTimeDbContextFactory<CatalogDbContext>
{
    public CatalogDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<CatalogDbContext>().UseSqlite("Data Source=design-time.db").Options);
}
