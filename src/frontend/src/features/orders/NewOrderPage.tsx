import { useState } from 'react'
import {
  ActionIcon, Alert, Button, CloseButton, FileButton, Grid, Group, Image, NumberInput, Paper, SegmentedControl, Select, SimpleGrid,
  Stack, Switch, TagsInput, Text, Textarea, TextInput, Title,
} from '@mantine/core'
import { DateInput, TimeInput } from '@mantine/dates'
import { useMutation, useQuery } from '@tanstack/react-query'
import { IconArrowLeft, IconPhotoPlus, IconPlus, IconTrash } from '@tabler/icons-react'
import dayjs from 'dayjs'
import { useNavigate } from 'react-router'
import { api } from '../../api/client'
import type { Plague, PlagueStatus, ProductForPlague, VisitListItem } from '../../api/types'
import { useBranding } from '../../app/BrandingContext'
import { PageHeader } from '../../components/common'
import { compressImage } from '../../lib/files'
import { browserOffsetMinutes, fmtDateTime, toApiDateTime } from '../../lib/format'
import { notifyError, notifySuccess } from '../../lib/notify'
import { useLookups } from '../visits/VisitModal'
import { BAIT_TYPES, GLUE_TYPES, INSTALLED, luminousColumns } from './orderLabels'

interface PlagueRow { plagueId: string | null; statusId: string | null; location: string; quantity: number }
interface PheromoneRow { type: string; point: string; quantity: number }
interface LuminousRow { point: string; quantity: number; counts: Record<number, number> }
interface ConsumptionRow { plagueId: string | null; plagueProductId: string | null; applicationId: string | null; lot: string; expirationDate: string; quantity: number }
interface NonConformityRow { place: string; description: string; suggestion: string }

function Toggle({ label, checked, onChange }: { label: string; checked: boolean; onChange: (v: boolean) => void }) {
  return <Switch label={label} checked={checked} onChange={(e) => onChange(e.currentTarget.checked)} size="md" />
}

function Block({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <Paper p="md">
      <Title order={5} mb="sm" tt="uppercase" c="brand.8">
        {title}
      </Title>
      <Stack gap="sm">{children}</Stack>
    </Paper>
  )
}

/** Lançamento manual de OS pela web (mesmo conteúdo da OS enviada pelo aplicativo). */
export function NewOrderPage() {
  const navigate = useNavigate()
  const branding = useBranding()
  const lookups = useLookups()

  const [customerId, setCustomerId] = useState<string | null>(null)
  const [technicianId, setTechnicianId] = useState<string | null>(null)
  const [visitId, setVisitId] = useState<string | null>(null)
  const [date, setDate] = useState<string | null>(dayjs().format('YYYY-MM-DD'))
  const [timeStart, setTimeStart] = useState('08:00')
  const [timeEnd, setTimeEnd] = useState('09:00')
  const [responsibleName, setResponsibleName] = useState('')
  const [responsibleBusiness, setResponsibleBusiness] = useState('')
  const [executed, setExecuted] = useState(true)
  const [reason, setReason] = useState('')
  const [hadPlagues, setHadPlagues] = useState(false)
  const [plagues, setPlagues] = useState<PlagueRow[]>([])
  const [equipment, setEquipment] = useState(false)
  const [hadBaiting, setHadBaiting] = useState(false)
  const [baitInstalled, setBaitInstalled] = useState(0)
  const [baitPoints, setBaitPoints] = useState<Record<string, string[]>>({})
  const [hadGlue, setHadGlue] = useState(false)
  const [glueInstalled, setGlueInstalled] = useState(0)
  const [gluePoints, setGluePoints] = useState<Record<string, string[]>>({})
  const [hadLuminous, setHadLuminous] = useState(false)
  const [luminousInstalled, setLuminousInstalled] = useState(0)
  const [needCount, setNeedCount] = useState(false)
  const [countMode, setCountMode] = useState<'trap' | 'insect'>('trap')
  const [luminous, setLuminous] = useState<LuminousRow[]>([])
  const [hadPheromone, setHadPheromone] = useState(false)
  const [pheromones, setPheromones] = useState<PheromoneRow[]>([])
  const [hadConsumption, setHadConsumption] = useState(false)
  const [consumptions, setConsumptions] = useState<ConsumptionRow[]>([])
  const [nonConformities, setNonConformities] = useState<NonConformityRow[]>([])
  const [evidences, setEvidences] = useState<string[]>([])
  const [observations, setObservations] = useState('')

  const plagueList = useQuery({ queryKey: ['plagues', 'active'], queryFn: () => api.get<Plague[]>('/api/plagues', { onlyActive: true }) })
  const statuses = useQuery({ queryKey: ['plague-statuses'], queryFn: () => api.get<PlagueStatus[]>('/api/plague-statuses') })
  const pendingVisits = useQuery({
    queryKey: ['visits', 'pending', customerId],
    queryFn: () => api.get<VisitListItem[]>('/api/visits/pending-for-order', { customerId }),
    enabled: !!customerId,
  })

  const plagueOptions = (plagueList.data ?? []).map((p) => ({ value: String(p.id), label: p.name }))
  const statusOptions = (statuses.data ?? []).map((s) => ({ value: String(s.id), label: s.description }))
  const insects = luminousColumns(branding.otherPlaguesLabel)

  const save = useMutation({
    mutationFn: () => {
      if (!customerId || !technicianId || !date) throw new Error('Informe cliente, técnico e data.')
      const offset = browserOffsetMinutes()
      const start = dayjs(`${date}T${timeStart}`)
      const end = dayjs(`${date}T${timeEnd}`)
      const flags = {
        hadEvidenceOfPlague: executed && hadPlagues,
        hadEquipmentRevision: executed && equipment,
        hadBaiting: executed && equipment && hadBaiting,
        hadPlaquesOfGlue: executed && equipment && hadGlue,
        hadLuminousTrap: executed && equipment && hadLuminous,
        customerNeedPlagueAccount: executed && equipment && hadLuminous && needCount,
        hadPheromoneTrap: executed && equipment && hadPheromone,
        hadProductConsumption: executed && hadConsumption,
      }
      const points = (map: Record<string, string[]>, types: readonly { type: string }[], installed: number) => [
        ...types.flatMap((t) => (map[t.type] ?? []).map((point) => ({ type: t.type, point, quantity: 0 }))),
        { type: INSTALLED, point: '', quantity: installed },
      ]
      return api.post<{ id: number; sequence: number }>('/api/orders', {
        customerId: Number(customerId),
        visitId: visitId ? Number(visitId) : 0,
        technicianId: Number(technicianId),
        orderDate: date,
        timeStart: toApiDateTime(start.toDate()),
        timeEnd: toApiDateTime(end.toDate()),
        offsetMinutes: offset,
        serviceExecuted: executed,
        reasonOfNonExecution: executed ? null : reason,
        responsibleName,
        responsibleBusiness,
        observations,
        details: flags,
        plagues: flags.hadEvidenceOfPlague
          ? plagues.filter((p) => p.plagueId && p.statusId).map((p) => ({ plagueId: Number(p.plagueId), plagueStatusId: Number(p.statusId), location: p.location, quantity: p.quantity }))
          : [],
        baitings: flags.hadBaiting ? points(baitPoints, BAIT_TYPES, baitInstalled).map((b) => ({ ...b, rodentQuantity: 0 })) : [],
        plaquesOfGlue: flags.hadPlaquesOfGlue ? points(gluePoints, GLUE_TYPES, glueInstalled).map((g) => ({ ...g, rodentQuantity: g.type === 'capturado' ? 1 : 0 })) : [],
        pheromones: flags.hadPheromoneTrap ? pheromones.filter((p) => p.point) : [],
        luminous: flags.hadLuminousTrap
          ? [
              ...(needCount
                ? luminous
                    .filter((l) => l.point)
                    .map((l) =>
                      countMode === 'trap'
                        ? { point: l.point, quantity: l.quantity, plagueCounts: [] }
                        : { point: l.point, quantity: 0, plagueCounts: insects.map((c) => ({ plagueId: c.code, quantity: l.counts[c.code] ?? 0 })) },
                    )
                : []),
              { point: '', quantity: luminousInstalled, plagueCounts: [] },
            ]
          : [],
        consumptions: flags.hadProductConsumption
          ? consumptions
              .filter((c) => c.plagueProductId && c.applicationId)
              .map((c) => ({ plagueProductId: Number(c.plagueProductId), productApplicationId: Number(c.applicationId), quantity: c.quantity, lot: c.lot, expirationDate: c.expirationDate }))
          : [],
        nonConformities: nonConformities.filter((n) => n.place || n.description),
        evidences,
      })
    },
    onSuccess: (result) => {
      notifySuccess(`OS #${result.sequence} criada com sucesso`)
      navigate(`/os/${result.id}`, { replace: true })
    },
    onError: notifyError,
  })

  const updateList = <T,>(setter: React.Dispatch<React.SetStateAction<T[]>>, index: number, patch: Partial<T>) =>
    setter((rows) => rows.map((r, i) => (i === index ? { ...r, ...patch } : r)))
  const removeAt = <T,>(setter: React.Dispatch<React.SetStateAction<T[]>>, index: number) => setter((rows) => rows.filter((_, i) => i !== index))

  return (
    <>
      <PageHeader
        title="Lançar ordem de serviço"
        subtitle="Use quando o atendimento não foi registrado pelo aplicativo"
        actions={
          <Button variant="subtle" leftSection={<IconArrowLeft size={16} />} onClick={() => navigate('/os')}>
            Voltar
          </Button>
        }
      />
      <Stack>
        <Block title="Informações gerais">
          <Grid gap="sm">
            <Grid.Col span={{ base: 12, md: 6 }}>
              <Select
                label="Cliente"
                data={lookups.customers}
                searchable
                withAsterisk
                value={customerId}
                onChange={(v) => {
                  setCustomerId(v)
                  setVisitId(null)
                }}
              />
            </Grid.Col>
            <Grid.Col span={{ base: 12, md: 6 }}>
              <Select label="Técnico" data={lookups.technicians} searchable withAsterisk value={technicianId} onChange={setTechnicianId} />
            </Grid.Col>
            <Grid.Col span={{ base: 12, md: 6 }}>
              <Select
                label="Visita pendente (opcional)"
                description="Ao vincular, a visita é encerrada com check-in no início da OS"
                data={(pendingVisits.data ?? []).map((v) => ({ value: String(v.id), label: `${fmtDateTime(v.visitDate)} - ${v.technicianName ?? ''}` }))}
                clearable
                disabled={!customerId}
                value={visitId}
                onChange={(v) => {
                  setVisitId(v)
                  const visit = pendingVisits.data?.find((x) => String(x.id) === v)
                  if (visit) {
                    setDate(dayjs(visit.visitDate).format('YYYY-MM-DD'))
                    if (visit.technicianId) setTechnicianId(String(visit.technicianId))
                  }
                }}
              />
            </Grid.Col>
            <Grid.Col span={{ base: 12, sm: 4, md: 2 }}>
              <DateInput label="Data" valueFormat="DD/MM/YYYY" withAsterisk value={date} onChange={setDate} mt={{ md: 22 }} />
            </Grid.Col>
            <Grid.Col span={{ base: 6, sm: 4, md: 2 }}>
              <TimeInput label="Início" value={timeStart} onChange={(e) => setTimeStart(e.currentTarget.value)} mt={{ md: 22 }} />
            </Grid.Col>
            <Grid.Col span={{ base: 6, sm: 4, md: 2 }}>
              <TimeInput label="Término" value={timeEnd} onChange={(e) => setTimeEnd(e.currentTarget.value)} mt={{ md: 22 }} />
            </Grid.Col>
            <Grid.Col span={{ base: 12, sm: 6 }}>
              <TextInput label="Nome do responsável" value={responsibleName} onChange={(e) => setResponsibleName(e.currentTarget.value)} />
            </Grid.Col>
            <Grid.Col span={{ base: 12, sm: 6 }}>
              <TextInput label="Cargo do responsável" value={responsibleBusiness} onChange={(e) => setResponsibleBusiness(e.currentTarget.value)} />
            </Grid.Col>
          </Grid>
        </Block>

        <Block title="Informações básicas do serviço">
          <Toggle label="O serviço foi realizado?" checked={executed} onChange={setExecuted} />
          {!executed && <Textarea label="Motivo da não realização" withAsterisk value={reason} onChange={(e) => setReason(e.currentTarget.value)} maxLength={500} />}
        </Block>

        {executed && (
          <>
            <Block title="Pragas encontradas">
              <Toggle label="Houve evidências ou focos de pragas?" checked={hadPlagues} onChange={setHadPlagues} />
              {hadPlagues && (
                <>
                  {plagues.map((p, i) => (
                    <Group key={i} align="flex-end" gap="xs" wrap="nowrap">
                      <Select label="Praga" data={plagueOptions} searchable value={p.plagueId} onChange={(v) => updateList(setPlagues, i, { plagueId: v })} style={{ flex: 2 }} />
                      <Select label="Status" data={statusOptions} value={p.statusId} onChange={(v) => updateList(setPlagues, i, { statusId: v })} style={{ flex: 2 }} />
                      <TextInput label="Local" value={p.location} onChange={(e) => updateList(setPlagues, i, { location: e.currentTarget.value })} style={{ flex: 2 }} />
                      <NumberInput label="Qtd." min={0} value={p.quantity} onChange={(v) => updateList(setPlagues, i, { quantity: Number(v) || 0 })} w={90} />
                      <ActionIcon color="red" variant="subtle" onClick={() => removeAt(setPlagues, i)} aria-label="Remover praga" mb={4}>
                        <IconTrash size={18} />
                      </ActionIcon>
                    </Group>
                  ))}
                  <Button variant="light" leftSection={<IconPlus size={16} />} w="fit-content" onClick={() => setPlagues((r) => [...r, { plagueId: null, statusId: null, location: '', quantity: 1 }])}>
                    Adicionar praga
                  </Button>
                </>
              )}
            </Block>

            <Block title="Equipamentos">
              <Toggle label="Houve revisão de equipamento?" checked={equipment} onChange={setEquipment} />
              {equipment && (
                <Stack gap="lg">
                  <Stack gap="xs">
                    <Toggle label="Houve revisão de iscagem?" checked={hadBaiting} onChange={setHadBaiting} />
                    {hadBaiting && (
                      <PointsEditor installed={baitInstalled} onInstalled={setBaitInstalled} types={BAIT_TYPES} points={baitPoints} onPoints={setBaitPoints} />
                    )}
                  </Stack>
                  <Stack gap="xs">
                    <Toggle label="Houve revisão de placa de cola/armadilha mecânica?" checked={hadGlue} onChange={setHadGlue} />
                    {hadGlue && <PointsEditor installed={glueInstalled} onInstalled={setGlueInstalled} types={GLUE_TYPES} points={gluePoints} onPoints={setGluePoints} />}
                  </Stack>
                  <Stack gap="xs">
                    <Toggle label="Houve revisão de armadilha luminosa?" checked={hadLuminous} onChange={setHadLuminous} />
                    {hadLuminous && (
                      <Stack gap="xs" pl="md">
                        <NumberInput label="Quantidade de pontos" min={0} value={luminousInstalled} onChange={(v) => setLuminousInstalled(Number(v) || 0)} w={200} />
                        <Toggle label="O cliente exige contagem de insetos por armadilha?" checked={needCount} onChange={setNeedCount} />
                        {needCount && (
                          <>
                            <SegmentedControl
                              value={countMode}
                              onChange={(v) => setCountMode(v as 'trap' | 'insect')}
                              data={[
                                { value: 'trap', label: 'Total por armadilha' },
                                { value: 'insect', label: 'Por tipo de inseto' },
                              ]}
                              w="fit-content"
                            />
                            {luminous.map((l, i) => (
                              <Group key={i} gap="xs" align="flex-end" wrap="wrap">
                                <TextInput label="Identificação" value={l.point} onChange={(e) => updateList(setLuminous, i, { point: e.currentTarget.value })} w={140} />
                                {countMode === 'trap' ? (
                                  <NumberInput label="Quantidade" min={0} value={l.quantity} onChange={(v) => updateList(setLuminous, i, { quantity: Number(v) || 0 })} w={120} />
                                ) : (
                                  insects.map((c) => (
                                    <NumberInput
                                      key={c.code}
                                      label={c.label}
                                      min={0}
                                      w={100}
                                      value={l.counts[c.code] ?? 0}
                                      onChange={(v) => updateList(setLuminous, i, { counts: { ...l.counts, [c.code]: Number(v) || 0 } })}
                                    />
                                  ))
                                )}
                                <ActionIcon color="red" variant="subtle" onClick={() => removeAt(setLuminous, i)} aria-label="Remover armadilha" mb={4}>
                                  <IconTrash size={18} />
                                </ActionIcon>
                              </Group>
                            ))}
                            <Button variant="light" w="fit-content" leftSection={<IconPlus size={16} />} onClick={() => setLuminous((r) => [...r, { point: '', quantity: 0, counts: {} }])}>
                              Adicionar armadilha
                            </Button>
                          </>
                        )}
                      </Stack>
                    )}
                  </Stack>
                  <Stack gap="xs">
                    <Toggle label="Houve revisão de armadilha de feromônio?" checked={hadPheromone} onChange={setHadPheromone} />
                    {hadPheromone && (
                      <Stack gap="xs" pl="md">
                        {pheromones.map((p, i) => (
                          <Group key={i} gap="xs" align="flex-end">
                            <Select label="Tipo" data={['Traça', 'Besouro']} value={p.type} onChange={(v) => updateList(setPheromones, i, { type: v ?? 'Traça' })} w={130} allowDeselect={false} />
                            <TextInput label="Identificação" value={p.point} onChange={(e) => updateList(setPheromones, i, { point: e.currentTarget.value })} w={160} />
                            <NumberInput label="Quantidade" min={0} value={p.quantity} onChange={(v) => updateList(setPheromones, i, { quantity: Number(v) || 0 })} w={120} />
                            <ActionIcon color="red" variant="subtle" onClick={() => removeAt(setPheromones, i)} aria-label="Remover armadilha" mb={4}>
                              <IconTrash size={18} />
                            </ActionIcon>
                          </Group>
                        ))}
                        <Button variant="light" w="fit-content" leftSection={<IconPlus size={16} />} onClick={() => setPheromones((r) => [...r, { type: 'Traça', point: '', quantity: 0 }])}>
                          Adicionar armadilha
                        </Button>
                      </Stack>
                    )}
                  </Stack>
                </Stack>
              )}
            </Block>

            <Block title="Consumo de produtos">
              <Toggle label="Houve consumo de produtos?" checked={hadConsumption} onChange={setHadConsumption} />
              {hadConsumption && (
                <>
                  {consumptions.map((c, i) => (
                    <ConsumptionLine
                      key={i}
                      row={c}
                      plagueOptions={plagueOptions}
                      onChange={(patch) => updateList(setConsumptions, i, patch)}
                      onRemove={() => removeAt(setConsumptions, i)}
                    />
                  ))}
                  <Button
                    variant="light"
                    w="fit-content"
                    leftSection={<IconPlus size={16} />}
                    onClick={() => setConsumptions((r) => [...r, { plagueId: null, plagueProductId: null, applicationId: null, lot: '', expirationDate: '', quantity: 0 }])}
                  >
                    Adicionar produto
                  </Button>
                </>
              )}
            </Block>
          </>
        )}

        <Block title="Informações adicionais">
          <Text size="sm" fw={500}>
            Não conformidades
          </Text>
          {nonConformities.map((n, i) => (
            <Group key={i} gap="xs" align="flex-end" wrap="nowrap">
              <TextInput label="Área e local" value={n.place} onChange={(e) => updateList(setNonConformities, i, { place: e.currentTarget.value })} style={{ flex: 1 }} />
              <TextInput label="Não conformidade" value={n.description} onChange={(e) => updateList(setNonConformities, i, { description: e.currentTarget.value })} style={{ flex: 2 }} />
              <TextInput label="Ação sugerida" value={n.suggestion} onChange={(e) => updateList(setNonConformities, i, { suggestion: e.currentTarget.value })} style={{ flex: 2 }} />
              <ActionIcon color="red" variant="subtle" onClick={() => removeAt(setNonConformities, i)} aria-label="Remover" mb={4}>
                <IconTrash size={18} />
              </ActionIcon>
            </Group>
          ))}
          <Button variant="light" w="fit-content" leftSection={<IconPlus size={16} />} onClick={() => setNonConformities((r) => [...r, { place: '', description: '', suggestion: '' }])}>
            Adicionar não conformidade
          </Button>

          <Text size="sm" fw={500} mt="sm">
            Evidências (fotos)
          </Text>
          <SimpleGrid cols={{ base: 2, sm: 4, md: 6 }}>
            {evidences.map((e, i) => (
              <div key={i} style={{ position: 'relative' }}>
                <Image src={e} h={110} fit="cover" radius="sm" alt="Evidência" />
                <CloseButton
                  size="sm"
                  style={{ position: 'absolute', top: 4, right: 4, background: 'white' }}
                  onClick={() => setEvidences((r) => r.filter((_, x) => x !== i))}
                  aria-label="Remover foto"
                />
              </div>
            ))}
          </SimpleGrid>
          <FileButton
            multiple
            accept="image/png,image/jpeg"
            onChange={async (files) => {
              try {
                const images = await Promise.all(files.map((f) => compressImage(f)))
                setEvidences((r) => [...r, ...images])
              } catch (e) {
                notifyError(e)
              }
            }}
          >
            {(props) => (
              <Button {...props} variant="light" w="fit-content" leftSection={<IconPhotoPlus size={16} />}>
                Adicionar fotos
              </Button>
            )}
          </FileButton>
        </Block>

        <Block title="Observações">
          <Textarea autosize minRows={3} value={observations} onChange={(e) => setObservations(e.currentTarget.value)} />
        </Block>

        <Alert variant="light" color="orange">
          Revise as informações antes de salvar: a OS recebe numeração sequencial e passa a constar nos relatórios do cliente.
        </Alert>
        <Group justify="flex-end">
          <Button variant="default" onClick={() => navigate('/os')}>
            Cancelar
          </Button>
          <Button onClick={() => save.mutate()} loading={save.isPending}>
            Salvar OS
          </Button>
        </Group>
      </Stack>
    </>
  )
}

function PointsEditor({
  installed,
  onInstalled,
  types,
  points,
  onPoints,
}: {
  installed: number
  onInstalled: (v: number) => void
  types: readonly { type: string; label: string }[]
  points: Record<string, string[]>
  onPoints: (v: Record<string, string[]>) => void
}) {
  return (
    <Stack gap="xs" pl="md">
      <NumberInput label="Quantidade de pontos" min={0} value={installed} onChange={(v) => onInstalled(Number(v) || 0)} w={200} />
      {types.map((t) => (
        <TagsInput
          key={t.type}
          label={`${t.label} (identificação dos pontos)`}
          placeholder="Digite o número do ponto e tecle Enter"
          value={points[t.type] ?? []}
          onChange={(v) => onPoints({ ...points, [t.type]: v })}
        />
      ))}
    </Stack>
  )
}

function ConsumptionLine({
  row,
  plagueOptions,
  onChange,
  onRemove,
}: {
  row: ConsumptionRow
  plagueOptions: { value: string; label: string }[]
  onChange: (patch: Partial<ConsumptionRow>) => void
  onRemove: () => void
}) {
  const products = useQuery({
    queryKey: ['products', 'by-plague', row.plagueId],
    queryFn: () => api.get<ProductForPlague[]>(`/api/products/by-plague/${row.plagueId}`),
    enabled: !!row.plagueId,
  })
  const product = products.data?.find((p) => String(p.plagueProductId) === row.plagueProductId)

  return (
    <Paper p="xs" bg="gray.0">
      <Grid gap="xs" align="flex-end">
        <Grid.Col span={{ base: 12, md: 3 }}>
          <Select label="Praga" data={plagueOptions} searchable value={row.plagueId} onChange={(v) => onChange({ plagueId: v, plagueProductId: null, applicationId: null })} />
        </Grid.Col>
        <Grid.Col span={{ base: 12, md: 3 }}>
          <Select
            label="Produto"
            data={(products.data ?? []).map((p) => ({ value: String(p.plagueProductId), label: `${p.name} (${p.unity})` }))}
            searchable
            disabled={!row.plagueId}
            value={row.plagueProductId}
            onChange={(v) => onChange({ plagueProductId: v, applicationId: null })}
          />
        </Grid.Col>
        <Grid.Col span={{ base: 12, md: 2 }}>
          <Select
            label="Equipamento"
            data={(product?.applications ?? []).map((a) => ({ value: String(a.id), label: a.description }))}
            disabled={!product}
            value={row.applicationId}
            onChange={(v) => onChange({ applicationId: v })}
          />
        </Grid.Col>
        <Grid.Col span={{ base: 4, md: 1 }}>
          <TextInput label="Lote" value={row.lot} onChange={(e) => onChange({ lot: e.currentTarget.value })} />
        </Grid.Col>
        <Grid.Col span={{ base: 4, md: 1 }}>
          <TextInput label="Validade" placeholder="MM/AAAA" maxLength={10} value={row.expirationDate} onChange={(e) => onChange({ expirationDate: e.currentTarget.value })} />
        </Grid.Col>
        <Grid.Col span={{ base: 3, md: 1 }}>
          <NumberInput label={`Qtd.${product ? ` (${product.unity})` : ''}`} min={0} decimalScale={3} decimalSeparator="," value={row.quantity} onChange={(v) => onChange({ quantity: Number(v) || 0 })} />
        </Grid.Col>
        <Grid.Col span={{ base: 1, md: 1 }}>
          <ActionIcon color="red" variant="subtle" onClick={onRemove} aria-label="Remover produto" mb={4}>
            <IconTrash size={18} />
          </ActionIcon>
        </Grid.Col>
      </Grid>
    </Paper>
  )
}
