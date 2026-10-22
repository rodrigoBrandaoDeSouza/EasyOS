namespace EasyOS.Domain.Platform;

/// <summary>
/// Cliente da EasyOS (empresa que contrata o sistema). Cada tenant tem o próprio banco
/// de dados e é identificado pelo domínio usado no acesso.
/// </summary>
public class Tenant
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Identificador curto e estável (ex.: "termite"). Usado como prefixo no storage.</summary>
    public string Slug { get; set; } = "";
    public string Name { get; set; } = "";
    public bool Active { get; set; } = true;

    /// <summary>Connection string do banco do tenant, cifrada com Data Protection.</summary>
    public string ConnectionStringProtected { get; set; } = "";

    public TenantBranding Branding { get; set; } = new();
    public TenantSettings Settings { get; set; } = new();

    public List<TenantDomain> Domains { get; set; } = [];

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class TenantDomain
{
    public int Id { get; set; }
    public Guid TenantId { get; set; }

    /// <summary>Host em minúsculas, sem porta (ex.: "os.termite.com.br").</summary>
    public string Host { get; set; } = "";
    public bool IsPrimary { get; set; }
}

/// <summary>Identidade visual do tenant (substitui o build por cliente do frontend legado).</summary>
public class TenantBranding
{
    public string PrimaryColor { get; set; } = "#37ab8c";
    public string HeadingColor { get; set; } = "#333333";

    /// <summary>Rótulo da sexta coluna de pragas nos relatórios ("Outros" na maioria dos clientes).</summary>
    public string OtherPlaguesLabel { get; set; } = "Outros";

    /// <summary>Chaves no storage dos arquivos de marca (null = usar o padrão EasyOS).</summary>
    public string? LogoKey { get; set; }
    public string? LoginBackgroundKey { get; set; }
    public string? FaviconKey { get; set; }

    /// <summary>Logo usado no cabeçalho dos PDFs (OS, certificado, não conformidades).</summary>
    public string? ReportLogoKey { get; set; }
}

/// <summary>Configurações que no legado ficavam no Web.config de cada cliente.</summary>
public class TenantSettings
{
    /// <summary>SEQUENCE_BY_COSTUMER: numeração da OS por cliente (true) ou por empresa (false).</summary>
    public bool OrderSequenceByCustomer { get; set; } = true;

    /// <summary>CERTIFICATEDATEONVISIT: exige a data do certificado ao agendar/editar visitas.</summary>
    public bool CertificateDateOnVisit { get; set; }

    /// <summary>Flags do app mobile (customs no login legado). DayVisitsOnly é true quando ausente no legado.</summary>
    public bool DayVisitsOnly { get; set; } = true;
    public bool TravelTime { get; set; }
    public bool LockTime { get; set; }

    public SmtpSettings Smtp { get; set; } = new();

    /// <summary>Flags CFG_* devolvidas ao app no login.</summary>
    public List<TenantFeature> Features { get; set; } = [];
}

public class TenantFeature
{
    /// <summary>Nome em maiúsculas, com prefixo CFG_ (ex.: CFG_SHOW_PRICES).</summary>
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
}

public class SmtpSettings
{
    public string? Host { get; set; }
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string? UserName { get; set; }

    /// <summary>Senha cifrada com Data Protection.</summary>
    public string? PasswordProtected { get; set; }
    public string? FromAddress { get; set; }
    public string? FromName { get; set; }
    public string? Subject { get; set; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(FromAddress);
}

/// <summary>Usuário do painel central da EasyOS (gerencia os tenants).</summary>
public class PlatformUser
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = "";
    public string Name { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public bool Active { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
