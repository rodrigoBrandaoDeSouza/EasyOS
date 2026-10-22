import { useEffect, useState } from 'react'
import {
  ActionIcon, Alert, Button, ColorInput, Divider, FileButton, Grid, Group, Image, NumberInput, Paper, PasswordInput, Radio, Stack,
  Switch, Text, TextInput, Title,
} from '@mantine/core'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { IconArrowLeft, IconDatabase, IconPlus, IconTrash, IconUpload } from '@tabler/icons-react'
import { useNavigate, useParams } from 'react-router'
import { api } from '../api/client'
import { Loading } from '../components/common'
import { notifyError, notifySuccess } from '../lib/notify'

interface Smtp {
  host: string | null
  port: number
  enableSsl: boolean
  userName: string | null
  fromAddress: string | null
  fromName: string | null
  subject: string | null
  hasPassword: boolean
}

interface TenantDetail {
  id: string
  slug: string
  name: string
  active: boolean
  databaseSummary: string
  domains: { host: string; isPrimary: boolean }[]
  branding: {
    primaryColor: string
    headingColor: string
    otherPlaguesLabel: string
    hasLogo: boolean
    hasLoginBackground: boolean
    hasFavicon: boolean
    hasReportLogo: boolean
  }
  settings: {
    orderSequenceByCustomer: boolean
    certificateDateOnVisit: boolean
    dayVisitsOnly: boolean
    travelTime: boolean
    lockTime: boolean
    smtp: Smtp
    features: { key: string; value: string }[]
  }
}

const ASSETS = [
  { key: 'logo', label: 'Logo (menu e login)', flag: 'hasLogo' },
  { key: 'login-background', label: 'Fundo da tela de login', flag: 'hasLoginBackground' },
  { key: 'favicon', label: 'Favicon', flag: 'hasFavicon' },
  { key: 'report-logo', label: 'Logo dos PDFs (PNG/JPG)', flag: 'hasReportLogo' },
] as const

const EMPTY: TenantDetail = {
  id: '',
  slug: '',
  name: '',
  active: true,
  databaseSummary: '',
  domains: [{ host: '', isPrimary: true }],
  branding: { primaryColor: '#37ab8c', headingColor: '#333333', otherPlaguesLabel: 'Outros', hasLogo: false, hasLoginBackground: false, hasFavicon: false, hasReportLogo: false },
  settings: {
    orderSequenceByCustomer: true,
    certificateDateOnVisit: false,
    dayVisitsOnly: true,
    travelTime: false,
    lockTime: false,
    smtp: { host: '', port: 587, enableSsl: true, userName: '', fromAddress: '', fromName: '', subject: '', hasPassword: false },
    features: [],
  },
}

/** Cadastro de um cliente EasyOS: domínios, banco de dados, identidade visual e configurações do antigo Web.config. */
export function TenantEditPage() {
  const { tenantId } = useParams()
  const isNew = !tenantId
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const [t, setT] = useState<TenantDetail>(EMPTY)
  const [connectionString, setConnectionString] = useState('')
  const [smtpPassword, setSmtpPassword] = useState<string | null>(null)
  const [assetVersion, setAssetVersion] = useState(0)

  const detail = useQuery({ queryKey: ['tenant', tenantId], queryFn: () => api.get<TenantDetail>(`/api/platform/tenants/${tenantId}`), enabled: !isNew })
  useEffect(() => {
    if (detail.data) setT(detail.data)
  }, [detail.data])

  const test = useMutation({
    mutationFn: () =>
      connectionString
        ? api.post<{ ok: boolean; message: string }>('/api/platform/tenants/test-connection', { connectionString })
        : api.post<{ ok: boolean; message: string }>(`/api/platform/tenants/${tenantId}/test-connection`),
    onSuccess: (r) => (r.ok ? notifySuccess(r.message) : notifyError(new Error(r.message))),
    onError: notifyError,
  })

  const save = useMutation({
    mutationFn: () => {
      const body = {
        slug: t.slug,
        name: t.name,
        active: t.active,
        connectionString: connectionString || null,
        domains: t.domains.filter((d) => d.host.trim()),
        primaryColor: t.branding.primaryColor,
        headingColor: t.branding.headingColor,
        otherPlaguesLabel: t.branding.otherPlaguesLabel,
        ...t.settings,
        smtpPassword,
      }
      return isNew ? api.post<TenantDetail>('/api/platform/tenants', body) : api.put<TenantDetail>(`/api/platform/tenants/${tenantId}`, body)
    },
    onSuccess: (saved) => {
      queryClient.invalidateQueries({ queryKey: ['tenants'] })
      queryClient.setQueryData(['tenant', saved.id], saved)
      setConnectionString('')
      setSmtpPassword(null)
      notifySuccess('Cliente salvo')
      if (isNew) navigate(`/clientes/${saved.id}`, { replace: true })
    },
    onError: notifyError,
  })

  const uploadAsset = async (asset: string, file: File | null) => {
    if (!file) return
    const form = new FormData()
    form.append('file', file)
    try {
      await api.upload(`/api/platform/tenants/${tenantId}/branding/${asset}`, form, 'PUT')
      await queryClient.invalidateQueries({ queryKey: ['tenant', tenantId] })
      setAssetVersion((v) => v + 1)
      notifySuccess('Imagem enviada')
    } catch (e) {
      notifyError(e)
    }
  }

  const removeAsset = async (asset: string) => {
    try {
      await api.delete(`/api/platform/tenants/${tenantId}/branding/${asset}`)
      await queryClient.invalidateQueries({ queryKey: ['tenant', tenantId] })
    } catch (e) {
      notifyError(e)
    }
  }

  if (!isNew && detail.isPending) return <Loading />
  const setSettings = (patch: Partial<TenantDetail['settings']>) => setT((s) => ({ ...s, settings: { ...s.settings, ...patch } }))
  const setSmtp = (patch: Partial<Smtp>) => setSettings({ smtp: { ...t.settings.smtp, ...patch } })
  const setBranding = (patch: Partial<TenantDetail['branding']>) => setT((s) => ({ ...s, branding: { ...s.branding, ...patch } }))

  return (
    <Stack maw={1000}>
      <Group justify="space-between">
        <Group>
          <ActionIcon variant="subtle" onClick={() => navigate('/clientes')} aria-label="Voltar">
            <IconArrowLeft size={18} />
          </ActionIcon>
          <Title order={2}>{isNew ? 'Novo cliente' : t.name}</Title>
        </Group>
        <Button onClick={() => save.mutate()} loading={save.isPending}>
          Salvar
        </Button>
      </Group>

      <Paper p="md">
        <Title order={4} mb="sm">
          Identificação
        </Title>
        <Grid>
          <Grid.Col span={{ base: 12, sm: 6 }}>
            <TextInput label="Nome" withAsterisk value={t.name} onChange={(e) => setT({ ...t, name: e.currentTarget.value })} />
          </Grid.Col>
          <Grid.Col span={{ base: 12, sm: 4 }}>
            <TextInput
              label="Identificador"
              description="Letras minúsculas, números e hífen. Não pode ser alterado depois."
              withAsterisk
              disabled={!isNew}
              value={t.slug}
              onChange={(e) => setT({ ...t, slug: e.currentTarget.value.toLowerCase() })}
            />
          </Grid.Col>
          <Grid.Col span={{ base: 12, sm: 2 }}>
            <Switch label="Ativo" mt={30} checked={t.active} onChange={(e) => setT({ ...t, active: e.currentTarget.checked })} />
          </Grid.Col>
        </Grid>
      </Paper>

      <Paper p="md">
        <Title order={4} mb="sm">
          Domínios
        </Title>
        <Text size="sm" c="dimmed" mb="sm">
          Aponte o DNS (registro A/CNAME) de cada domínio para o servidor. O certificado HTTPS é emitido automaticamente no primeiro acesso.
        </Text>
        <Radio.Group value={String(t.domains.findIndex((d) => d.isPrimary))} onChange={(v) => setT({ ...t, domains: t.domains.map((d, i) => ({ ...d, isPrimary: String(i) === v })) })}>
          <Stack gap="xs">
            {t.domains.map((d, i) => (
              <Group key={i} gap="xs">
                <TextInput
                  placeholder="os.cliente.com.br"
                  value={d.host}
                  onChange={(e) => setT({ ...t, domains: t.domains.map((x, j) => (j === i ? { ...x, host: e.currentTarget.value.trim().toLowerCase() } : x)) })}
                  style={{ flex: 1 }}
                />
                <Radio value={String(i)} label="Principal" />
                <ActionIcon color="red" variant="subtle" onClick={() => setT({ ...t, domains: t.domains.filter((_, j) => j !== i) })} disabled={t.domains.length === 1} aria-label="Remover domínio">
                  <IconTrash size={16} />
                </ActionIcon>
              </Group>
            ))}
          </Stack>
        </Radio.Group>
        <Button variant="light" size="xs" mt="sm" leftSection={<IconPlus size={14} />} onClick={() => setT({ ...t, domains: [...t.domains, { host: '', isPrimary: false }] })}>
          Adicionar domínio
        </Button>
      </Paper>

      <Paper p="md">
        <Title order={4} mb="sm">
          Banco de dados
        </Title>
        {!isNew && (
          <Text size="sm" mb="xs">
            Atual: <b>{t.databaseSummary}</b>
          </Text>
        )}
        <PasswordInput
          label={isNew ? 'Connection string (SQL Server)' : 'Nova connection string'}
          description={isNew ? 'Ex.: Server=tcp:servidor.database.windows.net;Initial Catalog=easyos_cliente;User ID=...;Password=...;Encrypt=True' : 'Deixe em branco para manter a atual. É guardada cifrada.'}
          value={connectionString}
          onChange={(e) => setConnectionString(e.currentTarget.value)}
        />
        <Button variant="light" mt="sm" leftSection={<IconDatabase size={16} />} loading={test.isPending} disabled={isNew && !connectionString} onClick={() => test.mutate()}>
          Testar conexão
        </Button>
      </Paper>

      <Paper p="md">
        <Title order={4} mb="sm">
          Identidade visual
        </Title>
        <Grid>
          <Grid.Col span={{ base: 12, sm: 4 }}>
            <ColorInput label="Cor principal" value={t.branding.primaryColor} onChange={(v) => setBranding({ primaryColor: v })} format="hex" />
          </Grid.Col>
          <Grid.Col span={{ base: 12, sm: 4 }}>
            <ColorInput label="Cor dos títulos" value={t.branding.headingColor} onChange={(v) => setBranding({ headingColor: v })} format="hex" />
          </Grid.Col>
          <Grid.Col span={{ base: 12, sm: 4 }}>
            <TextInput
              label="Rótulo do 5º inseto (armadilha luminosa)"
              description='"Outros" na maioria dos clientes; alguns usam "Abelhas"'
              value={t.branding.otherPlaguesLabel}
              onChange={(e) => setBranding({ otherPlaguesLabel: e.currentTarget.value })}
            />
          </Grid.Col>
        </Grid>
        <Divider my="md" />
        {isNew ? (
          <Alert variant="light">Salve o cliente para enviar logo, fundo do login e favicon.</Alert>
        ) : (
          <Grid>
            {ASSETS.map((a) => (
              <Grid.Col key={a.key} span={{ base: 12, sm: 6 }}>
                <Group gap="sm" wrap="nowrap">
                  <Image
                    src={t.branding[a.flag] ? `/api/platform/tenants/${tenantId}/branding/${a.key}?v=${assetVersion}` : undefined}
                    w={96}
                    h={56}
                    fit="contain"
                    bg="gray.1"
                    radius="sm"
                    fallbackSrc="data:image/gif;base64,R0lGODlhAQABAAAAACw="
                    alt=""
                  />
                  <Stack gap={4}>
                    <Text size="sm" fw={500}>
                      {a.label}
                    </Text>
                    <Group gap={4}>
                      <FileButton accept="image/png,image/jpeg,image/svg+xml,image/x-icon,image/webp" onChange={(f) => uploadAsset(a.key, f)}>
                        {(props) => (
                          <Button {...props} size="xs" variant="light" leftSection={<IconUpload size={14} />}>
                            Enviar
                          </Button>
                        )}
                      </FileButton>
                      {t.branding[a.flag] && (
                        <Button size="xs" variant="subtle" color="red" onClick={() => removeAsset(a.key)}>
                          Remover
                        </Button>
                      )}
                    </Group>
                  </Stack>
                </Group>
              </Grid.Col>
            ))}
          </Grid>
        )}
      </Paper>

      <Paper p="md">
        <Title order={4} mb="sm">
          Regras do sistema
        </Title>
        <Stack gap="xs">
          <Switch
            label="Numeração da OS por cliente (desligado: sequência única por empresa) — SEQUENCE_BY_COSTUMER"
            checked={t.settings.orderSequenceByCustomer}
            onChange={(e) => setSettings({ orderSequenceByCustomer: e.currentTarget.checked })}
          />
          <Switch
            label="Exigir validade do certificado ao agendar visita — CERTIFICATEDATEONVISIT"
            checked={t.settings.certificateDateOnVisit}
            onChange={(e) => setSettings({ certificateDateOnVisit: e.currentTarget.checked })}
          />
          <Divider label="Aplicativo mobile" labelPosition="left" />
          <Switch label="Técnico vê apenas as visitas do dia (DayVisitsOnly)" checked={t.settings.dayVisitsOnly} onChange={(e) => setSettings({ dayVisitsOnly: e.currentTarget.checked })} />
          <Switch label="Registrar tempo de deslocamento (TravelTime)" checked={t.settings.travelTime} onChange={(e) => setSettings({ travelTime: e.currentTarget.checked })} />
          <Switch label="Bloqueio por horário (LockTime)" checked={t.settings.lockTime} onChange={(e) => setSettings({ lockTime: e.currentTarget.checked })} />
        </Stack>
        <Divider label="Flags CFG_* enviadas ao aplicativo" labelPosition="left" my="md" />
        <Stack gap="xs">
          {t.settings.features.map((f, i) => (
            <Group key={i} gap="xs">
              <TextInput
                placeholder="CFG_NOME"
                value={f.key}
                onChange={(e) => setSettings({ features: t.settings.features.map((x, j) => (j === i ? { ...x, key: e.currentTarget.value.toUpperCase() } : x)) })}
                w={240}
              />
              <TextInput
                placeholder="valor"
                value={f.value}
                onChange={(e) => setSettings({ features: t.settings.features.map((x, j) => (j === i ? { ...x, value: e.currentTarget.value } : x)) })}
                style={{ flex: 1 }}
              />
              <ActionIcon color="red" variant="subtle" onClick={() => setSettings({ features: t.settings.features.filter((_, j) => j !== i) })} aria-label="Remover flag">
                <IconTrash size={16} />
              </ActionIcon>
            </Group>
          ))}
          <Button variant="light" size="xs" w="fit-content" leftSection={<IconPlus size={14} />} onClick={() => setSettings({ features: [...t.settings.features, { key: 'CFG_', value: '' }] })}>
            Adicionar flag
          </Button>
        </Stack>
      </Paper>

      <Paper p="md">
        <Title order={4} mb="xs">
          E-mail (SMTP)
        </Title>
        <Text size="sm" c="dimmed" mb="sm">
          Usado no envio de senha e de documentos. Em branco, usa o SMTP padrão da EasyOS.
        </Text>
        <Grid>
          <Grid.Col span={{ base: 12, sm: 6 }}>
            <TextInput label="Servidor" value={t.settings.smtp.host ?? ''} onChange={(e) => setSmtp({ host: e.currentTarget.value })} />
          </Grid.Col>
          <Grid.Col span={{ base: 6, sm: 3 }}>
            <NumberInput label="Porta" value={t.settings.smtp.port} onChange={(v) => setSmtp({ port: Number(v) || 587 })} />
          </Grid.Col>
          <Grid.Col span={{ base: 6, sm: 3 }}>
            <Switch label="TLS/SSL" mt={30} checked={t.settings.smtp.enableSsl} onChange={(e) => setSmtp({ enableSsl: e.currentTarget.checked })} />
          </Grid.Col>
          <Grid.Col span={{ base: 12, sm: 6 }}>
            <TextInput label="Usuário" value={t.settings.smtp.userName ?? ''} onChange={(e) => setSmtp({ userName: e.currentTarget.value })} />
          </Grid.Col>
          <Grid.Col span={{ base: 12, sm: 6 }}>
            <PasswordInput
              label="Senha"
              placeholder={t.settings.smtp.hasPassword ? '•••••••• (mantida)' : ''}
              value={smtpPassword ?? ''}
              onChange={(e) => setSmtpPassword(e.currentTarget.value)}
            />
          </Grid.Col>
          <Grid.Col span={{ base: 12, sm: 6 }}>
            <TextInput label="E-mail remetente" value={t.settings.smtp.fromAddress ?? ''} onChange={(e) => setSmtp({ fromAddress: e.currentTarget.value })} />
          </Grid.Col>
          <Grid.Col span={{ base: 12, sm: 6 }}>
            <TextInput label="Nome do remetente" value={t.settings.smtp.fromName ?? ''} onChange={(e) => setSmtp({ fromName: e.currentTarget.value })} />
          </Grid.Col>
          <Grid.Col span={12}>
            <TextInput label="Assunto do e-mail de nova senha" value={t.settings.smtp.subject ?? ''} onChange={(e) => setSmtp({ subject: e.currentTarget.value })} />
          </Grid.Col>
        </Grid>
      </Paper>

      <Group justify="flex-end">
        <Button onClick={() => save.mutate()} loading={save.isPending}>
          Salvar
        </Button>
      </Group>
    </Stack>
  )
}
