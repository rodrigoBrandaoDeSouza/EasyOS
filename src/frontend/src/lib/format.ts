import dayjs from 'dayjs'
import 'dayjs/locale/pt-br'

dayjs.locale('pt-br')

/** As datas da API vêm como horário local sem fuso ("2025-03-10T14:00:00"); nunca convertemos fuso no cliente. */
export const fmtDate = (value: string | null | undefined) => (value ? dayjs(value).format('DD/MM/YYYY') : '—')
export const fmtTime = (value: string | null | undefined) => (value ? dayjs(value).format('HH:mm') : '—')
export const fmtDateTime = (value: string | null | undefined) => (value ? dayjs(value).format('DD/MM/YYYY HH:mm') : '—')

/** Data no formato da API (yyyy-MM-dd). */
export const toApiDate = (value: Date | string | null | undefined) => (value ? dayjs(value).format('YYYY-MM-DD') : undefined)

/** Data e hora local, sem fuso, no formato aceito pela API. */
export const toApiDateTime = (value: Date | string) => dayjs(value).format('YYYY-MM-DDTHH:mm:ss')

/** Offset do navegador em minutos no padrão do legado (ex.: −180 em Brasília). */
export const browserOffsetMinutes = () => -new Date().getTimezoneOffset()

const numberFormat = new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 3 })
export const fmtNumber = (value: number | null | undefined) => (value === null || value === undefined ? '—' : numberFormat.format(value))

export function fmtBytes(bytes: number) {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  return `${(bytes / 1024 / 1024).toFixed(1)} MB`
}

export const UFS = [
  ['AC', 'Acre'], ['AL', 'Alagoas'], ['AM', 'Amazonas'], ['AP', 'Amapá'], ['BA', 'Bahia'], ['CE', 'Ceará'],
  ['DF', 'Distrito Federal'], ['ES', 'Espírito Santo'], ['GO', 'Goiás'], ['MA', 'Maranhão'], ['MG', 'Minas Gerais'],
  ['MS', 'Mato Grosso do Sul'], ['MT', 'Mato Grosso'], ['PA', 'Pará'], ['PB', 'Paraíba'], ['PE', 'Pernambuco'],
  ['PI', 'Piauí'], ['PR', 'Paraná'], ['RJ', 'Rio de Janeiro'], ['RN', 'Rio Grande do Norte'], ['RO', 'Rondônia'],
  ['RR', 'Roraima'], ['RS', 'Rio Grande do Sul'], ['SC', 'Santa Catarina'], ['SE', 'Sergipe'], ['SP', 'São Paulo'],
  ['TO', 'Tocantins'],
] as const

export const UF_OPTIONS = UFS.map(([value, label]) => ({ value, label: `${value} - ${label}` }))

export const COMPANY_PROFILES = [
  { value: '1', label: 'Administrador' },
  { value: '2', label: 'Back Office' },
  { value: '3', label: 'Técnico' },
]

export const CUSTOMER_PROFILES = [
  { value: '1', label: 'Administrador' },
  { value: '2', label: 'Visitante' },
]

export const profileLabel = (type: 'company' | 'customer', profile: number) =>
  (type === 'company' ? COMPANY_PROFILES : CUSTOMER_PROFILES).find((p) => p.value === String(profile))?.label ?? '—'
