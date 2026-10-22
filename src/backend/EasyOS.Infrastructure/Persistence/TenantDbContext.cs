using EasyOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EasyOS.Infrastructure.Persistence;

/// <summary>
/// Contexto do banco de um tenant. Mapeia o esquema legado sem alterá-lo: o app mobile
/// continua usando a API antiga sobre os mesmos bancos durante a migração.
/// A connection string é definida por requisição, a partir do tenant resolvido pelo domínio.
/// </summary>
public class TenantDbContext(DbContextOptions<TenantDbContext> options) : DbContext(options)
{
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<CompanyUser> CompanyUsers => Set<CompanyUser>();
    public DbSet<CompanyLoginAudit> CompanyLogins => Set<CompanyLoginAudit>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CustomerUser> CustomerUsers => Set<CustomerUser>();
    public DbSet<CustomerLoginAudit> CustomerLogins => Set<CustomerLoginAudit>();
    public DbSet<Plague> Plagues => Set<Plague>();
    public DbSet<CompanyPlague> CompanyPlagues => Set<CompanyPlague>();
    public DbSet<PlagueStatus> PlagueStatuses => Set<PlagueStatus>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductType> ProductTypes => Set<ProductType>();
    public DbSet<CompanyProduct> CompanyProducts => Set<CompanyProduct>();
    public DbSet<ProductApplication> ProductApplications => Set<ProductApplication>();
    public DbSet<ProductActiveIngredient> ProductActiveIngredients => Set<ProductActiveIngredient>();
    public DbSet<PlagueProduct> PlagueProducts => Set<PlagueProduct>();
    public DbSet<TechnicalManager> TechnicalManagers => Set<TechnicalManager>();
    public DbSet<Visit> Visits => Set<Visit>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderDetail> OrderDetails => Set<OrderDetail>();
    public DbSet<OrderDetailsBaiting> OrderDetailsBaitings => Set<OrderDetailsBaiting>();
    public DbSet<OrderDetailsLuminous> OrderDetailsLuminous => Set<OrderDetailsLuminous>();
    public DbSet<LuminousPlaquePlague> LuminousPlaquePlagues => Set<LuminousPlaquePlague>();
    public DbSet<OrderDetailsPheromone> OrderDetailsPheromones => Set<OrderDetailsPheromone>();
    public DbSet<OrderDetailsPlague> OrderDetailsPlagues => Set<OrderDetailsPlague>();
    public DbSet<OrderDetailsPlaquesOfGlue> OrderDetailsPlaquesOfGlue => Set<OrderDetailsPlaquesOfGlue>();
    public DbSet<OrderConsumption> OrderConsumptions => Set<OrderConsumption>();
    public DbSet<OrderEvidence> OrderEvidences => Set<OrderEvidence>();
    public DbSet<OrderNonConformity> OrderNonConformities => Set<OrderNonConformity>();
    public DbSet<MonthlyComment> MonthlyComments => Set<MonthlyComment>();
    public DbSet<Parameter> Parameters => Set<Parameter>();

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        // O esquema legado usa varchar; parâmetros nvarchar impediriam o uso de índices.
        builder.Properties<string>().AreUnicode(false);
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<CustomerUser>().HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId);

        b.Entity<CompanyPlague>().HasOne(x => x.Plague).WithMany().HasForeignKey(x => x.PlagueId);

        b.Entity<Product>(e =>
        {
            e.HasOne(x => x.ProductType).WithMany().HasForeignKey(x => x.ProductTypeId);
            e.HasMany(x => x.Applications).WithOne().HasForeignKey(x => x.ProductId);
            e.HasMany(x => x.ActiveIngredients).WithOne().HasForeignKey(x => x.ProductId);
            e.HasMany(x => x.PlagueProducts).WithOne(x => x.Product).HasForeignKey(x => x.ProductId);
        });
        b.Entity<CompanyProduct>().HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId);
        b.Entity<PlagueProduct>().HasOne(x => x.Plague).WithMany().HasForeignKey(x => x.PlagueId);

        b.Entity<Visit>(e =>
        {
            e.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId);
            e.HasOne(x => x.ExecutionUser).WithMany().HasForeignKey(x => x.ExecutionUserId);
            e.HasOne(x => x.ResponsibleUser).WithMany().HasForeignKey(x => x.ResponsibleUserId);
        });

        b.Entity<Order>(e =>
        {
            e.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId);
            e.HasOne(x => x.ExecutionUser).WithMany().HasForeignKey(x => x.ExecutionUserId);
            e.HasMany(x => x.Details).WithOne().HasForeignKey(x => x.OrderId);
            e.HasMany(x => x.Evidences).WithOne().HasForeignKey(x => x.OrderId);
            e.HasMany(x => x.NonConformities).WithOne().HasForeignKey(x => x.OrderId);
        });

        b.Entity<OrderDetail>(e =>
        {
            e.HasMany(x => x.Baitings).WithOne().HasForeignKey(x => x.OrderDetailId);
            e.HasMany(x => x.Luminous).WithOne().HasForeignKey(x => x.OrderDetailId);
            e.HasMany(x => x.Pheromones).WithOne().HasForeignKey(x => x.OrderDetailId);
            e.HasMany(x => x.Plagues).WithOne().HasForeignKey(x => x.OrderDetailId);
            e.HasMany(x => x.PlaquesOfGlue).WithOne().HasForeignKey(x => x.OrderDetailId);
            e.HasMany(x => x.Consumptions).WithOne().HasForeignKey(x => x.OrderDetailId);
        });

        b.Entity<OrderDetailsLuminous>().HasMany(x => x.Plagues).WithOne().HasForeignKey(x => x.OrderDetailsLuminousId);
        b.Entity<LuminousPlaquePlague>().HasOne(x => x.Plague).WithMany().HasForeignKey(x => x.PlagueId);

        b.Entity<OrderDetailsPlague>(e =>
        {
            e.HasOne(x => x.Plague).WithMany().HasForeignKey(x => x.PlagueId);
            e.HasOne(x => x.PlagueStatus).WithMany().HasForeignKey(x => x.PlagueStatusId);
        });

        b.Entity<OrderConsumption>(e =>
        {
            e.HasOne(x => x.PlagueProduct).WithMany().HasForeignKey(x => x.PlagueProductId);
            e.HasOne(x => x.ProductApplication).WithMany().HasForeignKey(x => x.ProductApplicationId);
        });

        b.Entity<MonthlyComment>().HasOne(x => x.CompanyUser).WithMany().HasForeignKey(x => x.CompanyUserId);

        // O banco legado não tem ON DELETE CASCADE; o EF também não deve presumir.
        foreach (var fk in b.Model.GetEntityTypes().SelectMany(t => t.GetForeignKeys()))
            fk.DeleteBehavior = DeleteBehavior.NoAction;
    }
}
