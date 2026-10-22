import { useState } from 'react'
import { ActionIcon, Badge, Button, FileInput, Grid, Group, Image, Menu, Modal, Paper, PasswordInput, Select, Stack, Table, Text, TextInput } from '@mantine/core'
import { useForm } from '@mantine/form'
import { useDebouncedValue } from '@mantine/hooks'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { IconDots, IconPencil, IconPlus, IconPower, IconSearch, IconSignature } from '@tabler/icons-react'
import { api } from '../../api/client'
import type { CompanyUser } from '../../api/types'
import { ActiveBadge, confirmAction, Empty, Loading, PageHeader } from '../../components/common'
import { useMe } from '../../auth/AuthContext'
import { COMPANY_PROFILES, profileLabel } from '../../lib/format'
import { notifyError, notifySuccess } from '../../lib/notify'
import { readAsDataUrl } from '../../lib/files'

export function CompanyUsersPage() {
  const [search, setSearch] = useState('')
  const [debounced] = useDebouncedValue(search, 300)
  const [editing, setEditing] = useState<CompanyUser | 'new' | null>(null)
  const queryClient = useQueryClient()
  const me = useMe()

  const users = useQuery({
    queryKey: ['company-users', debounced],
    queryFn: () => api.get<CompanyUser[]>('/api/company-users', { q: debounced }),
  })

  const toggle = useMutation({
    mutationFn: (u: CompanyUser) => api.post(`/api/company-users/${u.id}/status`, { active: !u.active }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['company-users'] })
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
          <Table.ScrollContainer minWidth={700}>
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
                {users.data.map((u) => (
                  <Table.Tr key={u.id} style={{ opacity: u.active ? 1 : 0.6 }}>
                    <Table.Td>
                      <Group gap={6}>
                        <Text size="sm" fw={500}>
                          {u.name}
                        </Text>
                        {u.hasSignature && <IconSignature size={16} title="Possui assinatura" />}
                      </Group>
                      <Text size="xs" c="dimmed">
                        {u.phone}
                      </Text>
                    </Table.Td>
                    <Table.Td>{u.email}</Table.Td>
                    <Table.Td>{u.department}</Table.Td>
                    <Table.Td>
                      <Badge variant="light" color={u.profile === 3 ? 'grape' : 'brand'}>
                        {profileLabel('company', u.profile)}
                      </Badge>
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
                            disabled={u.id === me.id}
                            onClick={() =>
                              confirmAction(u.active ? 'Confirma a inativação deste usuário?' : 'Confirma a ativação deste usuário?', () => toggle.mutate(u))
                            }
                          >
                            {u.active ? 'Desativar' : 'Ativar'}
                          </Menu.Item>
                        </Menu.Dropdown>
                      </Menu>
                    </Table.Td>
                  </Table.Tr>
                ))}
              </Table.Tbody>
            </Table>
          </Table.ScrollContainer>
        )}
      </Paper>
      {editing && <CompanyUserModal user={editing === 'new' ? null : editing} onClose={() => setEditing(null)} />}
    </>
  )
}

function CompanyUserModal({ user, onClose }: { user: CompanyUser | null; onClose: () => void }) {
  const isNew = user === null
  const queryClient = useQueryClient()
  // undefined = manter a assinatura atual; '' = remover; data URL = nova assinatura.
  const [signature, setSignature] = useState<string | undefined>(undefined)

  const current = useQuery({
    queryKey: ['company-user-signature', user?.id],
    queryFn: () => api.get<{ signature: string } | undefined>(`/api/company-users/${user!.id}/signature`),
    enabled: !!user?.hasSignature,
  })

  const form = useForm({
    initialValues: {
      name: user?.name ?? '',
      email: user?.email ?? '',
      phone: user?.phone ?? '',
      department: user?.department ?? '',
      profile: String(user?.profile ?? 2),
      password: '',
      confirm: '',
    },
    validate: {
      name: (v) => (v.trim() ? null : 'Informe o nome'),
      email: (v) => (/^\S+@\S+\.\S+$/.test(v) ? null : 'E-mail inválido'),
      department: (v) => (v.trim() ? null : 'Informe o departamento'),
      password: (v) => (isNew && v.length < 6 ? 'A senha deve ter ao menos 6 caracteres' : !isNew && v && v.length < 6 ? 'Mínimo de 6 caracteres' : null),
      confirm: (v, values) => (v === values.password ? null : 'As senhas não conferem'),
    },
  })

  const save = useMutation({
    mutationFn: (v: typeof form.values) => {
      const body = { name: v.name, email: v.email, phone: v.phone, department: v.department, profile: Number(v.profile), password: v.password || null, signature: signature ?? null }
      return isNew ? api.post('/api/company-users', body) : api.put(`/api/company-users/${user!.id}`, body)
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['company-users'] })
      queryClient.invalidateQueries({ queryKey: ['company-user-signature'] })
      notifySuccess(isNew ? 'Usuário cadastrado com sucesso' : 'Usuário editado com sucesso')
      onClose()
    },
    onError: notifyError,
  })

  const preview = signature === undefined ? current.data?.signature : signature || null

  return (
    <Modal opened onClose={onClose} title={isNew ? 'Novo usuário' : `Editar ${user!.name}`} size="lg">
      <form onSubmit={form.onSubmit((v) => save.mutate(v))}>
        <Grid gap="sm">
          <Grid.Col span={12}>
            <TextInput label="Nome" withAsterisk {...form.getInputProps('name')} />
          </Grid.Col>
          <Grid.Col span={{ base: 12, sm: 7 }}>
            <TextInput label="E-mail" type="email" withAsterisk {...form.getInputProps('email')} />
          </Grid.Col>
          <Grid.Col span={{ base: 12, sm: 5 }}>
            <TextInput label="Telefone" {...form.getInputProps('phone')} />
          </Grid.Col>
          <Grid.Col span={{ base: 12, sm: 7 }}>
            <TextInput label="Departamento" withAsterisk {...form.getInputProps('department')} />
          </Grid.Col>
          <Grid.Col span={{ base: 12, sm: 5 }}>
            <Select label="Perfil" data={COMPANY_PROFILES} allowDeselect={false} {...form.getInputProps('profile')} />
          </Grid.Col>
          <Grid.Col span={{ base: 12, sm: 6 }}>
            <PasswordInput
              label={isNew ? 'Senha' : 'Nova senha'}
              description={isNew ? undefined : 'Deixe em branco para manter a atual'}
              withAsterisk={isNew}
              autoComplete="new-password"
              {...form.getInputProps('password')}
            />
          </Grid.Col>
          <Grid.Col span={{ base: 12, sm: 6 }}>
            <PasswordInput label="Confirmar senha" autoComplete="new-password" mt={isNew ? 0 : 22} {...form.getInputProps('confirm')} />
          </Grid.Col>
          <Grid.Col span={12}>
            <Stack gap={6}>
              <FileInput
                label="Assinatura (imagem)"
                description="Usada na OS e nos documentos emitidos pelo técnico"
                accept="image/png,image/jpeg"
                placeholder="Selecionar imagem"
                clearable
                onChange={async (file) => {
                  if (!file) return setSignature(undefined)
                  if (file.size > 1024 * 1024) return notifyError(new Error('A imagem deve ter até 1 MB.'))
                  setSignature(await readAsDataUrl(file))
                }}
              />
              {preview && (
                <Group>
                  <Image src={preview} alt="Assinatura" h={70} w="auto" fit="contain" bg="gray.0" p={4} />
                  <Button variant="subtle" color="red" size="xs" onClick={() => setSignature('')}>
                    Remover assinatura
                  </Button>
                </Group>
              )}
            </Stack>
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
