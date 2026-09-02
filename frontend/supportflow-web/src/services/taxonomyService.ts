import { apiClient } from '../api/apiClient'
import type {
  TaxonomyListQuery,
  TaxonomyListResponse,
  TicketCategory,
  TicketClassificationItem,
  TicketTag,
} from '../types/taxonomy'

export async function getCategories(
  query: TaxonomyListQuery,
): Promise<TaxonomyListResponse<TicketCategory>> {
  const { data } = await apiClient.get<TaxonomyListResponse<TicketCategory>>('/api/admin/categories', {
    params: query,
  })
  return data
}

export async function getTags(query: TaxonomyListQuery): Promise<TaxonomyListResponse<TicketTag>> {
  const { data } = await apiClient.get<TaxonomyListResponse<TicketTag>>('/api/admin/tags', {
    params: query,
  })
  return data
}

export async function createCategory(request: { name: string; description: string | null }): Promise<TicketCategory> {
  const { data } = await apiClient.post<TicketCategory>('/api/admin/categories', request)
  return data
}

export async function updateCategory(
  id: number,
  request: { name: string; description: string | null },
): Promise<TicketCategory> {
  const { data } = await apiClient.patch<TicketCategory>(`/api/admin/categories/${id}`, request)
  return data
}

export async function updateCategoryStatus(id: number, isActive: boolean): Promise<TicketCategory> {
  const { data } = await apiClient.patch<TicketCategory>(`/api/admin/categories/${id}/status`, { isActive })
  return data
}

export async function createTag(request: { name: string }): Promise<TicketTag> {
  const { data } = await apiClient.post<TicketTag>('/api/admin/tags', request)
  return data
}

export async function updateTag(id: number, request: { name: string }): Promise<TicketTag> {
  const { data } = await apiClient.patch<TicketTag>(`/api/admin/tags/${id}`, request)
  return data
}

export async function updateTagStatus(id: number, isActive: boolean): Promise<TicketTag> {
  const { data } = await apiClient.patch<TicketTag>(`/api/admin/tags/${id}/status`, { isActive })
  return data
}

export async function getAssignableCategories(activeOnly = true): Promise<TicketClassificationItem[]> {
  const { data } = await apiClient.get<TicketClassificationItem[]>('/api/taxonomy/categories', {
    params: { activeOnly },
  })
  return data
}

export async function getAssignableTags(activeOnly = true): Promise<TicketClassificationItem[]> {
  const { data } = await apiClient.get<TicketClassificationItem[]>('/api/taxonomy/tags', {
    params: { activeOnly },
  })
  return data
}
