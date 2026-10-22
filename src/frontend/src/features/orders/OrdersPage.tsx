import { useState } from 'react'
import { Button, Group, Pagination, Paper, Select, Stack } from '@mantine/core'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { IconPlus } from '@tabler/icons-react'
import dayjs from 'dayjs'
import { useNavigate, useSearchParams } from 'react-router'
import { api } from '../../api/client'
import type { OrderListItem, Paged } from '../../api/types'
import { useCustomerId } from '../../app/guards'
import { DateRangeFilter, Loading, PageHeader } from '../../components/common'
import { useLookups } from '../visits/VisitModal'
import { OrderList } from './OrderList'

/** OS da empresa: filtros por período, cliente e técnico (como na tela do legado). */
export function OrdersPage() {
  const navigate = useNavigate()
  const lookups = useLookups()
  const [range, setRange] = useState<[string | null, string | null]>([
    dayjs().subtract(1, 'month').format('YYYY-MM-DD'),
    dayjs().format('YYYY-MM-DD'),
  ])
  const [customerId, setCustomerId] = useState<string | null>(null)
  // Vindo do relatório de produtividade: /os?technician=ID
  const [params] = useSearchParams()
  const [technicianId, setTechnicianId] = useState<string | null>(params.get('technician'))
  const [page, setPage] = useState(1)

  const orders = useQuery({
    queryKey: ['orders', range, customerId, technicianId, page],
    queryFn: () =>
      api.get<Paged<OrderListItem>>('/api/orders', { from: range[0], to: range[1], customerId, technicianId, page, pageSize: 15 }),
    placeholderData: keepPreviousData,
  })

  return (
    <>
      <PageHeader
        title="Ordens de Serviço"
        actions={
          <Button leftSection={<IconPlus size={16} />} onClick={() => navigate('/os/nova')}>
            Lançar OS
          </Button>
        }
      />
      <Stack>
        <Paper p="sm">
          <Group align="flex-end" gap="sm">
            <DateRangeFilter
              from={range[0]}
              to={range[1]}
              onApply={(f, t) => {
                setRange([f, t])
                setPage(1)
              }}
            />
            <Select
              label="Cliente"
              placeholder="Todos"
              data={lookups.customers}
              searchable
              clearable
              value={customerId}
              onChange={(v) => {
                setCustomerId(v)
                setPage(1)
              }}
              w={240}
            />
            <Select
              label="Técnico"
              placeholder="Todos"
              data={lookups.technicians}
              searchable
              clearable
              value={technicianId}
              onChange={(v) => {
                setTechnicianId(v)
                setPage(1)
              }}
              w={200}
            />
          </Group>
        </Paper>
        {orders.isPending ? <Loading /> : <OrderList orders={orders.data?.items ?? []} linkTo={(o) => `/os/${o.id}`} />}
        {(orders.data?.totalPages ?? 0) > 1 && (
          <Group justify="center">
            <Pagination total={orders.data!.totalPages} value={page} onChange={setPage} />
          </Group>
        )}
      </Stack>
    </>
  )
}

/** OS do cliente (área do cliente): últimos 13 meses por padrão, como no legado. */
export function CustomerOrdersPage() {
  const customerId = useCustomerId()
  const [range, setRange] = useState<[string | null, string | null]>([
    dayjs().subtract(13, 'month').format('YYYY-MM-DD'),
    dayjs().format('YYYY-MM-DD'),
  ])
  const [page, setPage] = useState(1)

  const orders = useQuery({
    queryKey: ['orders', 'customer', customerId, range, page],
    queryFn: () => api.get<Paged<OrderListItem>>('/api/orders', { customerId, from: range[0], to: range[1], page, pageSize: 10 }),
    placeholderData: keepPreviousData,
  })

  return (
    <>
      <PageHeader title="Ordens de Serviço" />
      <Stack>
        <DateRangeFilter
          from={range[0]}
          to={range[1]}
          onApply={(f, t) => {
            setRange([f, t])
            setPage(1)
          }}
        />
        {orders.isPending ? <Loading /> : <OrderList orders={orders.data?.items ?? []} linkTo={(o) => `/c/${customerId}/os/${o.id}`} />}
        {(orders.data?.totalPages ?? 0) > 1 && (
          <Group justify="center">
            <Pagination total={orders.data!.totalPages} value={page} onChange={setPage} />
          </Group>
        )}
      </Stack>
    </>
  )
}
