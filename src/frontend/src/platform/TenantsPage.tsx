import { useState } from 'react'
import { Anchor, Badge, Button, Group, Paper, Table, Text, TextInput, Title } from '@mantine/core'
import { useQuery } from '@tanstack/react-query'
import { IconPlus, IconSearch } from '@tabler/icons-react'
import { Link } from 'react-router'
import { api } from '../api/client'
import { Loading } from '../components/common'

interface TenantListItem {
  id: string
  slug: string
  name: string
  active: boolean
  primaryHost: string | null
  domains: string[]
  updatedAt: string
}

export function TenantsPage() {
  const tenants = useQuery({ queryKey: ['tenants'], queryFn: () => api.get<TenantListItem[]>('/api/platform/tenants') })
  const [search, setSearch] = useState('')
  const term = search.trim().toLowerCase()
  const list = (tenants.data ?? []).filter((t) => !term || t.name.toLowerCase().includes(term) || t.slug.includes(term) || t.domains.some((d) => d.includes(term)))

  return (
    <>
      <Group justify="space-between" mb="md">
        <Title order={2}>Clientes</Title>
        <Button component={Link} to="/clientes/novo" leftSection={<IconPlus size={16} />}>
          Novo cliente
        </Button>
      </Group>
      <Paper p="md">
        <TextInput placeholder="Buscar por nome, identificador ou domínio" leftSection={<IconSearch size={16} />} value={search} onChange={(e) => setSearch(e.currentTarget.value)} mb="md" maw={420} />
        {tenants.isPending ? (
          <Loading />
        ) : (
          <Table.ScrollContainer minWidth={640}>
            <Table>
              <Table.Thead>
                <Table.Tr>
                  <Table.Th>Cliente</Table.Th>
                  <Table.Th>Identificador</Table.Th>
                  <Table.Th>Domínios</Table.Th>
                  <Table.Th>Status</Table.Th>
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {list.map((t) => (
                  <Table.Tr key={t.id}>
                    <Table.Td>
                      <Anchor component={Link} to={`/clientes/${t.id}`} fw={600}>
                        {t.name}
                      </Anchor>
                    </Table.Td>
                    <Table.Td>
                      <Text size="sm" ff="monospace">
                        {t.slug}
                      </Text>
                    </Table.Td>
                    <Table.Td>
                      <Group gap={4}>
                        {t.domains.map((d) => (
                          <Badge key={d} variant={d === t.primaryHost ? 'filled' : 'light'} tt="none">
                            {d}
                          </Badge>
                        ))}
                      </Group>
                    </Table.Td>
                    <Table.Td>
                      <Badge color={t.active ? 'teal' : 'gray'}>{t.active ? 'Ativo' : 'Suspenso'}</Badge>
                    </Table.Td>
                  </Table.Tr>
                ))}
              </Table.Tbody>
            </Table>
          </Table.ScrollContainer>
        )}
        <Text size="xs" c="dimmed" mt="sm">
          {list.length} cliente(s)
        </Text>
      </Paper>
    </>
  )
}
