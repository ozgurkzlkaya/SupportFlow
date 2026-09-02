import { apiConfig } from '@/config/api';
import { File, Paths } from 'expo-file-system';

export class ApiError extends Error {
  constructor(
    message: string,
    public readonly status: number | null,
  ) {
    super(message);
    this.name = 'ApiError';
  }
}

export interface DownloadedFile {
  uri: string;
  size: number;
}

type RequestBody = object | FormData | undefined;

interface ApiRequestOptions {
  method?: 'GET' | 'POST' | 'PATCH' | 'PUT' | 'DELETE';
  token?: string | null;
  body?: RequestBody;
}

let unauthorizedHandler: (() => void) | null = null;

export function setUnauthorizedHandler(handler: (() => void) | null) {
  unauthorizedHandler = handler;
}

export async function apiRequest<T>(
  path: string,
  { method = 'GET', token, body }: ApiRequestOptions = {},
): Promise<T> {
  if (!apiConfig.baseUrl) {
    throw new ApiError(
      'Sunucu adresi yapılandırılmamış. Lütfen uygulama bağlantı ayarını kontrol edin.',
      null,
    );
  }

  const headers: Record<string, string> = { Accept: 'application/json' };
  const isMultipart = typeof FormData !== 'undefined' && body instanceof FormData;

  if (token) headers.Authorization = `Bearer ${token}`;
  if (body && !isMultipart) headers['Content-Type'] = 'application/json';

  let response: Response;
  try {
    response = await fetch(`${apiConfig.baseUrl}${path}`, {
      method,
      headers,
      body: body ? (isMultipart ? body : JSON.stringify(body)) : undefined,
    });
  } catch {
    throw new ApiError('Sunucuya bağlanılamadı. İnternet bağlantınızı kontrol edin.', null);
  }

  const payload = await readResponseBody(response);
  if (!response.ok) {
    if (response.status === 401) unauthorizedHandler?.();
    throw new ApiError(getSafeErrorMessage(payload, response.status), response.status);
  }

  return payload as T;
}

// Native download keeps the bearer token and writes an actual local file for Android sharing.
export async function apiDownloadToFile(
  path: string,
  token: string | null,
  fileName: string,
): Promise<DownloadedFile> {
  if (!apiConfig.baseUrl) {
    throw new ApiError(
      'Sunucu adresi yapılandırılmamış. Lütfen uygulama bağlantı ayarını kontrol edin.',
      null,
    );
  }

  const url = `${apiConfig.baseUrl}${path}`;

  try {
    const destination = new File(Paths.cache, fileName);
    const downloaded = await File.downloadFileAsync(url, destination, {
      headers: {
        Accept: '*/*',
        Authorization: `Bearer ${token ?? ''}`,
      },
      // Each open uses a unique cache filename, so an existing file is unexpected.
      idempotent: false,
    });
    const localFile = new File(downloaded.uri);
    const exists = localFile.exists;
    const size = localFile.size;

    if (!downloaded.uri.startsWith('file://') || !exists || size <= 0) {
      throw new ApiError('İndirilen dosya doğrulanamadı.', null);
    }

    return { uri: downloaded.uri, size };
  } catch {
    throw new ApiError('Dosya indirilemedi. Lütfen tekrar deneyin.', null);
  }
}

async function readResponseBody(response: Response): Promise<unknown> {
  if (response.status === 204) return undefined;
  const contentType = response.headers.get('content-type') ?? '';
  if (!contentType.includes('application/json')) return undefined;

  try {
    return await response.json();
  } catch {
    return undefined;
  }
}

function getSafeErrorMessage(payload: unknown, status: number): string {
  if (status === 401) return 'E-posta veya şifre hatalı.';
  if (status === 409) return 'Bu e-posta adresi zaten kayıtlı.';
  if (status === 400) return 'Lütfen bilgileri kontrol edin.';
  if (status === 403) return 'Bu işlem için yetkiniz bulunmuyor.';
  return 'İşlem şu anda tamamlanamadı. Lütfen tekrar deneyin.';
}
