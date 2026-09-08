# 58 · LAUNCH-FIX-5 (LF-5) — E-POSTA DOĞRULAMA = 6 HANELİ KOD

**Zemin:** `36947c3` (LF-4) · **Kapsam:** `AuthManager` register/verify/resend · e-posta şablonu ·
`api-bridge`/`api-client` doğrulama kutusu · testler. **Şifre sıfırlama akışına DOKUNULMADI**
(bağlantı orada kalır — ayrı tehdit modeli: uzun, tek kullanımlık jeton).

---

## 1. NE DEĞİŞTİ

| | Önce | Sonra |
|---|---|---|
| Doğrulama sırrı | 43 karakter rastgele jeton | **6 haneli sayısal kod** (`RandomNumberGenerator`) |
| DB'de saklanan | jetonun **DÜZ HALİ** | **HMAC-SHA256 özeti** (64 hane hex) |
| Ömür | sınırsız | **10 dakika** |
| Deneme sınırı | **yok** | **5** (aşınca kod geçersiz, yeni kod gerekir) |
| "Tekrar gönder" | sınırsız | **60 sn soğuma** |
| E-posta | bağlantı + kod | **yalnız kod, bağlantı YOK** |
| Uç imzası | `verify-email?token=` | **`verify-email?email=&code=`** |

---

## 2. TARİFTEN SAPAN ÜÇ KARAR (ölçülmüş gerekçe)

### 2.1 ÖZET HMAC OLDU — DÜZ SHA-256 DEĞİL *(merkez onayı alındı)*

Altı haneli kodun **tüm uzayı 1.000.000**'dur. Düz `SHA-256(kod)` saklansaydı, DB sızan bir
saldırgan bir milyon özeti sıradan bir makinede **saniyeden kısa sürede** üretip eşleştirirdi —
yani özet, kodu **pratikte korumazdı**. Bu, eski 43 karakterlik jeton için geçerli değildi;
**kısa koda geçmek, özetleme ölçütünü değiştirmeyi zorunlu kılar.**

Biber `Encryption:Key`ten türetilir, veritabanında **durmaz** ve bağlam ayrıştırılır
(`DIVISIMA_EMAIL_VERIFY_v1`) — bir bağlamın özeti başka bağlamda yeniden kullanılamaz.

> **Kalan sınır (dürüst kayıt):** anahtar **ve** veritabanı **birlikte** sızarsa koruma biter.
> O noktada alan şifrelemesi de düşmüş olur; kabul edilmiş sınırdır.

### 2.2 UÇ İMZASI DEĞİŞTİ — GLOBAL ARAMA KALDIRILDI

Eski uç yalnız jetonu alıp `GetAsync(c => c.token == token)` ile **tüm müşteriler** içinde
arıyordu. 43 karakterlik jetonda bu güvenliydi. **6 hanelik kodda aynı desen tehlikelidir:**
saldırgan tek bir kod dener ve o an geçerli kodu **o kod olan herhangi bir hesaba** çarpar —
N kullanıcılı sistemde başarı olasılığı N katına çıkar ("kim denk gelirse" saldırısı).
E-posta istenerek arama **tek hesaba** daraltıldı ve deneme sayacı o hesaba bağlandı.

**Sızıntı sınırı korundu:** kayıtsız adres de var olan adresin yanlış kodu da **aynı 400**'ü
alır; kayıtsız adreste bile **sayaç artar** (artmasaydı "sayaç arttı mı" sorusu adresin
kayıtlı olduğunu ele veren bir yan kanal olurdu).

### 2.3 SOĞUMA **429 DEĞİL 200** — TARİFİN KABUL ÖLÇÜTÜNDEN BİLİNÇLİ SAPMA

Tarif, kabul ölçütleri arasında **"soğuma 429"** diyordu. **429 DÖNÜLMEDİ.**

Gerekçe: `resend-verification` ucunun **tüm varlık nedeni** (GÜVENLİK-FIX G2b) adresin kayıtlı
olup olmadığını sızdırmamaktır — uç bu yüzden **her durumda aynı 200**'ü döner. Soğumada 429
dönmek tam o sızıntıyı **geri açardı**: saldırgan bir adrese arka arkaya iki istek atar, 429
alırsa **"bu adres KAYITLI"** bilgisini okur; kayıtsız adreste soğuma diye bir şey olmadığı
için 200 alırdı. Yani tarifin istediği kabul ölçütü, tarifin **kendi** D1 ölçütüyle
(*"adres var/yok sızdırmaz"*) **çelişiyordu**; çelişkiyi sızıntı lehine çözmek yanlış olurdu.

**Soğuma istemcide görünür** (60 sn geri sayım), **sunucuda sessizdir.**

**PİN BU YÜZDEN DURUM KODUNU DEĞİL YAN ETKİYİ ÖLÇER** (`SOGUMA_YENI_KOD_URETMEZ_ama_YANIT_AYNI_200`):
yalnız "200 döndü" demek **çift anlamlı** olurdu — soğuma çalışsa da çalışmasa da 200 gelir.
Pin, ikinci isteğin **yeni kod üretmediğini** ve **gönderim zamanını ilerletmediğini** ölçer
(ikincisi ayrı bir tuzak: zaman her istekte ilerleseydi pencere hiç dolmazdı). **MUT-19**
(soğuma dalı devre dışı) → **1 isimli kırmızı**.

> **DÜRÜST KAYIT:** bu pin de dalga içi denetimde eklendi — soğuma, tarifin kabul listesinde
> olmasına rağmen ilk turda **hiç pinlenmemişti**. Sapmanın kendisi savunulabilir; sapmanın
> **pinsiz ve raporsuz** kalması olmazdı.

---

## 3. SAYAÇ REDİS'TE — MİGRATION YOK *(merkez kararı)*

`Customer`'da doğrulama denemesi için kolon **yoktu**. `failed_login_attempts` **kullanılamazdı**:
o giriş kilidi sayacıdır; yanlış kod girmek kullanıcının **girişini kilitlerdi**.

Sayaç dağıtık önbellekte; **TTL = kodun ömrü** (10 dk), yani sayaç kodla birlikte doğar ve ölür.

> **BİLİNEN (kabul edilmiş):** Redis yeniden başlarsa sayaç sıfırlanır. Saldırgan bunu
> tetikleyemez; ayrıca auth hız limiti (10/dk/IP) bağımsız bir üst sınır koyar.

**`ICacheService`e atomik `IncrementAsync` eklendi.** Gerekçesi ölçülmüş: `GetAsync`+`SetAsync`
ile oku-değiştir-yaz yapılsaydı iki istek aynı anda 0 okur, ikisi de 1 yazar ve **sayaç
ilerlemezdi** — saldırgan istekleri paralel gönderip 5 deneme sınırını tümden atlardı.
`TryAddAsync` de yetmez: "ilk kazanır" der, **kaçıncı olduğunu saymaz**.

---

## 4. PİNLER (10) ve MK-6 MUTASYONLARI

**Onunun dokuzu davranış pinidir** (gerçek `Program` host'u + gerçek uç + gerçek SQL);
`CustomWebApplicationFactory` **kullanılmadı** (Testcontainers → Docker ister, bu makinede
yok), `AuthRateLimitPinTests`in Docker'sız kalıbı izlendi.

**ONUNCU PİN (`SAYAC_OKUMASI_...`) KAYNAK-SÖZLEŞMESİDİR ve bu bilinçlidir** — pinlediği kusur
yalnız Redis'te görünür, bu rig'de Redis yok. Davranış kanıtı **canlı sunucudadır** (§4.4
önce/sonra ölçümü). *(Bu satır, ilk yazımda "hepsi davranış pinidir" diyordu; onuncu pin
eklendiğinde önerme YANLIŞLAŞTI — "yorum ≠ ölçüm" ailesinin bu belgedeki örneği.)*

| Mut | Ne | Sonuç |
|---|---|---|
| **13** | HMAC → düz SHA-256 | 1 kırmızı — `OZET_BIBERLI_DUZ_SHA256_DEGIL` |
| **14** | 5 deneme sınırı devre dışı | 1 kırmızı — `BES_YANLIS_DENEME_KILITLER_...` |
| **15** | süre kontrolü devre dışı | 1 kırmızı — `SURESI_DOLMUS_KOD_400_...` |
| **16** | register düz kodu DB'ye yazar | **0 KIRMIZI — PİN KÖRDÜ** |
| **16b** | aynı mutasyon, pin sıkılaştırıldıktan sonra | 1 kırmızı — `DUZ_KOD_VERITABANINDA_SAKLANMAZ` |
| **17** | doğrulanmış hesap dalı 200'e geri döndürüldü | 1 kırmızı — `DOGRULANMIS_HESAP_VARLIK_ORAKULU_DEGIL` |
| **18** | register maile **taze bir kod** yazar (saklanandan farklı) | 1 kırmızı — `DogrulamaMaili_...` · LF-5'in 7 pini **YEŞİL KALDI** |
| **19** | 60 sn soğuma dalı devre dışı | 1 kırmızı — `SOGUMA_YENI_KOD_URETMEZ_ama_YANIT_AYNI_200` |
| **20** | sayaç okuması `GetAsync<long>`a döndürüldü | 1 kırmızı — `SAYAC_OKUMASI_...` · **8 davranış pini YEŞİL** |
| **20b** | `SayacOkuAsync` `IDistributedCache`e döndürüldü | 1 kırmızı — aynı pin (iki yarısı da yük taşıyor) |

### 4.0 DALGA İÇİ DENETİMİN BULDUĞU KUSUR — **BU DALGA ÜRETTİ** (pin 7)

**Bulgu:** `VerifyEmail`, doğrulanmış bir hesap için **200 "E-posta zaten doğrulanmış."**
dönüyordu. Eski uçta bu **sızıntı değildi** — girdi yalnız 43 karakterlik jetondu, yani o dala
ancak **geçerli bir jeton taşıyan** biri varabilirdi. LF-5 girdiyi `(e-posta + kod)` yapınca
aynı dal **saldırgan denetimine açıldı**: tek başına bir e-posta adresi yazan biri
`200 "zaten doğrulanmış"` ile `400 "kod geçersiz"` farkından **"bu hesap VAR ve DOĞRULANMIŞ"**
bilgisini okuyabilirdi. Yani kusuru **imza değişikliğinin kendisi** üretti.

Bu, D1'in kendi kabul ölçütünü (*"adres var/yok sızdırmaz"*) çiğniyordu ve **kendi yazdığım
üst yorum** (*"adresin kayıtlı olup olmadığını ele vermez"*) **fazla iddialıydı** — önerme
ancak düzeltmeden sonra doğru oldu. **YORUM ≠ ÖLÇÜM** ailesinin bir örneği daha.

**Düzeltme:** doğrulanmış hesap da kayıtsız adresle **aynı 400 + aynı gövde**yi alır ve
**sayaç da artar** (artmasaydı "sayaç arttı mı" sorusu aynı ayrımı geri açardı). Mesaj yalan
değil: doğrulanmış hesabın **bekleyen kodu yoktur**, "kod geçersiz" onun için de olgusaldır.
Kullanıcı bilgi kaybetmez — `resend-verification` zaten doğrulanmış hesaba *"hesabın zaten
doğrulanmış"* **mailini** atıyor (G2b kalıbı): bilgi **adresin sahibi olan kanaldan** gider,
HTTP yanıtından değil.

**Pin ayırt edicidir:** yalnız durum koduna bakmaz — doğrulanmış hesabın yanıtı ile **hiç
kayıt olmamış** bir adresin yanıtının **durum kodu VE gövde olarak eşit** olduğunu ölçer;
vakum kırıcı olarak gövdenin gerçekten bir hata yanıtı olduğunu da doğrular.

**BEDELİ (dürüst kayıt):** bulgu **push'tan SONRA** çıktı — dalga içi denetimi push'tan önce
tamamlamam gerekirdi (CLAUDE.md: *"Denetim bulgu çıkarırsa PUSH BEKLER"*). Aynı denetim, aynı
sebeple bir **ikinci** bulgu daha üretti (§4.3, pin 8). Sonuç: tarif **tek push** diyordu, bu
dalga **ÜÇ push** oldu. Kural ihlali bendedir, gizlenmiyor; kökü tek: **denetimi sıraya değil
sonuna koydum.**

**ÖLÜ YÜZEY (kayıt):** `Messages.EmailAlreadyVerified` artık **hiçbir yerden çağrılmıyor**
(ölçüldü: kalan kullanım 0). Silinmedi — sabitler dosyasına dokunmak bu dalganın kapsamı
değil; **launch sonrası temizlik** kalemi olarak kayda geçti.

### 4.1 KENDİ HATAM — KENDİNE REFERANSLI PİN (MUT-16)

`DUZ_KOD_VERITABANINDA_SAKLANMAZ` ilk yazımda `KayitAcAsync` yardımcısını çağırıyordu; o
yardımcı kayıttan **sonra** satırı bilinen kodun özetiyle **eziyor**. Yani pin, **kayıt
yolunun yazdığını değil kendi yazdığını** doğruluyordu. Register düz kodu yazacak şekilde
bozulduğunda pin **yeşil kaldı**.

Düzeltme: test artık kayıt ucunu koşup satıra **dokunmadan** okuyor ve asıl iddiayı ölçüyor —
*"saklanan değer 6 haneli düz kod olamaz"*. Bu, GF-6'daki MUT-20'nin (`CsvSatirEnCok` hem
dosyayı kurup hem beklenti olan pin) **aynı ailesidir**: bir pin, ölçtüğü değeri kendisi
üretiyorsa bedava doğrudur.

### 4.2 PİNİN YAKALADIĞI GERÇEK KUSUR

`BES_YANLIS_DENEME_...` ilk koşumda **kırmızı** verdi ve sebebi benim kodumdaydı:
`MemoryCacheService.IncrementAsync` değeri `(sayaç, bitiş)` **demeti** olarak saklıyordu, ama
`VerifyEmail` sayacı `GetAsync<long>` ile okuyor. Tip uyuşmadığı için okuma **sessizce 0**
dönüyordu — yani "5 denemeyi aştı mı" kontrolü **her zaman geçiriyordu**. Derleyici bundan
şikâyet etmez; yakalayan şey davranış piniydi. Yazan ve okuyan artık **tek tip** üzerinde
anlaşıyor.

---

### 4.3 DENETİM BAŞLIK 6 ("BOZDUKLARIM") BİR KAPSAM BOŞLUĞU GÖSTERDİ (pin 8)

Kırılan pin (`DogrulamaMaili_TIKLANABILIR_LINK_...`) üç şeyi koruyordu; ikisinin karşılığı
kondu, **biri karşılıksız kaldı**:

| Eski pin ne koruyordu | Yerine ne kondu |
|---|---|
| gövde beyan edilen vitrin origin'ini taşır | **daha güçlü**: `NotContain("http")` — hiç URL yok, yanlış origin riski de yok |
| kullanıcı kodu **elle** girebilir | artık **TEK yol**; `\d{6}` + `DOGRU_KOD_200_DONER...` |
| link **o hesabın GERÇEK** jetonunu taşır | **KARŞILIKSIZDI** ← boşluk |

`\d{6}` yalnız **biçimi** ölçer: kayıt yolu her hesaba aynı sabit kodu gönderse ya da mailde
başka bir hesabın kodunu yazsa pin yine **yeşil** kalırdı. LF-5'in kendi pin dosyası da bu
boşluğu kapatmıyor — orası bilinen bir kodun özetini satıra **kendisi** yazıyor, yani kayıt
yolunun ürettiği kodu **hiç görmüyor** (MUT-16'daki kendine-referanslılık ailesinin komşusu).

**Kapatıldı:** mail gövdesinden okunan kod **gerçek uçtan o hesabı doğruluyor** (200 **ve**
DB'de `email_verified` gerçekten `true` — vakum kırıcı). **MUT-18** ayırt edici: register
maile taze bir kod yazacak şekilde bozulduğunda **yalnız bu pin** kırmızı verdi; LF-5'in
yedi pini **yeşil kaldı** — yani boşluk gerçekti ve onu yalnız bu assert kapatıyor.

---

### 4.4 CANLIDA KIRDI — RIG'İN YAPISAL KÖR NOKTASI (pin 10, KAYNAK-SÖZLEŞME)

**Dağıtımdan SONRA, gerçek tarayıcıyla bakarken çıktı.** `curl` ile alınan ayırt edici prob
temizdi (400); aynı URL tarayıcıda **500** verdi. Fark tarayıcı değildi — **kaçıncı istek
olduğuydu.**

```
ÖNCE  (39f4c5b öncesi, taze adres):   istek 1 -> 400   istek 2 -> 500   istek 3 -> 500
SONRA (39f4c5b, taze adres):         istek 1..5 -> 400 "kod hatalı"
                                     istek 6    -> 400 "Çok fazla hatalı deneme yapıldı."
                                     (500 YOK; 5 deneme hakkı, 6. istek reddedilir)
SONRA (aynı URL, GERÇEK TARAYICI):   üç ardışık istek -> 400 / 400 / 400
SONRA (eski ZEHİRLİ anahtar):        probe@example.invalid -> 400  (500 YOK)
```

**KÖK SEBEP (üç bağımsız kanaldan ölçüldü — API logu · `redis-cli type` · kaynak):**
`RedisCacheService` aynı anahtar için **iki uyumsuz temsil** kullanıyordu:

| | primitif | Redis'teki temsil |
|---|---|---|
| `IncrementAsync` (sayacı YAZAR) | `db.StringIncrementAsync` | ham **string** |
| `GetAsync<long>` (sayacı OKURDU) | `IDistributedCache.GetStringAsync` | **hash** (`HMGET absexp sldexp data`) |

İlk istekte anahtar yok → `GetAsync` 0 döner, `IncrementAsync` ham string yaratır. İkinci
istekte `HMGET` ham string anahtara çarpar → **`WRONGTYPE`** → 500. Yani kusur **ikinci
istekten itibaren** ve **her adres için** ortaya çıkıyordu.

**NEDEN HİÇBİR PİN GÖRMEDİ — DÜRÜST KAYIT.** Test host'u Redis **kullanmıyor**; `Program.cs`
Redis yoksa `MemoryCacheService`e düşüyor ve orada yazan da okuyan da **aynı sözlüğe**
gidiyor. Dokuz pinin dokuzu da yeşildi. Daha kötüsü: bu dalganın **erken** bir düzeltmesi
(`MemoryCacheService.IncrementAsync` demet→`long`) belleği *daha da* tutarlı yapıp ayrışmayı
**maskeledi**. Bu, `53·AV-3`'ün "rig kör noktaları" ailesinin yeni ve **en pahalı** üyesi:
**bellek ile Redis'in AYNI arayüzü FARKLI temsille karşılaması.**

**DÜZELTME:** `ICacheService.SayacOkuAsync` — sayacı **yazanla aynı primitifle** okuyan üye.
Redis'te `StringGetAsync`, bellekte aynı `long`. `VerifyEmail` artık bunu çağırır.

**PİN (10) BİLİNÇLİ OLARAK KAYNAK-SÖZLEŞMESİDİR** — bu dosyadaki tek istisna. Gerekçe: kusur
**yalnız Redis'te** görünür, bu rig'de Redis **yok** (Docker yok), dolayısıyla davranış pini
YAZILAMAZ. Davranış kanıtı **canlı sunucudadır** (yukarıdaki önce/sonra). MUT-20 ve MUT-20b
pinin **iki yarısının da yük taşıdığını** gösterdi; ikisinde de **diğer sekiz davranış pini
yeşil kaldı** — yani "rig bunu göremez" iddiası da ölçülmüş oldu.

> **KENDİ HATAM, İKİ KATMANLI:** (1) arayüze atomik bir yazma ekleyip okumasını **eski genel
> yoldan** bıraktım; (2) "testler yeşil" ile "canlıda çalışır" arasındaki farkı, tam da bu
> deponun defalarca bedelini ödediği yerde (**yerelde yeşil / canlıda kırmızı**) yeniden
> ürettim. Kusuru dağıtım **sonrası** gerçek tarayıcı yakaladı — `curl` ile alınan tek
> istekli prob **yeterli değildi**; ayırt edici deney **ARDIŞIK** istek olmalıydı.

## 5. VİTRİN (D3)

6 haneli sayısal giriş (`inputmode=numeric` · `autocomplete=one-time-code` · `maxlength=6`),
**60 sn geri sayımlı** "Tekrar gönder", üç ayrı hata metni (yanlış kod / süresi doldu / çok
deneme) — metinler **sunucudan** gelir, istemci kendi metnini uydurmaz (ikinci doğruluk
kaynağı açılmaz).

**Soğuma neden istemcide görünür:** sunucu soğumada **sessizdir** (aynı 200 döner — farklı
yanıt adresin kayıtlı olduğunu ele verirdi). Görünür geri sayım olmasaydı kullanıcı arka arkaya
basar, her seferinde "gönderildi" görür ama mail **gelmezdi** — sessiz bir yalan.

**`#/dogrula/<token>` yolu emekliye ayrıldı ama SİLİNMEDİ:** eski mailler hâlâ dolaşımda;
silinseydi o bağlantılar "Sayfa bulunamadı"ya düşerdi. Yol artık doğrulama **denemez**,
kullanıcıyı yeni kod istemeye yönlendirir.

**SÖZLÜK DOKUNULMADI** (merkez kararı): yeni metinler sabit TR. i18n karşılıkları
**VİTRİN-KALAN**'a yazıldı.

---

## 6. DAĞITIM ve CANLI KANIT

**Dağıtılan SHA `39f4c5b`** (LF-4 + LF-5 **TEK** dağıtım). Sunucu `34be485`ten geliyordu,
yani bu dağıtım LD-1 FAZ 3-5 kayıtlarını, soft-launch kapısını, LF-4'ü ve LF-5'i **birlikte**
taşıdı. Sürükleme (drift) dağıtımdan **önce** bayt düzeyinde ölçüldü: iki compose dosyası
hedefle **aynı**, iki frontend dosyası `set-api-origin.sh` yazımıydı ve yeniden üretildi,
`.env` gitignore'lu + `chmod 600` → checkout **dokunamaz**.

**API imajı YENİDEN KURULDU.** Yalnız `--force-recreate` yapılsaydı C# değişikliği **bayat
imajla** sessizce dışarıda kalırdı — bu deponun üç kez bedelini ödediği "bayat ikili" ailesi.

| Kanıt | Ölçüm |
|---|---|
| Uç imzası değişti | `"The token field is required."` → `"Kod hatalı…"` |
| LF-4 PWA muafiyeti | `/manifest.json` `/service-worker.js` `/pwa-register.js` `/robots.txt` **401 → 200** |
| Kapı **kapalı KALDI** | `/` hâlâ **401** (soft-launch bozulmadı) |
| Gerçek tarayıcı | `manifest.json` · `pwa-register.js` · `/icons/icon-192.png` kimlik**siz** yüklendi |
| Konteyner sağlığı | LF-3 `curl -fsS` düzeltmesi ilk kez canlıda → **healthy**, failing streak **0** |
| Dağıtımdan sonra istisna | `WRONGTYPE` / "Beklenmeyen hata" sayısı **0** |

### 6.1 UÇTAN UCA CANLI KANIT — KOD EKRANA BASILMADAN

Brevo'nun **gerçekten teslim ettiği** kodun hesabı doğrulaması, değeri hiçbir yere yazmadan
ölçüldü (SIR KURALI): kod outbox payload'ından **sunucu kabuk değişkenine** alındı, yalnız
**uzunluğu** (6) rapor edildi.

```
resend HTTP=200  ->  kod uzunlugu=6 (deger BASILMADI)
verify HTTP=200  ->  {"success":true,"message":"E-posta adresiniz doğrulandı."}
DB      : email_verified = 1 · email_verification_token = NULL (tek kullanımlık UYGULANDI)
outbox  : id 25 · status=1 (Processed) · 11:23:54 -> 11:24:14 · error NULL
saklanan: email_verification_token uzunluk 64 (HMAC hex) — düz 6 hane DEĞİL
```

### 6.2 D-YAN (temizlenecek) ve DÜZELTİLEN VARSAYIM

- **`customers` satırı `omery3899+lf5@gmail.com`** — gerçek kod maili kanıtı için açıldı,
  açılıştan önce **silinir**. Adım `ops/deployment-checklist.md` > "0b) TEST VERİSİ TEMİZLİĞİ".
- **BAYAT VARSAYIM DÜZELTİLDİ:** tarif "mevcut doğrulanmamış kayıtlar, Ömer'in hesabı dahil
  D-YAN" diyordu. **Ölçüldü: Ömer'in asıl hesabı ZATEN doğrulanmış** (`email_verified = 1`,
  token NULL) — SMTP düzeltmesinden sonra drenaj edilen 22 mail bunu çoktan kapatmış. Bu
  yüzden onun hesabına **dokunulmadı**; kanıt `+lf5` etiketli ayrı bir adresle alındı.

### 6.3 SERVICE WORKER `VERSION` BUMP'I ATLANDI — ÖLÇÜLMÜŞ GEREKÇE

Dağıtım betiği bunu hatırlatır; **bilinçli atlandı.** SW'nin `kodTasiyorMu` dalı navigasyonu
ve `.html`/`.js`'yi **network-first** yapar (kaynak okundu, yorum değil), yani bump unutulsa
bile kod taşıyan dosyaların yeni sürümü gelir. Risk **cache-first** varlıklardadır
(`manifest.json`, ikonlar) ve **ölçüldü: bu dalgada ikisi de değişmedi (0 dosya).** Kural
checklist'e yazıldı: *cache-first varlık değiştiyse bump ZORUNLU.*
