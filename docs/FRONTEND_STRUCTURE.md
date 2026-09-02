# Frontend Yapısı

## Kaynak Ağacı

```text
frontend/supportflow-web/src/
├── api/             Axios API istemcisi
├── assets/brand/    Logo, işaret ve giriş ekranı illüstrasyonu
├── components/
│   ├── brand/       Marka bileşenleri
│   ├── common/      Paylaşılan ticket, SLA, pagination ve layout parçaları
│   ├── conversations/ Sohbet merkezi bileşenleri
│   └── dashboard/   Queue Aging ve Agent Workload bölümleri
├── context/         AuthProvider ve context değeri
├── hooks/           useAuth
├── layouts/         AdminLayout, CustomerLayout, TechnicalLayout
├── pages/           auth, customer, technical, admin ve shared sayfalar
├── routes/          ProtectedRoute
├── services/        Domain bazlı API çağrıları
├── types/           TypeScript DTO tipleri
├── utils/           Oturum, tarih, dosya, SLA/aging yardımcıları
├── App.tsx          Lazy route tanımları
├── main.tsx         React başlangıcı
└── theme.ts          Material UI teması
```

`node_modules` ve `dist` kaynak ağacının parçası değildir; sırasıyla bağımlılık ve üretim build çıktısıdır.

## İstemci Mimarisi

- `api/apiClient.ts`, `VITE_API_BASE_URL` üzerinden Axios oluşturur ve access token'ı `Authorization: Bearer` başlığına ekler.
- `AuthProvider`, başlangıçta `/api/auth/me` ile oturumu doğrular; 401 yanıtında token'ı temizler ve oturumu kapatır.
- `ProtectedRoute`, giriş yapılmamış kullanıcıyı `/login` sayfasına, yanlış role sahip kullanıcıyı `/unauthorized` sayfasına yönlendirir.
- `App.tsx`, role göre layout ve sayfaları `lazy`/`Suspense` ile yükler.
- `services/`, backend endpoint'leri için ince istemci katmanıdır; iş kuralları frontend'e kopyalanmaz.

## Paylaşılan UI

`components/common` içinde `AppShell`, bildirim zili, pagination kontrolü, durum/öncelik/SLA/aging chip'leri, ek listesi/yükleyicisi, sınıflandırma kartı ve activity timeline yer alır. `components/conversations` sohbet listesi, detay, mesaj görünümü ve genel yanıt bestecisini taşır. Dashboard bileşenleri Queue Aging ile Agent Workload API görünümlerini sunar.

## Sayfalar ve Rotalar

| Route | Rol | Sayfa | Amaç |
| --- | --- | --- | --- |
| `/` | Oturumlu kullanıcı | `HomeRedirect` | Role göre varsayılan ana sayfaya yönlendirir. |
| `/login` | Public | `LoginPage` | Giriş. |
| `/register` | Public | `RegisterPage` | Public Customer kaydı. |
| `/unauthorized` | Public | `UnauthorizedPage` | Role uygun olmayan rota bilgisi. |
| `/customer` | Customer | `CustomerHomePage` | Müşteri ana sayfası. |
| `/customer/tickets` | Customer | `CustomerTicketsPage` | Kendi ticket listesi. |
| `/customer/tickets/new` | Customer | `CreateTicketPage` | Yeni ticket. |
| `/customer/tickets/:id` | Customer | `CustomerTicketDetailPage` | Sahibi olduğu ticket detayı. |
| `/customer/notifications` | Customer | `NotificationsPage` | Bildirimler. |
| `/customer/profile` | Customer | `ProfilePage` | Profil/parola işlemleri. |
| `/technical` | TechnicalStaff | `TechnicalHomePage` | Teknik ana sayfa. |
| `/technical/tickets` | TechnicalStaff | `TechnicalTicketsPage` | Operasyonel ticket listesi. |
| `/technical/tickets/:id` | TechnicalStaff | `TechnicalTicketDetailPage` | Ticket yönetimi. |
| `/technical/conversations` | TechnicalStaff | `ConversationsPage` | Sohbet merkezi. |
| `/technical/notifications` | TechnicalStaff | `NotificationsPage` | Bildirimler. |
| `/technical/profile` | TechnicalStaff | `ProfilePage` | Profil/parola işlemleri. |
| `/admin` | Admin | `AdminHomePage` | Yönetici dashboard'u. |
| `/admin/tickets` | Admin | `TechnicalTicketsPage` | Tüm ticket listesi. |
| `/admin/tickets/:id` | Admin | `TechnicalTicketDetailPage` | Ticket detay/yönetim. |
| `/admin/conversations` | Admin | `ConversationsPage` | Sohbet merkezi. |
| `/admin/users` | Admin | `AdminUsersPage` | Kullanıcı yönetimi. |
| `/admin/users/:id` | Admin | `AdminUserDetailPage` | Kullanıcı detayı/audit. |
| `/admin/taxonomy` | Admin | `AdminTaxonomyPage` | Kategori ve etiket yönetimi. |
| `/admin/gmail` | Admin | `GmailIntegrationPage` | Gmail OAuth/durum/manual işlem/diagnostics. |
| `/admin/notifications` | Admin | `NotificationsPage` | Bildirimler. |
| `/admin/profile` | Admin | `ProfilePage` | Profil/parola işlemleri. |

## Sayfalama ve Oturum

`TicketListQuery`; `page`, `pageSize`, arama, durum, öncelik, atama, aging ve sınıflandırma filtrelerini taşır. `PaginationControls` ve `TicketPagination` aynı `PagedResponse` sözleşmesini kullanır.

Mevcut Web V1 istemcisi access token'ı `localStorage` içinde tutar. 401 durumunda token kaldırılır, oturum geçersiz işareti `sessionStorage` içinde tutulur ve kullanıcı girişe yönlendirilir. Bu yaklaşımın production güvenlik borcu [Güvenlik ve Roller](SECURITY_AND_ROLES.md) belgesinde açıklanmıştır.
