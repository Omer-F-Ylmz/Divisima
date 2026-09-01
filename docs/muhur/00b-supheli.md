## SUPHELI DAVRANISLAR

**DURUM: ACIK KALEMLER #14 (LAUNCH SONRASI) ve #20 (bugun BOSLUK YOK, testte kapatildi).**
**#22 KAPANDI - GUVENLIK-FIX-4 (govde SHA-256 bagi + tek kaynak kimlik + bayt-birebir replay).**
**#21 KAPANDI - A2-FIX (kullanici karari: sifre politikasi TEK MERKEZDEN, dort giriste de).**
**#19 KAPANDI - GUVENLIK-FIX-2 (kullanici karari: secenek iii).**
Kapananlar: #1..#13 ilgili sprintlerde · **#15, #17, #18 mini dalgalarda** ·
**#16 BILINCLI olarak bos birakildi (verilmis karar, erteleme degil)**.
Asagidaki maddeler kayit olarak duruyor; her birinin basinda guncel durumu yazili.

Sprint 5'in iki maddesi (kilit/sadakat ciftlenmesi + kumulatif iade siniri) **S6'da**,
Sprint 6'nin iki maddesi (basarisiz odemede fatura + transaction'siz callback) **S7'de**
KAPANDI. Acik kalan / yeni bulunanlar:

1. **Kupon `used_count` artisi IDEMPOTENT DEGIL.** (S7 tasarim calismasinda olculdu)
   `IncrementCouponUsageWithRetry` duz bir sayac artisidir. Bugun zararsiz cunku
   callback tam bir kez calisiyor; ama B bolgesi at-least-once bir mekanizmaya
   (outbox) tasinirsa sayac FAZLA sayar. Sprint 8'in on kosulu - defterde.
   Cozum adaylari: `coupon_usages` satirlarindan turetmek ya da `(coupon_id, order_id)`
   unique indeks + artisi insert basarisina baglamak.
2. **`InvoiceManager.GenerateForOrder` siparis DURUMUNU kontrol etmiyor.** (S7'de
   okundu, dokunulmadi) Var olan herhangi bir siparis id'si icin fatura kesiyor;
   tek koruma cagiranin dogru yerden cagirmasi. S7'de cagri onay dalina tasindigi
   icin bugun sorun yok, ama uc kendi basina korumasiz. Duzeltme karari kullanicinin.
3. **`LocalImageStorage` dosyayi CWD'ye yaziyor, `UseStaticFiles` ContentRoot'tan sunuyor.**
   (E4a'da OLCULDU) `PhysicalRoot = Directory.GetCurrentDirectory()/wwwroot/uploads/products`,
   sunum ise `IWebHostEnvironment.WebRootPath` (= ContentRoot/wwwroot). Ikisi yalniz
   CALISMA DIZINI content root ile AYNI oldugunda ortusur - `dotnet run --project` ve
   normal yayinlarda ortusuyor, ama calisma dizini farkli baslatilan bir servis (systemd
   `WorkingDirectory` verilmemis, Windows Service) yuklemeleri hic servis edilmeyen bir
   dizine yazar: yukleme "basarili" doner, gorsel SONSUZA KADAR 404. Testte bu ayrisma
   birebir gozlendi (dosya test bin'ine yazildi, 404 alindi) ve test host'unda
   `UseContentRoot(CWD)` ile hizalandi. Uretim duzeltmesi `WebRootPath` kullanmak olurdu -
   YAPILMADI, karar kullanicinin.
   **E2b: ARTIK TEORIK DEGIL - CANLI ORTAMDA GERCEKLESTI.** Storefront urun 2 gorselleri
   icin 404 aliyor. Olculdu: `product_images` tablosunda 3 satir var
   (`/uploads/products/3088...png` dahil, `is_primary=1`), ama
   `Divisima.API/wwwroot/uploads/products/` **BOS** ve dosya adi repo genelinde
   HICBIR YERDE yok. Dosyalari iceren TEK dizin
   `Divisima.IntegrationTests/bin/Release/net8.0/wwwroot/uploads/products` (test
   yuklemeleri). Yani E4a'da yuklenen gercek gorseller, o anki CALISMA DIZININE yazilmis
   ve orasi sunulan dizin DEGIL; sonucta veritabani "gorsel var" diyor, dosya yok,
   vitrin SONSUZA KADAR 404. Tam olarak yukarida ongorulen zarar. Sprint 8 madde 4.
4. **Storefront liste yolu `category_name` / `total_stock` / `sizes` DOLDURMUYOR.**
   (E1'de olculdu, pinlendi) `ProductProfile` ucunu de `Ignore` ediyor; admin `GetList`
   `sizes`'i sonradan dolduruyor ama `GetListSearchAndFilterWithPaging` (yorumunda
   "storefront" yazan yol) hicbirini doldurmuyor. Ham veriyle vitrindeki HER urun
   "kategorisiz + 0 stok + bedensiz" -> bastan sona "Tukendi" gorunur. E1 istemci
   tarafinda telafi etti (kategori: `category_id`+kategori listesi; stok/beden: urun
   basina detay cagrisi, sayfa boyutu 24). Kalici duzeltme backend'de: liste yolu da
   admin `GetList` gibi doldurmali. Pin: `Filter_ListeYolu_..._DOLDURMUYOR_PINLENIR`.
5. **Refresh token httpOnly cookie ile TASINMIYOR (devir notu YANLISTI).**
   (E1'de olculdu) `AuthController.SetRefreshTokenCookie` TANIMLI ama HIC CAGRILMIYOR;
   login refresh token'i GOVDEDE donuyor, `/api/auth/refresh` `[FromBody]` bekliyor,
   `AuthManager.RefreshToken` hicbir yerde cookie okumuyor. `Logout` ise hic yazilmayan
   cookie'yi okuyor (`Request.Cookies["refresh_token"]` -> null). Yani "access localStorage
   + refresh httpOnly cookie" modeli YARIM: yazma yolu olu. E1 istemciyi bugun CALISAN
   sozlesmeye (govde) uydurdu; refresh token JS'in erisebildigi yerde duruyor ve bu
   httpOnly'den ZAYIF. Duzeltme BACKEND isi (cookie yaz + cookie'den oku + logout'u
   duzelt), karar kullanicinin.
6. **[KAPANDI - E3] Hesabim > Siparislerim ekrani MOCK siparis listesi ciziyordu ve COKUYORDU.**
   (E2b'de olculdu) `index.html` satir 2524'teki `accOrders()` `MOCK_ORDERS` uzerinde
   donuyor ve her kalem icin `byId(id).price` okuyor. E1 katalogu gercek API'ye
   bagladigi icin `byId` artik yalniz GERCEK urunleri biliyor; mock siparislerin kalem
   id'leri (olculdu: 1, 8, 5, 13, 18, 3) gercek katalogda (olculdu: 2, 1) karsilik
   BULMUYOR -> `byId(8)` undefined -> "Uncaught TypeError: Cannot read properties of
   undefined (reading 'price')" ve tum `renderAccount` render'i cokuyor (yakalandi:
   `router()` cagrisi bu istisnayla duruyor, `accountView` BOS kaliyor).
   E2b SADECE yalani ve cokmeyi kaldirdi (`api-bridge.js` -> `wireAccountOrders`,
   `window.accOrders` ezilir, notr durum cizilir). GERCEK listeyi
   (`/api/order/my-orders` + zaman cizelgesi) baglamak **E3 madde (a)**; oraya kadar
   ekran gercek siparisleri GOSTERMIYOR.
   **KAPANIS (E3):** yedi sekmenin tamami gercek uclara baglandi; elle dogrulamada 18 gercek
   siparis, tembel acilan kalem + zaman cizelgesi, iade talebi ve iade listesi uctan uca
   suruldu. `wireAccountOrders` gecici yamasi kaldirildi.
7. **[KAPANDI - E2b] SERVICE WORKER SURUMLEME YOK - YAYINLANAN DUZELTME KULLANICIYA ULASMIYORDU.**
   (E2b'de olculdu; kullanicinin suphesi dogru cikti) `frontend/service-worker.js`:
   - `const CACHE = "divisima-v1"` **SABIT** - hicbir surumleme/hash yok.
   - `SHELL = ["/", "/index.html", "/manifest.json", "/api-client.js"]` -> `index.html`
     (yani **CSP meta etiketi**) install aninda onbellege aliniyor.
   - API disi her GET **cache-first**: `caches.match(req).then(cached => cached || fetch(...))`.
     Onbellekte varsa aga HIC cikilmaz.
   - Fetch handler her GET yanitini onbellege YAZIYOR -> `api-bridge.js` de ilk yuklemede
     girip sonsuza kadar oradan servis ediliyor.
   - `activate` yalniz `k !== CACHE` olanlari siliyor; CACHE hic degismedigi icin
     **hicbir sey silinmiyor**.
   - SW dosyasi kendisi degismedigi icin tarayici YENI SW kurmuyor; `skipWaiting()` /
     `clients.claim()` hic devreye girmiyor.
   Sonuc: ilk ziyaretten sonra `index.html` ve `api-bridge.js` kullanicinin tarayicisinda
   DONMUS. E2b'de CSP duzeltmeleri ancak Ctrl+Shift+R (navigasyonda SW atlanir) ile
   ulasti, normal yenileme/yeni sekmede ESKI surum geri geldi - teshis bu yuzden
   tutarsiz gorunuyordu. URETIMDE ANLAMI: yayinlanan hicbir duzeltme (guvenlik yamasi
   dahil) mevcut kullanicilara ULASMAZ. Aday duzeltme: CACHE adini her dagitimda degisen
   bir surume baglamak + navigasyon/`index.html` icin network-first. YAPILMADI -
   kullanici acikca "olc ve adim adim talimat ver" dedi, kod degisikligi istemedi.
   **EK KANIT (E2b, 2. olay): ORIGIN ERISILEMEZKEN DE ESKI SURUM SERVIS EDILIYOR.**
   Statik sunucu (:5173) fark edilmeden olmustu (`curl` -> `http=000`). Tarayici yine de
   sayfayi ACTI: SW'nin fetch handler'indaki `.catch(() => caches.match("/index.html"))`
   dali devreye girip ONBELLEKTEKI ESKI index.html'i servis etti - `?v=2` cache-buster'i
   dahil. Kullanici "duzeltme uygulanmadi" sanirken aslinda SUNUCU KAPALIYDI ve SW bunu
   GIZLEDI. Uretimdeki karsiligi: origin coktugunde kullanici hicbir hata gormez, aylar
   once onbellege alinmis bir surumu kullanmaya devam eder ve operasyon kesintiyi
   musteri tarafinda GOREMEZ. Ayni duzeltme (surumlu CACHE + navigasyonda network-first)
   bunu da kapatir.
   **KAPANIS (E2b - kullanicinin KENDI tarayicisinda olculdu).** Duzeltme iki ayak uzerine
   kuruldu: (a) `VERSION` sabiti -> `CACHE = "divisima-" + VERSION`, `activate` artik
   `k !== CACHE` olan HER onbellegi gercekten siliyor, `skipWaiting` + `clients.claim`
   devrede; (b) navigasyon + `.html` + `.js` NETWORK-FIRST, yani VERSION bumpi UNUTULSA
   BILE yayinlanan duzeltme ulasir - surumleme temizlik icin, tek dayanak degil. Offline
   yedegi YALNIZ navigasyona veriliyor (bir `.js` istegine HTML donmek "sunucu oldu"
   durumunu gizliyordu). `pwa-register.js`'e `reg.update()` eklendi - statik sunucu cache
   basligi gondermedigi icin tarayici SW betigini gec fark edebiliyordu.
   OLCUM (Bypass KAPALI, elle temizlik YOK, 2 x normal F5): dortlu CSP kontrolu TRUE,
   `caches.keys()` -> `['divisima-2026-08-21-e2b']` (**`divisima-v1` YOK** - activate sildi),
   SW kaydi 1. Yani guncelleme kullaniciya ELLE MUDAHALE OLMADAN ulasti.


8. **`SuccessDataResult<string>` ASIRI YUKLEME BELIRSIZLIGI - KOK SEBEP ACIK.**
   (E3'te olculdu; iki cagri yeri E3'te DUZELTILDI, kok sebep DURUYOR) `T = string` oldugunda
   `(T data)` ile `(string message)` AYNI imzaya duser; C# generic OLMAYAN adayi secer. Tek
   argumanli `new SuccessDataResult<string>(x)` veriyi MESSAGE'a yazar, `Data` null kalir ve
   `Success` true oldugu icin **hata SESSIZDIR**. Olculen zarar: `invoice-html` **200 +
   Content-Length: 0** (Faturalarim ekrani hic calismamisti) ve `referral/my-code`
   `{"data":null,...,"message":"REF..."}`. E3 yalniz iki cagriyi `data:` adlandirilmis
   argumana cevirdi; **kurucu setine dokunulmadi**, yani yeni yazilacak tek argumanli bir
   string cagrisi yine sessizce bozuk olur. Kokten cozum karari kullanicinin -
   **SPRINT 8 MADDE 11**. Bugunku davranis uc duzeyinde pinli (`ResultOverloadPinTests`).

9. **Bildirim aboneliklerinde `unsubscribe` ve "aboneliklerim" UCU YOK.** (E3'te olculdu)
   Tum controller'lar tarandi: yalniz `subscribe` var. Kullanici kurdugu stok/fiyat
   bildirimini ne GOREBILIYOR ne KAPATABILIYOR. E3 istemcisi bunu gizlemiyor (abonelik TEK
   YONLU kuruluyor, geri alma sozu verilmiyor) ama kalici cozum backend isi.
   **SPRINT 8 MADDE 10.**

10. **[KAPANDI - SPRINT 8 MADDE 12] `#/urun/{id}` PAYLASIM BAGLANTILARI.**
   **ONEMLI DUZELTME: bu maddenin E3'teki TESHISI YANLISTI.** Asagidaki eski metin
   "router `#/urun` yolunu TANIMIYOR" diyordu; Sprint 8'de kaynak okunup TEKRAR olculdu ve
   yol `index.html:2077`'de MEVCUT cikti:
   `else if(top==='urun'){ showHome(); var _pid=+h[1]; if(byId(_pid)) openDetail(_pid); }`
   Olcum: `#/urun/1` ile acilan sayfada gorunen view **"home"**, `detailOpenId` **1** - yani
   urun detayi GERCEKTEN aciliyor. Gordugum "Sayfa Bulunamadi" bir 404 SAYFASI DEGIL, SAYFA
   BASLIGIYDI; ilk raporda bu ikisi karistirilmisti.
   **GERCEK KUSUR IKI TANEYDI (ikisi de duzeltildi):**
   (a) `setDocTitle()` icinde `urun` dali YOK - bilinmeyen yol dalina duser. Ustelik router
       onu `openDetail`DEN SONRA cagiriyor, yani `setProductSchema`'nin koydugu dogru baslik
       hemen EZILIYOR. Paylasilan her urun baglantisi sekmede ve sosyal onizlemede
       "Sayfa Bulunamadi" gorunuyordu.
   (b) Katalog yarisi: acilista router PRODUCTS'in O ANDAKI (mock) icerigiyle calisiyor,
       gercek katalog asenkron geliyor ve `loadCatalog` sonrasi yeniden yonlendirme YALNIZ
       `#/kategori` icin yapiliyordu (Favorilerim'de bu oturumda olculen yarisin aynisi).
   Duzeltme `api-bridge.js`'te: `setDocTitle` sarmalandi + katalog sonrasi `urunRotasiniTazele()`.
   OLCULEN SONUC: baslik "Sayfa Bulunamadi · Divisima" -> **"Siyah Midi Elbise · Divisima"**.

   Eski (YANLIS) teshis, kayit icin: (E3'te olculdu)
   `index.html:2154` `shareUrl(id)` -> `#/urun/<id>` uretiyor ve urun kartindaki WhatsApp /
   Facebook / X / Pinterest / "baglantiyi kopyala" secenekleri bu adresi paylasiyor. Ancak
   urun detayi bir ROTA DEGIL, `openDetail(id)` ile acilan bir MODAL; router `#/urun` yolunu
   TANIMIYOR. Olculdu: `location.hash = "#/urun/1"` -> sayfa basligi **"Sayfa Bulunamadi ·
   Divisima"**. Uretimdeki anlami: paylasilan her urun baglantisi 404 sayfasina dusuyor -
   sosyal trafik ve SEO tarafinda dogrudan kayip. E3 KAPSAMI DISI (E3 hesap/CMS/bildirim
   yuzeyi), duzeltilmedi. Duzeltme adayi: router'a `#/urun/:id` yolu eklemek ve o yolda
   katalog yuklendikten sonra `openDetail(id)` cagirmak.

11. **`dvs_profile.email` GERCEK GIRISTE DOLDURULMUYOR.** (E3'te olculdu) index.html kendi
   yerel profil deposunu (`dvs_profile`) ve ondan tureyen `window.userEmail` degiskenini
   kullaniyor; E1 girisi gercek uclara bagladi ama e-posta alanini DOLDURMUYOR. Olculdu:
   giris yapilmis kullanicida `dvs_profile = {"name":"E3 Fix","email":""}`. E3 bunu KENDI
   tuketicisi icin kapatti (fiyat uyarisi artik `/api/Account/summary`'den okuyor ve
   `window.userEmail`'i de esitliyor), ama index.html'in o degiskeni okuyan DIGER yerleri
   hala bos gorebilir. Genel duzeltme (girisin profil deposunu gercek ozetle doldurmasi)
   yapilmadi - karar kullanicinin.

12. **Fatura HTML'inin satir ici `<style>` blogu okuma katmaninda SOKULUYOR.** (E3'te olculdu,
   BILINCLI) `OrderManager.GetInvoiceHtml` govdeyi satir ici `<style>` ile uretiyor; okuma
   katmanindaki DOMPurify izin listesinde `style` etiketi YOK, bu yuzden modal faturayi
   BICIMSIZ (sade tablo) ciziyor. Icerik TAM - siparis no, kalemler, matrah/KDV, genel toplam
   hepsi var. Guvenli taraf bilincli secildi (`style` etiketini acmak CSS enjeksiyonu yuzeyi
   getirir). Kalici cozum adaylari: faturayi `sandbox`'li bir iframe'de servis etmek ya da
   bicimlendirmeyi storefront'un kendi CSS'ine tasimak. Duzeltme YAPILMADI.

13. **UYGULAMA KULTUR PINLEMIYOR - PARA BICIMLENDIRMESI ORTAMA GORE DEGISIYOR.**
   (E3 run'inda CANLI ORTAMDA kanitlandi) `Program.cs`'te ne `RequestLocalization` ne
   `CultureInfo.DefaultThreadCurrentCulture` var; `csproj`'de `InvariantGlobalization`
   ayari da yok (tum cozum tarandi). `OrderManager.GetInvoiceHtml` tutarlari
   `{order.total_price:N2}` ile, yani **AMBIENT kulturle** basiyor.
   OLCUM: `tr-TR` -> `549,90` / `1.049,70`;  Invariant -> `549.90` / `1,049.70`.
   GitHub kosucusu (Linux, LANG=C.UTF-8) invariant kulturde kostugu icin fatura govdesi
   orada NOKTA ayracli cikti - bu, testin kultur bagimli literalini kirdi ve boylece
   davranis ORTAMDA GORULDU (teori degil). Uretimdeki anlami: Turk musteriye kesilen
   faturanin tutari, uygulamanin kostugu kabin/konteyner yerelinden etkileniyor;
   `LANG` verilmemis bir Linux dagitiminda `1,049.70 TL` yazar.
   Ayni risk fatura disindaki her `:N2` / `:C` / tarih bicimlendirmesi icin gecerli.
   **KARAR VERILDI (kullanici): SPRINT 8 MADDE 13'e yukseltildi, DOGRULUK commit'ine
   girer.** Magaza TEK PAZARLI (TR / TRY); tasarim olcerek kurulacak ve fatura govdesinin
   kosucu kulturunden BAGIMSIZ `tr` bicimiyle ciktigi pinlenecek.

14. **`X-Api-Version` BASLIGI AYRISTIRILAMAZSA TUM API BLANKET 400 VERIYOR.** (Sprint 8
   madde 9'da olculdu) `HeaderApiVersionReader("X-Api-Version")` ayristiramadigi bir degerle
   karsilasinca istegi **hangi uca giderse gitsin** bos govdeli 400 ile dusuruyor - ve bunu
   endpoint'in versiyon-NOTRLUGUNE bakmadan yapiyor (`[ApiVersionNeutral]` action ve controller
   duzeyinde AYRI AYRI denendi, ikisi de ENGELLEMEDI). Yani basligi "V1", "v1.0-beta", "latest"
   gibi bir degerle gonderen HERHANGI bir ucuncu taraf entegrasyonu, uctan bagimsiz olarak
   erisemez hale gelir. Ustelik yanit govdesi BOS oldugu icin karsi taraf sebebi goremez -
   Iyzico entegrasyonunda tam olarak bu yasandi ve teshis ancak sunucu logundan yapilabildi.
   Sprint 8 madde 9 YALNIZ `/api/payment/webhook` yolunu muaf tutti (kapsam bilerek dar,
   pinli). **GENEL COZUM KARARI KULLANICININ:** aday (i) ayristirilamayan degeri YOK SAYAN
   tolere edici bir okuyucu (mevcut istemciler etkilenmez, bozuk baslik sessizce varsayilana
   duser), (ii) 400'u KORUYUP govdeye acik bir hata mesaji koymak (teshis edilebilir olur ama
   entegrasyon yine kirilir). Bugunku davranis DIGER uclar icin pinli
   (`AyniV1Basligi_BASKA_BIR_UCTA_HALA_400_KAPSAM_DAR_KALDI`).

15. **30 DK TOKEN ZAMAN ASIMI WEBHOOK KURTARMA YOLUNU DA SINIRLIYOR.** (Sprint 8 madde 9'da
   olculdu) `HandleCallback`'teki `payment.created_at.AddMinutes(30) < DateTime.Now` guard'i
   TARAYICI callback replay'i icin dogru bir savunmadir; ama webhook AYNI kodu kullaniyor ve
   webhook FARKLI zamanlama karakteristigine sahip bir kanaldir (saglayici bildirimi
   geciktirebilir ya da saatler sonra yeniden deneyebilir). Bugunku davranis: 30 dakikadan
   eski bir GERCEK bildirim geldiginde odeme **Failed** isaretleniyor ve 400 donuyor - yani
   parasi ALINMIS bir odeme "basarisiz" olarak defterlenip mutabakat kaybediliyor. Siparis #33
   canli ornek: kurtarma denenemedi cunku token 58 dakikalikti ve tetiklenseydi kanit da
   bozulurdu. Bugun siparis Pending kaliyor (Failed'dan daha durust bir hal), ama bu SANS
   eseri - guard tetiklenseydi Failed olurdu. Aday cozumler: (i) webhook yolunda zaman asimini
   uygulamamak (otorite zaten retrieve - saglayici odemenin gercek durumunu soyluyor),
   (ii) zaman asimini gecen ama retrieve'i SUCCESS donen odemeler icin "elle mutabakat"
   kuyrugu acmak.
   **[KAPANDI - MINI DALGA] (i) SECILDI.** Kanal ayrimi bir enum'a tasindi
   (`PaymentNotificationChannel`); yas siniri YALNIZ `ProviderWebhook`'ta gevsedi, tarayici
   yolunda AYNEN duruyor. Ayrinti ve pinler: MINI DALGA bolumu (b).

16. **`Webhook:AllowedIps` ALLOWLIST'I VAR AMA BOS - VE PROXY ARKASINDA CALISMAZ.**
   (Sprint 8 madde 9'da bulundu) `WebhookIpAllowlistMiddleware` `/api/payment/webhook` yolunu
   saglayici IP araliklarina kapatabiliyor, ama `Webhook:AllowedIps` listesi depoda HICBIR
   YERDE doldurulmamis; bos oldugu icin middleware TAMAMEN atlaniyor. Bu, imza olmayan bir
   ucta mevcut EN GUCLU ek savunma katmani ve yalniz YAPILANDIRMA isi - kod degisikligi
   gerektirmiyor. Sprint 8'de olculen gercek Iyzico bildirimi
   `Cf-Connecting-Ip=213.226.118.95` tasiyordu (tunel uzerinden gorulen kaynak).
   **IKI UYARI:** (a) saglayici IP'leri degisebilir - liste bayatlarsa GERCEK bildirimler 403
   yer ve kurtarma yolu yine olur, yani liste ancak izlenirse guvenlidir; (b) middleware
   `RemoteIpAddress` okuyor - ters proxy/LB arkasinda `ForwardedHeaders:KnownProxies`
   DOLDURULMAZSA bu deger proxy'nin IP'sidir ve allowlist ya herkesi gecirir ya herkesi
   reddeder. Iki ayar birlikte anlamlidir (ayni not rate limit bolumunde de var).
   **[KARAR VERILDI - MINI DALGA] BILINCLI OLARAK BOS BIRAKILDI.** Gerekce: bu uc, kaybolan
   callback'in TEK kurtarma yoludur; liste BAYATLARSA gercek bildirimler 403 yer ve kurtarma
   yolu SESSIZCE OLUR - yanlis doldurulmus bir allowlist bos birakmaktan DAHA TEHLIKELIDIR.
   Doldurma kosullari `appsettings.Development.example.json`'daki `//Webhook3` / `//Webhook4`
   aciklamalarina yazildi. Bu bir ERTELEME DEGIL, VERILMIS bir karardir.

17. **`/api/payment/callback` RATE LIMIT POLICY'SI DISINDA.** (Sprint 8 madde 9'da olculdu)
   `[EnableRateLimiting("payment")]` yalniz `Initialize` uzerindeydi; madde 9'da `Webhook`'a da
   eklendi. `Callback` HALA yalniz GlobalLimiter'in 100/dk'sinda (yerlesik yolda). Redis yolu
   path eslesmesiyle (`/payment/`) onu da 10/dk'ya bagliyor - yani IKI YAPILANDIRMA HALA
   AYRISIYOR, sadece webhook icin hizalandi. Callback bir TARAYICI ucu oldugu icin farkli
   degerlendirilebilir (musteri basina dogal olarak seyrek), ama ayrisma bilincli bir karar
   degil, sadece kapsam disinda kalmis bir bosluk.
   **[KAPANDI - MINI DALGA (d)]** `Callback` action'ina `[EnableRateLimiting("payment")]`
   eklendi; iki yapilandirma artik ayni davraniyor. Pin:
   `Callback_PAYMENT_KOVASINDA_OnBirinci_Istek_429`.

18. **[KAPANDI - MINI DALGA 2] `ConfirmReservation` EXPIRE OLMUS REZERVASYONU HIC GORMUYOR:
   ONAY STOGU DUSURMUYOR VE UYARI DA YAZMIYOR.**
   **KAPANIS:** sorgu `Active` VEYA `Expired`'i kapsayacak sekilde genisletildi; `Released`
   ANLAMSAL gerekceyle DISARIDA birakildi; telafi dali ATOMIK gecise baglandi (kendi actigi
   ikinci-dusum kapisini kapatir); "stok yok" uyarisi hareket kaydinin YANINDA siparis zaman
   cizelgesine de dusuluyor. Ayrinti, sinirin gerekcesi ve pinler: **MINI DALGA 2** bolumu.
   Asagidaki metin bulgunun kaydidir.
   Siparis #33'un kurtarmasinda OLCULDU. Kurtarma odeme tarafinda kusursuz calisti (Success,
   Confirmed, fatura DIV-2026-000033, 104 puan) ama:
   ```
   stock_reservations  id=34  order_id=33  status=3 (Expired)
   product_stocks      urun 2 / M  stock_quantity=10  reserved_quantity=0   (DEGISMEDI)
   stock_movements     reference_id=33 -> 0 SATIR                            (UYARI BILE YOK)
   order_items         urun 2 / M  quantity=2                                (2 adet satildi)
   ```
   **KOK SEBEP (kaynak okundu):** `StockManager.ConfirmReservation` ilk satiri
   `GetListAsync(r => r.order_id == orderId && r.status == Active)` - sorgu YALNIZ **Active**
   rezervasyonlari getiriyor. Icerideki "expire olmustu, stogu yeniden guvenceye al; yoksa
   `UYARI: odeme alindi fakat stok yok ... manuel iade/tedarik gerekli` yaz" telafi dali
   yalniz `TryTransitionAsync` **0** dondugunde, yani expire islemi sorgu ILE gecis ARASINDA
   olustugunda calisiyor. Rezervasyon sorgu anINDA **ZATEN Expired** ise dongu HIC donmuyor ve
   o telafi dali **OLU** kaliyor. (Madde (b) hazirliginda bu telafiyi okuyup "sessiz overselling
   riski yok" demistim - **YANLISTI**, telafi yalniz YARIS durumunu kapsiyor. Duzeltiliyor.)
   **URETIMDEKI ANLAMI:** para alinir, siparis Confirmed olur, fatura kesilir, puan yazilir -
   fiziksel stok DUSMEZ ve kimse bunu goremez (hareket kaydi bile yok). Envanter SESSIZCE sisirilir.
   **KAPSAM:** yalniz webhook degil - `ConfirmReservation`'i cagiran HER onay yolu (COD/havale
   admin onayi dahil) ayni bosluga sahip. Yani bosluk (b) ile OLUSMADI, ama (b) "uzun sure
   Pending kalmis siparisi onayla" yolunu NORMALLESTIRDIGI icin **ulasma olasiligini artirdi**.
   **PINLENDI, DUZELTILMEDI** (ev kurali): `WebhookContractTests` ->
   `SUPHELI_RezervasyonEXPIRE_Olduysa_Onay_STOK_DUSURMUYOR_ve_UYARI_YAZMIYOR_PINLENIR`.
   Pin, GERCEK temizlik yolunu (`IStockService.ReleaseExpiredReservations`) kosturarak on kosulu
   kuruyor - sahte kurgu degil. Aday duzeltme: `ConfirmReservation`'in sorgusunu Active +
   Expired'i kapsayacak sekilde genisletmek (mevcut telafi dali zaten yazili, yalnizca
   ULASILAMIYOR). **Duzeltme karari kullanicinin.**


19. **[KAPANDI - GUVENLIK-FIX-2] HESAP KILITLENMESI BIR ENUMERATION KANALIYDI (G2'nin KALAN yuzeyi).**
   **KAPANIS:** kullanici karari secenek (iii) - kilit bilgisi YALNIZ SIFRE DOGRUYSA bildirilir.
   Yanlis sifre + kilitli hesap artik kayitsiz adresle BIREBIR ayni 401'i doner; dogru sifre +
   kilitli hesap 403 kilit mesajini alir. Sira degisikliginin actigi KILIT UZATMA kapisi da
   kapatildi (kilitliyken yanlis sifre sayaci artirmaz, kilidi uzatmaz). Ayrinti ve pinler:
   **GUVENLIK-FIX-2** bolumu. Asagidaki metin bulgunun kaydidir.
   (GUVENLIK-FIX dalgasinda olculdu) `AuthManager.Login` kilit kontrolunu SIFRE
   DOGRULAMASINDAN ONCE yapiyor: 5 basarisiz denemeden sonra KAYITLI bir adres
   **403 "Cok fazla basarisiz deneme..."**, kayitsiz bir adres **401 "E-posta veya sifre
   hatali."** doner. Yani saldirgan 5 istek harcayarak adresin kayitli olup olmadigini
   ogrenebilir. G2/G2b kayit ve dogrulama uclarindaki kanallari KAPATTI; bu kanal ACIK KALDI.
   **BILEREK DOKUNULMADI - CUNKU KAPATMAK BEDELLI:** kilidi gizlemek, gercek kullaniciya
   "hesabin 15 dakika kilitli" diyememek demektir; kullanici sifresini dogru yazdigi halde
   401 alir ve neden giremedigini anlayamaz. Auth kovasi (10/dk/IP) hizi kisitliyor.
   Aday cozumler: (i) aynen birak (bugunku), (ii) kilit bilgisini de E-POSTAYA tasi ve
   yanitta 401 don (G2 kalibi), (iii) kilidi sifre DOGRUYSA bildir (o zaman kanal kapanir
   ama kilitli hesaba dogru sifreyle gelen kullanici yine bilgilenir). **Karar kullanicinin.**


20. **VARSAYILAN-KAPALI KURAL CONTROLLER'LARLA SINIRLI - MINIMAL-API UCU EKLENIRSE
   VARSAYILAN ACIK OLUR.** (GUVENLIK-FIX / G5'te olculdu, kullanici karariyla deftere alindi)
   `app.MapControllers().RequireAuthorization()` YALNIZ controller uclarini kapsar.
   Istenen `options.FallbackPolicy` idi ve HER endpoint'i kapsardi, ama OLCULDU ki mevcut bir
   pini kiriyor: `X-Api-Version` ayristirilamayinca Asp.Versioning gercek endpoint yerine
   METADATA'SIZ bir HATA endpoint'i koyuyor; FallbackPolicy onu da kapsayinca 400'u yazan kod
   HIC calismiyor ve istek 401'e donusuyor. Bu, SUPHELI #14'u DAHA KOTU yapardi (entegratore
   401 demek onu kimlik hatasi aramaya yonlendirir), bu yuzden kapsam controller'lara
   daraltildi - gerekce `Program.cs`'te.
   **BUGUN BOSLUK YOK** (olculdu: 150 action'in tamami acikca isaretli, uygulamada minimal-API
   ucu ve [Authorize]'siz hub YOK). **RISK GELECEKTE:** ileride eklenecek bir `app.MapGet` /
   `app.MapPost` ucu ya da yeni bir hub, isaretlenmezse VARSAYILAN OLARAK ACIK olur.
   Bu bosluk RUNTIME'da degil TEST'te kapatildi:
   `SecurityHardeningTests.VarsayilanKapali_ACIK_Uclari_KIRMAZ_ve_HER_UC_ACIKCA_ISARETLIDIR`
   her uretim ucunun acikca isaretli oldugunu tarar (oznitelikler YANSIMAYLA okunur;
   `EndpointMetadata` okunsaydi konvansiyonun ekledigi `AuthorizeAttribute` yuzunden tarama
   VAKUM olurdu). Sessiz bir 401 yerine KIRMIZI BIR TEST secildi.
   Aday kalici cozum: Asp.Versioning'in hata endpoint'ine anonim metadata iliskilendirilebilir
   hale gelirse (ya da SUPHELI #14 genel olarak cozulurse) FallbackPolicy'ye gecilebilir.

21. **[KAPANDI - A2-FIX] SIFRE POLITIKASI UC AYRI GIRIS NOKTASINDA UC AYRI - SIFIRLAMA UCUNDA HIC YOK.**
   **KAPANIS:** kural TEK MERKEZE (`Divisima.Core.Security.SifrePolitikasi`) tasindi ve DORT
   giriste de uygulaniyor. Ayrinti: A2-FIX bolumu. Asagidaki metin BULGUNUN kaydidir.
   (LAUNCH-FIX Dalga A / A2'de olculdu; A2 bu akisi ARAYUZE BAGLADIGI icin kapi artik her
   musteriye acik.) Olculen tablo:
   ```
   POST /api/auth/register        CustomerRegisterRequestValidator
                                  -> >=8 karakter + buyuk + kucuk + rakam
   POST /api/account/change-password  AccountManager.cs:73
                                  -> yalnizca >= 6 karakter, KARMASIKLIK YOK
   POST /api/auth/reset-password      AuthManager.ResetPassword
                                  -> HICBIR KONTROL YOK; dto.new_password dogrudan hash'leniyor
                                     (bu DTO icin FluentValidation validator'i da YOK - tarandi)
   ```
   **URETIMDEKI ANLAMI:** "Sifremi unuttum" ile gelen bir kullanici, KAYITTA reddedilecek bir
   sifreyi (ornegin `abc`) belirleyebilir. Yani politika, atlatilmasi en kolay yoldan
   uygulanmiyor. Dalga A'da istemci tarafina kayit kuralinin AYNISI kondu (`sifreSifirlaEkrani`)
   ama bu bir GUVENCE DEGIL - dogrudan uca istek atan biri icin yok hukmunde.
   **DUZELTILMEDI** (ev kurali: supheli uretim davranisi duzeltilmez, pinlenir).
   Bugunku davranis ADIYLA sabitlendi:
   `LaunchFixMailZinciriTests.SUPHELI_SifreSifirlamada_SUNUCU_TARAFI_SIFRE_POLITIKASI_YOK_PINLENIR`
   - pin CIFT-ANLAM KIRICI: ayni zayif sifrenin KAYITTA 400 aldigi da assert ediliyor, yani
   kural VAR, yalnizca bu ucta UYGULANMIYOR.
   Aday cozum: sifre politikasini TEK yerde toplayip (ornegin `SifrePolitikasi.Dogrula`) uc
   giris noktasinin ucunde de cagirmak. `ChangePassword`'un 6 karakterlik esigi de bu kararin
   kapsamina girer - onu 8'e cikarmak MEVCUT kullanicilarin sifre degistirmesini zorlastirir,
   yani bir URUN karari. **Karar kullanicinin.**
   **YAN GOZLEM (kozmetik, ayni metotta):** `ResetPassword`'un basinda AYNI
   `string.IsNullOrWhiteSpace(dto.token)` kontrolu IKI KEZ var (farkli mesajlarla); ikincisi
   ULASILAMAZ. Zarar yok, temizlik kalemi.

### KALICI ONLEM: KANIT MASKESI (Dalga A duzeltmesine bindi)

`secret-scan` kirmizisi UCUNCU KEZ ayni sinifta tekrarlayinca kural **kaynaginda kapatildi** -
ayrinti bolum 1'deki "MASKELEME URETIM NOKTASINDA YAPILIR" maddesinde. Uygulanan yuzey:

| Yer | Ne yapiyor |
|---|---|
| `Divisima.Core/Utilities/Text/KanitMaskesi.cs` | tek olcut, tek uygulama |
| `TestAuthHelper.EnsureAsync` | **paylasilan** yardimci; register/verify/**login** kosuyor |
| 26 test sitesi | assert mesajina ham govde koyan her yer mekanik olarak sarmalandi |
| `NetgsmSmsService` | uretimdeki TEK ham saglayici-govdesi logu |
| SMTP yakalayicisi (scratchpad) | `.eml`'i **yazarken** kirpar |

**OLCULEN YAN ETKI (durust kayit):** olcut, uretilmis test e-postalarinin yerel kismini da
maskeliyor (`maske.17…@example.com`) - cunku onlar da 16+ karakter, rakam ve kucuk harf
iceriyor. Gercek musteri adresleri (`ad.soyad@...`) rakam icermedigi icin DOKUNULMAZ. Bu bir
kayip degil kazanc sayildi: adres kisisel veridir, teshis kanalinda maskeli olmasi dogrudur.

**SINIR (durust kayit):** `/` bilincli olarak jeton karakteri SAYILMIYOR - iceri alindiginda
`.../#/dogrula/<jeton>` tek parca sayilip YOL da yutuluyordu (pin bunu yakaladi). Bedeli:
standart base64 (base64url degil) bir sir `/` karakterlerinde parcalara bolunur; her parca
ayri degerlendirilir ve 16+ karakterli olanlar YINE maskelenir. Olctugumuz jetonlarin
(dogrulama/sifirlama, JWT, Guid) hicbiri `/` icermiyor.

22. **[KAPANDI - GUVENLIK-FIX-4] IDEMPOTENCY FILTRESININ ANAHTARI GOVDEYE BAGLI DEGILDI.**
   KAPANIS: govde SHA-256'si kayda yazildi (farkli govde -> 422, replay YOK, sessiz dusus YOK),
   kimlik cozunurlugu `IdempotencyKimligi.Coz` ile middleware ile TEK KAYNAKTAN birlestirildi,
   replay govdesi HAM BAYT olarak saklanip AYNEN veriliyor (bayt-birebir). Ayrinti ve canli
   olcumler GUVENLIK-FIX-4 bolumunde. Asagidaki metin BULGUNUN kaydidir.
   (GUVENLIK DALGASI 2 / B4'te olculdu.)
   `IdempotencyFilter.cs:57` anahtari su uc parcadan uretiyor:
   ```
   var raw = $"{keyValues}|{context.HttpContext.Request.Path}|{userScope}";
   ```
   **GOVDE HASH'I YOK.** Olculen davranis (gercek uclar, gercek hesap):
   ```
   Idempotency-Key: K  + govde1  -> 201, siparis 177
   Idempotency-Key: K  + GOVDE2  -> 201 replayed=true, BIRINCI istegin yaniti
                                    ikinci siparis HIC OLUSMADI (istek sessizce dustu)
   ```
   Yani anahtari yeniden kullanan bir istemci, GONDERDIGINDEN FARKLI bir seyin sonucunu
   "basarili" olarak alir. Bir ag tekrarinda bu DOGRU davranistir; anahtari yanlislikla
   sabitleyen bir entegrasyonda ise **sessiz veri kaybidir**.
   **IKINCI YUZU - `userScope` DAIMA `"anon"`:** satir 56 `User?.Identity?.Name` okuyor ve
   D4'te DAVRANISLA olculdu ki bu deger her zaman null'dur (token'a `ClaimTypes.Name`
   yazilmiyor; D4'te MIDDLEWARE bu yuzden `ClaimTypes.NameIdentifier`a cevrildi ama FILTRE
   cevrilmedi). Sonuc: filtrenin kapsaminda kullanici ayrimi YOKTUR - ayni anahtari ayni yola
   gonderen IKI FARKLI kullanici ayni kovaya duser.
   **UCUNCU (kozmetik):** replay yaniti **PascalCase** (`Data`/`Success`), orijinal yanit
   **camelCase** - ayni uc iki farkli sozlesme donduruyor.
   **BUGUN SOMURULEMEZ (olculdu):** storefront `Idempotency-Key` basligini **HIC GONDERMIYOR**
   (`frontend/*.js` ve `*.html` tarandi: 0 gecis). Asil mekanizma govdedeki `request_id`dir ve
   o CALISIYOR (istemci uretiyor, `OrderManager` kontrol ediyor). Filtre yalniz DORT para
   ucunda takili: `order/place`, `guest-checkout/place`, `loyalty/redeem`, `giftcard/redeem`.
   Risk GELECEKTEKI API istemcilerinedir (mobil uygulama, pazaryeri entegrasyonu).
   Aday duzeltme: anahtara govde hash'i eklemek + `userScope`u `NameIdentifier`a cevirmek
   (D4'te middleware icin yapilanin aynisi) + replay yanitinin serilestirmesini orijinalle
   hizalamak. **Karar kullanicinin.**

