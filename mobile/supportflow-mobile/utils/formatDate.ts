const isoDateWithoutOffsetPattern =
  /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?$/;

function parseApiDate(value: string | null | undefined): Date | null {
  if (!value) return null;

  // Older API responses may omit DateTime.Kind after a SQL Server read.
  const normalizedValue = isoDateWithoutOffsetPattern.test(value) ? `${value}Z` : value;
  const date = new Date(normalizedValue);
  return Number.isNaN(date.getTime()) ? null : date;
}

export function formatDateTime(value: string | null | undefined) {
  const date = parseApiDate(value);
  if (!date) return '—';

  return new Intl.DateTimeFormat('tr-TR', {
    day: '2-digit',
    month: 'short',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  }).format(date);
}

export function formatFileSize(bytes: number) {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}
