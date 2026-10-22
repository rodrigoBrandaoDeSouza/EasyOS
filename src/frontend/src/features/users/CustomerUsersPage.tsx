import { useState } from 'react'
import { ActionIcon, Badge, Button, Grid, Group, Menu, Modal, Paper, PasswordInput, Select, Table, Text, TextInput } from '@mantine/core'
import { useForm } from '@mantine/form'
import { useDebouncedValue } from '@mantine/hooks'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { IconDots, IconPencil, IconPlus, IconPower, IconSearch } from '@tabler/icons-react'
import { api } from '../../api/client'
import type { CustomerUser } from '../../api/types'
import { useCustomerId } from '../../app/guards'
import { useMe } from '../../auth/AuthContext'
import { ActiveBadge, confirmAction, Empty, Loading, PageHeader } from '../../components/common'
import { CUSTOMER_PROFILES, profileLabel } from '../../lib/format'
import { notifyError, notifySuccess } from '../../lib/notify'

export function CustomerUsersPage() {
  const customerId = useCustomerId()
  const me = useMe()
  const [search, setSearch] = useState('')
  const [debounced] = useDebouncedValue(search, 300)
  const [editing, setEditing] = useState<CustomerUser | 'new' | null>(null)
  const queryClient = useQueryClient()
  const base = `/api/customers/${customerId}/users`

  const users = useQuery({
    queryKey: ['customer-users', customerId, debounced],
    queryFn: () => api.get<CustomerUser[]>(base, { q: debounced }),
  })

  const toggle = useMutation({
    mutationFn: (u: CustomerUser) => api.post(`${base}/${u.id}/status`, { active: !u.active }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['customer-users', customerId] })
      notifySuccess('Status do usuário atualizado')
    },
    onError: notifyError,
  })

  return (
    <>
      <PageHeader
        title="Usuários"
        actions={
          <Button leftSection={<IconPlus size={16} />} onClick={() => setEditing('new')}>
            Novo usuário
          </Button>
        }
      />
      <Paper p="md">
        <TextInput
          placeholder="Buscar por nome"
          leftSection={<IconSearch size={16} />}
          value={search}
          onChange={(e) => setSearch(e.currentTarget.value)}
          mb="md"
          maw={420}
        />
        {users.isPending ? (
          <Loading />
        ) : !users.data?.length ? (
          <Empty>Nenhum usuário encontrado.</Empty>
        ) : (
          <Table.ScrollContainer minWidth={600}>
            <Table>
              <Table.Thead>
                <Table.Tr>
                  <Table.Th>Nome</Table.Th>
                  <Table.Th>E-mail</Table.Th>
                  <Table.Th>Departamento</Table.Th>
                  <Table.Th>Perfil</Table.Th>
                  <Table.Th>Status</Table.Th>
                  <Table.Th w={50} />
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {users.data.map((u) => {
                  const self = me.type === 'customer' && me.id === u.id
                  return (
                    <Table.Tr key={u.id} style={{ opacity: u.active ? 1 : 0.6 }}>
                      <Table.Td>
                        <Text size="sm" fw={500}>
                          {u.name}
                        </Text>
                      </Table.Td>
                      <Table.Td>{u.email}</Table.Td>
                      <Table.Td>{u.department}</Table.Td>
                      <Table.Td>
                        <Badge variant="light">{profileLabel('customer', u.profile)}</Badge>
                      </Table.Td>
                      <Table.Td>
                        <ActiveBadge active={u.active} />
                      </Table.Td>
                      <Table.Td>
                        <Menu position="bottom-end">
                          <Menu.Target>
                            <ActionIcon variant="subtle" color="gray" aria-label="Ações">
                              <IconDots size={18} />
                            </ActionIcon>
                          </Menu.Target>
                          <Menu.Dropdown>
                            <Menu.Item leftSection={<IconPencil size={16} />} disabled={!u.active} onClick={() => setEditing(u)}>
                              Editar
                            </Menu.Item>
                            <Menu.Item
                              leftSection={<IconPower size={16} />}
                              color={u.active ? 'red' : 'teal'}
                              disabled={self}
                              onClick={() =>
                                confirmAction(u.active ? 'Confirma a inativação deste usuário?' : 'Confirma a ativação deste usuário?', () =>
                                  toggle.mutate(u),
                                )
                              }
                            >
                              {u.active ? 'Desativar' : 'Ativar'}
                            </Menu.Item>
                          </Menu.Dropdown>
                        </Menu>
                      </Table.Td>
                    </Table.Tr>
                  )
                })}
              </Table.Tbody>
            </Table>
          </Table.ScrollContainer>
        )}
      </Paper>
      {editing && <CustomerUserModal base={base} customerId={customerId} user={editing === 'new' ? null : editing} onClose={() => setEditing(null)} />}
    </>
  )
}

function CustomerUserModal({ base, customerId, user, onClose }: { base: string; customerId: number; user: CustomerUser | null; onClose: () => void }) {
  const isNew = user === null
  const queryClient = useQueryClient()
  const form = useForm({
    initialValues: {
      name: user?.name ?? '',
      email: user?.email ?? '',
      department: user?.department ?? '',
      profile: String(user?.profile ?? 2),
      password: '',
      confirm: '',
    },
    validate: {
      name: (v) => (v.trim() ? null : 'Informe o nome'),
      email: (v) => (/^\S+@\S+\.\S+$/.test(v) ? null : 'E-mail inválido'),
      password: (v) => (isNew && v.length < 6 ? 'A senha deve ter ao menos 6 caracteres' : !isNew && v && v.length < 6 ? 'Mínimo de 6 caracteres' : null),
      confirm: (v, values) => (v === values.password ? null : 'As senhas não conferem'),
    },
  })

  const save = useMutation({
    mutationFn: (v: typeof form.values) => {
      const body = { name: v.name, email: v.email, department: v.department, profile: Number(v.profile), password: v.password || null }
      return isNew ? api.post(base, body) : api.put(`${base}/${user!.id}`, body)
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['customer-users', customerId] })
      notifySuccess(isNew ? 'Usuário cadastrado com sucesso' : 'Usuário editado com sucesso')
      onClose()
    },
    onError: notifyError,
  })

  return (
    <Modal opened onClose={onClose} title={isNew ? 'Novo usuário' : `Editar ${user!.name}`}>
      <form onSubmit={form.onSubmit((v) => save.mutate(v))}>
        <Grid gap="sm">
          <Grid.Col span={12}>
            <TextInput label="Nome" withAsterisk {...form.getInputProps('name')} />
          </Grid.Col>
          <Grid.Col span={12}>
            <TextInput label="E-mail" type="email" withAsterisk {...form.getInputProps('email')} />
          </Grid.Col>
          <Grid.Col span={{ base: 12, sm: 6 }}>
            <TextInput label="Departamento" {...form.getInputProps('department')} />
          </Grid.Col>
          <Grid.Col span={{ base: 12, sm: 6 }}>
            <Select label="Perfil" data={CUSTOMER_PROFILES} allowDeselect={false} {...form.getInputProps('profile')} />
          </Grid.Col>
          <Grid.Col span={{ base: 12, sm: 6 }}>
            <PasswordInput
              label={isNew ? 'Senha' : 'Nova senha'}
              description={isNew ? undefined : 'Deixe em branco para manter'}
              withAsterisk={isNew}
              autoComplete="new-password"
              {...form.getInputProps('password')}
            />
          </Grid.Col>
          <Grid.Col span={{ base: 12, sm: 6 }}>
            <PasswordInput label="Confirmar senha" autoComplete="new-password" mt={isNew ? 0 : 22} {...form.getInputProps('confirm')} />
          </Grid.Col>
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
