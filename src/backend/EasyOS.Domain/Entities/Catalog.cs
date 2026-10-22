using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EasyOS.Domain.Entities;

[Table("Plagues")]
public class Plague
{
    [Key, Column("idplague")] public int Id { get; set; }
    [Column("name")] public string Name { get; set; } = "";
    [Column("last_update")] public DateTime LastUpdate { get; set; }
    [Column("plague_type")] public string PlagueType { get; set; } = "";
}

/// <summary>Praga habilitada para uma empresa.</summary>
[Table("CompanyPlagues")]
public class CompanyPlague
{
    [Key, Column("idcompanyplague")] public int Id { get; set; }
    [Column("idplague")] public int PlagueId { get; set; }
    [Column("idcompany")] public int CompanyId { get; set; }
    [Column("active")] public bool Active { get; set; }
    [Column("last_update")] public DateTime LastUpdate { get; set; }
    [Column("removed")] public bool Removed { get; set; }

    public Plague? Plague { get; set; }
}

[Table("PlagueStatus")]
public class PlagueStatus
{
    [Key, Column("idplaguestatus")] public int Id { get; set; }
    [Column("description")] public string Description { get; set; } = "";
}

[Table("Products")]
public class Product
{
    [Key, Column("idproduct")] public int Id { get; set; }
    [Column("idproducttype")] public int ProductTypeId { get; set; }
    [Column("name")] public string Name { get; set; } = "";
    [Column("chemical_group")] public string ChemicalGroup { get; set; } = "";
    [Column("active_ingredient")] public string ActiveIngredient { get; set; } = "";
    [Column("registration_number")] public string RegistrationNumber { get; set; } = "";
    [Column("created_date")] public DateTime CreatedDate { get; set; }
    [Column("last_update")] public DateTime LastUpdate { get; set; }
    [Column("equipment_type")] public string EquipmentType { get; set; } = "";
    [Column("symptoms")] public string Symptoms { get; set; } = "";
    [Column("antidotes")] public string Antidotes { get; set; } = "";
    [Column("product_code")] public int ProductCode { get; set; }

    public ProductType? ProductType { get; set; }
    public List<ProductApplication> Applications { get; set; } = [];
    public List<ProductActiveIngredient> ActiveIngredients { get; set; } = [];
    public List<PlagueProduct> PlagueProducts { get; set; } = [];
}

[Table("ProductType")]
public class ProductType
{
    [Key, Column("idproducttype")] public int Id { get; set; }
    [Column("type")] public string Type { get; set; } = "";
    [Column("unity")] public string Unity { get; set; } = "";
}

/// <summary>Produto habilitado para uma empresa.</summary>
[Table("CompanyProducts")]
public class CompanyProduct
{
    [Key, Column("idcompanyproduct")] public int Id { get; set; }
    [Column("idcompany")] public int CompanyId { get; set; }
    [Column("idproduct")] public int ProductId { get; set; }
    [Column("active")] public bool Active { get; set; }
    [Column("last_update")] public DateTime LastUpdate { get; set; }

    public Product? Product { get; set; }
}

/// <summary>Forma de aplicação de um produto.</summary>
[Table("ProductApplication")]
public class ProductApplication
{
    [Key, Column("idproductapplication")] public int Id { get; set; }
    [Column("active")] public bool Active { get; set; }
    [Column("idproduct")] public int ProductId { get; set; }
    [Column("description")] public string Description { get; set; } = "";
    [Column("last_update")] public DateTime LastUpdate { get; set; }
}

[Table("ProductActiveIngredients")]
public class ProductActiveIngredient
{
    [Key, Column("idproductactiveingredient")] public int Id { get; set; }
    [Column("idproduct")] public int ProductId { get; set; }
    [Column("activeingredient")] public string ActiveIngredient { get; set; } = "";
    [Column("concentration")] public double Concentration { get; set; }
    [Column("active")] public bool Active { get; set; }
}

/// <summary>Relação produto × praga com a concentração indicada.</summary>
[Table("PlagueProduct")]
public class PlagueProduct
{
    [Key, Column("idplagueproduct")] public int Id { get; set; }
    [Column("idproduct")] public int ProductId { get; set; }
    [Column("idplague")] public int PlagueId { get; set; }
    [Column("concentration")] public double Concentration { get; set; }
    [Column("last_update")] public DateTime LastUpdate { get; set; }
    [Column("active")] public bool Active { get; set; }

    public Plague? Plague { get; set; }
    public Product? Product { get; set; }
}

/// <summary>Responsável técnico da empresa por UF.</summary>
[Table("TechnicalManager")]
public class TechnicalManager
{
    [Key, Column("idtechnicalmanager")] public int Id { get; set; }
    [Column("idcompany")] public int CompanyId { get; set; }
    [Column("state")] public string State { get; set; } = "";
    [Column("register")] public string Register { get; set; } = "";
    [Column("name")] public string Name { get; set; } = "";
}

[Table("MonthlyComments")]
public class MonthlyComment
{
    [Key, Column("id")] public int Id { get; set; }
    [Column("idcustomer")] public int CustomerId { get; set; }
    [Column("idcompanyuser")] public int CompanyUserId { get; set; }
    [Column("referencemonth")] public int ReferenceMonth { get; set; }
    [Column("referenceyear")] public int ReferenceYear { get; set; }
    [Column("comment")] public string Comment { get; set; } = "";
    [Column("created_date")] public DateTime CreatedDate { get; set; }

    public CompanyUser? CompanyUser { get; set; }
}

/// <summary>Textos parametrizáveis (ex.: observações do certificado).</summary>
[Table("Parameters")]
public class Parameter
{
    [Key, Column("idparameter")] public int Id { get; set; }
    [Column("parameter")] public string Name { get; set; } = "";
    [Column("description")] public string Description { get; set; } = "";
    [Column("active")] public bool Active { get; set; }
    [Column("created_date")] public DateTime CreatedDate { get; set; }
}
