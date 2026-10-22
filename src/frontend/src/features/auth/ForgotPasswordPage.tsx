import { useState } from 'react'
import { Alert, Anchor, Button, Center, Paper, Stack, Text, TextInput, Title } from '@mantine/core'
import { Link } from 'react-router'
import { api } from '../../api/client'
import { notifyError } from '../../lib/notify'

export function ForgotPasswordPage() {
  const [email, setEmail] = useState('')
  const [sent, setSent] = useState(false)
  const [loading, setLoading] = useState(false)

  const submit = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!email.trim()) return
    setLoading(true)
    try {
      await api.post('/api/auth/forgot-password', { email })
      setSent(true)
    } catch (err) {
      notifyError(err)
    } finally {
      setLoading(false)
    }
  }

  return (
    <Center mih="100vh" p="md" bg="brand.7">
      <Paper w="100%" maw={420} p="xl" shadow="lg">
        <form onSubmit={submit}>
          <Stack>
            <Title order={3}>Esqueci minha senha</Title>
            {sent ? (
              <Alert color="teal">
                Se o e-mail estiver cadastrado, você receberá uma nova senha em instantes. Verifique também a caixa de spam.
              </Alert>
            ) : (
              <>
                <Text size="sm" c="dimmed">
                  Informe seu e-mail. Enviaremos uma nova senha de acesso.
                </Text>
                <TextInput label="E-mail" type="email" value={email} onChange={(e) => setEmail(e.currentTarget.value)} required />
                <Button type="submit" loading={loading}>
                  Enviar nova senha
                </Button>
              </>
            )}
            <Anchor component={Link} to="/login" size="sm" ta="center">
              Voltar para o login
            </Anchor>
          </Stack>
        </form>
      </Paper>
    </Center>
  )
}
