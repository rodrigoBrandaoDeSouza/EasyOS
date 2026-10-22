import { Button, Group, Paper, Stack, Text } from '@mantine/core'
import { useQuery } from '@tanstack/react-query'
import { IconPrinter } from '@tabler/icons-react'
import { Link } from 'react-router'
import { api } from '../../api/client'
import type { CertificateListItem } from '../../api/types'
import { useCustomerId } from '../../app/guards'
import { Empty, Loading, PageHeader } from '../../components/common'
import { fmtDate } from '../../lib/format'

export function CertificatesPage() {
  const customerId = useCustomerId()
  const certificates = useQuery({
    queryKey: ['certificates', customerId],
    queryFn: () => api.get<CertificateListItem[]>(`/api/customers/${customerId}/certificates`),
  })

  return (
    <>
      <PageHeader title="Certificados" subtitle="Certificados de execução de serviço das OS realizadas" />
      {certificates.isPending ? (
        <Loading />
      ) : !certificates.data?.length ? (
        <Empty>Nenhum certificado disponível.</Empty>
      ) : (
        <Stack gap="xs">
          {certificates.data.map((c) => (
            <Paper key={c.orderId} p="sm">
              <Group justify="space-between">
                <Stack gap={0}>
                  <Text size="sm" c="dimmed">
                    Ordem nº {c.sequence}
                  </Text>
                  <Text fw={600}>
                    {fmtDate(c.startDate)} a {fmtDate(c.endDate)}
                  </Text>
                </Stack>
                <Button variant="light" leftSection={<IconPrinter size={16} />} component={Link} to={`/imprimir/certificado/${c.orderId}`} target="_blank">
                  Imprimir
                </Button>
              </Group>
            </Paper>
          ))}
        </Stack>
      )}
    </>
  )
}
