import { createTheme, type MantineColorsTuple } from '@mantine/core'
import { generateColors } from '@mantine/colors-generator'

const FALLBACK = '#37ab8c'

/** Tema com a cor principal do cliente (substitui o build de SASS por cliente do sistema antigo). */
export function buildTheme(primaryHex?: string | null) {
  let brand: MantineColorsTuple
  try {
    brand = generateColors(primaryHex || FALLBACK)
  } catch {
    brand = generateColors(FALLBACK)
  }
  return createTheme({
    primaryColor: 'brand',
    colors: { brand },
    fontFamily: 'Inter, Roboto, system-ui, -apple-system, "Segoe UI", sans-serif',
    defaultRadius: 'md',
    cursorType: 'pointer',
    components: {
      Button: { defaultProps: { radius: 'md' } },
      Paper: { defaultProps: { withBorder: true, radius: 'md' } },
      Table: { defaultProps: { striped: true, highlightOnHover: true, verticalSpacing: 'xs' } },
    },
  })
}
