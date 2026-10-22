using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EasyOS.Domain.Entities;

/// <summary>Empresa prestadora de serviço cadastrada no banco do tenant.</summary>
[Table("Companies")]
public class Company
{
    [Key, Column("idcompany")] public int Id { get; set; }
    [Column("trading_name")] public string TradingName { get; set; } = "";
    [Column("company_name")] public string CompanyName { get; set; } = "";
    [Column("address")] public string Address { get; set; } = "";
    [Column("number")] public int Number { get; set; }
    [Column("complement")] public string Complement { get; set; } = "";
    [Column("neighborhood")] public string Neighborhood { get; set; } = "";
    [Column("city")] public string City { get; set; } = "";
    [Column("state")] public string State { get; set; } = "";
    [Column("zipcode")] public string Zipcode { get; set; } = "";
    [Column("phone")] public string Phone { get; set; } = "";
    [Column("fax")] public string? Fax { get; set; }
    [Column("email")] public string? Email { get; set; }
    [Column("contact_name")] public string ContactName { get; set; } = "";
    [Column("active")] public bool Active { get; set; }
    [Column("logo", TypeName = "text")] public string? Logo { get; set; }
    [Column("use_baitings")] public bool UseBaitings { get; set; }
    [Column("use_plaque_of_glue")] public bool UsePlaqueOfGlue { get; set; }
    [Column("use_luminous_trap")] public bool UseLuminousTrap { get; set; }
    [Column("use_pheromone_trap")] public bool UsePheromoneTrap { get; set; }
    [Column("created_date")] public DateTime CreatedDate { get; set; }
    [Column("cpf_cnpj")] public string? CpfCnpj { get; set; }
    [Column("last_update")] public DateTime LastUpdate { get; set; }
    [Column("charter")] public string Charter { get; set; } = "";
    [Column("charter_expiration")] public DateTime CharterExpiration { get; set; }
    [Column("inea_code")] public string? IneaCode { get; set; }
    [Column("environmental_permit")] public string? EnvironmentalPermit { get; set; }
    [Column("environmental_permit_expiration")] public DateTime? EnvironmentalPermitExpiration { get; set; }
    [Column("legal_representative")] public string? LegalRepresentative { get; set; }
}
