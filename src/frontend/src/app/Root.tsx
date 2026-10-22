import { lazy, Suspense } from 'react'
import { Center, Loader, MantineProvider, Text } from '@mantine/core'
import { useQuery } from '@tanstack/react-query'
import { api } from '../api/client'
import { TenantApp } from './TenantApp'
import { buildTheme } from './theme'

// O painel EasyOS só é baixado no domínio do painel.
const PlatformApp = lazy(async () => ({ default: (await import('../platform/PlatformApp')).PlatformApp }))

/** O mesmo frontend atende os domínios dos clientes e o domínio do painel EasyOS. */
export function Root() {
  const context = useQuery({
    queryKey: ['context'],
    queryFn: () => api.get<{ kind: 'tenant' | 'platform' }>('/api/context'),
    staleTime: Infinity,
  })

  if (context.isPending) {
    return (
      <MantineProvider theme={buildTheme()}>
        <Center h="100vh">
          <Loader />
        </Center>
      </MantineProvider>
    )
  }

  if (context.isError) {
    return (
      <MantineProvider theme={buildTheme()}>
        <Center h="100vh">
          <Text c="dimmed">Não foi possível acessar o servidor. Tente novamente em instantes.</Text>
        </Center>
      </MantineProvider>
    )
  }

  return context.data.kind === 'platform' ? (
    <Suspense fallback={null}>
      <PlatformApp />
    </Suspense>
  ) : (
    <TenantApp />
  )
}
