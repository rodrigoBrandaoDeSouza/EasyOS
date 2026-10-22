import { Anchor, Badge, Button, Divider, Grid, Group, Image, Paper, SimpleGrid, Stack, Table, Text, Title } from '@mantine/core'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { IconArrowLeft, IconCertificate, IconPrinter, IconTrash } from '@tabler/icons-react'
import { Link, useNavigate, useParams } from 'react-router'
import { api } from '../../api/client'
import type { Certificate, OrderDetail, Parameter, ResponsibleSignature } from '../../api/types'
import { useBranding } from '../../app/BrandingContext'
import { useMe } from '../../auth/AuthContext'
import { confirmAction, Loading } from '../../components/common'
import { fmtDate, fmtDateTime, fmtNumber, fmtTime } from '../../lib/format'
import { notifyError, notifySuccess } from '../../lib/notify'
import { OrderStatusBadge } from './OrderList'
import { BAIT_TYPES, dilution, GLUE_TYPES, INSTALLED, luminousColumns, PHEROMONE_TYPES } from './orderLabels'
import { CesButton } from '../inea/CesButton'

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <Paper p="md" className="avoid-break">
      <Title order={5} mb="sm" tt="uppercase" c="brand.8">
        {title}
      </Title>
      {children}
    </Paper>
  )
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <Stack gap={0}>
      <Text size="xs" c="dimmed">
        {label}
      </Text>
      <Text size="sm">{children || '—'}</Text>
    </Stack>
  )
}

function Points({ items }: { items: { point: string | null }[] }) {
  if (!items.length) return <Text size="sm">0</Text>
  return (
    <Group gap={4}>
      <Text size="sm" fw={600}>
        {items.length}
      </Text>
      {items.map((i, idx) => (
        <Badge key={idx} variant="outline" color="gray" size="sm">
          {i.point}
        </Badge>
      ))}
    </Group>
  )
}

/** Detalhe da OS (empresa e cliente). Também é a versão de impressão (Ctrl+P / Salvar como PDF). */
export function OrderPage() {
  const { orderId, customerId } = useParams()
  const me = useMe()
  const branding = useBranding()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const id = Number(orderId)
  const backTo = customerId ? `/c/${customerId}/os` : '/os'

  const order = useQuery({ queryKey: ['order', id], queryFn: () => api.get<OrderDetail>(`/api/orders/${id}`) })
  const responsible = useQuery({
    queryKey: ['order', id, 'responsible'],
    queryFn: () => api.get<ResponsibleSignature>(`/api/orders/${id}/responsible-signature`),
  })
  const parameters = useQuery({ queryKey: ['parameters'], queryFn: () => api.get<Parameter[]>('/api/parameters'), staleTime: 5 * 60_000 })
  // Parâmetro OS_RDC_622: exibe a descrição química dos produtos e o responsável técnico na própria OS.
  const isRdc622 = parameters.data?.some((p) => p.name === 'OS_RDC_622') ?? false
  const certificate = useQuery({
    queryKey: ['order', id, 'certificate'],
    queryFn: () => api.get<Certificate>(`/api/orders/${id}/certificate`),
    enabled: isRdc622,
  })

  const cancel = useMutation({
    mutationFn: () => api.delete(`/api/orders/${id}`),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['orders'] })
      queryClient.invalidateQueries({ queryKey: ['order', id] })
      notifySuccess('OS cancelada')
    },
    onError: notifyError,
  })

  if (order.isPending) return <Loading />
  if (order.isError) return <Text c="red">{order.error.message}</Text>
  const os = order.data
  const certificateUrl = `/imprimir/certificado/${os.id}`
  const installed = (list: { type: string | null; quantity: number }[]) => list.find((x) => x.type === INSTALLED)?.quantity ?? 0
  const insects = luminousColumns(branding.otherPlaguesLabel)
  const hasInsectCount = os.luminous.some((l) => l.plagueCounts.length > 0)
  const luminousPoints = os.luminous.filter((l) => l.point)

  return (
    <Stack>
      <Group justify="space-between" className="no-print">
        <Button variant="subtle" leftSection={<IconArrowLeft size={16} />} onClick={() => navigate(backTo)}>
          Voltar
        </Button>
        <Group gap="xs">
          {os.active && os.serviceExecuted && (
            <Button variant="light" leftSection={<IconCertificate size={16} />} component={Link} to={certificateUrl} target="_blank">
              Certificado
            </Button>
          )}
          {os.customer.state === 'RJ' && <CesButton orderId={os.id} />}
          <Button variant="light" leftSection={<IconPrinter size={16} />} onClick={() => window.print()}>
            Imprimir
          </Button>
          {me.type === 'company' && os.active && (
            <Button
              color="red"
              variant="light"
              leftSection={<IconTrash size={16} />}
              onClick={() => confirmAction('Confirma o cancelamento desta OS? Ela deixará de contar nos relatórios.', () => cancel.mutate(), 'Cancelar OS', true)}
            >
              Cancelar OS
            </Button>
          )}
        </Group>
      </Group>

      <Paper p="md">
        <Group justify="space-between" align="flex-start">
          <Stack gap={2}>
            <Group gap="xs">
              <Title order={2}>OS #{os.sequence}</Title>
              <OrderStatusBadge active={os.active} executed={os.serviceExecuted} />
            </Group>
            <Text c="dimmed" size="sm">
              {os.company.companyName} · Alvará {os.company.charter}
            </Text>
          </Stack>
          {branding.logoUrl && <Image src={branding.logoUrl} h={48} w="auto" fit="contain" alt={branding.name} className="print-only" />}
        </Group>
        <Divider my="sm" />
        <SimpleGrid cols={{ base: 2, sm: 4 }} spacing="sm">
          <Field label="Data">{fmtDate(os.orderDate)}</Field>
          <Field label="Horário">
            {fmtTime(os.timeStart)} às {fmtTime(os.timeEnd)}
          </Field>
          <Field label="Check-in">{os.checkinDate ? fmtDateTime(os.checkinDate) : null}</Field>
          <Field label="Técnico">{os.technicianName}</Field>
          <Field label="Cliente">{os.customer.tradingName}</Field>
          <Field label="Razão social">{os.customer.companyName}</Field>
          <Field label="CNPJ/CPF">{os.customer.cpfCnpj}</Field>
          <Field label="Responsável técnico">{os.technicalResponsible}</Field>
        </SimpleGrid>
        <Text size="sm" mt="sm">
          {os.customer.address}, {os.customer.number} {os.customer.complement} - {os.customer.neighborhood} - {os.customer.city}/{os.customer.state} -{' '}
          {os.customer.zipcode}
        </Text>
      </Paper>

      <Section title="Informações básicas do serviço">
        <Text size="sm">
          <b>O serviço foi realizado?</b> {os.serviceExecuted ? 'Sim' : 'Não'}
        </Text>
        {!os.serviceExecuted && os.reasonOfNonExecution && (
          <Text size="sm">
            <b>Motivo:</b> {os.reasonOfNonExecution}
          </Text>
        )}
      </Section>

      {os.plagues.length > 0 && (
        <Section title="Pragas encontradas">
          <Table.ScrollContainer minWidth={500}>
            <Table>
              <Table.Thead>
                <Table.Tr>
                  <Table.Th>Praga</Table.Th>
                  <Table.Th>Status</Table.Th>
                  <Table.Th>Local</Table.Th>
                  <Table.Th ta="right">Quantidade</Table.Th>
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {os.plagues.map((p, i) => (
                  <Table.Tr key={i}>
                    <Table.Td>{p.plagueName}</Table.Td>
                    <Table.Td>{p.plagueStatus}</Table.Td>
                    <Table.Td>{p.location}</Table.Td>
                    <Table.Td ta="right">{p.quantity}</Table.Td>
                  </Table.Tr>
                ))}
              </Table.Tbody>
            </Table>
          </Table.ScrollContainer>
        </Section>
      )}

      {os.baitings.length > 0 && (
        <Section title="Iscagem">
          <Stack gap="xs">
            <Text size="sm">
              <b>Quantidade de pontos:</b> {installed(os.baitings)}
            </Text>
            {BAIT_TYPES.map((t) => (
              <Group key={t.type} gap="xs" align="flex-start">
                <Text size="sm" w={300}>
                  {t.label}
                </Text>
                <Points items={os.baitings.filter((b) => b.type === t.type)} />
              </Group>
            ))}
          </Stack>
        </Section>
      )}

      {os.plaquesOfGlue.length > 0 && (
        <Section title="Placas de cola / Armadilha mecânica">
          <Stack gap="xs">
            <Text size="sm">
              <b>Quantidade de pontos:</b> {installed(os.plaquesOfGlue)}
            </Text>
            {GLUE_TYPES.map((t) => (
              <Group key={t.type} gap="xs" align="flex-start">
                <Text size="sm" w={300}>
                  {t.label}
                </Text>
                <Points items={os.plaquesOfGlue.filter((b) => b.type === t.type)} />
              </Group>
            ))}
          </Stack>
        </Section>
      )}

      {os.luminous.length > 0 && (
        <Section title="Armadilhas luminosas">
          <Text size="sm" mb="xs">
            <b>Quantidade de pontos:</b> {installed(os.luminous.map((l) => ({ type: l.point ? null : INSTALLED, quantity: l.quantity })))}
          </Text>
          {luminousPoints.length > 0 &&
            (hasInsectCount ? (
              <Table.ScrollContainer minWidth={500}>
                <Table>
                  <Table.Thead>
                    <Table.Tr>
                      <Table.Th>Identificação</Table.Th>
                      {insects.map((c) => (
                        <Table.Th key={c.code} ta="right">
                          {c.label}
                        </Table.Th>
                      ))}
                    </Table.Tr>
                  </Table.Thead>
                  <Table.Tbody>
                    {luminousPoints.map((l, i) => (
                      <Table.Tr key={i}>
                        <Table.Td>{l.point}</Table.Td>
                        {insects.map((c) => (
                          <Table.Td key={c.code} ta="right">
                            {l.plagueCounts.find((p) => p.plagueId === c.code)?.quantity ?? 0}
                          </Table.Td>
                        ))}
                      </Table.Tr>
                    ))}
                  </Table.Tbody>
                </Table>
              </Table.ScrollContainer>
            ) : (
              <Group gap={4}>
                {luminousPoints.map((l, i) => (
                  <Badge key={i} variant="outline" color="gray">
                    {l.point} ({l.quantity})
                  </Badge>
                ))}
              </Group>
            ))}
        </Section>
      )}

      {os.pheromones.length > 0 && (
        <Section title="Armadilhas de feromônio">
          <Stack gap="xs">
            {PHEROMONE_TYPES.map((type) => {
              const items = os.pheromones.filter((p) => p.type === type)
              return (
                <Group key={type} gap="xs">
                  <Text size="sm" w={120} fw={600}>
                    {type}
                  </Text>
                  {items.length === 0 ? (
                    <Text size="sm">0</Text>
                  ) : (
                    items.map((p, i) => (
                      <Badge key={i} variant="outline" color="gray">
                        {p.point}: {p.quantity}
                      </Badge>
                    ))
                  )}
                </Group>
              )
            })}
          </Stack>
        </Section>
      )}

      {os.consumptions.length > 0 && (
        <Section title="Consumo de produtos">
          <Table.ScrollContainer minWidth={900}>
            <Table fz="sm">
              <Table.Thead>
                <Table.Tr>
                  <Table.Th>Praga</Table.Th>
                  <Table.Th>Produto</Table.Th>
                  <Table.Th>Lote</Table.Th>
                  <Table.Th>Validade</Table.Th>
                  <Table.Th ta="right">Quantidade</Table.Th>
                  <Table.Th ta="right">Diluente</Table.Th>
                  <Table.Th ta="right">Calda</Table.Th>
                  <Table.Th ta="right">Concentração</Table.Th>
                  <Table.Th>Equipamento</Table.Th>
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {os.consumptions.map((c, i) => {
                  const d = dilution(c.productName, c.unity, c.quantity, c.concentration)
                  return (
                    <Table.Tr key={i}>
                      <Table.Td>{c.plagueName}</Table.Td>
                      <Table.Td>{c.productName}</Table.Td>
                      <Table.Td>{c.lot}</Table.Td>
                      <Table.Td>{c.expirationDate}</Table.Td>
                      <Table.Td ta="right">
                        {fmtNumber(c.quantity)} {c.unity}
                      </Table.Td>
                      <Table.Td ta="right">{d ? `${fmtNumber(d.diluent)} ml` : ''}</Table.Td>
                      <Table.Td ta="right">{d ? `${fmtNumber(d.syrup)} ml` : ''}</Table.Td>
                      <Table.Td ta="right">{fmtNumber(c.concentration)}</Table.Td>
                      <Table.Td>{c.applicationName}</Table.Td>
                    </Table.Tr>
                  )
                })}
              </Table.Tbody>
            </Table>
          </Table.ScrollContainer>
        </Section>
      )}

      {os.nonConformities.length > 0 && (
        <Section title="Não conformidades">
          <Stack gap="xs">
            {os.nonConformities.map((n) => (
              <Text key={n.id} size="sm">
                <b>{n.place}:</b> {n.description}
                <br />
                <b>Ação sugerida:</b> {n.suggestion}
              </Text>
            ))}
          </Stack>
        </Section>
      )}

      {os.evidenceIds.length > 0 && (
        <Section title="Evidências">
          <SimpleGrid cols={{ base: 2, sm: 3, md: 4 }}>
            {os.evidenceIds.map((e) => (
              <Anchor key={e} href={`/api/orders/${os.id}/evidences/${e}`} target="_blank">
                <Image src={`/api/orders/${os.id}/evidences/${e}`} radius="sm" h={160} fit="cover" alt="Evidência" loading="lazy" />
              </Anchor>
            ))}
          </SimpleGrid>
        </Section>
      )}

      {os.observations && (
        <Section title="Observações">
          <Text size="sm" style={{ whiteSpace: 'pre-wrap' }}>
            {os.observations}
          </Text>
        </Section>
      )}

      {(os.visitDescription || os.treatedAreas) && (
        <Section title="Áreas tratadas">
          <Text size="sm" style={{ whiteSpace: 'pre-wrap' }}>
            {[os.visitDescription, os.treatedAreas].filter(Boolean).join('\n')}
          </Text>
        </Section>
      )}

      {isRdc622 && certificate.data && (
        <Section title="Descrição química dos produtos">
          <Table.ScrollContainer minWidth={800}>
            <Table fz="sm">
              <Table.Thead>
                <Table.Tr>
                  <Table.Th>Produto</Table.Th>
                  <Table.Th>Grupo Químico</Table.Th>
                  <Table.Th>Princípio Ativo</Table.Th>
                  <Table.Th>M.S. e M.A.P.A.</Table.Th>
                  <Table.Th>Praga Alvo</Table.Th>
                  <Table.Th>Sintomas</Table.Th>
                  <Table.Th>Antídotos</Table.Th>
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {certificate.data.products.map((p) => (
                  <Table.Tr key={p.id}>
                    <Table.Td>{p.name}</Table.Td>
                    <Table.Td>{p.chemicalGroup}</Table.Td>
                    <Table.Td>{p.activeIngredient}</Table.Td>
                    <Table.Td>{p.registrationNumber}</Table.Td>
                    <Table.Td>{p.targetPlagues}</Table.Td>
                    <Table.Td>{p.symptoms}</Table.Td>
                    <Table.Td>{p.antidotes}</Table.Td>
                  </Table.Tr>
                ))}
              </Table.Tbody>
            </Table>
          </Table.ScrollContainer>
          <Text size="sm" mt="sm">
            Garantia técnica: <b>{fmtDate(certificate.data.validFrom)}</b> a <b>{fmtDate(certificate.data.validUntil)}</b>
          </Text>
          {certificate.data.technicalManager && (
            <Stack gap={0} mt="sm">
              <Image
                src={`/api/company/technical-managers/${certificate.data.technicalManager.state}/signature`}
                h={60}
                w="auto"
                fit="contain"
                alt="Assinatura do responsável técnico"
                fallbackSrc="data:image/gif;base64,R0lGODlhAQABAAAAACw="
              />
              <Text size="xs" c="dimmed">
                Responsável Técnico
              </Text>
              <Text size="sm" fw={600}>
                {certificate.data.technicalManager.name} - {certificate.data.technicalManager.register}
              </Text>
            </Stack>
          )}
          <Text size="sm" mt="sm">
            Disque Intoxicação 0800-722-6001
          </Text>
        </Section>
      )}

      <Paper p="md" className="avoid-break">
        <Grid>
          {os.hasTechnicianSignature && (
            <Grid.Col span={{ base: 12, sm: 6 }}>
              <Stack gap={2}>
                <Image src={`/api/orders/${os.id}/technician-signature`} h={70} w="auto" fit="contain" alt="Assinatura do técnico" />
                <Text fw={600} size="sm">
                  {os.technicianName}
                </Text>
                <Text size="xs" c="dimmed">
                  Técnico
                </Text>
              </Stack>
            </Grid.Col>
          )}
          <Grid.Col span={{ base: 12, sm: 6 }}>
            <Stack gap={2}>
              {responsible.data?.signature && <Image src={responsible.data.signature} h={70} w="auto" fit="contain" alt="Assinatura do responsável" />}
              <Text fw={600} size="sm">
                {responsible.data?.name ?? os.responsibleName}
              </Text>
              <Text size="xs" c="dimmed">
                {responsible.data?.business ?? os.responsibleBusiness}
              </Text>
            </Stack>
          </Grid.Col>
        </Grid>
      </Paper>
    </Stack>
  )
}
