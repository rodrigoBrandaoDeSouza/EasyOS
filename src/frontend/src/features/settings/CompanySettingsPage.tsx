import { Tabs } from '@mantine/core'
import { IconBug, IconBuilding, IconFileCertificate, IconFlask, IconUserShield } from '@tabler/icons-react'
import { useSearchParams } from 'react-router'
import { PageHeader } from '../../components/common'
import { useCompany } from '../customers/CustomersPage'
import { CompanyDataTab } from './CompanyDataTab'
import { PlaguesTab } from './PlaguesTab'
import { ProductsTab } from './ProductsTab'
import { TechnicalManagersTab } from './TechnicalManagersTab'
import { IneaTab } from '../inea/IneaTab'

export function CompanySettingsPage() {
  const [params, setParams] = useSearchParams()
  const company = useCompany()
  const tab = params.get('aba') ?? 'empresa'

  return (
    <>
      <PageHeader title="Configurações" />
      <Tabs value={tab} onChange={(v) => setParams(v ? { aba: v } : {})} keepMounted={false}>
        <Tabs.List mb="md">
          <Tabs.Tab value="empresa" leftSection={<IconBuilding size={16} />}>
            Empresa
          </Tabs.Tab>
          <Tabs.Tab value="responsaveis" leftSection={<IconUserShield size={16} />}>
            Responsáveis técnicos
          </Tabs.Tab>
          <Tabs.Tab value="pragas" leftSection={<IconBug size={16} />}>
            Pragas
          </Tabs.Tab>
          <Tabs.Tab value="produtos" leftSection={<IconFlask size={16} />}>
            Produtos
          </Tabs.Tab>
          {company.data?.state === 'RJ' && (
            <Tabs.Tab value="inea" leftSection={<IconFileCertificate size={16} />}>
              INEA (RAAE)
            </Tabs.Tab>
          )}
        </Tabs.List>
        <Tabs.Panel value="empresa">
          <CompanyDataTab />
        </Tabs.Panel>
        <Tabs.Panel value="responsaveis">
          <TechnicalManagersTab />
        </Tabs.Panel>
        <Tabs.Panel value="pragas">
          <PlaguesTab />
        </Tabs.Panel>
        <Tabs.Panel value="produtos">
          <ProductsTab />
        </Tabs.Panel>
        <Tabs.Panel value="inea">
          <IneaTab />
        </Tabs.Panel>
      </Tabs>
    </>
  )
}
