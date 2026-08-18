# Divisima Backend

Katmanlı mimariyle yazılmış **ASP.NET Core 8 (net8.0)** e-ticaret backend'i.
Kadın moda perakendesi (Divisima) domain'i için ürün/kategori, sepet, sipariş, stok
rezervasyonu, kupon, Iyzico 3DS ödeme, iade, fatura/e-fatura, kargo takibi ve
admin dashboard uçlarını sağlar. JWT kimlik doğrulama, Hangfire arka plan işleri,
SignalR bildirim ve Swagger dokümantasyonu içerir.

## Katman yapısı

| Katman | Sorumluluk |
|---|---|
| `Divisima.Core` | Çekirdek soyutlamalar: `IEntity`/`IDto`, Result pattern, generic repository, `UnitOfWork`, JWT & hashing helper'ları, cache/mail/SMS/push soyutlamaları, Iyzico & e-fatura entegrasyon istemcileri |
| `Divisima.Entity` | Entity'ler (düz, navigation property'siz) ve DTO'lar |
| `Divisima.Dal` | `I{X}Dal` / `Ef{X}Dal`, `DivisimaDbContext`, EF Core konfigürasyonu, UnitOfWork implementasyonu |
| `Divisima.Bussiness` | `I{X}Service` / `{X}Manager` iş kuralları, AutoMapper, FluentValidation, event pipeline + Outbox, Autofac DI modülü |
| `Divisima.API` | İnce controller'lar, `Program.cs`, middleware'ler (exception, security headers, rate limit, CSRF), SignalR hub, Swagger |
| `Divisima.IntegrationTests` | `WebApplicationFactory` + Testcontainers ile gerçek SQL Server'a karşı entegrasyon testleri |

Bağımlılık yönü: `API → Bussiness → Dal → Entity → Core`

## Kurulum

### 1. Gereksinimler
- .NET 8 SDK (veya üzeri)
- SQL Server (yerelde LocalDB/Express yeterli) — alternatif olarak `docker compose up`
- (Opsiyonel) Redis — cache ve dağıtık rate limit için

### 2. Secret'lar nereye yazılır

Depodaki `Divisima.API/appsettings.json` **hiçbir gerçek secret içermez**; hassas alanlar
`CHANGE_ME` placeholder'ıyla gelir. Gerçek değerleri şuraya yazın:

```
Divisima.API/appsettings.Development.json   ← .gitignore'da, commit EDİLMEZ
```

Şablonu kopyalayarak başlayın:

```bash
cp Divisima.API/appsettings.Development.example.json Divisima.API/appsettings.Development.json
```

Doldurulması gereken alanlar:

| Anahtar | Açıklama |
|---|---|
| `ConnectionStrings:DivisimaDb` | SQL Server bağlantı cümlesi |
| `TokenOptions:SecurityKey` | JWT imzalama anahtarı — HS256 için **en az 32 bayt** (`openssl rand -base64 48`) |
| `Encryption:Key` | AES-256-GCM alan şifreleme anahtarı — **tam 32 bayt, geçerli base64** (`openssl rand -base64 32`) |
| `Iyzico:ApiKey` / `Iyzico:SecretKey` | Iyzico ödeme anahtarları (sandbox veya prod) |
| `MailSettings:*` | SMTP sunucu ve kimlik bilgileri |
| `Captcha:SecretKey` | Turnstile/reCAPTCHA gizli anahtarı |
| `Sms:*`, `Push:*`, `EInvoice:*` | Netgsm / FCM / e-fatura sağlayıcı ayarları (varsayılan: kapalı) |

`Program.cs` başlangıçta **fail-fast** doğrulama yapar: connection string boşsa veya JWT
anahtarı 32 bayttan kısaysa uygulama açılmaz. Production'da ayrıca `CHANGE_ME` gibi
placeholder değerler reddedilir.

**Production'da** `appsettings.Development.json` kullanılmaz. Secret'ları environment
değişkeni olarak verin (çift alt çizgi ile iç içe anahtar) veya Azure Key Vault'u açın
(`Vault:Enabled=true`, `Vault:Uri=...`):

```bash
export ConnectionStrings__DivisimaDb="Server=...;Database=...;User Id=...;Password=..."
export TokenOptions__SecurityKey="..."
export Encryption__Key="..."
export Iyzico__SecretKey="..."
```

### 3. Çalıştırma

```bash
dotnet restore
dotnet ef database update --project Divisima.Dal --startup-project Divisima.API
dotnet run --project Divisima.API
```

- Swagger: `https://localhost:56321/swagger`
- Health: `https://localhost:56321/health`
- Hangfire: `https://localhost:56321/hangfire`

Docker ile (SQL Server + Redis + API birlikte):

```bash
DB_PASSWORD='<güçlü-parola>' docker compose up
```

### 4. Test

```bash
dotnet test
```

## Diğer belgeler

- [DEVELOPMENT-NOTES.md](DEVELOPMENT-NOTES.md) — modül modül geliştirme geçmişi ve ayrıntılı özellik listesi
- [IMPROVEMENTS.md](IMPROVEMENTS.md) — production sertleştirme notları
- [SECURITY.md](SECURITY.md) — güvenlik politikası ve sorumlu açıklama
