# Sunum Ekran Görüntüsü Planı

Bu plan, çalışan gerçek SupportFlow arayüzünden alınacak ekran görüntülerini düzenli ve sunum dostu şekilde hazırlamak içindir.

## Genel Çekim Kuralları

- Tarayıcıyı masaüstü genişliğinde açın: tercihen `1440x900` veya benzeri
- Tarayıcı zoom değeri `100%` olsun
- Sekme sayısını azaltın, dikkat dağıtan üst çubukları temizleyin
- Şifre yöneticisi pop-up’ları görünmesin
- DevTools açık olmasın
- URL çubuğu görünüyorsa localhost dışında kişisel veri görünmesin
- Karanlık mod yerine açık tema tercih edin
- Eğer ekranda hata banner’ı yoksa özellikle oluşturmayın
- Bildirim, ticket ve Gmail verileri gerçek ama güvenli örneklerden seçilsin

## Slayt 9 İçin En Güçlü Görseller

Öncelik sırası:

1. Customer ticket detay ekranı
2. Technical/Admin ticket yönetim ekranı
3. Admin Gmail entegrasyonu ekranı
4. Login ekranı

Alternatif / yedek:

- Customer dashboard
- Notifications ekranı
- Profile ekranı

## Ekran Görüntüsü Checklist

### 1. Login Ekranı

- Kullanıcı rolü: Giriş yapılmamış
- Route / sayfa: `/login`
- Görünmesi gerekenler:
  - SupportFlow logosu
  - giriş formu
  - e-posta ve şifre alanları
  - giriş butonu
  - kayıt ekranına yönlendiren bağlantı
- Görünmemesi gerekenler:
  - tarayıcı şifre öneri penceresi
  - validation hata mesajı
  - backend bağlantı hatası
- Önerilen kadraj:
  - form ekranın merkezinde
  - sol/sağ görsel alanlar dengeli kalmalı
- Kullanılacağı slayt: `Slide 9`
- Öncelik: Orta

### 2. Customer Dashboard

- Kullanıcı rolü: `Customer`
- Route / sayfa: `/customer`
- Görünmesi gerekenler:
  - “Hoş geldiniz” başlığı
  - özet kartları
  - son talepler bölümü
  - sol menüde müşteri navigasyonu
- Görünmemesi gerekenler:
  - boş liste durumu, mümkünse
  - hatalı yüklenme mesajları
- Önerilen kadraj:
  - sol menü + ana içerik aynı karede
  - üst metrik kartları görünür olsun
- Kullanılacağı slayt: `Slide 9` veya yedek
- Öncelik: Orta

### 3. Customer Ticket Detay + Attachment

- Kullanıcı rolü: `Customer`
- Route / sayfa: `/customer/tickets/{attachment görünen ticket id}`
- Görünmesi gerekenler:
  - ticket numarası ve başlığı
  - durum ve öncelik chip’leri
  - “Ek Dosyalar” bölümü
  - dosya adı ve mümkünse görsel thumbnail
  - konuşma alanı
- Görünmemesi gerekenler:
  - “bazı ek dosyalar yüklenemedi” uyarısı
  - boş attachment alanı
  - debug benzeri metinler
- Önerilen kadraj:
  - başlık + ek dosya alanı + konuşma bloğu aynı ekranda
  - mümkünse sağ taraftaki bilgi paneli de görünsün
- Kullanılacağı slayt: `Slide 9`
- Öncelik: Çok yüksek

### 4. Attachment Preview Açılır Görünüm

- Kullanıcı rolü: `Customer`
- Route / sayfa: `/customer/tickets/{same ticket id}`
- Görünmesi gerekenler:
  - ek dosya önizleme modalı veya görsel açılımı
  - arka planda ticket ekranının bir kısmı
- Görünmemesi gerekenler:
  - tarayıcı download çubuğu
  - dosya sistemi yolu
- Önerilen kadraj:
  - önizleme pencereyi baskın şekilde kaplasın
  - arka planın SupportFlow olduğu anlaşılmalı
- Kullanılacağı slayt: `Slide 9` için alternatif kırpılmış görsel
- Öncelik: Orta

### 5. Technical Ticket Yönetim Ekranı

- Kullanıcı rolü: `TechnicalStaff`
- Route / sayfa: `/technical/tickets/{aktif ticket id}`
- Görünmesi gerekenler:
  - ticket başlığı ve ticket numarası
  - müşteri bilgisi
  - atama alanı
  - durum güncelleme alanı
  - konuşma akışı
  - iç not / teknik işlem alanı varsa görünür bölümler
- Görünmemesi gerekenler:
  - yetki hatası
  - boş state
  - müşteriye ait olmayan gizli alanları temsil etmeyen eksik görünüm
- Önerilen kadraj:
  - mümkünse atama kartı ile konuşma alanı aynı karede
  - operasyonel görünüm hissi vermeli
- Kullanılacağı slayt: `Slide 9`
- Öncelik: Çok yüksek

### 6. Admin Gmail Entegrasyonu

- Kullanıcı rolü: `Admin`
- Route / sayfa: `/admin/gmail`
- Görünmesi gerekenler:
  - “Gmail Entegrasyonu” başlığı
  - mailbox bilgisi
  - capability kartları: yapılandırma, yetkilendirme, okuma, gönderme
  - eylem butonları
  - varsa işleme özeti
- Görünmemesi gerekenler:
  - OAuth URL
  - token, secret veya credential benzeri bilgiler
  - hatalı durum mesajları, eğer sunum amacıyla özellikle gösterilmiyorsa
- Önerilen kadraj:
  - üst durum kartları ile eylem alanı aynı karede
  - çok uzun sayfada aşağı kaydırıp özeti ayrıca ikinci kare olarak almak mümkün
- Kullanılacağı slayt: `Slide 8` veya `Slide 9`
- Öncelik: Çok yüksek

### 7. Notifications Ekranı

- Kullanıcı rolü: `TechnicalStaff` veya `Admin`
- Route / sayfa: `/technical/notifications` veya `/admin/notifications`
- Görünmesi gerekenler:
  - “Bildirimler” başlığı
  - okunmamış sayı chip’i
  - birden fazla bildirim kartı
  - ticket’a git butonu
- Görünmemesi gerekenler:
  - boş bildirim durumu
  - tümü okunmuş ve görsel olarak zayıf liste, mümkünse
- Önerilen kadraj:
  - üst filtre butonları ve birkaç bildirim kartı birlikte görünmeli
- Kullanılacağı slayt: `Slide 7` veya `Slide 9` için yedek
- Öncelik: Orta

### 8. Profile Ekranı

- Kullanıcı rolü: `Customer`, `TechnicalStaff` veya `Admin`
- Route / sayfa: rolüne göre `/customer/profile`, `/technical/profile`, `/admin/profile`
- Görünmesi gerekenler:
  - “Profilim” başlığı
  - kullanıcı kartı
  - profil bilgileri formu
  - şifre değiştirme alanı
- Görünmemesi gerekenler:
  - kişisel olmayan test verileri
  - validation hata mesajı
- Önerilen kadraj:
  - üst özet kartı ve profil formu birlikte görünmeli
- Kullanılacağı slayt: yedek / soru-cevap desteği
- Öncelik: Düşük-Orta

## 3–4 Ekranla Tek Slayt Yerleşim Önerisi

### Öneri A — En dengeli yerleşim

- Büyük görsel: Customer Ticket Detay + Attachment
- Sağ üst küçük görsel: Login
- Sağ orta küçük görsel: Technical Ticket Yönetimi
- Sağ alt küçük görsel: Admin Gmail Entegrasyonu

### Öneri B — Daha teknik odaklı yerleşim

- Sol büyük görsel: Technical/Admin Ticket Yönetimi
- Sağ üst görsel: Customer Ticket Detay
- Sağ alt görsel: Admin Gmail Entegrasyonu

## Çekimden Önce Son Kontrol

- İlgili role giriş yapıldı mı
- Ekranda veri dolu mu
- Hata banner’ı görünmüyor mu
- Gizli bilgi yok mu
- Sekme/yer imi kalabalığı temizlendi mi
- Ekran çözünürlüğü sunuma uygun mu
- Aynı ticket hem müşteri hem staff tarafında hazır mı
