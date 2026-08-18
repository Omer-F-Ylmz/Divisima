# SIEM / Merkezi Log Entegrasyonu

`SecurityEventManager` structured log üretir; bunlar SIEM'e akıtılır. Program.cs'teki Serilog
yapılandırmasına sink eklenir (paketler: `Serilog.Sinks.Elasticsearch` veya `Serilog.Sinks.Seq`).

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

## Alerting kuralları (SIEM tarafında)
| Kural | Eşik | Aksiyon |
|-------|------|---------|
| Aynı IP'den başarısız login | 10/dk | IP geçici blokla + uyarı |
| Aynı hesap AccountLocked | 3/saat | Hesap sahibine mail + SOC bildirimi |
| PaymentFraud / AmountMismatch | herhangi | Anında SOC alarmı |
| IdorAttempt (403 sahiplik) | 5/dk | Kullanıcı oturumu incele + uyarı |
| Yeni ülke/cihazdan login | herhangi | Kullanıcıya doğrulama maili |

## Güvenlik olay tipleri (kodda üretilen)
`LoginFailed`, `AccountLocked`, `AccountDeleted`, `PaymentFraud`, `PaymentAmountMismatch`,
`PaymentSignatureInvalid`, `IdorAttempt`, `NewDeviceLogin` — hepsi `severity` (Info/Warning/Critical) ile.
