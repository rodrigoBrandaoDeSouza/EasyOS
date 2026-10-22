import { useState } from 'react'
import { Button, Group, Paper, Select, Stack, Table, Text, Textarea } from '@mantine/core'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import dayjs from 'dayjs'
import { api } from '../../api/client'
import type { MonthlyComments } from '../../api/types'
import { useCustomerId } from '../../app/guards'
import { useMe } from '../../auth/AuthContext'
import { Loading, PageHeader } from '../../components/common'
import { fmtDate } from '../../lib/format'
import { notifyError, notifySuccess } from '../../lib/notify'

const MONTHS = ['Janeiro', 'Fevereiro', 'Março', 'Abril', 'Maio', 'Junho', 'Julho', 'Agosto', 'Setembro', 'Outubro', 'Novembro', 'Dezembro']

/** Comentário mensal da empresa para o cliente: um por mês encerrado, sem edição posterior. */
export function MonthlyCommentsPage() {
  const customerId = useCustomerId()
  const me = useMe()
  const now = dayjs()
  const [year, setYear] = useState(now.year())
  const [drafts, setDrafts] = useState<Record<number, string>>({})
  const queryClient = useQueryClient()

  const comments = useQuery({
    queryKey: ['monthly-comments', customerId, year],
    queryFn: () => api.get<MonthlyComments>(`/api/customers/${customerId}/monthly-comments`, { year }),
  })

  const save = useMutation({
    mutationFn: ({ month, comment }: { month: number; comment: string }) =>
      api.post(`/api/customers/${customerId}/monthly-comments`, { year, month, comment }),
    onSuccess: (_, v) => {
      queryClient.invalidateQueries({ queryKey: ['monthly-comments', customerId] })
      setDrafts((d) => ({ ...d, [v.month]: '' }))
      notifySuccess('Comentário salvo')
    },
    onError: notifyError,
  })

  const years = Array.from({ length: now.year() - 2015 + 1 }, (_, i) => String(now.year() - i))
  const canWrite = me.type === 'company'

  return (
    <>
      <PageHeader
        title="Comentários Mensais"
        actions={<Select data={years} value={String(year)} onChange={(v) => v && setYear(Number(v))} w={110} aria-label="Ano" />}
      />
      {comments.isPending ? (
        <Loading />
      ) : (
        <Paper p="md">
          <Table.ScrollContainer minWidth={600}>
            <Table>
              <Table.Thead>
                <Table.Tr>
                  <Table.Th w={120}>Mês</Table.Th>
                  <Table.Th>Comentário</Table.Th>
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {comments.data!.months.map((m) => {
                  const closed = year < now.year() || (year === now.year() && m.month < now.month() + 1)
                  return (
                    <Table.Tr key={m.month}>
                      <Table.Td fw={600}>{MONTHS[m.month - 1]}</Table.Td>
                      <Table.Td>
                        {m.comment ? (
                          <Stack gap={2}>
                            <Text size="sm" style={{ whiteSpace: 'pre-wrap' }}>
                              {m.comment}
                            </Text>
                            <Text size="xs" c="dimmed">
                              {m.author} · {fmtDate(m.createdAt)}
                            </Text>
                          </Stack>
                        ) : canWrite && closed ? (
                          <Group align="flex-end" wrap="nowrap">
                            <Textarea
                              autosize
                              minRows={1}
                              placeholder="Escreva o comentário do mês"
                              value={drafts[m.month] ?? ''}
                              onChange={(e) => setDrafts((d) => ({ ...d, [m.month]: e.currentTarget.value }))}
                              style={{ flex: 1 }}
                            />
                            <Button
                              size="xs"
                              disabled={!drafts[m.month]?.trim()}
                              loading={save.isPending && save.variables?.month === m.month}
                              onClick={() => save.mutate({ month: m.month, comment: drafts[m.month] })}
                            >
                              Salvar
                            </Button>
                          </Group>
                        ) : (
                          <Text size="sm" c="dimmed">
                            —
                          </Text>
                        )}
                      </Table.Td>
                    </Table.Tr>
                  )
                })}
              </Table.Tbody>
            </Table>
          </Table.ScrollContainer>
          {canWrite && (
            <Text size="xs" c="dimmed" mt="xs">
              Só é possível comentar meses já encerrados. Após salvo, o comentário não pode ser alterado.
            </Text>
          )}
        </Paper>
      )}
    </>
  )
}
