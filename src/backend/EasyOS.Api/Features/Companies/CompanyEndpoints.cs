using EasyOS.Api.Auth;
using EasyOS.Api.Common;
using EasyOS.Domain.Entities;
using EasyOS.Infrastructure;
using EasyOS.Infrastructure.Persistence;
using EasyOS.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;

namespace EasyOS.Api.Features.Companies;

public sealed record CompanyDto(
    int Id, string TradingName, string CompanyName, string? CpfCnpj,
    string Address, int Number, string Complement, string Neighborhood, string City, string State, string Zipcode,
    string Phone, string? Fax, string? Email, string ContactName,
    string Charter, DateTime CharterExpiration,
    string? IneaCode, string? EnvironmentalPermit, DateTime? EnvironmentalPermitExpiration, string? LegalRepresentative,
    bool UseBaitings, bool UsePlaqueOfGlue, bool UseLuminousTrap, bool UsePheromoneTrap)
{
    public static CompanyDto From(Company c) => new(
        c.Id, c.TradingName, c.CompanyName, c.CpfCnpj, c.Address, c.Number, c.Complement, c.Neighborhood, c.City, c.State, c.Zipcode,
        c.Phone, c.Fax, c.Email, c.ContactName, c.Charter, c.CharterExpiration,
        c.IneaCode, c.EnvironmentalPermit, c.EnvironmentalPermitExpiration, c.LegalRepresentative,
        c.UseBaitings, c.UsePlaqueOfGlue, c.UseLuminousTrap, c.UsePheromoneTrap);
}

public sealed record CompanyUpdateRequest(
    string TradingName, string CompanyName, string? CpfCnpj,
    string Address, int Number, string? Complement, string Neighborhood, string City, string State, string Zipcode,
    string Phone, string? Fax, string? Email, string ContactName,
    string Charter, DateTime CharterExpiration,
    string? IneaCode, string? EnvironmentalPermit, DateTime? EnvironmentalPermitExpiration, string? LegalRepresentative,
    bool UseBaitings, bool UsePlaqueOfGlue, bool UseLuminousTrap, bool UsePheromoneTrap);

public sealed record TechnicalManagerDto(int Id, string State, string Register, string Name);
public sealed record TechnicalManagerItem(string State, string? Register, string? Name);

public static class CompanyEndpoints
{
    public static void MapCompanyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/company").WithTags("Empresa");

        // Usado por todas as telas (ex.: regra de campos exigidos no RJ), inclusive pela área do cliente.
        group.MapGet("/", GetCurrent).RequireAuthorization(Policies.TenantUser);
        group.MapPut("/", Update).RequireAuthorization(Policies.CompanyAdmin);

        group.MapGet("/technical-managers", async (Access access, TenantDbContext db, CancellationToken ct) =>
        {
            var companyId = await access.CurrentCompanyIdAsync(ct);
            return await db.TechnicalManagers.AsNoTracking().Where(t => t.CompanyId == companyId).OrderBy(t => t.State)
                .Select(t => new TechnicalManagerDto(t.Id, t.State, t.Register, t.Name)).ToListAsync(ct);
        }).RequireAuthorization(Policies.Company);

        group.MapPut("/technical-managers", SaveTechnicalManagers).RequireAuthorization(Policies.CompanyAdmin);

        // Assinatura do responsável técnico de cada UF (impressa no certificado). No legado era um arquivo
        // assinaturas/assinatura-{uf}.png copiado manualmente no servidor de cada cliente.
        group.MapGet("/technical-managers/{state}/signature", async (string state, TenantFiles files, HttpContext http, CancellationToken ct) =>
        {
            var content = await files.GetAsync(TechnicalSignatureKey(state), ct);
            if (content is null) return Results.NotFound();
            Platform.AssetHeaders.Apply(http);
            return Results.Stream(content.Content, content.ContentType);
        }).RequireAuthorization(Policies.TenantUser);

        group.MapPut("/technical-managers/{state}/signature", async (string state, IFormFile file, TenantFiles files, CancellationToken ct) =>
        {
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (ext is not (".png" or ".jpg" or ".jpeg")) throw new BusinessException("A assinatura deve ser PNG ou JPG.");
            if (file.Length is 0 or > 2 * 1024 * 1024) throw new BusinessException("A imagem deve ter até 2 MB.");
            await using var stream = file.OpenReadStream();
            await files.PutAsync(TechnicalSignatureKey(state), stream, ext == ".png" ? "image/png" : "image/jpeg", ct);
            return Results.NoContent();
        }).RequireAuthorization(Policies.CompanyAdmin).DisableAntiforgery();

        group.MapDelete("/technical-managers/{state}/signature", async (string state, TenantFiles files, CancellationToken ct) =>
        {
            await files.DeleteAsync(TechnicalSignatureKey(state), ct);
            return Results.NoContent();
        }).RequireAuthorization(Policies.CompanyAdmin);
    }

    public static string TechnicalSignatureKey(string state)
    {
        var uf = (state ?? "").Trim().ToUpperInvariant();
        if (uf.Length != 2 || !uf.All(char.IsAsciiLetterUpper)) throw new BusinessException("UF inválida.");
        return $"{TenantFiles.BrandingFolder}/technical-signatures/{uf}";
    }

    private static async Task<CompanyDto> GetCurrent(Access access, TenantDbContext db, CancellationToken ct)
    {
        int companyId;
        if (access.User.IsCustomerUser)
            companyId = (await access.CustomerAsync(access.User.RequireCustomerId(), ct)).CompanyId;
        else
            companyId = await access.CurrentCompanyIdAsync(ct);

        var company = await db.Companies.AsNoTracking().FirstOrDefaultAsync(c => c.Id == companyId, ct) ?? throw new NotFoundException("Empresa não encontrada.");
        return CompanyDto.From(company);
    }

    private static async Task<CompanyDto> Update(CompanyUpdateRequest req, Access access, TenantDbContext db, IClock clock, CancellationToken ct)
    {
        var companyId = await access.CurrentCompanyIdAsync(ct);
        var c = await db.Companies.FirstOrDefaultAsync(x => x.Id == companyId, ct) ?? throw new NotFoundException("Empresa não encontrada.");

        c.TradingName = Text.Required(req.TradingName, "o nome fantasia");
        c.CompanyName = Text.Required(req.CompanyName, "a razão social");
        c.CpfCnpj = Text.Clean(req.CpfCnpj);
        c.Address = Text.Required(req.Address, "o endereço");
        c.Number = req.Number;
        c.Complement = Text.Clean(req.Complement) ?? "";
        c.Neighborhood = Text.Required(req.Neighborhood, "o bairro");
        c.City = Text.Required(req.City, "a cidade");
        c.State = Text.Required(req.State, "o estado").ToUpperInvariant();
        c.Zipcode = Text.Required(req.Zipcode, "o CEP");
        c.Phone = Text.Required(req.Phone, "o telefone");
        c.Fax = Text.Clean(req.Fax);
        c.Email = Text.Clean(req.Email);
        c.ContactName = Text.Required(req.ContactName, "o nome do contato");
        c.Charter = Text.Required(req.Charter, "o alvará");
        c.CharterExpiration = req.CharterExpiration.Date;
        c.IneaCode = Text.Clean(req.IneaCode);
        c.EnvironmentalPermit = Text.Clean(req.EnvironmentalPermit);
        c.EnvironmentalPermitExpiration = req.EnvironmentalPermitExpiration?.Date;
        c.LegalRepresentative = Text.Clean(req.LegalRepresentative);
        c.UseBaitings = req.UseBaitings;
        c.UsePlaqueOfGlue = req.UsePlaqueOfGlue;
        c.UseLuminousTrap = req.UseLuminousTrap;
        c.UsePheromoneTrap = req.UsePheromoneTrap;
        c.LastUpdate = clock.Now;

        await db.SaveChangesAsync(ct);
        return CompanyDto.From(c);
    }

    /// <summary>Grava o responsável técnico de cada UF (equivalente ao "updateAll" do legado).</summary>
    private static async Task<IResult> SaveTechnicalManagers(List<TechnicalManagerItem> items, Access access, TenantDbContext db, CancellationToken ct)
    {
        var companyId = await access.CurrentCompanyIdAsync(ct);
        var existing = await db.TechnicalManagers.Where(t => t.CompanyId == companyId).ToListAsync(ct);

        foreach (var item in items.Where(i => !string.IsNullOrWhiteSpace(i.State)))
        {
            var state = item.State.Trim().ToUpperInvariant();
            var tm = existing.FirstOrDefault(t => t.State.Trim().Equals(state, StringComparison.OrdinalIgnoreCase));
            if (tm is null)
            {
                tm = new TechnicalManager { CompanyId = companyId, State = state };
                db.TechnicalManagers.Add(tm);
                existing.Add(tm);
            }
            tm.Name = item.Name?.Trim() ?? "";
            tm.Register = item.Register?.Trim() ?? "";
        }

        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }
}
