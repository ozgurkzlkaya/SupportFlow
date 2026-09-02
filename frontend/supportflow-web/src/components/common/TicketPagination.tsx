import { PaginationControls } from './PaginationControls'
import type { PaginationMetadata } from '../../types/pagination'

interface TicketPaginationProps {
  pagination: PaginationMetadata
  onPageChange: (page: number) => void
  onPageSizeChange: (pageSize: number) => void
}

export function TicketPagination({ pagination, onPageChange, onPageSizeChange }: TicketPaginationProps) {
  return (
    <PaginationControls
      itemLabel="talep"
      onPageChange={onPageChange}
      onPageSizeChange={onPageSizeChange}
      pagination={pagination}
    />
  )
}
