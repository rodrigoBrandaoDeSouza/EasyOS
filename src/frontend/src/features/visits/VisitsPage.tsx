import { useState } from 'react'
import { ActionIcon, Button, Grid, Group, Paper, Select, Stack, Text, Title, Tooltip } from '@mantine/core'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { IconPencil, IconPlus, IconTrash } from '@tabler/icons-react'
import FullCalendar from '@fullcalendar/react'
import dayGridPlugin from '@fullcalendar/react/daygrid'
import interactionPlugin from '@fullcalendar/react/interaction'
import ptBrLocale from '@fullcalendar/react/locales/pt-br'
import classicThemePlugin from '@fullcalendar/react/themes/classic'
import '@fullcalendar/react/skeleton.css'
import '@fullcalendar/react/themes/classic/theme.css'
import '@fullcalendar/react/themes/classic/palette.css'
import dayjs from 'dayjs'
import { api } from '../../api/client'
import type { CalendarItem, Paged, VisitListItem } from '../../api/types'
import { confirmAction, Empty, Loading, PageHeader } from '../../components/common'
import { fmtDate } from '../../lib/format'
import { notifyError, notifySuccess } from '../../lib/notify'
import { VisitCard } from './VisitCard'
import { VisitModal } from './VisitModal'

export function VisitsPage() {
  const thisYear = new Date().getFullYear()
  const [year, setYear] = useState(thisYear)
  const [day, setDay] = useState(dayjs().format('YYYY-MM-DD'))
  const [editing, setEditing] = useState<VisitListItem | 'new' | null>(null)
  const queryClient = useQueryClient()

  const calendar = useQuery({
    queryKey: ['visits', 'calendar', year],
    queryFn: () => api.get<CalendarItem[]>('/api/visits/calendar', { year }),
  })

  const dayVisits = useQuery({
    queryKey: ['visits', 'day', day],
    queryFn: () => api.get<Paged<VisitListItem>>('/api/visits', { from: day, to: day, pageSize: 100 }),
  })

  const remove = useMutation({
    mutationFn: (id: number) => api.delete(`/api/visits/${id}`),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['visits'] })
      notifySuccess('Visita excluída com sucesso')
    },
    onError: notifyError,
  })

  const years = Array.from({ length: thisYear - 2015 + 3 }, (_, i) => String(2015 + i)).reverse()

  return (
    <>
      <PageHeader
        title="Visitas"
        actions={
          <Button leftSection={<IconPlus size={16} />} onClick={() => setEditing('new')}>
            Agendar visita
          </Button>
        }
      />
      <Grid>
        <Grid.Col span={{ base: 12, lg: 7 }}>
          <Paper p="md">
            <Group justify="flex-end" mb="xs">
              <Select size="xs" w={110} data={years} value={String(year)} onChange={(v) => v && setYear(Number(v))} aria-label="Ano" />
            </Group>
            <FullCalendar
              key={year}
              plugins={[classicThemePlugin, dayGridPlugin, interactionPlugin]}
              initialView="dayGridMonth"
              initialDate={year === thisYear ? undefined : `${year}-01-01`}
              locale={ptBrLocale}
              height="auto"
              dayMaxEvents={3}
              validRange={{ start: `${year}-01-01`, end: `${year + 1}-01-01` }}
              events={(calendar.data ?? []).map((v) => ({
                id: String(v.id),
                title: v.customerName,
                start: v.visitDate,
                color: v.done ? 'var(--mantine-color-gray-5)' : 'var(--mantine-color-brand-6)',
              }))}
              dateClick={(info) => setDay(info.dateStr)}
              eventClick={(info) => setDay(dayjs(info.event.start).format('YYYY-MM-DD'))}
            />
          </Paper>
        </Grid.Col>
        <Grid.Col span={{ base: 12, lg: 5 }}>
          <Stack>
            <Group justify="space-between">
              <Title order={4}>Visitas de {fmtDate(day)}</Title>
              <Button size="xs" variant="light" leftSection={<IconPlus size={14} />} onClick={() => setEditing('new')}>
                Agendar neste dia
              </Button>
            </Group>
            {dayVisits.isPending ? (
              <Loading />
            ) : !dayVisits.data?.items.length ? (
              <Empty>Nenhuma visita neste dia.</Empty>
            ) : (
              dayVisits.data.items.map((v) => (
                <VisitCard
                  key={v.id}
                  visit={v}
                  orderLink={v.orderId ? `/os/${v.orderId}` : undefined}
                  actions={
                    v.active && !v.checkinDate ? (
                      <Group gap={4} wrap="nowrap">
                        <Tooltip label="Editar">
                          <ActionIcon variant="subtle" onClick={() => setEditing(v)} aria-label="Editar visita">
                            <IconPencil size={18} />
                          </ActionIcon>
                        </Tooltip>
                        <Tooltip label="Excluir">
                          <ActionIcon
                            variant="subtle"
                            color="red"
                            onClick={() => confirmAction('Deseja excluir a visita?', () => remove.mutate(v.id), 'Excluir', true)}
                            aria-label="Excluir visita"
                          >
                            <IconTrash size={18} />
                          </ActionIcon>
                        </Tooltip>
                      </Group>
                    ) : null
                  }
                />
              ))
            )}
            <Text size="xs" c="dimmed">
              Clique em um dia do calendário para ver as visitas.
            </Text>
          </Stack>
        </Grid.Col>
      </Grid>
      {editing && <VisitModal visit={editing === 'new' ? null : editing} defaultDate={day} onClose={() => setEditing(null)} />}
    </>
  )
}
