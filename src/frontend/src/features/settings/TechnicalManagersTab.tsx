import { useEffect, useState } from 'react'
import { ActionIcon, Button, FileButton, Group, Image, Paper, Table, Text, TextInput, Tooltip } from '@mantine/core'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { IconTrash, IconUpload } from '@tabler/icons-react'
import { api } from '../../api/client'
import type { TechnicalManager } from '../../api/types'
import { confirmAction, Loading } from '../../components/common'
import { UFS } from '../../lib/format'
import { notifyError, notifySuccess } from '../../lib/notify'

type Row = { state: string; name: string; register: string }

/**
 * Responsável técnico por UF (o do estado do cliente aparece no certificado), com a imagem da assinatura —
 * que no sistema antigo era um arquivo copiado manualmente no servidor de cada cliente.
 */
export function TechnicalManagersTab() {
  const queryClient = useQueryClient()
  const managers = useQuery({ queryKey: ['technical-managers'], queryFn: () => api.get<TechnicalManager[]>('/api/company/technical-managers') })
  const [rows, setRows] = useState<Row[]>([])
  const [version, setVersion] = useState(0)

  useEffect(() => {
    if (!managers.data) return
    setRows(UFS.map(([uf]) => {
      const m = managers.data.find((x) => x.state.trim().toUpperCase() === uf)
      return { state: uf, name: m?.name ?? '', register: m?.register ?? '' }
    }))
  }, [managers.data])

  const save = useMutation({
    mutationFn: () => api.put('/api/company/technical-managers', rows),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['technical-managers'] })
      notifySuccess('Responsáveis técnicos salvos')
    },
    onError: notifyError,
  })

  const uploadSignature = async (state: string, file: File | null) => {
    if (!file) return
    const form = new FormData()
    form.append('file', file)
    try {
      await api.upload(`/api/company/technical-managers/${state}/signature`, form, 'PUT')
      setVersion((v) => v + 1)
      notifySuccess(`Assinatura de ${state} enviada`)
    } catch (e) {
      notifyError(e)
    }
  }

  const removeSignature = async (state: string) => {
    try {
      await api.delete(`/api/company/technical-managers/${state}/signature`)
      setVersion((v) => v + 1)
    } catch (e) {
      notifyError(e)
    }
  }

  if (managers.isPending) return <Loading />
  const update = (i: number, patch: Partial<Row>) => setRows((r) => r.map((x, idx) => (idx === i ? { ...x, ...patch } : x)))

  return (
    <Paper p="md">
      <Text size="sm" c="dimmed" mb="sm">
        O responsável técnico do estado do cliente é impresso no certificado. Envie a assinatura em PNG (fundo transparente) ou JPG.
      </Text>
      <Table.ScrollContainer minWidth={760}>
        <Table>
          <Table.Thead>
            <Table.Tr>
              <Table.Th w={60}>UF</Table.Th>
              <Table.Th>Nome</Table.Th>
              <Table.Th w={200}>Registro (CRQ/CREA...)</Table.Th>
              <Table.Th w={230}>Assinatura</Table.Th>
            </Table.Tr>
          </Table.Thead>
          <Table.Tbody>
            {rows.map((r, i) => (
              <Table.Tr key={r.state}>
                <Table.Td fw={600}>{r.state}</Table.Td>
                <Table.Td>
                  <TextInput size="xs" value={r.name} onChange={(e) => update(i, { name: e.currentTarget.value })} aria-label={`Responsável ${r.state}`} />
                </Table.Td>
                <Table.Td>
                  <TextInput size="xs" value={r.register} onChange={(e) => update(i, { register: e.currentTarget.value })} aria-label={`Registro ${r.state}`} />
                </Table.Td>
                <Table.Td>
                  <Group gap={6} wrap="nowrap">
                    <Image
                      src={`/api/company/technical-managers/${r.state}/signature?v=${version}`}
                      h={32}
                      w={110}
                      fit="contain"
                      fallbackSrc="data:image/gif;base64,R0lGODlhAQABAAAAACw="
                      alt=""
                    />
                    <FileButton accept="image/png,image/jpeg" onChange={(f) => uploadSignature(r.state, f)}>
                      {(props) => (
                        <Tooltip label="Enviar assinatura">
                          <ActionIcon {...props} variant="subtle" aria-label={`Enviar assinatura ${r.state}`}>
                            <IconUpload size={16} />
                          </ActionIcon>
                        </Tooltip>
                      )}
                    </FileButton>
                    <Tooltip label="Remover assinatura">
                      <ActionIcon
                        variant="subtle"
                        color="red"
                        onClick={() => confirmAction(`Remover a assinatura de ${r.state}?`, () => removeSignature(r.state), 'Remover', true)}
                        aria-label={`Remover assinatura ${r.state}`}
                      >
                        <IconTrash size={16} />
                      </ActionIcon>
                    </Tooltip>
                  </Group>
                </Table.Td>
              </Table.Tr>
            ))}
          </Table.Tbody>
        </Table>
      </Table.ScrollContainer>
      <Group justify="flex-end" mt="md">
        <Button onClick={() => save.mutate()} loading={save.isPending}>
          Salvar responsáveis
        </Button>
      </Group>
    </Paper>
  )
}
