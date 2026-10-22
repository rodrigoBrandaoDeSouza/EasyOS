using System.Security.Claims;
using EasyOS.Api.Common;
using EasyOS.Domain.Entities;

namespace EasyOS.Api.Auth;

public static class EasyClaims
{
    public const string UserId = "uid";
    public const string UserType = "utype";
    public const string Profile = "profile";
    public const string TenantId = "tid";
    public const string CompanyId = "cid";
    public const string CustomerId = "custid";
    /// <summary>Impressão digital do hash de senha: troca de senha invalida as sessões abertas.</summary>
    public const string PasswordStamp = "pstamp";
    public const string ValidatedAt = "vat";
}

public static class UserTypes
{
    public const string Company = "company";
    public const string Customer = "customer";
}

public static class Policies
{
    /// <summary>Usuário da empresa prestadora (administrador ou back office).</summary>
    public const string Company = nameof(Company);
    public const string CompanyAdmin = nameof(CompanyAdmin);
    /// <summary>Qualquer usuário autenticado do tenant (empresa ou cliente).</summary>
    public const string TenantUser = nameof(TenantUser);
    /// <summary>Administrador do cliente, ou usuário da empresa (que administra todos os clientes).</summary>
    public const string CustomerAdmin = nameof(CustomerAdmin);
    public const string Platform = nameof(Platform);
}

/// <summary>Usuário autenticado na requisição atual, com o escopo de dados que ele pode ver.</summary>
public sealed class CurrentUser(IHttpContextAccessor accessor)
{
    private ClaimsPrincipal Principal => accessor.HttpContext?.User ?? new ClaimsPrincipal();

    public bool IsAuthenticated => Principal.Identity?.IsAuthenticated == true;
    public int Id => int.Parse(Principal.FindFirstValue(EasyClaims.UserId) ?? throw new ForbiddenException());
    public string Type => Principal.FindFirstValue(EasyClaims.UserType) ?? "";
    public int Profile => int.Parse(Principal.FindFirstValue(EasyClaims.Profile) ?? "0");
    public string Name => Principal.FindFirstValue(ClaimTypes.Name) ?? "";

    public bool IsCompanyUser => Type == UserTypes.Company;
    public bool IsCustomerUser => Type == UserTypes.Customer;
    public bool IsCompanyAdmin => IsCompanyUser && Profile == (int)CompanyProfile.Administrator;
    public bool IsCustomerAdmin => IsCustomerUser && Profile == (int)CustomerProfile.BackOffice;

    /// <summary>
    /// Empresa do usuário. No legado, idcompany = 0 significa "todas as empresas do banco";
    /// nesse caso devolve null.
    /// </summary>
    public int? CompanyId
    {
        get
        {
            var value = Principal.FindFirstValue(EasyClaims.CompanyId);
            return value is null || value == "0" ? null : int.Parse(value);
        }
    }

    /// <summary>Cliente do usuário (apenas para usuários de cliente).</summary>
    public int? CustomerId
    {
        get
        {
            var value = Principal.FindFirstValue(EasyClaims.CustomerId);
            return value is null ? null : int.Parse(value);
        }
    }

    public int RequireCustomerId() => CustomerId ?? throw new ForbiddenException();
}
