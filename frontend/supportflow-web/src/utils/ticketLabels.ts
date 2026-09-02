import { TicketPriority, TicketStatus } from '../types/ticket'

const statusLabels: Record<TicketStatus, string> = {
  [TicketStatus.New]: 'Yeni',
  [TicketStatus.Assigned]: 'Atandı',
  [TicketStatus.InProgress]: 'Devam Ediyor',
  [TicketStatus.WaitingForCustomer]: 'Müşteriden Bilgi Bekleniyor',
  [TicketStatus.Resolved]: 'Çözüldü',
  [TicketStatus.Closed]: 'Kapandı',
  [TicketStatus.Reopened]: 'Yeniden Açıldı',
}

const priorityLabels: Record<TicketPriority, string> = {
  [TicketPriority.Low]: 'Düşük',
  [TicketPriority.Normal]: 'Normal',
  [TicketPriority.High]: 'Yüksek',
  [TicketPriority.Critical]: 'Kritik',
}

export function getTicketStatusLabel(status: TicketStatus): string {
  return statusLabels[status]
}

export function getTicketPriorityLabel(priority: TicketPriority): string {
  return priorityLabels[priority]
}
