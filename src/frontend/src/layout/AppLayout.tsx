import { useState } from 'react'
import { ActionIcon, Alert, AppShell, Avatar, Burger, Group, Image, Menu, NavLink, ScrollArea, Text, Title, UnstyledButton } from '@mantine/core'
import { useDisclosure } from '@mantine/hooks'
import { useQuery } from '@tanstack/react-query'
import {
  IconArrowLeft, IconBuildingStore, IconCalendarEvent, IconCertificate, IconChartBar, IconClipboardList,
  IconFiles, IconGauge, IconKey, IconLogout, IconMessage2, IconReportAnalytics, IconSettings, IconUsers,
} from '@tabler/icons-react'
import { NavLink as RouterNavLink, Outlet, useLocation, useNavigate, useParams } from 'react-router'
import { api } from '../api/client'
import type { Customer } from '../api/types'
import { useBranding } from '../app/BrandingContext'
import { canManageCustomer, isCompanyAdmin, useAuth, useMe } from '../auth/AuthContext'
import { ChangePasswordModal } from '../features/auth/ChangePasswordModal'
import { CUSTOMER_REPORTS } from '../features/reports/reportCatalog'

interface Item {
  label: string
  to: string
  icon: typeof IconGauge
  children?: { label: string; to: string }[]
}

function companyItems(admin: boolean): Item[] {
  return [
    { label: 'Dashboard', to: '/painel', icon: IconGauge },
    { label: 'Clientes', to: '/clientes', icon: IconBuildingStore },
    ...(admin ? [{ label: 'Usuários', to: '/usuarios', icon: IconUsers }] : []),
    { label: 'Visitas', to: '/visitas', icon: IconCalendarEvent },
    { label: 'Ordens de Serviço', to: '/os', icon: IconClipboardList },
    { label: 'Relatório Produtividade', to: '/produtividade', icon: IconChartBar },
    { label: 'Arquivos', to: '/arquivos', icon: IconFiles },
    ...(admin ? [{ label: 'Configurações', to: '/configuracoes', icon: IconSettings }] : []),
  ]
}

function customerItems(base: string, manage: boolean): Item[] {
  return [
    { label: 'Dashboard', to: `${base}/painel`, icon: IconGauge },
    ...(manage ? [{ label: 'Usuários', to: `${base}/usuarios`, icon: IconUsers }] : []),
    { label: 'Cronograma de Visitas', to: `${base}/visitas`, icon: IconCalendarEvent },
    { label: 'Ordens de Serviço', to: `${base}/os`, icon: IconClipboardList },
    { label: 'Downloads', to: `${base}/arquivos`, icon: IconFiles },
    {
      label: 'Relatórios',
      to: `${base}/relatorios`,
      icon: IconReportAnalytics,
      children: CUSTOMER_REPORTS.map((r) => ({ label: r.menu, to: `${base}/relatorios/${r.type}` })),
    },
    { label: 'Certificados', to: `${base}/certificados`, icon: IconCertificate },
    { label: 'Comentários Mensais', to: `${base}/comentarios`, icon: IconMessage2 },
    ...(manage ? [{ label: 'Configurações', to: `${base}/configuracoes`, icon: IconSettings }] : []),
  ]
}

export function AppLayout({ area }: { area: 'company' | 'customer' }) {
  const [opened, { toggle, close }] = useDisclosure()
  const [passwordOpen, setPasswordOpen] = useState(false)
  const me = useMe()
  const { logout } = useAuth()
  const branding = useBranding()
  const navigate = useNavigate()
  const location = useLocation()
  const { customerId } = useParams()

  // Usuário da empresa "acessando como" um cliente: mostra qual cliente está sendo visualizado.
  const viewingCustomer = useQuery({
    queryKey: ['customer', Number(customerId)],
    queryFn: () => api.get<Customer>(`/api/customers/${customerId}`),
    enabled: area === 'customer' && me.type === 'company' && !!customerId,
  })

  const items = area === 'company' ? companyItems(isCompanyAdmin(me)) : customerItems(`/c/${customerId}`, canManageCustomer(me))

  return (
    <AppShell
      header={{ height: 60 }}
      navbar={{ width: 270, breakpoint: 'sm', collapsed: { mobile: !opened } }}
      padding="md"
    >
      <AppShell.Header className="no-print">
        <Group h="100%" px="md" justify="space-between" wrap="nowrap">
          <Group gap="sm" wrap="nowrap">
            <Burger opened={opened} onClick={toggle} hiddenFrom="sm" size="sm" aria-label="Abrir menu" />
            {branding.logoUrl ? (
              <Image src={branding.logoUrl} alt={branding.name} h={38} w="auto" fit="contain" />
            ) : (
              <Title order={4} c="brand.7">
                {branding.name}
              </Title>
            )}
          </Group>
          <Menu position="bottom-end" width={220}>
            <Menu.Target>
              <UnstyledButton aria-label="Menu do usuário">
                <Group gap="xs" wrap="nowrap">
                  <Avatar color="brand" radius="xl" size="sm">
                    {me.name.slice(0, 1).toUpperCase()}
                  </Avatar>
                  <Text size="sm" fw={500} visibleFrom="xs" lineClamp={1} maw={180}>
                    {me.name}
                  </Text>
                </Group>
              </UnstyledButton>
            </Menu.Target>
            <Menu.Dropdown>
              <Menu.Label>{me.email}</Menu.Label>
              <Menu.Item leftSection={<IconKey size={16} />} onClick={() => setPasswordOpen(true)}>
                Alterar senha
              </Menu.Item>
              <Menu.Item
                color="red"
                leftSection={<IconLogout size={16} />}
                onClick={async () => {
                  await logout()
                  navigate('/login', { replace: true })
                }}
              >
                Sair
              </Menu.Item>
            </Menu.Dropdown>
          </Menu>
        </Group>
      </AppShell.Header>

      <AppShell.Navbar p="xs" className="no-print">
        <ScrollArea>
          {area === 'customer' && me.type === 'company' && (
            <Alert variant="light" color="brand" mb="xs" p="xs">
              <Text size="xs" c="dimmed">
                Visualizando como cliente
              </Text>
              <Text size="sm" fw={600} lineClamp={2}>
                {viewingCustomer.data?.tradingName ?? '…'}
              </Text>
              <Group gap={4} mt={4}>
                <ActionIcon variant="subtle" size="sm" onClick={() => navigate('/clientes')} aria-label="Voltar para clientes">
                  <IconArrowLeft size={14} />
                </ActionIcon>
                <Text size="xs" c="brand.7" style={{ cursor: 'pointer' }} onClick={() => navigate('/clientes')}>
                  Voltar para clientes
                </Text>
              </Group>
            </Alert>
          )}
          {items.map((item) =>
            item.children ? (
              <NavLink
                key={item.to}
                label={item.label}
                leftSection={<item.icon size={18} stroke={1.6} />}
                defaultOpened={location.pathname.startsWith(item.to)}
                childrenOffset={28}
              >
                {item.children.map((child) => (
                  <NavLink key={child.to} component={RouterNavLink} to={child.to} label={child.label} onClick={close} />
                ))}
              </NavLink>
            ) : (
              <NavLink
                key={item.to}
                component={RouterNavLink}
                to={item.to}
                label={item.label}
                leftSection={<item.icon size={18} stroke={1.6} />}
                onClick={close}
              />
            ),
          )}
        </ScrollArea>
      </AppShell.Navbar>

      <AppShell.Main>
        <Outlet />
      </AppShell.Main>

      <ChangePasswordModal opened={passwordOpen} onClose={() => setPasswordOpen(false)} />
    </AppShell>
  )
}
