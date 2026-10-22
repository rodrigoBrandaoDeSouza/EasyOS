import { createContext, useContext } from 'react'
import type { Branding } from '../api/types'

export const BrandingContext = createContext<Branding | null>(null)

export function useBranding(): Branding {
  return (
    useContext(BrandingContext) ?? {
      name: 'EasyOS',
      primaryColor: '#37ab8c',
      headingColor: '#333333',
      otherPlaguesLabel: 'Outros',
      logoUrl: null,
      loginBackgroundUrl: null,
      faviconUrl: null,
    }
  )
}
