# API Referansı

Tüm endpoint'ler aksi belirtilmedikçe JSON kullanır. Enum değerleri API'de string olarak serileştirilir. Swagger, Development ortamında `/swagger` altında sunulur.

## Authentication

| Method | Endpoint | Erişim | Amaç |
| --- | --- | --- | --- |
| POST | `/api/auth/register` | Public | Yalnızca Customer rolünde kullanıcı kaydı. |
| POST | `/api/auth/login` | Public | JWT access token ve kullanıcı bilgisi döndürür. |
| GET | `/api/auth/me` | Authenticated | Mevcut etkin kullanıcıyı doğrular. |

## Profil ve Kullanıcılar

| Method | Endpoint | Erişim | Amaç |
| --- | --- | --- | --- |
| GET | `/api/users/me` | Authenticated | Profil getirir. |
| PATCH | `/api/users/me` | Authenticated | Profil günceller. |
| POST | `/api/users/me/change-password` | Authenticated | Parola değiştirir. |
| GET | `/api/users/technical-staff` | TechnicalStaff, Admin | Etkin teknik personel listesi. |

## Ticket, Mesaj, Ek ve Timeline

| Method | Endpoint | Erişim | Amaç |
| --- | --- | --- | --- |
| POST | `/api/tickets` | Customer | Yeni portal ticket'ı oluşturur. |
| GET | `/api/tickets` | Authenticated | Yetkiye göre sayfalı ticket listesi. |
| GET | `/api/tickets/summary` | Authenticated | Ticket dashboard özeti. |
| GET | `/api/tickets/{id}` | Authenticated | Yetkiye göre ticket detayı. |
| PATCH | `/api/tickets/{id}/classification` | TechnicalStaff, Admin | Kategori/etiket sınıflandırması. |
| GET | `/api/tickets/{id}/timeline` | Authenticated | Sayfalı birleşik activity timeline. |
| GET | `/api/tickets/{id}/messages` | Authenticated | Görünür ticket mesajları. |
| POST | `/api/tickets/{id}/messages` | Authenticated | Rol/ownership kuralına göre mesaj veya iç not ekler. |
| POST | `/api/tickets/{id}/messages/{messageId}/retry-delivery` | TechnicalStaff, Admin | Başarısız genel yanıt teslimatını aynı mesaj üzerinde yeniden dener. |
| PATCH | `/api/tickets/{id}/assign` | TechnicalStaff, Admin | Teknik personele atar. |
| PATCH | `/api/tickets/{id}/status` | TechnicalStaff, Admin | Durum geçişi yapar. |
| PATCH | `/api/tickets/{id}/priority` | TechnicalStaff, Admin | Önceliği değiştirir. |
| GET | `/api/tickets/{id}/history` | Authenticated | Durum geçmişi. |
| GET | `/api/tickets/{id}/attachments` | Authenticated | Ek metadata listesi. |
| POST | `/api/tickets/{id}/attachments` | Authenticated | `multipart/form-data`, alan adı `file` ile ek yükler. |
| GET | `/api/tickets/{id}/attachments/{attachmentId}` | Authenticated | Yetkili binary dosya indirir/önizler. |

### `GET /api/tickets` Query Parametreleri

| Parametre | Açıklama |
| --- | --- |
| `priority`, `status` | `TicketPriority` ve `TicketStatus` filtreleri. |
| `assignedUserId`, `assignmentState` | Atanan kullanıcı veya `TicketAssignmentFilter`; birlikte kullanılamaz. `InactiveAssignee` yalnızca Admin. |
| `activeOnly`, `search` | Yaşam döngüsü ve metin araması. |
| `agingFilter`, `sort` | `TicketAgingFilter` ve `TicketListSort`. |
| `categoryId`, `classificationState`, `tagId` | Sınıflandırma filtreleri; `categoryId` ile `classificationState` birlikte kullanılamaz. |
| `page`, `pageSize` | 1 tabanlı sayfa; varsayılanlar `1`/`25`, en büyük boyut `100`. |

```json
{
  "items": [],
  "pagination": {
    "page": 1,
    "pageSize": 25,
    "totalCount": 0,
    "totalPages": 0,
    "hasPreviousPage": false,
    "hasNextPage": false
  }
}
```

## Bildirimler ve Sohbetler

| Method | Endpoint | Erişim | Amaç |
| --- | --- | --- | --- |
| GET | `/api/notifications?unreadOnly={bool}` | Authenticated | Kullanıcının bildirimleri. |
| GET | `/api/notifications/unread-count` | Authenticated | Okunmamış sayısı. |
| PATCH | `/api/notifications/{id}/read` | Authenticated | Tek bildirimi okur. |
| PATCH | `/api/notifications/read-all` | Authenticated | Tüm bildirimleri okur. |
| GET | `/api/conversations?search=&filter=` | TechnicalStaff, Admin | Sohbet özeti ve filtre sayıları. |
| POST | `/api/conversations/{requesterType}/{requesterId}/read` | TechnicalStaff, Admin | Sohbeti okundu işaretler. |
| GET | `/api/conversations/{requesterType}/{requesterId}` | TechnicalStaff, Admin | Sohbet detayı. |
| PUT | `/api/conversations/{requesterType}/{requesterId}/pin` | TechnicalStaff, Admin | Sabitler. |
| DELETE | `/api/conversations/{requesterType}/{requesterId}/pin` | TechnicalStaff, Admin | Sabitlemeyi kaldırır. |

## Dashboard, Taxonomy ve Admin

| Method | Endpoint | Erişim | Amaç |
| --- | --- | --- | --- |
| GET | `/api/dashboard/queue-aging` | TechnicalStaff, Admin | Bekleyen yanıt sayıları ve en eski ticket'lar. |
| GET | `/api/dashboard/agent-workload` | Admin | Ekip/agent iş yükü, SLA ve aging. |
| GET | `/api/taxonomy/categories?activeOnly={bool}` | TechnicalStaff, Admin | Sınıflandırma kategorileri. |
| GET | `/api/taxonomy/tags?activeOnly={bool}` | TechnicalStaff, Admin | Sınıflandırma etiketleri. |
| GET/POST | `/api/admin/categories` | Admin | Sayfalı kategori listeleme/oluşturma. |
| PATCH | `/api/admin/categories/{id}` | Admin | Kategori güncelleme. |
| PATCH | `/api/admin/categories/{id}/status` | Admin | Kategori aktifliği. |
| GET/POST | `/api/admin/tags` | Admin | Sayfalı etiket listeleme/oluşturma. |
| PATCH | `/api/admin/tags/{id}` | Admin | Etiket güncelleme. |
| PATCH | `/api/admin/tags/{id}/status` | Admin | Etiket aktifliği. |
| GET | `/api/admin/users` | Admin | Sayfalı kullanıcı listesi. |
| GET | `/api/admin/users/{id}` | Admin | Kullanıcı detayı. |
| GET | `/api/admin/users/{id}/audit` | Admin | Sayfalı user-admin audit. |
| PATCH | `/api/admin/users/{id}/status` | Admin | Aktif/pasif durumu. |

Admin taxonomy listeleri `search`, `status`, `page`, `pageSize`; kullanıcı listesi `search`, `role`, `status`, `page`, `pageSize` kabul eder.

## Gmail

| Method | Endpoint | Erişim | Amaç |
| --- | --- | --- | --- |
| GET | `/api/integrations/gmail/oauth/start` | Admin | OAuth URL üretir. |
| GET | `/api/integrations/gmail/oauth/callback` | Public callback | Google callback'ini tamamlar. |
| GET | `/api/integrations/gmail/oauth/status` | Admin | OAuth/background runtime durumu. |
| POST | `/api/integrations/gmail/process` | Admin | Manuel gelen e-posta işleme. |
| GET | `/api/integrations/gmail/diagnostics/messages` | Admin | Read-only diagnostics. |

Rate-limit sırasında Gmail endpoint'leri retry zamanı içeren `429` döndürebilir. [Gmail Entegrasyonu](GMAIL_INTEGRATION.md) ayrıntıları açıklar.
