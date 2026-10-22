using EasyOS.Api.Auth;
using EasyOS.Api.Common;
using EasyOS.Api.Features.Users;
using EasyOS.Domain.Entities;
using EasyOS.Infrastructure;
using EasyOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EasyOS.Api.Features.Catalog;

public sealed record PlagueDto(int Id, string Name, string PlagueType, bool Active);
public sealed record PlagueRequest(string Name, string PlagueType);
public sealed record PlagueStatusDto(int Id, string Description);

public sealed record ProductListItem(
    int Id, string Name, int ProductTypeId, string Unity, int ProductCode, string RegistrationNumber,
    string ChemicalGroup, string ActiveIngredient, string EquipmentType, string Symptoms, string Antidotes, bool Active);

public sealed record ProductPlagueItem(int PlagueId, string? PlagueName, double Concentration);
public sealed record ProductApplicationItem(int? Id, string Description);
public sealed record ProductIngredientItem(int? Id, string ActiveIngredient, double Concentration);

public sealed record ProductDetail(
    int Id, string Name, int ProductTypeId, int ProductCode, string RegistrationNumber, string ChemicalGroup,
    string ActiveIngredient, string EquipmentType, string Symptoms, string Antidotes,
    IReadOnlyList<ProductPlagueItem> Plagues, IReadOnlyList<ProductApplicationItem> Applications,
    IReadOnlyList<ProductIngredientItem> ActiveIngredients);

public sealed record ProductRequest(
    string Name, int ProductTypeId, int ProductCode, string? RegistrationNumber, string? ChemicalGroup,
    string? ActiveIngredient, string? EquipmentType, string? Symptoms, string? Antidotes,
    List<ProductPlagueItem>? Plagues, List<ProductApplicationItem>? Applications, List<ProductIngredientItem>? ActiveIngredients);

public sealed record ProductTypeDto(int Id, string Type, string Unity);

/// <summary>Produto indicado para uma praga, com as formas de aplicação (lançamento de consumo na OS).</summary>
public sealed record ProductForPlague(int PlagueProductId, int ProductId, int ProductTypeId, string Name, string Unity, double Concentration,
    IReadOnlyList<ProductApplicationItem> Applications);

public sealed record ParameterDto(int Id, string Name, string Description);

/// <summary>
/// Pragas e produtos são cadastros globais do banco; cada empresa ativa/desativa os seus
/// (tabelas CompanyPlagues e CompanyProducts), como no legado.
/// </summary>
public static class CatalogEndpoints
{
    public static void MapCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        var plagues = app.MapGroup("/api/plagues").WithTags("Pragas");
        plagues.MapGet("/", ListPlagues).RequireAuthorization(Policies.TenantUser);
        plagues.MapPost("/", CreatePlague).RequireAuthorization(Policies.CompanyAdmin);
        plagues.MapPut("/{id:int}", UpdatePlague).RequireAuthorization(Policies.CompanyAdmin);
        plagues.MapPost("/{id:int}/status", ChangePlagueStatus).RequireAuthorization(Policies.CompanyAdmin);

        app.MapGet("/api/plague-statuses", async (TenantDbContext db, CancellationToken ct) =>
            await db.PlagueStatuses.AsNoTracking().OrderBy(s => s.Id).Select(s => new PlagueStatusDto(s.Id, s.Description)).ToListAsync(ct))
            .WithTags("Pragas").RequireAuthorization(Policies.TenantUser);

        var products = app.MapGroup("/api/products").WithTags("Produtos");
        products.MapGet("/", ListProducts).RequireAuthorization(Policies.Company);
        products.MapGet("/{id:int}", GetProduct).RequireAuthorization(Policies.Company);
        products.MapGet("/by-plague/{plagueId:int}", ProductsByPlague).RequireAuthorization(Policies.Company);
        products.MapPost("/", CreateProduct).RequireAuthorization(Policies.CompanyAdmin);
        products.MapPut("/{id:int}", UpdateProduct).RequireAuthorization(Policies.CompanyAdmin);
        products.MapPost("/{id:int}/status", ChangeProductStatus).RequireAuthorization(Policies.CompanyAdmin);

        app.MapGet("/api/product-types", async (TenantDbContext db, CancellationToken ct) =>
            await db.ProductTypes.AsNoTracking().OrderBy(t => t.Type).Select(t => new ProductTypeDto(t.Id, t.Type, t.Unity)).ToListAsync(ct))
            .WithTags("Produtos").RequireAuthorization(Policies.Company);

        // Parâmetros ativos do banco do cliente (ex.: OS_RDC_622 muda o layout da OS e do certificado).
        app.MapGet("/api/parameters", async (TenantDbContext db, CancellationToken ct) =>
            await db.Parameters.AsNoTracking().Where(p => p.Active).OrderBy(p => p.Name)
                .Select(p => new ParameterDto(p.Id, p.Name, p.Description)).ToListAsync(ct))
            .WithTags("Parâmetros").RequireAuthorization(Policies.TenantUser);
    }

    // ---------- Pragas ----------

    private static async Task<List<PlagueDto>> ListPlagues(bool? onlyActive, Access access, TenantDbContext db, CancellationToken ct)
    {
        var companyId = await CompanyForCatalog(access, db, ct);
        var query = db.CompanyPlagues.AsNoTracking().Where(cp => cp.CompanyId == companyId && !cp.Removed);
        if (onlyActive ?? false) query = query.Where(cp => cp.Active);
        return await query.OrderBy(cp => cp.Plague!.Name)
            .Select(cp => new PlagueDto(cp.PlagueId, cp.Plague!.Name, cp.Plague.PlagueType, cp.Active))
            .ToListAsync(ct);
    }

    private static async Task<IResult> CreatePlague(PlagueRequest req, TenantDbContext db, IClock clock, CancellationToken ct)
    {
        var now = clock.Now;
        var plague = new Plague { Name = Text.Required(req.Name, "o nome"), PlagueType = Text.Required(req.PlagueType, "o tipo"), LastUpdate = now };
        var companies = await db.Companies.Select(c => c.Id).ToListAsync(ct);

        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            db.Plagues.Add(plague);
            await db.SaveChangesAsync(ct);
            // Igual ao legado: a praga nova fica disponível para todas as empresas do banco.
            db.CompanyPlagues.AddRange(companies.Select(id => new CompanyPlague { CompanyId = id, PlagueId = plague.Id, Active = true, LastUpdate = now }));
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });
        return Results.Created($"/api/plagues/{plague.Id}", new PlagueDto(plague.Id, plague.Name, plague.PlagueType, true));
    }

    private static async Task<IResult> UpdatePlague(int id, PlagueRequest req, TenantDbContext db, IClock clock, CancellationToken ct)
    {
        var plague = await db.Plagues.FirstOrDefaultAsync(p => p.Id == id, ct) ?? throw new NotFoundException("Praga não encontrada.");
        plague.Name = Text.Required(req.Name, "o nome");
        plague.PlagueType = Text.Required(req.PlagueType, "o tipo");
        plague.LastUpdate = clock.Now;
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> ChangePlagueStatus(int id, StatusRequest req, Access access, TenantDbContext db, IClock clock, CancellationToken ct)
    {
        var companyId = await access.CurrentCompanyIdAsync(ct);
        var link = await db.CompanyPlagues.FirstOrDefaultAsync(cp => cp.PlagueId == id && cp.CompanyId == companyId, ct)
            ?? throw new NotFoundException("Praga não encontrada.");
        link.Active = req.Active;
        link.LastUpdate = clock.Now;
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    // ---------- Produtos ----------

    private static async Task<List<ProductListItem>> ListProducts(bool? onlyActive, Access access, TenantDbContext db, CancellationToken ct)
    {
        var companyId = await access.CurrentCompanyIdAsync(ct);
        var query = db.CompanyProducts.AsNoTracking().Where(cp => cp.CompanyId == companyId);
        if (onlyActive ?? false) query = query.Where(cp => cp.Active);
        return await query.OrderBy(cp => cp.Product!.Name)
            .Select(cp => new ProductListItem(cp.ProductId, cp.Product!.Name, cp.Product.ProductTypeId, cp.Product.ProductType!.Unity,
                cp.Product.ProductCode, cp.Product.RegistrationNumber, cp.Product.ChemicalGroup, cp.Product.ActiveIngredient,
                cp.Product.EquipmentType, cp.Product.Symptoms, cp.Product.Antidotes, cp.Active))
            .ToListAsync(ct);
    }

    private static async Task<ProductDetail> GetProduct(int id, Access access, TenantDbContext db, CancellationToken ct)
    {
        var companyId = await access.CurrentCompanyIdAsync(ct);
        if (!await db.CompanyProducts.AnyAsync(cp => cp.ProductId == id && cp.CompanyId == companyId, ct))
            throw new NotFoundException("Produto não encontrado.");

        var p = await db.Products.AsNoTracking()
            .Include(x => x.PlagueProducts.Where(pp => pp.Active)).ThenInclude(pp => pp.Plague)
            .Include(x => x.Applications.Where(a => a.Active))
            .Include(x => x.ActiveIngredients.Where(i => i.Active))
            .AsSplitQuery()
            .FirstAsync(x => x.Id == id, ct);

        return new ProductDetail(p.Id, p.Name, p.ProductTypeId, p.ProductCode, p.RegistrationNumber, p.ChemicalGroup, p.ActiveIngredient,
            p.EquipmentType, p.Symptoms, p.Antidotes,
            p.PlagueProducts.Select(pp => new ProductPlagueItem(pp.PlagueId, pp.Plague?.Name, pp.Concentration)).OrderBy(x => x.PlagueName).ToList(),
            p.Applications.Select(a => new ProductApplicationItem(a.Id, a.Description)).ToList(),
            p.ActiveIngredients.Select(i => new ProductIngredientItem(i.Id, i.ActiveIngredient, i.Concentration)).ToList());
    }

    private static async Task<List<ProductForPlague>> ProductsByPlague(int plagueId, Access access, TenantDbContext db, CancellationToken ct)
    {
        var companyId = await access.CurrentCompanyIdAsync(ct);
        var rows = await db.PlagueProducts.AsNoTracking()
            .Where(pp => pp.PlagueId == plagueId && pp.Active &&
                         db.CompanyProducts.Any(cp => cp.ProductId == pp.ProductId && cp.CompanyId == companyId && cp.Active))
            .OrderBy(pp => pp.Product!.Name)
            .Select(pp => new
            {
                pp.Id, pp.ProductId, pp.Product!.ProductTypeId, pp.Product.Name, pp.Product.ProductType!.Unity, pp.Concentration,
                Applications = pp.Product.Applications.Where(a => a.Active).Select(a => new ProductApplicationItem(a.Id, a.Description)).ToList(),
            })
            .ToListAsync(ct);
        return rows.Select(r => new ProductForPlague(r.Id, r.ProductId, r.ProductTypeId, r.Name, r.Unity, r.Concentration, r.Applications)).ToList();
    }

    private static async Task<IResult> CreateProduct(ProductRequest req, TenantDbContext db, IClock clock, CancellationToken ct)
    {
        var now = clock.Now;
        var product = new Product { CreatedDate = now };
        ApplyProduct(product, req, now);
        var companies = await db.Companies.Select(c => c.Id).ToListAsync(ct);

        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            db.Products.Add(product);
            await db.SaveChangesAsync(ct);

            // Igual ao legado: o produto novo é habilitado para todas as empresas do banco.
            db.CompanyProducts.AddRange(companies.Select(id => new CompanyProduct { CompanyId = id, ProductId = product.Id, Active = true, LastUpdate = now }));
            foreach (var pl in DistinctPlagues(req.Plagues))
                db.PlagueProducts.Add(new PlagueProduct { ProductId = product.Id, PlagueId = pl.PlagueId, Concentration = pl.Concentration, Active = true, LastUpdate = now });
            foreach (var a in CleanApplications(req.Applications))
                db.ProductApplications.Add(new ProductApplication { ProductId = product.Id, Description = a.Description, Active = true, LastUpdate = now });
            foreach (var i in CleanIngredients(req.ActiveIngredients))
                db.ProductActiveIngredients.Add(new ProductActiveIngredient { ProductId = product.Id, ActiveIngredient = i.ActiveIngredient, Concentration = i.Concentration, Active = true });

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });
        return Results.Created($"/api/products/{product.Id}", new { product.Id });
    }

    /// <summary>
    /// Atualiza o produto e sincroniza pragas, aplicações e princípios ativos. Itens removidos são
    /// desativados (não apagados), pois OS antigas os referenciam.
    /// </summary>
    private static async Task<IResult> UpdateProduct(int id, ProductRequest req, Access access, TenantDbContext db, IClock clock, CancellationToken ct)
    {
        var companyId = await access.CurrentCompanyIdAsync(ct);
        if (!await db.CompanyProducts.AnyAsync(cp => cp.ProductId == id && cp.CompanyId == companyId, ct))
            throw new NotFoundException("Produto não encontrado.");

        var now = clock.Now;
        var product = await db.Products.FirstAsync(p => p.Id == id, ct);
        ApplyProduct(product, req, now);

        // Pragas: vínculo por idplague (reativa vínculos antigos em vez de duplicar).
        var plagueLinks = await db.PlagueProducts.Where(pp => pp.ProductId == id).ToListAsync(ct);
        var wanted = DistinctPlagues(req.Plagues).ToList();
        foreach (var link in plagueLinks.Where(l => l.Active && wanted.All(w => w.PlagueId != l.PlagueId)))
        {
            link.Active = false;
            link.LastUpdate = now;
        }
        foreach (var w in wanted)
        {
            var link = plagueLinks.Where(l => l.PlagueId == w.PlagueId).OrderByDescending(l => l.Active).FirstOrDefault();
            if (link is null)
                db.PlagueProducts.Add(new PlagueProduct { ProductId = id, PlagueId = w.PlagueId, Concentration = w.Concentration, Active = true, LastUpdate = now });
            else if (!link.Active || link.Concentration != w.Concentration)
            {
                link.Active = true;
                link.Concentration = w.Concentration;
                link.LastUpdate = now;
            }
        }

        // Aplicações: por id.
        var applications = await db.ProductApplications.Where(a => a.ProductId == id && a.Active).ToListAsync(ct);
        var wantedApps = CleanApplications(req.Applications).ToList();
        foreach (var a in applications.Where(a => wantedApps.All(w => w.Id != a.Id)))
        {
            a.Active = false;
            a.LastUpdate = now;
        }
        foreach (var w in wantedApps)
        {
            var existing = w.Id is null ? null : applications.FirstOrDefault(a => a.Id == w.Id);
            if (existing is null)
                db.ProductApplications.Add(new ProductApplication { ProductId = id, Description = w.Description, Active = true, LastUpdate = now });
            else if (existing.Description != w.Description)
            {
                existing.Description = w.Description;
                existing.LastUpdate = now;
            }
        }

        // Princípios ativos: por id.
        var ingredients = await db.ProductActiveIngredients.Where(i => i.ProductId == id && i.Active).ToListAsync(ct);
        var wantedIngredients = CleanIngredients(req.ActiveIngredients).ToList();
        foreach (var i in ingredients.Where(i => wantedIngredients.All(w => w.Id != i.Id)))
            i.Active = false;
        foreach (var w in wantedIngredients)
        {
            var existing = w.Id is null ? null : ingredients.FirstOrDefault(i => i.Id == w.Id);
            if (existing is null)
                db.ProductActiveIngredients.Add(new ProductActiveIngredient { ProductId = id, ActiveIngredient = w.ActiveIngredient, Concentration = w.Concentration, Active = true });
            else
            {
                existing.ActiveIngredient = w.ActiveIngredient;
                existing.Concentration = w.Concentration;
            }
        }

        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> ChangeProductStatus(int id, StatusRequest req, Access access, TenantDbContext db, IClock clock, CancellationToken ct)
    {
        var companyId = await access.CurrentCompanyIdAsync(ct);
        var link = await db.CompanyProducts.FirstOrDefaultAsync(cp => cp.ProductId == id && cp.CompanyId == companyId, ct)
            ?? throw new NotFoundException("Produto não encontrado.");
        link.Active = req.Active;
        link.LastUpdate = clock.Now;
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static void ApplyProduct(Product p, ProductRequest r, DateTime now)
    {
        p.Name = Text.Required(r.Name, "o nome do produto");
        if (r.ProductTypeId <= 0) throw new BusinessException("Informe o tipo do produto.");
        p.ProductTypeId = r.ProductTypeId;
        p.ProductCode = r.ProductCode;
        p.RegistrationNumber = r.RegistrationNumber?.Trim() ?? "";
        p.ChemicalGroup = r.ChemicalGroup?.Trim() ?? "";
        p.ActiveIngredient = r.ActiveIngredient?.Trim() ?? "";
        p.EquipmentType = r.EquipmentType?.Trim() ?? "";
        p.Symptoms = r.Symptoms?.Trim() ?? "";
        p.Antidotes = r.Antidotes?.Trim() ?? "";
        p.LastUpdate = now;
    }

    private static IEnumerable<ProductPlagueItem> DistinctPlagues(List<ProductPlagueItem>? items) =>
        (items ?? []).Where(i => i.PlagueId > 0).GroupBy(i => i.PlagueId).Select(g => g.Last());

    private static IEnumerable<ProductApplicationItem> CleanApplications(List<ProductApplicationItem>? items) =>
        (items ?? []).Where(i => !string.IsNullOrWhiteSpace(i.Description)).Select(i => i with { Description = i.Description.Trim() });

    private static IEnumerable<ProductIngredientItem> CleanIngredients(List<ProductIngredientItem>? items) =>
        (items ?? []).Where(i => !string.IsNullOrWhiteSpace(i.ActiveIngredient)).Select(i => i with { ActiveIngredient = i.ActiveIngredient.Trim() });

    /// <summary>A área do cliente também lista pragas (filtros de relatório): usa a empresa do cliente.</summary>
    private static async Task<int> CompanyForCatalog(Access access, TenantDbContext db, CancellationToken ct) =>
        access.User.IsCustomerUser
            ? (await access.CustomerAsync(access.User.RequireCustomerId(), ct)).CompanyId
            : await access.CurrentCompanyIdAsync(ct);
}
