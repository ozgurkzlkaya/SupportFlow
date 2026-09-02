const turkishDateTimeFormatter = new Intl.DateTimeFormat('tr-TR', {
  day: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
  month: 'long',
  timeZone: 'Europe/Istanbul',
  year: 'numeric',
})

const isoDateWithoutOffsetPattern =
  /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?$/

export function parseApiDate(value: string | null | undefined): Date | null {
  if (!value) {
    return null
  }

  const normalizedValue = isoDateWithoutOffsetPattern.test(value) ? `${value}Z` : value
  const date = new Date(normalizedValue)

  return Number.isNaN(date.getTime()) ? null : date
}

export function formatDate(value: string | null | undefined): string {
  const date = parseApiDate(value)
  return date === null ? '—' : turkishDateTimeFormatter.format(date)
}
