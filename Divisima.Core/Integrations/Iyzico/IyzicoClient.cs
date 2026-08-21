using System.Collections.Concurrent;
using System.Globalization;
using Iyzipay;
using Iyzipay.Model;
using Iyzipay.Request;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Divisima.Core.Integrations.Iyzico
{
    // Açıklayıcı yorum: GERÇEK Iyzipay SDK entegrasyonu. Checkout Form (CF) akışı:
    //   InitializeCheckoutForm -> Iyzico'da form token üretir (kart bilgisi Iyzico'da toplanır, sunucuya gelmez).
    //   RetrievePaymentResult -> ödeme sonucunu token ile Iyzico'dan sunucu-sunucu çeker (güven kaynağı).
    // "Iyzico:UseRealSdk" false ise (dev/test) güvenli placeholder döner - böylece .NET+anahtar olmadan da akış test edilir.
    public class IyzicoClient : IIyzicoClient
    {
        private readonly IConfiguration _config;
        private readonly ILogger<IyzicoClient> _logger;

        public IyzicoClient(IConfiguration config, ILogger<IyzicoClient> logger)
        {
            _config = config;
            _logger = logger;
        }

        // Açıklayıcı yorum: SDK ayarları (apiKey/secretKey kasadan; baseUrl sandbox/production)
        // Aciklayici yorum: Taksit secenekleri config'ten (virgullu liste, or. "1,2,3,6,9,12"). Gecersizse tek cekim.
        private List<int> GetEnabledInstallments()
        {
            var raw = _config["Iyzico:EnabledInstallments"];
            if (string.IsNullOrWhiteSpace(raw)) return new List<int> { 1, 2, 3, 6, 9, 12 };
            var list = new List<int>();
            foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries))
                if (int.TryParse(part.Trim(), out var n) && n >= 1 && n <= 12) list.Add(n);
            return list.Count > 0 ? list : new List<int> { 1 };
        }

        private Options BuildOptions() => new Options
        {
            ApiKey = _config["Iyzico:ApiKey"],
            SecretKey = _config["Iyzico:SecretKey"],
            BaseUrl = _config["Iyzico:BaseUrl"] ?? "https://sandbox-api.iyzipay.com"
        };

        // GUVENSIZ VARSAYILAN FIX (H53): eskiden "bool.TryParse(...) && v" idi -> anahtar EKSIKSE false donuyordu,
        // yani MOCK mod devreye giriyordu ve InitializeCheckoutForm sahte token ile Success=true donuyordu.
        // Uretim config'inde bu TEK anahtar unutulursa site ODEME ALMADAN siparis onayliyordu (bedava alisveris).
        // Artik FAIL-CLOSED: mock mod YALNIZCA anahtar acikca "false" yazildiginda acilir; anahtar yoksa veya
        // bozuksa GERCEK SDK kullanilir (ApiKey/SecretKey eksikse odeme baslatma yuksek sesle basarisiz olur).
        private bool MockExplicitlyRequested =>
            _config["Iyzico:UseRealSdk"] != null
            && bool.TryParse(_config["Iyzico:UseRealSdk"], out var parsedFlag)
            && parsedFlag == false;

        private bool UseRealSdk => !MockExplicitlyRequested;

        // Açıklayıcı yorum: Dev/test için token->beklenen tutar eşlemesi. UseRealSdk=false iken retrieve
        // doğru tutarı döndürebilsin diye (manager'ın tutar doğrulaması gerçek akıştaki gibi çalışır). Üretimde kullanılmaz.
        private static readonly ConcurrentDictionary<string, decimal> _devTokenAmounts = new();

        // E2b - MOCK MODU IADE KIMLIGI. Gercek Iyzico refund'da paymentId DEGIL, odeme
        // KIRILIMININ paymentTransactionId'sini ister; yanlis kimlikle cagrilirsa
        // "Bu isyerine ait odeme kirilim kaydi bulunamadi" ile REDDEDER (olculdu).
        // Mock eskiden HER kimlige Success=true donuyordu - bu yuzden uretimdeki tip
        // karisikligi hicbir testte gorunmuyordu. Artik mock da SADECE kendi urettigi
        // kirilim kimliklerini kabul ediyor; boylece hata CI'da pinlenebilir.
        private static readonly ConcurrentDictionary<string, byte> _devItemTransactionIds = new();

        public async Task<IyzicoCheckoutInitResult> InitializeCheckoutFormAsync(IyzicoCheckoutInitRequest request)
        {
            // Açıklayıcı yorum: Dev/test - gerçek SDK kapalıysa güvenli placeholder
            if (!UseRealSdk)
            {
                // Mock mod ACIKCA istendi. Bu mod PARA CEKMEDEN "basarili" doner - uretimde ASLA kullanilmamali.
                // Her cagrida KRITIK seviyede loglanir ki yanlislikla acik kalirsa gozden kacmasin.
                _logger.LogCritical("ODEME MOCK MODUNDA (Iyzico:UseRealSdk=false) - GERCEK TAHSILAT YAPILMIYOR. " +
                                    "Bu ayar URETIMDE kapali olmalidir.");
                var devToken = Guid.NewGuid().ToString("N");
                _devTokenAmounts[devToken] = request.Price; // retrieve'de aynı tutar dönsün
                return new IyzicoCheckoutInitResult
                {
                    Success = true,
                    Token = devToken,
                    CheckoutFormContent = "<!-- Iyzico CF (UseRealSdk=false) -->"
                };
            }

            var options = BuildOptions();
            var ci = CultureInfo.InvariantCulture;

            var cfRequest = new CreateCheckoutFormInitializeRequest
            {
                Locale = Locale.TR.ToString(),
                ConversationId = request.ConversationId,
                Price = request.Price.ToString(ci),
                PaidPrice = request.Price.ToString(ci),
                Currency = Currency.TRY.ToString(),
                BasketId = request.ConversationId,
                PaymentGroup = PaymentGroup.PRODUCT.ToString(),
                CallbackUrl = request.CallbackUrl,
                // Aciklayici yorum: TAKSIT - iframe'de sunulacak taksit secenekleri (config: Iyzico:EnabledInstallments).
                // Musteri iframe'de secer; secilen taksit + komisyon retrieve sonucunda doner. Varsayilan: tek cekim + 2,3,6,9,12.
                EnabledInstallments = GetEnabledInstallments(),
                // Açıklayıcı yorum: Alıcı/adres bilgisi (Iyzico zorunlu alanları)
                Buyer = new Buyer
                {
                    Id = request.CustomerId.ToString(),
                    Name = request.BuyerName,
                    Surname = request.BuyerName,
                    Email = request.BuyerEmail,
                    IdentityNumber = "11111111111",
                    RegistrationAddress = "Divisima",
                    City = "Istanbul",
                    Country = "Turkey",
                    Ip = "0.0.0.0"
                },
                BillingAddress = new Address
                {
                    ContactName = request.BuyerName,
                    City = "Istanbul",
                    Country = "Turkey",
                    Description = "Divisima"
                },
                BasketItems = new List<BasketItem>
                {
                    new BasketItem
                    {
                        Id = request.ConversationId,
                        Name = "Divisima Sipariş",
                        Category1 = "Moda",
                        ItemType = BasketItemType.VIRTUAL.ToString(),
                        Price = request.Price.ToString(ci)
                    }
                }
            };

            // Açıklayıcı yorum: Iyzico'ya asenkron çağrı (SDK senkron; Task.Run ile sarılır)
            var result = await Task.Run(() => CheckoutFormInitialize.Create(cfRequest, options));

            if (result.Status != "success")
            {
                _logger.LogWarning("Iyzico CF init başarısız: {Error}", result.ErrorMessage);
                return new IyzicoCheckoutInitResult { Success = false, ErrorMessage = result.ErrorMessage };
            }

            return new IyzicoCheckoutInitResult
            {
                Success = true,
                Token = result.Token,
                CheckoutFormContent = result.CheckoutFormContent
            };
        }

        public async Task<IyzicoPaymentResult> RetrievePaymentResultAsync(string token)
        {
            // Açıklayıcı yorum: Dev/test placeholder (gerçek tutar 0 - manager tutar doğrulamasında dikkat)
            if (!UseRealSdk)
            {
                // Açıklayıcı yorum: Init'te saklanan beklenen tutarı dön (yoksa 0). Gerçek akıştaki tutar doğrulaması test edilebilir.
                _devTokenAmounts.TryRemove(token, out var devAmount);
                var devItemTx = "ITX-" + Guid.NewGuid().ToString("N");
                _devItemTransactionIds[devItemTx] = 1;
                return new IyzicoPaymentResult
                {
                    Success = true,
                    PaymentId = Guid.NewGuid().ToString("N"),
                    ItemTransactionId = devItemTx,
                    ItemTransactionCount = 1,
                    PaidPrice = devAmount,
                    Currency = "TRY",
                    FraudStatus = "1"
                };
            }

            var options = BuildOptions();
            var retrieveRequest = new RetrieveCheckoutFormRequest { Token = token };

            // Açıklayıcı yorum: SUNUCU-SUNUCU sonuç sorgusu - callback gövdesinden bağımsız güven kaynağı
            var result = await Task.Run(() => CheckoutForm.Retrieve(retrieveRequest, options));

            var ci = CultureInfo.InvariantCulture;
            bool paid = result.Status == "success" && result.PaymentStatus == "SUCCESS";
            decimal.TryParse(result.PaidPrice, NumberStyles.Any, ci, out var paidPrice);

            // E2b - ODEME KIRILIMI (itemTransaction). IADE bu kimligi ister, paymentId'yi DEGIL.
            // Olculdu (gercek sandbox yaniti): paymentId=37399936 iken paymentTransactionId=39316344
            // ve itemTransaction SAYISI = 1. Sayinin 1 olmasi bizim CF init'imizin sepeti TEK
            // BasketItem olarak gondermesinden geliyor (bkz. InitializeCheckoutFormAsync).
            // Sayi 1 DEGILSE tasarim varsayimi bozulmustur: sessizce ilkini secip yanlis tutar
            // iade etmek yerine GURULTULU loglanir, kimlik yine ilk kirilimdan alinir.
            var itemTx = result.PaymentItems?.FirstOrDefault()?.PaymentTransactionId;
            var itemTxCount = result.PaymentItems?.Count ?? 0;
            if (paid && itemTxCount != 1)
                _logger.LogError("Iyzico retrieve BEKLENMEYEN kirilim sayisi: {Adet} (beklenen 1). " +
                                 "Iade kimligi ilk kirilimdan alindi - kismi iade tutari yanlis olabilir. token={Token}",
                                 itemTxCount, token);
            if (paid && string.IsNullOrWhiteSpace(itemTx))
                _logger.LogError("Iyzico retrieve KIRILIM KIMLIGI BOS - bu odeme IADE EDILEMEZ. token={Token}", token);

            return new IyzicoPaymentResult
            {
                Success = paid,
                PaymentId = result.PaymentId,
                ItemTransactionId = itemTx,
                ItemTransactionCount = itemTxCount,
                ConversationId = result.ConversationId,
                PaidPrice = paidPrice,
                Currency = result.Currency,
                FraudStatus = result.FraudStatus?.ToString() ?? "0",
                Installment = (result.Installment ?? 0) > 0 ? result.Installment!.Value : 1,   // secilen taksit
                ErrorMessage = result.ErrorMessage
            };
        }

        // Açıklayıcı yorum: Callback imza doğrulama (Iyzico secretKey HMAC-SHA256, timing-safe) - önceki turdan korunur
        public bool VerifyCallbackSignature(string token, string signature)
        {
            if (string.IsNullOrEmpty(signature)) return false;
            var secret = _config["Iyzico:SecretKey"] ?? "";
            using var hmac = new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes(secret));
            var computed = Convert.ToHexString(hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
            return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(computed),
                System.Text.Encoding.UTF8.GetBytes(signature.ToLowerInvariant()));
        }
        public async Task<IyzicoRefundResult> RefundAsync(string paymentTransactionId, decimal amount)
        {
            // Açıklayıcı yorum: Dev/test - gerçek SDK kapalıysa başarılı placeholder
            if (!UseRealSdk)
            {
                // E2b: MOCK ARTIK KIMLIK DOGRULUYOR. Eskiden HER kimlige Success=true donuyordu;
                // bu yuzden uretimdeki tip karisikligi (paymentId yerine paymentTransactionId)
                // hicbir testte gorunmedi ve ancak GERCEK sandbox turunda ortaya cikti.
                // Gercek Iyzico yanlis kimlige "Bu isyerine ait odeme kirilim kaydi bulunamadi"
                // diyor - mock da ayni sekilde REDDEDIYOR ki hata CI'da pinlenebilsin.
                if (string.IsNullOrWhiteSpace(paymentTransactionId) || !_devItemTransactionIds.ContainsKey(paymentTransactionId))
                    return new IyzicoRefundResult
                    {
                        Success = false,
                        ErrorMessage = "Bu isyerine ait odeme kirilim kaydi bulunamadi (mock)"
                    };
                return new IyzicoRefundResult { Success = true, RefundId = Guid.NewGuid().ToString("N") };
            }

            var options = BuildOptions();
            var ci = CultureInfo.InvariantCulture;
            // Açıklayıcı yorum: Iyzipay Refund - paymentTransactionId ile kısmi/tam iade
            var request = new CreateRefundRequest
            {
                Locale = Locale.TR.ToString(),
                ConversationId = Guid.NewGuid().ToString("N"),
                PaymentTransactionId = paymentTransactionId,
                Price = amount.ToString(ci),
                Currency = Currency.TRY.ToString()
            };
            var result = await Task.Run(() => Refund.Create(request, options));
            if (result.Status != "success")
            {
                _logger.LogWarning("Iyzico refund başarısız: {Error}", result.ErrorMessage);
                return new IyzicoRefundResult { Success = false, ErrorMessage = result.ErrorMessage };
            }
            return new IyzicoRefundResult { Success = true, RefundId = result.PaymentId };
        }

    }
}

