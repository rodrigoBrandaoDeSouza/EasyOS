using EasyOS.Domain.Platform;

namespace EasyOS.Infrastructure.MultiTenancy;

/// <summary>Snapshot imutável do tenant usado durante uma requisição (com a connection string já decifrada).</summary>
public sealed record TenantInfo(
    Guid Id,
    string Slug,
    string Name,
    bool Active,
    string ConnectionString,
    string? PrimaryHost,
    TenantBranding Branding,
    TenantSettings Settings);

/// <summary>Tenant da requisição atual. Preenchido pelo middleware de resolução por domínio.</summary>
public interface ITenantContext
{
    TenantInfo? Current { get; }

    /// <summary>Tenant atual; lança se a requisição não pertence a um tenant.</summary>
    TenantInfo Tenant => Current ?? throw new TenantNotResolvedException();
}

public sealed class TenantContext : ITenantContext
{
    public TenantInfo? Current { get; private set; }

    public void Set(TenantInfo tenant)
    {
        if (Current is not null && Current.Id != tenant.Id)
            throw new InvalidOperationException("O tenant da requisição não pode ser trocado depois de definido.");
        Current = tenant;
    }
}

public sealed class TenantNotResolvedException() : Exception("Nenhum tenant associado a esta requisição.");
