using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EasyOS.Domain.Entities;

/// <summary>
/// Visita agendada. As datas são gravadas deslocadas (como faz o legado);
/// somar <see cref="OffsetMinutes"/> para obter o horário local.
/// </summary>
[Table("Visits")]
public class Visit
{
    public const int DefaultOffsetMinutes = -180;

    [Key, Column("idvisit")] public int Id { get; set; }
    [Column("idcompany")] public int CompanyId { get; set; }
    [Column("idcustomer")] public int CustomerId { get; set; }
    [Column("idcompanyuser_execution")] public int? ExecutionUserId { get; set; }
    [Column("visit_date")] public DateTime VisitDate { get; set; }
    [Column("created_date")] public DateTime CreatedDate { get; set; }
    [Column("checkin_date")] public DateTime? CheckinDate { get; set; }
    [Column("offset_date")] public int? OffsetDate { get; set; }
    [Column("idcompanyuser_responsible")] public int ResponsibleUserId { get; set; }
    [Column("active")] public bool Active { get; set; }
    [Column("has_confirmed")] public bool HasConfirmed { get; set; }
    [Column("confirmation_date")] public DateTime? ConfirmationDate { get; set; }
    [Column("last_update")] public DateTime LastUpdate { get; set; }
    [Column("description")] public string? Description { get; set; }
    [Column("auxiliar_technicians")] public string? AuxiliarTechnicians { get; set; }
    [Column("certificate_date")] public int? CertificateDate { get; set; }
    [Column("treated_areas")] public string? TreatedAreas { get; set; }

    [NotMapped] public int OffsetMinutes => OffsetDate ?? DefaultOffsetMinutes;

    public Customer? Customer { get; set; }
    public CompanyUser? ExecutionUser { get; set; }
    public CompanyUser? ResponsibleUser { get; set; }
}
