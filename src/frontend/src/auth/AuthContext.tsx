import { createContext, useContext, useEffect, type ReactNode } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { api, ApiError, setUnauthorizedHandler } from '../api/client'
import type { Me } from '../api/types'

interface AuthState {
  me: Me | null
  loading: boolean
  login: (email: string, password: string) => Promise<Me>
  logout: () => Promise<void>
}

const AuthContext = createContext<AuthState | null>(null)

async function fetchMe(): Promise<Me | null> {
  try {
    return await api.get<Me>('/api/auth/me')
  } catch (e) {
    if (e instanceof ApiError && e.status === 401) return null
    throw e
  }
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient()
  const me = useQuery({ queryKey: ['me'], queryFn: fetchMe, staleTime: 5 * 60_000, retry: false })

  useEffect(() => {
    // Sessão expirada em qualquer chamada: limpa o cache e volta para o login.
    setUnauthorizedHandler(() => {
      queryClient.clear()
      queryClient.setQueryData(['me'], null)
    })
  }, [queryClient])

  const value: AuthState = {
    me: me.data ?? null,
    loading: me.isPending,
    login: async (email, password) => {
      const user = await api.post<Me>('/api/auth/login', { email, password })
      queryClient.clear()
      queryClient.setQueryData(['me'], user)
      return user
    },
    logout: async () => {
      await api.post('/api/auth/logout')
      queryClient.clear()
      queryClient.setQueryData(['me'], null)
    },
  }

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth() {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth fora do AuthProvider')
  return ctx
}

/** Usuário logado (só usar dentro de rotas protegidas). */
export function useMe(): Me {
  const { me } = useAuth()
  if (!me) throw new Error('Usuário não autenticado')
  return me
}

export const isCompanyAdmin = (me: Me) => me.type === 'company' && me.profile === 1
export const isCustomerAdmin = (me: Me) => me.type === 'customer' && me.profile === 1
/** Pode gerenciar usuários e cadastro do cliente: empresa (qualquer perfil web) ou admin do cliente. */
export const canManageCustomer = (me: Me) => me.type === 'company' || isCustomerAdmin(me)
