import { notifications } from '@mantine/notifications'
import { ApiError } from '../api/client'

export function notifySuccess(message: string) {
  notifications.show({ color: 'teal', message, autoClose: 3500 })
}

export function notifyError(error: unknown) {
  const message = error instanceof ApiError || error instanceof Error ? error.message : 'Ocorreu um erro inesperado.'
  notifications.show({ color: 'red', title: 'Atenção', message, autoClose: 6000 })
}
