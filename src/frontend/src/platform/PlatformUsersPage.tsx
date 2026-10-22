import { useState } from 'react'
import { Badge, Button, Group, Modal, Paper, PasswordInput, Stack, Switch, Table, TextInput, Title } from '@mantine/core'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { IconPlus } from '@tabler/icons-react'
import { api } from '../api/client'
import { Loading } from '../components/common'
import { notifyError, notifySuccess } from '../lib/notify'

interface PlatformUser {
  id: string
  name: string
  email: string
  active: boolean
  createdAt: string
}

export function PlatformUsersPage() {
  const users = useQuery({ queryKey: ['platform-users'], queryFn: () => api.get<PlatformUser[]>('/api/platform/users') })
  const [editing, setEditing] = useState<PlatformUser | 'new' | null>(null)

  return (
    <>
      <Group justify="space-between" mb="md">
        <Title order={2}>Usuários do painel</Title>
        <Button leftSection={<IconPlus size={16} />} onClick={() => setEditing('new')}>
          Novo usuário
        </Button>
      </Group>
      <Paper p="md">
        {users.isPending ? (
          <Loading />
        ) : (
          <Table>
            <Table.Thead>
              <Table.Tr>
                <Table.Th>Nome</Table.Th>
                <Table.Th>E-mail</Table.Th>
                <Table.Th>Status</Table.Th>
              </Table.Tr>
            </Table.Thead>
            <Table.Tbody>
              {users.data?.map((u) => (
                <Table.Tr key={u.id} style={{ cursor: 'pointer' }} onClick={() => setEditing(u)}>
                  <Table.Td>{u.name}</Table.Td>
                  <Table.Td>{u.email}</Table.Td>
                  <Table.Td>
                    <Badge color={u.active ? 'teal' : 'gray'}>{u.active ? 'Ativo' : 'Inativo'}</Badge>
                  </Table.Td>
                </Table.Tr>
              ))}
            </Table.Tbody>
          </Table>
        )}
      </Paper>
      {editing && <PlatformUserModal user={editing === 'new' ? null : editing} onClose={() => setEditing(null)} />}
    </>
  )
}

function PlatformUserModal({ user, onClose }: { user: PlatformUser | null; onClose: () => void }) {
  const queryClient = useQueryClient()
  const [name, setName] = useState(user?.name ?? '')
  const [email, setEmail] = useState(user?.email ?? '')
  const [active, setActive] = useState(user?.active ?? true)
  const [password, setPassword] = useState('')
  const save = useMutation({
    mutationFn: () =>
      user ? api.put(`/api/platform/users/${user.id}`, { name, active, password: password || null }) : api.post('/api/platform/users', { name, email, password }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['platform-users'] })
      notifySuccess('Usuário salvo')
      onClose()
    },
    onError: notifyError,
  })
  return (
    <Modal opened onClose={onClose} title={user ? 'Editar usuário' : 'Novo usuário'} centered>
      <Stack>
        <TextInput label="Nome" value={name} onChange={(e) => setName(e.currentTarget.value)} />
        <TextInput label="E-mail" value={email} disabled={!!user} onChange={(e) => setEmail(e.currentTarget.value)} />
        <PasswordInput
          label={user ? 'Nova senha' : 'Senha'}
          description="Mínimo de 10 caracteres"
          value={password}
          onChange={(e) => setPassword(e.currentTarget.value)}
        />
        {user && <Switch label="Ativo" checked={active} onChange={(e) => setActive(e.currentTarget.checked)} />}
        <Group justify="flex-end">
          <Button variant="default" onClick={onClose}>
            Cancelar
          </Button>
          <Button onClick={() => save.mutate()} loading={save.isPending}>
            Salvar
          </Button>
        </Group>
      </Stack>
    </Modal>
  )
}
