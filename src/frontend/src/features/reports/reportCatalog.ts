/** Relatórios da área do cliente (mesma lista e nomes do menu do sistema antigo). */
export const CUSTOMER_REPORTS = [
  { type: 'product-consumption', menu: 'Consumo de Produtos', unit: ' ' },
  { type: 'input-consumption', menu: 'Consumo de Insumos', unit: 'unidade' },
  { type: 'plagues-found', menu: 'Foco/Pragas encontradas', unit: 'unidade' },
  { type: 'product-plague', menu: 'Inseticidas X Pragas', unit: ' ' },
  { type: 'pheromone-traps', menu: 'Arms. de Feromônios', unit: 'unidade' },
  { type: 'luminous-traps', menu: 'Arms. Luminosas', unit: 'unidade' },
  { type: 'rodents-dead', menu: 'Roedores Mortos', unit: 'unidade' },
  { type: 'glue-traps', menu: 'Placa de Cola/Armadilha Mecânica', unit: 'unidade' },
  { type: 'gnawed-baits', menu: 'Iscas Roídas', unit: 'unidade' },
  { type: 'rodents-rodenticide', menu: 'Rodenticidas x Roedores', unit: ' ' },
  { type: 'non-conformities', menu: 'Não Conformidades', unit: '' },
] as const

export type CustomerReportType = (typeof CUSTOMER_REPORTS)[number]['type']
