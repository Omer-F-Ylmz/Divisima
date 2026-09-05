# SIEM / Merkezi Log Entegrasyonu

> **DURUM: BU BELGE BİR TARİFTİR, KURULU BİR ENTEGRASYON DEĞİL. OKUYUCU YOKTUR.**
> Aktif Serilog sink'leri yalnız Console ve File'dır (`Program.cs`) ve
> `Serilog.Sinks.Elasticsearch` / `Serilog.Sinks.Seq` paket referansı **0**'dır
> (`Divisima.API.csproj`). Critical olaylarda tetiklenen SignalR `"admins"` alarmı
> **BOŞ GRUBA** yayın yapar — `NotificationHub.JoinAdminGroup()` çağıranı yoktur
> (istemci tarafında SignalR 0 geçiş). SIEM launch sonrasıdır.
>
> **NOT (GF-5/K6):** her iki aktif sink de artık `MaskeliFormatter`dan geçer. SIEM sink'i
> eklenirse **o da aynı formatter'ı almalıdır**; aksi halde EF Core / SQL Server kaynaklı
> ham istisna metinleri (ör. `Truncated value: '...'`) maskesiz olarak SIEM'e akar.

`SecurityEventManager` structured log üretir; SIEM bağlandığında bunlar akıtılacaktır.
Program.cs'teki Serilog yapılandırmasına sink eklenir (paketler:
`Serilog.Sinks.Elasticsearch` veya `Serilog.Sinks.Seq`).

## Elasticsearch/OpenSearch sink
```csharp
builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/divisima-.log", rollingInterval: RollingInterval.Day)
    // SIEM: güvenlik olayları Elasticsearch'e
    .WriteTo.Elasticsearch(new ElasticsearchSinkOptions(new Uri(ctx.Configuration["Siem:ElasticUri"]))
    {
        IndexFormat = "divisima-security-{0:yyyy.MM.dd}",
        MinimumLogEventLevel = Serilog.Events.LogEventLevel.Warning  // SECURITY olayları Warning+
    }));
```

## Alerting kuralları (SIEM tarafında) — HEDEF DURUM
> Ölçüldü: SIEM bağlı olmadığı için bugün hiçbiri koşmuyor. **Veri temeli GF-5 ile değişti** —
> `ip_address` artık her olayda doluyor (K1), 429 reddi ve sahiplik ihlali artık olay yazıyor
> (K2). Aşağıdaki tabloda her kuralın **bugünkü veri temeli** ayrıca belirtilmiştir.

| Kural | Eşik | Aksiyon | Veri temeli (GF-5 sonrası) |
|-------|------|---------|----------------------------|
| Aynı IP'den başarısız login | 10/dk | IP geçici blokla + uyarı | **VAR** — `ip_address` K1 ile doluyor; kayıtsız e-posta denemesi de `LoginFailed` yazıyor (`customer_id` NULL) |
| Aynı hesap AccountLocked | 3/saat | Hesap sahibine mail + SOC bildirimi | **VAR** (GF-5 öncesinde de vardı) |
| PaymentFraud / AmountMismatch | herhangi | Anında SOC alarmı | **YOK** — bu iki tip kodda hâlâ ÜRETİLMİYOR (yalnız `Messages.*` sabiti olarak geçer) |
| `PaymentSignatureInvalid` | herhangi | SOC alarmı + sağlayıcı imza biçimi kontrolü | **VAR** — K2 ile eklendi (`IyzicoPaymentManager`) |
| `IdorAttempt` (sahiplik ihlali, **404**) | 5/dk | Kullanıcı oturumu incele + uyarı | **KISMEN** — K2 ile Order + Payment uçlarında yazılıyor; kalan yedi manager BİLİNEN boşluk |
| `RateLimitExceeded` | 5/dk | IP incele | **VAR** — K2 ile eklendi; 60 sn örnekleme (kova+IP başına TEK satır) |
| Yeni ülke/cihazdan login | herhangi | Kullanıcıya doğrulama maili | **YOK** — `NewDeviceLogin` üretilmiyor |

> **Durum kodu düzeltmesi:** eski tablo `IdorAttempt`i "403 sahiplik" diye yazıyordu.
> Sahiplik ihlalinde tek sözleşme **404**'tür (GF-1/K4) — varlık sızdırılmaz.

## Güvenlik olay tipleri
### BUGÜN KODDA ÜRETİLEN (ölçüldü)
`LoginFailed` (kayıtlı **ve** kayıtsız e-posta; ikisi `customer_id`nin dolu/NULL olmasıyla
ayrılır) · `AccountLocked` · `ChangePasswordFailed` · `AccountDeleted` · `TwoFactorChallenge` ·
`TwoFactorFailed` · `RefreshTokenReuse` · `ResetPassword` · `Logout` (GF-5/K2) ·
`IdorAttempt` (GF-5/K2) · `RateLimitExceeded` (GF-5/K2) · `PaymentSignatureInvalid` (GF-5/K2)
— hepsi `severity` (Info/Warning/Critical) ile.

### BU BELGENİN ESKİDEN SAYDIĞI AMA HÂLÂ ÜRETİLMEYEN TİPLER
`PaymentFraud`, `PaymentAmountMismatch` — bu adlar kodda YALNIZ müşteriye dönen metin sabiti
olarak geçer (`Messages.PaymentFraudReject`, `Messages.PaymentAmountMismatch`),
`security_events` tipi olarak DEĞİL.
`NewDeviceLogin` — kodda yalnız `SecurityEvent.cs` YORUMUNDA geçer.

> Ölçüm notu: `LogAsync("` çapası tek başına yetmez — iki çağrı yeri tipi **ternary** ile
> seçer (`AuthManager.cs`, `AccountManager.cs`) ve o çapa onları kaçırır.
