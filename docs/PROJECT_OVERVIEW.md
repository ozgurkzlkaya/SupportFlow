# SupportFlow

## Proje Amacı

SupportFlow, teknik destek taleplerinin portal ve Gmail üzerinden alınması, doğru destek ekibine yönlendirilmesi ve tüm iletişim geçmişinin izlenmesi için geliştirilmiş bir ticket yönetim platformudur. Sistem; kayıtlı müşteriler, Gmail ile gelen harici talep sahipleri, teknik personel ve yöneticiler için ortak fakat role göre sınırlandırılmış iş akışları sunar.

Roller:

- **Customer**: Kendi taleplerini açar, takip eder, yanıtlar ve dosya ekler.
- **TechnicalStaff**: Operasyonel talepleri görüntüler, yanıtlar, iç not ekler ve iş akışını günceller.
- **Admin**: Tüm operasyonu, kullanıcıları, sınıflandırmayı ve Gmail entegrasyonunu yönetir.

```text
Customer / Gmail
       |
     Ticket
       |
Technical Support
       |
Messages / Internal Notes
       |
Classification / Assignment
       |
SLA + Queue Aging
       |
Resolution / Closure
```

## Ana Modüller

- JWT tabanlı kimlik doğrulama ve profil yönetimi
- Ticket oluşturma, listeleme, sayfalama, arama ve filtreleme
- Durum, öncelik, atama ve yeniden açma yaşam döngüsü
- Genel mesajlar, iç notlar ve teslimat durumu/retry desteği
- Dosya ekleri ve yetkili indirme/önizleme
- Kategori, etiket ve ticket sınıflandırması
- Bildirimler
- Sohbetler merkezi: talep sahibi gruplama, sabitleme, okunmamış ve yanıt bekleyen durumları
- Birleşik ticket zaman çizelgesi ve kalıcı `TicketActivity` kaydı
- SLA ilk yanıt/çözüm takibi
- Queue Aging ve ekip iş yükü panoları
- Yönetici kullanıcı yönetimi ve denetim kaydı
- Gmail OAuth, gelen e-posta işleme, ilişkilendirme, ek alma ve çoğaltmayı önleme

## Teknoloji Yığını

| Alan | Mevcut teknoloji |
| --- | --- |
| Backend | .NET 10 (`net10.0`), ASP.NET Core Web API |
| ORM / veritabanı | Entity Framework Core 10.0.10, Microsoft SQL Server |
| Kimlik doğrulama | `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.10, JWT |
| Gmail | Google.Apis.Auth 1.75.0, Google.Apis.Gmail.v1 1.75.0.4225 |
| API dokümantasyonu | Swashbuckle.AspNetCore 10.2.3 / Swagger |
| Frontend | React 19.2.8, TypeScript ~6.0.2, React Router 7.18.2 |
| UI / HTTP | Material UI 9.3.1, Emotion, Axios 1.19.0 |
| Frontend build | Vite 8.2.0, oxlint 1.75.0 |
| Test | xUnit 2.9.3, Microsoft.NET.Test.Sdk 17.14.1, EF Core SQLite 10.0.10 |

## Yaşam Döngüsü Özeti

Portalda Customer tarafından açılan veya Gmail'den içeri alınan talep kalıcı olarak saklanır. Teknik personel/Admin atama, sınıflandırma, öncelik ve durum işlemlerini yapar. Genel yanıtlar talep sahibine görünür; iç notlar yalnızca personel görünürlüğündedir. SLA ve Queue Aging, ticket iletileri ve yaşam döngüsü üzerinden hesaplanır. Çözülmüş/kapalı talebe gelen yeni talep sahibi etkinliği mevcut yeniden açma kurallarıyla işlenir.

## Dokümantasyon Haritası

- [Mimari](ARCHITECTURE.md)
- [Veritabanı ER Diyagramı](DATABASE_ER_DIAGRAM.md)
- [Veritabanı Referansı](DATABASE_REFERENCE.md)
- [Backend Yapısı](BACKEND_STRUCTURE.md)
- [Frontend Yapısı](FRONTEND_STRUCTURE.md)
- [API Referansı](API_REFERENCE.md)
- [Güvenlik ve Roller](SECURITY_AND_ROLES.md)
- [Gmail Entegrasyonu](GMAIL_INTEGRATION.md)
- [Web V1 Son Durumu](WEB_V1_FINAL_STATUS.md)
