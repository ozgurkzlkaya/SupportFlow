import { parseApiDate } from './formatDate'

export function formatRelativeDate(value: string): string {
  const date = parseApiDate(value)

  if (date === null) {
    return '—'
  }

  const now = Date.now()
  const diffMs = date.getTime() - now
  const diffMinutes = Math.round(diffMs / 60000)

  const formatter = new Intl.RelativeTimeFormat('tr-TR', { numeric: 'auto' })

  if (Math.abs(diffMinutes) < 60) {
    return formatter.format(diffMinutes, 'minute')
  }

  const diffHours = Math.round(diffMinutes / 60)
  if (Math.abs(diffHours) < 24) {
    return formatter.format(diffHours, 'hour')
  }

  const diffDays = Math.round(diffHours / 24)
  if (Math.abs(diffDays) < 30) {
    return formatter.format(diffDays, 'day')
  }

  return new Intl.DateTimeFormat('tr-TR', {
    day: '2-digit',
    month: 'short',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
    timeZone: 'Europe/Istanbul',
  }).format(date)
}
