import { Box, type BoxProps } from '@mui/material'

export function PageContainer(props: BoxProps) {
  return <Box width="100%" maxWidth={1440} mx="auto" px={{ xs: 2, sm: 3, lg: 3 }} py={{ xs: 3, md: 5 }} {...props} />
}
