import type { ReactNode } from 'react'
import { Badge, Button, Center, Group, Loader, Stack, Text, Title } from '@mantine/core'
import { DatePickerInput } from '@mantine/dates'
import { modals } from '@mantine/modals'
import { IconCalendar, IconFilter } from '@tabler/icons-react'
import { useState } from 'react'

export function PageHeader({ title, subtitle, actions }: { title: string; subtitle?: ReactNode; actions?: ReactNode }) {
  return (
    <Group justify="space-between" mb="lg" align="flex-end" className="no-print">
      <Stack gap={2}>
        <Title order={2}>{title}</Title>
        {subtitle && (
          <Text c="dimmed" size="sm">
            {subtitle}
          </Text>
        )}
      </Stack>
      {actions && <Group gap="xs">{actions}</Group>}
    </Group>
  )
}

export function Loading() {
  return (
    <Center py="xl">
      <Loader />
    </Center>
  )
}

export function Empty({ children }: { children: ReactNode }) {
  return (
    <Center py="xl">
      <Text c="dimmed">{children}</Text>
    </Center>
  )
}

/** Diálogo de confirmação padrão para ações destrutivas ou que mudam status. */
export function confirmAction(message: string, onConfirm: () => void, confirmLabel = 'Confirmar', danger = false) {
  modals.openConfirmModal({
    title: 'Confirmação',
    centered: true,
    children: <Text size="sm">{message}</Text>,
    labels: { confirm: confirmLabel, cancel: 'Cancelar' },
    confirmProps: danger ? { color: 'red' } : undefined,
    onConfirm,
  })
}

export function ActiveBadge({ active }: { active: boolean }) {
  return (
    <Badge color={active ? 'teal' : 'gray'} variant="light">
      {active ? 'Ativo' : 'Inativo'}
    </Badge>
  )
}

const STATUS_COLORS: Record<string, string> = {
  pendente: 'orange',
  'não confirmada': 'yellow',
  confirmada: 'blue',
  realizada: 'teal',
  'não realizada': 'red',
  cancelada: 'gray',
}

export function VisitStatusBadge({ status }: { status: string }) {
  return (
    <Badge color={STATUS_COLORS[status] ?? 'gray'} variant="light" tt="none">
      {status}
    </Badge>
  )
}

/** Filtro de período (datas locais), usado em dashboards, listas e relatórios. */
export function DateRangeFilter({
  from,
  to,
  onApply,
}: {
  from: string | null
  to: string | null
  onApply: (from: string | null, to: string | null) => void
}) {
  const [value, setValue] = useState<[string | null, string | null]>([from, to])
  return (
    <Group gap="xs" align="flex-end" className="no-print">
      <DatePickerInput
        type="range"
        label="Período"
        placeholder="Selecione o período"
        valueFormat="DD/MM/YYYY"
        leftSection={<IconCalendar size={16} />}
        value={value}
        onChange={setValue}
        allowSingleDateInRange
        miw={260}
      />
      <Button leftSection={<IconFilter size={16} />} variant="light" onClick={() => onApply(value[0], value[1] ?? value[0])}>
        Filtrar
      </Button>
    </Group>
  )
}
