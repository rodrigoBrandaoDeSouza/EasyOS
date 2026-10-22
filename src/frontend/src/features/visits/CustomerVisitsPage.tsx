import { useState } from 'react'
import { Group, Pagination, Stack } from '@mantine/core'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import dayjs from 'dayjs'
import { api } from '../../api/client'
import type { Paged, VisitListItem } from '../../api/types'
import { useCustomerId } from '../../app/guards'
import { DateRangeFilter, Empty, Loading, PageHeader } from '../../components/common'
import { VisitCard } from './VisitCard'

/** Cronograma de visitas do cliente (passadas e futuras), como na área do cliente do legado. */
export function CustomerVisitsPage() {
  const customerId = useCustomerId()
  const [range, setRange] = useState<[string | null, string | null]>([
    dayjs().startOf('year').format('YYYY-MM-DD'),
    dayjs().add(12, 'month').format('YYYY-MM-DD'),
  ])
  const [page, setPage] = useState(1)

  const visits = useQuery({
    queryKey: ['visits', 'customer', customerId, range, page],
    queryFn: () => api.get<Paged<VisitListItem>>('/api/visits', { customerId, from: range[0], to: range[1], page, pageSize: 10 }),
    placeholderData: keepPreviousData,
  })

  return (
    <>
      <PageHeader title="Cronograma de Visitas" />
      <Stack>
        <DateRangeFilter
          from={range[0]}
          to={range[1]}
          onApply={(f, t) => {
            setRange([f, t])
            setPage(1)
          }}
        />
        {visits.isPending ? (
          <Loading />
        ) : !visits.data?.items.length ? (
          <Empty>Nenhuma visita no período.</Empty>
        ) : (
          visits.data.items.map((v) => (
            <VisitCard key={v.id} visit={v} orderLink={v.orderId ? `/c/${customerId}/os/${v.orderId}` : undefined} />
          ))
        )}
        {(visits.data?.totalPages ?? 0) > 1 && (
          <Group justify="center">
            <Pagination total={visits.data!.totalPages} value={page} onChange={setPage} />
          </Group>
        )}
      </Stack>
    </>
  )
}
