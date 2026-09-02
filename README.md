# SupportFlow

<p align="center">
  <strong>Rol tabanlı, çok kanallı müşteri destek ve talep yönetim platformu</strong>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white" alt=".NET 10" />
  <img src="https://img.shields.io/badge/React-TypeScript-3178C6?logo=react&logoColor=white" alt="React TypeScript" />
  <img src="https://img.shields.io/badge/React%20Native-Expo-000020?logo=expo&logoColor=white" alt="React Native Expo" />
  <img src="https://img.shields.io/badge/SQL%20Server-EF%20Core-CC2927?logo=microsoftsqlserver&logoColor=white" alt="SQL Server EF Core" />
  <img src="https://img.shields.io/badge/Gmail-OAuth%202.0-EA4335?logo=gmail&logoColor=white" alt="Gmail OAuth 2.0" />
</p>

SupportFlow; müşteri, teknik ekip ve yönetici rollerinin destek taleplerini tek bir merkezden yönetebilmesi için geliştirilmiş full-stack bir destek platformudur. Web, mobil ve e-posta kanallarından oluşan akışı; talep yönetimi, mesajlaşma, ek dosyalar, bildirimler, sohbetler ve Gmail entegrasyonu ile bir araya getirir.

> **Proje durumu:** Web ve mobil uygulama akışları tamamlandı; backend doğrulamaları, fiziksel Android testleri ve final teslim kontrolleri gerçekleştirildi. Proje demo/staj sunumu için hazırdır.

## İçindekiler

- [Proje Özeti](#proje-özeti)
- [Öne Çıkan Özellikler](#öne-çıkan-özellikler)
- [Kullanıcı Rolleri](#kullanıcı-rolleri)
- [Sistem Mimarisi](#sistem-mimarisi)
- [Teknoloji Yığını](#teknoloji-yığını)
- [Proje Yapısı](#proje-yapısı)
- [Kurulum ve Çalıştırma](#kurulum-ve-çalıştırma)
- [Gmail Entegrasyonu](#gmail-entegrasyonu)
- [Güvenlik](#güvenlik)
- [Test ve Doğrulama](#test-ve-doğrulama)
- [Demo APK](#demo-apk)
- [Üretime Geçiş Notları](#üretime-geçiş-notları)
- [Dokümantasyon](#dokümantasyon)

## Proje Özeti

SupportFlow'un temel amacı, farklı kanallardan gelen müşteri destek taleplerini izlenebilir ve yetkilendirilmiş tek bir operasyon akışında toplamaktır.

- **Web uygulaması**, destek operasyonlarının ana yönetim merkezi olarak tasarlanmıştır.
- **Mobil uygulama**, Customer, TechnicalStaff ve Admin rollerinin hareket halindeyken sisteme erişmesini sağlar.
- **ASP.NET Core Web API**, iş kurallarının, veri erişiminin, yetkilendirmenin ve entegrasyonların merkezidir.
- **Gmail entegrasyonu**, e-posta üzerinden gelen taleplerin mevcut destek akışıyla ilişkilendirilmesini sağlar.

## Öne Çıkan Özellikler

- JWT tabanlı kimlik doğrulama ve backend seviyesinde rol bazlı yetkilendirme
- Müşteri talebi oluşturma, görüntüleme, mesajlaşma ve ek dosya yönetimi
- Teknik ekip için talep atama, durum ve öncelik güncelleme
- Public yanıt ve ekip içi not ayrımı
- Kategori, etiket ve talep sınıflandırma yönetimi
- Bildirim sistemi ve okunmamış bildirim takibi
- Merkezi sohbet görünümü; okunmamış, yanıt bekleyen ve pinli konuşmalar
- Talep aktivite zaman çizelgesi ve durum geçmişi
- SLA, queue aging ve agent workload operasyon görünümü
- Güvenli ek dosya yükleme, yetkili indirme ve mobil paylaşım/önizleme
- Gmail OAuth 2.0 bağlantısı ve gelen e-posta işleme
- E-posta/talep ilişkilendirme ve kalıcı tekrar işleme engelleme
- Gmail rate-limit durumları için cooldown/backoff yaklaşımı
- Web ve mobil istemcilerde role göre ayrıştırılmış kullanıcı deneyimi

## Kullanıcı Rolleri

| Rol | Yetkiler |
| --- | --- |
| **Customer** | Kendi taleplerini oluşturur ve görüntüler; mesaj, ek dosya ve bildirim akışlarını kullanır. |
| **TechnicalStaff** | Talep kuyruğunda çalışır; atama, durum, öncelik, public yanıt, iç not ve sohbet özelliklerini kullanır. |
| **Admin** | Operasyon görünürlüğüne ek olarak kullanıcıları, sınıflandırma verilerini ve Gmail entegrasyonunu yönetir. |

Yetki kontrolleri yalnızca arayüzde değil, API tarafında da uygulanır.

## Sistem Mimarisi

```text
┌──────────────────────────┐          ┌──────────────────────────┐
│ React + TypeScript Web   │          │ React Native + Expo      │
│ Customer / Staff / Admin │          │ Customer / Staff / Admin │
└─────────────┬────────────┘          └─────────────┬────────────┘
              │                                     │
              └─────────────────┬───────────────────┘
                                │ HTTPS / REST + JWT
                                ▼
                   ┌──────────────────────────┐
                   │ ASP.NET Core Web API     │
                   │ Auth • Business Rules   │
                   │ Gmail • Attachments     │
                   └────────────┬─────────────┘
                                │
                     ┌──────────┴──────────┐
                     ▼                     ▼
             ┌───────────────┐     ┌─────────────────┐
             │ SQL Server    │     │ Gmail API       │
             │ EF Core       │     │ OAuth 2.0       │
             └───────────────┘     └─────────────────┘
```

İstemciler veritabanına doğrudan erişmez. İş kuralları, rol kontrolleri ve Gmail OAuth token/credential işlemleri backend tarafında tutulur.

## Teknoloji Yığını

| Katman | Teknolojiler |
| --- | --- |
| **Backend** | ASP.NET Core, .NET 10, EF Core, SQL Server, JWT Bearer Authentication |
| **Backend Tests** | xUnit |
| **Web** | React, TypeScript, Vite, Material UI |
| **Mobile** | React Native, Expo SDK 54, TypeScript, Expo Router, Expo SecureStore |
| **Entegrasyon** | Gmail API, OAuth 2.0 |
| **Veri Yönetimi** | Entity Framework Core Migrations |

## Proje Yapısı

```text
SupportFlow/
├── backend/
│   ├── SupportFlow.Api/          # ASP.NET Core Web API
│   └── SupportFlow.Api.Tests/    # Backend test projesi
├── frontend/
│   └── supportflow-web/          # React web uygulaması
├── mobile/
│   └── supportflow-mobile/       # React Native / Expo uygulaması
├── docs/                         # Teknik, demo ve teslim dokümantasyonu
├── .config/                      # .NET tool yapılandırması
├── .gitignore
└── README.md
```

Build çıktıları, bağımlılık klasörleri, OAuth token/credential dosyaları, yerel environment dosyaları, yüklenen dosyalar, signing anahtarları ve teslim artifact'ları kaynak kontrolüne dahil edilmez.

## Kurulum ve Çalıştırma

### Gereksinimler

- .NET SDK 10
- Node.js ve npm
- SQL Server
- Mobil Android geliştirme için JDK 17 ve Android SDK
- Gmail entegrasyonu kullanılacaksa Google Cloud OAuth yapılandırması

> Gizli değerleri repository içine yazmayın. JWT anahtarı, Gmail credential'ları ve ortama özel ayarlar için User Secrets veya güvenli environment variable kullanın.

### Backend

```powershell
cd backend/SupportFlow.Api
dotnet restore
dotnet run --launch-profile http --urls "http://0.0.0.0:5135"
```

Yerel SQL Server connection string ve JWT ayarlarını geliştirme ortamınızda yapılandırın.

### Web

```powershell
cd frontend/supportflow-web
npm ci
npm run dev
```

API hedefini gerektiğinde `VITE_API_BASE_URL` ile yapılandırın.

### Mobile

Telefon üzerinden LAN geliştirme/demosu için bilgisayar ve telefon aynı erişilebilir ağda olmalıdır.

```powershell
cd mobile/supportflow-mobile
npm ci
$env:EXPO_PUBLIC_API_URL="http://<PC_LAN_IP>:5135"
npx expo start --dev-client --lan
```

API adresi kaynak koda sabitlenmez. `supportflowmobile` custom scheme'i mobil Gmail OAuth dönüş akışında kullanılır.

## Gmail Entegrasyonu

SupportFlow, Gmail API ile OAuth 2.0 tabanlı bir entegrasyon kullanır.

```text
Admin → OAuth Başlat
      → Google Authorization
      → Backend Callback
      → Token backend'de güvenli saklama
      → Gmail Integration Ready
```

Temel tasarım kararları:

- OAuth client secret ve token'lar yalnızca backend tarafındadır.
- Mobil istemciye token veya authorization code gönderilmez.
- Gmail yönetimi Admin rolüyle sınırlandırılmıştır.
- Manuel **Gelen Mailleri İşle** akışı desteklenir.
- Otomatik background sync varsayılan olarak kapalıdır.
- Gelen iletiler talep/konuşma akışıyla ilişkilendirilebilir.
- İşlenen Gmail mesajları kalıcı olarak takip edilerek tekrar işleme engellenir.
- 429/rate-limit durumları için cooldown/backoff uygulanır.

## Güvenlik

Projede uygulanan başlıca güvenlik yaklaşımları:

- JWT Bearer Authentication
- Backend seviyesinde rol bazlı authorization
- Mobil oturum verileri için Expo SecureStore
- OAuth token ve client secret bilgilerinin backend'de tutulması
- Ek dosyalara yetki kontrollü erişim
- Hassas dosyaların `.gitignore` ile kaynak kontrolünden çıkarılması
- Production secret'larının repository dışında tutulması

## Test ve Doğrulama

Final doğrulama sürecinde backend test paketi **521/521** başarıyla tamamlanmıştır. Backend build işlemi **0 warning / 0 error** ile sonuçlanmış; web ve mobil uygulamalarda lint, TypeScript ve build/export kontrolleri gerçekleştirilmiştir.

```powershell
# Backend
dotnet build backend/SupportFlow.Api/SupportFlow.Api.csproj
dotnet test backend/SupportFlow.Api.Tests/SupportFlow.Api.Tests.csproj

# Web
cd frontend/supportflow-web
npm ci
npm run lint
npm run build

# Mobile
cd mobile/supportflow-mobile
npm ci
npx tsc --noEmit
npm run lint
npx expo config --type public
npx expo export --platform android
```

Ek olarak uygulama fiziksel Android cihaz üzerinde Customer, TechnicalStaff ve Admin rollerinin ana akışlarıyla test edilmiştir.

## Demo APK

`SupportFlow-1.0.0-M8.2-demo.apk`, kontrollü yerel demo amacıyla üretilmiş Android APK artifact'ıdır.

- Demo/test dağıtımı içindir.
- Play Store production release değildir.
- Kaynak repository'ye dahil edilmez.
- Final teslim paketinde ayrı artifact olarak tutulur.

## Üretime Geçiş Notları

Production ortamına geçmeden önce aşağıdaki operasyonel adımlar tamamlanmalıdır:

- Kararlı HTTPS API endpoint'i ve alan adı
- Google Cloud'da production OAuth callback adresi
- Organizasyona ait Android application/package ID
- Production signing keystore ve AAB üretim süreci
- CI/CD ve güvenli secret yönetimi
- Monitoring, logging ve backup politikaları
- Gmail kota/rate-limit yönetiminin production trafik altında doğrulanması
- Background sync stratejisinin production gereksinimlerine göre yapılandırılması

## Dokümantasyon

Daha ayrıntılı teknik bilgi için:

- [Proje Özeti](docs/PROJECT_SUMMARY.md)
- [Sistem Mimarisi](docs/ARCHITECTURE.md)
- [API Referansı](docs/API_REFERENCE.md)
- [Gmail Entegrasyonu](docs/GMAIL_INTEGRATION.md)
- [Güvenlik ve Roller](docs/SECURITY_AND_ROLES.md)
- [Web Uygulaması](docs/PC_WEB_APPLICATION.md)
- [Mobil Uygulama](docs/MOBILE_APPLICATION.md)
- [Demo Rehberi](docs/DEMO_GUIDE.md)
- [Demo Kontrol Listesi](docs/DEMO_CHECKLIST.md)
- [Final Proje Sonucu](docs/FINAL_PROJECT_RESULT.md)
- [Final Teslim Kontrol Listesi](docs/FINAL_DELIVERY_CHECKLIST.md)

---

<p align="center">
  <strong>SupportFlow</strong><br />
  Full-stack Customer Support & Ticket Management Platform
</p>
