import axios from 'axios'

interface ApiErrorPayload {
  message?: string
  title?: string
  errors?: Record<string, string[]>
}

interface FriendlyApiErrorOptions {
  fallback: string
  unauthorizedMessage?: string
  forbiddenMessage?: string
  notFoundMessage?: string
  conflictMessage?: string
  unavailableMessage?: string
}

export function getApiErrorStatus(error: unknown): number | undefined {
  return axios.isAxiosError<ApiErrorPayload>(error)
    ? error.response?.status
    : undefined
}

export function getApiErrorMessage(
  error: unknown,
  fallback = 'An unexpected error occurred.',
): string {
  if (!axios.isAxiosError<ApiErrorPayload>(error)) {
    return error instanceof Error ? error.message : fallback
  }

  const payload = error.response?.data

  if (payload?.message) {
    return payload.message
  }

  if (payload?.errors) {
    const validationMessage = Object.values(payload.errors).flat()[0]
    if (validationMessage) {
      return validationMessage
    }
  }

  if (payload?.title) {
    return payload.title
  }

  return error.response
    ? fallback
    : 'Unable to reach the SupportFlow API. Check that the backend is running.'
}

export function getFriendlyApiErrorMessage(
  error: unknown,
  options: FriendlyApiErrorOptions,
): string {
  const status = getApiErrorStatus(error)

  if (!status) {
    return (
      options.unavailableMessage ??
      'SupportFlow hizmetine ulaşılamıyor. Lütfen daha sonra tekrar deneyin.'
    )
  }

  if (status === 401 && options.unauthorizedMessage) {
    return options.unauthorizedMessage
  }

  if (status === 403 && options.forbiddenMessage) {
    return options.forbiddenMessage
  }

  if (status === 404 && options.notFoundMessage) {
    return options.notFoundMessage
  }

  if (status === 409 && options.conflictMessage) {
    return options.conflictMessage
  }

  const message = getApiErrorMessage(error, options.fallback)

  return message.startsWith('Unable to reach the SupportFlow API')
    ? options.unavailableMessage ??
        'SupportFlow hizmetine ulaşılamıyor. Lütfen daha sonra tekrar deneyin.'
    : message
}
