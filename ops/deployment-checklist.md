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

## Frontend origin'i - HER YAYINDA (DALGA-4-FIX-2 / M1)

Storefront ve admin panelinin API tabanı **kaynakta sabit gömülüdür** ve dağıtımda
yazılmalıdır. Depoda commit'li değer `http://localhost:5000`'dir; yerelde hiçbir ek adım
gerekmez, **yayında ise bu adım atlanırsa istekler son kullanıcının kendi makinesine
gider ve katalog boş gelir** (ölçüldü).

```bash
ops/set-api-origin.sh https://api.divisima.com   # yaz
ops/set-api-origin.sh --verify                   # doğrula (tutarsızsa exit 1)
```

Betik **tek girdiden** hem `meta[name="divisima-api-origin"]` değerini hem de CSP'nin
`img-src` / `connect-src` / `form-action` direktiflerini yazar - elle senkron YOKTUR.
Sayfa açılırken bir **tutarlılık guard'ı** aynı kontrolü tarayıcıda tekrarlar ve
uyuşmazlıkta ekrana kırmızı bir uyarı basar (sessizce yanlış origin'e düşmez).

- [ ] `ops/set-api-origin.sh <origin>` koşuldu ve `--verify` **exit 0** verdi
- [ ] Backend `Iyzico:CallbackUrl` origin'i **aynı** origin (form-action senkron kuralı -
      callback POST'u tarayıcıdan gelir; uyuşmazsa ödeme sonucu sessizce kaybolur)
- [ ] `frontend/service-worker.js` içindeki `VERSION` bump'landı (eski önbellek temizlensin)
- [ ] Yayın sonrası: storefront gerçek adresinden açıldı, katalog **dolu** geldi ve
      konsolda `[DIVISIMA YAPILANDIRMA]` satırı **yok**

## Zorunlu adımlar
- [ ] `Iyzico:BaseUrl` = `https://api.iyzipay.com` (sandbox değil)
- [ ] `Webhook:AllowedIps` = Iyzico production IP aralıkları
- [ ] `AllowedOrigins` = yalnız gerçek frontend domain(ler)i
- [ ] DB kullanıcısı en az yetki (SELECT/INSERT/UPDATE; DDL/DROP yok)
- [ ] `dotnet list package --vulnerable` temiz
- [ ] Serilog SIEM sink aktif (bkz. serilog-siem.md)
- [ ] Hangfire dashboard yetkilendirme (yalnız admin - şu an açık!)
- [ ] Rate limit eşikleri prod trafiğine göre ayarlandı
