import { createContext, useContext, type ReactNode } from 'react'
import { AppShell, Button, Center, Group, Loader, MantineProvider, NavLink, Text, Title } from '@mantine/core'
import { ModalsProvider } from '@mantine/modals'
import { Notifications } from '@mantine/notifications'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { IconBuildingSkyscraper, IconLogout, IconUsers } from '@tabler/icons-react'
import { createBrowserRouter, Navigate, NavLink as RouterNavLink, Outlet, useNavigate } from 'react-router'
import { RouterProvider } from 'react-router/dom'
import { api, ApiError } from '../api/client'
import { buildTheme } from '../app/theme'
import { PlatformLoginPage } from './PlatformLoginPage'
import { TenantEditPage } from './TenantEditPage'
import { TenantsPage } from './TenantsPage'
import { PlatformUsersPage } from './PlatformUsersPage'

interface PlatformMe {
  id: string
  name: string
  email: string
}

const PlatformAuthContext = createContext<PlatformMe | null>(null)
export const usePlatformMe = () => useContext(PlatformAuthContext)

async function fetchMe(): Promise<PlatformMe | null> {
  try {
    return await api.get<PlatformMe>('/api/platform/auth/me')
  } catch (e) {
    if (e instanceof ApiError && e.status === 401) return null
    throw e
  }
}

function RequirePlatformAuth({ children }: { children: ReactNode }) {
  const me = usePlatformMe()
  return me ? <>{children}</> : <Navigate to="/login" replace />
}

function PlatformLayout() {
  const me = usePlatformMe()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  return (
    <AppShell header={{ height: 56 }} navbar={{ width: 230, breakpoint: 'sm' }} padding="md">
      <AppShell.Header>
        <Group h="100%" px="md" justify="space-between">
          <Title order={4}>EasyOS · Painel</Title>
          <Group gap="xs">
            <Text size="sm" c="dimmed" visibleFrom="xs">
              {me?.email}
            </Text>
            <Button
              variant="subtle"
              size="xs"
              leftSection={<IconLogout size={14} />}
              onClick={async () => {
                await api.post('/api/platform/auth/logout')
                queryClient.setQueryData(['platform-me'], null)
                navigate('/login')
              }}
            >
              Sair
            </Button>
          </Group>
        </Group>
      </AppShell.Header>
      <AppShell.Navbar p="xs">
        <NavLink component={RouterNavLink} to="/clientes" label="Clientes (tenants)" leftSection={<IconBuildingSkyscraper size={18} />} />
        <NavLink component={RouterNavLink} to="/usuarios" label="Usuários do painel" leftSection={<IconUsers size={18} />} />
      </AppShell.Navbar>
      <AppShell.Main>
        <Outlet />
      </AppShell.Main>
    </AppShell>
  )
}

const router = createBrowserRouter([
  { path: '/login', element: <PlatformLoginPage /> },
  {
    element: (
      <RequirePlatformAuth>
        <PlatformLayout />
      </RequirePlatformAuth>
    ),
    children: [
      { path: '/clientes', element: <TenantsPage /> },
      { path: '/clientes/novo', element: <TenantEditPage /> },
      { path: '/clientes/:tenantId', element: <TenantEditPage /> },
      { path: '/usuarios', element: <PlatformUsersPage /> },
    ],
  },
  { path: '*', element: <Navigate to="/clientes" replace /> },
])

/** Painel central da EasyOS: cadastro dos clientes (tenants), domínios, banco e identidade visual. */
export function PlatformApp() {
  const me = useQuery({ queryKey: ['platform-me'], queryFn: fetchMe, retry: false })
  return (
    <MantineProvider theme={buildTheme('#2b6cb0')}>
      <ModalsProvider labels={{ confirm: 'Confirmar', cancel: 'Cancelar' }}>
        <Notifications position="top-right" />
        {me.isPending ? (
          <Center h="100vh">
            <Loader />
          </Center>
        ) : (
          <PlatformAuthContext.Provider value={me.data ?? null}>
            <RouterProvider router={router} />
          </PlatformAuthContext.Provider>
        )}
      </ModalsProvider>
    </MantineProvider>
  )
}
