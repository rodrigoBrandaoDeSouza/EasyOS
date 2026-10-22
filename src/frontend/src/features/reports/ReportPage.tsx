import { Fragment, useState } from 'react'
import { ActionIcon, Group, Paper, Stack, Table, Text } from '@mantine/core'
import { useQuery } from '@tanstack/react-query'
import { IconChevronDown, IconChevronRight, IconPrinter } from '@tabler/icons-react'
import dayjs from 'dayjs'
import { useParams } from 'react-router'
import { api } from '../../api/client'
import type { MonthlyReport, NonConformityReportItem } from '../../api/types'
import { useCustomerId } from '../../app/guards'
import { DateRangeFilter, Empty, Loading, PageHeader } from '../../components/common'
import { fmtDate, fmtNumber } from '../../lib/format'
import { ReportChart } from './ReportChart'
import { CUSTOMER_REPORTS } from './reportCatalog'

function defaultRange(): [string, string] {
  // Mesmo período padrão do legado: do início do mês de 13 meses atrás até hoje.
  return [dayjs().subtract(13, 'month').startOf('month').format('YYYY-MM-DD'), dayjs().format('YYYY-MM-DD')]
}

export function ReportPage() {
  const { type = '' } = useParams()
  const customerId = useCustomerId()
  const [range, setRange] = useState<[string | null, string | null]>(defaultRange())
  const meta = CUSTOMER_REPORTS.find((r) => r.type === type)

  return (
    <>
      <PageHeader
        title={meta?.menu ?? 'Relatório'}
        actions={
          <ActionIcon variant="light" size="lg" onClick={() => window.print()} aria-label="Imprimir relatório">
            <IconPrinter size={18} />
          </ActionIcon>
        }
      />
      <Stack>
        <DateRangeFilter key={type} from={range[0]} to={range[1]} onApply={(f, t) => setRange([f, t])} />
        {type === 'non-conformities' ? (
          <NonConformitiesReport customerId={customerId} from={range[0]} to={range[1]} />
        ) : (
          <MonthlyReportView customerId={customerId} type={type} from={range[0]} to={range[1]} unit={meta?.unit} />
        )}
      </Stack>
    </>
  )
}

function MonthlyReportView({ customerId, type, from, to, unit }: { customerId: number; type: string; from: string | null; to: string | null; unit?: string }) {
  const report = useQuery({
    queryKey: ['report', customerId, type, from, to],
    queryFn: () => api.get<MonthlyReport>(`/api/customers/${customerId}/reports/${type}`, { from, to }),
  })
  const [open, setOpen] = useState<string | null>(null)

  if (report.isPending) return <Loading />
  if (report.isError) return <Text c="red">{report.error.message}</Text>
  const r = report.data
  const drill = (category: string) => r.drillDowns?.find((d) => d.category === category)?.items ?? []

  return (
    <>
      <Paper p="md">
        <ReportChart report={r} unit={unit} />
      </Paper>
      <Paper p="md">
        {r.luminous ? (
          <LuminousTable report={r} />
        ) : (
          <Table.ScrollContainer minWidth={420}>
            <Table>
              <Table.Thead>
                <Table.Tr>
                  {r.drillDowns && <Table.Th w={36} />}
                  {r.table.headers.map((h) => (
                    <Table.Th key={h}>{h}</Table.Th>
                  ))}
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {r.table.rows.map((row) => {
                  const items = r.drillDowns ? drill(row[0]) : []
                  const expanded = open === row[0]
                  return (
                    <Fragment key={row[0]}>
                      <Table.Tr
                        style={{ cursor: items.length ? 'pointer' : undefined }}
                        onClick={() => items.length && setOpen(expanded ? null : row[0])}
                      >
                        {r.drillDowns && <Table.Td>{items.length > 0 && (expanded ? <IconChevronDown size={16} /> : <IconChevronRight size={16} />)}</Table.Td>}
                        {row.map((cell, i) => (
                          <Table.Td key={i} fw={i === 0 ? 600 : undefined}>
                            {cell}
                          </Table.Td>
                        ))}
                      </Table.Tr>
                      {expanded &&
                        items.map((item) => (
                          <Table.Tr key={item.name} bg="gray.0">
                            <Table.Td />
                            <Table.Td pl="xl">
                              <Text size="sm" c="dimmed">
                                {item.name || '(sem identificação)'}
                              </Text>
                            </Table.Td>
                            <Table.Td>{fmtNumber(item.quantity)}</Table.Td>
                          </Table.Tr>
                        ))}
                    </Fragment>
                  )
                })}
              </Table.Tbody>
            </Table>
          </Table.ScrollContainer>
        )}
        {r.drillDowns && (
          <Text size="xs" c="dimmed" mt="xs">
            Clique em um mês para ver o detalhamento por ponto.
          </Text>
        )}
      </Paper>
    </>
  )
}

/** Armadilhas luminosas: totais por inseto no mês e detalhamento por ponto. */
function LuminousTable({ report }: { report: MonthlyReport }) {
  const [open, setOpen] = useState<string | null>(null)
  const columns = report.luminousColumns ?? []
  return (
    <Table.ScrollContainer minWidth={640}>
      <Table>
        <Table.Thead>
          <Table.Tr>
            <Table.Th w={36} />
            <Table.Th>Mês</Table.Th>
            {columns.map((c) => (
              <Table.Th key={c} ta="right">
                {c}
              </Table.Th>
            ))}
          </Table.Tr>
        </Table.Thead>
        <Table.Tbody>
          {(report.luminous ?? []).map((m) => {
            const expanded = open === m.category
            return (
              <Fragment key={m.category}>
                <Table.Tr style={{ cursor: m.points.length ? 'pointer' : undefined }} onClick={() => m.points.length && setOpen(expanded ? null : m.category)}>
                  <Table.Td>{m.points.length > 0 && (expanded ? <IconChevronDown size={16} /> : <IconChevronRight size={16} />)}</Table.Td>
                  <Table.Td fw={600}>{m.category}</Table.Td>
                  {m.totals.map((v, i) => (
                    <Table.Td key={i} ta="right">
                      {v}
                    </Table.Td>
                  ))}
                </Table.Tr>
                {expanded &&
                  m.points.map((p) => (
                    <Table.Tr key={p.point} bg="gray.0">
                      <Table.Td />
                      <Table.Td pl="xl">{p.point}</Table.Td>
                      {p.values.map((v, i) => (
                        <Table.Td key={i} ta="right">
                          {v}
                        </Table.Td>
                      ))}
                    </Table.Tr>
                  ))}
              </Fragment>
            )
          })}
        </Table.Tbody>
      </Table>
    </Table.ScrollContainer>
  )
}

function NonConformitiesReport({ customerId, from, to }: { customerId: number; from: string | null; to: string | null }) {
  const items = useQuery({
    queryKey: ['report', customerId, 'non-conformities', from, to],
    queryFn: () => api.get<NonConformityReportItem[]>(`/api/customers/${customerId}/reports/non-conformities`, { from, to }),
  })
  if (items.isPending) return <Loading />
  if (!items.data?.length) return <Empty>Nenhuma não conformidade no período.</Empty>
  return (
    <Paper p="md">
      <Table.ScrollContainer minWidth={700}>
        <Table>
          <Table.Thead>
            <Table.Tr>
              <Table.Th w={70}>OS</Table.Th>
              <Table.Th w={100}>Data</Table.Th>
              <Table.Th>Área e Local</Table.Th>
              <Table.Th>Não Conformidade</Table.Th>
              <Table.Th>Ação Sugerida</Table.Th>
            </Table.Tr>
          </Table.Thead>
          <Table.Tbody>
            {items.data.map((n, i) => (
              <Table.Tr key={i}>
                <Table.Td>{n.sequence}</Table.Td>
                <Table.Td>{fmtDate(n.orderDate)}</Table.Td>
                <Table.Td>{n.place}</Table.Td>
                <Table.Td>{n.description}</Table.Td>
                <Table.Td>{n.suggestion}</Table.Td>
              </Table.Tr>
            ))}
          </Table.Tbody>
        </Table>
      </Table.ScrollContainer>
      <Group justify="flex-end" mt="xs">
        <Text size="xs" c="dimmed">
          {items.data.length} não conformidade(s)
        </Text>
      </Group>
    </Paper>
  )
}
