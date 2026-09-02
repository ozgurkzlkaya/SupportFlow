# Veritabanı Referansı

## Genel İlkeler

Model SQL Server üzerinde EF Core ile yönetilir. Operasyonel silme endpoint'leri yerine `IsActive` ve geçmiş kayıtları tercih edilir. İlişkilerde `NoAction` kullanılması, ticket, Gmail ve audit geçmişinin kazara cascade silinmesini önler.

| Tablo | Amaç | Önemli alanlar ve ilişkiler |
| --- | --- | --- |
| `Companies` | Şirket kaydı | `Id`, `Name`, `IsActive`; bir şirketin çok sayıda `User` kaydı olabilir. |
| `Users` | Kimlik, rol ve etkinlik durumu | Benzersiz `Email`, `PasswordHash`, `Role`, `IsActive`, isteğe bağlı `CompanyId`; ticket müşterisi/atananı, mesaj göndereni ve audit aktörü olabilir. |
| `ExternalRequesters` | Kayıtlı olmayan e-posta talep sahipleri | Benzersiz `NormalizedEmail`, `Email`, `DisplayName`; ticket, mesaj ve ek ile ilişkilidir. |
| `Tickets` | Ana iş kaydı | Benzersiz `TicketNumber`; müşteri veya external requester, atanan kullanıcı, kategori, durum/öncelik, SLA zamanları ve zaman damgaları içerir. |
| `TicketMessages` | Genel yanıt ve iç not | `TicketId`; `SenderId` veya `ExternalRequesterId`; `IsInternal`; outbound teslimat alanları (`OutboundDeliveryStatus`, deneme sayısı, teslim zamanı). |
| `TicketStatusHistories` | Durum geçiş geçmişi | `TicketId`, `ChangedByUserId`, eski/yeni durum, not ve `ChangedAt`. |
| `Attachments` | Ticket veya mesaja bağlı dosya metadatası | `TicketId`, isteğe bağlı `MessageId`, yükleyen user/external requester, özgün/depolanan ad, içerik türü ve boyut. Fiziksel yol API ile dışarı verilmez. |
| `TicketCategories` | Yönetilen kategori sözlüğü | Benzersiz `NormalizedName`, açıklama, `IsActive`; ticket'lar geçmiş kategoriyi korur. |
| `TicketTags` | Yönetilen etiket sözlüğü | Benzersiz `NormalizedName`, `IsActive`. |
| `TicketTagAssignments` | Ticket-etiket N:M köprüsü | Bileşik PK: `TicketId`, `TagId`; tekrar atamayı engeller. |
| `TicketActivities` | Değiştirilemez olay deposu | Ticket, aktör, görünürlük, kaynak, olay/recorded zamanı, ilgili message/history/attachment ve sürümlü JSON metadata. |
| `Notifications` | Kullanıcı bildirimleri | `UserId`, isteğe bağlı `TicketId`, `Type`, `IsRead`, metin ve oluşturulma zamanı. |
| `ConversationPins` | Personel kullanıcıya özel sohbet sabitlemesi | `UserId`, `RequesterType`, `RequesterId`; benzersiz üçlü. |
| `ConversationReadStates` | Personel kullanıcıya özel okunma durumu | `UserId`, `RequesterType`, `RequesterId`, `LastReadAt`; benzersiz üçlü. |
| `TicketEmailThreads` | Gmail thread alias'ı | `TicketId`, benzersiz `GmailThreadId`; bir ticket'ın birden çok thread alias'ı olabilir. |
| `ProcessedEmailMessages` | Gmail deduplication kaydı | Benzersiz `GmailMessageId`, `TicketEmailThreadId`, isteğe bağlı `InternetMessageId` ve `TicketMessageId`. |
| `UserAdminAudits` | Kullanıcı etkin/pasif işlemlerinin audit'i | Aktör ve hedef kullanıcı, eski/yeni aktiflik, hedef rol/e-posta ve zaman. |

## Ticket Yaşam Döngüsü

`Tickets.Status` ve `Tickets.Priority` string olarak saklanan enum'lardır. Ticket; `CustomerId` veya `ExternalRequesterId` ile bir talep sahibine bağlanır. `AssignedUserId` teknik personel/Admin iş akışı için, `CategoryId` sınıflandırma için isteğe bağlıdır. `ClosedAt`, ilk yanıt/çözüm SLA alanları ve `UpdatedAt` yaşam döngüsü görünümünü destekler.

Durum değişikliği hem `TicketStatusHistories` kaydı hem de uygun `TicketActivities` kaydı üretir. Yeni talep sahibi etkinliği mevcut ticket'ı ilişkilendirir; iş kuralları uygunsa `Reopened` durumuna getirir.

## Taksonomi ve Tarihsel Koruma

Kategori ve etiketler fiziksel silinmez; Admin yönetimi `IsActive` üzerinden çalışır. Böylece eski ticket'lar atandıkları kategori/etiket bilgisini korur. `TicketTagAssignments` yalnızca ilişki anahtarlarını saklar.

## Sohbet Okuma ve Pin Durumu

`ConversationPins` ve `ConversationReadStates`, ticket'ın değil personel kullanıcısının tercihidir. `RequesterType` ile aynı sayısal ID'ye sahip kayıtlı müşteri ve external requester ayrıştırılır. Bu tablolar ticket statüsünü, önceliğini, atamasını veya mesaj içeriğini değiştirmez.

## Gmail Kalıcılığı ve Çoğaltmayı Önleme

`TicketEmailThreads`, Gmail `threadId` değerini ticket'a bağlar. `ProcessedEmailMessages.GmailMessageId` benzersizliği aynı iletinin tekrar ticket/mesaj üretmesini engeller. `TicketMessageId`, gelen e-postanın yeni bir mesaj olarak işlendiği durumda bağlanır; başlangıç ticket e-postasında boş olabilir. `InternetMessageId` indekslidir ve ek ilişkilendirme sinyali olarak tutulur.

## Audit ve Etkinlik Geçmişi

`TicketActivities`, `TicketActivityWriter` aracılığıyla eklenir ve modelde setter'ları dışarı kapalıdır. Aktör check constraint'i user, external requester ve system biçimlerinin birbirine karışmasını engeller. `UserAdminAudits` ise kullanıcı aktiflik yönetimindeki aktör/hedef bilgisini kalıcı tutar.

## Önemli İndeksler

- Ticket numarası, kullanıcı e-postası, Gmail mesaj/thread kimlikleri ve normalize sınıflandırma adları için benzersiz indeksler
- SLA tarihleri ve `Tickets.CategoryId` için indeksler
- `TicketActivities` için ticket + zaman + id sıralama indeksi
- `ConversationPins` ve `ConversationReadStates` için kullanıcı/istek sahibi bileşik benzersiz indeksleri
- `UserAdminAudits` için hedef kullanıcı-zaman ve aktör indeksleri
