import { apiClient } from '../api/apiClient'
import type {
  GmailMailboxProcessingSummary,
  GmailMessageDiagnostic,
  GmailOAuthStartResponse,
  GmailOAuthStatus,
} from '../types/gmail'

export async function getGmailOAuthStatus(): Promise<GmailOAuthStatus> {
  const { data } = await apiClient.get<GmailOAuthStatus>(
    '/api/integrations/gmail/oauth/status',
  )
  return data
}

export async function getGmailMessageDiagnostics(): Promise<GmailMessageDiagnostic[]> {
  const { data } = await apiClient.get<GmailMessageDiagnostic[]>(
    '/api/integrations/gmail/diagnostics/messages',
  )
  return data
}

export async function startGmailOAuth(): Promise<GmailOAuthStartResponse> {
  const { data } = await apiClient.get<GmailOAuthStartResponse>(
    '/api/integrations/gmail/oauth/start',
  )
  return data
}

export async function processGmailMailbox(): Promise<GmailMailboxProcessingSummary> {
  const { data } = await apiClient.post<GmailMailboxProcessingSummary>(
    '/api/integrations/gmail/process',
  )
  return data
}
