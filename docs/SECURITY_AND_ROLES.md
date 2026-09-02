# Güvenlik ve Roller

## Kimlik Doğrulama

Backend JWT Bearer authentication kullanır. `Program.cs` issuer, audience, signing key ve token ömrünü doğrular; clock skew sıfırdır. `OnTokenValidated`, token'daki kullanıcı ve rolün veritabanında halen etkin ve aynı rolde olduğunu kontrol eder. Böylece devre dışı bırakılmış kullanıcıların eski token'ları geçerli kalmaz.

Public `POST /api/auth/register`, istemciden rol kabul etmez; `AuthService` yeni hesabı her zaman `Customer` rolünde oluşturur. Giriş pasif kullanıcı için başarısız olur.

## Yetki Matrisi

| Yetkinlik | Customer | TechnicalStaff | Admin |
| --- | --- | --- | --- |
| Kendi ticket'ını oluşturma | Evet | Hayır | Hayır |
| Kendi ticket'ını görüntüleme | Evet | - | - |
| Operasyonel ticket listesi | Hayır | Evet | Evet |
| Genel mesaj gönderme | Kendi ticket'ında | Evet | Evet |
| İç not ekleme/görme | Hayır | Evet | Evet |
| Atama, durum, öncelik | Hayır | Evet | Evet |
| Kategori/etiket sınıflandırması | Hayır | Evet | Evet |
| Sohbetler, pin/read-state | Hayır | Evet | Evet |
| Queue Aging | Hayır | Evet | Evet |
| Agent Workload | Hayır | Hayır | Evet |
| Kullanıcı/taxonomy yönetimi | Hayır | Hayır | Evet |
| Gmail yönetimi | Hayır | Hayır | Evet |

## Ownership ve Görünürlük

- Customer ticket sorguları `CustomerId == currentUserId` ile sınırlandırılır.
- Customer, yalnızca kendi ticket, mesaj, ek, timeline ve bildirimlerini görebilir.
- `TicketTimelineService`, Customer için yalnızca `RequesterVisible` activity'leri sunar.
- `IsInternal = true` mesajlar Customer görünümünde yer almaz.
- Customer sınıflandırma ve personele yönelik iç metadata'yı değiştiremez.
- Attachment yetkisi ticket erişiminden türetilir; fiziksel dosya yolu API ile dışarı verilmez.

## Kullanıcı Yönetimi Korumaları

Admin etkinlik değişikliklerini `UserAdminAudit` içine kaydeder. Sistem, yöneticinin kendi hesabını pasifleştirmesini ve son etkin Admin hesabının pasifleştirilmesini reddeder. Kullanıcılar soft/inactive davranışıyla korunur; genel hard-delete endpoint'i yoktur.

## Sohbet Kimliği

Pin ve read-state `UserId + RequesterType + RequesterId` ile benzersizdir. İki personel kullanıcısının okuma tercihi birbirini etkilemez; aynı sayısal ID'li registered/external requester da birbirine karışmaz.

## Bilinen Web V1 Güvenlik Borcu

Frontend access token'ı `localStorage` içinde tutar ve refresh-token mekanizması yoktur. 401 yanıtında token silinip girişe yönlendirme yapılır; production için HTTP-only secure cookie veya eşdeğer hardened token stratejisi değerlendirilmelidir. Bu belge mevcut davranışı açıklar, davranışı değiştirmez.

JWT anahtarı, SQL bağlantı bilgisi, Gmail OAuth client secret ve token store kaynak koduna veya dokümantasyona yazılmamalıdır.
