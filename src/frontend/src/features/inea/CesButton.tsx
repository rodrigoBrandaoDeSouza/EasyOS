import { Button } from '@mantine/core'
import { IconFileText } from '@tabler/icons-react'

/** CES (Comprovante de Execução de Serviço) exigido pelo INEA para clientes do RJ. */
export function CesButton({ orderId }: { orderId: number }) {
  return (
    <Button variant="light" leftSection={<IconFileText size={16} />} component="a" href={`/inea/ces.html?os=${orderId}`} target="_blank">
      Gerar CES
    </Button>
  )
}
