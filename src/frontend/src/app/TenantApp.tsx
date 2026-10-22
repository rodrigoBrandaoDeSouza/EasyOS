import { useEffect, useMemo } from 'react'
import { Center, Loader, MantineProvider } from '@mantine/core'
import { DatesProvider } from '@mantine/dates'
import { Notifications } from '@mantine/notifications'
import { ModalsProvider } from '@mantine/modals'
import { useQuery } from '@tanstack/react-query'
import { RouterProvider } from 'react-router/dom'
import { api } from '../api/client'
import type { Branding } from '../api/types'
import { AuthProvider } from '../auth/AuthContext'
import { BrandingContext } from './BrandingContext'
import { tenantRouter } from './routes'
import { buildTheme } from './theme'

export function TenantApp() {
  const branding = useQuery({
    queryKey: ['branding'],
    queryFn: () => api.get<Branding>('/api/tenant/branding'),
    staleTime: Infinity,
  })
  const theme = useMemo(() => buildTheme(branding.data?.primaryColor), [branding.data?.primaryColor])

  useEffect(() => {
    if (!branding.data) return
    document.title = branding.data.name
    const favicon = document.getElementById('favicon') as HTMLLinkElement | null
    if (favicon && branding.data.faviconUrl) favicon.href = branding.data.faviconUrl
  }, [branding.data])

  if (branding.isPending) {
    return (
      <MantineProvider theme={theme}>
        <Center h="100vh">
          <Loader />
        </Center>
      </MantineProvider>
    )
  }

  return (
    <MantineProvider theme={theme}>
      <DatesProvider settings={{ locale: 'pt-br', firstDayOfWeek: 0 }}>
        <ModalsProvider labels={{ confirm: 'Confirmar', cancel: 'Cancelar' }}>
          <Notifications position="top-right" />
          <BrandingContext.Provider value={branding.data ?? null}>
            <AuthProvider>
              <RouterProvider router={tenantRouter} />
            </AuthProvider>
          </BrandingContext.Provider>
        </ModalsProvider>
      </DatesProvider>
    </MantineProvider>
  )
}
