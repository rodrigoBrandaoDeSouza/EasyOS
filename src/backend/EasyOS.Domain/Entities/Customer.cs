using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EasyOS.Domain.Entities;

/// <summary>Cliente atendido pela empresa prestadora.</summary>
[Table("Customers")]
public class Customer
{
    [Key, Column("idcustomer")] public int Id { get; set; }
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
    [Column("contact_name")] public string ContactName { get; set; } = "";
    [Column("active")] public bool Active { get; set; }
    [Column("idcompany")] public int CompanyId { get; set; }
    [Column("created_date")] public DateTime CreatedDate { get; set; }
    [Column("cpf_cnpj")] public string? CpfCnpj { get; set; }
    [Column("latitude")] public float Latitude { get; set; }
    [Column("longitude")] public float Longitude { get; set; }
    [Column("last_update")] public DateTime LastUpdate { get; set; }
    /// <summary>Dia do mês usado como data dos certificados.</summary>
    [Column("certificate_date")] public int CertificateDate { get; set; }
    [Column("observation")] public string? Observation { get; set; }
    [Column("client_has_contract")] public bool? ClientHasContract { get; set; }
    [Column("activity_type")] public string? ActivityType { get; set; }
    [Column("fax")] public string? Fax { get; set; }
    [Column("email")] public string? Email { get; set; }
    [Column("contact_function")] public string? ContactFunction { get; set; }
}
