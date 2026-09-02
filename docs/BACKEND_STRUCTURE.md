# Backend Yapısı

```text
backend/
├── SupportFlow.Api/
│   ├── Controllers/        HTTP endpoint ve authorization sınırı
│   ├── Data/               AppDbContext
│   ├── DTOs/               API istek/yanıt sözleşmeleri
│   ├── Enums/              Rol, ticket, SLA, aging ve activity enum'ları
│   ├── Migrations/         EF Core migration'ları ve model snapshot
│   ├── Models/             Kalıcı EF entity'leri
│   ├── Services/
│   │   ├── Email/          E-posta içerik ve recipient bileşimi
│   │   ├── Gmail/          OAuth, inbox, correlation, send, rate limit
│   │   ├── QueueAging/     Aging hesaplama ve SQL projection
│   │   ├── Sla/            SLA hedefi, hesaplama ve yaşam döngüsü
│   │   └── TicketActivities/ Kalıcı activity yazımı ve metadata
│   ├── Settings/           Binding/validation yapılan ayarlar
│   ├── Program.cs          DI, JWT, CORS, Swagger ve pipeline
│   └── appsettings*.json   Ortam yapılandırması
└── SupportFlow.Api.Tests/  xUnit testleri
```

## Klasör Sorumlulukları

- **Controllers** yalnızca HTTP sözleşmesi, request doğrulaması, claim okuma ve servis çağrısı içerir; iş kuralı burada büyütülmemelidir.
- **Services** transaction, authorization'a bağlı alan kuralları, hesaplama ve dış entegrasyon davranışını içerir.
- **Models** EF Core kalıcı modelidir; API yanıtı yerine DTO kullanılmalıdır.
- **DTOs** web/mobile istemciler için kararlı veri sözleşmesidir.
- **Migrations** şema geçmişidir; elle düzenlemek yerine EF araç zinciri kullanılmalıdır.
- **Settings** güçlü tiplendirilmiş seçeneklerdir; secret içermez.

## Temel Servisler

| Servis | Sorumluluk |
| --- | --- |
| `AuthService` | Public Customer kaydı, parola hash doğrulaması, JWT üretimi ve aktif kullanıcı alma. |
| `UserService` | Profil, parola değişikliği ve aktif teknik personel listesi. |
| `TicketService` | Ticket CRUD iş akışları, mesaj, atama, durum, öncelik, klasifikasyon, ownership ve teslimat retry davranışı. |
| `AttachmentService` | Dosya doğrulama, kontrollü depolama, metadata ve yetkili indirme. |
| `NotificationService` | Ticket olaylarından kullanıcı bildirimleri ve read-state. |
| `ConversationService` | Talep sahibi bazlı sohbet özeti/detayı, pin, read-state, okunmamış ve bekleyen yanıt durumu. |
| `TicketTimelineService` | Activity ile legacy message/history kaynaklarını birleştirip paged timeline üretme. |
| `TicketActivityWriter` | Transaction içine değiştirilemez `TicketActivity` kaydı ekleme. |
| `TicketTaxonomyService` | Kategori/etiket yönetimi ve ticket sınıflandırması. |
| `AdminUserManagementService` | Kullanıcı liste/detay, aktiflik değişimi ve `UserAdminAudit`. |
| `TicketSlaService` / `TicketSlaCalculator` | SLA hedefi, ilk yanıt/çözüm yaşam döngüsü ve API state'i. |
| `QueueAgingCalculator` / `QueueAgingQueryProjection` | Bekleyen yanıt çapası, aging bucket'ı ve SQL projection yolu. |
| `AgentWorkloadService` | Aktif ticket, atama, SLA ve aging metriklerini ekip/agent bazında toplama. |

## Gmail Alt Sistemi

`Services/Gmail` altında aşağıdaki sorumluluklar ayrılmıştır:

- `GmailAuthorizationService`, `GmailOAuthStateStore`, `ProtectedFileDataStore`: OAuth ve yerel korumalı token store
- `GmailInboxService`, `GmailMessageParser`: Gmail listeleme, metadata/message parse ve ek descriptor'ları
- `GmailMailboxProcessor`, `InboundEmailProcessor`, `InboundEmailCorrelationService`: batch deduplication, inbound ticket/message işleme ve correlation
- `GmailSendService`, `TicketAcknowledgmentService`: Gmail REST ile outbound gönderim ve acknowledgment
- `GmailApiRateLimitState`, `GmailMailboxProcessingCoordinator`, `GmailInboxBackgroundService`: shared cooldown, serializasyon ve isteğe bağlı hosted sync
- `GmailDiagnosticsService`, `GmailDiagnosticsCache`: Admin-only read-only diagnostics

## Migration Geçmişi

Mevcut migration zinciri başlangıç şeması, Gmail inbound tracking/correlation, bildirim iyileştirmeleri, external requester, outbound teslimat, sohbet state'i, SLA, user audit, taxonomy ve ticket activity temelini içerir. Güncel EF modelinin kaynağı `Data/AppDbContext.cs`; snapshot `Migrations/AppDbContextModelSnapshot.cs` dosyasıdır.
