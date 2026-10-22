namespace EasyOS.Api.Features.Orders;

public sealed record OrderListItem(
    int Id, long Sequence, DateTime OrderDate, DateTime TimeStart, DateTime TimeEnd,
    int CustomerId, string CustomerName, int TechnicianId, string TechnicianName,
    bool ServiceExecuted, bool Active, string? Observations);

public sealed record OrderDetailsFlags(
    bool HadEvidenceOfPlague, bool HadEquipmentRevision, bool HadBaiting, bool HadPlaquesOfGlue,
    bool HadLuminousTrap, bool CustomerNeedPlagueAccount, bool HadPheromoneTrap, bool HadProductConsumption);

public sealed record PlagueFoundItem(int PlagueId, string PlagueName, int PlagueStatusId, string PlagueStatus, string? Location, int Quantity);
public sealed record TrapItem(string? Type, string? Point, int Quantity);
public sealed record GlueTrapItem(string? Type, string? Point, int Quantity, int RodentQuantity);
public sealed record LuminousPlagueCount(int PlagueId, string PlagueName, int Quantity);
public sealed record LuminousItem(string? Point, int Quantity, IReadOnlyList<LuminousPlagueCount> PlagueCounts);

public sealed record ConsumptionItem(
    int PlagueProductId, string PlagueName, string ProductName, string Unity, double Concentration,
    int ProductApplicationId, string ApplicationName, double Quantity, string Lot, string? ExpirationDate);

public sealed record NonConformityItem(int Id, string? Place, string? Description, string? Suggestion,
    string? Responsible, string? Action, DateTime? DueDate, DateTime? ClosingDate, int? Status, int? IsValid);

public sealed record OrderDetailDto(
    int Id, long Sequence, bool Active,
    DateTime OrderDate, DateTime TimeStart, DateTime TimeEnd, DateTime? CheckinDate, int OffsetMinutes,
    int VisitId, string? VisitDescription, string? TreatedAreas,
    bool ServiceExecuted, string? ReasonOfNonExecution, string? Observations,
    string? ResponsibleName, string? ResponsibleBusiness, bool HasResponsibleSignature,
    int TechnicianId, string TechnicianName, bool HasTechnicianSignature, string? TechnicalResponsible,
    OrderCustomerInfo Customer, OrderCompanyInfo Company,
    OrderDetailsFlags Details,
    IReadOnlyList<PlagueFoundItem> Plagues,
    IReadOnlyList<TrapItem> Baitings,
    IReadOnlyList<GlueTrapItem> PlaquesOfGlue,
    IReadOnlyList<TrapItem> Pheromones,
    IReadOnlyList<LuminousItem> Luminous,
    IReadOnlyList<ConsumptionItem> Consumptions,
    IReadOnlyList<NonConformityItem> NonConformities,
    IReadOnlyList<int> EvidenceIds);

public sealed record OrderCustomerInfo(
    int Id, string TradingName, string CompanyName, string? CpfCnpj,
    string Address, int Number, string Complement, string Neighborhood, string City, string State, string Zipcode,
    string? ActivityType, int CertificateDate);

public sealed record OrderCompanyInfo(
    int Id, string CompanyName, string TradingName, string? CpfCnpj, string Charter, DateTime CharterExpiration,
    string Address, int Number, string Neighborhood, string City, string State, string Zipcode, string Phone);

public sealed record ResponsibleSignatureDto(string? Name, string? Business, string? Signature);

public sealed record CertificateListItem(int OrderId, long Sequence, DateTime StartDate, DateTime EndDate);

public sealed record CertificateProduct(
    int Id, string Name, string ChemicalGroup, string ActiveIngredient, string RegistrationNumber,
    string Symptoms, string Antidotes, string Unity, string TargetPlagues);

public sealed record CertificateTechnicalManager(string State, string Name, string Register, bool HasSignature);

public sealed record CertificateDto(
    int OrderId, long Sequence, DateTime OrderDate, DateTime TimeStart, DateTime TimeEnd,
    DateTime ValidFrom, DateTime ValidUntil, int ValidityDays,
    string? ResponsibleName, string? ResponsibleBusiness,
    OrderCustomerInfo Customer, OrderCompanyInfo Company,
    string TechnicianName, CertificateTechnicalManager? TechnicalManager,
    string? TreatedAreas, IReadOnlyList<CertificateProduct> Products, IReadOnlyList<string> PlaguesFound);

// ---------- Lançamento manual (mesmo conteúdo da OS enviada pelo app) ----------

public sealed record OrderCreateRequest(
    int CustomerId,
    int VisitId,
    int TechnicianId,
    DateOnly OrderDate,
    // Horários locais (sem fuso) e offset do navegador em minutos.
    DateTime TimeStart,
    DateTime TimeEnd,
    int? OffsetMinutes,
    bool ServiceExecuted,
    string? ReasonOfNonExecution,
    string? ResponsibleName,
    string? ResponsibleBusiness,
    string? ResponsibleSignature,
    string? Observations,
    OrderDetailsFlags Details,
    List<OrderPlagueInput>? Plagues,
    List<TrapItem>? Baitings,
    List<GlueTrapItem>? PlaquesOfGlue,
    List<TrapItem>? Pheromones,
    List<LuminousInput>? Luminous,
    List<ConsumptionInput>? Consumptions,
    List<NonConformityInput>? NonConformities,
    List<string>? Evidences);

public sealed record OrderPlagueInput(int PlagueId, int PlagueStatusId, string? Location, int Quantity);
public sealed record LuminousCountInput(int PlagueId, int Quantity);
public sealed record LuminousInput(string? Point, int Quantity, List<LuminousCountInput>? PlagueCounts);
public sealed record ConsumptionInput(int PlagueProductId, int ProductApplicationId, double Quantity, string? Lot, string? ExpirationDate);
public sealed record NonConformityInput(string? Place, string? Description, string? Suggestion);
