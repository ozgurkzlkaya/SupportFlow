import { QueueAgingBucket } from '../types/ticket'

const labels: Record<QueueAgingBucket, string> = {
  [QueueAgingBucket.New]: 'Yeni',
  [QueueAgingBucket.ShortWait]: 'Kısa Bekleme',
  [QueueAgingBucket.Waiting]: 'Bekliyor',
  [QueueAgingBucket.LongWait]: 'Uzun Bekleme',
  [QueueAgingBucket.Overdue]: 'Gecikmiş',
  [QueueAgingBucket.CriticalWait]: 'Kritik Bekleme',
}

export function getQueueAgingLabel(bucket: QueueAgingBucket): string {
  return labels[bucket]
}
