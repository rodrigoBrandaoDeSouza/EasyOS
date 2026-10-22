import { useState } from 'react'
import { Anchor, Box, Button, Center, Image, Paper, PasswordInput, Stack, Text, TextInput, Title } from '@mantine/core'
import { useForm } from '@mantine/form'
import { Link, Navigate, useLocation, useNavigate } from 'react-router'
import { useBranding } from '../../app/BrandingContext'
import { useAuth } from '../../auth/AuthContext'
import { notifyError } from '../../lib/notify'

export function LoginPage() {
  const branding = useBranding()
  const { me, login } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const [submitting, setSubmitting] = useState(false)
  const form = useForm({
    initialValues: { email: '', password: '' },
    validate: {
      email: (v) => (v.trim() ? null : 'Informe o e-mail'),
      password: (v) => (v ? null : 'Informe a senha'),
    },
  })

  if (me) return <Navigate to="/" replace />

  const submit = form.onSubmit(async ({ email, password }) => {
    setSubmitting(true)
    try {
      const user = await login(email, password)
      const from = (location.state as { from?: string } | null)?.from
      navigate(from && from !== '/login' ? from : user.type === 'customer' ? `/c/${user.customerId}/painel` : '/painel', { replace: true })
    } catch (e) {
      notifyError(e)
    } finally {
      setSubmitting(false)
    }
  })

  return (
    <Box
      mih="100vh"
      style={{
        backgroundColor: 'var(--mantine-color-brand-7)',
        backgroundImage: branding.loginBackgroundUrl ? `url(${branding.loginBackgroundUrl})` : undefined,
        backgroundSize: 'cover',
        backgroundPosition: 'center',
      }}
    >
      <Center mih="100vh" p="md">
        <Paper w="100%" maw={400} p="xl" shadow="lg">
          <form onSubmit={submit}>
            <Stack>
              <Center>
                {branding.logoUrl ? (
                  <Image src={branding.logoUrl} alt={branding.name} mah={80} w="auto" fit="contain" />
                ) : (
                  <Title order={3} c="brand.7">
                    {branding.name}
                  </Title>
                )}
              </Center>
              <Text ta="center" c="dimmed" size="sm">
                Acesse com seu e-mail e senha
              </Text>
              <TextInput label="E-mail" type="email" autoComplete="username" autoFocus {...form.getInputProps('email')} />
              <PasswordInput label="Senha" autoComplete="current-password" {...form.getInputProps('password')} />
              <Button type="submit" loading={submitting} fullWidth>
                Entrar
              </Button>
              <Anchor component={Link} to="/esqueci-senha" size="sm" ta="center">
                Esqueci minha senha
              </Anchor>
            </Stack>
          </form>
        </Paper>
      </Center>
    </Box>
  )
}
