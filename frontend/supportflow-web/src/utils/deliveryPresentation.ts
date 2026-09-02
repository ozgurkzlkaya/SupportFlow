const deliveryFailurePrefix = 'e-posta gönderilemedi'

export function getDeliveryFailureMessage(safeDeliveryError?: string | null) {
  const normalizedError = safeDeliveryError?.replace(/\s+/g, ' ').trim()

  if (!normalizedError) {
    return 'E-posta gönderilemedi. Lütfen yeniden deneyin.'
  }

  if (normalizedError.toLocaleLowerCase('tr-TR').startsWith(deliveryFailurePrefix)) {
    return normalizedError
  }

  return `E-posta gönderilemedi. ${normalizedError}`
}
