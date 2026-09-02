import { Button, FormControl, InputLabel, MenuItem, Paper, Select, Stack, Typography } from '@mui/material'
import type { PaginationMetadata } from '../../types/pagination'

interface PaginationControlsProps {
  pagination: PaginationMetadata
  itemLabel: string
  onPageChange: (page: number) => void
  onPageSizeChange: (pageSize: number) => void
}

function getRangeSummary({ page, pageSize, totalCount }: PaginationMetadata, itemLabel: string): string {
  if (totalCount === 0) {
    return `0 ${itemLabel}`
  }

  const start = (page - 1) * pageSize + 1
  const end = Math.min(page * pageSize, totalCount)
  return `${start}-${end} / ${totalCount} ${itemLabel}`
}

export function PaginationControls({
  pagination,
  itemLabel,
  onPageChange,
  onPageSizeChange,
}: PaginationControlsProps) {
  const { hasNextPage, hasPreviousPage, page, pageSize, totalCount, totalPages } = pagination

  if (totalCount === 0) {
    return null
  }

  return (
    <Paper sx={{ border: '1px solid', borderColor: 'divider', p: 2 }}>
      <Stack
        direction={{ xs: 'column', md: 'row' }}
        spacing={1.5}
        sx={{ alignItems: { md: 'center' }, justifyContent: 'space-between' }}
      >
        <Typography color="text.secondary" variant="body2">
          {getRangeSummary(pagination, itemLabel)}
        </Typography>

        <Stack
          direction={{ xs: 'column', sm: 'row' }}
          spacing={1.25}
          sx={{ alignItems: { sm: 'center' }, width: { xs: '100%', md: 'auto' } }}
        >
          <FormControl size="small" sx={{ minWidth: 160 }}>
            <InputLabel id="page-size-label">Sayfa başına</InputLabel>
            <Select
              label="Sayfa başına"
              labelId="page-size-label"
              onChange={(event) => onPageSizeChange(Number(event.target.value))}
              value={pageSize}
            >
              <MenuItem value={25}>25</MenuItem>
              <MenuItem value={50}>50</MenuItem>
              <MenuItem value={100}>100</MenuItem>
            </Select>
          </FormControl>

          {totalPages > 1 ? (
            <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
              <Button
                aria-label="Önceki sayfa"
                disabled={!hasPreviousPage}
                onClick={() => onPageChange(page - 1)}
                variant="outlined"
              >
                Önceki
              </Button>
              <Typography sx={{ minWidth: 72, textAlign: 'center' }} variant="body2">
                {page} / {totalPages}
              </Typography>
              <Button
                aria-label="Sonraki sayfa"
                disabled={!hasNextPage}
                onClick={() => onPageChange(page + 1)}
                variant="outlined"
              >
                Sonraki
              </Button>
            </Stack>
          ) : (
            <Typography color="text.secondary" variant="body2">
              Sayfa 1 / 1
            </Typography>
          )}
        </Stack>
      </Stack>
    </Paper>
  )
}
