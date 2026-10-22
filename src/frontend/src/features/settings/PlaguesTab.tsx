import { useState } from 'react'
import { ActionIcon, Button, Group, Modal, Paper, Stack, Switch, Table, TextInput } from '@mantine/core'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { IconPencil, IconPlus } from '@tabler/icons-react'
import { api } from '../../api/client'
import type { Plague } from '../../api/types'
import { Loading } from '../../components/common'
import { notifyError, notifySuccess } from '../../lib/notify'

/** Pragas do banco: cadastro global, ativação por empresa (como no legado). */
export function PlaguesTab() {
  const queryClient = useQueryClient()
  const plagues = useQuery({ queryKey: ['plagues', 'all'], queryFn: () => api.get<Plague[]>('/api/plagues') })
  const [editing, setEditing] = useState<Plague | 'new' | null>(null)

  const toggle = useMutation({
    mutationFn: (p: Plague) => api.post(`/api/plagues/${p.id}/status`, { active: !p.active }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['plagues'] }),
    onError: notifyError,
  })

  if (plagues.isPending) return <Loading />

  return (
    <Paper p="md">
      <Group justify="flex-end" mb="sm">
        <Button leftSection={<IconPlus size={16} />} onClick={() => setEditing('new')}>
          Nova praga
        </Button>
      </Group>
      <Table.ScrollContainer minWidth={480}>
        <Table>
          <Table.Thead>
            <Table.Tr>
              <Table.Th w={70}>Cód.</Table.Th>
              <Table.Th>Nome</Table.Th>
              <Table.Th>Tipo</Table.Th>
              <Table.Th w={100}>Ativa</Table.Th>
              <Table.Th w={50} />
            </Table.Tr>
          </Table.Thead>
          <Table.Tbody>
            {plagues.data?.map((p) => (
              <Table.Tr key={p.id}>
                <Table.Td>{p.id}</Table.Td>
                <Table.Td>{p.name}</Table.Td>
                <Table.Td>{p.plagueType}</Table.Td>
                <Table.Td>
                  <Switch checked={p.active} onChange={() => toggle.mutate(p)} aria-label={`Ativar ${p.name}`} />
                </Table.Td>
                <Table.Td>
                  <ActionIcon variant="subtle" onClick={() => setEditing(p)} aria-label={`Editar ${p.name}`}>
                    <IconPencil size={16} />
                  </ActionIcon>
                </Table.Td>
              </Table.Tr>
            ))}
          </Table.Tbody>
        </Table>
      </Table.ScrollContainer>
      {editing && <PlagueModal plague={editing === 'new' ? null : editing} onClose={() => setEditing(null)} />}
    </Paper>
  )
}

function PlagueModal({ plague, onClose }: { plague: Plague | null; onClose: () => void }) {
  const queryClient = useQueryClient()
  const [name, setName] = useState(plague?.name ?? '')
  const [type, setType] = useState(plague?.plagueType ?? 'Inseto')
  const save = useMutation({
    mutationFn: () => (plague ? api.put(`/api/plagues/${plague.id}`, { name, plagueType: type }) : api.post('/api/plagues', { name, plagueType: type })),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['plagues'] })
      notifySuccess(plague ? 'Praga editada com sucesso' : 'Praga cadastrada com sucesso')
      onClose()
    },
    onError: notifyError,
  })
  return (
    <Modal opened onClose={onClose} title={plague ? 'Editar praga' : 'Nova praga'} centered>
      <Stack>
        <TextInput label="Nome" value={name} onChange={(e) => setName(e.currentTarget.value)} withAsterisk />
        <TextInput
          label="Tipo"
          description='Use "Roedor" para pragas contadas no relatório de roedores'
          value={type}
          onChange={(e) => setType(e.currentTarget.value)}
          withAsterisk
        />
        <Group justify="flex-end">
          <Button variant="default" onClick={onClose}>
            Cancelar
          </Button>
          <Button onClick={() => save.mutate()} loading={save.isPending} disabled={!name.trim() || !type.trim()}>
            Salvar
          </Button>
        </Group>
      </Stack>
    </Modal>
  )
}
