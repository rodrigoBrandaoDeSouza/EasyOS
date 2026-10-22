import type { ReactNode } from 'react'
import { Center, Loader } from '@mantine/core'
import { Navigate, useLocation, useParams } from 'react-router'
import { useAuth } from '../auth/AuthContext'

export function RequireAuth({ children }: { children: ReactNode }) {
  const { me, loading } = useAuth()
  const location = useLocation()
  if (loading) {
    return (
      <Center h="100vh">
        <Loader />
      </Center>
    )
  }
  if (!me) return <Navigate to="/login" replace state={{ from: location.pathname + location.search }} />
  return <>{children}</>
}

/** Área da empresa: usuários de cliente são levados para a área do próprio cliente. */
export function CompanyOnly({ children }: { children: ReactNode }) {
  const { me } = useAuth()
  if (me?.type === 'customer') return <Navigate to={`/c/${me.customerId}/painel`} replace />
  return <>{children}</>
}

/** Usuário de cliente só acessa a área do próprio cliente (o backend também valida). */
export function CustomerAreaGuard({ children }: { children: ReactNode }) {
  const { me } = useAuth()
  const { customerId } = useParams()
  if (me?.type === 'customer' && String(me.customerId) !== customerId) {
    return <Navigate to={`/c/${me.customerId}/painel`} replace />
  }
  return <>{children}</>
}

export function HomeRedirect() {
  const { me } = useAuth()
  if (!me) return <Navigate to="/login" replace />
  return <Navigate to={me.type === 'customer' ? `/c/${me.customerId}/painel` : '/painel'} replace />
}

/** Id do cliente da rota atual (área do cliente). */
export function useCustomerId(): number {
  const { customerId } = useParams()
  return Number(customerId)
}
