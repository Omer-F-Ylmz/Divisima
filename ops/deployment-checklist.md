# Production Deployment Kontrol Listesi

## Feature flag'ler (appsettings / env) - PRODUCTION'DA AÇILACAK
| Flag | Dev | Production |
|------|-----|------------|
| `Iyzico:UseRealSdk` | false | **true** (gerçek Iyzipay anahtarlarıyla) |
| `Captcha:Enabled` | false | **true** (gerçek Turnstile secret) |
| `Redis:Enabled` | false | **true** (dağıtık cache/lock/blacklist) |
| `Vault:Enabled` | false | **true** (secret'lar Key Vault'ta) |

## Secret'lar (Key Vault'a - appsettings'te ASLA)
- `TokenOptions--SecurityKey` (256-bit)
- `Encryption--Key` (32 byte base64)
- `Iyzico--ApiKey`, `Iyzico--SecretKey`
- `Captcha--SecretKey`
- `ConnectionStrings--DivisimaDb`

## Zorunlu adımlar
- [ ] `Iyzico:BaseUrl` = `https://api.iyzipay.com` (sandbox değil)
- [ ] `Webhook:AllowedIps` = Iyzico production IP aralıkları
- [ ] `AllowedOrigins` = yalnız gerçek frontend domain(ler)i
- [ ] DB kullanıcısı en az yetki (SELECT/INSERT/UPDATE; DDL/DROP yok)
- [ ] `dotnet list package --vulnerable` temiz
- [ ] Serilog SIEM sink aktif (bkz. serilog-siem.md)
- [ ] Hangfire dashboard yetkilendirme (yalnız admin - şu an açık!)
- [ ] Rate limit eşikleri prod trafiğine göre ayarlandı
