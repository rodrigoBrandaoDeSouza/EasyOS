import { useState } from 'react'
import { Button, Group, Modal, PasswordInput, Stack } from '@mantine/core'
import { useForm } from '@mantine/form'
import { api } from '../../api/client'
import { notifyError, notifySuccess } from '../../lib/notify'

export function ChangePasswordModal({ opened, onClose }: { opened: boolean; onClose: () => void }) {
  const [saving, setSaving] = useState(false)
  const form = useForm({
    initialValues: { currentPassword: '', newPassword: '', confirm: '' },
    validate: {
      currentPassword: (v) => (v ? null : 'Informe a senha atual'),
      newPassword: (v) => (v.length >= 6 ? null : 'Mínimo de 6 caracteres'),
      confirm: (v, values) => (v === values.newPassword ? null : 'As senhas não conferem'),
    },
  })

  const submit = form.onSubmit(async ({ currentPassword, newPassword }) => {
    setSaving(true)
    try {
      await api.post('/api/auth/change-password', { currentPassword, newPassword })
      notifySuccess('Senha alterada com sucesso')
      form.reset()
      onClose()
    } catch (e) {
      notifyError(e)
    } finally {
      setSaving(false)
    }
  })

  return (
    <Modal opened={opened} onClose={onClose} title="Alterar senha" centered>
      <form onSubmit={submit}>
        <Stack>
          <PasswordInput label="Senha atual" autoComplete="current-password" {...form.getInputProps('currentPassword')} />
          <PasswordInput label="Nova senha" autoComplete="new-password" {...form.getInputProps('newPassword')} />
          <PasswordInput label="Confirmar nova senha" autoComplete="new-password" {...form.getInputProps('confirm')} />
          <Group justify="flex-end">
            <Button variant="default" onClick={onClose}>
              Cancelar
            </Button>
            <Button type="submit" loading={saving}>
              Salvar
            </Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  )
}
