# 53 · GUVENLIK-AV-3 DAR (SALT OLCUM)

**Zemin:** `533f935` (HEAD=origin/main, agac 0, cift yesil) · **kod/config/docs DEGISMEDI**
**Tur:** salt olcum · 6 on-olcum ajani + 3 MK-4b denetcisi · 9 HAM defter (359 KB) · ~2,8 sa
**Karar:** GO/NO-GO olcutu **(B)** - "davranis kaniti bulunan" on sarti eklendi. **NO-GO 3.**

---

## 1. PILOT

Araclar: kaynak okuma · `sqlcmd` SELECT · canli API (`curl`; **`curl -I` KULLANILMADI**) ·
`sys.indexes`/`sys.columns` · DLL dizge+metadata · SQL plan onbellegi.
Denetciler ayri `git worktree` + ayri `DIVISIMA_TEST_DB` (`DivisimaAv3D1/D2/D3`).
**Tarayici kanali KOSULMADI** (turun kendi kor noktasi).

**RIG (Divisima Eki 2.3 - URUN VARSAYILANI DEGIL):** PID 27776, baslangic 02:50:49.
`--Iyzico:UseRealSdk=false` · `--AdminSeed:Enabled=false` · `--BackgroundJobs:Enabled=false`
· `--RateLimit:AuthPermitLimit=100` · `--MailSettings:Host=` · `ASPNETCORE_ENVIRONMENT=Development`.
Uc dogrulamasi: `/health` -> **200** (POZ) · `/api/ZZZyok` -> **404** (NEG).

---

## 2. EN RISKLI 10

Siralama **kanit gucune** gore (D2 hukmu: davranis kaniti olmayan YUKSEK, canli repro'lu
bulguyla AYNI SIRAYA KONMAZ - SDP 1.11.10).

| # | bulgu | siddet / on kosul / durum | kanal | davranis kaniti |
|---|---|---|---|---|
| 1 | **T1-B1** uye `request_id` replay: dedup yukleminde `customer_id` YOK | `[VERI-BOZAN]`+`[DURUSTLUK]` / KIMLIKLI / AKTIF | 3 | **D1 cift-kor canli**: m201 -> m188'in siparis 293 no'su + `replayed:true`; kendi siparisi HIC OLUSMADI |
| 2 | **T1-B2** uye siparisinde `address_id` kurali yok | `[VERI-BOZAN]` / KIMLIKLI / AKTIF | 3 | **D1**: DB'de **15 adressiz siparis, 14'u Confirmed+COD**; `OrderSnapshot.shipping_address` 175/175 null |
| 3 | **T1-B4** COD para hareketsiz "odenmis" uretiyor | `[PARA]` / KIMLIKLI / AKTIF | 3 | **D1**: `payments` **0/123** · fatura 122 · sadakat 33 satir / 1922 puan ilerliyor |
| 4 | **T4-F2** `customers`/`orders`'ta rowversion YOK | YUKSEK / KIMLIKLI / LATENT | 3 | **D1 kor noktayi kapatti**: plan onbelleginden GERCEK SQL - `UPDATE [customers] SET [address],[created_at],[password_hash],[store_credit]...` |
| 5 | **T4-F1** `Return/create` cift iade; `return_requests`'te UNIQUE yok | YUKSEK / KIMLIKLI+ADMIN / LATENT | 2 | **YOK** - D1 telafiyi olctu: `refunded_amount` CAS'i toplami `total_price` ile sinirliyor |
| 6 | **T1-B5 + T5-1** sahiplik ihlali izsiz (5 uctan 1'i yaziyor) | `[DURUSTLUK]` tespit / **ilgisiz** / AKTIF | 3 | **D1**: 20 istek -> delta **+3**, yalniz `payment/initialize` |
| 7 | **T3-1** `ICaptchaValidator` sifir tuketici; `SECURITY.md` aksini soyluyor | ORTA / KIMLIKSIZ-UZAK / AKTIF | 2 | **D1**: tam 3 gecis = arayuz + uygulama + DI kaydi; tuketici **0** |
| 8 | **T4-F3** denetim izi F-2'nin uretecegi kayba kor | ORTA / ilgisiz / AKTIF | 2 | **D1**: `audit_logs` yalniz `referral_code` gosteriyor, SQL tum satiri yaziyor |
| 9 | **T2-1** `product/import` transaction yok + satir siniri yok + tip kontrolu yok | ORTA / ADMIN / ACIK | 2 | canli ice aktarim **KOSULAMADI** (admin jetonu yok) |
| 10 | **T5-0** rig bayat ikili (`c5460c0` ikilide yok) | **ORTA** (T5 YUKSEK demisti; D3 duzeltti) / ilgisiz | 4 | uc denetci de bagimsiz dogruladi |

---

## 3. BULGU TABLOSU - 37 KALEM (kanal sutunu D2 DUZELTMELI)

> **SAYI DUZELTMESI (`54·ARSIV-4` turunda, MK-11/d ISTISNASI - muhrun KENDI sayi hatasi).**
> Bu baslik ilk yazimda **"32 KALEM"** diyordu; tablo **37 satir** tasiyor.
> **Ureten ifade:** `awk '/^\| id \| siddet/{f=1;next} f&&/^\|---/{next} f&&/^\|/{print $2}
> f&&!/^\|/{exit}' <bu dosya> | tr -d ' ' | sort -u | wc -l` -> **37**
> (aile dagilimi T1 8 · T2 7 · T3 9 · T4 8 · T5 2 · X 3; POZ kontrol `^| T1-B1 |` -> 1,
> NEG kontrol uydurma id -> 0).
> **KOK:** sayi AV-3 raporunda dogdu ("EN RISKLI 10 + kalan 22"; gercek kalan 27) ve
> URETEN IFADESIYLE kaydedilmedigi icin iki durak boyunca yakalanmadi - MK-3'un onlemek
> icin yazildigi sinif. **YAYILIM:** bu baslik + `1d67cf6` commit mesaji; commit mesaji
> GECMISTE KALIR (force-push YASAK). Kayit ayrica bolum 16'da.

**D2 OLCUMU:** ana akisin konsolide tablosunda kanal sutunu 27 satirin **10'unda** HAM'dan
uretilemiyordu; **besi YUKARI** sapmisti (T3-2/3/4/6/7: HAM `1 KANAL -> SUPHE`, tabloda 2)
ve boylece **bes SUPHE kaleme donusturulmustu**. Asagidaki sutun DUZELTILMIS degerlerdir.
`ham/T3.md` birebir: *"KANAL SAYISI: 1 (kaynak; PowerShell olcumu FARKLI RUNTIME oldugu icin
BAGIMSIZ KANAL SAYILMAZ)"*. ORTAK-KURAL: **TEK KANAL = SUPHE, tarife KALEM OLMAZ.**

| id | siddet | on kosul | durum | konum | kanal |
|---|---|---|---|---|---|
| T1-B1 | `[VERI-BOZAN]`+`[DURUSTLUK]` | KIMLIKLI | AKTIF | `OrderManager.PlaceOrder` dedup dali | 3 |
| T1-B2 | `[VERI-BOZAN]` | KIMLIKLI | AKTIF | `OrderCreateRequestValidator` / `address_id` | 3 |
| T1-B3 | `[MANTIK]` | KIMLIKLI | LATENT | `payment_method` 3..255 -> Online(0) | 2 |
| T1-B4 | `[PARA]` | KIMLIKLI | AKTIF | COD dali; fatura + sadakat + stok | 3 |
| T1-B5 | `[DURUSTLUK]` tespit | ilgisiz | AKTIF | 5 sahiplik ucu; `SahiplikIhlaliAsync` 2 cagri yeri | 3 |
| T1-B6 | `[MANTIK]`+`[DURUSTLUK]` | ilgisiz | AKTIF | kargo esigi kod 2000 / vaat 2.500 - **4 metin alani** (2 slug x 2 dil, D2) | 2 |
| T1-B7 | `[MANTIK]` | ilgisiz | LATENT | `replayed` bayragi istemcide olu | 2 |
| T1-B8 | `[UX]`+`[MANTIK]` | ilgisiz | AKTIF | byte tasma mesaji Ingilizce + `dto` | 2 |
| T2-1 | ORTA | ADMIN | ACIK | `product/import` transaction + satir siniri + tip | 2 |
| T2-2 | ORTA | ADMIN | ACIK | CSV `name`/`brand` uzunluk dogrulamasi yok | 2 |
| T2-3 | DUSUK | ADMIN | ACIK | CSV `brand` bos gecebilir | 2 |
| T2-4 | DUSUK-ORTA | ADMIN | ACIK | `errors` listesi sinirsiz | **1 SUPHE** |
| T2-5 | DUSUK | ilgisiz | ACIK | basarisiz satirlarin izi yok | 2 |
| T2-6 | DUSUK | ADMIN | LATENT | CSV formul enjeksiyonu (sink yok) | 2 |
| T2-7 | DUSUK | ADMIN | ACIK | `ParseCsvLine` RFC 4180 uyumsuz | **1 SUPHE** |
| T3-1 | ORTA | KIMLIKSIZ-UZAK | AKTIF | `ICaptchaValidator` tuketici 0 | 2 |
| T3-2 | DUSUK | KIMLIKLI | LATENT | `UrlValidator` IPv6 / DNS / yonlendirme | **1 SUPHE** |
| T3-3 | DUSUK | ilgisiz | LATENT | adli `HttpClient` timeout / govde siniri yok | **1 SUPHE** |
| T3-4 | DUSUK | ilgisiz | LATENT | `einvoice` istemcisi kayitli degil | **1 SUPHE** |
| T3-5 | DUSUK | KIMLIKSIZ-UZAK | AKTIF | sitemap XHTML script dugumu (SD-4 sinir genislemesi) | 2 |
| T3-6 | DUSUK | ADMIN/OPS | ACIK | `Storefront:BaseUrl` acilista dogrulanmiyor | **1 SUPHE** |
| T3-7 | DUSUK | **ilgisiz** (D2 duzeltmesi) | ACIK | `ISecretProvider` / KeyVault tumden olu | **1 SUPHE** |
| T3-8 | BILGI | ADMIN/OPS | LATENT | FCM URL'inde config'ten yol parcasi | 1 |
| T3-9 | DUSUK kapsam notu | ilgisiz | AKTIF | iki CI job `--locked-mode`suz restore | 2 |
| T4-F1 | YUKSEK | KIMLIKLI+ADMIN | LATENT | `return_requests` UNIQUE yok; check-then-act | 2 |
| T4-F2 | YUKSEK | KIMLIKLI | LATENT | `customers`/`orders` rowversion yok | 3 |
| T4-F3 | ORTA | ilgisiz | AKTIF | `AuditInterceptor` `ExecuteUpdateAsync`a kor | 2 |
| T4-F4 | ORTA | ADMIN | LATENT | `ProductAttribute/set`: `attributes:null` -> hepsi silinir, uc **200 doner** | **1 SUPHE** |
| T4-F5 | ORTA | ilgisiz | AKTIF | `OrderStatusMachine` iki elle kopya (`OrderManager` :700, :1088) | **1 SUPHE** |
| T4-F6 | DUSUK | ADMIN | AKTIF | Swagger "kalici siler", kod soft-delete | **1 SUPHE** |
| T4-F7 | DUSUK | KIMLIKLI | AKTIF | `Cart/add` cift-anlamli 400 | 2 |
| T4-F8 | DUSUK | KIMLIKSIZ-UZAK | AKTIF | anonim abonelik yabanci e-postaya | 2 |
| T5-0 | ORTA (olcum butunlugu) | ilgisiz | AKTIF | rig ikilisi `c5460c0` oncesi | 4 |
| T5-2 | DUSUK-ORTA | ilgisiz | AKTIF | logout CAS `audit_logs`a yazmiyor | 2 |
| X-1 | BILINEN sinir genislemesi = **YENI** | ilgisiz | AKTIF | 197 rezervasyonun 186'si suresi dolmus, `available` kalici dusuk | 2 |
| X-2 | ORTA | ilgisiz | AKTIF | `app.MapHub<NotificationHub>` `RequireAuthorization` YOK + 3 `MapHealthChecks` | 2 |
| X-3 | yapisal | ilgisiz | AKTIF | `BILINEN.md` 12 maddeyi DURUMSUZ sunuyor | 2 |

**T4-F4 ve T4-F5 ana akis tarafindan DUSURULMUSTU; D2 geri koydurdu** - dusme olcutu
tutarsizdi (ayni "1 kanal SUPHE" olcutundeki T2-4 / T2-7 / T4-F6 ICERI alinmisti).

### SAHIPSIZ KALEM (hicbir kapsamda degildi)

**T4/S-1** - `IyzicoPaymentManager` :398/:439 durum yazarken ne `IsValidTransition` ne durum
on kosulu tasiyor; gec gelen basarili callback **IPTAL EDILMIS siparisi Confirmed'e DIRILTIR**
(makine bu gecisi YASAKLAR). T-4 bunu "AV-3'un odeme ajanina" havale etti - **oyle bir ajan
YOKTU**. **Merkez karari: T4-F5 ile TEK KOK olarak launch oncesine alindi** (bolum 9).

---

## 4. T-1 FARK TABLOSU (misafir <-> uye)

Kaynaklar: `GuestCheckoutValidator` · `GuestCheckoutManager` · `OrderCreateRequestValidator` ·
`OrderManager.PlaceOrder`. **BES KAPIDA UYE ACIK.**

| kalem | misafir | uye | fark |
|---|---|---|---|
| `request_id` **SAHIPLIK yuklemi** | VAR (`sahip.email == email`, Ordinal) | **YOK** | **UYE ACIK** (T1-B1a) |
| `request_id` **GOVDEYE baglama** (sepet coklu-kumesi + kanonik kupon) | VAR (`AyniSiparisMiAsync`, GF-3/K12) | **YOK** | **UYE ACIK** (T1-B1b) |
| olcut TUTMAYINCA yanit | **400 sizintisiz** (`OrderPlaceFailed`) | **200 + BASKASININ `id`+`order_number`** | **UYE ACIK** |
| teslimat adresi ZORUNLU MU | **EVET** (`city`/`district`/`full_address` NotEmpty+MaxLength, `phone` desen) | **HAYIR** - `address_id` `int?`, validator kurali YOK | **UYE ACIK** (T1-B2) |
| `payment_method` gecersiz deger | **ACIKCA REDDEDILIR** (`!=1` -> 400 `GuestOnlyCashOnDelivery`) | **DOGRULANMIYOR** - 3..255 sessizce Online(0) | **UYE ACIK** (T1-B3) |
| `request_id` uzunluk (<=80) + karakter sinifi | VAR | **VAR** | YOK - GF-5/K4/D2 ikisini birden kapatti |
| ad uzunlugu (100) | VAR | uye KAYIT ucunde VAR | YOK |
| e-posta uzunlugu (200) | VAR (GF-5/F4) | uye KAYIT ucunde | YOK |
| telafi (on-yazilan satirlar) ATOMIK MI | VAR, GF-5/K4 tek transaction | **ILGISIZ** - uye yolunda `PlaceOrder` ONCESI satir yazilmiyor | sinif DOGMUYOR |
| kupon reddi YETIM birakir mi | EVET (GF-5/K4 telafisi kapatti) | **HAYIR** | uye AVANTAJLI |
| rate limit kovasi | `[EnableRateLimiting("auth")]` | **OZNITELIK YOK** -> global **100/dk** | **UYE GEVSEK** |

**KOK TEK:** GF-1 / GF-3 / GF-5'in **misafir yoluna** kazandirdigi kapilarin hicbiri
**uye yoluna** tasinmamis. Bu, GF-6a'nin tek gerekcesidir.

---

## 5. T-2 YUZEY TABLOSU

```
UC 1  POST /api/product-image/upload
  yetki        : [RequireUserType(Admin)] - anonim CANLI olculdu -> 401
  boyut siniri : [RequestSizeLimit(6 MB)] + servis katmani MaxBytes 5 MB
                 (oznitelik global 5 MB'i YUKARI ezer - BILINCLI, yorumu var)
  tip kontrolu : MIME allowlist {image/jpeg,image/png,image/webp} + MAGIC-BYTE imzasi
  depolama     : WebRootPath/uploads/products/<Guid:N><uzanti> - istemci dosya adi ATILIYOR
  servis yolu  : GET /uploads/products/<ad> - app.UseStaticFiles() ANONIM
                 (static middleware auth'tan ONCE kayitli)
  BULGU        : YOK - savunmalar tam ve PINLI

UC 2  POST /api/product/import
  yetki        : [RequireUserType(Admin)] - anonim CANLI olculdu -> 401
  boyut siniri : oznitelik YOK -> global Kestrel 5 MB
  tip kontrolu : YOK (content-type / uzanti / magic-byte HICBIRI); govde duz metin
  depolama     : DISKE YAZILMIYOR - govde bellekte string, dogrudan DB'ye
  servis yolu  : yok
  BULGU        : T2-1 · T2-2 · T2-3 · T2-4 · T2-5 · T2-6 · T2-7
```

**RATE LIMIT:** iki ucta da `[EnableRateLimiting]` **YOK**, ama `options.GlobalLimiter`
**IP basina 100/dk** ikisini de kapsiyor - "limitsiz" DEGIL, kova genis ama VAR.
**SAVUNMA ASIMETRISI:** ayni depo, ayni yetki sinifi, iki uc - biri magic-byte'a kadar
korunmus, digeri hicbir tip kontrolu tasimiyor.

---

## 6. KUMULATIF KAPSAM MATRISI (40 controller x AV-1 / AV-2 / AV-3)

**Denominator bagimsiz uretildi:** `ls Divisima.API/Controllers/*Controller.cs | wc -l` -> **40**
(NEG `*ZZZController.cs` -> 0); `[HttpX]` toplam **151** uc (NEG `[HttpZZZ` -> 0).
`51·AV-2` matrisi D2 tarafindan `comm` ile uc yonde dogrulandi: `av1kor 13` · `av2kor 17` ·
`ikisi 10` · **`AV-1 kor ∩ AV-2 kor = 0`** · kor-30 tam parcalanma.

**"HIC ANILMAYAN = 0" HEDEFI SAGLANIYOR - AMA AV-3'TEN BAGIMSIZ.** `AV-1 kor ∩ AV-2 kor = 0`
oldugu icin bu hedef AV-3 hicbir sey yapmasa da saglanirdi. **Anlamli olcut "AV-3'te de kor".**

### T-4'un beyani DUZELTILDI: 13/17 DEGIL, **18/12**
Ana akis ve D2 **ayri ayri** olctu, ayni sonuc. Kok sebep: T-4'un secim tablosu **21 UC GRUBU**
listeliyor (altisi ayni controller'in ikinci satiri) -> tekil controller **18**; eleme tablosunun
son bes satiri controller DEGIL, **zaten secilmis** controller'larin ELENEN UCLARI -> gercek **12**.
**KAYIT (yeni ders):** `13+17` da `18+12` de **30** verir - **iki farkli bolunme ayni toplami
veriyorsa TOPLAM KONTROLU KANIT DEGILDIR.**

### Kor-30'un durust parcalanisi (kademeli)

```
19  DERINLEMESINE olculdu   T-4'un 18'i + Seo (T-3, canli REPRO + XmlDocument + CSP olcumu)
 4  YALNIZ CANLI YETKI      AuditLog · Dashboard · ProductQuestion · SizeGuide
                            (T-4'un 20 uclu 401/403 matrisinden gectiler)
 5  YALNIZ KAYNAK ELEME     Comparison · Device · Merchandising · RecentlyViewed · Recommendation
 2  ILAN EDILMIS KAPSAM DISI  Seller · SellerAuth (00a:92 / 00a:101, DOKUNULMAZ - kor DEGIL)
--
30  TOPLAM   (NEG kontrol: olculen kumede kor-30 DISI ad -> 0; kesisim -> BOS)
```

**X-kapsam KOSULLU ongoruyordu:** *"T-4 devreye girmezse 29 controller ucuncu turda da kor
kalir"*. T-4 devreye girdi. **KAPSAM GERCEKTEN GENISLEDI, YER DEGISTIRMEDI** - SDP 1.12.10'un
ucuncu kez dusmesi beklenirken **DUSMEDI**. Turun en olumlu tek olcusu.

### Kor eksen ARTIK CONTROLLER'DA DEGIL, OWASP'TA
**A02 · A03 · A05 bu turda kor.** A03'u AV-2 adiyla `BOSLUK (istemci)` diye kaydetmisti;
AV-3 onu **hem tarife almadi hem `frontend/*` DOKUNULMAZ yuzeyinde birakti**. **A04 kusuru
IKINCI KEZ** (`51·AV-2`:236 kayitli): bes T'nin hicbirinde tasarim / is-mantigi ekseni YOK;
besi de UC ya da DOSYA ekseni. **A08** yalnizca A06'nin tedarik-zinciri yarisiyla dolayli.

### Matrisin kendi kor noktasi -> X-2
`app.MapControllers().RequireAuthorization()` **yalniz controller'lari** kapsiyor.
`Program.cs`: `app.MapHub<NotificationHub>("/hubs/notification")` **`RequireAuthorization()`
TASIMIYOR** + uc `MapHealthChecks`. **Bu 4 uc, uc AV turunun HICBIR matrisinde sayilmadi.**
`00b:20` ACIK kaleminin somut sayisidir; `NotificationHub` ayrica B9'daki "SignalR `admins`
alarmi BOS GRUBA yayin yapiyor" BILINEN'inin evidir.

---

## 7. BILINEN (YENIDEN ACILMAZ) - DURUM KAYDI

BILINEN'i **yeni bulgu diye sunan: 0**. Alti defterin altisi da "BILINEN - capraz dogrulama"
etiketini dogru kullandi ve SINIRINI GENISLETEN kismi ayirdi.

**TERS YON - YUTULMUS SINIR GENISLEMESI (1):** `X-1`. "BackgroundJobs kapali" BILINEN'dir;
**"197 rezervasyonun 186'si suresi dolmus ama duruyor, `available` KALICI dusuk, sonraki her
ajanin stok olcumu kirleniyor" DEGILDIR.** ORTAK-KURAL geregi **YENI BULGU**.

**IKI KALEM YANLIS "ACIK" SANILDI (T-4), IKISI DE KAPALI:**

| kalem | T-4 iddiasi | OLCULEN DURUM | olcen |
|---|---|---|---|
| `00b:3` `LocalImageStorage` CWD'ye yaziyor | "BILINEN, acik" | **KAPALI** - `PhysicalRoot => _env.WebRootPath`; `GetCurrentDirectory` tek gecis ve o da **YORUM ICINDE** | T-2 · X · **D2 bagimsiz** |
| `00b:13` uygulama kulturu pinlenmemis | "BILINEN, acik" | **KAPALI** - `Program.cs` `new CultureInfo("tr-TR")` + `DefaultThreadCurrentCulture/UICulture` (NEG `ZZZCulture` -> 0) | X · **D2 bagimsiz** |

Zarari bu turda **0** (yanlis NEGATIF yonunde), **ama mekanizma tam da X-3'un uyardigi
mekanizmadir** ve bu turda **CANLI GERCEKLESTI**: durumsuz bir BILINEN listesi, kapanmis bir
kalemi acik gosterir; regrese olsaydi "bilinen, sayilmaz" diye tek satira inip **RAPOR
EDILMEZDI**. -> **SDP 1.12.8 eki** (skill `sdp` v1.4 -> v1.5, bu turda uygulandi):
*"BILINEN listesi DURUM sutunuyla (ACIK / KAPALI / BAGLAYICI) kurulur; kapali kalem yeni
bulguyu bastirmaz."*

---

## 8. GO / NO-GO (olcut `51·AV-2`)

**OLCUT:** `KRITIK` **∨** `YUKSEK`+`KIMLIKSIZ-UZAK` **∨** `[PARA]`/`[VERI-BOZAN]` - **UC AYRI
DISJUNKT**. Siddet ON KOSULDAN bagimsiz verilemez; `ADMIN` on kosullu kalem KRITIK OLAMAZ.

**ANA AKIS OLCUTU TUTARSIZ UYGULADI (D2 yakaladi).** T1-B1 `KIMLIKLI`dir ve bloker yapilmistir -
dogrudur, cunku **ucuncu** disjunkt (`[VERI-BOZAN]`) atesler ve on kosul o disjunktta ROL
OYNAMAZ. Ama T4-F1/F2 **ayni ucuncu disjunktu tasidigi halde** (T-4'un KENDI etiketleriyle:
`Return POST create | [PARA]`, `STOK SISMESI [VERI-BOZAN]`, `Referral GET my-code | [PARA]`)
**ikinci** disjunktun on kosuluyla elendi. Ayni tabloda iki farkli kural.

**MERKEZ KARARI: (B).** Olcute **"davranis kaniti bulunan"** on sarti eklenir (SDP 1.11.10 ruhu).

| | kalem | gerekce |
|---|---|---|
| **NO-GO 1** | **T1-B1** | `[VERI-BOZAN]`+`[DURUSTLUK]`; D1 cift-kor CANLI repro |
| **NO-GO 2** | **T1-B2** | `[VERI-BOZAN]`; DB'de 15 adressiz siparis (14 Confirmed+COD) |
| **NO-GO 3** | **T1-B4** | `[PARA]`; `payments` 0/123, fatura+sadakat ilerliyor |
| **ADAY** | **T4-F1** | UNIQUE eksik (**migration**) - davranis kaniti YOK, hic kosulamadi |
| **ADAY** | **T4-F2** | rowversion eksik (**migration**) - REPRO-R1 **7 turda hit ALMADI** |

**ADAY KUTUSU ISLEYISI (merkez):** GF-6'da **kirmizi-once denemesi** yapilir; **hit yoksa
launch SONRASI GF-7'ye** duser. Migration gerektirdikleri icin ikisi de ayri karar ister.

**LAUNCH ONCESINE EK (merkez, AV-3 raporunda ayri satirlardaydi):**
**T4/S-1 + T4-F5 TEK KOK** = *"durum yazimi `IsValidTransition`'dan gecmiyor"*. S-1 sahipsizdi,
F-5 ana akis tarafindan dusurulmustu; ikisi de geri alindi. **X-2** (hub `RequireAuthorization`)
launch oncesi GF-6'ya - **tek satir + pin**.

---

## 9. GF-6 / GF-7 BOLUMLEMESI (merkez karari)

**GF-6 - LAUNCH ONCESI**

| kalem | icerik |
|---|---|
| **6a UYE YOLU BUTUNLUGU** | T1-B1 (dedup yuklemine `customer_id`) · T1-B2 (`address_id` kurali) · T1-B3 (`payment_method` dogrulamasi) · T1-B4 (COD). **TEK KOK** - bolum 4'un son cumlesi |
| **6b DURUM MAKINESI** | **T4/S-1 + T4-F5** - `IsValidTransition` TEK KAPI; iki elle kopya (`OrderManager` :700/:1088) ve iki kapisiz yazim (`IyzicoPaymentManager` :398/:439) ayni koke baglanir |
| **6c HUB** | **X-2** - `MapHub<NotificationHub>` `RequireAuthorization()` (tek satir + pin) |
| **6d ICE AKTARIM** | **T2-1** - transaction + satir siniri + tip kontrolu |
| **6e ADAY DENEMESI** | T4-F1 / T4-F2 **kirmizi-once**; hit yoksa GF-7 |

**GF-7 - LAUNCH SONRASI:** 6b kalani (T4-F3 denetim korlugu, S-4) · 6c kalani (T1-B5/T5-1
bes uc, T5-2 logout, yerlesik limiter 429 izsizligi, muhur 52 kapsam beyani duzeltmesi) ·
6d kalani (T2-2..T2-7) · **6e OLU/YANILTICI YUZEY** (T3-1 captcha · T3-4 einvoice ·
T3-6 BaseUrl · T3-7 Vault · T4-F4 · T4-F6 · T1-B6 dort metin alani).

**GF-6 BU TURDA BASLATILMAZ. Sirada ARSIV-4 (tarif merkezden).**

---

## 10. UC DENETCI (MK-4b: ayri worktree + ayri test DB + ayri scratchpad)

**D1 - CIFT-KOR KANIT DENETCISI (L3), 59.734 bayt.** Ajanlarin sonuclarini **GORMEDEN**, on
notr soruyu kendi plani ve komutlariyla yanitladi. Uc kazanim: **T1-B1'i canli repro etti**
(m201 -> m188'in siparis numarasi) · **T1-B2'ye DB kaniti ekledi** (15 adressiz siparis,
`OrderSnapshot.shipping_address` 175/175 null, `ShipmentManager` adresi hic okumuyor) ·
**T4-F2'nin ADIYLA YAZILMIS kor noktasini kapatti** (*"uretilen UPDATE ifadesinin SET listesi
GOZLENMEDI"*) - SQL plan onbelleginden gercek SET listesini cekti. Ayrica bir dedektorunu
duzeltti: `LIKE '%UPDATE [customers]%'` **oludur** (`[...]` karakter sinifi) -> `CHARINDEX`.

**D2 - RAPOR DENETCISI, 54.368 bayt.** Alti eksende de **ITIRAZ**. Uc deger-duzeyi uydurma ·
kanal sutununda 10 sapma (5'i yukari) · iki bulgu dusurulmus · X-kapsam'in tamami dusmus
(kesisim 0) · T-4'un 13/17 sayimi yanlis · **launch bloker gerekcesi yanlis**. Kendi turunda
DB'ye ve canli API'ye **bilincli olarak hic dokunmadi** (paylasilan DB'yi daha fazla
kirletmemek icin) ve bunu kor noktasi olarak yazdi. Ters yonde is: **T1'in kor noktasini
kapatti** - kargo esigi sapmasi `body_en`de de var, **dort metin alani**.

**D3 - KURAL-UYUM DENETCISI, 44.487 bayt.** M1/M2/M4/M10 **ONAY**, alti madde **ITIRAZ**;
**ihlallerin cogunlugu ANA AKISIN**. Kod degismedi (porcelain 0; `wwwroot/uploads` REF sonrasi
**0** yeni dosya) · uretim imzasi **14/14** · **MK-3 uclusu birebir** · **Omer'in hesabi icerik
duzeyinde dokunulmamis** (son siparis ve oturum **9 gun once**, `updated_at` NULL) · arac
yasaklari temiz (`curl -I` gecen 10 satirin 10'u "KULLANILMADI" beyani; `dotnet build` kosan
YOK) · MK-5/MK-4a alti defterde de tam. **Ajan ihlali tek:** T-5 sentetik bir kurgu sifre
literalini deftere yazdi (**12 karakter** - `KanitMaskesi`'nin `>=16` olcutunun **ALTINDA**,
yani "maske nasilsa yakalar" varsayimi burada da YANLIS olurdu). **T-1 · T-2 · T-3 · X-kapsam:
ihlal YOK.** D3 kendi hatasini da yazdi: **T-4'u denetlerken AYNI TUZAGA dustu** (POZ kontrol
icin jeton bicimli iki dosya yazip sildi).

---

## 11. T5-0 - RIG BAYAT IKILI (ORTA, IKI KALEM)

**BULGU:** kosan API ikilisi HEAD'in kaynagi DEGIL. DLL'ler **02:50:33-37**'de derlendi, surec
**02:50:49**'da basladi; `c5460c0` ise **03:58:58**. O anda HEAD `0a341e6` (02:38) idi.

**DORT KANAL (uc denetci de bagimsiz uretti):**
1. **mtime** - `Divisima.API.dll` 02:50:37 · `Bussiness.dll` 02:50:36 · `Core.dll` 02:50:33
2. **ikilide dizge** - `"E-posta en fazla"` kaynakta 2 dosya, uc DLL'de **0**; POZ capa
   `"Ad soyad en fazla"` -> **1**; NEG `"ZZZYokBoyleBirDizge"` -> 0.
   **YONTEM POZ KANITI (D3):** ayni UTF-16 sokumu ayni DLL'de bes baska `E-posta…` dizgesi
   buluyor -> `0` sonucu **YOKLUK KANITI**, yontem kusuru DEGIL.
3. **metadata** - `DetayEnUzun` / `izDetay` ikilide 0, kaynakta 2; `GirdiSinirlari` (onceki
   commit'ten) `Core.dll`de **1**
4. **canli ayirt edici deney** - 412 karakterlik `guest_email` GECIYOR; uye `register` 201
   karakterle **HTTP 500**

**PATLAMA YARICAPI OLCULDU, TAHMIN EDILMEDI.** Kacirilan dort commit'ten **yalniz `c5460c0`**
uretim kodu; onun **15 uretim dosyasinin 11'i KOD SATIRI 0** (saf yorum - D3 kendi yorum
suzgeciyle T-5'in bolunmesini BIREBIR dogruladi). Gercek delta iki kalem:
`SecurityEventManager` **KOD 8** (F1 - `detail` kirpma) · `CustomerRegisterRequestValidator` 4
+ `GuestCheckoutValidator` 4 + `GirdiSinirlari` 1 (F4 - e-posta 200 kapisi).

**GECERSIZ KILINAN CANLI OLCUMLER - TAM LISTE:**

| kalem | gecersizlik |
|---|---|
| **F1** (`detail` kirpma) | **HICBIRI.** D3 ucuncu kanalla olctu: `detail` kolonu 1000 karakter, canli `MAX(LEN(detail))` tum tabloda **94**, kirpma isareti 0 (POZ kontrol: `detail` dolu satir 83). **Tetikleyici kosul HIC OLUSMADI -> F1 LATENT.** |
| **F4** (e-posta 200 kapisi) | **YALNIZ T-5'in iki reprosu** (201 karakterlik `register` -> rig 500 / HEAD'de 400 olurdu; 412 karakterlik `guest_email`). **IKISI DE T-5 TARAFINDAN BEYAN EDILMIS.** |
| diger tum ajanlar | **ETKILENMIYOR.** T1'in 18 canli olcumu `GuestCheckoutManager` (KOD 0) · T4'un 21'i `AuditInterceptor` (KOD 0) + `IyzicoPaymentManager` (KOD 0) · digerlerinin kurgu e-postalari ~22 karakter, F4 esigi 200. SD-7, ip/ua ve T1'in `request_id` olcumu **GECERLI** (`git log -S` + `c5460c0` dosya listesiyle gosterildi). |

**SIDDET DUZELTMESI:** T-5 `YUKSEK / butun AV-3 ajanlarinin olcumunu kirletir` yazmisti;
ana akis bunu oldugu gibi aktardi. **D3 itirazi kabul: ORTA**, kapsam cumlesi olculmus
kapsamla degistirildi. **Rig'in HEAD'e cekilmesi MERKEZIN karari** (bu turda YAPILMADI -
ORTAK-KURAL yeniden derlemeyi zaten yasakliyordu, ve `bin/obj` REF sonrasi **0** yazim aldi).

---

## 12. ORTAK-KURAL CELISKISI (ana akis kusuru, kayit)

Turun ortak kural metni **kendi icinde celisiyordu**:
- *"BU TUR SALT OLCUMDUR"* bolumu: **"Salt okuma serbest: `dotnet build`/`test`/`list package`,
  `msbuild -getProperty`"** -> SERBEST
- *"RIG"* bolumu: **"`dotnet build` YOK (kosan surec DLL kilitler ve diger ajanlarin olcumunu
  kirletir)"** -> YASAK

**X-kapsam bu belirsizligi FARK ETTI ve kendi kor noktasi olarak yazmak zorunda kaldi.**
Kural SIMETRISI saglandi (herkes ayni metni aldi) ama kural METNI TUTARLI DEGILDI.
Fiili sonuc zarara donusmedi - **`dotnet build` kosan ajan YOK** (D3 olcumu, baglam acilarak) -
ama bir ajanin olcum yapmayi birakip **kural yorumlamasi**, tarif kusurudur.

---

## 13. KURGU KAYIT ENVANTERI

**AV-3 (SALT OLCUM turu) 14 KURGU KAYDI URETTI, hepsi URETIM YOLUNDAN** (register -> verify ->
login). Elle `INSERT` **YOK**, sema degisikligi **YOK**.

```
MAX'lar kapanista (2026-09-05 06:2x):
  musteri 202 · urun 955 · siparis 295 · adres 131 · fatura 128
  COUNT(*) user_sessions 420 · COUNT(*) security_events 83

MK-3 UCLUSU - UCU DE BIREBIR (ureten ifadeleriyle):
  SELECT COUNT(*),MIN(id),MAX(id),SUM(CAST(id AS bigint))
    FROM orders WHERE status=0 AND id<=210;              -> 35 / 9 / 210 / 3837
  SELECT COUNT(*),MAX(id) FROM orders WHERE customer_id=10;  -> 38 / 211
  SELECT COUNT(*),SUM(total_price),STRING_AGG(CAST(status AS varchar),',')
    FROM orders WHERE customer_id=74 AND id BETWEEN 234 AND 237;  -> 4 / 4698,60 / 0,0,1,1
  HICBIR AJAN Pending URETMEDI.
```

**URETIM IMZASI (SDP 1.11.10-a): 14/14** - `DATALENGTH(password_hash)` **69** /
`DATALENGTH(password_salt)` **16** (GF-1/K6 v2 zarfi). Imzasiz **0**.

| id | e-posta | ureten |
|---|---|---|
| 188, 189 | `av3.t1.1@` `av3.t1.2@` | T-1 |
| 190, 191, 194-199 | `av3.t4.1@` .. `av3.t4.8@` | T-4 |
| 192 | **`aaaa…@example.com`** - **DESEN IHLALI**, T-5 sinir reprosu (**kendi beyan etti**) | T-5 |
| 200 | `av3.t5.1@` | T-5 |
| 201, 202 | `av3.d1.1@` `av3.d1.2@` | **D1 (denetci)** |

**185 / 186 / 187 / 193 SAF KIMLIK BOSLUGUDUR** - alti FK tablosunda (`addresses`, `orders`,
`consent_records`, `user_sessions`, `carts`, `security_events`) yetim **0**; depo geneli
yetim adres 0 / yetim siparis 0. **Omer'in hesabi (musteri 10) KULLANILMADI** - icerik
duzeyinde dogrulandi (bolum 10, D3/M4).

### D-YAN'A DEVREDILEN (temizlik karari merkezin)

```
DY-A  musteri 192 - DESEN DISI e-posta (100 x 'a'), T-5 sinir reprosu.
DY-B  musteri 201/202 - DENETCI (D1) yazimi; KONSOLIDE 05:47'de kapandiktan SONRA (05:57) dogdu.
      SONUC: kurgu envanteri ARTIK ALTI DEFTERDEN TURETILEMIYOR.
DY-C  32 isimsiz security_events (user_agent curl/8.12.1) - ajan bazinda AYRISTIRILAMADI.
      Tur dagilimi: LoginFailed 29 · IdorAttempt 5 · Logout 1 · PaymentSignatureInvalid 1 ·
      RefreshTokenReuse 1  (T-5'in 5'i itemize; kalani atfedilemedi)
DY-D  outbox_messages 41 + audit_logs 333 - hicbir defterde YOK; AV-3 payi AYRILAMADI
      (D3 kor noktasi: supurme GETDATE() tabanli, GF-5'in 02:36-04:21 calismasini da kapsar)
DY-E  191, 194-199'da failed_login_attempts=4 (T-4 REPRO-R1 izi). Kilit esigi 5 - kilitlenen YOK.
```

**SUIT TABANI DEGISMEDI** (salt olcum turu): `Category=Sql` **382/382** · tam **777/780**
(uc kirmizi = bilinen Docker uclusu, yerelde Docker YOK).

---

## 14. CC HATALARI (ANA AKIS - 11)

1. **UC DEGER-DUZEYI UYDURMA** konsolide tabloda (D2 buldu): *"aile taramasi **6 kolon** temiz"*
   (gercek 9 satir; **acik olan `guest_email` ozetten DUSURULMUS** = yesile boyama) ·
   *"GF-3/K10 **uc iddia da canli**"* (birincisi **kaynak** teyidi) · *"A06 **5/5 YESIL**"*
   (T-3 kendi OLCEMEDIM listesinde: *"kapi ayirt-etme kaniti - once exit 1 / sonra exit 0 -
   BU TURDA ALINMADI"*; CLAUDE.md: **"yesil verdi tek basina kanit degildir"**).
2. **KANAL SUTUNU 10 SATIRDA HAM'DAN URETILEMIYORDU**, besi **YUKARI** -> bes SUPHE kaleme
   donusturulmustu. `TEK KANAL = SUPHE` kuralinin dogrudan ihlali.
3. **LAUNCH BLOKER GEREKCESI AYNI TABLODA IKI FARKLI KURAL** (bolum 8).
4. **T4-F4 / T4-F5 DUSURULDU**, ayni "1 kanal SUPHE" olcutundeki uc kalem ICERI ALINDI;
   **X-kapsam'in TEK SATIRI BILE girmemisti** (kesisim 0, 35.668 bayt defter).
5. **`/tmp` ENVANTER KAPISI KUSURLU KURULDU.** ONCE tabanini ureten ifade SONRA ile AYNI
   DEGILDI; taban o anda var olanin **~%13,7'sini** yakalamis (658 / ~4812) -> **kapi
   yapisal olarak AYIRT EDEMEZ**. Telafi `-type f` oldugu icin **79 yeni DIZINI olcmedi**
   (D3 taradi: temiz) ve `.tmp 4` sayimi **pencere adlandirilmadan** yapildi (turun kendi
   penceresinde **2**). Ayrica **harness'in kendisi** `tasks/*.output`u `%TEMP%` altina,
   **scratchpad DISINA** yaziyor - kural ajan disiplinine INDIRGENEMEZ.
6. **MK-4b ON-OLCUM FAZINDA UYGULANMADI.** Alti ajan da ANA CALISMA AGACINDA kostu; worktree
   yalniz uc denetciye verildi. Paylasilan **CANLI DB** olcum kirletti - somut zarar:
   T-3 musteri 192'yi yanlislikla T-4'e atfetti (uretici T-5) · T-5 kirlenmis bir cikarimi
   geri aldi · **D1, D3'un turunun ORTASINDA** musteri 201/202'yi yaratti ve
   `security_events` MAX'ini D3'un iki sorgusu arasinda 80 -> 83 ilerletti.
7. **ORTAK-KURAL KENDI ICINDE CELISKILI YAZILDI** (bolum 12).
8. **T5-0 YANLIS SIDDETLE AKTARILDI** - "YUKSEK / turun tum canli olcumlerini vuruyor";
   dogrusu **ORTA / iki kalem** (bolum 11).
9. **KAPSAM SAYISI TEK SAYIYLA VERILDI** ("5 kor"); **kademeli bicim** gerekiyordu
   (19 / 4 / 5 / 2). D2 ayni olguyu katı okumayla 11 (net 9) saydi - **ikisi de dogru,
   tek sayi ikisini de gizliyor**.
10. **KAPSAM MATRISI KONSOLIDE'YE HIC GIRMEMISTI** - SDP 1.12.10 muhre **KUMULATIF matris**
    istiyor; matris ancak bu muhurde yazildi.
11. **T1-B5 ve T5-1 IKI AYRI SATIR OLARAK SAYILDI** - **AYNI OLGU** (D2 olctu: 4 `OrderManager`
    + 1 `OrderStatusHistoryManager` = 5 uc; iki ajan birebir dogru). SDP 1.11.7 kok
    birlestirme ihlali; birlesik kanit gucu gorunmez kalmisti.

---

## 15. MERKEZ HATALARI (kayit)

1. **T-4 BEKLENTI LISTESI: `Refund` ve `Wallet`.** Tarif bu ikisini kor-30 icinde sayiyordu;
   **ikisi de CONTROLLER DEGIL** (depoda 40 controller var, ikisi listede yok). Refund
   `IRefundService` uzerinden Order/Payment uclarindan gecer. **Kaynaktan olculdu**, tarif
   duzeltildi - beklenen liste VARSAYILMADI.
2. **B-21 KAYDI BULUNAMADI.** T-2'nin tarif maddesi 7 `B-21` capraz dogrulamasi istiyordu;
   ajan kaydi **hicbir yerde bulamadi** ve madde **CEVAPSIZ** kaldi.
3. **"HIC ANILMAYAN 30" CERCEVESI.** Tarif kor-30'u "hic anilmayan" gibi cerceveledi; gercekte
   `AV-1 kor ∩ AV-2 kor = 0` oldugu icin **"hic anilmayan" zaten 0'di** ve hedef AV-3'ten
   BAGIMSIZ saglaniyordu. Anlamli olcut **"AV-3'te de kor"**tur (bolum 6).
4. **"48 TUR" ATFI KARISIKLIGI.** X-kapsam olctu: atif `docs/muhur/45-guvenlik-fix-1b.md`e
   gidiyor = **GF-1b refresh rotasyonu**, T-5'in andigi **GF-2b/K1 DEGIL**. Karistirilirsa
   **YANLIS REGRESYON kosulur**.
5. **MUHUR 52'NIN "Order+Payment" BEYANI YANLIS.** `52·GF-5` OLAY YUZEYI satiri
   *"sahiplik ihlali `IdorAttempt` kapsam Order+Payment"* diyor. **Olculen gercek:**
   `SahiplikIhlaliAsync` **IKI** cagri yeri - `IyzicoPaymentManager` (`"order"`) ve
   `OrderManager` (**`"address"`**). Uc ajan ayri ayri olctu (T-1, T-5, D1) ve D2 kaynaktan
   dogruladi. **CLAUDE.md B8'de bu satir DUZELTILDI** (yerine yazildi, eklenmedi).

---

## 16. KAPANIS KAPILARI

```
HEAD 533f935 · dal main · git status --porcelain 0 (bas ve son)
wwwroot/uploads BUGUN olusan dosya 0     (POZ kontrol -newermt 2026-08-01 -> 30)
scratchpad/av3 agacinda jeton eslemesi 0 (POZ girdi 1 / NEG girdi 0)
/tmp/.ses YOK - ISTISNA kaydi: uretici OLCULMEDI, icerik OKUNMADI
uc worktree SOKULDU (uc denetci de TAMAMLANMA sinyali verdi; MK-4b EK 46)
alti on-olcum defteri + uc denetci defteri = 9 HAM dosya, hicbiri 0 bayt (MK-5)
alti defterin altisi da MK-4a beyanini basinda tasiyor, altisi da SHA 533f935
```

**BU TUR SALT OLCUMDUR.** Kod / config / uretim davranisi **DEGISTIRILMEDI**; bu muhur ve
CLAUDE.md deltasi **docs-only** tek commit'tir. Duzeltme dalgasi **GF-6**'dir ve
**BU TURDA BASLATILMAZ**.

### 16.1 SONRADAN DUZELTME (`54·ARSIV-4`, MK-11/d ISTISNASI)

Bu muhur `1d67cf6` ile push edildikten SONRA **kendi sayi hatasi** bulundu ve `54·ARSIV-4`
commit'inde duzeltildi: **bolum 3 basligi "32 KALEM" -> "37 KALEM"** (ureten ifade ve
POZ/NEG kontrolu orada). **Baska hicbir satiri degismedi.**
**MK-11/d "arsiv BAYT-SABITTIR" kurali neden delindi:** kural, muhurun ICERIGINI sonradan
gelen bilgiyle degistirmeyi yasaklar; burada degisen sey muhurun KENDI tablosunu YANLIS
SAYAN bir basliktir - yani duzeltme, muhuru kendi kanitiyla TUTARLI hale getirir.
Istisna MERKEZ TARAFINDAN VERILDI ve kapsami TEK SATIRDIR.
