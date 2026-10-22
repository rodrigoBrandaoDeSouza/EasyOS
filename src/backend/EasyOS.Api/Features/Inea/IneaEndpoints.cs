using System.Globalization;
using EasyOS.Api.Auth;
using EasyOS.Api.Common;
using EasyOS.Api.Features.Companies;
using EasyOS.Domain.Entities;
using EasyOS.Infrastructure;
using EasyOS.Infrastructure.Persistence;
using EasyOS.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;

namespace EasyOS.Api.Features.Inea;

/// <summary>
/// Documentos do INEA (RJ): RAAE mensal e CES por OS. Devolvem os mesmos campos (snake_case) que os
/// formulários HTML do legado preenchem por id, para manter o layout oficial idêntico.
/// </summary>
public static class IneaEndpoints
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public static void MapIneaEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/inea").WithTags("INEA");
        group.MapGet("/raae", Raae).RequireAuthorization(Policies.Company);
        group.MapGet("/ces/{orderId:int}", Ces).RequireAuthorization(Policies.TenantUser);

        // Assinatura do representante legal (RAAE). No legado era um arquivo copiado no servidor do cliente.
        var signature = app.MapGroup("/api/company/legal-representative/signature").WithTags("INEA");
        signature.MapGet("/", async (TenantFiles files, HttpContext http, CancellationToken ct) =>
        {
            var content = await files.GetAsync(LegalSignatureKey, ct);
            if (content is null) return Results.NotFound();
            Platform.AssetHeaders.Apply(http);
            return Results.Stream(content.Content, content.ContentType);
        }).RequireAuthorization(Policies.Company);
        signature.MapPut("/", async (IFormFile file, TenantFiles files, CancellationToken ct) =>
        {
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (ext is not (".png" or ".jpg" or ".jpeg")) throw new BusinessException("A assinatura deve ser PNG ou JPG.");
            if (file.Length is 0 or > 2 * 1024 * 1024) throw new BusinessException("A imagem deve ter até 2 MB.");
            await using var stream = file.OpenReadStream();
            await files.PutAsync(LegalSignatureKey, stream, ext == ".png" ? "image/png" : "image/jpeg", ct);
            return Results.NoContent();
        }).RequireAuthorization(Policies.CompanyAdmin).DisableAntiforgery();
        signature.MapDelete("/", async (TenantFiles files, CancellationToken ct) =>
        {
            await files.DeleteAsync(LegalSignatureKey, ct);
            return Results.NoContent();
        }).RequireAuthorization(Policies.CompanyAdmin);
    }

    private const string LegalSignatureKey = $"{TenantFiles.BrandingFolder}/legal-representative-signature";

    private static string Na(string? value) => string.IsNullOrWhiteSpace(value) ? "N/A" : value;

    // ---------- RAAE ----------

    private static async Task<Dictionary<string, object?>> Raae(int year, int month, Access access, TenantDbContext db, IClock clock, CancellationToken ct)
    {
        if (month is < 1 or > 12 || year is < 2015 or > 2100) throw new BusinessException("Período inválido.");
        var companyId = await access.CurrentCompanyIdAsync(ct);
        var company = await db.Companies.AsNoTracking().FirstAsync(c => c.Id == companyId, ct);
        var start = new DateTime(year, month, 1);
        var end = start.AddMonths(1);

        var orders = db.Orders.AsNoTracking().Where(o => o.Customer!.CompanyId == companyId && o.OrderDate >= start && o.OrderDate < end);
        var orderList = await orders.Select(o => new { o.Active, o.OrderSequence }).ToListAsync(ct);

        var plagues = await db.OrderDetailsPlagues.AsNoTracking()
            .Join(db.OrderDetails, p => p.OrderDetailId, d => d.Id, (p, d) => new { p, d })
            .Join(orders, x => x.d.OrderId, o => o.Id, (x, o) => x.p.Plague!.Name)
            .GroupBy(n => n).Select(g => new { Name = g.Key, Count = g.Count() }).ToListAsync(ct);

        var consumption = await db.OrderConsumptions.AsNoTracking()
            .Join(db.OrderDetails, c => c.OrderDetailId, d => d.Id, (c, d) => new { c, d })
            .Join(orders, x => x.d.OrderId, o => o.Id, (x, o) => new
            {
                x.c.Quantity,
                x.c.PlagueProduct!.Concentration,
                x.c.PlagueProduct.Product!.Name,
                x.c.PlagueProduct.Product.ActiveIngredient,
                x.c.PlagueProduct.Product.ProductTypeId,
            })
            .ToListAsync(ct);

        static string Quantity(double total, int type) =>
            type == 4 ? total.ToString(PtBr) : (total / 1000).ToString(PtBr) + (type == 1 ? " L" : " kg");

        var places = await db.Customers.AsNoTracking()
            .Where(c => c.CompanyId == companyId && c.ActivityType != null && c.ActivityType != "")
            .Join(orders, c => c.Id, o => o.CustomerId, (c, o) => c.ActivityType!)
            .GroupBy(a => a).Select(g => new { Name = g.Key, Count = g.Count() }).ToListAsync(ct);

        var tm = await db.TechnicalManagers.AsNoTracking()
            .FirstOrDefaultAsync(t => t.CompanyId == companyId && t.State.Trim() == company.State.Trim(), ct);
        var lastDay = end.AddDays(-1);

        return new()
        {
            ["company_name"] = company.CompanyName,
            ["trading_name"] = company.TradingName,
            ["uf"] = company.State.Trim().ToUpperInvariant(),
            ["cpf_cnpj"] = Na(company.CpfCnpj),
            ["address"] = $"{company.Address}, {company.Number}{(string.IsNullOrWhiteSpace(company.Complement) ? "" : " - " + company.Complement)}",
            ["neighborhood"] = Na(company.Neighborhood),
            ["city"] = Na(company.City),
            ["zipcode"] = Na(company.Zipcode),
            ["phone"] = Na(company.Phone),
            ["fax"] = Na(company.Fax),
            ["email"] = Na(company.Email),
            ["inea_code"] = Na(company.IneaCode),
            ["environmental_permit"] = Na(company.EnvironmentalPermit),
            ["environmental_permit_expiration"] = company.EnvironmentalPermitExpiration?.ToString("dd/MM/yyyy") ?? "N/A",
            ["raae_number"] = lastDay.ToString("MM/yy"),
            ["raae_date"] = $"{start:dd} a {lastDay:dd/MM/yy}",
            ["orders_total"] = orderList.Count.ToString(),
            ["orders_complete"] = orderList.Count(o => o.Active).ToString(),
            ["orders_canceled"] = orderList.Count(o => !o.Active).ToString(),
            ["last_order"] = orderList.Where(o => o.OrderSequence.HasValue).Select(o => o.OrderSequence!.Value).DefaultIfEmpty().Max() is var last and > 0 ? last.ToString() : "N/A",
            ["plagues"] = plagues.OrderBy(p => p.Name).Select(p => new { name = p.Name, quantity = p.Count.ToString() }).ToList(),
            ["products"] = consumption
                .GroupBy(c => new { Name = $"{c.ActiveIngredient} a {c.Concentration.ToString(PtBr)}%", c.ProductTypeId })
                .OrderBy(g => g.Key.Name)
                .Select(g => new { name = g.Key.Name, quantity = Quantity(g.Sum(c => c.Quantity), g.Key.ProductTypeId) }).ToList(),
            ["brands"] = consumption
                .GroupBy(c => new { c.Name, c.ProductTypeId })
                .OrderBy(g => g.Key.Name)
                .Select(g => new { name = g.Key.Name, quantity = Quantity(g.Sum(c => c.Quantity), g.Key.ProductTypeId) }).ToList(),
            ["places"] = places.OrderBy(p => p.Name).Select(p => new { name = p.Name, quantity = p.Count.ToString() }).ToList(),
            ["date"] = clock.Now.ToString("dd/MM/yyyy"),
            ["technical_manager"] = tm?.Name ?? "",
            ["legal_representative"] = Na(company.LegalRepresentative),
        };
    }

    // ---------- CES ----------

    private static readonly (string Key, string Code)[] ChemicalCodes =
    [
        ("amidinohidrazona", "1"), ("benzoiluruia", "2"), ("ester", "3"), ("alifatico", "3"), ("eter", "4"), ("piridiloxipropilico", "4"),
        ("inorganico", "5"), ("microbiologico", "6"), ("metil", "7"), ("carbamato", "7"), ("neonicotinoide", "8"), ("organofosforado", "9"),
        ("pirazol", "10"), ("piretroide", "11"), ("aulfonamida", "12"), ("triazinamina", "13"), ("tetroxocano", "14"), ("cumarinico", "15"),
        ("indadiona", "16"), ("benzotiopiranona", "17"), ("oxadiazina", "18"),
    ];

    private static readonly (string Key, string Code)[] EquipmentCodes =
    [
        ("press costal manual", "1"), ("press. costal manual", "1"), ("pressao costal manual", "1"), ("manual", "1"), ("pulverizador manual", "1"),
        ("pulverizador motorizado", "2"), ("motorizado", "2"), ("pistola de aplicacao gel", "3"), ("pistola aplicadora gel", "3"),
        ("pistola aplicadora de gel", "3"), ("seringa de aplicacao gel", "4"), ("seringa aplicadora gel", "4"), ("seringa aplicadora de gel", "4"),
        ("termonebulizadora", "5"), ("termonebulizacao", "5"), ("nebulizacao", "5"), ("atomizacao", "6"), ("atomizador", "6"),
        ("atomizadora", "6"), ("polvilhadeira", "7"), ("porta iscas para roedores", "8"), ("porta iscas para baratas", "9"),
        ("porta iscas para formigas", "10"), ("armadilhas adesivas", "11"),
    ];

    private static string CodeFor((string Key, string Code)[] table, string value)
    {
        // Mesmo critério do legado: compara sem acento e em minúsculas, pelo primeiro termo contido.
        var normalized = Text.RemoveDiacritics(value ?? "").ToLowerInvariant();
        return table.FirstOrDefault(t => normalized.Contains(t.Key)).Code ?? "";
    }

    private static async Task<Dictionary<string, object?>> Ces(int orderId, Access access, TenantDbContext db, CancellationToken ct)
    {
        var order = await access.Orders().Where(o => o.Id == orderId).Include(o => o.Customer).Include(o => o.ExecutionUser)
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException("Ordem de serviço não encontrada.");
        var customer = order.Customer!;
        var company = await db.Companies.AsNoTracking().FirstAsync(c => c.Id == customer.CompanyId, ct);
        var tm = await db.TechnicalManagers.AsNoTracking()
            .FirstOrDefaultAsync(t => t.CompanyId == company.Id && t.State.Trim() == customer.State.Trim(), ct);
        var visitDescription = order.VisitId == 0 ? null : await db.Visits.AsNoTracking().Where(v => v.Id == order.VisitId).Select(v => v.Description).FirstOrDefaultAsync(ct);
        var detail = await db.OrderDetails.AsNoTracking().Where(d => d.OrderId == orderId)
            .Include(d => d.Baitings).Include(d => d.Luminous).Include(d => d.Pheromones).Include(d => d.PlaquesOfGlue)
            .AsSplitQuery().FirstOrDefaultAsync(ct);

        var consumptions = await db.OrderConsumptions.AsNoTracking()
            .Where(c => db.OrderDetails.Any(d => d.Id == c.OrderDetailId && d.OrderId == orderId))
            .Select(c => new
            {
                c.Quantity,
                c.PlagueProduct!.Concentration,
                ProductId = c.PlagueProduct.Product!.Id,
                c.PlagueProduct.Product.ChemicalGroup,
                c.PlagueProduct.Product.ProductTypeId,
                Unity = c.PlagueProduct.Product.ProductType!.Unity,
                Plague = c.PlagueProduct.Plague!.Name,
                Equipment = c.ProductApplication!.Description,
            })
            .ToListAsync(ct);
        var productIds = consumptions.Select(c => c.ProductId).Distinct().ToList();
        var ingredients = await db.ProductActiveIngredients.AsNoTracking()
            .Where(i => productIds.Contains(i.ProductId) && i.Active).ToListAsync(ct);

        var products = consumptions.Select(c =>
        {
            var mine = ingredients.Where(i => i.ProductId == c.ProductId).ToList();
            return new Dictionary<string, object?>
            {
                ["chemical_code"] = "",
                ["chemical_group"] = CodeFor(ChemicalCodes, c.ChemicalGroup),
                ["active_ingredient"] = string.Join(" + ", mine.Select(i => i.ActiveIngredient)),
                ["concentration"] = string.Join(" + ", mine.Select(i => i.Concentration.ToString(PtBr))),
                ["quantity"] = c.Concentration != 0 && c.ProductTypeId != 4 ? $"{c.Quantity.ToString(PtBr)} {c.Unity}" : "N/A",
                ["diluent"] = c.Concentration != 0 && c.ProductTypeId == 1 ? "H2O" : "N/A",
                ["plague"] = c.Plague,
                ["equipment"] = CodeFor(EquipmentCodes, c.Equipment),
            };
        }).ToList();

        // Pragas controladas (dos produtos) + encontradas, marcadas nas caixas do formulário.
        var plagueNames = (await db.OrderDetailsPlagues.AsNoTracking()
                .Where(p => db.OrderDetails.Any(d => d.Id == p.OrderDetailId && d.OrderId == orderId))
                .Select(p => p.Plague!.Name).ToListAsync(ct))
            .Concat(consumptions.Select(c => c.Plague))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        var result = new Dictionary<string, object?>();
        var others = new List<string>();
        foreach (var name in plagueNames)
        {
            var key = name.Trim().ToLowerInvariant() switch
            {
                "barata de cozinha" or "baratas de cozinha" or "barata" or "baratas" => "plague_kitchen_roach",
                "barata de esgoto" or "baratas de esgoto" => "plague_sewer_roach",
                "rato" or "ratos" or "roedor" or "roedores" => "plague_rat",
                "ratazana" or "ratazanas" => "plague_vole",
                "camundongo" or "camundongos" => "plague_mouse",
                "pulga" or "pulgas" => "plague_flea",
                "cupim subterrâneo" or "cupins subterrâneo" => "plague_undergroung_termite",
                "cupim" or "cupins" or "cupim de madeira seca" or "cupins de madeira seca" => "plague_wood_termite",
                "mosca" or "moscas" => "plague_fly",
                "mosquito" or "mosquitos" => "plague_mosquito",
                _ => null,
            };
            if (key is null) others.Add(name);
            else result[key] = true;
        }
        if (others.Count > 0) result["plague_other"] = true;

        var nonChemicals = new List<string>();
        var luminous = detail?.Luminous.Count ?? 0;
        var pheromones = detail?.Pheromones.Count ?? 0;
        var glue = detail?.PlaquesOfGlue.Count ?? 0;
        var baitings = detail?.Baitings.Count ?? 0;
        if (luminous > 0) nonChemicals.Add("armadilhas luminosas");
        if (pheromones > 0) nonChemicals.Add("armadilhas de feromônios");
        if (glue > 0) nonChemicals.Add("placas de cola");
        if (plagueNames.Any(n => n.Equals("abelha", StringComparison.OrdinalIgnoreCase) || n.Equals("abelhas", StringComparison.OrdinalIgnoreCase)))
            nonChemicals.Add("retirada");
        if (glue > 0) { result["plague_rat"] = true; result["plague_fly"] = true; }
        if (baitings > 0) result["plague_rat"] = true;
        if (pheromones > 0 || luminous > 0) result["plague_fly"] = true;

        var offset = order.OffsetMinutes;
        result["ces_number"] = (order.OrderSequence ?? order.Id).ToString();
        result["applicant_signature"] = order.ExecutionUser?.Signature;
        result["responsible_signature"] = order.ResponsibleSignature;
        result["responsible_name"] = order.ResponsibleName;
        result["responsible_business"] = order.ResponsibleBusiness;
        result["observations"] = order.Observations;
        result["time_start"] = LegacyTime.ToLocal(order.TimeStart, offset).ToString("HH:mm");
        result["time_end"] = LegacyTime.ToLocal(order.TimeEnd, offset).ToString("HH:mm");
        result["service_description"] = visitDescription ?? "";
        result["corrective_preventive_measures"] = await db.OrderNonConformities.AsNoTracking().Where(n => n.OrderId == orderId)
            .Select(n => new { place = n.Place, description = n.Description, suggestion = n.Suggestion }).ToListAsync(ct);
        result["evidencesImages"] = await db.OrderEvidences.AsNoTracking().Where(e => e.OrderId == orderId).OrderBy(e => e.Id)
            .Select(e => $"/api/orders/{orderId}/evidences/{e.Id}").ToListAsync(ct);
        result["company_company_name"] = company.CompanyName;
        result["company_address"] = $"{company.Address}, {company.Number}{(string.IsNullOrWhiteSpace(company.Complement) ? "" : " - " + company.Complement)} - {company.Neighborhood} - {company.City}/{company.State} - {company.Zipcode}";
        result["company_phone"] = $"Telefone: {company.Phone}{(string.IsNullOrWhiteSpace(company.Fax) ? "" : " / Fax: " + company.Fax)}";
        result["company_email"] = company.Email ?? "";
        result["company_cpf_cnpj"] = string.IsNullOrWhiteSpace(company.IneaCode) ? "N/A" : company.CpfCnpj;
        result["inea_code"] = Na(company.IneaCode);
        result["environmental_permit"] = Na(company.EnvironmentalPermit);
        result["environmental_permit_expiration"] = company.EnvironmentalPermitExpiration?.ToString("dd/MM/yyyy") ?? "N/A";
        result["company_name"] = customer.CompanyName;
        result["trading_name"] = customer.TradingName;
        result["activity_type"] = Na(customer.ActivityType);
        result["cpf_cnpj"] = Na(customer.CpfCnpj);
        result["address"] = $"{customer.Address}, {customer.Number}{(string.IsNullOrWhiteSpace(customer.Complement) ? "" : " - " + customer.Complement)}";
        result["neighborhood"] = Na(customer.Neighborhood);
        result["city"] = Na(customer.City);
        result["zipcode"] = Na(customer.Zipcode);
        result["phone"] = Na(customer.Phone);
        result["fax"] = Na(customer.Fax);
        result["email"] = Na(customer.Email);
        result["contact_name"] = Na(customer.ContactName);
        result["contact_function"] = Na(customer.ContactFunction);
        result["products"] = products;
        result["non_chemicals"] = nonChemicals.Count == 0 ? "" : char.ToUpper(nonChemicals[0][0]) + string.Join(", ", nonChemicals)[1..];
        result["plagues"] = plagueNames;
        result["other_description"] = others.Count > 0 ? ": " + string.Join(", ", others) : "______________________";
        result["date"] = order.OrderDate.ToString("dd/MM/yyyy");
        result["companyuser_execution"] = order.ExecutionUser?.Name;
        result["technical_manager"] = tm?.Name ?? "";
        result["uf"] = customer.State.Trim().ToUpperInvariant();
        return result;
    }
}
