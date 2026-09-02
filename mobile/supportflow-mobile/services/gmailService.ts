import { apiRequest } from '@/services/apiClient';
import type {
  GmailIntegrationStatus,
  GmailMailboxProcessingSummary,
  GmailOAuthStartResponse,
} from '@/types/gmail';

const GMAIL_PATH = '/api/integrations/gmail';

export const gmailService = {
  getStatus(token: string) {
    return apiRequest<GmailIntegrationStatus>(`${GMAIL_PATH}/oauth/status`, { token });
  },

  startAuthorization(token: string, returnTarget: 'mobile' = 'mobile') {
    return apiRequest<GmailOAuthStartResponse>(
      `${GMAIL_PATH}/oauth/start?returnTarget=${returnTarget}`,
      { token },
    );
  },

  processMailbox(token: string) {
    return apiRequest<GmailMailboxProcessingSummary>(`${GMAIL_PATH}/process`, {
      method: 'POST',
      token,
    });
  },
};
