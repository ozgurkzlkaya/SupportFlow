# SupportFlow Proje Özeti

## 1. Proje Amacı

SupportFlow; müşteri destek taleplerini, ekip çalışmalarını ve e-posta kaynaklı talepleri tek sistemde takip etmek için geliştirilmiş bir destek yönetim platformudur.

## 2. Çözülen Problem

Dağınık e-posta, mesaj ve manuel takip süreçleri; talep kaybına, belirsiz sahipliğe ve geciken yanıtlara neden olur. SupportFlow bu akışları talep numarası, rol, durum, atama, zaman çizelgesi ve bildirimler üzerinden görünür hale getirir.

## 3. Sistem Mimarisi

React web uygulaması ve React Native/Expo mobil uygulaması, ASP.NET Core Web API ile haberleşir. API; EF Core üzerinden SQL Server'a erişir ve Gmail API/OAuth entegrasyonunu backend tarafında yürütür.

## 4. Kullanılan Teknolojiler

- ASP.NET Core / .NET 10, EF Core, SQL Server, JWT ve xUnit
- React, TypeScript, Vite ve Material UI
- React Native, Expo SDK 54, Expo Router ve Expo SecureStore
- Gmail API ve OAuth 2.0

## 5. Roller ve Yetkiler

- **Customer:** Kendi taleplerini oluşturur, takip eder, mesaj/ek ekler ve bildirimlerini görür.
- **TechnicalStaff:** Operasyon kuyruğunda talep atama, durum/öncelik güncelleme, public yanıt, iç not ve sohbet işlemlerini yapar.
- **Admin:** Teknik operasyonlara ek olarak kullanıcı yönetimi, kategori/etiket yönetimi ve Gmail entegrasyonuna erişir.

## 6. Talep Yaşam Döngüsü

Talepler oluşturulur, atanır, işleme alınır, müşteri bekleme veya çözüm durumlarına geçer; gerekirse yeniden açılır ya da kapatılır. Public mesajlar müşteri iletişimini, iç notlar ekip içi çalışmayı destekler. Durum, atama, öncelik, sınıflandırma ve iletişim olayları zaman çizelgesine yansır.

## 7. Gmail Entegrasyon Mimarisi

OAuth credential ve token'lar backend'de tutulur. Gelen iletiler thread, header ve talep numarası sinyalleriyle ilişkilendirilir; işlenmiş mesaj kaydı tekrar işlemeyi engeller. Ekler yalnız gerektiğinde alınır. Otomatik sync varsayılan olarak kapalıdır; Admin için manuel işleme desteklenir.

## 8. Güvenlik Yaklaşımı

JWT tabanlı oturum, API rol yetkilendirmesi, kaynak sahipliği denetimleri ve Admin-only Gmail yönetimi uygulanır. Secret, token, OAuth credential, parola ve signing anahtarları kaynak kontrolü ve teslim paketi dışında tutulur.

## 9. Mobil Uygulama

Mobil istemci; rol farkındalıklı gezinme, müşteri talep yaşam döngüsü, teknik ekip/yönetici talep ve sohbet işlemleri, bildirimler, profil, ek dosyalar ve Admin Gmail görünümünü içerir. Gmail OAuth dönüşü backend callback ve `supportflowmobile` uygulama şeması ile gerçekleştirilir.

## 10. Web Uygulaması

Web istemcisi dashboard, talep yönetimi, sohbet merkezi, bildirimler, kullanıcı ve sınıflandırma yönetimi, SLA, queue aging, agent workload, Gmail entegrasyonu ve audit zaman çizelgesi gibi operasyon ekranlarını sunar.

## 11. Test Stratejisi

Backend xUnit testleri iş kuralları, yetkilendirme, Gmail işleme, sohbet durumları, pagination, SLA ve operasyon hesaplamalarını kapsar. Web ve mobil için TypeScript/lint/build/export kalite kontrolleri uygulanır. Kritik mobil akışlar fiziksel Android cihazda doğrulanmıştır.

## 12. Önemli Teknik Kararlar

- Kalıcı veriler SQL Server'da, şema değişimleri EF Core migration'larında tutulur.
- API listeleri ölçeklenebilir server-side pagination ile sunulur.
- Gmail tekrar işleme, `ProcessedEmailMessages` tabanlı dayanıklı deduplication ile engellenir.
- Otomatik Gmail polling kota güvenliği nedeniyle varsayılan olarak kapalı tutulur.
- Mobil API adresi `EXPO_PUBLIC_API_URL` ile ortamdan alınır; LAN IP kaynak kodda sabit değildir.

## 13. Çözülen Zorluklar

Gmail OAuth state yaşam döngüsü, e-posta/başlık/thread korelasyonu, dış istek sahipleri, güvenli ek dosya erişimi, mobil Android ek dosya paylaşımı, saat dilimi görünümü, teslim durumlu public yanıtlar ve konuşma okunma/bekleme durumları ele alınmıştır.

## 14. Sınırlamalar ve Üretim Değerlendirmesi

Mevcut APK yerel demo içindir; production signing, organizasyona ait package ID, AAB dağıtımı, kararlı HTTPS API/OAuth callback ve merkezi secret yönetimi gereklidir. Gmail otomatik sync, deploy ortamında kota doğrulaması yapılmadan açılmamalıdır.

## 15. Son Durum

Web V1 dondurulmuş, Mobile V1 M8.2 fiziksel kabulü tamamlanmış durumdadır. Proje staj/demo teslimi için hazır; production dağıtımı yukarıdaki operasyonel gereksinimlerin tamamlanmasına bağlıdır.
