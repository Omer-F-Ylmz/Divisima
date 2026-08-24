using System.Text;
using System.Threading.RateLimiting;
using Asp.Versioning;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using Divisima.API.Hubs;
using Divisima.API.Middlewares;
using Divisima.API.Services;
using Divisima.Bussiness.DependencyResolvers.Autofac;
using Divisima.Bussiness.Mapping.AutoMapper;
using Divisima.Bussiness.Outbox;
using Divisima.Core.Integrations.Iyzico;
using Divisima.Core.Security.Authorization;
using Divisima.Core.Security.Identity;
using Divisima.Core.Security.JWT;
using Divisima.Core.Utilities.Caching;
using Divisima.Core.Utilities.Enums;
using Divisima.Core.Utilities.Mail;
using Divisima.Core.Utilities.Notifications;
using Divisima.Core.Utilities.Results;
using Divisima.DataAccess.Concrete.Context;
using Divisima.DataAccess.Interceptors;
using FluentValidation;
using FluentValidation.AspNetCore;
using Hangfire;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Polly;
using Polly.Extensions.Http;
using Serilog;

// SPRINT 8 MADDE 13 - KULTUR PINLEME (TEK NOKTA).
//
// OLCULEN SORUN: uygulama hicbir yerde kultur pinlemiyordu; para ve tarih bicimlendirmesi
// KOSTUGU KABININ yereline gore degisiyordu. E3 run'inda bu CANLI ORTAMDA gorundu: GitHub
// kosucusu (Linux, LANG=C.UTF-8) invariant kulturde kostugu icin fatura govdesindeki tutar
// "1,049.70" olarak basildi ve tr bicimini bekleyen bir test kirildi.
//   OLCUM: tr-TR -> "549,90" / "1.049,70"   |   Invariant -> "549.90" / "1,049.70"
// URETIMDEKI ANLAMI: LANG verilmemis bir Linux dagitiminda Turk musteriye kesilen faturada
// tutar NOKTA ayracli yazilir. Bu bir gorunum meselesi degil - fatura mali bir beyandir.
//
// NEDEN "DefaultThreadCurrentCulture", "RequestLocalization" DEGIL (olculerek secildi):
//   * Magaza TEK PAZARLI (TR / TRY). Bicimlendirmenin istemcinin "Accept-Language" basligina
//     gore degismesi ISTENMEZ - Ingilizce tarayicidan siparis veren musteriye NOKTA ayracli
//     bir Turk faturasi cikardi.
//   * RequestLocalization yalnizca ISTEK hattini etkiler. Fatura, fiyat-dususu e-postasi ve
//     outbox islemleri ARKA PLAN islerinde (Hangfire) da uretiliyor; orada middleware yok ve
//     kultur yine kabin yereline duserdi. Tek nokta pinleme her ikisini de kapsar.
//
// ETKILENEN YUZEY (tarandi): OrderManager (fatura HTML'i - 11 tutar + 1 tarih),
// PriceDropManager (fiyat dususu e-postasi - 2 tutar). Iyzico entegrasyonu ZATEN acikca
// InvariantCulture kullaniyor (IyzicoClient), yani bu pinleme saglayiciya giden tutarlari
// ETKILEMEZ. "{Guid:N}" kullanimlari sayi bicimi DEGIL Guid bicimidir - ilgisiz.
var trCulture = new System.Globalization.CultureInfo("tr-TR");
System.Globalization.CultureInfo.DefaultThreadCurrentCulture = trCulture;
System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = trCulture;

var builder = WebApplication.CreateBuilder(args);

// Açıklayıcı yorum: Config fail-fast - kritik ayar eksik/zayıfsa uygulama BAŞLAMASIN (ilk istekte patlamak yerine).
{
    var cfg = builder.Configuration;
    var conn = cfg.GetConnectionString("DivisimaDb");
    if (string.IsNullOrWhiteSpace(conn))
        throw new InvalidOperationException("FATAL: Config - ConnectionStrings:DivisimaDb tanımlı değil.");
    var jwtKey = cfg["TokenOptions:SecurityKey"];
    if (string.IsNullOrWhiteSpace(jwtKey) || Encoding.UTF8.GetByteCount(jwtKey) < 32)
        throw new InvalidOperationException("FATAL: Config - TokenOptions:SecurityKey eksik veya 256 bit'ten kısa (HS256 için en az 32 bayt).");
    // Açıklayıcı yorum: Prod'da placeholder secret tespit et (yanlışlıkla gerçek secret'sız deploy önleme)
    if (!builder.Environment.IsDevelopment())
    {
        // "CHANGE_IN_PRODUCTION" SONRADAN EKLENDI: database/advanced_simulation.py bu dizgeyi
        // reddettigini DOGRULUYORDU ama C# listesinde YOKTU - simulasyon, kodda olmayan bir kurali
        // test ediyordu (sahte guvence). Simulasyonu zayiflatmak yerine kod guclendirildi.
        var placeholders = new[] { "CHANGE_ME", "CHANGE_IN_PRODUCTION", "placeholder", "your-", "xxxxx", "TODO" };
        if (placeholders.Any(p => (jwtKey ?? "").Contains(p, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("FATAL: Config - TokenOptions:SecurityKey placeholder değeri içeriyor (prod'da gerçek secret gerekli).");

        // Açıklayıcı yorum: ALAN ŞİFRELEME ANAHTARI - prod'da ZORUNLU.
        // AesEncryptionProvider anahtar boşsa SESSİZCE sabit bir metinden (SHA256("DIVISIMA_DEV_
        // ENCRYPTION_KEY")) anahtar türetiyor. Bu dev kolaylığı; prod'da çalışırsa tüm alan
        // şifrelemesi HERKESİN BİLEBİLECEĞİ bir anahtarla yapılır ve hiçbir uyarı çıkmaz.
        // Depodaki appsettings.json'da değer boş olduğu için bu sessiz düşüş gerçek bir risk.
        // Development'ta fallback korunur - yalnız prod'da açılış engellenir.
        var encKey = cfg["Encryption:Key"];
        if (string.IsNullOrWhiteSpace(encKey))
            throw new InvalidOperationException("FATAL: Config - Encryption:Key tanımlı değil (prod'da AES-256 anahtarı zorunlu; üret: openssl rand -base64 32).");
        byte[] encKeyBytes;
        try { encKeyBytes = Convert.FromBase64String(encKey); }
        catch (FormatException) { throw new InvalidOperationException("FATAL: Config - Encryption:Key geçerli base64 değil."); }
        if (encKeyBytes.Length != 32)
            throw new InvalidOperationException($"FATAL: Config - Encryption:Key AES-256 için TAM 32 bayt olmalı (bulunan: {encKeyBytes.Length}).");

        // Açıklayıcı yorum: MAIL SUNUCUSU - prod'da ZORUNLU.
        // SmtpMailService, Host boşsa gönderim yapmaz ve yalnız uyarı loglar (Development kolaylığı).
        // Prod'da bu sessiz davranış e-posta doğrulama, parola sıfırlama ve sipariş bildirimlerinin
        // HİÇ gitmemesi demektir - üstelik her çağıran başarı görür. Açılışta engellenir.
        if (string.IsNullOrWhiteSpace(cfg["MailSettings:Host"]))
            throw new InvalidOperationException("FATAL: Config - MailSettings:Host tanımlı değil (prod'da e-posta gönderimi zorunlu; Host boşsa hiçbir mail gitmez).");

        // SPRINT 8 MADDE 7 - ODEME CALLBACK ADRESI: prod'da ZORUNLU. (E2b'de OLCULDU)
        // IyzicoPaymentManager.Initialize, DTO'daki callback_url BOS geldiginde bu config
        // degerini kullanir; storefront o alani GONDERMIYOR. Deger bos kalirsa gercek Iyzico
        // BOS callbackUrl'i kabul etmiyor ve HER kart odemesi init'te 400 ile duser - musteri
        // yalnizca "Odeme baslatilamadi." goruru. E2b'de bu belirti birebir olculdu. Ilk odeme
        // denemesinde patlamak yerine ACILISTA durulur.
        var iyzicoCallback = cfg["Iyzico:CallbackUrl"];
        if (string.IsNullOrWhiteSpace(iyzicoCallback))
            throw new InvalidOperationException(
                "FATAL: Config - Iyzico:CallbackUrl tanımlı değil (prod'da zorunlu; boşsa her kart ödemesi init'te 400 ile düşer). " +
                "SENKRON UYARISI: bu adresin origin'i, storefront CSP'sindeki form-action listesiyle AYNI olmalıdır - " +
                "aksi halde Iyzico'nun sonuç POST'u tarayıcı tarafından engellenir, callback hiç ateşlenmez ve " +
                "'para çekildi ama sipariş Pending' durumu oluşur (E2b'de yaşandı).");
        // Adres MUTLAK ve HTTPS olmali: Iyzico goreli adrese POST edemez, duz HTTP'yi de kabul etmez.
        if (!Uri.TryCreate(iyzicoCallback, UriKind.Absolute, out var cbUri) || cbUri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException(
                $"FATAL: Config - Iyzico:CallbackUrl mutlak bir HTTPS adresi olmalı (bulunan: '{iyzicoCallback}').");
    }
}

// B5: Serilog - yapılandırılmış loglama (console + günlük dosya)
//
// ══ DALGA C / C4 - SAKLAMA SINIRLARI ACIKCA YAZILDI ══════════════════════════════════════
// ONCEKI HALI yalnizca `rollingInterval: Day` veriyordu ve gerisi Serilog.Sinks.File 5.0.0
// VARSAYILANLARINA birakiliyordu: fileSizeLimitBytes = 1 GB, rollOnFileSizeLimit = FALSE,
// retainedFileCountLimit = 31.
// TEHLIKELI OLAN "rollOnFileSizeLimit = false" IDI: bir gunun dosyasi 1 GB'a ulastiginda sink
// yeni dosyaya GECMEZ, yazmayi SESSIZCE BIRAKIR - yani en cok log uretilen (yani en cok sorun
// yasanan) gunde loglar tam da ihtiyac duyuldugu anda kesilir. Uzerine, bu depoda log dosyasi
// operatorun bakabilecegi tek teshis kanallarindan biri (Hangfire panosu erisilemez durumda -
// bkz. C4 / FailedJobDto).
//
// DataRetentionJob bu bosluğu KAPATMIYOR: o yalniz VERITABANINI temizler (oturum 90g,
// islenmis outbox 30g, guvenlik olayi 1y) - log DOSYALARINA hic dokunmaz.
//
// Degerler ACIKCA yaziliyor ki varsayilanlar surum yukseltmesiyle degistiginde davranis
// SESSIZCE kaymasin:
//   - gunluk dosya + 100 MB'da PARCALA (rollOnFileSizeLimit) -> yazma ASLA sessizce durmaz
//   - 30 gun saklama -> DataRetentionJob'un outbox penceresiyle ayni buyukluk
//   - shared: false (varsayilan) korunur - tek surec yaziyor
builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/divisima-.log",
        rollingInterval: RollingInterval.Day,
        rollOnFileSizeLimit: true,
        fileSizeLimitBytes: 100L * 1024 * 1024,
        retainedFileCountLimit: 30));

// B10: Secrets - environment değişkenleri (production'da JWT key + connection string buradan)
builder.Configuration.AddEnvironmentVariables();

// Autofac DI (Cafixo kalıbı)
builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());
builder.Host.ConfigureContainer<ContainerBuilder>(b => b.RegisterModule(new AutofacBusinessModule()));

// Açıklayıcı yorum: HttpContext erişimi (audit interceptor kullanıcı id'sini buradan alır)
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<AuditInterceptor>();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// Açıklayıcı yorum: DbContext + audit interceptor (SaveChanges otomatik denetlenir)
builder.Services.AddDbContext<DivisimaDbContext>((sp, options) =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DivisimaDb"),
            // Aciklayici yorum: DAYANIKLILIK - gecici DB kopmalarinda otomatik retry.
            // NOT: Bu AKTIF EDILMEDEN once transaction kullanan manager'lar (OrderManager/GiftCard/
            // Loyalty/Referral/Return/StoreCredit/IyzicoPayment) IUnitOfWork.ExecuteInTransactionAsync'e
            // tasinmali - aksi halde manuel BeginTransaction retry stratejisi tarafindan reddedilir.
            // Primitive hazir; tasima + dotnet build/test sonrasi asagidaki satiri acin:
            // sqlOpts => sqlOpts.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorNumbersToAdd: null))
            sqlOpts => { })
           .AddInterceptors(sp.GetRequiredService<AuditInterceptor>()));

// B3: cache + dağıtık altyapı (Redis:Enabled true ise Redis, değilse in-memory)
builder.Services.AddMemoryCache();
var redisEnabled = bool.TryParse(builder.Configuration["Redis:Enabled"], out var re) && re;
var redisConn = builder.Configuration["Redis:Connection"] ?? "localhost:6379";
if (redisEnabled)
{
    // Açıklayıcı yorum: GERÇEK Redis - çok sunuculu ortamda cache/lock/blacklist dağıtık çalışır
    var mux = StackExchange.Redis.ConnectionMultiplexer.Connect(redisConn);
    builder.Services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(mux);
    builder.Services.AddStackExchangeRedisCache(o => o.Configuration = redisConn);
    // Açıklayıcı yorum: ICacheService Redis (IDistributedCache), lock Redis (RedLock)
    builder.Services.AddSingleton<ICacheService, Divisima.Core.Utilities.Caching.RedisCacheService>();
    builder.Services.AddSingleton<Divisima.Core.Utilities.Locking.IDistributedLock, Divisima.Core.Utilities.Locking.RedisDistributedLock>();
    // Açıklayıcı yorum: Redis dağıtık rate limiter (çok sunuculu ortamda merkezi sayaç)
    builder.Services.AddSingleton<Divisima.Core.Security.RateLimiting.IDistributedRateLimiter, Divisima.Core.Security.RateLimiting.RedisRateLimiter>();
}
else
{
    // Açıklayıcı yorum: Dev/tek sunucu - in-memory (aynı arayüzler)
    builder.Services.AddSingleton<ICacheService, MemoryCacheService>();
    builder.Services.AddSingleton<Divisima.Core.Utilities.Locking.IDistributedLock, Divisima.Core.Utilities.Locking.InMemoryDistributedLock>();
    builder.Services.AddSingleton<Divisima.Core.Security.RateLimiting.IDistributedRateLimiter, Divisima.Core.Security.RateLimiting.InMemoryRateLimiter>();
}

// Framework bağımlı servisler
builder.Services.AddScoped<ITokenHelper, JwtHelper>();
builder.Services.AddScoped<IMailService, SmtpMailService>();
// LAUNCH-FIX A1(c): e-postadaki baglantilarin TEK KAYNAGI. Gerekce IMailLinkBuilder'in basinda.
builder.Services.AddScoped<IMailLinkBuilder, MailLinkBuilder>();
builder.Services.AddScoped<INotificationService, SignalRNotificationService>();
builder.Services.AddScoped<IIyzicoClient, IyzicoClient>();
builder.Services.AddScoped<Divisima.Core.Security.TwoFactor.ITwoFactorService, Divisima.Core.Security.TwoFactor.TotpService>();
builder.Services.AddSingleton<Divisima.Core.Security.Encryption.IEncryptionProvider, Divisima.Core.Security.Encryption.AesEncryptionProvider>();
builder.Services.AddScoped<Divisima.Core.Security.JWT.ITokenBlacklist, Divisima.Core.Security.JWT.CacheTokenBlacklist>();
builder.Services.AddSingleton<Divisima.Core.Utilities.Secrets.ISecretProvider, Divisima.Core.Utilities.Secrets.ConfigurationSecretProvider>();
// Aciklayici yorum: DAYANIKLILIK (#6) - dis servis cagrilarinda gecici hatalar icin retry + circuit-breaker.
// Yavas/coken bir dis servis (captcha/push/SMS/kargo) tum sistemi asagi cekmesin; ardisik hatada devre acilir.
var resiliencePolicy = Policy.WrapAsync(
    HttpPolicyExtensions.HandleTransientHttpError()
        .WaitAndRetryAsync(3, attempt => TimeSpan.FromMilliseconds(200 * Math.Pow(2, attempt))),   // 400/800/1600 ms - ustel geri cekilme
    HttpPolicyExtensions.HandleTransientHttpError()
        .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30)));                                          // 5 ardisik hata -> 30 sn devre acik
builder.Services.AddHttpClient("turnstile").AddPolicyHandler(resiliencePolicy);
builder.Services.AddHttpClient("fcm").AddPolicyHandler(resiliencePolicy);
builder.Services.AddHttpClient("sms").AddPolicyHandler(resiliencePolicy);
builder.Services.AddScoped<Divisima.Core.Integrations.Notifications.IPushNotificationService, Divisima.Core.Integrations.Notifications.FcmPushNotificationService>();
builder.Services.AddScoped<Divisima.Core.Integrations.Notifications.ISmsService, Divisima.Core.Integrations.Notifications.NetgsmSmsService>();
builder.Services.AddScoped<Divisima.Core.Integrations.EInvoice.IEInvoiceProvider, Divisima.Core.Integrations.EInvoice.GibEInvoiceProvider>();
builder.Services.AddHttpClient("shipping").AddPolicyHandler(resiliencePolicy);
builder.Services.AddScoped<Divisima.Core.Integrations.Shipping.ICarrierProvider, Divisima.Core.Integrations.Shipping.DefaultCarrierProvider>();
builder.Services.AddScoped<Divisima.Core.Storage.IImageStorage, Divisima.Core.Storage.LocalImageStorage>();
builder.Services.AddScoped<Divisima.Core.Security.Captcha.ICaptchaValidator, Divisima.Core.Security.Captcha.TurnstileCaptchaValidator>();

// AutoMapper
builder.Services.AddAutoMapper(cfg =>
{
    cfg.AddProfile<ProductProfile>();
    cfg.AddProfile<CategoryProfile>();
    cfg.AddProfile<CouponProfile>();
    cfg.AddProfile<CollectionProfile>();
    cfg.AddProfile<OrderProfile>();
    cfg.AddProfile<ProductReviewProfile>();
    cfg.AddProfile<ContentProfile>();
    cfg.AddProfile<AddressProfile>();
});

// B11: FluentValidation
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssembly(typeof(AutofacBusinessModule).Assembly);

// JWT
var tokenOptions = builder.Configuration.GetSection("TokenOptions").Get<TokenOptions>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = tokenOptions.Issuer,
            ValidAudience = tokenOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(tokenOptions.SecurityKey)),
            ClockSkew = TimeSpan.Zero,
            // Açıklayıcı yorum: ALG CONFUSION engeli - yalnızca HMAC-SHA256 imzası kabul edilir.
            // "alg":"none" veya asimetrik anahtar karışıklığı (RS256->HS256) saldırıları reddedilir.
            ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256, SecurityAlgorithms.HmacSha256Signature }
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken) && context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                    context.Token = accessToken;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddSingleton<IAuthorizationHandler, RequireUserTypeHandler>();
builder.Services.AddAuthorization(options =>
{
    foreach (UserTypeEnum t in Enum.GetValues(typeof(UserTypeEnum)))
        options.AddPolicy($"{RequireUserTypeAttribute.PolicyPrefix}{t}",
            policy => policy.Requirements.Add(new RequireUserTypeRequirement(t)));
});

// B10: CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("DivisimaFrontend", policy =>
        policy.WithOrigins(builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? new[] { "https://divisima.com" })
              .AllowAnyHeader().AllowAnyMethod().AllowCredentials()
              // ══ DALGA-3-FIX (P1) - PREFLIGHT ONBELLEGI ═══════════════════════════════════
              //
              // Storefront ile API AYRI ORIGIN'lerde (divisima.com <-> api.divisima.com) ve her
              // korumali cagri `Authorization` basligi tasidigi icin BASIT ISTEK degildir:
              // tarayici once OPTIONS preflight gonderir. `Access-Control-Max-Age` YOKKEN bu
              // yanit ancak tarayicinin KISA varsayilani kadar onbelleklenir.
              //
              // OLCULDU (Chrome, gercek gezinti): Max-Age YOKKEN 24 saniyede 12 kimlikli istek
              // -> 4 OPTIONS. Bir hesap gezintisinde 34 istegin 15'i preflight'ti (trafigin %44'u).
              // 100 ms RTT'li mobil baglantida bu, saf ek yuk olarak saniyeler demek.
              //
              // NEDEN 10 DAKIKA (600 sn) - daha buyugu DEGIL:
              // Tarayicilar bu degeri KENDI UST SINIRLARINA kirpar; bilinen tavanlar farkli
              // (WebKit/Safari en dusuk tavana sahip, ~600 sn; Chromium ve Firefox daha yuksek).
              // 600 sn, BUTUN yaygin tarayicilarin TAM OLARAK uyguladigi en buyuk ortak degerdir:
              // daha buyuk bir sayi Safari'de sessizce kirpilir, yani kagit uzerinde kalir.
              // Ayrica CORS politikasi degistiginde eski izin en fazla 10 dakika yasar - guvenlik
              // tarafinda makul bir tazelenme penceresi. (Tarayici tavanlari BU DALGADA
              // OLCULMEDI; olculen sey Chrome'da preflight sayisinin 4 -> 1'e dusmesidir.
              .SetPreflightMaxAge(TimeSpan.FromMinutes(10)));
});

// B6: Rate limiting
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions { PermitLimit = 100, Window = TimeSpan.FromMinutes(1) }));
    // GÜVENLİK/DOĞRULUK: "payment" policy'si EKSİKTİ -> PaymentController [EnableRateLimiting("payment")]
    // tanımsız policy'ye referans veriyordu; .NET 8 yerleşik limiter bunu runtime'da InvalidOperationException
    // ile reddeder (ödeme endpoint'i 500). Redis middleware'indeki "payment" scope (10/dk) ile tutarlı tanımlandı.
    //
    // PARTITION DÜZELTMESİ: bu iki policy önce AddFixedWindowLimiter ile tanımlıydı; o aşırı yükleme
    // TEK bir limiter örneği üretir, yani kova TÜM kullanıcılar arasında paylaşılırdı. Sonuç: site
    // genelinde dakikada 5 register/login isteği - tek bir kullanıcı herkesin girişini kilitleyebilirdi
    // (testle doğrulandı: 6. istek 429, üstelik farklı uçtan). GlobalLimiter zaten IP'ye bölünmüştü,
    // bu ikisi bölünmemişti. Artık AddPolicy + RateLimitPartition ile istemci başına ayrı kova var.
    //
    // DEPLOY NOTU: bölümleme RemoteIpAddress'e dayanır. Ters proxy/LB arkasında
    // ForwardedHeaders:KnownProxies DOLDURULMAZSA tüm istekler LB'nin IP'sinde toplanır ve bu
    // düzeltme kâğıt üstünde kalır - iki ayar birlikte anlamlıdır.
    // Açıklayıcı yorum: Limitler YAPILANDIRILABİLİR (varsayılan bugünkü değerler). İki gerekçe:
    //  1) Prod'da eşiği değiştirmek için yeniden derleme gerekmesin (CGNAT arkasındaki müşteriler,
    //     kampanya günleri gibi durumlarda ayarlanabilmeli).
    //  2) Entegrasyon testleri gerçek istemci kimliği üretemiyor (test sunucusunda RemoteIpAddress
    //     null); onlarca müşteri yaratan bir test tek partition'da limite takılıyordu. Test host'u
    //     bu anahtarı yükselterek limiti devre dışı bırakabiliyor - üretim varsayılanı değişmiyor
    //     ve limitin KENDİSİ AuthRateLimitPinTests'te varsayılan değerle pinli kalıyor.
    var authPermitLimit = int.TryParse(builder.Configuration["RateLimit:AuthPermitLimit"], out var apl) && apl > 0 ? apl : 10;
    var paymentPermitLimit = int.TryParse(builder.Configuration["RateLimit:PaymentPermitLimit"], out var ppl) && ppl > 0 ? ppl : 10;

    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        // Bölümlenince 5/dk gereksiz dar kalıyordu (aynı evden/ofisten giren birkaç kullanıcı
        // birbirini kilitliyordu). İstemci başına 10/dk hem kaba kuvvete kapalı hem yaşanabilir.
        factory: _ => new FixedWindowRateLimiterOptions { PermitLimit = authPermitLimit, Window = TimeSpan.FromMinutes(1) }));

    options.AddPolicy("payment", context => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions { PermitLimit = paymentPermitLimit, Window = TimeSpan.FromMinutes(1) }));
});

// B12: API versiyonlama
builder.Services.AddApiVersioning(o =>
{
    o.DefaultApiVersion = new ApiVersion(1, 0);
    o.AssumeDefaultVersionWhenUnspecified = true;
    o.ReportApiVersions = true;
    // Açıklayıcı yorum: B4 - Versiyon URL segmenti + header + query'den okunabilir (istemci esnekliği; v2'de controller [ApiVersion] ile ayrılır)
    //
    // SPRINT 8 MADDE 9: baslik okuyucusu WebhookExemptHeaderApiVersionReader ile sarmalandi -
    // yalniz /api/payment/webhook yolunda "X-Api-Version" YOK SAYILIR. Gerekce, olcumler ve
    // denenip ELENEN alternatifler (action/controller [ApiVersionNeutral], middleware) o
    // sinifin basindaki blokta; burada tekrarlanmiyor ki iki yer ayrisamasin.
    o.ApiVersionReader = Asp.Versioning.ApiVersionReader.Combine(
        new Asp.Versioning.UrlSegmentApiVersionReader(),
        new Divisima.API.Versioning.WebhookExemptHeaderApiVersionReader(),
        new Asp.Versioning.QueryStringApiVersionReader("api-version"));
}).AddApiExplorer(o => { o.GroupNameFormat = "'v'VVV"; o.SubstituteApiVersionInUrl = true; });

// B8: Hangfire (SQL Server storage)
builder.Services.AddHangfire(cfg => cfg
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(builder.Configuration.GetConnectionString("DivisimaDb")));
builder.Services.AddHangfireServer();

// B9: Health checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<DivisimaDbContext>("database", tags: new[] { "ready" })  // DB erişimi = readiness
    .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(), tags: new[] { "live" }); // süreç ayakta = liveness

// B9+: OpenTelemetry - distributed tracing + metrikler (OTLP ile Jaeger/Tempo/Prometheus'a)
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("Divisima.API"))
    .WithTracing(t => t
        .AddAspNetCoreInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        .AddOtlpExporter())
    .WithMetrics(m => m
        .AddAspNetCoreInstrumentation()
        .AddRuntimeInstrumentation()
        .AddOtlpExporter());


// B7: SignalR
builder.Services.AddSignalR();

// Açıklayıcı yorum: A1 - Ters proxy arkasında gerçek istemci IP + şema (rate-limit, webhook allowlist, audit IP bunlara bağlı).
// GÜVENLİK (X-Forwarded-For SPOOFING engeli): Önceden KnownNetworks+KnownProxies KOŞULSUZ Clear ediliyordu ->
// ASP.NET checkKnownIps=false -> middleware X-Forwarded-For'u HERKESTEN güvenir -> saldırgan IP sahteleyip
// per-IP rate-limit'i (auth 5/dk) ATLAR (sınırsız brute-force) + webhook-IP-allowlist'i kandırır. Artık YALNIZCA
// config'teki ingress/LB IP'lerine güvenilir; tanımlı değilse ASP.NET güvenli varsayılanı (localhost-only) korunur.
var trustedProxies = builder.Configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? Array.Empty<string>();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1; // yalnız tek hop (doğrudan proxy); zincirin gerisi client tarafından sahtelenebilir
    if (trustedProxies.Length > 0)
    {
        // Prod: SADECE bilinen ingress/LB IP'lerine güven (bunlar X-Forwarded-For'u dogru ekler)
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();
        foreach (var ip in trustedProxies)
            if (System.Net.IPAddress.TryParse(ip, out var addr))
                options.KnownProxies.Add(addr);
    }
    // else: ASP.NET varsayilani (KnownProxies=127.0.0.1 + KnownNetworks=127.0.0.0/8) korunur ->
    // keyfi X-Forwarded-For GUVENILMEZ (RemoteIpAddress gercek baglanti IP'si kalir) -> spoofing engelli.
});

// Açıklayıcı yorum: D1 - Yanıt sıkıştırma (JSON liste uçları için bant genişliği/gecikme kazancı)
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true; // JSON API - yanıtta attacker-kontrollü secret yansıtılmıyor (BREACH riski düşük)
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});

// Açıklayıcı yorum: D3 - İstek gövdesi + koleksiyon bağlama limiti (devasa items[] dizisi kötüye kullanımını önle)
builder.WebHost.ConfigureKestrel(o =>
{
    o.Limits.MaxRequestBodySize = 5 * 1024 * 1024; // 5 MB
    // GUVENLIK-FIX (G8): "Server: Kestrel" parmak izi basligi KAPATILDI. Surum bilgisi
    // vermiyordu, yani tek basina bir acik degil; ama sunucu yiginini gereksiz yere
    // beyan ediyor ve toplu tarama araclarina bedava bir siniflandirma ipucu veriyor.
    // Kapatmanin islevsel bedeli YOK (basligi okuyan bir istemci/ara katman depoda yok).
    o.AddServerHeader = false;
});
builder.Services.Configure<Microsoft.AspNetCore.Mvc.MvcOptions>(o => o.MaxModelBindingCollectionSize = 500);

builder.Services.AddControllers()
    // Açıklayıcı yorum: B2 - Doğrulama hatası da custom ErrorResult zarfı dönsün (varsayılan ProblemDetails yerine - API sözleşmesi tutarlı)
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var firstError = context.ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .FirstOrDefault(m => !string.IsNullOrWhiteSpace(m)) ?? "Geçersiz istek.";
            return new BadRequestObjectResult(new ErrorResult(firstError));
        };
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => c.EnableAnnotations());

var app = builder.Build();

// Açıklayıcı yorum: İlk admin tohumlama (idempotent, AdminSeed:Enabled=true ise). user_type alanı sonrası admin oluşturmanın güvenli yolu.
using (var _seedScope = app.Services.CreateScope())
{
    try
    {
        var _seeder = _seedScope.ServiceProvider.GetRequiredService<Divisima.Bussiness.Seed.AdminSeeder>();
        await _seeder.SeedAsync();

        // E3: legal icerik tohumlama. IDEMPOTENT - slug zaten varsa DOKUNULMAZ, yani admin'in
        // CMS'ten yaptigi duzenleme sonraki aciliste EZILMEZ. AdminSeed gibi bayrakla kapatilmis
        // DEGIL: bos legal sayfa yayinlamak (KVKK, mesafeli satis) kabul edilebilir bir varsayilan degil.
        var _contentSeeder = _seedScope.ServiceProvider.GetRequiredService<Divisima.Bussiness.Seed.ContentSeeder>();
        await _contentSeeder.SeedAsync();
    }
    catch (Exception _seedEx)
    {
        // Açıklayıcı yorum: Tohumlama hatası uygulamayı DURDURMAZ (ör. DB henüz migrate edilmemiş) - sadece loglanır
        app.Logger.LogError(_seedEx, "Admin tohumlama başarısız - uygulama devam ediyor.");
    }
}

// Açıklayıcı yorum: A1 - Forwarded header'ları EN ÖNCE işle; rate-limit/allowlist/audit gerçek IP'yi görsün
app.UseForwardedHeaders();
// Açıklayıcı yorum: D1 - Sıkıştırma (statik dosyalar ve API yanıtları için)
app.UseResponseCompression();
// Açıklayıcı yorum: D2 - Katalog GET yanıtlarına ETag/304 (istemci önbellek)
app.UseMiddleware<Divisima.API.Middlewares.ETagMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHsts();   // B10: HSTS
}

app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseSerilogRequestLogging();                    // B5
app.UseMiddleware<ExceptionMiddleware>();          // A2 global hata
app.UseMiddleware<WebhookIpAllowlistMiddleware>(); // webhook IP allowlist
app.UseMiddleware<CorrelationIdMiddleware>();      // B5 correlation
app.UseHttpsRedirection();
app.UseStaticFiles();                              // wwwroot/uploads - ürün görselleri (statik dosya sunumu)
app.UseCors("DivisimaFrontend");                   // B10
// Açıklayıcı yorum: Redis açıksa dağıtık rate limit (merkezi sayaç); değilse .NET yerleşik (sunucu-başına)
var _redisRateLimit = bool.TryParse(app.Configuration["Redis:Enabled"], out var _rr) && _rr;
if (_redisRateLimit)
    app.UseMiddleware<Divisima.API.Middlewares.RedisRateLimitMiddleware>();
else
    app.UseRateLimiter();                              // B6 (yerleşik, tek sunucu)
app.UseMiddleware<IdempotencyMiddleware>();        // çift işlem engeli (tüm mutasyonlar)
app.UseMiddleware<AntiforgeryMiddleware>();        // CSRF (cookie tabanlı istekler)
app.UseAuthentication();
app.UseMiddleware<TokenBlacklistMiddleware>();     // iptal edilen token kontrolü
app.UseAuthorization();

// ══ GUVENLIK-FIX (G5) - KIMLIK DOGRULAMA ARTIK VARSAYILAN ════════════════════════════════
//
// ONCEKI DURUM: AddAuthorization YALNIZ tip politikalarini kaydediyordu; ne FallbackPolicy
// ne de MapControllers().RequireAuthorization() vardi. Yani yetki ozniteligi OLMAYAN bir
// controller ucu VARSAYILAN OLARAK HERKESE ACIK oluyordu.
//
// BUGUN BIR BOSLUK YOK - OLCULDU: 150 action'in TAMAMI etkili bir oznitelik tasiyor (action
// ya da sinif duzeyinde; iki gecisli tarama ile dogrulandi. Ilk taramam YANLISTI - oznitelik
// satirlari ARASINDAKI yorum satirlarinda tamponu sifirliyor ve iki [AllowAnonymous] ucunu
// "yetkisiz" gosteriyordu; tarayici duzeltildi). Bu degisiklik BUGUNU degil YARINI kapatir:
// oznitelik koymayi unutan yeni bir uc, sessizce acilmak yerine 401 verir.
//
// [AllowAnonymous] KAZANIR: yetkilendirme ara katmani, endpoint metadata'sinda IAllowAnonymous
// gorurse politikayi HIC degerlendirmez. Yani mevcut anonim uclar (katalog, arama, kayit,
// giris, webhook, abonelik...) AYNEN calisir - olculdu.
//
// ══ NEDEN FallbackPolicy DEGIL - OLCUMLE ALINAN KARAR ═══════════════════════════════════
// Ilk uygulama `options.FallbackPolicy = RequireAuthenticatedUser()` idi ve MEVCUT BIR PINI
// KIRDI: `WebhookContractTests.AyniV1Basligi_BASKA_BIR_UCTA_HALA_400_KAPSAM_DAR_KALDI`
// beklenen 400 yerine 401 buldu. Sebep OLCULDU: `X-Api-Version` ayristirilamayinca
// Asp.Versioning gercek endpoint'i secmeyip YERINE metadata'siz bir HATA endpoint'i koyuyor;
// FallbackPolicy o endpoint'i de kapsadigi icin 400'u yazan kod HIC CALISMIYOR, istek 401'e
// donusuyor. Bu, SUPHELI #14'te belgelenen "bozuk surum basligi tum API'yi 400'e dusuruyor"
// sorununu DAHA KOTU hale getirirdi: entegratore 401 demek, onu kimlik hatasi aramaya
// yonlendirir. Kapsami controller'lara daraltmak ayni guvenceyi bu yan etki OLMADAN veriyor.
//
// KALAN BOSLUK (durust kayit): bu satir YALNIZ controller uclarini kapsar. Ileride eklenecek
// bir minimal-API endpoint'i (app.MapGet/MapPost) ya da yeni bir hub yine varsayilan olarak
// acik olurdu. O bosluk RUNTIME'da degil TEST'te kapatildi: SecurityHardeningTests ->
// `FallbackPolicy_ACIK_Uclari_KIRMAZ_ve_URETIM_UCLARI_ISARETLIDIR` her uretim ucunun ACIKCA
// isaretli oldugunu tarar. Sessiz bir 401 yerine kirmizi bir test tercih edildi.
app.MapControllers().RequireAuthorization();
app.MapHub<NotificationHub>("/hubs/notification"); // B7
// GUVENLIK-FIX (G5): health uclari ACIKCA anonim olarak isaretlendi. Bugun ZORUNLU DEGIL
// (varsayilan-kapali kural MapControllers ile SINIRLI, health uclari onun disinda) - isaret
// NIYET beyani ve ileriye donuk emniyet: kapsam bir gun FallbackPolicy ile genisletilirse
// probe.lar 401 alip orkestratörün pod.u saglıksiz saymasina yol acmaz.
app.MapHealthChecks("/health").AllowAnonymous();    // B9 - genel (tüm kontroller)
// Açıklayıcı yorum: K8s/orkestratör probe'ları - liveness (süreç) vs readiness (bağımlılıklar)
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live")
}).AllowAnonymous();
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
}).AllowAnonymous();
app.UseHangfireDashboard("/hangfire", new Hangfire.DashboardOptions
{
    Authorization = new[] { new Divisima.API.Services.HangfireAuthorizationFilter() }
});

// B8: Recurring job - Outbox işleyici (dakikada bir)
RecurringJob.AddOrUpdate<OutboxProcessor>("outbox-processor", p => p.ProcessPendingAsync(), Cron.Minutely);
// Açıklayıcı yorum: Veri saklama/temizlik - her gün (eski oturum/outbox/log temizliği)
RecurringJob.AddOrUpdate<Divisima.Bussiness.Outbox.DataRetentionJob>("data-retention", j => j.RunAsync(), Cron.Daily);
RecurringJob.AddOrUpdate<Divisima.Bussiness.Jobs.ReservationCleanupJob>("reservation-cleanup", j => j.RunAsync(), "*/5 * * * *"); // her 5 dk süresi dolan rezervasyonlar
RecurringJob.AddOrUpdate<Divisima.Bussiness.Jobs.AbandonedCartReminderJob>("abandoned-cart-reminder", j => j.RunAsync(), Cron.Hourly); // saatlik terk sepet hatırlatması
RecurringJob.AddOrUpdate<Divisima.Bussiness.Jobs.BirthdayOfferJob>("birthday-offers", j => j.RunAsync(), "0 9 * * *"); // her gün 09:00 doğum günü teklifleri
RecurringJob.AddOrUpdate<Divisima.Bussiness.Jobs.WinBackJob>("win-back", j => j.RunAsync(), "0 10 * * *"); // her gün 10:00 win-back
RecurringJob.AddOrUpdate<Divisima.Bussiness.Jobs.ReviewInviteJob>("review-invites", j => j.RunAsync(), "0 11 * * *"); // her gün 11:00 yorum daveti

app.Run();

// Açıklayıcı yorum: Integration test'lerin erişimi için partial Program
public partial class Program { }

