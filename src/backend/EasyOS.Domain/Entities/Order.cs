using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EasyOS.Domain.Entities;

/// <summary>Ordem de serviço. Horários gravados deslocados; somar <see cref="OffsetMinutes"/> para o horário local.</summary>
[Table("Orders")]
public class Order
{
    [Key, Column("idorder")] public int Id { get; set; }
    [Column("created_date")] public DateTime CreatedDate { get; set; }
    [Column("idcustomer")] public int CustomerId { get; set; }
    [Column("order_date")] public DateTime OrderDate { get; set; }
    [Column("time_start")] public DateTime TimeStart { get; set; }
    [Column("time_end")] public DateTime TimeEnd { get; set; }
    [Column("offset_date")] public int? OffsetDate { get; set; }
    [Column("service_executed")] public bool ServiceExecuted { get; set; }
    [Column("reason_of_non_execution")] public string? ReasonOfNonExecution { get; set; }
    [Column("responsible_name")] public string? ResponsibleName { get; set; }
    [Column("responsible_business")] public string? ResponsibleBusiness { get; set; }
    [Column("responsible_signature", TypeName = "text")] public string? ResponsibleSignature { get; set; }
    [Column("observations", TypeName = "text")] public string? Observations { get; set; }
    /// <summary>0 quando a OS foi lançada sem visita (sem FK navegável por isso).</summary>
    [Column("idvisit")] public int VisitId { get; set; }
    [Column("active")] public bool Active { get; set; }
    [Column("idcompanyuser_execution")] public int ExecutionUserId { get; set; }
    [Column("deleted")] public bool Deleted { get; set; }
    [Column("order_sequence")] public long? OrderSequence { get; set; }

    [NotMapped] public int OffsetMinutes => OffsetDate ?? Visit.DefaultOffsetMinutes;

    public Customer? Customer { get; set; }
    public CompanyUser? ExecutionUser { get; set; }
    public List<OrderDetail> Details { get; set; } = [];
    public List<OrderEvidence> Evidences { get; set; } = [];
    public List<OrderNonConformity> NonConformities { get; set; } = [];
}

[Table("OrderDetails")]
public class OrderDetail
{
    [Key, Column("idorderdetail")] public int Id { get; set; }
    [Column("idorder")] public int OrderId { get; set; }
    [Column("had_evidence_of_plague")] public bool HadEvidenceOfPlague { get; set; }
    [Column("had_equipment_revision")] public bool HadEquipmentRevision { get; set; }
    [Column("had_baiting")] public bool HadBaiting { get; set; }
    [Column("had_plaques_of_glue")] public bool HadPlaquesOfGlue { get; set; }
    [Column("had_luminous_trap")] public bool HadLuminousTrap { get; set; }
    [Column("customer_need_plague_account")] public bool CustomerNeedPlagueAccount { get; set; }
    [Column("had_pheromone_trap")] public bool HadPheromoneTrap { get; set; }
    [Column("had_product_consumption")] public bool HadProductConsumption { get; set; }
    [Column("created_date")] public DateTime CreatedDate { get; set; }

    public List<OrderDetailsBaiting> Baitings { get; set; } = [];
    public List<OrderDetailsLuminous> Luminous { get; set; } = [];
    public List<OrderDetailsPheromone> Pheromones { get; set; } = [];
    public List<OrderDetailsPlague> Plagues { get; set; } = [];
    public List<OrderDetailsPlaquesOfGlue> PlaquesOfGlue { get; set; } = [];
    public List<OrderConsumption> Consumptions { get; set; } = [];
}

/// <summary>Iscagem (pontos de isca).</summary>
[Table("OrderDetailsBaitings")]
public class OrderDetailsBaiting
{
    [Key, Column("idorderdetailsbaiting")] public int Id { get; set; }
    [Column("type")] public string Type { get; set; } = "";
    [Column("point")] public string Point { get; set; } = "";
    [Column("idorderdetail")] public int OrderDetailId { get; set; }
    [Column("quantity")] public int Quantity { get; set; }
}

/// <summary>Armadilha luminosa.</summary>
[Table("OrderDetailsLuminous")]
public class OrderDetailsLuminous
{
    [Key, Column("idorderdetailsluminous")] public int Id { get; set; }
    [Column("quantity")] public int Quantity { get; set; }
    [Column("point")] public string Point { get; set; } = "";
    [Column("idorderdetail")] public int OrderDetailId { get; set; }

    public List<LuminousPlaquePlague> Plagues { get; set; } = [];
}

/// <summary>Pragas capturadas numa armadilha luminosa.</summary>
[Table("LuminousPlaquePlagues")]
public class LuminousPlaquePlague
{
    [Key, Column("idluminousplaqueplagues")] public int Id { get; set; }
    [Column("quantity")] public int Quantity { get; set; }
    [Column("idorderdetailsluminous")] public int OrderDetailsLuminousId { get; set; }
    [Column("idplague")] public int PlagueId { get; set; }

    public Plague? Plague { get; set; }
}

/// <summary>Armadilha de feromônio.</summary>
[Table("OrderDetailsPheromone")]
public class OrderDetailsPheromone
{
    [Key, Column("idorderdetailspheromone")] public int Id { get; set; }
    [Column("type")] public string Type { get; set; } = "";
    [Column("idorderdetail")] public int OrderDetailId { get; set; }
    [Column("point")] public string Point { get; set; } = "";
    [Column("quantity")] public int Quantity { get; set; }
}

/// <summary>Praga encontrada durante a OS.</summary>
[Table("OrderDetailsPlagues")]
public class OrderDetailsPlague
{
    [Key, Column("idorderdetailsplague")] public int Id { get; set; }
    [Column("quantity")] public int Quantity { get; set; }
    [Column("idplague")] public int PlagueId { get; set; }
    [Column("idorderdetail")] public int OrderDetailId { get; set; }
    [Column("idplaguestatus")] public int PlagueStatusId { get; set; }
    [Column("plague_location_description")] public string? PlagueLocationDescription { get; set; }

    public Plague? Plague { get; set; }
    public PlagueStatus? PlagueStatus { get; set; }
}

/// <summary>Placa de cola / armadilha mecânica.</summary>
[Table("OrderDetailsPlaquesOfGlue")]
public class OrderDetailsPlaquesOfGlue
{
    [Key, Column("idorderplaquesofglue")] public int Id { get; set; }
    [Column("idorderdetail")] public int OrderDetailId { get; set; }
    [Column("rodent_quantity")] public int RodentQuantity { get; set; }
    [Column("point")] public string? Point { get; set; }
    [Column("type")] public string? Type { get; set; }
    [Column("quantity")] public int Quantity { get; set; }
}

/// <summary>Consumo de produto na OS.</summary>
[Table("OrderConsumption")]
public class OrderConsumption
{
    [Key, Column("idorderconsumption")] public int Id { get; set; }
    [Column("idorderdetails")] public int OrderDetailId { get; set; }
    [Column("idplagueproduct")] public int PlagueProductId { get; set; }
    [Column("quantity")] public double Quantity { get; set; }
    [Column("created_date")] public DateTime CreatedDate { get; set; }
    [Column("idproductapplication")] public int ProductApplicationId { get; set; }
    [Column("expiration_date")] public string? ExpirationDate { get; set; }
    [Column("lot")] public string Lot { get; set; } = "";

    public PlagueProduct? PlagueProduct { get; set; }
    public ProductApplication? ProductApplication { get; set; }
}

/// <summary>Foto/evidência da OS, em base64 no campo data.</summary>
[Table("OrderEvidences")]
public class OrderEvidence
{
    [Key, Column("idorderevidence")] public int Id { get; set; }
    [Column("created_date")] public DateTime CreatedDate { get; set; }
    [Column("data", TypeName = "text")] public string Data { get; set; } = "";
    [Column("idorder")] public int OrderId { get; set; }
}

[Table("OrderNonConformities")]
public class OrderNonConformity
{
    [Key, Column("idordernonconformity")] public int Id { get; set; }
    [Column("place")] public string? Place { get; set; }
    [Column("description", TypeName = "text")] public string? Description { get; set; }
    [Column("suggestion", TypeName = "text")] public string? Suggestion { get; set; }
    [Column("created_date")] public DateTime CreatedDate { get; set; }
    [Column("idorder")] public int OrderId { get; set; }
    [Column("Responsible")] public string? Responsible { get; set; }
    [Column("Action")] public string? Action { get; set; }
    [Column("DueDate")] public DateTime? DueDate { get; set; }
    [Column("ClosingDate")] public DateTime? ClosingDate { get; set; }
    [Column("Status")] public int? Status { get; set; }
    [Column("isValid")] public int? IsValid { get; set; }
}
