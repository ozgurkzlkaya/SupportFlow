import type { UserRole } from './auth'
import type { TicketPriority, TicketStatus } from './ticket'

export const TicketTimelineKind = {
  TicketCreated: 'TicketCreated',
  StatusChanged: 'StatusChanged',
  PriorityChanged: 'PriorityChanged',
  AssignmentChanged: 'AssignmentChanged',
  ClassificationChanged: 'ClassificationChanged',
  MessageCreated: 'MessageCreated',
  InternalNoteCreated: 'InternalNoteCreated',
  AttachmentAdded: 'AttachmentAdded',
} as const

export type TicketTimelineKind =
  (typeof TicketTimelineKind)[keyof typeof TicketTimelineKind]

export const TicketTimelineSource = {
  Portal: 'Portal',
  GmailInbound: 'GmailInbound',
  System: 'System',
} as const

export type TicketTimelineSource =
  (typeof TicketTimelineSource)[keyof typeof TicketTimelineSource]

export const TicketTimelineVisibility = {
  Internal: 'Internal',
  RequesterVisible: 'RequesterVisible',
} as const

export type TicketTimelineVisibility =
  (typeof TicketTimelineVisibility)[keyof typeof TicketTimelineVisibility]

export const TicketTimelineActorKind = {
  User: 'User',
  ExternalRequester: 'ExternalRequester',
  System: 'System',
} as const

export type TicketTimelineActorKind =
  (typeof TicketTimelineActorKind)[keyof typeof TicketTimelineActorKind]

export interface TicketTimelineActor {
  kind: TicketTimelineActorKind
  displayName: string
  role: UserRole | null
}

export interface TicketTimelineMetadata {
  oldStatus: TicketStatus | null
  newStatus: TicketStatus | null
  oldPriority: TicketPriority | null
  newPriority: TicketPriority | null
  oldAssigneeName: string | null
  newAssigneeName: string | null
  oldCategoryName: string | null
  newCategoryName: string | null
  addedTags: string[] | null
  removedTags: string[] | null
}

export interface TicketTimelineAttachment {
  fileName: string
  contentType: string
  fileSize: number
}

export interface TicketTimelineItem {
  id: string
  kind: TicketTimelineKind
  occurredAt: string
  source: TicketTimelineSource | null
  visibility: TicketTimelineVisibility
  actor: TicketTimelineActor
  title: string
  summary: string | null
  relatedMessageId: number | null
  relatedAttachmentId: number | null
  metadata: TicketTimelineMetadata | null
  attachment: TicketTimelineAttachment | null
}
