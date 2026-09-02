import { apiClient } from '../api/apiClient'
import type { AgentWorkloadDashboard, QueueAgingDashboard } from '../types/dashboard'

export async function getQueueAgingDashboard(): Promise<QueueAgingDashboard> {
  const { data } = await apiClient.get<QueueAgingDashboard>('/api/dashboard/queue-aging')
  return data
}

export async function getAgentWorkloadDashboard(): Promise<AgentWorkloadDashboard> {
  const { data } = await apiClient.get<AgentWorkloadDashboard>('/api/dashboard/agent-workload')
  return data
}
