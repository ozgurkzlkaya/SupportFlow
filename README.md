# SupportFlow

SupportFlow, müşteri destek taleplerini tek bir akışta yöneten çok kanallı bir destek platformudur. Müşteri, teknik ekip ve yönetici rollerinin talepleri; mesajlar, ek dosyalar, bildirimler, sohbetler ve Gmail entegrasyonu ile yönetmesine olanak sağlar.

## Proje Özeti

Proje; e-posta ve uygulama üzerinden gelen destek taleplerini izlenebilir, yetkilendirilmiş ve operasyonel olarak ölçülebilir bir yapıda toplar. Web uygulaması operasyon merkezi olarak, mobil uygulama ise müşteri ve ekiplerin sahadaki erişimi için tasarlanmıştır.

## Ana Özellikler

- JWT tabanlı kimlik doğrulama ve rol bazlı yetkilendirme
- Müşteri talep oluşturma, mesajlaşma, bildirim ve ek dosya akışı
- Teknik ekip için atama, durum, öncelik, sınıflandırma, iç not ve public yanıt işlemleri
- Yönetici için kullanıcı, kategori/etiket, Gmail ve operasyon görünürlüğü
- Sohbet merkezi, okunmamış/yanıt bekleyen/pinli konuşmalar
- Talep aktivite zaman çizelgesi, SLA, queue aging ve agent workload görünümü
- Güvenli ek dosya yükleme, yetkili indirme ve mobil paylaşım/önizleme akışı
- Gmail OAuth 2.0, gelen e-posta ilişkilendirme, ek alma ve kalıcı tekrar engelleme
- React Native/Expo mobil uygulaması ile Customer, TechnicalStaff ve Admin deneyimi

## Kullanıcı Rolleri

| Rol | Başlıca yetkiler |
| --- | --- |
| Customer | Kendi taleplerini, mesajlarını, eklerini ve bildirimlerini yönetir. |
| TechnicalStaff | Talep kuyruğunda çalışır; atama, durum/öncelik, public yanıt, iç not ve sohbetleri kullanır. |
| Admin | Operasyon görünürlüğüne ek olarak kullanıcı, sınıflandırma ve Gmail entegrasyonunu yönetir. |

## Mimari

```text
Mobile (React Native/Expo) ─┐
                            ├─> ASP.NET Core Web API ─> SQL Server
Web (React/TypeScript) ─────┘              │
                                           └─> Gmail API / OAuth 2.0
```

API iş kurallarını, yetkilendirmeyi ve entegrasyon gizli bilgilerini sunucu tarafında tutar. Web ve mobil istemciler yalnızca JWT ile korunan API sözleşmelerini kullanır.

## Teknoloji Yığını

- **Backend:** ASP.NET Core, .NET 10, EF Core, SQL Server, JWT, xUnit
- **Web:** React, TypeScript, Vite, Material UI
- **Mobile:** React Native, Expo SDK 54, TypeScript, Expo Router, Expo SecureStore
- **Entegrasyon:** Gmail API ve OAuth 2.0

## Depo Yapısı

```text
SupportFlow/
├── backend/                 ASP.NET Core API, testler ve EF Core migration'lar
├── frontend/                React web uygulaması
├── mobile/                  React Native / Expo mobil uygulaması
├── docs/                    Teknik, demo ve teslim dokümantasyonu
├── .artifacts/              Yerel, teslim amaçlı üretilen artifact'lar
└── README.md                Ana proje girişi
```

`bin/`, `obj/`, `node_modules/`, Expo/Gradle önbellekleri, IDE dosyaları, yerel veritabanı ayarları, OAuth token/credential dosyaları ve signing anahtarları teslim paketi veya kaynak kontrolü için değildir.

## Yerel Geliştirme Gereksinimleri

- .NET SDK 10
- Node.js ve npm
- SQL Server
- Android derlemesi için JDK 17 ve Android SDK
- Yerel API yapılandırması için connection string, JWT anahtarı ve gerekli Gmail OAuth ayarları

Gizli değerleri User Secrets, güvenli ortam değişkenleri veya kaynak kontrolü dışındaki yerel yapılandırmada saklayın.

## Backend'i Başlatma

```powershell
cd C:\Users\Costa\Desktop\SupportFlow\backend\SupportFlow.Api
dotnet run --launch-profile http --urls "http://0.0.0.0:5135"
```

## Web'i Başlatma

```powershell
cd frontend/supportflow-web
npm install
npm run dev
```

Web API hedefini `VITE_API_BASE_URL` ile yapılandırın.

## Mobile'ı Başlatma

Telefonla LAN demosu için mobil uygulamanın backend'e aynı ağ üzerinden ulaşabilmesi gerekir. API adresini kaynak koda sabitlemeyin:

```powershell
cd mobile/supportflow-mobile
$env:EXPO_PUBLIC_API_URL = "http://<PC_LAN_IP>:5135"
npx expo start --dev-client --clear
```

`supportflowmobile` özel şeması, mobil Gmail OAuth dönüş akışı için kullanılır. Telefon ve backend geliştirme makinesi aynı erişilebilir ağda olmalıdır.

## Gmail Entegrasyonu

- OAuth credential ve token'lar yalnızca backend tarafındadır; istemcilere taşınmaz veya commit edilmez.
- Gmail kontrolü Admin rolüyle sınırlıdır.
- Otomatik arka plan senkronizasyonu varsayılan olarak kapalıdır.
- Admin için manuel **Gelen Mailleri İşle** akışı desteklenir.
- Gmail 429 durumları için cooldown/backoff ve kalıcı işlenmiş mesaj tekrar engelleme mekanizmaları bulunur.
- Üretim ortamı, Google Cloud'a kayıtlı kararlı HTTPS callback ve güvenli secret yönetimi gerektirir.

## Test ve Doğrulama

```powershell
# Backend
dotnet build backend/SupportFlow.Api/SupportFlow.Api.csproj
dotnet test backend/SupportFlow.Api.Tests/SupportFlow.Api.Tests.csproj

# Web
cd frontend/supportflow-web
npm run lint
npm run build

# Mobile
cd mobile/supportflow-mobile
npx tsc --noEmit
npm run lint
npx expo config --type public
npx expo export --platform android
```

## Demo APK

`SupportFlow-1.0.0-M8.2-demo.apk`, kontrollü yerel demo için üretilmiş imzalı APK artifact'ıdır. Play Store/üretim dağıtımı değildir. Final teslim içindeki artifact konumu `artifacts/SupportFlow-1.0.0-M8.2-demo.apk` olacaktır.

## Üretim İçin Dikkat Edilecekler

- Kararlı HTTPS API ve OAuth callback alan adı
- Organizasyona ait Android package ID
- Güvenli CI/secret yönetimi ve production signing keystore
- AAB/Play Store signing süreci
- Dağıtım ortamına özel Gmail kota ve otomatik senkronizasyon doğrulaması

## Dokümantasyon

- [Proje Özeti](docs/PROJECT_SUMMARY.md)
- [Demo Rehberi](docs/DEMO_GUIDE.md)
- [Final Teslim Kontrol Listesi](docs/FINAL_DELIVERY_CHECKLIST.md)
- [Release ve Teslim Rehberi](docs/RELEASE_DELIVERY.md)
- [Demo Kontrol Listesi](docs/DEMO_CHECKLIST.md)
- [Mimari](docs/ARCHITECTURE.md)
- [Gmail Entegrasyonu](docs/GMAIL_INTEGRATION.md)
- [API Referansı](docs/API_REFERENCE.md)
