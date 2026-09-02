import type { PaginationMetadata } from './pagination'

export interface TicketClassificationItem {
  id: number
  name: string
  isActive: boolean
}

export interface TicketClassification {
  category: TicketClassificationItem | null
  tags: TicketClassificationItem[]
}

export interface UpdateTicketClassificationRequest {
  categoryId: number | null
  tagIds: number[]
}

export type TaxonomyStatusFilter = 'Active' | 'Inactive'

export interface TicketCategory {
  id: number
  name: string
  description: string | null
  isActive: boolean
  createdAt: string
  updatedAt: string | null
  usageCount: number
}

export interface TicketTag {
  id: number
  name: string
  isActive: boolean
  createdAt: string
  updatedAt: string | null
  usageCount: number
}

export interface TaxonomyListQuery {
  search?: string
  status?: TaxonomyStatusFilter
  page: number
  pageSize: number
}

export interface TaxonomyListResponse<T> {
  items: T[]
  pagination: PaginationMetadata
}
