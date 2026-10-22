import { Checkbox, Divider, Grid, MaskInput, NumberInput, PasswordInput, Select, Textarea, TextInput } from '@mantine/core'
import type { UseFormReturnType } from '@mantine/form'
import type { Customer } from '../../api/types'
import { CUSTOMER_PROFILES, UF_OPTIONS } from '../../lib/format'

export interface CustomerFormValues {
  tradingName: string
  companyName: string
  cpfCnpj: string
  address: string
  number: number | string
  complement: string
  neighborhood: string
  city: string
  state: string
  zipcode: string
  phone: string
  contactName: string
  contactFunction: string
  fax: string
  email: string
  activityType: string
  clientHasContract: boolean
  certificateDate: number | string
  observation: string
  // Primeiro usuário (somente no cadastro).
  userName: string
  userEmail: string
  userDepartment: string
  userProfile: string
  userPassword: string
}

export function customerInitialValues(c?: Customer | null): CustomerFormValues {
  return {
    tradingName: c?.tradingName ?? '',
    companyName: c?.companyName ?? '',
    cpfCnpj: c?.cpfCnpj ?? '',
    address: c?.address ?? '',
    number: c?.number ?? '',
    complement: c?.complement ?? '',
    neighborhood: c?.neighborhood ?? '',
    city: c?.city ?? '',
    state: c?.state ?? 'SP',
    zipcode: c?.zipcode ?? '',
    phone: c?.phone ?? '',
    contactName: c?.contactName ?? '',
    contactFunction: c?.contactFunction ?? '',
    fax: c?.fax ?? '',
    email: c?.email ?? '',
    activityType: c?.activityType ?? '',
    clientHasContract: c?.clientHasContract ?? false,
    certificateDate: c?.certificateDate ?? 30,
    observation: c?.observation ?? '',
    userName: '',
    userEmail: '',
    userDepartment: '',
    userProfile: '1',
    userPassword: '',
  }
}

/**
 * Validações do cadastro de cliente. Para empresas do RJ, os campos exigidos pelos documentos
 * do INEA (tipo de atividade, função do contato e e-mail) passam a ser obrigatórios, como no legado.
 */
export function customerValidation(isNew: boolean, companyIsRj: boolean) {
  const required = (label: string) => (v: unknown) => (String(v ?? '').trim() ? null : `Informe ${label}`)
  return {
    tradingName: required('o nome fantasia'),
    companyName: required('a razão social'),
    cpfCnpj: required('o CNPJ/CPF'),
    address: required('o logradouro'),
    number: required('o número'),
    neighborhood: required('o bairro'),
    city: required('a cidade'),
    state: required('o estado'),
    zipcode: required('o CEP'),
    phone: required('o telefone'),
    contactName: required('o contato'),
    activityType: companyIsRj ? required('o tipo de atividade') : undefined,
    contactFunction: companyIsRj ? required('a função do contato') : undefined,
    email: companyIsRj ? required('o e-mail') : undefined,
    userName: isNew ? required('o nome do usuário') : undefined,
    userEmail: isNew ? required('o e-mail do usuário') : undefined,
    userPassword: isNew ? (v: string) => (v.length >= 6 ? null : 'A senha deve ter ao menos 6 caracteres') : undefined,
  }
}

export function toCustomerRequest(v: CustomerFormValues) {
  return {
    tradingName: v.tradingName,
    companyName: v.companyName,
    cpfCnpj: v.cpfCnpj,
    address: v.address,
    number: Number(v.number) || 0,
    complement: v.complement,
    neighborhood: v.neighborhood,
    city: v.city,
    state: v.state,
    zipcode: v.zipcode,
    phone: v.phone,
    contactName: v.contactName,
    contactFunction: v.contactFunction,
    fax: v.fax,
    email: v.email,
    activityType: v.activityType,
    clientHasContract: v.clientHasContract,
    certificateDate: Number(v.certificateDate) || 0,
    observation: v.observation,
  }
}

export function CustomerFields({
  form,
  isNew,
  restricted = false,
}: {
  form: UseFormReturnType<CustomerFormValues>
  isNew: boolean
  /** Usuário do cliente editando o próprio cadastro: CNPJ e validade do certificado ficam bloqueados. */
  restricted?: boolean
}) {
  return (
    <Grid gap="sm">
      <Grid.Col span={{ base: 12, sm: 6 }}>
        <TextInput label="Razão Social" withAsterisk {...form.getInputProps('companyName')} />
      </Grid.Col>
      <Grid.Col span={{ base: 12, sm: 6 }}>
        <TextInput label="Nome Fantasia" withAsterisk {...form.getInputProps('tradingName')} />
      </Grid.Col>
      <Grid.Col span={{ base: 12, sm: 4 }}>
        <TextInput label="CNPJ/CPF" withAsterisk disabled={restricted} {...form.getInputProps('cpfCnpj')} />
      </Grid.Col>
      <Grid.Col span={{ base: 6, sm: 4 }}>
        <NumberInput
          label="Validade do Certificado (dias)"
          min={0}
          max={999}
          disabled={restricted}
          {...form.getInputProps('certificateDate')}
        />
      </Grid.Col>
      <Grid.Col span={{ base: 6, sm: 4 }}>
        <TextInput label="Tipo de Atividade" {...form.getInputProps('activityType')} />
      </Grid.Col>
      {!restricted && (
        <Grid.Col span={12}>
          <Checkbox label="Cliente tem contrato" {...form.getInputProps('clientHasContract', { type: 'checkbox' })} />
        </Grid.Col>
      )}

      <Grid.Col span={12}>
        <Divider label="Endereço e contato" labelPosition="left" />
      </Grid.Col>
      <Grid.Col span={{ base: 12, sm: 8 }}>
        <TextInput label="Logradouro" withAsterisk {...form.getInputProps('address')} />
      </Grid.Col>
      <Grid.Col span={{ base: 4, sm: 4 }}>
        <TextInput label="Número" withAsterisk {...form.getInputProps('number')} />
      </Grid.Col>
      <Grid.Col span={{ base: 8, sm: 4 }}>
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
        <Select label="Estado" data={UF_OPTIONS} searchable withAsterisk {...form.getInputProps('state')} />
      </Grid.Col>
      <Grid.Col span={{ base: 12, sm: 6 }}>
        <TextInput label="Contato" withAsterisk {...form.getInputProps('contactName')} />
      </Grid.Col>
      <Grid.Col span={{ base: 12, sm: 6 }}>
        <TextInput label="Função do contato" {...form.getInputProps('contactFunction')} />
      </Grid.Col>
      <Grid.Col span={{ base: 12, sm: 4 }}>
        <TextInput label="Telefone" withAsterisk {...form.getInputProps('phone')} />
      </Grid.Col>
      <Grid.Col span={{ base: 12, sm: 4 }}>
        <TextInput label="Fax" {...form.getInputProps('fax')} />
      </Grid.Col>
      <Grid.Col span={{ base: 12, sm: 4 }}>
        <TextInput label="E-mail" type="email" {...form.getInputProps('email')} />
      </Grid.Col>
      <Grid.Col span={12}>
        <Textarea label="Observações" autosize minRows={2} maxLength={1000} {...form.getInputProps('observation')} />
      </Grid.Col>

      {isNew && (
        <>
          <Grid.Col span={12}>
            <Divider label="Primeiro usuário do cliente" labelPosition="left" />
          </Grid.Col>
          <Grid.Col span={{ base: 12, sm: 6 }}>
            <TextInput label="Nome" withAsterisk {...form.getInputProps('userName')} />
          </Grid.Col>
          <Grid.Col span={{ base: 12, sm: 6 }}>
            <TextInput label="E-mail" type="email" withAsterisk {...form.getInputProps('userEmail')} />
          </Grid.Col>
          <Grid.Col span={{ base: 12, sm: 4 }}>
            <TextInput label="Departamento" {...form.getInputProps('userDepartment')} />
          </Grid.Col>
          <Grid.Col span={{ base: 12, sm: 4 }}>
            <Select label="Perfil" data={CUSTOMER_PROFILES} allowDeselect={false} {...form.getInputProps('userProfile')} />
          </Grid.Col>
          <Grid.Col span={{ base: 12, sm: 4 }}>
            <PasswordInput label="Senha" withAsterisk autoComplete="new-password" {...form.getInputProps('userPassword')} />
          </Grid.Col>
        </>
      )}
    </Grid>
  )
}
