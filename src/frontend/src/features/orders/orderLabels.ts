/** Tipos gravados pelo app/legado nos detalhes da OS e seus rótulos na tela. */
export const BAIT_TYPES = [
  { type: 'Mofada', label: 'Trocados por mofo ou deterioração' },
  { type: 'Roída', label: 'Trocados por estarem roídos' },
  { type: 'Quebrada', label: 'Obstruídos, quebrados e/ou extraviados' },
] as const

export const GLUE_TYPES = [
  { type: 'sujeira', label: 'Trocados por sujeira ou deterioração' },
  { type: 'capturado', label: 'Trocados por roedor aderido' },
  { type: 'removida', label: 'Obstruídos, quebrados e/ou extraviados' },
] as const

export const PHEROMONE_TYPES = ['Traça', 'Besouro'] as const

/** Linha de total ("instaladas") que o legado grava junto com os pontos. */
export const INSTALLED = 'instaladas'

export function luminousColumns(otherLabel: string) {
  return [
    { code: 1, label: 'Mosca' },
    { code: 2, label: 'Mosquito' },
    { code: 3, label: 'Mariposa' },
    { code: 4, label: 'Besouro' },
    { code: 5, label: otherLabel },
  ]
}

/**
 * Diluente e calda calculados como na tela de OS do legado: só para produtos líquidos (ml)
 * ou formulações diluíveis (sufixos PS, PM, 1 G, WS, CE, SC...).
 */
export function dilution(productName: string, unity: string, quantity: number, concentration: number) {
  const diluible = unity === 'ml' || /PS$|PM$|1 G$| WS | CE$| SC$| SC /.test(productName)
  if (!diluible || !concentration) return null
  const diluent = (quantity * 100) / concentration
  return { diluent, syrup: diluent + quantity }
}
