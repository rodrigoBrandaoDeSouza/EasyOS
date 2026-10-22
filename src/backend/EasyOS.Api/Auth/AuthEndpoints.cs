using System.Globalization;
using System.Security.Claims;
using EasyOS.Api.Common;
using EasyOS.Domain.Entities;
using EasyOS.Infrastructure;
using EasyOS.Infrastructure.Email;
using EasyOS.Infrastructure.MultiTenancy;
using EasyOS.Infrastructure.Persistence;
using EasyOS.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;

namespace EasyOS.Api.Auth;

public sealed record LoginRequest(string Email, string Password);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public sealed record ForgotPasswordRequest(string Email);

public sealed record MeResponse(
    int Id,
    string Name,
    string Email,
    string Type,
    int Profile,
    int? CompanyId,
    int? CustomerId,
    string? CustomerName);

public static class AuthEndpoints
{
    private const string InvalidCredentials = "Usuário ou senha inválidos.";

    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Autenticação");

        group.MapPost("/login", Login).AllowAnonymous().RequireRateLimiting("login");
        group.MapPost("/logout", async (HttpContext http) =>
        {
            await http.SignOutAsync(AuthSchemes.Tenant);
            return Results.NoContent();
        }).AllowAnonymous();
        group.MapGet("/me", Me).RequireAuthorization(Policies.TenantUser);
        group.MapPost("/change-password", ChangePassword).RequireAuthorization(Policies.TenantUser);
        group.MapPost("/forgot-password", ForgotPassword).AllowAnonymous().RequireRateLimiting("login");
    }

    private static async Task<IResult> Login(
        LoginRequest req, HttpContext http, TenantDbContext db, ITenantContext tenant, IClock clock, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrEmpty(req.Password))
            throw new BusinessException(InvalidCredentials);

        var email = req.Email.Trim().ToLower();
        var ip = http.Connection.RemoteIpAddress?.ToString() ?? "";

        // 1) Usuário da empresa prestadora.
        var companyUser = await db.CompanyUsers.AsTracking()
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email && u.Active, ct);
        if (companyUser is not null && LegacyPasswordHasher.Verify(req.Password, companyUser.Password))
        {
            if (companyUser.ProfileLevel == CompanyProfile.Technician)
                throw new BusinessException("Usuário não tem permissão suficiente para logar no sistema.");

            db.CompanyLogins.Add(new CompanyLoginAudit
            {
                CompanyUserId = companyUser.Id, Ip = Truncate(ip, 50), CreatedDate = clock.Now, Action = "Login",
                Description = $"O usuário de id {companyUser.Id} fez login com o ip {ip}",
            });
            await db.SaveChangesAsync(ct);

            await SignIn(http, tenant.Tenant, clock, companyUser.Id, companyUser.Name, companyUser.Email, UserTypes.Company,
                (int)companyUser.ProfileLevel, companyUser.Password, companyId: companyUser.CompanyId, customerId: null);
            return Results.Ok(ToMe(companyUser));
        }

        // 2) Usuário do cliente final.
        var customerUser = await db.CustomerUsers.AsTracking().Include(u => u.Customer)
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email && u.Active, ct);
        if (customerUser is not null && LegacyPasswordHasher.Verify(req.Password, customerUser.Password))
        {
            if (customerUser.Customer is not { Active: true })
                throw new BusinessException("Sua empresa está com a conta desativada.");

            db.CustomerLogins.Add(new CustomerLoginAudit
            {
                CustomerUserId = customerUser.Id, Ip = Truncate(ip, 50), CreatedDate = clock.Now, Action = "Login",
                Description = $"O usuário de id {customerUser.Id} fez login com o ip {ip}",
            });
            await db.SaveChangesAsync(ct);

            await SignIn(http, tenant.Tenant, clock, customerUser.Id, customerUser.Name, customerUser.Email, UserTypes.Customer,
                (int)customerUser.ProfileLevel, customerUser.Password, companyId: customerUser.Customer.CompanyId, customerId: customerUser.CustomerId);
            return Results.Ok(ToMe(customerUser));
        }

        throw new BusinessException(InvalidCredentials);
    }

    private static async Task<IResult> Me(CurrentUser user, TenantDbContext db, CancellationToken ct)
    {
        if (user.IsCompanyUser)
        {
            var cu = await db.CompanyUsers.AsNoTracking().FirstOrDefaultAsync(u => u.Id == user.Id, ct) ?? throw new NotFoundException();
            return Results.Ok(ToMe(cu));
        }
        var c = await db.CustomerUsers.AsNoTracking().Include(u => u.Customer).FirstOrDefaultAsync(u => u.Id == user.Id, ct) ?? throw new NotFoundException();
        return Results.Ok(ToMe(c));
    }

    private static async Task<IResult> ChangePassword(
        ChangePasswordRequest req, CurrentUser user, TenantDbContext db, HttpContext http, ITenantContext tenant, IClock clock, CancellationToken ct)
    {
        ValidateNewPassword(req.NewPassword);

        if (user.IsCompanyUser)
        {
            var u = await db.CompanyUsers.AsTracking().FirstOrDefaultAsync(x => x.Id == user.Id, ct) ?? throw new NotFoundException();
            if (!LegacyPasswordHasher.Verify(req.CurrentPassword, u.Password)) throw new BusinessException("Senha atual incorreta.");
            u.Password = LegacyPasswordHasher.Hash(req.NewPassword);
            u.ChangePassword = false;
            u.LastUpdate = clock.Now;
            await db.SaveChangesAsync(ct);
            await SignIn(http, tenant.Tenant, clock, u.Id, u.Name, u.Email, UserTypes.Company, (int)u.ProfileLevel, u.Password, u.CompanyId, null);
        }
        else
        {
            var u = await db.CustomerUsers.AsTracking().Include(x => x.Customer).FirstOrDefaultAsync(x => x.Id == user.Id, ct) ?? throw new NotFoundException();
            if (!LegacyPasswordHasher.Verify(req.CurrentPassword, u.Password)) throw new BusinessException("Senha atual incorreta.");
            u.Password = LegacyPasswordHasher.Hash(req.NewPassword);
            u.ChangePassword = false;
            await db.SaveChangesAsync(ct);
            await SignIn(http, tenant.Tenant, clock, u.Id, u.Name, u.Email, UserTypes.Customer, (int)u.ProfileLevel, u.Password, u.Customer!.CompanyId, u.CustomerId);
        }
        return Results.NoContent();
    }

    /// <summary>
    /// Gera uma senha nova e envia por e-mail (mesmo fluxo do legado). Responde sempre 204
    /// para não revelar quais e-mails existem.
    /// </summary>
    private static async Task<IResult> ForgotPassword(
        ForgotPasswordRequest req, TenantDbContext db, IEmailSender email, ITenantContext tenant, IClock clock,
        ILogger<ForgotPasswordRequest> logger, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Email)) return Results.NoContent();
        var address = req.Email.Trim().ToLower();
        var newPassword = LegacyPasswordHasher.GenerateRandomPassword();

        var companyUser = await db.CompanyUsers.AsTracking().FirstOrDefaultAsync(u => u.Email.ToLower() == address && u.Active, ct);
        var customerUser = companyUser is null
            ? await db.CustomerUsers.AsTracking().FirstOrDefaultAsync(u => u.Email.ToLower() == address && u.Active, ct)
            : null;
        if (companyUser is null && customerUser is null) return Results.NoContent();

        if (companyUser is not null)
        {
            companyUser.Password = LegacyPasswordHasher.Hash(newPassword);
            companyUser.ChangePassword = true;
            companyUser.LastUpdate = clock.Now;
        }
        else
        {
            customerUser!.Password = LegacyPasswordHasher.Hash(newPassword);
            customerUser.ChangePassword = true;
        }

        try
        {
            await email.SendAsync(new EmailMessage(
                [companyUser?.Email ?? customerUser!.Email],
                tenant.Tenant.Settings.Smtp.Subject ?? $"{tenant.Tenant.Name} - nova senha",
                $"<p>Sua senha EasyOS foi redefinida. Sua nova senha é <strong>{newPassword}</strong>.</p>" +
                "<p>Recomendamos alterá-la após o primeiro acesso.</p>"), ct);
            await db.SaveChangesAsync(ct);
        }
        catch (Exception e)
        {
            // Sem e-mail enviado, a senha antiga é mantida.
            logger.LogError(e, "Falha ao enviar e-mail de redefinição de senha");
            throw new BusinessException("Não foi possível enviar o e-mail de redefinição. Tente novamente mais tarde.");
        }
        return Results.NoContent();
    }

    public static void ValidateNewPassword(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < 6)
            throw new BusinessException("A nova senha deve ter pelo menos 6 caracteres.");
        // O hash legado usa ASCII: caracteres fora dele virariam '?' e enfraqueceriam a senha.
        if (password.Any(c => c > 127))
            throw new BusinessException("A senha não pode conter acentos ou caracteres especiais fora do padrão ASCII.");
    }

    private static Task SignIn(HttpContext http, TenantInfo tenant, IClock clock, int userId, string name, string email,
        string type, int profile, string passwordHash, int? companyId, int? customerId)
    {
        var claims = new List<Claim>
        {
            new(EasyClaims.UserId, userId.ToString(CultureInfo.InvariantCulture)),
            new(ClaimTypes.Name, name),
            new(ClaimTypes.Email, email),
            new(EasyClaims.UserType, type),
            new(EasyClaims.Profile, profile.ToString(CultureInfo.InvariantCulture)),
            new(EasyClaims.TenantId, tenant.Id.ToString()),
            new(EasyClaims.PasswordStamp, AuthSetup.PasswordStamp(passwordHash)),
            new(EasyClaims.ValidatedAt, clock.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture)),
        };
        if (companyId is not null) claims.Add(new(EasyClaims.CompanyId, companyId.Value.ToString(CultureInfo.InvariantCulture)));
        if (customerId is not null) claims.Add(new(EasyClaims.CustomerId, customerId.Value.ToString(CultureInfo.InvariantCulture)));

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, AuthSchemes.Tenant));
        return http.SignInAsync(AuthSchemes.Tenant, principal);
    }

    private static MeResponse ToMe(CompanyUser u) =>
        new(u.Id, u.Name, u.Email, UserTypes.Company, (int)u.ProfileLevel, u.CompanyId == 0 ? null : u.CompanyId, null, null);

    private static MeResponse ToMe(CustomerUser u) =>
        new(u.Id, u.Name, u.Email, UserTypes.Customer, (int)u.ProfileLevel, u.Customer?.CompanyId, u.CustomerId, u.Customer?.TradingName);

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}
