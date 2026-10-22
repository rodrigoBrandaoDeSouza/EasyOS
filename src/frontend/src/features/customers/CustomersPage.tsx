import { useState } from 'react'
import { ActionIcon, Alert, Badge, Button, Group, Menu, Modal, Pagination, Paper, Stack, Table, Text, TextInput, Tooltip } from '@mantine/core'
import { useForm } from '@mantine/form'
import { useDebouncedValue } from '@mantine/hooks'
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { IconDots, IconEye, IconMapPinOff, IconPencil, IconPlus, IconPower, IconSearch } from '@tabler/icons-react'
import { useNavigate } from 'react-router'
import { api } from '../../api/client'
import type { Company, Customer, Paged } from '../../api/types'
import { ActiveBadge, confirmAction, Empty, Loading, PageHeader } from '../../components/common'
import { notifyError, notifySuccess } from '../../lib/notify'
import { CustomerFields, customerInitialValues, customerValidation, toCustomerRequest, type CustomerFormValues } from './CustomerForm'

export function useCompany() {
  return useQuery({ queryKey: ['company'], queryFn: () => api.get<Company>('/api/company'), staleTime: 5 * 60_000 })
}

export function CustomersPage() {
  const [search, setSearch] = useState('')
  const [debounced] = useDebouncedValue(search, 350)
  const [page, setPage] = useState(1)
  const [editing, setEditing] = useState<Customer | 'new' | null>(null)
  const navigate = useNavigate()
  const queryClient = useQueryClient()

  const customers = useQuery({
    queryKey: ['customers', debounced, page],
    queryFn: () => api.get<Paged<Customer>>('/api/customers', { q: debounced, page, pageSize: 15 }),
    placeholderData: keepPreviousData,
  })

  const toggle = useMutation({
    mutationFn: (c: Customer) => api.post(`/api/customers/${c.id}/status`, { active: !c.active }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['customers'] })
      notifySuccess('Status do cliente atualizado')
    },
    onError: notifyError,
  })

  return (
    <>
      <PageHeader
        title="Clientes"
        actions={
          <Button leftSection={<IconPlus size={16} />} onClick={() => setEditing('new')}>
            Novo cliente
          </Button>
        }
      />
      <Paper p="md">
        <TextInput
          placeholder="Buscar por nome fantasia"
          leftSection={<IconSearch size={16} />}
          value={search}
          onChange={(e) => {
            setSearch(e.currentTarget.value)
            setPage(1)
          }}
          mb="md"
          maw={420}
        />
        {customers.isPending ? (
          <Loading />
        ) : !customers.data?.items.length ? (
          <Empty>Nenhum cliente encontrado.</Empty>
        ) : (
          <Table.ScrollContainer minWidth={760}>
            <Table>
              <Table.Thead>
                <Table.Tr>
                  <Table.Th w={70}>Cód.</Table.Th>
                  <Table.Th>Cliente</Table.Th>
                  <Table.Th>CNPJ/CPF</Table.Th>
                  <Table.Th>Cidade</Table.Th>
                  <Table.Th>Contato</Table.Th>
                  <Table.Th w={90}>Status</Table.Th>
                  <Table.Th w={110} />
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {customers.data.items.map((c) => (
                  <Table.Tr key={c.id} style={{ opacity: c.active ? 1 : 0.6 }}>
                    <Table.Td>{c.id}</Table.Td>
                    <Table.Td>
                      <Group gap={6} wrap="nowrap">
                        <Stack gap={0}>
                          <Text fw={600} size="sm">
                            {c.tradingName}
                          </Text>
                          <Text size="xs" c="dimmed">
                            {c.companyName}
                          </Text>
                        </Stack>
                        {c.missingGeolocation && (
                          <Tooltip label="Endereço não localizado no mapa. Verifique o endereço do cliente.">
                            <IconMapPinOff size={16} color="var(--mantine-color-red-6)" />
                          </Tooltip>
                        )}
                        {c.clientHasContract && (
                          <Badge size="xs" variant="light">
                            Contrato
                          </Badge>
                        )}
                      </Group>
                    </Table.Td>
                    <Table.Td>{c.cpfCnpj}</Table.Td>
                    <Table.Td>
                      {c.city}/{c.state}
                    </Table.Td>
                    <Table.Td>
                      <Text size="sm">{c.contactName}</Text>
                      <Text size="xs" c="dimmed">
                        {c.phone}
                      </Text>
                    </Table.Td>
                    <Table.Td>
                      <ActiveBadge active={c.active} />
                    </Table.Td>
                    <Table.Td>
                      <Group gap={4} justify="flex-end" wrap="nowrap">
                        <Tooltip label="Acessar como cliente">
                          <ActionIcon variant="subtle" onClick={() => navigate(`/c/${c.id}/painel`)} aria-label="Acessar como cliente">
                            <IconEye size={18} />
                          </ActionIcon>
                        </Tooltip>
                        <Menu position="bottom-end">
                          <Menu.Target>
                            <ActionIcon variant="subtle" color="gray" aria-label="Mais ações">
                              <IconDots size={18} />
                            </ActionIcon>
                          </Menu.Target>
                          <Menu.Dropdown>
                            <Menu.Item leftSection={<IconPencil size={16} />} disabled={!c.active} onClick={() => setEditing(c)}>
                              Editar
                            </Menu.Item>
                            <Menu.Item
                              leftSection={<IconPower size={16} />}
                              color={c.active ? 'red' : 'teal'}
                              onClick={() =>
                                confirmAction(
                                  c.active ? 'Confirma a inativação deste cliente?' : 'Confirma a ativação deste cliente?',
                                  () => toggle.mutate(c),
                                )
                              }
                            >
                              {c.active ? 'Desativar' : 'Ativar'}
                            </Menu.Item>
                          </Menu.Dropdown>
                        </Menu>
                      </Group>
                    </Table.Td>
                  </Table.Tr>
                ))}
              </Table.Tbody>
            </Table>
          </Table.ScrollContainer>
        )}
        {(customers.data?.totalPages ?? 0) > 1 && (
          <Group justify="center" mt="md">
            <Pagination total={customers.data!.totalPages} value={page} onChange={setPage} />
          </Group>
        )}
      </Paper>

      {editing && <CustomerModal customer={editing === 'new' ? null : editing} onClose={() => setEditing(null)} />}
    </>
  )
}

function CustomerModal({ customer, onClose }: { customer: Customer | null; onClose: () => void }) {
  const isNew = customer === null
  const company = useCompany()
  const queryClient = useQueryClient()
  const form = useForm<CustomerFormValues>({
    initialValues: customerInitialValues(customer),
    validate: customerValidation(isNew, company.data?.state === 'RJ'),
  })

  const save = useMutation({
    mutationFn: (v: CustomerFormValues) =>
      isNew
        ? api.post('/api/customers', {
            customer: toCustomerRequest(v),
            user: { name: v.userName, email: v.userEmail, department: v.userDepartment, profile: Number(v.userProfile), password: v.userPassword },
          })
        : api.put(`/api/customers/${customer!.id}`, toCustomerRequest(v)),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['customers'] })
      queryClient.invalidateQueries({ queryKey: ['customer'] })
      notifySuccess(isNew ? 'Cliente cadastrado com sucesso' : 'Cliente editado com sucesso')
      onClose()
    },
    onError: notifyError,
  })

  return (
    <Modal opened onClose={onClose} title={isNew ? 'Novo cliente' : `Editar ${customer!.tradingName}`} size="xl">
      <form onSubmit={form.onSubmit((v) => save.mutate(v))}>
        <Stack>
          {company.data?.state === 'RJ' && (
            <Alert variant="light" p="xs">
              Empresa do RJ: tipo de atividade, função do contato e e-mail são obrigatórios para os documentos do INEA.
            </Alert>
          )}
          <CustomerFields form={form} isNew={isNew} />
          <Text size="xs" c="dimmed">
            A localização (latitude/longitude) é calculada automaticamente a partir do endereço.
          </Text>
          <Group justify="flex-end">
            <Button variant="default" onClick={onClose}>
              Cancelar
            </Button>
            <Button type="submit" loading={save.isPending}>
              Salvar
            </Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  )
}
