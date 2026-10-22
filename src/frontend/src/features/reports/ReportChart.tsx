import { CompositeChart } from '@mantine/charts'
import type { MonthlyReport } from '../../api/types'

const PALETTE = ['brand.6', 'orange.6', 'grape.6', 'teal.6', 'red.6', 'indigo.6']

/** Gráfico mensal: séries "column" viram barras e "line" viram linhas (como os gráficos do legado). */
export function ReportChart({ report, height = 280, unit }: { report: MonthlyReport; height?: number; unit?: string }) {
  const data = report.categories.map((category, i) => {
    const row: Record<string, string | number> = { month: category }
    report.series.forEach((s) => (row[s.name] = s.data[i] ?? 0))
    return row
  })
  return (
    <CompositeChart
      h={height}
      data={data}
      dataKey="month"
      withLegend
      legendProps={{ verticalAlign: 'bottom' }}
      yAxisLabel={unit && unit.trim() ? unit : undefined}
      valueFormatter={(v) => new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 3 }).format(v)}
      series={report.series.map((s, i) => ({
        name: s.name,
        color: PALETTE[i % PALETTE.length],
        type: s.type === 'line' ? 'line' : 'bar',
      }))}
    />
  )
}
