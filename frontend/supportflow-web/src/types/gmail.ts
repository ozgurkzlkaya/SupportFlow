export interface GmailOAuthStatus {
  configured: boolean
  authorized: boolean
  canRead: boolean
  canSend: boolean
  mailbox: string
  backgroundSync: GmailBackgroundSyncStatus
}

export interface GmailBackgroundSyncStatus {
  enabled: boolean
  intervalSeconds: number
  isRunning: boolean
  lastStartedAt: string | null
  lastCompletedAt: string | null
  lastSuccessfulAt: string | null
  lastError: string | null
  lastSummary: GmailBackgroundSyncSummary | null
  isRateLimited: boolean
  rateLimitedUntil: string | null
  consecutiveFailures: number
}

export interface GmailBackgroundSyncSummary {
  examined: number
  newTickets: number
  newExternalTickets: number
  messagesAdded: number
  alreadyProcessed: number
  failed: number
}

export interface GmailOAuthStartResponse {
  authorizationUrl: string
}

export interface GmailMailboxProcessingSummary {
  processed: number
  newTickets: number
  messagesAdded: number
  alreadyProcessed: number
  unknownCustomers: number
  senderMismatches: number
  selfMessagesIgnored: number
  systemMessagesSkipped: number
  failed: number
  results: GmailMailboxProcessingItem[]
  newExternalTickets: number
  externalRequestersCreated: number
  externalRequestersMatched: number
}

export interface GmailMailboxProcessingItem {
  status: string
  ticketId: number | null
  ticketNumber: string | null
  reason: string | null
  externalRequesterId: number | null
  externalRequesterWasCreated: boolean | null
}

export interface GmailMessageDiagnostic {
  gmailMessageId: string
  gmailThreadId: string
  subject: string
  rawFrom: string
  parsedSenderEmail: string
  internalDateUtc: string
  internalDateEuropeIstanbul: string
  hasAttachments: boolean
  attachmentCount: number
  attachmentFileNames: string[]
  isAlreadyProcessed: boolean
  userExists: boolean
  matchingSupportFlowUserId: number | null
  matchingSupportFlowUserRole: string | null
  matchingSupportFlowUserIsActive: boolean | null
  registeredUserMatch: boolean
  externalRequesterMatch: boolean
  externalRequesterId: number | null
  requesterType: 'RegisteredUser' | 'ExternalRequester' | 'Unmatched'
  isCached: boolean
  cachedAt: string | null
}
