using System.Globalization;
using EasyOS.Api.Common;
using EasyOS.Api.Features.Orders;
using EasyOS.Domain.Entities;
using EasyOS.Infrastructure;
using EasyOS.Infrastructure.MultiTenancy;
using EasyOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EasyOS.Api.Features.Reports;

/// <summary>
/// Relatórios da área do cliente e dashboards. Reproduz as agregações do legado (DashboardBLL/ReportsDAL):
/// considera só OS ativas, agrupa pelo mês da data da OS e usa os mesmos rótulos e regras de filtro.
/// </summary>
public sealed class ReportService(TenantDbContext db, Access access, ITenantContext tenant, IClock clock)
{
    // Tipos de produto fixos do legado (tabela ProductType).
    private const int LiquidInsecticide = 1;
    private const int SolidInsecticide = 2;
    private const int Rodenticide = 3;
    private const int Supplies = 4;

    // Status "Indivíduos mortos" (PlagueStatus) e praga 1 (Roedores), excluída de "pragas encontradas".
    private const int DeadStatus = 2;
    private const int RodentPlagueId = 1;

    public static readonly string[] CustomerReports =
    [
        "product-consumption", "input-consumption", "plagues-found", "product-plague", "pheromone-traps",
        "luminous-traps", "rodents-dead", "glue-traps", "gnawed-baits", "rodents-rodenticide",
    ];

    /// <summary>Período padrão dos relatórios do legado: do início do mês de 13 meses atrás até hoje.</summary>
    public (DateOnly From, DateOnly To) DefaultRange(DateOnly? from, DateOnly? to)
    {
        var today = DateOnly.FromDateTime(clock.Now);
        var start = from ?? new DateOnly(today.Year, today.Month, 1).AddMonths(-13);
        var end = to ?? today;
        if (end < start) throw new BusinessException("A data final deve ser maior que a inicial.");
        if (end.DayNumber - start.DayNumber > 366 * 5) throw new BusinessException("O período máximo é de 5 anos.");
        return (start, end);
    }

    public async Task<MonthlyReport> CustomerReportAsync(int customerId, string type, DateOnly from, DateOnly to, CancellationToken ct)
    {
        await access.EnsureCustomerAsync(customerId, ct);
        var months = Months(from, to);
        var orders = ActiveOrders(customerId, from, to);

        return type switch
        {
            "product-consumption" => await ProductConsumption(orders, months, ct),
            "input-consumption" => await InputConsumption(orders, months, ct),
            "plagues-found" => await PlaguesFound(orders, months, ct),
            "product-plague" => await ProductPlague(orders, months, ct),
            "pheromone-traps" => await Pheromones(orders, months, ct),
            "luminous-traps" => await Luminous(orders, months, ct),
            "rodents-dead" => await RodentsDead(orders, months, ct),
            "glue-traps" => await GlueTraps(orders, months, ct),
            "gnawed-baits" => await GnawedBaits(orders, months, ct),
            "rodents-rodenticide" => await RodentsRodenticide(orders, months, ct),
            _ => throw new NotFoundException("Relatório não encontrado."),
        };
    }

    public async Task<CustomerDashboard> CustomerDashboardAsync(int customerId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        await access.EnsureCustomerAsync(customerId, ct);
        var months = Months(from, to);
        var orders = ActiveOrders(customerId, from, to);

        var plagues = await PlaguesFound(orders, months, ct);
        var glue = await MonthlyCount(GlueTrapRows(orders, "capturado"), months, ct);
        var dead = await MonthlySum(DeadRodentRows(orders), months, ct);
        var consumption = await ProductConsumption(orders, months, ct);

        var rodents = Build("Roedores", months,
            [new ChartSeries("PLACA DE COLA", "column", glue), new ChartSeries("ROEDORES MORTOS", "column", dead)],
            ["Data", "Placa de Cola", "Mortos"]);

        return new CustomerDashboard(from, to, plagues with { DrillDowns = null }, rodents, consumption);
    }

    /// <summary>Não conformidades das OS executadas no período (relatório "Não Conformidades").</summary>
    public async Task<List<NonConformityReportItem>> NonConformitiesAsync(int customerId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        await access.EnsureCustomerAsync(customerId, ct);
        var (start, end) = Bounds(from, to);
        return await db.OrderNonConformities.AsNoTracking()
            .Join(db.Orders.Where(o => o.CustomerId == customerId && o.Active && o.ServiceExecuted && o.OrderDate >= start && o.OrderDate < end),
                n => n.OrderId, o => o.Id, (n, o) => new { n, o })
            .OrderByDescending(x => x.o.OrderDate).ThenBy(x => x.n.Id)
            .Select(x => new NonConformityReportItem(x.o.Id, x.o.OrderSequence ?? 0, x.o.OrderDate, x.n.Place, x.n.Description, x.n.Suggestion))
            .ToListAsync(ct);
    }

    /// <summary>Produtividade dos técnicos ativos no período (área da empresa).</summary>
    public async Task<List<ProductivityItem>> ProductivityAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        var companyId = await access.CurrentCompanyIdAsync(ct);
        var (start, end) = Bounds(from, to);

        var technicians = await db.CompanyUsers.AsNoTracking()
            .Where(u => u.CompanyId == companyId && u.Active && u.ProfileLevel == CompanyProfile.Technician)
            .OrderBy(u => u.Name).Select(u => new { u.Id, u.Name }).ToListAsync(ct);
        var ids = technicians.Select(t => t.Id).ToList();

        var orders = await db.Orders.AsNoTracking()
            .Where(o => ids.Contains(o.ExecutionUserId) && o.Active && o.OrderDate >= start && o.OrderDate < end)
            .Select(o => new { o.ExecutionUserId, o.ServiceExecuted, o.TimeStart, o.TimeEnd })
            .ToListAsync(ct);
        var scheduled = await db.Visits.AsNoTracking()
            .Where(v => v.ExecutionUserId != null && ids.Contains(v.ExecutionUserId.Value) && v.VisitDate >= start && v.VisitDate < end &&
                        !(v.CheckinDate == null && !v.Active))
            .GroupBy(v => v.ExecutionUserId!.Value)
            .Select(g => new { Id = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.Count, ct);

        return technicians.Select(t =>
        {
            var mine = orders.Where(o => o.ExecutionUserId == t.Id).ToList();
            var seconds = (long)mine.Sum(o => Math.Max(0, (o.TimeEnd - o.TimeStart).TotalSeconds));
            var accomplished = mine.Count(o => o.ServiceExecuted);
            var notAccomplished = mine.Count(o => !o.ServiceExecuted);
            var planned = scheduled.GetValueOrDefault(t.Id);
            return new ProductivityItem(t.Id, t.Name, (int)(seconds / 3600), (int)(seconds % 3600 / 60),
                planned, accomplished, notAccomplished, Math.Max(0, planned - accomplished - notAccomplished));
        }).ToList();
    }

    public async Task<CompanyDashboard> CompanyDashboardAsync(DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var companyId = await access.CurrentCompanyIdAsync(ct);
        var today = DateOnly.FromDateTime(clock.Now);
        // Padrão do legado: mês corrente inteiro.
        var start = from ?? new DateOnly(today.Year, today.Month, 1);
        var finish = to ?? new DateOnly(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month));
        if (finish < start) throw new BusinessException("A data final deve ser maior que a inicial.");
        if (finish.DayNumber - start.DayNumber > 400) throw new BusinessException("O período máximo do dashboard é de 1 ano.");
        var (s, e) = Bounds(start, finish);

        var companyOrders = db.Orders.AsNoTracking().Where(o => o.Customer!.CompanyId == companyId && o.Active && o.OrderDate >= s && o.OrderDate < e);

        var perDay = await companyOrders.GroupBy(o => o.OrderDate.Date).Select(g => new { Day = g.Key, Count = g.Count() }).ToListAsync(ct);
        var days = Enumerable.Range(0, finish.DayNumber - start.DayNumber + 1).Select(i => start.AddDays(i)).ToList();

        var perTechnician = await companyOrders
            .GroupBy(o => new { o.ExecutionUserId, o.ExecutionUser!.Name })
            .Select(g => new NamedQuantity(g.Key.Name, g.Count()))
            .ToListAsync(ct);

        // Consumo por tipo de produto (exceto insumos), convertido para L/kg como no legado.
        var consumption = await db.OrderConsumptions.AsNoTracking()
            .Join(db.OrderDetails, c => c.OrderDetailId, d => d.Id, (c, d) => new { c, d })
            .Join(companyOrders, x => x.d.OrderId, o => o.Id, (x, o) => x.c)
            .Where(c => c.PlagueProduct!.Product!.ProductTypeId != Supplies)
            .GroupBy(c => new { c.PlagueProduct!.Product!.ProductType!.Type, c.PlagueProduct.Product.ProductType.Unity })
            .Select(g => new { g.Key.Type, g.Key.Unity, Total = g.Sum(c => c.Quantity) })
            .ToListAsync(ct);

        return new CompanyDashboard(start, finish,
            await db.CompanyUsers.CountAsync(u => u.CompanyId == companyId && u.Active && u.ProfileLevel == CompanyProfile.Technician, ct),
            await companyOrders.CountAsync(ct),
            await db.Customers.CountAsync(c => c.CompanyId == companyId && c.Active, ct),
            days.Select(d => d.ToString("dd/MM", CultureInfo.InvariantCulture)).ToList(),
            days.Select(d => (double)(perDay.FirstOrDefault(p => DateOnly.FromDateTime(p.Day) == d)?.Count ?? 0)).ToList(),
            perTechnician.OrderByDescending(t => t.Quantity).ToList(),
            consumption.Select(c => new NamedQuantity($"{c.Type} ({LargerUnit(c.Unity)})", Math.Round(c.Total / 1000, 3))).ToList());
    }

    // ---------- Relatórios ----------

    private async Task<MonthlyReport> ProductConsumption(IQueryable<Order> orders, List<(int Y, int M)> months, CancellationToken ct)
    {
        var rows = await ConsumptionRows(orders).ToListAsync(ct);
        double[] Sum(int type) => months.Select(m => rows.Where(r => r.Type == type && r.Y == m.Y && r.M == m.M).Sum(r => r.Quantity)).ToArray();
        return Build("Consumo de Produtos", months,
            [
                new ChartSeries("INSETICIDA LIQUIDO (ml)", "line", Sum(LiquidInsecticide)),
                new ChartSeries("INSETICIDA SÓLIDO (g)", "line", Sum(SolidInsecticide)),
                new ChartSeries("RODENTICIDA (g)", "line", Sum(Rodenticide)),
            ],
            ["Data", "Inseticida Liquido (ml)", "Inseticida Sólido (g)", "Rodenticida (g)"]);
    }

    private async Task<MonthlyReport> InputConsumption(IQueryable<Order> orders, List<(int Y, int M)> months, CancellationToken ct)
    {
        var rows = (await ConsumptionRows(orders).ToListAsync(ct)).Where(r => r.Type == Supplies).ToList();
        var totals = months.Select(m => rows.Where(r => r.Y == m.Y && r.M == m.M).Sum(r => r.Quantity)).ToArray();
        var drill = months.Select(m => new DrillDown(Label(m),
            rows.Where(r => r.Y == m.Y && r.M == m.M).GroupBy(r => r.Product).Select(g => new NamedQuantity(g.Key, g.Sum(r => r.Quantity)))
                .OrderBy(x => x.Name).ToList())).ToList();
        return Build("Consumo de Insumos", months, [new ChartSeries("Consumo de Insumos", "column", totals)], ["Mês", "Insumos"]) with { DrillDowns = drill };
    }

    private async Task<MonthlyReport> PlaguesFound(IQueryable<Order> orders, List<(int Y, int M)> months, CancellationToken ct)
    {
        var rows = await db.OrderDetailsPlagues.AsNoTracking()
            .Where(p => p.PlagueId != RodentPlagueId)
            .Join(db.OrderDetails, p => p.OrderDetailId, d => d.Id, (p, d) => new { p, d })
            .Join(orders, x => x.d.OrderId, o => o.Id, (x, o) => new { o.OrderDate.Year, o.OrderDate.Month, x.p.Quantity, Name = x.p.Plague!.Name })
            .ToListAsync(ct);
        var totals = months.Select(m => (double)rows.Where(r => r.Year == m.Y && r.Month == m.M).Sum(r => r.Quantity)).ToArray();
        var drill = months.Select(m => new DrillDown(Label(m),
            rows.Where(r => r.Year == m.Y && r.Month == m.M).GroupBy(r => r.Name).Select(g => new NamedQuantity(g.Key, g.Sum(r => r.Quantity)))
                .OrderBy(x => x.Name).ToList())).ToList();
        return Build("Foco/Pragas encontradas", months, [new ChartSeries("PRAGAS ENCONTRADAS", "column", totals)], ["Data", "Pragas Encontradas"]) with { DrillDowns = drill };
    }

    private async Task<MonthlyReport> ProductPlague(IQueryable<Order> orders, List<(int Y, int M)> months, CancellationToken ct)
    {
        var plagues = await PlaguesFound(orders, months, ct);
        var consumption = await ConsumptionRows(orders).ToListAsync(ct);
        var insecticide = months.Select(m => consumption.Where(r => r.Type == LiquidInsecticide && r.Y == m.Y && r.M == m.M).Sum(r => r.Quantity)).ToArray();
        return Build("Inseticidas X Pragas", months,
            [plagues.Series[0], new ChartSeries("INSETICIDA (ml)", "line", insecticide)],
            ["Data", "Pragas Encontradas (un)", "Consumo (ml)"]);
    }

    private async Task<MonthlyReport> Pheromones(IQueryable<Order> orders, List<(int Y, int M)> months, CancellationToken ct)
    {
        var rows = await db.OrderDetailsPheromones.AsNoTracking()
            .Join(db.OrderDetails, p => p.OrderDetailId, d => d.Id, (p, d) => new { p, d })
            .Join(orders, x => x.d.OrderId, o => o.Id, (x, o) => new { o.OrderDate.Year, o.OrderDate.Month, x.p.Type, x.p.Quantity })
            .ToListAsync(ct);
        double[] Sum(string type) => months.Select(m => (double)rows.Where(r => r.Type == type && r.Year == m.Y && r.Month == m.M).Sum(r => r.Quantity)).ToArray();
        return Build("Armadilhas de Feromônios", months,
            [new ChartSeries("Traça", "column", Sum("Traça")), new ChartSeries("Besouro", "column", Sum("Besouro"))],
            ["Data", "Traça", "Besouro"]);
    }

    /// <summary>
    /// Armadilhas luminosas: total do mês = quantidades sem espécie + contagens por inseto (só pontos identificados),
    /// com tabela por mês e por ponto nas colunas fixas do legado.
    /// </summary>
    private async Task<MonthlyReport> Luminous(IQueryable<Order> orders, List<(int Y, int M)> months, CancellationToken ct)
    {
        var traps = await db.OrderDetailsLuminous.AsNoTracking()
            .Where(l => l.Point != null && l.Point != "")
            .Join(db.OrderDetails, l => l.OrderDetailId, d => d.Id, (l, d) => new { l, d })
            .Join(orders, x => x.d.OrderId, o => o.Id, (x, o) => new { x.l.Id, o.OrderDate.Year, o.OrderDate.Month, x.l.Point, x.l.Quantity })
            .ToListAsync(ct);
        var trapIds = traps.Select(t => t.Id).ToList();
        var counts = trapIds.Count == 0 ? [] : await db.LuminousPlaquePlagues.AsNoTracking()
            .Where(p => trapIds.Contains(p.OrderDetailsLuminousId))
            .Select(p => new { p.OrderDetailsLuminousId, p.PlagueId, p.Quantity })
            .ToListAsync(ct);

        int[] Values(IEnumerable<int> ids, IEnumerable<int> unspecified)
        {
            var idSet = ids.ToHashSet();
            var mine = counts.Where(c => idSet.Contains(c.OrderDetailsLuminousId)).ToList();
            return [.. LuminousInsects.Codes.Select(code => mine.Where(c => c.PlagueId == code).Sum(c => Math.Max(0, c.Quantity))), unspecified.Sum()];
        }

        var luminous = months.Select(m =>
        {
            var monthTraps = traps.Where(t => t.Year == m.Y && t.Month == m.M).ToList();
            var points = monthTraps.GroupBy(t => t.Point)
                .Select(g => new LuminousPoint(g.Key, Values(g.Select(t => t.Id), g.Select(t => t.Quantity))))
                .OrderBy(p => int.TryParse(p.Point, out var n) ? n : int.MaxValue).ThenBy(p => p.Point)
                .ToList();
            return new LuminousMonth(Label(m), Values(monthTraps.Select(t => t.Id), monthTraps.Select(t => t.Quantity)), points);
        }).ToList();

        var totals = luminous.Select(l => (double)l.Totals.Sum()).ToArray();
        var columns = LuminousInsects.Columns(tenant.Tenant.Branding.OtherPlaguesLabel);
        var drill = luminous.Select(l => new DrillDown(l.Category, l.Points.Select(p => new NamedQuantity(p.Point, p.Values.Sum())).ToList())).ToList();
        var table = new ReportTable(["Mês", .. columns], luminous.Select(l => (IReadOnlyList<string>)[l.Category, .. l.Totals.Select(Format)]).ToList());

        return new MonthlyReport("Armadilhas Luminosas", months.Select(Label).ToList(),
            [new ChartSeries("Encontrados em Arms. Luminosas", "column", totals)], table, drill, luminous, columns);
    }

    private async Task<MonthlyReport> RodentsDead(IQueryable<Order> orders, List<(int Y, int M)> months, CancellationToken ct)
    {
        var rows = await DeadRodentRows(orders).ToListAsync(ct);
        var totals = months.Select(m => (double)rows.Where(r => r.Y == m.Y && r.M == m.M).Sum(r => r.Quantity)).ToArray();
        var drill = DrillByName(rows.Select(r => (r.Y, r.M, Name: (r.Location ?? "").ToLowerInvariant(), (double)r.Quantity)), months);
        return Build("Roedores Mortos", months, [new ChartSeries("Roedores Mortos", "column", totals)], ["Data", "Individuos Mortos"]) with { DrillDowns = drill };
    }

    private async Task<MonthlyReport> GlueTraps(IQueryable<Order> orders, List<(int Y, int M)> months, CancellationToken ct)
    {
        var rows = await GlueTrapRows(orders, "capturado").ToListAsync(ct);
        var totals = months.Select(m => (double)rows.Count(r => r.Y == m.Y && r.M == m.M)).ToArray();
        var drill = DrillByName(rows.Select(r => (r.Y, r.M, Name: (r.Point ?? "").ToLowerInvariant(), 1d)), months);
        return Build("Placa de Cola/Armadilha Mecânica", months,
            [new ChartSeries("Capturados em Placa de Cola/Armadilha Mecânica", "column", totals)],
            ["Data", "Placa de Cola/Armadilha Mecânica"]) with { DrillDowns = drill };
    }

    private async Task<MonthlyReport> GnawedBaits(IQueryable<Order> orders, List<(int Y, int M)> months, CancellationToken ct)
    {
        var rows = await db.OrderDetailsBaitings.AsNoTracking()
            .Where(b => b.Type == "Roída")
            .Join(db.OrderDetails, b => b.OrderDetailId, d => d.Id, (b, d) => new { b, d })
            .Join(orders, x => x.d.OrderId, o => o.Id, (x, o) => new { Y = o.OrderDate.Year, M = o.OrderDate.Month, x.b.Point })
            .ToListAsync(ct);
        var totals = months.Select(m => (double)rows.Count(r => r.Y == m.Y && r.M == m.M)).ToArray();
        var drill = DrillByName(rows.Select(r => (r.Y, r.M, Name: (r.Point ?? "").ToLowerInvariant(), 1d)), months);
        return Build("Iscas Roídas", months, [new ChartSeries("Iscas Roídas", "column", totals)], ["Data", "Iscas Roidas"]) with { DrillDowns = drill };
    }

    private async Task<MonthlyReport> RodentsRodenticide(IQueryable<Order> orders, List<(int Y, int M)> months, CancellationToken ct)
    {
        var dead = await MonthlySum(DeadRodentRows(orders), months, ct);
        var consumption = await ConsumptionRows(orders).ToListAsync(ct);
        var rodenticideKg = months.Select(m => consumption.Where(r => r.Type == Rodenticide && r.Y == m.Y && r.M == m.M).Sum(r => r.Quantity) / 1000).ToArray();
        return Build("Rodenticidas x Roedores", months,
            [new ChartSeries("Roedores Mortos (un)", "column", dead), new ChartSeries("Rodenticida (kg)", "line", rodenticideKg)],
            ["Data", "Mortos (un)", "Rodenticida (kg)"]);
    }

    // ---------- Consultas base ----------

    private IQueryable<Order> ActiveOrders(int customerId, DateOnly from, DateOnly to)
    {
        var (start, end) = Bounds(from, to);
        return db.Orders.AsNoTracking().Where(o => o.CustomerId == customerId && o.Active && o.OrderDate >= start && o.OrderDate < end);
    }

    private sealed record ConsumptionRow(int Y, int M, int Type, string Product, double Quantity);

    private IQueryable<ConsumptionRow> ConsumptionRows(IQueryable<Order> orders) =>
        db.OrderConsumptions.AsNoTracking()
            .Join(db.OrderDetails, c => c.OrderDetailId, d => d.Id, (c, d) => new { c, d })
            .Join(orders, x => x.d.OrderId, o => o.Id, (x, o) => new ConsumptionRow(
                o.OrderDate.Year, o.OrderDate.Month, x.c.PlagueProduct!.Product!.ProductTypeId, x.c.PlagueProduct.Product.Name, x.c.Quantity));

    private sealed record DeadRodentRow(int Y, int M, int Quantity, string? Location);

    private IQueryable<DeadRodentRow> DeadRodentRows(IQueryable<Order> orders) =>
        db.OrderDetailsPlagues.AsNoTracking()
            .Where(p => p.Plague!.PlagueType == "Roedor" && p.PlagueStatusId == DeadStatus)
            .Join(db.OrderDetails, p => p.OrderDetailId, d => d.Id, (p, d) => new { p, d })
            .Join(orders, x => x.d.OrderId, o => o.Id, (x, o) => new DeadRodentRow(o.OrderDate.Year, o.OrderDate.Month, x.p.Quantity, x.p.PlagueLocationDescription));

    private sealed record GlueRow(int Y, int M, string? Point);

    private IQueryable<GlueRow> GlueTrapRows(IQueryable<Order> orders, string type) =>
        db.OrderDetailsPlaquesOfGlue.AsNoTracking()
            .Where(g => g.Type == type)
            .Join(db.OrderDetails, g => g.OrderDetailId, d => d.Id, (g, d) => new { g, d })
            .Join(orders, x => x.d.OrderId, o => o.Id, (x, o) => new GlueRow(o.OrderDate.Year, o.OrderDate.Month, x.g.Point));

    private static async Task<double[]> MonthlyCount(IQueryable<GlueRow> rows, List<(int Y, int M)> months, CancellationToken ct)
    {
        var data = await rows.ToListAsync(ct);
        return months.Select(m => (double)data.Count(r => r.Y == m.Y && r.M == m.M)).ToArray();
    }

    private static async Task<double[]> MonthlySum(IQueryable<DeadRodentRow> rows, List<(int Y, int M)> months, CancellationToken ct)
    {
        var data = await rows.ToListAsync(ct);
        return months.Select(m => (double)data.Where(r => r.Y == m.Y && r.M == m.M).Sum(r => r.Quantity)).ToArray();
    }

    // ---------- Montagem ----------

    private static MonthlyReport Build(string title, List<(int Y, int M)> months, IReadOnlyList<ChartSeries> series, IReadOnlyList<string> headers)
    {
        var categories = months.Select(Label).ToList();
        var rows = categories.Select((c, i) => (IReadOnlyList<string>)[c, .. series.Select(s => Format(s.Data[i]))]).ToList();
        return new MonthlyReport(title, categories, series, new ReportTable(headers, rows));
    }

    private static List<DrillDown> DrillByName(IEnumerable<(int Y, int M, string Name, double Quantity)> rows, List<(int Y, int M)> months)
    {
        var list = rows.ToList();
        return months.Select(m => new DrillDown(Label(m),
            list.Where(r => r.Y == m.Y && r.M == m.M)
                .GroupBy(r => r.Name)
                .Select(g => new NamedQuantity(g.Key, g.Sum(r => r.Quantity)))
                // Pontos numéricos em ordem numérica, como no legado.
                .OrderBy(x => int.TryParse(x.Name, out var n) ? n : int.MaxValue).ThenBy(x => x.Name)
                .ToList())).ToList();
    }

    private static List<(int Y, int M)> Months(DateOnly from, DateOnly to)
    {
        var list = new List<(int, int)>();
        for (var d = new DateOnly(from.Year, from.Month, 1); d <= to; d = d.AddMonths(1)) list.Add((d.Year, d.Month));
        return list;
    }

    private static string Label((int Y, int M) m) => $"{m.M:00}/{m.Y}";

    private static string Format(double value) => value.ToString("0.###", CultureInfo.GetCultureInfo("pt-BR"));

    private static string Format(int value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>Intervalo de datas da OS: dia inicial inclusivo até o fim do dia final (o legado perdia o último dia).</summary>
    private static (DateTime Start, DateTime End) Bounds(DateOnly from, DateOnly to) =>
        (from.ToDateTime(TimeOnly.MinValue), to.AddDays(1).ToDateTime(TimeOnly.MinValue));

    private static string LargerUnit(string unit) => unit.ToUpperInvariant() switch
    {
        "ML" => "L",
        "G" => "KG",
        "UN" => "UN",
        _ => unit,
    };
}
