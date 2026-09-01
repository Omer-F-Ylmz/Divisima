## MINI DALGA (LAUNCH ONCESI SON ISLER) - TAMAMLANDI

Sprint 8 kapandiktan sonra kullanicinin actigi kapsam-sinirli dalga: bes kalem.

### (a) `workflow_dispatch` TETIGI

`security.yml`'a eklendi. **Gerekcesi OLCUMDUR, kolaylik degil:** `gitleaks-action` kaynagi
okundu - `push` yalniz SON COMMIT'i tarar (`--log-opts=-1`), `schedule`/`workflow_dispatch`
ise HICBIR `--log-opts` almaz ve TUM GECMISI tarar. Yani ".gitleaksignore gercekten tutuyor
mu" sorusunu bir PUSH kosumu ASLA yanitlayamaz. Tetik olmadan tek kanit haftalik cron'du.
**ELLE TETIKLEME KULLANICIDA:** `POST .../workflows/{id}/dispatches` KIMLIK ISTER; anonim
API ile tetiklenemez ve PAT ISTENMEZ (ev kurali). Tetik main'e dustukten sonra GitHub
arayuzunde "Run workflow" gorunur.
**SONUC:** kullanici tetikledi - **run 32540908505 SUCCESS**, `Gitleaks (secret taramasi)`
SUCCESS. O kosum TUM GECMISI taradi (jetonlarin durdugu `19d101f` DAHIL); `.gitleaksignore`
fingerprint'leri TUTTU. Dogrulama boslugu KAPANDI.

### (b) SUPHELI #15 KAPANDI - WEBHOOK'TA TOKEN YASI SINIRI GEVSEDI

**TASARIM OLCEREK KURULDU.** Onceki imza `HandleCallback(dto, bool imzaZorunlu = true)` idi.
Olculdu: Sprint 8 madde 9'dan sonra **her iki uretim cagri yeri de `false` veriyordu**, yani
bayrak artik KANALI ayirt etmiyordu. Ikinci bir bool eklemek (`tokenYasiSiniriUygula`)
gecersiz bilesimlere kapi acardi (`imzaZorunlu: true` + `tokenYasi: false` gibi hicbir kanalin
karsiligi olmayan bir kombinasyon).

**SECILEN: TEK ENUM** - `PaymentNotificationChannel { Strict = 0, BrowserCallback, ProviderWebhook }`.
Politika TEK YERDE turer (`HandleCallback` basi), cagri yerleri yalnizca KANALI soyler.
Varsayilan `Strict` - FAIL-CLOSED. Gerekce enum'un basinda, `SuccessDataResult` belirsizligi
(madde 11) referansiyla: "bir bayragin sessizce yanlis anlama gelmesi" bedeli bu depoda bir kez
odendi, ayni tuzak bilerek tekrarlanmiyor.

| Kanal | Imza | Token yasi siniri (30 dk) |
|---|---|---|
| `Strict` (varsayilan) | ZORUNLU | UYGULANIR |
| `BrowserCallback` | gelirse dogrulanir | **UYGULANIR** (tarayici replay'i gercek senaryo) |
| `ProviderWebhook` | gelirse dogrulanir | **UYGULANMAZ** |

**CF-CALLBACK YOLUNA DOKUNULMADI** (kullanici sarti) - pinli.
Gevseyen TEK sey yas siniri: yalniz-Pending + retrieve otoritesi + tutar + para birimi + fraud
AYNEN duruyor.

**STOK TARAFI OLCULDU** (relaxation oncesi zorunlu kontrol): `ConfirmReservation` "rezervasyon
expire olmustu ama odeme basarili" durumunu ELE ALIYOR - stok varsa dogrudan dusuyor, yoksa
hareket kaydina GURULTULU uyari yaziyor. Yani sessiz overselling riski YOK **diye dusunuldu** -
ama (c)'deki canli kurtarma bu telafinin OLU oldugunu gosterdi; bkz. **SUPHELI #18**.

PINLER (`WebhookContractTests`): `GECIKMIS_GercekBildirim_WEBHOOKTA_FAILEDLANMAZ_Confirmeda_Tasir` ·
`AyniGecikme_TARAYICI_CALLBACKINDE_TokenYasi_Guardina_TAKILIR` (cift-anlam kirici - gevseme
KANAL BAZLI) · `VARSAYILAN_KANAL_STRICT_GecikmisTokeni_REDDEDER_FailClosed` (gecerli imza
gonderilir ki red sebebi YAS olsun).

### (c) SIPARIS #33 KURTARILDI - KURTARMA YOLUNUN CANLI KANITI

(b) girdikten sonra gercek webhook govdesi elle tetiklendi (token yasi **173 dakika**).

```
YANIT : 200 in 1063 ms   ("Ödeme başarılı, siparişiniz onaylandı.")
        1063 ms = retrieve GERCEKTEN kostu (gercek Iyzico sorgusu)
orders   #33  status=1 (Confirmed)  is_online_payment_done=1
payments      payment_status=1  transaction_id=37415135  item_transaction_id=39331730
              paid_price=1049.70
outbox        PaymentConfirmed x1 -> status=1 (Processed)  retry_count=0
invoices      1 satir  DIV-2026-000033  status=1 (Sent)
loyalty       1 satir  104 puan
timeline      "Ödeme onaylandı"  UYARI/KRITIK notu: 0
```

`transaction_id` 1. turdaki gercek bildirimin `iyziPaymentId` degeriyle BIREBIR AYNI - yani
kurtarilan sey gercekten O odeme.

**AMA STOK DUSMEDI** - bkz. SUPHELI #18. Kurtarma odeme/siparis/fatura/puan tarafinda tamdir,
envanter tarafinda DEGILDIR.

### (d) SUPHELI #17 KAPANDI - CALLBACK DA "payment" KOVASINDA

`Callback` action'ina `[EnableRateLimiting("payment")]`. Yeni bir sayi degil: Redis yolu
(`/payment/` -> 10/dk) ile yerlesik yolu ayni davranisa getiriyor.
PIN (`PaymentCallbackRedirectTests`): `Callback_PAYMENT_KOVASINDA_OnBirinci_Istek_429`
(AYRI host, uretim varsayilani; ilk on istek **302** aliyor - uygulamaya ULASIYORLAR).
Sinifin diger pinleri icin ana fabrikada limit yukseltildi (iki-host deseni).

### (e) SUPHELI #16 BILINCLI BOS BIRAKILDI (kullanici karari)

`Webhook:AllowedIps` DOLDURULMUYOR. Gerekce deftere ve `appsettings.Development.example.json`
aciklamasina yazildi: bu uc, kaybolan callback'in TEK kurtarma yoludur; liste BAYATLARSA
gercek bildirimler 403 yer ve kurtarma yolu SESSIZCE OLUR - **yanlis doldurulmus bir allowlist
bos birakmaktan DAHA TEHLIKELIDIR**. Doldurulacaksa: yalniz resmi Iyzico IP listesinden,
bayatlama riski bilinerek ve `ForwardedHeaders:KnownProxies` ile BIRLIKTE.
Ayrica example.json'daki eski "yalniz imza kalir" ifadesi DUZELTILDI - madde 9'da olculdu ki
gercek bildirim imza TASIMIYOR.
**SUPHELI #14 launch-sonrasi deftere alindi.**

### DIS KONTROLU + 5. KONTROL

5 assert ters -> **5 AYRI ISIMLI KIRMIZI** (geri alindi).
5. kontrol, iki uretim mutasyonu TEK dalgada (farkli testleri vurduklari icin ayristirilabilir):
- `tokenYasiSiniriUygula = true` (kanal gevsemesi geri alindi) -> `GECIKMIS_GercekBildirim_...`
  **400** dondu; siparis #33'un kurtarma ONCESI zarari BIREBIR. `AyniGecikme_TARAYICI_...` ve
  `VARSAYILAN_KANAL_STRICT_...` dogru sekilde YESIL kaldi (mutasyon KATI davranisi bozmuyor).
- `[EnableRateLimiting("payment")]` kaldirildi -> `Callback_..._429` on birinci istekte **302**
  buldu; olculen boslugun aynisi.
Ikisi de geri alindi.

