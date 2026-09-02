export interface GmailBackgroundSyncStatus {
  enabled: boolean;
  intervalSeconds: number;
  isRunning: boolean;
  lastStartedAt: string | null;
  lastCompletedAt: string | null;
  lastSuccessfulAt: string | null;
  lastError: string | null;
  isRateLimited: boolean;
  rateLimitedUntil: string | null;
  consecutiveFailures: number;
}

export interface GmailIntegrationStatus {
  configured: boolean;
  authorized: boolean;
  canRead: boolean;
  canSend: boolean;
  mailbox: string;
  backgroundSync: GmailBackgroundSyncStatus | null;
}

export interface GmailOAuthStartResponse {
  authorizationUrl: string;
  completionReturnUri: string | null;
}

export interface GmailMailboxProcessingSummary {
  processed: number;
  newTickets: number;
  newExternalTickets: number;
  externalRequestersCreated: number;
  externalRequestersMatched: number;
  messagesAdded: number;
  alreadyProcessed: number;
  unknownCustomers: number;
  senderMismatches: number;
  selfMessagesIgnored: number;
  systemMessagesSkipped: number;
  failed: number;
  listedCandidates: number;
  alreadyProcessedSkipped: number;
  unseenFetched: number;
  attachmentsFetched: number;
}
