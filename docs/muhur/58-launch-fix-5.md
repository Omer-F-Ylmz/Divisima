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

## 2. TARİFİN ÖTESİNE GEÇEN İKİ KARAR (ölçülmüş gerekçe)

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

## 4. PİNLER (6) ve MK-6 MUTASYONLARI

Hepsi **davranış** pinidir: gerçek `Program` host'u + gerçek uç + gerçek SQL. `CustomWebApplicationFactory`
**kullanılmadı** (Testcontainers → Docker ister, bu makinede yok); `AuthRateLimitPinTests`in
Docker'sız kalıbı izlendi.

| Mut | Ne | Sonuç |
|---|---|---|
| **13** | HMAC → düz SHA-256 | 1 kırmızı — `OZET_BIBERLI_DUZ_SHA256_DEGIL` |
| **14** | 5 deneme sınırı devre dışı | 1 kırmızı — `BES_YANLIS_DENEME_KILITLER_...` |
| **15** | süre kontrolü devre dışı | 1 kırmızı — `SURESI_DOLMUS_KOD_400_...` |
| **16** | register düz kodu DB'ye yazar | **0 KIRMIZI — PİN KÖRDÜ** |
| **16b** | aynı mutasyon, pin sıkılaştırıldıktan sonra | 1 kırmızı — `DUZ_KOD_VERITABANINDA_SAKLANMAZ` |

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
