using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EasyOS.Domain.Entities;

/// <summary>Usuário da empresa prestadora (administrador, back office ou técnico).</summary>
[Table("CompanyUsers")]
public class CompanyUser
{
    [Key, Column("idcompanyuser")] public int Id { get; set; }
    [Column("phone")] public string? Phone { get; set; }
    [Column("name")] public string Name { get; set; } = "";
    [Column("email")] public string Email { get; set; } = "";
    /// <summary>Hash MD5 em hex maiúsculo. Formato legado, mantido enquanto o app mobile usar a API antiga.</summary>
    [Column("password")] public string Password { get; set; } = "";
    [Column("active")] public bool Active { get; set; }
    [Column("department")] public string Department { get; set; } = "";
    [Column("signature", TypeName = "text")] public string? Signature { get; set; }
    [Column("id_profile_level")] public CompanyProfile ProfileLevel { get; set; }
    [Column("idcompany")] public int CompanyId { get; set; }
    [Column("change_password")] public bool ChangePassword { get; set; }
    [Column("created_date")] public DateTime CreatedDate { get; set; }
    [Column("last_update")] public DateTime LastUpdate { get; set; }
}

public enum CompanyProfile
{
    Administrator = 1,
    BackOffice = 2,
    Technician = 3,
}

/// <summary>Usuário do cliente final (área do cliente).</summary>
[Table("CustomerUsers")]
public class CustomerUser
{
    [Key, Column("idcustomeruser")] public int Id { get; set; }
    [Column("name")] public string Name { get; set; } = "";
    [Column("email")] public string Email { get; set; } = "";
    /// <summary>Hash MD5 em hex maiúsculo (formato legado).</summary>
    [Column("password")] public string Password { get; set; } = "";
    [Column("active")] public bool Active { get; set; }
    [Column("department")] public string? Department { get; set; }
    [Column("id_profile_level")] public CustomerProfile ProfileLevel { get; set; }
    [Column("idcustomer")] public int CustomerId { get; set; }
    [Column("change_password")] public bool ChangePassword { get; set; }
    [Column("created_date")] public DateTime CreatedDate { get; set; }

    public Customer? Customer { get; set; }
}

public enum CustomerProfile
{
    BackOffice = 1,
    Visitor = 2,
}

[Table("CompaniesLogin")]
public class CompanyLoginAudit
{
    [Key, Column("idcompanylogin")] public int Id { get; set; }
    [Column("idcompanyuser")] public int CompanyUserId { get; set; }
    [Column("ip")] public string Ip { get; set; } = "";
    [Column("created_date")] public DateTime CreatedDate { get; set; }
    [Column("action")] public string Action { get; set; } = "";
    [Column("description")] public string Description { get; set; } = "";
}

[Table("CustomersLogin")]
public class CustomerLoginAudit
{
    [Key, Column("idcustomerlogin")] public int Id { get; set; }
    [Column("idcustomeruser")] public int CustomerUserId { get; set; }
    [Column("ip")] public string Ip { get; set; } = "";
    [Column("created_date")] public DateTime CreatedDate { get; set; }
    [Column("action")] public string Action { get; set; } = "";
    [Column("description")] public string Description { get; set; } = "";
}
