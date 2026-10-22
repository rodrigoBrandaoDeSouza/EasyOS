import { Alert, Button, Grid, Group, Modal, NumberInput, Select, Textarea } from '@mantine/core'
import { DateInput, TimeInput } from '@mantine/dates'
import { useForm } from '@mantine/form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import dayjs from 'dayjs'
import { api } from '../../api/client'
import type { LookupItem, VisitListItem } from '../../api/types'
import { useMe } from '../../auth/AuthContext'
import { browserOffsetMinutes, toApiDateTime } from '../../lib/format'
import { notifyError, notifySuccess } from '../../lib/notify'

export function useLookups() {
  const customers = useQuery({
    queryKey: ['lookup', 'customers'],
    queryFn: () => api.get<LookupItem[]>('/api/customers/lookup', { onlyActive: true }),
    staleTime: 60_000,
  })
  const technicians = useQuery({
    queryKey: ['lookup', 'technicians'],
    queryFn: () => api.get<LookupItem[]>('/api/company-users/lookup', { role: 'technician' }),
    staleTime: 60_000,
  })
  const users = useQuery({
    queryKey: ['lookup', 'users'],
    queryFn: () => api.get<LookupItem[]>('/api/company-users/lookup'),
    staleTime: 60_000,
  })
  const toOptions = (items?: LookupItem[]) => (items ?? []).map((i) => ({ value: String(i.id), label: i.name }))
  return {
    customers: toOptions(customers.data),
    technicians: toOptions(technicians.data),
    users: toOptions(users.data),
  }
}

/** Agendamento e edição de visita. Na edição de visita passada permite registrar o check-in manual. */
export function VisitModal({ visit, defaultDate, onClose }: { visit: VisitListItem | null; defaultDate?: string; onClose: () => void }) {
  const isNew = visit === null
  const me = useMe()
  const lookups = useLookups()
  const queryClient = useQueryClient()
  const initial = visit ? dayjs(visit.visitDate) : dayjs(defaultDate ?? undefined).hour(8).minute(0)
  const isPast = !isNew && dayjs(visit.visitDate).isBefore(dayjs())

  const form = useForm({
    initialValues: {
      customerId: visit ? String(visit.customerId) : '',
      technicianId: visit?.technicianId ? String(visit.technicianId) : '',
      responsibleId: visit ? String(visit.responsibleId) : String(me.id),
      date: initial.format('YYYY-MM-DD'),
      time: initial.format('HH:mm'),
      description: visit?.description ?? '',
      certificateDate: visit?.certificateDate ?? ('' as number | ''),
      checkinDate: '' as string | null,
      checkinTime: '',
    },
    validate: {
      customerId: (v) => (v ? null : 'Selecione o cliente'),
      responsibleId: (v) => (v ? null : 'Selecione o responsável'),
      date: (v) => (v ? null : 'Informe a data'),
      time: (v) => (/^\d{2}:\d{2}$/.test(v) ? null : 'Informe o horário'),
      checkinTime: (v, values) => (values.checkinDate && !/^\d{2}:\d{2}$/.test(v) ? 'Informe o horário do check-in' : null),
    },
  })

  const save = useMutation({
    mutationFn: (v: typeof form.values) => {
      const body = {
        customerId: Number(v.customerId),
        technicianId: v.technicianId ? Number(v.technicianId) : null,
        responsibleId: Number(v.responsibleId),
        visitDate: toApiDateTime(`${v.date}T${v.time}`),
        offsetMinutes: browserOffsetMinutes(),
        description: v.description,
        certificateDate: v.certificateDate === '' ? null : Number(v.certificateDate),
        checkinDate: v.checkinDate ? toApiDateTime(`${dayjs(v.checkinDate).format('YYYY-MM-DD')}T${v.checkinTime}`) : null,
      }
      return isNew ? api.post('/api/visits', body) : api.put(`/api/visits/${visit!.id}`, body)
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['visits'] })
      notifySuccess(isNew ? 'Visita agendada com sucesso' : 'Visita editada com sucesso')
      onClose()
    },
    onError: notifyError,
  })

  return (
    <Modal opened onClose={onClose} title={isNew ? 'Agendar visita' : `Visita #${visit!.id}`} size="lg">
      <form onSubmit={form.onSubmit((v) => save.mutate(v))}>
        <Grid gap="sm">
          <Grid.Col span={12}>
            <Select
              label="Cliente"
              data={isNew ? lookups.customers : [{ value: String(visit!.customerId), label: visit!.customerName }]}
              searchable
              withAsterisk
              disabled={!isNew}
              nothingFoundMessage="Nenhum cliente encontrado"
              {...form.getInputProps('customerId')}
            />
          </Grid.Col>
          <Grid.Col span={{ base: 12, sm: 6 }}>
            <DateInput label="Data" valueFormat="DD/MM/YYYY" withAsterisk {...form.getInputProps('date')} />
          </Grid.Col>
          <Grid.Col span={{ base: 12, sm: 6 }}>
            <TimeInput label="Horário" withAsterisk {...form.getInputProps('time')} />
          </Grid.Col>
          <Grid.Col span={{ base: 12, sm: 6 }}>
            <Select
              label="Técnico"
              description="Sem técnico, a visita fica como “não confirmada”"
              data={lookups.technicians}
              searchable
              clearable
              {...form.getInputProps('technicianId')}
            />
          </Grid.Col>
          <Grid.Col span={{ base: 12, sm: 6 }}>
            <Select label="Agendado por" data={lookups.users} searchable withAsterisk mt={22} {...form.getInputProps('responsibleId')} />
          </Grid.Col>
          <Grid.Col span={{ base: 12, sm: 6 }}>
            <NumberInput
              label="Validade do certificado (dias)"
              description="Opcional: substitui a validade do cadastro do cliente"
              min={0}
              max={999}
              {...form.getInputProps('certificateDate')}
            />
          </Grid.Col>
          <Grid.Col span={12}>
            <Textarea label="Descrição" autosize minRows={3} maxLength={8000} {...form.getInputProps('description')} />
          </Grid.Col>
          {(visit?.treatedAreas || (visit?.auxiliarTechnicians.length ?? 0) > 0) && (
            <Grid.Col span={12}>
              <Alert variant="light" p="xs">
                {visit?.treatedAreas && <div>Áreas tratadas: {visit.treatedAreas}</div>}
                {visit && visit.auxiliarTechnicians.length > 0 && <div>Técnicos auxiliares: {visit.auxiliarTechnicians.join(', ')}</div>}
              </Alert>
            </Grid.Col>
          )}
          {isPast && (
            <>
              <Grid.Col span={12}>
                <Alert variant="light" color="orange" p="xs">
                  Visita passada sem check-in. Se o atendimento foi feito sem o aplicativo, registre o check-in manual — a visita será encerrada.
                </Alert>
              </Grid.Col>
              <Grid.Col span={{ base: 12, sm: 6 }}>
                <DateInput
                  label="Data do check-in"
                  valueFormat="DD/MM/YYYY"
                  clearable
                  minDate={dayjs(visit!.visitDate).format('YYYY-MM-DD')}
                  maxDate={dayjs().format('YYYY-MM-DD')}
                  {...form.getInputProps('checkinDate')}
                />
              </Grid.Col>
              <Grid.Col span={{ base: 12, sm: 6 }}>
                <TimeInput label="Horário do check-in" {...form.getInputProps('checkinTime')} />
              </Grid.Col>
            </>
          )}
        </Grid>
        <Group justify="flex-end" mt="lg">
          <Button variant="default" onClick={onClose}>
            Cancelar
          </Button>
          <Button type="submit" loading={save.isPending}>
            Salvar
          </Button>
        </Group>
      </form>
    </Modal>
  )
}
