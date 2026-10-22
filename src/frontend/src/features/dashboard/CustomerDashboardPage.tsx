import { useState } from 'react'
import { Button, Grid, Group, Paper, SimpleGrid, Stack, Text, Title } from '@mantine/core'
import { useQuery } from '@tanstack/react-query'
import { IconCalendarEvent, IconCertificate, IconClipboardCheck } from '@tabler/icons-react'
import dayjs from 'dayjs'
import { Link } from 'react-router'
import { api } from '../../api/client'
import type { CustomerDashboard, OrderListItem, Paged, VisitListItem } from '../../api/types'
import { useCustomerId } from '../../app/guards'
import { DateRangeFilter, Loading, PageHeader } from '../../components/common'
import { fmtDate, fmtTime } from '../../lib/format'
import { ReportChart } from '../reports/ReportChart'

/** Dashboard do cliente: próxima visita, última OS (com certificado) e gráficos dos últimos 13 meses. */
export function CustomerDashboardPage() {
  const customerId = useCustomerId()
  const [range, setRange] = useState<[string | null, string | null]>([
    dayjs().subtract(13, 'month').startOf('month').format('YYYY-MM-DD'),
    dayjs().format('YYYY-MM-DD'),
  ])

  const dashboard = useQuery({
    queryKey: ['dashboard', 'customer', customerId, range],
    queryFn: () => api.get<CustomerDashboard>(`/api/customers/${customerId}/dashboard`, { from: range[0], to: range[1] }),
  })
  const next = useQuery({
    queryKey: ['visits', 'upcoming', customerId],
    queryFn: () => api.get<VisitListItem[]>('/api/visits/upcoming', { customerId, take: 1 }),
  })
  const last = useQuery({
    queryKey: ['orders', 'last', customerId],
    queryFn: () => api.get<Paged<OrderListItem>>('/api/orders', { customerId, pageSize: 1 }),
  })

  const lastOrder = last.data?.items[0]
  const nextVisit = next.data?.[0]

  return (
    <>
      <PageHeader title="Dashboard" />
      <Stack>
        <SimpleGrid cols={{ base: 1, sm: 2 }}>
          <Paper p="md">
            <Group gap="md" wrap="nowrap">
              <IconCalendarEvent size={36} color="var(--mantine-color-brand-6)" stroke={1.5} />
              <Stack gap={0}>
                <Text size="xs" c="dimmed" tt="uppercase" fw={600}>
                  Próxima visita
                </Text>
                <Text fw={700} size="lg">
                  {nextVisit ? `${fmtDate(nextVisit.visitDate)} às ${fmtTime(nextVisit.visitDate)}` : 'Não há visitas'}
                </Text>
              </Stack>
            </Group>
          </Paper>
          <Paper p="md">
            <Group justify="space-between" wrap="nowrap">
              <Group gap="md" wrap="nowrap">
                <IconClipboardCheck size={36} color="var(--mantine-color-brand-6)" stroke={1.5} />
                <Stack gap={0}>
                  <Text size="xs" c="dimmed" tt="uppercase" fw={600}>
                    Última visita realizada
                  </Text>
                  <Text fw={700} size="lg">
                    {lastOrder ? `${fmtDate(lastOrder.orderDate)} - ${fmtTime(lastOrder.timeStart)}` : 'Não há visitas'}
                  </Text>
                  {lastOrder && (
                    <Text size="sm" c="dimmed">
                      OS nº {lastOrder.sequence}
                    </Text>
                  )}
                </Stack>
              </Group>
              {lastOrder && lastOrder.active && lastOrder.serviceExecuted && (
                <Button
                  variant="light"
                  size="xs"
                  leftSection={<IconCertificate size={14} />}
                  component={Link}
                  to={`/imprimir/certificado/${lastOrder.id}`}
                  target="_blank"
                >
                  Certificado
                </Button>
              )}
            </Group>
          </Paper>
        </SimpleGrid>

        <DateRangeFilter from={range[0]} to={range[1]} onApply={(f, t) => setRange([f, t])} />

        {dashboard.isPending || !dashboard.data ? (
          <Loading />
        ) : (
          <Grid>
            <Grid.Col span={{ base: 12, md: 6 }}>
              <Paper p="md">
                <Title order={5} mb="sm">
                  Pragas encontradas
                </Title>
                <ReportChart report={dashboard.data.plaguesFound} height={240} unit="unidade" />
              </Paper>
            </Grid.Col>
            <Grid.Col span={{ base: 12, md: 6 }}>
              <Paper p="md">
                <Title order={5} mb="sm">
                  Roedores
                </Title>
                <ReportChart report={dashboard.data.rodents} height={240} unit="unidade" />
              </Paper>
            </Grid.Col>
            <Grid.Col span={12}>
              <Paper p="md">
                <Title order={5} mb="sm">
                  Consumo de produtos
                </Title>
                <ReportChart report={dashboard.data.consumption} height={260} />
              </Paper>
            </Grid.Col>
          </Grid>
        )}
      </Stack>
    </>
  )
}
