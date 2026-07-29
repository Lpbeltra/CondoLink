import { Box, type BoxProps } from '@mui/material'

export function PageContainer(props: BoxProps) {
  return (
    <Box
      width="100%"
      maxWidth={1440}
      mx={{ xs: 'auto', md: 0 }}
      px={{ xs: 2, sm: 3, lg: 1.5 }}
      py={{ xs: 3, md: 4 }}
      {...props}
    />
  )
}
