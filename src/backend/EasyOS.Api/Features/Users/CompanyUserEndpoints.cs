using EasyOS.Api.Auth;
using EasyOS.Api.Common;
using EasyOS.Domain.Entities;
using EasyOS.Infrastructure;
using EasyOS.Infrastructure.Persistence;
using EasyOS.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace EasyOS.Api.Features.Users;

public sealed record CompanyUserDto(
    int Id, string Name, string Email, string? Phone, string Department, int Profile, bool Active, bool HasSignature, DateTime CreatedDate);

public sealed record CompanyUserRequest(
    string Name, string Email, string? Phone, string Department, int Profile,
    // Na edição, vazio mantém a senha atual.
    string? Password,
    // Imagem da assinatura em data URL (base64). null mantém a atual; string vazia remove.
    string? Signature);

public sealed record LookupItem(int Id, string Name, bool Active);

public sealed record StatusRequest(bool Active);

public static class CompanyUserEndpoints
{
    private const int MaxSignatureLength = 2 * 1024 * 1024;

    public static void MapCompanyUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/company-users").WithTags("Usuários da empresa");

        group.MapGet("/", List).RequireAuthorization(Policies.Company);
        group.MapGet("/lookup", Lookup).RequireAuthorization(Policies.Company);
        group.MapGet("/{id:int}", Get).RequireAuthorization(Policies.Company);
        group.MapGet("/{id:int}/signature", GetSignature).RequireAuthorization(Policies.Company);
        group.MapPost("/", Create).RequireAuthorization(Policies.CompanyAdmin);
        group.MapPut("/{id:int}", Update).RequireAuthorization(Policies.CompanyAdmin);
        group.MapPost("/{id:int}/status", ChangeStatus).RequireAuthorization(Policies.CompanyAdmin);
    }

    private static async Task<List<CompanyUserDto>> List(string? q, Access access, TenantDbContext db, CancellationToken ct) =>
        await access.CompanyUsers()
            .WhereContains(db, u => u.Name, q)
            .OrderBy(u => u.Name)
            .Select(u => new CompanyUserDto(u.Id, u.Name, u.Email, u.Phone, u.Department, (int)u.ProfileLevel, u.Active,
                u.Signature != null, u.CreatedDate))
            .ToListAsync(ct);

    /// <summary>Lista para autocomplete. role=technician restringe a técnicos (como "getTechnicians" do legado).</summary>
    private static async Task<List<LookupItem>> Lookup(string? role, bool? onlyActive, Access access, CancellationToken ct)
    {
        var query = access.CompanyUsers();
        if (role == "technician") query = query.Where(u => u.ProfileLevel == CompanyProfile.Technician);
        if (onlyActive ?? true) query = query.Where(u => u.Active);
        return await query.OrderBy(u => u.Name).Select(u => new LookupItem(u.Id, u.Name, u.Active)).ToListAsync(ct);
    }

    private static async Task<CompanyUserDto> Get(int id, Access access, CancellationToken ct) =>
        await access.CompanyUsers().Where(u => u.Id == id)
            .Select(u => new CompanyUserDto(u.Id, u.Name, u.Email, u.Phone, u.Department, (int)u.ProfileLevel, u.Active,
                u.Signature != null, u.CreatedDate))
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException("Usuário não encontrado.");

    private static async Task<IResult> GetSignature(int id, Access access, CancellationToken ct)
    {
        var signature = await access.CompanyUsers().Where(u => u.Id == id).Select(u => u.Signature).FirstOrDefaultAsync(ct);
        return string.IsNullOrEmpty(signature) ? Results.NoContent() : Results.Ok(new { signature });
    }

    private static async Task<IResult> Create(CompanyUserRequest req, Access access, TenantDbContext db, IClock clock, CancellationToken ct)
    {
        var companyId = await access.CurrentCompanyIdAsync(ct);
        var email = ValidateEmail(req.Email);
        if (await db.CompanyUsers.AnyAsync(u => u.Email.ToLower() == email && u.Active, ct))
            throw new BusinessException("Já existe um email cadastrado para essa empresa.");
        AuthEndpoints.ValidateNewPassword(req.Password);

        var user = new CompanyUser
        {
            CompanyId = companyId,
            Password = LegacyPasswordHasher.Hash(req.Password!),
            Active = true,
            CreatedDate = clock.Now,
        };
        Apply(user, req, email, clock);
        db.CompanyUsers.Add(user);
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/company-users/{user.Id}", ToDto(user));
    }

    private static async Task<CompanyUserDto> Update(int id, CompanyUserRequest req, Access access, TenantDbContext db, IClock clock, CancellationToken ct)
    {
        await EnsureInScope(id, access, ct);
        var user = await db.CompanyUsers.FirstAsync(u => u.Id == id, ct);
        var email = ValidateEmail(req.Email);
        if (await db.CompanyUsers.AnyAsync(u => u.Id != id && u.Email.ToLower() == email && u.Active, ct))
            throw new BusinessException("Já existe um email cadastrado para essa empresa.");
        if (id == access.User.Id && req.Profile != (int)CompanyProfile.Administrator)
            throw new BusinessException("Você não pode remover seu próprio perfil de administrador.");

        Apply(user, req, email, clock);
        if (!string.IsNullOrEmpty(req.Password))
        {
            AuthEndpoints.ValidateNewPassword(req.Password);
            user.Password = LegacyPasswordHasher.Hash(req.Password);
        }
        await db.SaveChangesAsync(ct);
        return ToDto(user);
    }

    private static async Task<IResult> ChangeStatus(int id, StatusRequest req, Access access, TenantDbContext db, IClock clock, CancellationToken ct)
    {
        await EnsureInScope(id, access, ct);
        if (id == access.User.Id && !req.Active) throw new BusinessException("Você não pode desativar o próprio usuário.");
        var user = await db.CompanyUsers.FirstAsync(u => u.Id == id, ct);
        if (req.Active && await db.CompanyUsers.AnyAsync(u => u.Id != id && u.Email.ToLower() == user.Email.ToLower() && u.Active, ct))
            throw new BusinessException("Já existe outro usuário ativo com este e-mail.");
        user.Active = req.Active;
        user.LastUpdate = clock.Now;
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static void Apply(CompanyUser user, CompanyUserRequest req, string email, IClock clock)
    {
        if (!Enum.IsDefined(typeof(CompanyProfile), req.Profile)) throw new BusinessException("Perfil inválido.");
        user.Name = Text.Required(req.Name, "o nome");
        user.Email = email;
        user.Phone = Text.Clean(req.Phone);
        user.Department = Text.Required(req.Department, "o departamento");
        user.ProfileLevel = (CompanyProfile)req.Profile;
        user.LastUpdate = clock.Now;

        if (req.Signature is not null)
        {
            if (req.Signature.Length == 0) user.Signature = null;
            else if (!req.Signature.StartsWith("data:image/", StringComparison.Ordinal) || req.Signature.Length > MaxSignatureLength)
                throw new BusinessException("A assinatura deve ser uma imagem de até 1,5 MB.");
            else user.Signature = req.Signature;
        }
    }

    private static async Task EnsureInScope(int id, Access access, CancellationToken ct)
    {
        if (!await access.CompanyUsers().AnyAsync(u => u.Id == id, ct)) throw new NotFoundException("Usuário não encontrado.");
    }

    public static string ValidateEmail(string? email)
    {
        var e = Text.Required(email, "o e-mail").ToLowerInvariant();
        if (!System.Net.Mail.MailAddress.TryCreate(e, out _)) throw new BusinessException("E-mail inválido.");
        return e;
    }

    private static CompanyUserDto ToDto(CompanyUser u) =>
        new(u.Id, u.Name, u.Email, u.Phone, u.Department, (int)u.ProfileLevel, u.Active, !string.IsNullOrEmpty(u.Signature), u.CreatedDate);
}
