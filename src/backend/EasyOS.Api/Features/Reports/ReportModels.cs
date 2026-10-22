namespace EasyOS.Api.Features.Reports;

public sealed record ChartSeries(string Name, string Type, IReadOnlyList<double> Data);

/// <summary>Detalhamento de um mês (ex.: por ponto de isca, por praga, por produto).</summary>
public sealed record DrillDown(string Category, IReadOnlyList<NamedQuantity> Items);

public sealed record NamedQuantity(string Name, double Quantity);

public sealed record ReportTable(IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<string>> Rows);

/// <summary>Relatório mensal padrão: gráfico (categorias = meses), tabela e detalhamento opcional por mês.</summary>
public sealed record MonthlyReport(
    string Title,
    IReadOnlyList<string> Categories,
    IReadOnlyList<ChartSeries> Series,
    ReportTable Table,
    IReadOnlyList<DrillDown>? DrillDowns = null,
    IReadOnlyList<LuminousMonth>? Luminous = null,
    IReadOnlyList<string>? LuminousColumns = null);

/// <summary>Armadilhas luminosas: totais do mês por inseto e por ponto (colunas fixas: 5 insetos + não especificado).</summary>
public sealed record LuminousMonth(string Category, IReadOnlyList<int> Totals, IReadOnlyList<LuminousPoint> Points);

public sealed record LuminousPoint(string Point, IReadOnlyList<int> Values);

public sealed record NonConformityReportItem(int OrderId, long Sequence, DateTime OrderDate, string? Place, string? Description, string? Suggestion);

public sealed record ProductivityItem(
    int TechnicianId, string TechnicianName, int HoursWorked, int MinutesWorked,
    int Scheduled, int Accomplished, int NotAccomplished, int Pending);

public sealed record CustomerDashboard(
    DateOnly From, DateOnly To,
    MonthlyReport PlaguesFound, MonthlyReport Rodents, MonthlyReport Consumption);

public sealed record CompanyDashboard(
    DateOnly From, DateOnly To,
    int Technicians, int ServiceOrders, int ActiveCustomers,
    IReadOnlyList<string> Days, IReadOnlyList<double> OrdersPerDay,
    IReadOnlyList<NamedQuantity> OrdersPerTechnician,
    IReadOnlyList<NamedQuantity> ConsumptionByProductType);
