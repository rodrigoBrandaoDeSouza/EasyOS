import { useEffect, useState } from 'react'
import { ActionIcon, Button, Divider, Grid, Group, Modal, NumberInput, Paper, Select, Stack, Switch, Table, Text, TextInput, Textarea } from '@mantine/core'
import { useDebouncedValue } from '@mantine/hooks'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { IconPencil, IconPlus, IconSearch, IconTrash } from '@tabler/icons-react'
import { api } from '../../api/client'
import type { Plague, ProductDetail, ProductListItem, ProductType } from '../../api/types'
import { Loading } from '../../components/common'
import { notifyError, notifySuccess } from '../../lib/notify'

export function ProductsTab() {
  const queryClient = useQueryClient()
  const [search, setSearch] = useState('')
  const [debounced] = useDebouncedValue(search, 200)
  const [editing, setEditing] = useState<number | 'new' | null>(null)
  const products = useQuery({ queryKey: ['products'], queryFn: () => api.get<ProductListItem[]>('/api/products') })

  const toggle = useMutation({
    mutationFn: (p: ProductListItem) => api.post(`/api/products/${p.id}/status`, { active: !p.active }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['products'] }),
    onError: notifyError,
  })

  if (products.isPending) return <Loading />
  const term = debounced.trim().toLowerCase()
  const list = (products.data ?? []).filter((p) => !term || p.name.toLowerCase().includes(term) || p.activeIngredient.toLowerCase().includes(term))

  return (
    <Paper p="md">
      <Group justify="space-between" mb="sm">
        <TextInput placeholder="Buscar produto" leftSection={<IconSearch size={16} />} value={search} onChange={(e) => setSearch(e.currentTarget.value)} w={300} />
        <Button leftSection={<IconPlus size={16} />} onClick={() => setEditing('new')}>
          Novo produto
        </Button>
      </Group>
      <Table.ScrollContainer minWidth={760}>
        <Table>
          <Table.Thead>
            <Table.Tr>
              <Table.Th w={80}>Código</Table.Th>
              <Table.Th>Produto</Table.Th>
              <Table.Th>Princípio ativo</Table.Th>
              <Table.Th>Registro</Table.Th>
              <Table.Th w={70}>Unid.</Table.Th>
              <Table.Th w={80}>Ativo</Table.Th>
              <Table.Th w={50} />
            </Table.Tr>
          </Table.Thead>
          <Table.Tbody>
            {list.map((p) => (
              <Table.Tr key={p.id}>
                <Table.Td>{p.productCode}</Table.Td>
                <Table.Td>{p.name}</Table.Td>
                <Table.Td>{p.activeIngredient}</Table.Td>
                <Table.Td>{p.registrationNumber}</Table.Td>
                <Table.Td>{p.unity}</Table.Td>
                <Table.Td>
                  <Switch checked={p.active} onChange={() => toggle.mutate(p)} aria-label={`Ativar ${p.name}`} />
                </Table.Td>
                <Table.Td>
                  <ActionIcon variant="subtle" onClick={() => setEditing(p.id)} aria-label={`Editar ${p.name}`}>
                    <IconPencil size={16} />
                  </ActionIcon>
                </Table.Td>
              </Table.Tr>
            ))}
          </Table.Tbody>
        </Table>
      </Table.ScrollContainer>
      {editing && <ProductModal productId={editing === 'new' ? null : editing} onClose={() => setEditing(null)} />}
    </Paper>
  )
}

interface FormState {
  name: string
  productTypeId: string | null
  productCode: number
  registrationNumber: string
  chemicalGroup: string
  activeIngredient: string
  equipmentType: string
  symptoms: string
  antidotes: string
  plagues: { plagueId: string | null; concentration: number }[]
  applications: { id: number | null; description: string }[]
  ingredients: { id: number | null; activeIngredient: string; concentration: number }[]
}

const EMPTY: FormState = {
  name: '', productTypeId: null, productCode: 0, registrationNumber: '', chemicalGroup: '', activeIngredient: '', equipmentType: '',
  symptoms: '', antidotes: '', plagues: [], applications: [], ingredients: [],
}

/** Produto com pragas (e concentração), formas de aplicação e princípios ativos, como no cadastro do legado. */
function ProductModal({ productId, onClose }: { productId: number | null; onClose: () => void }) {
  const queryClient = useQueryClient()
  const [state, setState] = useState<FormState>(EMPTY)
  const types = useQuery({ queryKey: ['product-types'], queryFn: () => api.get<ProductType[]>('/api/product-types') })
  const plagues = useQuery({ queryKey: ['plagues', 'all'], queryFn: () => api.get<Plague[]>('/api/plagues') })
  const detail = useQuery({
    queryKey: ['product', productId],
    queryFn: () => api.get<ProductDetail>(`/api/products/${productId}`),
    enabled: productId !== null,
  })

  useEffect(() => {
    const d = detail.data
    if (!d) return
    setState({
      ...d,
      productTypeId: String(d.productTypeId),
      plagues: d.plagues.map((p) => ({ plagueId: String(p.plagueId), concentration: p.concentration })),
      applications: d.applications,
      ingredients: d.activeIngredients,
    })
  }, [detail.data])

  const save = useMutation({
    mutationFn: () => {
      const body = {
        name: state.name,
        productTypeId: Number(state.productTypeId),
        productCode: state.productCode,
        registrationNumber: state.registrationNumber,
        chemicalGroup: state.chemicalGroup,
        activeIngredient: state.activeIngredient,
        equipmentType: state.equipmentType,
        symptoms: state.symptoms,
        antidotes: state.antidotes,
        plagues: state.plagues.filter((p) => p.plagueId).map((p) => ({ plagueId: Number(p.plagueId), concentration: p.concentration })),
        applications: state.applications.filter((a) => a.description.trim()),
        activeIngredients: state.ingredients.filter((i) => i.activeIngredient.trim()),
      }
      return productId ? api.put(`/api/products/${productId}`, body) : api.post('/api/products', body)
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['products'] })
      queryClient.invalidateQueries({ queryKey: ['product', productId] })
      notifySuccess(productId ? 'Produto editado com sucesso' : 'Produto adicionado com sucesso')
      onClose()
    },
    onError: notifyError,
  })

  const set = (patch: Partial<FormState>) => setState((s) => ({ ...s, ...patch }))
  const plagueOptions = (plagues.data ?? []).map((p) => ({ value: String(p.id), label: p.name }))

  return (
    <Modal opened onClose={onClose} title={productId ? 'Editar produto' : 'Novo produto'} size="xl">
      {productId && detail.isPending ? (
        <Loading />
      ) : (
        <Stack>
          <Grid gap="sm">
            <Grid.Col span={{ base: 12, sm: 8 }}>
              <TextInput label="Nome comercial" withAsterisk value={state.name} onChange={(e) => set({ name: e.currentTarget.value })} />
            </Grid.Col>
            <Grid.Col span={{ base: 12, sm: 4 }}>
              <Select
                label="Tipo"
                withAsterisk
                data={(types.data ?? []).map((t) => ({ value: String(t.id), label: `${t.type} (${t.unity})` }))}
                value={state.productTypeId}
                onChange={(v) => set({ productTypeId: v })}
              />
            </Grid.Col>
            <Grid.Col span={{ base: 6, sm: 3 }}>
              <NumberInput label="Código" min={0} value={state.productCode} onChange={(v) => set({ productCode: Number(v) || 0 })} />
            </Grid.Col>
            <Grid.Col span={{ base: 6, sm: 3 }}>
              <TextInput label="Registro M.S." value={state.registrationNumber} onChange={(e) => set({ registrationNumber: e.currentTarget.value })} />
            </Grid.Col>
            <Grid.Col span={{ base: 12, sm: 3 }}>
              <TextInput label="Grupo químico" value={state.chemicalGroup} onChange={(e) => set({ chemicalGroup: e.currentTarget.value })} />
            </Grid.Col>
            <Grid.Col span={{ base: 12, sm: 3 }}>
              <TextInput label="Princípio ativo" value={state.activeIngredient} onChange={(e) => set({ activeIngredient: e.currentTarget.value })} />
            </Grid.Col>
            <Grid.Col span={{ base: 12, sm: 4 }}>
              <TextInput label="Tipo de equipamento" value={state.equipmentType} onChange={(e) => set({ equipmentType: e.currentTarget.value })} />
            </Grid.Col>
            <Grid.Col span={{ base: 12, sm: 4 }}>
              <Textarea label="Sintomas" autosize value={state.symptoms} onChange={(e) => set({ symptoms: e.currentTarget.value })} />
            </Grid.Col>
            <Grid.Col span={{ base: 12, sm: 4 }}>
              <Textarea label="Antídotos" autosize value={state.antidotes} onChange={(e) => set({ antidotes: e.currentTarget.value })} />
            </Grid.Col>
          </Grid>

          <Divider label="Pragas e concentração" labelPosition="left" />
          {state.plagues.map((p, i) => (
            <Group key={i} gap="xs" align="flex-end">
              <Select
                label="Praga"
                data={plagueOptions}
                searchable
                value={p.plagueId}
                onChange={(v) => set({ plagues: state.plagues.map((x, j) => (j === i ? { ...x, plagueId: v } : x)) })}
                style={{ flex: 1 }}
              />
              <NumberInput
                label="Concentração"
                min={0}
                decimalScale={4}
                decimalSeparator=","
                value={p.concentration}
                onChange={(v) => set({ plagues: state.plagues.map((x, j) => (j === i ? { ...x, concentration: Number(v) || 0 } : x)) })}
                w={140}
              />
              <ActionIcon color="red" variant="subtle" mb={4} onClick={() => set({ plagues: state.plagues.filter((_, j) => j !== i) })} aria-label="Remover praga">
                <IconTrash size={16} />
              </ActionIcon>
            </Group>
          ))}
          <Button variant="light" size="xs" w="fit-content" leftSection={<IconPlus size={14} />} onClick={() => set({ plagues: [...state.plagues, { plagueId: null, concentration: 0 }] })}>
            Relacionar praga
          </Button>

          <Divider label="Formas de aplicação / equipamentos" labelPosition="left" />
          {state.applications.map((a, i) => (
            <Group key={i} gap="xs" align="flex-end">
              <TextInput
                label="Descrição"
                value={a.description}
                onChange={(e) => set({ applications: state.applications.map((x, j) => (j === i ? { ...x, description: e.currentTarget.value } : x)) })}
                style={{ flex: 1 }}
              />
              <ActionIcon color="red" variant="subtle" mb={4} onClick={() => set({ applications: state.applications.filter((_, j) => j !== i) })} aria-label="Remover aplicação">
                <IconTrash size={16} />
              </ActionIcon>
            </Group>
          ))}
          <Button variant="light" size="xs" w="fit-content" leftSection={<IconPlus size={14} />} onClick={() => set({ applications: [...state.applications, { id: null, description: '' }] })}>
            Adicionar aplicação
          </Button>

          <Divider label="Princípios ativos (documentos INEA)" labelPosition="left" />
          {state.ingredients.map((ing, i) => (
            <Group key={i} gap="xs" align="flex-end">
              <TextInput
                label="Princípio ativo"
                value={ing.activeIngredient}
                onChange={(e) => set({ ingredients: state.ingredients.map((x, j) => (j === i ? { ...x, activeIngredient: e.currentTarget.value } : x)) })}
                style={{ flex: 1 }}
              />
              <NumberInput
                label="Concentração (%)"
                min={0}
                decimalScale={4}
                decimalSeparator=","
                value={ing.concentration}
                onChange={(v) => set({ ingredients: state.ingredients.map((x, j) => (j === i ? { ...x, concentration: Number(v) || 0 } : x)) })}
                w={160}
              />
              <ActionIcon color="red" variant="subtle" mb={4} onClick={() => set({ ingredients: state.ingredients.filter((_, j) => j !== i) })} aria-label="Remover princípio ativo">
                <IconTrash size={16} />
              </ActionIcon>
            </Group>
          ))}
          <Button
            variant="light"
            size="xs"
            w="fit-content"
            leftSection={<IconPlus size={14} />}
            onClick={() => set({ ingredients: [...state.ingredients, { id: null, activeIngredient: '', concentration: 0 }] })}
          >
            Adicionar princípio ativo
          </Button>

          <Text size="xs" c="dimmed">
            Produtos são compartilhados por todas as empresas deste banco; a ativação é por empresa.
          </Text>
          <Group justify="flex-end">
            <Button variant="default" onClick={onClose}>
              Cancelar
            </Button>
            <Button onClick={() => save.mutate()} loading={save.isPending} disabled={!state.name.trim() || !state.productTypeId}>
              Salvar
            </Button>
          </Group>
        </Stack>
      )}
    </Modal>
  )
}
