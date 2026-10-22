/** Erro de API com a mensagem já pronta para exibir ao usuário (vinda do ProblemDetails). */
export class ApiError extends Error {
  readonly status: number

  constructor(status: number, message: string) {
    super(message)
    this.status = status
  }
}

type Query = Record<string, string | number | boolean | null | undefined>

function withQuery(path: string, query?: Query): string {
  if (!query) return path
  const params = new URLSearchParams()
  for (const [key, value] of Object.entries(query)) {
    if (value !== undefined && value !== null && value !== '') params.set(key, String(value))
  }
  const qs = params.toString()
  return qs ? `${path}?${qs}` : path
}

let onUnauthorized: (() => void) | undefined

/** Chamado quando a sessão expira (401), para levar o usuário ao login. */
export function setUnauthorizedHandler(handler: () => void) {
  onUnauthorized = handler
}

async function request<T>(method: string, path: string, body?: unknown, query?: Query): Promise<T> {
  const isForm = body instanceof FormData
  const response = await fetch(withQuery(path, query), {
    method,
    credentials: 'same-origin',
    headers: {
      // Exigido pelo backend em toda chamada que altera dados (proteção contra CSRF).
      'X-Requested-With': 'easyos',
      ...(body !== undefined && !isForm ? { 'Content-Type': 'application/json' } : {}),
    },
    body: body === undefined ? undefined : isForm ? body : JSON.stringify(body),
  })

  if (response.status === 401 && !path.includes('/auth/')) onUnauthorized?.()

  if (!response.ok) {
    let message = 'Não foi possível concluir a operação. Tente novamente.'
    try {
      const problem = await response.json()
      message = problem.detail || problem.title || message
    } catch {
      if (response.status === 403) message = 'Você não tem permissão para esta operação.'
      if (response.status === 429) message = 'Muitas tentativas. Aguarde um minuto e tente novamente.'
    }
    throw new ApiError(response.status, message)
  }

  if (response.status === 204 || response.headers.get('content-length') === '0') return undefined as T
  const type = response.headers.get('content-type') ?? ''
  return (type.includes('json') ? response.json() : response.text()) as Promise<T>
}

export const api = {
  get: <T>(path: string, query?: Query) => request<T>('GET', path, undefined, query),
  post: <T = void>(path: string, body?: unknown, query?: Query) => request<T>('POST', path, body ?? {}, query),
  put: <T = void>(path: string, body?: unknown) => request<T>('PUT', path, body ?? {}),
  delete: <T = void>(path: string, query?: Query) => request<T>('DELETE', path, undefined, query),
  upload: <T = void>(path: string, form: FormData, method: 'POST' | 'PUT' = 'POST', query?: Query) =>
    request<T>(method, path, form, query),
  url: withQuery,
}
