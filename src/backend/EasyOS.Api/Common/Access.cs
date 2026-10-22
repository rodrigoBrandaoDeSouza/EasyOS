using EasyOS.Api.Auth;
using EasyOS.Domain.Entities;
using EasyOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EasyOS.Api.Common;

/// <summary>
/// Regras de escopo de dados. Usuário da empresa enxerga os clientes da sua empresa
/// (ou de todas, quando idcompany = 0 no legado). Usuário de cliente enxerga apenas o próprio cliente.
/// </summary>
public sealed class Access(CurrentUser user, TenantDbContext db)
{
    public CurrentUser User => user;

    /// <summary>Clientes visíveis para o usuário atual.</summary>
    public IQueryable<Customer> Customers()
    {
        var query = db.Customers.AsNoTracking();
        if (user.IsCustomerUser) return query.Where(c => c.Id == user.RequireCustomerId());
        var companyId = user.CompanyId;
        return companyId is null ? query : query.Where(c => c.CompanyId == companyId);
    }

    /// <summary>Garante que o cliente existe e está no escopo do usuário (senão 404, para não revelar existência).</summary>
    public async Task<Customer> CustomerAsync(int customerId, CancellationToken ct) =>
        await Customers().FirstOrDefaultAsync(c => c.Id == customerId, ct) ?? throw new NotFoundException("Cliente não encontrado.");

    public async Task EnsureCustomerAsync(int customerId, CancellationToken ct)
    {
        if (!await Customers().AnyAsync(c => c.Id == customerId, ct))
            throw new NotFoundException("Cliente não encontrado.");
    }

    /// <summary>Empresa do usuário. Para usuários "todas as empresas", exige que a empresa seja informada.</summary>
    public int CompanyIdOrThrow(int? requested = null)
    {
        if (user.CompanyId is { } own)
        {
            if (requested is not null && requested != own) throw new NotFoundException("Empresa não encontrada.");
            return own;
        }
        return requested ?? throw new BusinessException("Informe a empresa.");
    }

    /// <summary>Empresa usada nas telas: a do usuário, ou a primeira ativa do banco para usuários globais.</summary>
    public async Task<int> CurrentCompanyIdAsync(CancellationToken ct)
    {
        if (user.CompanyId is { } own) return own;
        return await db.Companies.AsNoTracking().Where(c => c.Active).OrderBy(c => c.Id).Select(c => c.Id).FirstOrDefaultAsync(ct) is var id and > 0
            ? id
            : throw new NotFoundException("Nenhuma empresa cadastrada.");
    }

    public IQueryable<Visit> Visits()
    {
        var query = db.Visits.AsNoTracking();
        if (user.IsCustomerUser) return query.Where(v => v.CustomerId == user.RequireCustomerId());
        var companyId = user.CompanyId;
        return companyId is null ? query : query.Where(v => v.CompanyId == companyId);
    }

    public IQueryable<Order> Orders()
    {
        var query = db.Orders.AsNoTracking();
        if (user.IsCustomerUser) return query.Where(o => o.CustomerId == user.RequireCustomerId());
        var companyId = user.CompanyId;
        return companyId is null ? query : query.Where(o => o.Customer!.CompanyId == companyId);
    }

    public IQueryable<CompanyUser> CompanyUsers()
    {
        var query = db.CompanyUsers.AsNoTracking();
        var companyId = user.CompanyId;
        return companyId is null ? query : query.Where(u => u.CompanyId == companyId);
    }
}
