using EasyOS.Domain.Platform;
using Microsoft.EntityFrameworkCore;

namespace EasyOS.Infrastructure.Persistence;

/// <summary>Banco central da plataforma: cadastro de tenants, domínios e usuários do painel EasyOS.</summary>
public class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<TenantDomain> TenantDomains => Set<TenantDomain>();
    public DbSet<PlatformUser> PlatformUsers => Set<PlatformUser>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Tenant>(e =>
        {
            e.ToTable("Tenants");
            e.HasIndex(x => x.Slug).IsUnique();
            e.Property(x => x.Slug).HasMaxLength(60);
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.ConnectionStringProtected).HasMaxLength(4000);
            e.HasMany(x => x.Domains).WithOne().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            e.OwnsOne(x => x.Branding, o => o.ToJson());
            e.OwnsOne(x => x.Settings, o =>
            {
                o.ToJson();
                o.OwnsOne(s => s.Smtp);
                o.OwnsMany(s => s.Features);
            });
        });

        b.Entity<TenantDomain>(e =>
        {
            e.ToTable("TenantDomains");
            e.HasIndex(x => x.Host).IsUnique();
            e.Property(x => x.Host).HasMaxLength(253);
        });

        b.Entity<PlatformUser>(e =>
        {
            e.ToTable("PlatformUsers");
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.Email).HasMaxLength(320);
            e.Property(x => x.Name).HasMaxLength(200);
        });
    }
}
