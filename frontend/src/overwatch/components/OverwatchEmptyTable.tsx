import { Paper, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Typography } from '@mui/material'

interface OverwatchEmptyTableProps {
  columns: string[]
  emptyMessage: string
}

export function OverwatchEmptyTable({ columns, emptyMessage }: OverwatchEmptyTableProps) {
  return (
    <TableContainer
      component={Paper}
      elevation={0}
      sx={{ mt: 3, border: '1px solid', borderColor: 'divider' }}
    >
      <Table sx={{ minWidth: 720 }}>
        <TableHead>
          <TableRow>
            {columns.map((column) => (
              <TableCell key={column} sx={{ fontWeight: 750 }}>{column}</TableCell>
            ))}
          </TableRow>
        </TableHead>
        <TableBody>
          <TableRow>
            <TableCell colSpan={columns.length} sx={{ py: 7, textAlign: 'center' }}>
              <Typography color="text.secondary">{emptyMessage}</Typography>
            </TableCell>
          </TableRow>
        </TableBody>
      </Table>
    </TableContainer>
  )
}
