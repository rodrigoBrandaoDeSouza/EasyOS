import { Anchor, Badge, Group, Paper, Stack, Text } from '@mantine/core'
import { Link } from 'react-router'
import type { VisitListItem } from '../../api/types'
import { VisitStatusBadge } from '../../components/common'
import { fmtDate, fmtDateTime, fmtTime } from '../../lib/format'

/** Cartão de visita usado nas listas da empresa e do cliente. */
export function VisitCard({ visit, orderLink, actions }: { visit: VisitListItem; orderLink?: string; actions?: React.ReactNode }) {
  return (
    <Paper p="sm">
      <Group justify="space-between" align="flex-start" wrap="nowrap">
        <Stack gap={2} style={{ minWidth: 0 }}>
          <Group gap="xs">
            <Text fw={600}>{visit.customerName}</Text>
            <VisitStatusBadge status={visit.status} />
            {visit.isLate && visit.active && (
              <Badge color="red" variant="dot">
                Atrasada
              </Badge>
            )}
          </Group>
          <Text size="sm">
            <b>{fmtDate(visit.visitDate)}</b> às {fmtTime(visit.visitDate)}
          </Text>
          <Text size="xs" c="dimmed">
            {visit.address}, {visit.number} {visit.complement} - {visit.neighborhood} - {visit.city}/{visit.state}
          </Text>
          <Text size="sm">
            <b>Técnico:</b> {visit.technicianName ?? 'Não confirmado'}
            {visit.technicianPhone ? ` · ${visit.technicianPhone}` : ''} · <b>Agendado por:</b> {visit.responsibleName}
          </Text>
          {visit.checkinDate && (
            <Text size="sm">
              <b>Check-in:</b> {fmtDateTime(visit.checkinDate)}
              {visit.orderId > 0 && orderLink && (
                <>
                  {' '}
                  ·{' '}
                  <Anchor component={Link} to={orderLink}>
                    OS #{visit.orderSequence}
                  </Anchor>
                </>
              )}
            </Text>
          )}
          {visit.description && (
            <Text size="sm" c="dimmed" style={{ whiteSpace: 'pre-wrap' }}>
              {visit.description}
            </Text>
          )}
        </Stack>
        {actions}
      </Group>
    </Paper>
  )
}
