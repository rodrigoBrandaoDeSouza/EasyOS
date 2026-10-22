import { useEffect } from 'react'
import { Button, Checkbox, Divider, Grid, Group, MaskInput, NumberInput, Paper, Select, TextInput } from '@mantine/core'
import { DateInput } from '@mantine/dates'
import { useForm } from '@mantine/form'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import dayjs from 'dayjs'
import { api } from '../../api/client'
import type { Company } from '../../api/types'
import { Loading } from '../../components/common'
import { toApiDate, UF_OPTIONS } from '../../lib/format'
import { notifyError, notifySuccess } from '../../lib/notify'
import { useCompany } from '../customers/CustomersPage'

export function CompanyDataTab() {
  const company = useCompany()
  const queryClient = useQueryClient()
  const form = useForm({
    initialValues: {
      tradingName: '', companyName: '', cpfCnpj: '', address: '', number: 0 as number | string, complement: '', neighborhood: '',
      city: '', state: 'SP', zipcode: '', phone: '', fax: '', email: '', contactName: '', charter: '',
      charterExpiration: null as string | null, ineaCode: '', environmentalPermit: '', environmentalPermitExpiration: null as string | null,
      legalRepresentative: '', useBaitings: false, usePlaqueOfGlue: false, useLuminousTrap: false, usePheromoneTrap: false,
    },
    validate: {
      tradingName: (v) => (v.trim() ? null : 'Obrigatório'),
      companyName: (v) => (v.trim() ? null : 'Obrigatório'),
      address: (v) => (v.trim() ? null : 'Obrigatório'),
      neighborhood: (v) => (v.trim() ? null : 'Obrigatório'),
      city: (v) => (v.trim() ? null : 'Obrigatório'),
      zipcode: (v) => (v.trim() ? null : 'Obrigatório'),
      phone: (v) => (v.trim() ? null : 'Obrigatório'),
      contactName: (v) => (v.trim() ? null : 'Obrigatório'),
      charter: (v) => (v.trim() ? null : 'Obrigatório'),
      charterExpiration: (v) => (v ? null : 'Obrigatório'),
    },
  })

  useEffect(() => {
    const c = company.data
    if (!c) return
    form.setValues({
      ...c,
      cpfCnpj: c.cpfCnpj ?? '',
      fax: c.fax ?? '',
      email: c.email ?? '',
      ineaCode: c.ineaCode ?? '',
      environmentalPermit: c.environmentalPermit ?? '',
      legalRepresentative: c.legalRepresentative ?? '',
      charterExpiration: c.charterExpiration ? dayjs(c.charterExpiration).format('YYYY-MM-DD') : null,
      environmentalPermitExpiration: c.environmentalPermitExpiration ? dayjs(c.environmentalPermitExpiration).format('YYYY-MM-DD') : null,
    })
    form.resetDirty()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [company.data])

  const save = useMutation({
    mutationFn: (v: typeof form.values) =>
      api.put<Company>('/api/company', {
        ...v,
        number: Number(v.number) || 0,
        charterExpiration: toApiDate(v.charterExpiration),
        environmentalPermitExpiration: toApiDate(v.environmentalPermitExpiration) ?? null,
      }),
    onSuccess: (data) => {
      queryClient.setQueryData(['company'], data)
      notifySuccess('Editado com sucesso')
    },
    onError: notifyError,
  })

  if (company.isPending) return <Loading />
  const isRj = form.values.state === 'RJ'

  return (
    <Paper p="md">
      <form onSubmit={form.onSubmit((v) => save.mutate(v))}>
        <Grid gap="sm">
          <Grid.Col span={{ base: 12, sm: 6 }}>
            <TextInput label="Razão Social" withAsterisk {...form.getInputProps('companyName')} />
          </Grid.Col>
          <Grid.Col span={{ base: 12, sm: 6 }}>
            <TextInput label="Nome Fantasia" withAsterisk {...form.getInputProps('tradingName')} />
          </Grid.Col>
          <Grid.Col span={{ base: 12, sm: 4 }}>
            <TextInput label="CNPJ/CPF" {...form.getInputProps('cpfCnpj')} />
          </Grid.Col>
          <Grid.Col span={{ base: 12, sm: 4 }}>
            <TextInput label="Licença de funcionamento (alvará)" withAsterisk {...form.getInputProps('charter')} />
          </Grid.Col>
          <Grid.Col span={{ base: 12, sm: 4 }}>
            <DateInput label="Validade da licença" valueFormat="DD/MM/YYYY" withAsterisk {...form.getInputProps('charterExpiration')} />
          </Grid.Col>
          <Grid.Col span={12}>
            <Divider label="Endereço e contato" labelPosition="left" />
          </Grid.Col>
          <Grid.Col span={{ base: 12, sm: 8 }}>
            <TextInput label="Endereço" withAsterisk {...form.getInputProps('address')} />
          </Grid.Col>
          <Grid.Col span={{ base: 6, sm: 4 }}>
            <NumberInput label="Número" min={0} {...form.getInputProps('number')} />
          </Grid.Col>
          <Grid.Col span={{ base: 6, sm: 4 }}>
            <TextInput label="Complemento" {...form.getInputProps('complement')} />
          </Grid.Col>
          <Grid.Col span={{ base: 12, sm: 4 }}>
            <TextInput label="Bairro" withAsterisk {...form.getInputProps('neighborhood')} />
          </Grid.Col>
          <Grid.Col span={{ base: 12, sm: 4 }}>
            <MaskInput label="CEP" mask="99999-999" withAsterisk {...form.getInputProps('zipcode')} />
          </Grid.Col>
          <Grid.Col span={{ base: 12, sm: 8 }}>
            <TextInput label="Cidade" withAsterisk {...form.getInputProps('city')} />
          </Grid.Col>
          <Grid.Col span={{ base: 12, sm: 4 }}>
            <Select label="Estado" data={UF_OPTIONS} searchable {...form.getInputProps('state')} />
          </Grid.Col>
          <Grid.Col span={{ base: 12, sm: 4 }}>
            <TextInput label="Nome do contato" withAsterisk {...form.getInputProps('contactName')} />
          </Grid.Col>
          <Grid.Col span={{ base: 12, sm: 4 }}>
            <TextInput label="Telefone" withAsterisk {...form.getInputProps('phone')} />
          </Grid.Col>
          <Grid.Col span={{ base: 12, sm: 4 }}>
            <TextInput label="Fax" {...form.getInputProps('fax')} />
          </Grid.Col>
          <Grid.Col span={{ base: 12, sm: 8 }}>
            <TextInput label="E-mail" type="email" {...form.getInputProps('email')} />
          </Grid.Col>
          {isRj && (
            <>
              <Grid.Col span={12}>
                <Divider label="INEA (RJ)" labelPosition="left" />
              </Grid.Col>
              <Grid.Col span={{ base: 12, sm: 4 }}>
                <TextInput label="Código INEA" {...form.getInputProps('ineaCode')} />
              </Grid.Col>
              <Grid.Col span={{ base: 12, sm: 4 }}>
                <TextInput label="Licença ambiental" {...form.getInputProps('environmentalPermit')} />
              </Grid.Col>
              <Grid.Col span={{ base: 12, sm: 4 }}>
                <DateInput label="Validade da licença ambiental" valueFormat="DD/MM/YYYY" clearable {...form.getInputProps('environmentalPermitExpiration')} />
              </Grid.Col>
              <Grid.Col span={{ base: 12, sm: 8 }}>
                <TextInput label="Representante legal" {...form.getInputProps('legalRepresentative')} />
              </Grid.Col>
            </>
          )}
          <Grid.Col span={12}>
            <Divider label="Equipamentos utilizados" labelPosition="left" />
          </Grid.Col>
          <Grid.Col span={12}>
            <Group>
              <Checkbox label="Iscagem" {...form.getInputProps('useBaitings', { type: 'checkbox' })} />
              <Checkbox label="Placa de cola" {...form.getInputProps('usePlaqueOfGlue', { type: 'checkbox' })} />
              <Checkbox label="Armadilha luminosa" {...form.getInputProps('useLuminousTrap', { type: 'checkbox' })} />
              <Checkbox label="Armadilha de feromônio" {...form.getInputProps('usePheromoneTrap', { type: 'checkbox' })} />
            </Group>
          </Grid.Col>
        </Grid>
        <Group justify="flex-end" mt="lg">
          <Button type="submit" loading={save.isPending}>
            Salvar
          </Button>
        </Group>
      </form>
    </Paper>
  )
}
