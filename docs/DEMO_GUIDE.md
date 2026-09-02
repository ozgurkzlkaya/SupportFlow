# SupportFlow Demo Rehberi

Bu akış kontrollü yerel demo içindir. Gerçek parola, token, OAuth credential veya gizli yapılandırma ekranda ya da belgede paylaşılmamalıdır. Demo hesapları ayrı ve güvenli kanaldan sağlanır.

## Hazırlık

1. API'yi başlatın: `dotnet run --launch-profile http --urls "http://0.0.0.0:5135"`.
2. Web demosu gerekiyorsa `frontend/supportflow-web` içinde `npm run dev` çalıştırın.
3. Android demo APK'sını yükleyin veya mobile development client'ı başlatın.
4. Telefonun API'ye aynı ağdan eriştiğini doğrulayın.
5. `GmailBackgroundSync.Enabled` değerinin `false` kaldığını doğrulayın.

## Customer Akışı

1. Customer hesabıyla giriş yapın.
2. Talep listesini açın; arama/filtre varsa gösterin.
3. Yeni talep oluşturun ve detayını açın.
4. Public mesaj gönderin; zaman çizelgesindeki güncellemeyi gösterin.
5. Bir ek dosya yükleyip Android paylaşım/açma ekranında önizleyin.
6. Bildirimler ve Profil ekranlarını gösterin.
7. Customer için Sohbetler ve Gmail Integration sekmelerinin görünmediğini vurgulayın.
8. Çıkış yapın.

## TechnicalStaff Akışı

1. TechnicalStaff hesabıyla giriş yapın.
2. Talep kuyruğunu ve bir talep detayını açın.
3. Varsa atama, durum, öncelik ve sınıflandırma işlemlerini gösterin.
4. Bir public yanıt veya iç not ekleyin; iç notun müşteri görünümünden ayrıldığını açıklayın.
5. Sohbetler ekranında istek sahibi gruplamasını ve talep yönlendirmesini gösterin.
6. Çıkış yapın.

## Admin Akışı

1. Admin hesabıyla giriş yapın.
2. Dashboard ve talep yönetimi ekranlarını gösterin.
3. Kullanıcı/sınıflandırma, SLA, queue aging veya workload ekranlarından demo için uygun olanları açın.
4. Bildirimler ve Profil ekranını gösterin.
5. Gmail Integration ekranında yalnızca bağlantı durumunu gösterin.
6. Gmail demo yapılacaksa, Gmail kotası sağlıklı olduğunda **Gelen Mailleri İşle** eylemini bir kez kullanın; otomatik sync'i açmayın.
7. Rol ayrımı ve API tarafındaki yetkilendirme yaklaşımıyla bitirin.

## Demo Notları

- Gmail OAuth ve token'lar mobile taşınmaz; backend tarafından yönetilir.
- Gmail manuel işleme bir fallback akışıdır. Rate limit oluşursa tekrar denemek yerine cooldown süresine uyun.
- M8.2 APK'sı local demo artifact'ıdır; Play Store üretim paketi değildir.
