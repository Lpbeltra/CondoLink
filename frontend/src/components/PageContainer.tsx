import { createContext, useContext, type ReactNode } from 'react'
import { Box, type BoxProps } from '@mui/material'

const fullWidthContext = createContext(false)

export function PageContainerScope({ fullWidth, children }: { fullWidth: boolean; children: ReactNode }) {
  return <fullWidthContext.Provider value={fullWidth}>{children}</fullWidthContext.Provider>
}

export function PageContainer(props: BoxProps) {
  const fullWidth = useContext(fullWidthContext)
  return (
    <Box
      width="100%"
      maxWidth={fullWidth ? 'none' : 1440}
      mx={fullWidth ? 0 : 'auto'}
      px={{ xs: 2, sm: 3, lg: 4 }}
      py={{ xs: 3, md: 4 }}
      {...props}
    />
  )
}
