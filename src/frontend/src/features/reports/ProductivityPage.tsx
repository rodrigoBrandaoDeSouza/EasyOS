import { useState } from 'react'
import { Anchor, Paper, Stack, Table, Text } from '@mantine/core'
import { useQuery } from '@tanstack/react-query'
import dayjs from 'dayjs'
import { Link } from 'react-router'
import { api } from '../../api/client'
import type { ProductivityItem } from '../../api/types'
import { DateRangeFilter, Empty, Loading, PageHeader } from '../../components/common'

/** Relatório de produtividade dos técnicos (padrão do legado: do início do mês até hoje). */
export function ProductivityPage() {
  const [range, setRange] = useState<[string | null, string | null]>([dayjs().startOf('month').format('YYYY-MM-DD'), dayjs().format('YYYY-MM-DD')])
  const report = useQuery({
    queryKey: ['productivity', range],
    queryFn: () => api.get<ProductivityItem[]>('/api/reports/productivity', { from: range[0], to: range[1] }),
  })

  return (
    <>
      <PageHeader title="Relatório de Produtividade" />
      <Stack>
        <DateRangeFilter from={range[0]} to={range[1]} onApply={(f, t) => setRange([f, t])} />
        {report.isPending ? (
          <Loading />
        ) : !report.data?.length ? (
          <Empty>Nenhum técnico ativo.</Empty>
        ) : (
          <Paper p="md">
            <Table.ScrollContainer minWidth={640}>
              <Table>
                <Table.Thead>
                  <Table.Tr>
                    <Table.Th>Técnico</Table.Th>
                    <Table.Th ta="center">Horas trabalhadas</Table.Th>
                    <Table.Th ta="center">OS agendadas</Table.Th>
                    <Table.Th ta="center">Realizadas</Table.Th>
                    <Table.Th ta="center">Não realizadas</Table.Th>
                    <Table.Th ta="center">Pendentes</Table.Th>
                  </Table.Tr>
                </Table.Thead>
                <Table.Tbody>
                  {report.data.map((t) => (
                    <Table.Tr key={t.technicianId}>
                      <Table.Td>
                        <Anchor component={Link} to={`/os?technician=${t.technicianId}`}>
                          {t.technicianName}
                        </Anchor>
                      </Table.Td>
                      <Table.Td ta="center">
                        {t.hoursWorked}h {t.minutesWorked}m
                      </Table.Td>
                      <Table.Td ta="center">{t.scheduled}</Table.Td>
                      <Table.Td ta="center">{t.accomplished}</Table.Td>
                      <Table.Td ta="center">{t.notAccomplished}</Table.Td>
                      <Table.Td ta="center">{t.pending}</Table.Td>
                    </Table.Tr>
                  ))}
                </Table.Tbody>
              </Table>
            </Table.ScrollContainer>
            <Text size="xs" c="dimmed" mt="xs">
              Agendadas = visitas do técnico no período (exceto canceladas). Pendentes = agendadas − realizadas − não realizadas.
            </Text>
          </Paper>
        )}
      </Stack>
    </>
  )
}
