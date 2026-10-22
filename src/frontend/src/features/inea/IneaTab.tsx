import { useState } from 'react'
import { Button, Divider, FileButton, Group, Image, Paper, Select, Text } from '@mantine/core'
import { IconFileText, IconTrash, IconUpload } from '@tabler/icons-react'
import dayjs from 'dayjs'
import { api } from '../../api/client'
import { notifyError, notifySuccess } from '../../lib/notify'

const MONTHS = ['Janeiro', 'Fevereiro', 'Março', 'Abril', 'Maio', 'Junho', 'Julho', 'Agosto', 'Setembro', 'Outubro', 'Novembro', 'Dezembro']

/** RAAE: Relatório de Atividades Ambientais mensal exigido pelo INEA (RJ). */
export function IneaTab() {
  const previous = dayjs().subtract(1, 'month')
  const [month, setMonth] = useState(String(previous.month() + 1))
  const [year, setYear] = useState(String(previous.year()))
  const [version, setVersion] = useState(0)
  const years = Array.from({ length: dayjs().year() - 2015 + 1 }, (_, i) => String(dayjs().year() - i))

  const upload = async (file: File | null) => {
    if (!file) return
    const form = new FormData()
    form.append('file', file)
    try {
      await api.upload('/api/company/legal-representative/signature', form, 'PUT')
      setVersion((v) => v + 1)
      notifySuccess('Assinatura enviada')
    } catch (e) {
      notifyError(e)
    }
  }

  const remove = async () => {
    try {
      await api.delete('/api/company/legal-representative/signature')
      setVersion((v) => v + 1)
    } catch (e) {
      notifyError(e)
    }
  }

  return (
    <Paper p="md">
      <Text size="sm" c="dimmed" mb="md">
        Gera o RAAE (Relatório de Atividades Ambientais) do mês, com as OS, pragas, produtos e locais atendidos.
      </Text>
      <Group align="flex-end">
        <Select label="Mês" data={MONTHS.map((m, i) => ({ value: String(i + 1), label: m }))} value={month} onChange={(v) => v && setMonth(v)} w={160} />
        <Select label="Ano" data={years} value={year} onChange={(v) => v && setYear(v)} w={110} />
        <Button leftSection={<IconFileText size={16} />} component="a" href={`/inea/raae.html?ano=${year}&mes=${month}`} target="_blank">
          Gerar RAAE
        </Button>
      </Group>

      <Divider my="lg" label="Assinatura do representante legal (impressa no RAAE)" labelPosition="left" />
      <Group>
        <Image
          src={`/api/company/legal-representative/signature?v=${version}`}
          h={50}
          w={180}
          fit="contain"
          bg="gray.0"
          fallbackSrc="data:image/gif;base64,R0lGODlhAQABAAAAACw="
          alt=""
        />
        <FileButton accept="image/png,image/jpeg" onChange={upload}>
          {(props) => (
            <Button {...props} variant="light" leftSection={<IconUpload size={16} />}>
              Enviar assinatura
            </Button>
          )}
        </FileButton>
        <Button variant="subtle" color="red" leftSection={<IconTrash size={16} />} onClick={remove}>
          Remover
        </Button>
      </Group>
    </Paper>
  )
}
