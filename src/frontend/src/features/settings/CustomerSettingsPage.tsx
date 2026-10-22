import { useEffect } from 'react'
import { Button, Group, Paper } from '@mantine/core'
import { useForm } from '@mantine/form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../../api/client'
import type { Customer } from '../../api/types'
import { useCustomerId } from '../../app/guards'
import { useMe } from '../../auth/AuthContext'
import { Loading, PageHeader } from '../../components/common'
import { notifyError, notifySuccess } from '../../lib/notify'
import { CustomerFields, customerInitialValues, customerValidation, toCustomerRequest, type CustomerFormValues } from '../customers/CustomerForm'
import { useCompany } from '../customers/CustomersPage'

/** Configurações da área do cliente: o próprio cadastro (CNPJ e validade do certificado bloqueados para o cliente). */
export function CustomerSettingsPage() {
  const customerId = useCustomerId()
  const me = useMe()
  const company = useCompany()
  const queryClient = useQueryClient()
  const customer = useQuery({ queryKey: ['customer', customerId], queryFn: () => api.get<Customer>(`/api/customers/${customerId}`) })
  const restricted = me.type === 'customer'

  const form = useForm<CustomerFormValues>({
    initialValues: customerInitialValues(null),
    validate: customerValidation(false, company.data?.state === 'RJ'),
  })

  useEffect(() => {
    if (customer.data) {
      form.setValues(customerInitialValues(customer.data))
      form.resetDirty()
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [customer.data])

  const save = useMutation({
    mutationFn: (v: CustomerFormValues) => api.put<Customer>(`/api/customers/${customerId}`, toCustomerRequest(v)),
    onSuccess: (data) => {
      queryClient.setQueryData(['customer', customerId], data)
      notifySuccess('Editado com sucesso')
    },
    onError: notifyError,
  })

  return (
    <>
      <PageHeader title="Configurações" subtitle="Dados cadastrais" />
      {customer.isPending ? (
        <Loading />
      ) : (
        <Paper p="md">
          <form onSubmit={form.onSubmit((v) => save.mutate(v))}>
            <CustomerFields form={form} isNew={false} restricted={restricted} />
            <Group justify="flex-end" mt="lg">
              <Button type="submit" loading={save.isPending}>
                Salvar
              </Button>
            </Group>
          </form>
        </Paper>
      )}
    </>
  )
}
