import { useEffect } from 'react'
import { Box, Button, Center, Divider, Group, Image, Loader, Stack, Table, Text, Title } from '@mantine/core'
import { useQuery } from '@tanstack/react-query'
import { IconPrinter } from '@tabler/icons-react'
import { useParams } from 'react-router'
import { api } from '../../api/client'
import type { Certificate } from '../../api/types'
import { useBranding } from '../../app/BrandingContext'
import { fmtDate } from '../../lib/format'

function formatCnpj(value: string | null) {
  const digits = (value ?? '').replace(/\D/g, '')
  return digits.length === 14 ? digits.replace(/(\d{2})(\d{3})(\d{3})(\d{4})(\d{2})/, '$1.$2.$3/$4-$5') : value ?? ''
}

/** Certificado de execução de serviço, no mesmo conteúdo do certificate.html do legado, pronto para imprimir. */
export function CertificatePrintPage() {
  const { orderId } = useParams()
  const branding = useBranding()
  const certificate = useQuery({
    queryKey: ['order', Number(orderId), 'certificate'],
    queryFn: () => api.get<Certificate>(`/api/orders/${orderId}/certificate`),
  })

  useEffect(() => {
    if (certificate.data) document.title = `Certificado OS ${certificate.data.sequence}`
  }, [certificate.data])

  if (certificate.isPending) {
    return (
      <Center h="100vh">
        <Loader />
      </Center>
    )
  }
  if (certificate.isError) return <Center h="100vh">{certificate.error.message}</Center>

  const c = certificate.data
  const company = c.company
  const customer = c.customer

  return (
    <Box maw={900} mx="auto" p="xl" bg="white">
      <Group justify="flex-end" className="no-print" mb="md">
        <Button leftSection={<IconPrinter size={16} />} onClick={() => window.print()}>
          Imprimir / Salvar PDF
        </Button>
      </Group>

      <Stack gap="md">
        <Group justify="space-between" align="center">
          {branding.logoUrl ? <Image src={branding.logoUrl} h={70} w="auto" fit="contain" alt={branding.name} /> : <Title order={3}>{branding.name}</Title>}
          <Stack gap={0} ta="right">
            <Title order={3}>CERTIFICADO</Title>
            <Text size="sm">
              OS nº <b>{c.sequence}</b>
            </Text>
          </Stack>
        </Group>
        <Divider />

        <Stack gap={2}>
          <Text fw={700}>{company.companyName}</Text>
          <Text size="sm">
            {company.address}, {company.number} / {company.neighborhood} - CEP: {company.zipcode} {company.city} - {company.state}
          </Text>
          <Text size="sm">
            Telefone: {company.phone} · CNPJ: {formatCnpj(company.cpfCnpj)}
          </Text>
          <Text size="sm">
            Licença de funcionamento: <b>{company.charter}</b> · validade {fmtDate(company.charterExpiration)}
          </Text>
        </Stack>

        <Text>
          Certificamos que foram executados os serviços de controle de pragas urbanas nas dependências de:
        </Text>
        <Stack gap={2} p="sm" style={{ border: '1px solid var(--mantine-color-gray-4)', borderRadius: 6 }}>
          <Text fw={700}>{customer.companyName}</Text>
          <Text size="sm">
            {customer.address}, {customer.number} / {customer.neighborhood} - CEP: {customer.zipcode} {customer.city} - {customer.state}
          </Text>
          <Text size="sm">CNPJ/CPF: {customer.cpfCnpj}</Text>
        </Stack>

        <Text>
          Período de garantia: <b>{fmtDate(c.validFrom)}</b> a <b>{fmtDate(c.validUntil)}</b> ({c.validityDays} dias).
        </Text>

        {c.products.length > 0 && (
          <Table withTableBorder withColumnBorders fz="xs" striped={false}>
            <Table.Thead>
              <Table.Tr>
                <Table.Th>Produto</Table.Th>
                <Table.Th>Grupo químico</Table.Th>
                <Table.Th>Princípio ativo</Table.Th>
                <Table.Th>Registro M.S.</Table.Th>
                <Table.Th>Pragas alvo</Table.Th>
                <Table.Th>Sintomas</Table.Th>
                <Table.Th>Antídotos</Table.Th>
              </Table.Tr>
            </Table.Thead>
            <Table.Tbody>
              {c.products.map((p) => (
                <Table.Tr key={p.id}>
                  <Table.Td style={{ wordBreak: 'break-word' }}>{p.name}</Table.Td>
                  <Table.Td style={{ wordBreak: 'break-word' }}>{p.chemicalGroup}</Table.Td>
                  <Table.Td style={{ wordBreak: 'break-word' }}>{p.activeIngredient}</Table.Td>
                  <Table.Td style={{ wordBreak: 'break-word' }}>{p.registrationNumber}</Table.Td>
                  <Table.Td style={{ wordBreak: 'break-word' }}>{p.targetPlagues}</Table.Td>
                  <Table.Td style={{ wordBreak: 'break-word' }}>{p.symptoms}</Table.Td>
                  <Table.Td style={{ wordBreak: 'break-word' }}>{p.antidotes}</Table.Td>
                </Table.Tr>
              ))}
            </Table.Tbody>
          </Table>
        )}

        <Text size="sm">Em caso de intoxicação, ligue para o Disque Intoxicação: 0800-722-6001.</Text>

        {c.technicalManager && (
          <Stack gap={0} align="center" mt="xl">
            <Image
              src={`/api/company/technical-managers/${c.technicalManager.state}/signature`}
              h={70}
              w="auto"
              fit="contain"
              alt="Assinatura do responsável técnico"
              fallbackSrc="data:image/gif;base64,R0lGODlhAQABAAAAACw="
            />
            <Divider w={280} />
            <Text fw={700} size="sm">
              {c.technicalManager.name}
            </Text>
            <Text size="sm">{c.technicalManager.register}</Text>
            <Text size="xs" c="dimmed">
              Responsável Técnico
            </Text>
          </Stack>
        )}
      </Stack>
    </Box>
  )
}
