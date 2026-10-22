import { useState } from 'react'
import { Button, Center, Paper, PasswordInput, Stack, TextInput, Title } from '@mantine/core'
import { useQueryClient } from '@tanstack/react-query'
import { Navigate, useNavigate } from 'react-router'
import { api } from '../api/client'
import { notifyError } from '../lib/notify'
import { usePlatformMe } from './PlatformApp'

export function PlatformLoginPage() {
  const me = usePlatformMe()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [loading, setLoading] = useState(false)

  if (me) return <Navigate to="/clientes" replace />

  const submit = async (e: React.FormEvent) => {
    e.preventDefault()
    setLoading(true)
    try {
      const user = await api.post('/api/platform/auth/login', { email, password })
      queryClient.setQueryData(['platform-me'], user)
      navigate('/clientes', { replace: true })
    } catch (err) {
      notifyError(err)
    } finally {
      setLoading(false)
    }
  }

  return (
    <Center mih="100vh" bg="gray.1" p="md">
      <Paper w="100%" maw={380} p="xl" shadow="md">
        <form onSubmit={submit}>
          <Stack>
            <Title order={3}>EasyOS · Painel</Title>
            <TextInput label="E-mail" type="email" value={email} onChange={(e) => setEmail(e.currentTarget.value)} required autoFocus />
            <PasswordInput label="Senha" value={password} onChange={(e) => setPassword(e.currentTarget.value)} required />
            <Button type="submit" loading={loading}>
              Entrar
            </Button>
          </Stack>
        </form>
      </Paper>
    </Center>
  )
}
