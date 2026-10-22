import { Badge, Group, Paper, Stack, Text, UnstyledButton } from '@mantine/core'
import { IconChevronRight } from '@tabler/icons-react'
import { useNavigate } from 'react-router'
import type { OrderListItem } from '../../api/types'
import { Empty } from '../../components/common'
import { fmtDate, fmtTime } from '../../lib/format'

export function OrderStatusBadge({ active, executed }: { active: boolean; executed: boolean }) {
  if (!active) return <Badge color="gray">Cancelada</Badge>
  return executed ? (
    <Badge color="teal" variant="light">
      Realizada
    </Badge>
  ) : (
    <Badge color="red" variant="light">
      Não realizada
    </Badge>
  )
}

export function OrderList({ orders, linkTo }: { orders: OrderListItem[]; linkTo: (o: OrderListItem) => string }) {
  const navigate = useNavigate()
  if (!orders.length) return <Empty>Nenhuma ordem de serviço no período.</Empty>
  return (
    <Stack gap="xs">
      {orders.map((o) => (
        <UnstyledButton key={o.id} onClick={() => navigate(linkTo(o))}>
          <Paper p="sm" style={{ opacity: o.active ? 1 : 0.6 }}>
            <Group justify="space-between" wrap="nowrap">
              <Group gap="lg" wrap="nowrap" align="flex-start">
                <Stack gap={0} miw={92}>
                  <Text fw={700}>{fmtDate(o.orderDate)}</Text>
                  <Text size="xs" c="dimmed">
                    das {fmtTime(o.timeStart)} às {fmtTime(o.timeEnd)}
                  </Text>
                </Stack>
                <Stack gap={2} style={{ minWidth: 0 }}>
                  <Group gap="xs">
                    <Text fw={600}>{o.customerName}</Text>
                    <OrderStatusBadge active={o.active} executed={o.serviceExecuted} />
                  </Group>
                  <Text size="sm">
                    <b>OS nº</b> {o.sequence} · <b>Realizada por:</b> {o.technicianName}
                  </Text>
                  {o.observations && (
                    <Text size="sm" c="dimmed" lineClamp={1}>
                      {o.observations}
                    </Text>
                  )}
                </Stack>
              </Group>
              <IconChevronRight size={18} color="var(--mantine-color-gray-5)" />
            </Group>
          </Paper>
        </UnstyledButton>
      ))}
    </Stack>
  )
}
