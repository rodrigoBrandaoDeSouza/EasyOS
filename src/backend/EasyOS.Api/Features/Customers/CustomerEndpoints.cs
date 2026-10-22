using EasyOS.Api.Auth;
using EasyOS.Api.Common;
using EasyOS.Api.Features.Users;
using EasyOS.Domain.Entities;
using EasyOS.Infrastructure;
using EasyOS.Infrastructure.Geocoding;
using EasyOS.Infrastructure.Persistence;
using EasyOS.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace EasyOS.Api.Features.Customers;

public sealed record CustomerDto(
    int Id, string TradingName, string CompanyName, string? CpfCnpj,
    string Address, int Number, string Complement, string Neighborhood, string City, string State, string Zipcode,
    string Phone, string ContactName, string? ContactFunction, string? Fax, string? Email,
    string? ActivityType, bool ClientHasContract, int CertificateDate, string? Observation,
    float Latitude, float Longitude, bool Active, int CompanyId)
{
    public bool MissingGeolocation => Latitude == 0 || Longitude == 0;

    public static CustomerDto From(Customer c) => new(
        c.Id, c.TradingName, c.CompanyName, c.CpfCnpj, c.Address, c.Number, c.Complement, c.Neighborhood, c.City, c.State, c.Zipcode,
        c.Phone, c.ContactName, c.ContactFunction, c.Fax, c.Email, c.ActivityType, c.ClientHasContract ?? false, c.CertificateDate,
        c.Observation, c.Latitude, c.Longitude, c.Active, c.CompanyId);
}

public sealed record CustomerRequest(
    string TradingName, string CompanyName, string? CpfCnpj,
    string Address, int Number, string? Complement, string Neighborhood, string City, string State, string Zipcode,
    string Phone, string ContactName, string? ContactFunction, string? Fax, string? Email,
    string? ActivityType, bool ClientHasContract, int CertificateDate, string? Observation);

public sealed record NewCustomerUser(string Name, string Email, string? Department, int Profile, string Password);

public sealed record CustomerCreateRequest(CustomerRequest Customer, NewCustomerUser User);

public static class CustomerEndpoints
{
    public static void MapCustomerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/customers").WithTags("Clientes");

        group.MapGet("/", List).RequireAuthorization(Policies.Company);
        group.MapGet("/lookup", Lookup).RequireAuthorization(Policies.Company);
        group.MapGet("/{id:int}", Get).RequireAuthorization(Policies.TenantUser);
        group.MapPost("/", Create).RequireAuthorization(Policies.Company);
        // Usuário da empresa edita tudo; administrador do cliente edita os dados do próprio cadastro.
        group.MapPut("/{id:int}", Update).RequireAuthorization(Policies.CustomerAdmin);
        group.MapPost("/{id:int}/status", ChangeStatus).RequireAuthorization(Policies.Company);
    }

    private static async Task<PagedResult<CustomerDto>> List(
        string? q, bool? active, int? page, int? pageSize, Access access, TenantDbContext db, CancellationToken ct)
    {
        var query = access.Customers().WhereContains(db, c => c.TradingName, q);
        if (active is not null) query = query.Where(c => c.Active == active);
        var result = await query.OrderBy(c => c.Id).ToPagedAsync(page, pageSize, ct);
        return new PagedResult<CustomerDto>(result.Items.Select(CustomerDto.From).ToList(), result.Page, result.PageSize, result.TotalItems);
    }

    private static async Task<List<LookupItem>> Lookup(bool? onlyActive, Access access, CancellationToken ct)
    {
        var query = access.Customers();
        if (onlyActive ?? false) query = query.Where(c => c.Active);
        return await query.OrderBy(c => c.TradingName).Select(c => new LookupItem(c.Id, c.TradingName, c.Active)).ToListAsync(ct);
    }

    private static async Task<CustomerDto> Get(int id, Access access, CancellationToken ct) =>
        CustomerDto.From(await access.CustomerAsync(id, ct));

    private static async Task<IResult> Create(
        CustomerCreateRequest req, Access access, TenantDbContext db, IGeocoder geocoder, IClock clock, CancellationToken ct)
    {
        var companyId = await access.CurrentCompanyIdAsync(ct);
        var data = req.Customer ?? throw new BusinessException("Dados do cliente não enviados.");
        var userReq = req.User ?? throw new BusinessException("Usuário não enviado");
        var email = CompanyUserEndpoints.ValidateEmail(userReq.Email);
        AuthEndpoints.ValidateNewPassword(userReq.Password);
        if (!Enum.IsDefined(typeof(CustomerProfile), userReq.Profile)) throw new BusinessException("Perfil inválido.");

        var cpf = Text.Required(data.CpfCnpj, "o CNPJ/CPF");
        var tradingName = Text.Required(data.TradingName, "o nome fantasia");
        if (await db.Customers.AnyAsync(c => c.TradingName == tradingName && c.CpfCnpj == cpf && c.Active, ct))
            throw new BusinessException("Já existe um cliente ativo com este nome fantasia e CNPJ/CPF.");
        if (await db.CustomerUsers.AnyAsync(u => u.Email.ToLower() == email && u.Active, ct))
            throw new BusinessException("Email já cadastrado.");

        var now = clock.Now;
        var customer = new Customer { CompanyId = companyId, Active = true, CreatedDate = now };
        await Apply(customer, data, fullEdit: true, geocoder, clock, ct);

        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            db.Customers.Add(customer);
            await db.SaveChangesAsync(ct);
            db.CustomerUsers.Add(new CustomerUser
            {
                CustomerId = customer.Id,
                Name = Text.Required(userReq.Name, "o nome do usuário"),
                Email = email,
                Department = Text.Clean(userReq.Department),
                ProfileLevel = (CustomerProfile)userReq.Profile,
                Password = LegacyPasswordHasher.Hash(userReq.Password),
                Active = true,
                ChangePassword = false,
                CreatedDate = now,
            });
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });

        return Results.Created($"/api/customers/{customer.Id}", CustomerDto.From(customer));
    }

    private static async Task<CustomerDto> Update(
        int id, CustomerRequest req, Access access, TenantDbContext db, IGeocoder geocoder, IClock clock, CancellationToken ct)
    {
        await access.EnsureCustomerAsync(id, ct);
        var customer = await db.Customers.FirstAsync(c => c.Id == id, ct);
        // No legado o cliente não altera CNPJ/CPF nem a validade do certificado (campos somente leitura).
        await Apply(customer, req, fullEdit: access.User.IsCompanyUser, geocoder, clock, ct);
        await db.SaveChangesAsync(ct);
        return CustomerDto.From(customer);
    }

    private static async Task<IResult> ChangeStatus(int id, StatusRequest req, Access access, TenantDbContext db, IClock clock, CancellationToken ct)
    {
        await access.EnsureCustomerAsync(id, ct);
        var customer = await db.Customers.FirstAsync(c => c.Id == id, ct);
        customer.Active = req.Active;
        customer.LastUpdate = clock.Now;
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task Apply(Customer c, CustomerRequest r, bool fullEdit, IGeocoder geocoder, IClock clock, CancellationToken ct)
    {
        var addressChanged =
            c.Address != r.Address || c.Number != r.Number || c.Neighborhood != r.Neighborhood ||
            c.City != r.City || c.State != r.State || c.Zipcode != r.Zipcode || c.Latitude == 0 || c.Longitude == 0;

        c.TradingName = Text.Required(r.TradingName, "o nome fantasia");
        c.CompanyName = Text.Required(r.CompanyName, "a razão social");
        c.Address = Text.Required(r.Address, "o logradouro");
        c.Number = r.Number;
        c.Complement = Text.Clean(r.Complement) ?? "";
        c.Neighborhood = Text.Required(r.Neighborhood, "o bairro");
        c.City = Text.Required(r.City, "a cidade");
        c.State = Text.Required(r.State, "o estado").ToUpperInvariant();
        c.Zipcode = Text.Required(r.Zipcode, "o CEP");
        c.Phone = Text.Required(r.Phone, "o telefone");
        c.ContactName = Text.Required(r.ContactName, "o contato");
        c.ContactFunction = Text.Clean(r.ContactFunction);
        c.Fax = Text.Clean(r.Fax);
        c.Email = Text.Clean(r.Email);
        c.ActivityType = Text.Clean(r.ActivityType);
        c.Observation = Text.Clean(r.Observation) ?? "";
        if (c.Observation.Length > 1000) throw new BusinessException("As observações devem ter até 1000 caracteres.");
        c.LastUpdate = clock.Now;

        if (fullEdit)
        {
            c.CpfCnpj = Text.Required(r.CpfCnpj, "o CNPJ/CPF");
            c.CertificateDate = r.CertificateDate;
            c.ClientHasContract = r.ClientHasContract;
        }

        if (addressChanged)
        {
            var point = await geocoder.GeocodeAsync($"{c.Address}, {c.Number} - {c.Neighborhood}, {c.City} - {c.State}, {c.Zipcode}, Brasil", ct);
            c.Latitude = point?.Latitude ?? 0;
            c.Longitude = point?.Longitude ?? 0;
        }
    }
}
