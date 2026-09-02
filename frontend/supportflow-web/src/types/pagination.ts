export interface PaginationMetadata {
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
  hasPreviousPage: boolean
  hasNextPage: boolean
}

export interface PagedResponse<T> {
  items: T[]
  pagination: PaginationMetadata
}
