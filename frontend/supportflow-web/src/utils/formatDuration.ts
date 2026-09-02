export function formatDuration(totalMinutes: number | null | undefined): string {
  if (totalMinutes === null || totalMinutes === undefined || totalMinutes <= 0) {
    return '0 dk'
  }

  const days = Math.floor(totalMinutes / (24 * 60))
  const hours = Math.floor((totalMinutes % (24 * 60)) / 60)
  const minutes = totalMinutes % 60
  const parts: string[] = []

  if (days > 0) parts.push(`${days} gün`)
  if (hours > 0) parts.push(`${hours} sa`)
  if (minutes > 0 && days === 0) parts.push(`${minutes} dk`)

  return parts.join(' ') || '0 dk'
}
