import { useState } from 'react'
import { ActionIcon, Anchor, Breadcrumbs, Button, FileButton, Group, Modal, Paper, Table, Text, TextInput, Tooltip } from '@mantine/core'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { IconDownload, IconFile, IconFolder, IconFolderPlus, IconTrash, IconUpload } from '@tabler/icons-react'
import dayjs from 'dayjs'
import { api } from '../../api/client'
import type { FolderListing } from '../../api/types'
import { isCompanyAdmin, useMe } from '../../auth/AuthContext'
import { confirmAction, Empty, Loading, PageHeader } from '../../components/common'
import { fmtBytes } from '../../lib/format'
import { notifyError, notifySuccess } from '../../lib/notify'

/**
 * Arquivos da empresa (antigo "Downloads"): todos os usuários consultam e baixam;
 * o administrador da empresa envia arquivos, cria pastas e exclui.
 */
export function FilesPage() {
  const me = useMe()
  const canManage = isCompanyAdmin(me)
  const [path, setPath] = useState('')
  const [newFolder, setNewFolder] = useState<string | null>(null)
  const queryClient = useQueryClient()

  const listing = useQuery({ queryKey: ['files', path], queryFn: () => api.get<FolderListing>('/api/files', { path }) })
  const refresh = () => queryClient.invalidateQueries({ queryKey: ['files'] })

  const upload = useMutation({
    mutationFn: (files: File[]) => {
      const form = new FormData()
      files.forEach((f) => form.append('files', f))
      return api.upload('/api/files/upload', form, 'POST', { path })
    },
    onSuccess: () => {
      refresh()
      notifySuccess('Arquivo(s) enviado(s)')
    },
    onError: notifyError,
  })

  const createFolder = useMutation({
    mutationFn: (name: string) => api.post('/api/files/folder', { path, name }),
    onSuccess: () => {
      refresh()
      setNewFolder(null)
    },
    onError: notifyError,
  })

  const remove = useMutation({
    mutationFn: (target: string) => api.delete('/api/files', { path: target }),
    onSuccess: () => {
      refresh()
      notifySuccess('Removido com sucesso')
    },
    onError: notifyError,
  })

  const segments = path ? path.split('/') : []

  return (
    <>
      <PageHeader
        title="Downloads"
        actions={
          canManage && (
            <>
              <Button variant="light" leftSection={<IconFolderPlus size={16} />} onClick={() => setNewFolder('')}>
                Nova pasta
              </Button>
              <FileButton multiple onChange={(files) => files.length && upload.mutate(files)}>
                {(props) => (
                  <Button {...props} leftSection={<IconUpload size={16} />} loading={upload.isPending}>
                    Enviar arquivos
                  </Button>
                )}
              </FileButton>
            </>
          )
        }
      />
      <Paper p="md">
        <Breadcrumbs mb="md">
          <Anchor onClick={() => setPath('')}>Arquivos</Anchor>
          {segments.map((s, i) => (
            <Anchor key={i} onClick={() => setPath(segments.slice(0, i + 1).join('/'))}>
              {s}
            </Anchor>
          ))}
        </Breadcrumbs>
        {listing.isPending ? (
          <Loading />
        ) : !listing.data?.folders.length && !listing.data?.files.length ? (
          <Empty>Pasta vazia.</Empty>
        ) : (
          <Table.ScrollContainer minWidth={520}>
            <Table>
              <Table.Thead>
                <Table.Tr>
                  <Table.Th>Nome</Table.Th>
                  <Table.Th w={110}>Tamanho</Table.Th>
                  <Table.Th w={150}>Modificado</Table.Th>
                  <Table.Th w={90} />
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {listing.data!.folders.map((f) => (
                  <Table.Tr key={f.path}>
                    <Table.Td>
                      <Anchor onClick={() => setPath(f.path)}>
                        <Group gap={6}>
                          <IconFolder size={18} />
                          {f.name}
                        </Group>
                      </Anchor>
                    </Table.Td>
                    <Table.Td />
                    <Table.Td />
                    <Table.Td>
                      {canManage && (
                        <ActionIcon
                          variant="subtle"
                          color="red"
                          onClick={() => confirmAction(`Excluir a pasta "${f.name}" e todo o seu conteúdo?`, () => remove.mutate(f.path), 'Excluir', true)}
                          aria-label="Excluir pasta"
                        >
                          <IconTrash size={18} />
                        </ActionIcon>
                      )}
                    </Table.Td>
                  </Table.Tr>
                ))}
                {listing.data!.files.map((f) => (
                  <Table.Tr key={f.path}>
                    <Table.Td>
                      <Group gap={6}>
                        <IconFile size={18} />
                        <Text size="sm">{f.name}</Text>
                      </Group>
                    </Table.Td>
                    <Table.Td>{fmtBytes(f.size)}</Table.Td>
                    <Table.Td>{dayjs(f.lastModifiedUtc).format('DD/MM/YYYY HH:mm')}</Table.Td>
                    <Table.Td>
                      <Group gap={4} wrap="nowrap">
                        <Tooltip label="Baixar">
                          <ActionIcon variant="subtle" component="a" href={api.url('/api/files/download', { path: f.path })} aria-label="Baixar arquivo">
                            <IconDownload size={18} />
                          </ActionIcon>
                        </Tooltip>
                        {canManage && (
                          <ActionIcon
                            variant="subtle"
                            color="red"
                            onClick={() => confirmAction(`Excluir o arquivo "${f.name}"?`, () => remove.mutate(f.path), 'Excluir', true)}
                            aria-label="Excluir arquivo"
                          >
                            <IconTrash size={18} />
                          </ActionIcon>
                        )}
                      </Group>
                    </Table.Td>
                  </Table.Tr>
                ))}
              </Table.Tbody>
            </Table>
          </Table.ScrollContainer>
        )}
      </Paper>

      <Modal opened={newFolder !== null} onClose={() => setNewFolder(null)} title="Nova pasta" centered>
        <form
          onSubmit={(e) => {
            e.preventDefault()
            if (newFolder?.trim()) createFolder.mutate(newFolder.trim())
          }}
        >
          <TextInput label="Nome da pasta" value={newFolder ?? ''} onChange={(e) => setNewFolder(e.currentTarget.value)} data-autofocus />
          <Group justify="flex-end" mt="md">
            <Button variant="default" onClick={() => setNewFolder(null)}>
              Cancelar
            </Button>
            <Button type="submit" loading={createFolder.isPending}>
              Criar
            </Button>
          </Group>
        </form>
      </Modal>
    </>
  )
}
