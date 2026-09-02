# Sunumda Gelebilecek Teknik Sorular

Bu soru-cevap listesi, SupportFlow’un mevcut gerçek implementasyonuna göre hazırlanmıştır.

Toplam soru sayısı: `24`

## 1. Neden ASP.NET Core Web API kullandınız?

ASP.NET Core performanslı, katmanlı mimari kurmaya uygun ve kurumsal projelerde yaygın kullanılan bir framework. JWT authentication, middleware yapısı, DI ve EF Core entegrasyonu nedeniyle backend tarafı için güçlü bir tercih oldu.

## 2. Neden React kullandınız?

Arayüzü bileşen tabanlı kurmak, sayfaları role göre ayırmak ve tekrar kullanılabilir UI parçaları üretmek için React uygun bir yapı sundu. Ayrıca TypeScript ile birlikte bakım kolaylığı sağladı.

## 3. TypeScript kullanmanın avantajı ne oldu?

API’den dönen veri tiplerini daha güvenli kullanabildim. Özellikle auth, ticket, attachment, notification ve Gmail response tiplerinde hata riskini azalttı.

## 4. Neden Microsoft SQL Server seçtiniz?

Proje .NET ekosistemi ile uyumlu olduğu için SQL Server doğal bir tercih oldu. EF Core ile güçlü entegrasyon sağladı ve ilişkisel modelleme açısından yeterliydi.

## 5. REST nedir ve bu projede nasıl kullanıldı?

REST, istemci ile sunucu arasında HTTP tabanlı kaynak odaklı iletişim yaklaşımıdır. Bu projede frontend, backend ile `/api/auth`, `/api/tickets`, `/api/notifications`, `/api/integrations` gibi endpoint’ler üzerinden konuşuyor.

## 6. JWT nedir?

JWT, kullanıcı kimliğini ve rol bilgisini taşıyan imzalı erişim token’ıdır. Kullanıcı giriş yaptıktan sonra frontend bu token’ı API isteklerinde bearer token olarak gönderir.

## 7. JWT güvenliği bu projede nasıl sağlandı?

Backend token’ın imzasını, issuer’ını, audience’ını ve süresini doğruluyor. Ayrıca token doğrulandıktan sonra veritabanından kullanıcının hâlâ aktif olup olmadığına ve rolünün değişip değişmediğine tekrar bakıyor.

## 8. Rol bazlı yetkilendirme nasıl çalışıyor?

Controller seviyesinde `[Authorize]` ve `[Authorize(Roles = ...)]` kullanılıyor. Bunun yanında service katmanında da müşteri sahipliği gibi iş kuralları kontrol ediliyor. Yani sadece route koruması değil, veri erişim kısıtı da uygulanıyor.

## 9. Customer ile TechnicalStaff arasındaki temel fark nedir?

Customer yalnızca kendi ticket’larını görüp genel yanıt verebilir. TechnicalStaff ise tüm ticket’ları görebilir, atama yapabilir, durum güncelleyebilir ve iç not ekleyebilir.

## 10. Admin’in ek yetkisi nedir?

Admin, teknik personelin operasyonel yetkilerine ek olarak Gmail entegrasyonu ekranına ve Gmail işleme endpoint’lerine erişebilir.

## 11. EF Core bu projede ne işe yarıyor?

EF Core, veritabanı tablolarını C# entity’leri üzerinden yönetmeyi sağlıyor. Sorgular, ilişkiler ve şema tanımları büyük ölçüde EF Core üzerinden kurulmuş durumda.

## 12. Migration nedir?

Migration, veritabanı şemasındaki değişiklikleri versiyonlu şekilde yönetmeye yarar. Bu projede migration dosyaları üzerinden tablo ve ilişki değişimleri takip ediliyor.

## 13. Neden service layer kullandınız?

Controller’ların sadece HTTP katmanında kalmasını istedim. Ticket oluşturma, durum değiştirme, bildirim üretme, Gmail işleme gibi iş kuralları service katmanında toplandı. Bu hem test edilebilirliği hem de kod okunabilirliğini artırdı.

## 14. Attachment güvenliği nasıl sağlanıyor?

Dosyalar sadece yetkili kullanıcılara gösteriliyor. Yüklemede dosya boyutu kontrolü var, izinli uzantılar kontrol ediliyor ve içerik imzası da doğrulanıyor. Ayrıca dosya yolu uploads kök dizini dışına taşamıyor.

## 15. Bu projede hangi dosya türlerine izin veriliyor?

Şu anda PNG, JPG, JPEG, WEBP ve PDF dosyalarına izin veriliyor. Maksimum boyut 5 MB, ticket başına da en fazla 5 dosya saklanıyor.

## 16. Neden Gmail entegrasyonu eklediniz?

Gerçek destek süreçlerinde müşteriler çoğu zaman doğrudan e-posta atıyor. Bu nedenle sadece web formu değil, mailbox üzerinden gelen talepleri de sisteme alabilen bir yapı kurmak istedim.

## 17. Neden OAuth 2.0 kullandınız?

Gmail API erişimi için güvenli ve standart yöntem OAuth 2.0’dır. Kullanıcının parolasını uygulamada tutmadan, yetki bazlı erişim sağlanır.

## 18. Neden sadece `gmail.readonly` ve `gmail.send` scope’ları istendi?

Minimum yetki prensibini uygulamak için. Sistem sadece gelen kutusunu okumaya ve acknowledgment e-postası göndermeye ihtiyaç duyuyor. Daha geniş Gmail yetkileri gerekmiyor.

## 19. E-posta tekrar işlenmesi nasıl engelleniyor?

Her işlenen Gmail mesajı `ProcessedEmailMessages` tablosuna kaydediliyor ve `GmailMessageId` üzerinde unique index bulunuyor. İşlem başlamadan önce de aynı ID’ye bakılarak deduplication yapılıyor.

## 20. Bir e-posta yanıtının hangi ticket’a ait olduğu nasıl bulunuyor?

Önce duplicate kontrolü yapılıyor. Sonra sırasıyla Gmail thread ID, `In-Reply-To`, `References` ve konu satırındaki ticket numarası kontrol ediliyor. Hiçbiri eşleşmezse yeni ticket oluşturma yoluna gidiliyor.

## 21. Çözülmüş bir ticket’a müşteri yeniden yazarsa ne oluyor?

Yeni ticket açılmıyor. Mevcut ticket’a yeni mesaj ekleniyor ve eğer ticket `Resolved` veya `Closed` durumundaysa otomatik olarak `Reopened` durumuna geçiriliyor.

## 22. Public reply ile internal note arasındaki fark nedir?

Public reply müşteri tarafından görülebilen yanıttır. Internal note ise sadece teknik ekip ve admin tarafında görünür; customer bu mesajları göremez.

## 23. Customer ownership nasıl korunuyor?

Customer rolündeki kullanıcılar API’de sadece kendi `CustomerId` kayıtlarıyla eşleşen ticket’lara erişebilir. Aynı kural mesajlar, attachments ve notification erişiminde de uygulanır.

## 24. JWT süresi dolarsa ne olur?

API isteği yetkisiz döner. Frontend tarafında oturumun geçersiz olduğu anlaşılır ve kullanıcı tekrar giriş yapmaya yönlendirilir. Bu davranış, korumalı route yapısı ile desteklenir.

## 25. Şifreler nasıl korunuyor?

Şifreler düz metin tutulmuyor. `IPasswordHasher<User>` ile hash’lenerek veritabanına yazılıyor. Girişte de hash doğrulaması yapılıyor.

## 26. Bu projede hangi test alanları öne çıktı?

Backend tarafında auth, ticket akışları, attachment davranışı, Gmail OAuth state lifecycle, Gmail correlation, deduplication ve inbox processing gibi kritik senaryolar test edildi. Ayrıca build ve regresyon kontrolleriyle davranış korunmaya çalışıldı.

## 27. Neden Gmail thread bilgisi için ayrı tablo kullandınız?

Bir ticket’ın birden fazla Gmail thread alias’ı ile ilişkilendirilebilmesi ve reply correlation’ın daha güvenilir yapılabilmesi için `TicketEmailThread` tablosu ayrı tasarlandı.

## 28. Self-message protection neden gerekliydi?

SupportFlow kendi gönderdiği acknowledgment e-postasını tekrar inbound ticket gibi işlememeli. Bu yüzden hem Gmail sorgusunda mailbox adresinden gelen mesajlar filtreleniyor hem de işlem katmanında ikinci bir kontrol yapılıyor.

## 29. Notification yapısı neden önemli?

Çünkü destek süreçlerinde sadece veri saklamak yetmez, kullanıcıyı aksiyon gerektiren olaylardan haberdar etmek gerekir. Bu projede yeni ticket, customer reply, assignment, status change ve Gmail olayları için bildirim üretildi.

## 30. Bu proje size teknik olarak ne kazandırdı?

Uçtan uca ürün geliştirme yaklaşımı kazandırdı. Frontend, backend, veritabanı, güvenlik, dosya yönetimi, dış servis entegrasyonu ve test gibi başlıkları aynı projede birlikte ele alma deneyimi sağladı.
