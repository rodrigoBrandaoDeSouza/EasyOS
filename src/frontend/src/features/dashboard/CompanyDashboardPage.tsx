import { useState } from 'react'
import { BarChart, DonutChart } from '@mantine/charts'
import { Grid, Group, Paper, SimpleGrid, Stack, Text, Title } from '@mantine/core'
import { useQuery } from '@tanstack/react-query'
import { IconBuildingStore, IconClipboardCheck, IconUsersGroup } from '@tabler/icons-react'
import dayjs from 'dayjs'
import { api } from '../../api/client'
import type { CompanyDashboard, VisitListItem } from '../../api/types'
import { DateRangeFilter, Empty, Loading, PageHeader } from '../../components/common'
import { fmtNumber } from '../../lib/format'

const COLORS = ['brand.6', 'orange.6', 'grape.6', 'teal.6', 'red.6', 'indigo.6', 'cyan.6', 'lime.6', 'pink.6', 'yellow.6']

function Stat({ label, value, icon: Icon }: { label: string; value: number; icon: typeof IconUsersGroup }) {
  return (
    <Paper p="md">
      <Group justify="space-between">
        <Stack gap={0}>
          <Text size="xs" c="dimmed" tt="uppercase" fw={600}>
            {label}
          </Text>
          <Text fz={28} fw={700}>
            {value}
          </Text>
        </Stack>
        <Icon size={32} color="var(--mantine-color-brand-6)" stroke={1.5} />
      </Group>
    </Paper>
  )
}

/** Dashboard da empresa: equipe, OS do período, clientes ativos, gráficos e próximas visitas. */
export function CompanyDashboardPage() {
  // Padrão do legado: mês corrente.
  const [range, setRange] = useState<[string | null, string | null]>([
    dayjs().startOf('month').format('YYYY-MM-DD'),
    dayjs().endOf('month').format('YYYY-MM-DD'),
  ])
  const dashboard = useQuery({
    queryKey: ['dashboard', 'company', range],
    queryFn: () => api.get<CompanyDashboard>('/api/dashboard/company', { from: range[0], to: range[1] }),
  })
  const upcoming = useQuery({ queryKey: ['visits', 'upcoming'], queryFn: () => api.get<VisitListItem[]>('/api/visits/upcoming', { take: 10 }) })

  const d = dashboard.data
  return (
    <>
      <PageHeader title="Dashboard" actions={<DateRangeFilter from={range[0]} to={range[1]} onApply={(f, t) => setRange([f, t])} />} />
      {dashboard.isPending || !d ? (
        <Loading />
      ) : (
        <Grid>
          <Grid.Col span={{ base: 12, lg: 8 }}>
            <Stack>
              <SimpleGrid cols={{ base: 1, sm: 3 }}>
                <Stat label="Equipe técnica" value={d.technicians} icon={IconUsersGroup} />
                <Stat label="OS no período" value={d.serviceOrders} icon={IconClipboardCheck} />
                <Stat label="Clientes ativos" value={d.activeCustomers} icon={IconBuildingStore} />
              </SimpleGrid>
              <Paper p="md">
                <Title order={5} mb="sm">
                  OS atendidas por dia
                </Title>
                <BarChart
                  h={220}
                  data={d.days.map((day, i) => ({ day, 'OS Atendidas': d.ordersPerDay[i] }))}
                  dataKey="day"
                  series={[{ name: 'OS Atendidas', color: 'brand.6' }]}
                  tickLine="y"
                />
              </Paper>
              <SimpleGrid cols={{ base: 1, md: 2 }}>
                <Paper p="md">
                  <Title order={5} mb="sm">
                    OS por técnico
                  </Title>
                  {d.ordersPerTechnician.length ? (
                    <DonutChart
                      h={220}
                      withLabelsLine
                      withLabels
                      data={d.ordersPerTechnician.map((t, i) => ({ name: t.name, value: t.quantity, color: COLORS[i % COLORS.length] }))}
                      tooltipDataSource="segment"
                    />
                  ) : (
                    <Empty>Sem OS no período.</Empty>
                  )}
                </Paper>
                <Paper p="md">
                  <Title order={5} mb="sm">
                    Consumo de produtos
                  </Title>
                  {d.consumptionByProductType.length ? (
                    <BarChart
                      h={220}
                      data={d.consumptionByProductType.map((c) => ({ name: c.name, Quantidade: c.quantity }))}
                      dataKey="name"
                      series={[{ name: 'Quantidade', color: 'orange.6' }]}
                      valueFormatter={(v) => fmtNumber(v)}
                      withBarValueLabel
                    />
                  ) : (
                    <Empty>Sem consumo no período.</Empty>
                  )}
                </Paper>
              </SimpleGrid>
            </Stack>
          </Grid.Col>
          <Grid.Col span={{ base: 12, lg: 4 }}>
            <Paper p="md">
              <Title order={5} mb="sm">
                Próximas visitas
              </Title>
              {upcoming.isPending ? (
                <Loading />
              ) : !upcoming.data?.length ? (
                <Empty>Nenhuma visita agendada.</Empty>
              ) : (
                <Stack gap="xs">
                  {upcoming.data.map((v) => (
                    <Group key={v.id} gap="sm" wrap="nowrap">
                      <Stack gap={0} miw={64} ta="center" c={v.isLate ? 'red' : undefined}>
                        <Text fw={700}>{dayjs(v.visitDate).format('DD MMM')}</Text>
                        <Text size="xs">{dayjs(v.visitDate).format('HH:mm')}</Text>
                      </Stack>
                      <Stack gap={0} style={{ minWidth: 0 }}>
                        <Text size="sm" fw={500} lineClamp={1}>
                          {v.customerName}
                        </Text>
                        <Text size="xs" c="dimmed" lineClamp={1}>
                          {v.technicianName ?? 'Técnico não confirmado'}
                        </Text>
                      </Stack>
                    </Group>
                  ))}
                </Stack>
              )}
            </Paper>
          </Grid.Col>
        </Grid>
      )}
    </>
  )
}
