using EasyOS.Api.Auth;
using EasyOS.Api.Common;
using EasyOS.Domain.Entities;
using EasyOS.Infrastructure;
using EasyOS.Infrastructure.Persistence;
using EasyOS.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace EasyOS.Api.Features.Users;

public sealed record CustomerUserDto(int Id, int CustomerId, string Name, string Email, string? Department, int Profile, bool Active, DateTime CreatedDate);

public sealed record CustomerUserRequest(string Name, string Email, string? Department, int Profile, string? Password);

/// <summary>Usuários de um cliente. Gerenciados pela empresa ou pelo administrador do próprio cliente.</summary>
public static class CustomerUserEndpoints
{
    public static void MapCustomerUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/customers/{customerId:int}/users").WithTags("Usuários do cliente")
            .RequireAuthorization(Policies.CustomerAdmin);

        group.MapGet("/", List);
        group.MapPost("/", Create);
        group.MapPut("/{id:int}", Update);
        group.MapPost("/{id:int}/status", ChangeStatus);
    }

    private static async Task<List<CustomerUserDto>> List(int customerId, string? q, Access access, TenantDbContext db, CancellationToken ct)
    {
        await access.EnsureCustomerAsync(customerId, ct);
        return await db.CustomerUsers.AsNoTracking()
            .Where(u => u.CustomerId == customerId)
            .WhereContains(db, u => u.Name, q)
            .OrderBy(u => u.Name)
            .Select(u => new CustomerUserDto(u.Id, u.CustomerId, u.Name, u.Email, u.Department, (int)u.ProfileLevel, u.Active, u.CreatedDate))
            .ToListAsync(ct);
    }

    private static async Task<IResult> Create(int customerId, CustomerUserRequest req, Access access, TenantDbContext db, IClock clock, CancellationToken ct)
    {
        await access.EnsureCustomerAsync(customerId, ct);
        var email = CompanyUserEndpoints.ValidateEmail(req.Email);
        if (await db.CustomerUsers.AnyAsync(u => u.Email.ToLower() == email && u.Active, ct))
            throw new BusinessException("Email já cadastrado.");
        AuthEndpoints.ValidateNewPassword(req.Password);

        var user = new CustomerUser
        {
            CustomerId = customerId,
            Password = LegacyPasswordHasher.Hash(req.Password!),
            Active = true,
            CreatedDate = clock.Now,
        };
        Apply(user, req, email);
        db.CustomerUsers.Add(user);
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/customers/{customerId}/users/{user.Id}", ToDto(user));
    }

    private static async Task<CustomerUserDto> Update(int customerId, int id, CustomerUserRequest req, Access access, TenantDbContext db, CancellationToken ct)
    {
        await access.EnsureCustomerAsync(customerId, ct);
        var user = await db.CustomerUsers.FirstOrDefaultAsync(u => u.Id == id && u.CustomerId == customerId, ct)
            ?? throw new NotFoundException("Usuário não encontrado.");
        var email = CompanyUserEndpoints.ValidateEmail(req.Email);
        if (await db.CustomerUsers.AnyAsync(u => u.Id != id && u.Email.ToLower() == email && u.Active, ct))
            throw new BusinessException("Email já cadastrado.");
        if (access.User.IsCustomerUser && id == access.User.Id && req.Profile != (int)CustomerProfile.BackOffice)
            throw new BusinessException("Você não pode remover seu próprio perfil de administrador.");

        Apply(user, req, email);
        if (!string.IsNullOrEmpty(req.Password))
        {
            AuthEndpoints.ValidateNewPassword(req.Password);
            user.Password = LegacyPasswordHasher.Hash(req.Password);
        }
        await db.SaveChangesAsync(ct);
        return ToDto(user);
    }

    private static async Task<IResult> ChangeStatus(int customerId, int id, StatusRequest req, Access access, TenantDbContext db, CancellationToken ct)
    {
        await access.EnsureCustomerAsync(customerId, ct);
        var user = await db.CustomerUsers.FirstOrDefaultAsync(u => u.Id == id && u.CustomerId == customerId, ct)
            ?? throw new NotFoundException("Usuário não encontrado.");
        if (access.User.IsCustomerUser && id == access.User.Id && !req.Active)
            throw new BusinessException("Você não pode desativar o próprio usuário.");
        if (req.Active && await db.CustomerUsers.AnyAsync(u => u.Id != id && u.Email.ToLower() == user.Email.ToLower() && u.Active, ct))
            throw new BusinessException("Já existe outro usuário ativo com este e-mail.");
        user.Active = req.Active;
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static void Apply(CustomerUser user, CustomerUserRequest req, string email)
    {
        if (!Enum.IsDefined(typeof(CustomerProfile), req.Profile)) throw new BusinessException("Perfil inválido.");
        user.Name = Text.Required(req.Name, "o nome");
        user.Email = email;
        user.Department = Text.Clean(req.Department);
        user.ProfileLevel = (CustomerProfile)req.Profile;
    }

    private static CustomerUserDto ToDto(CustomerUser u) =>
        new(u.Id, u.CustomerId, u.Name, u.Email, u.Department, (int)u.ProfileLevel, u.Active, u.CreatedDate);
}
