# 48 · GUVENLIK-FIX-2b (GF-2b) FAZ 1 — ISTEMCI OTURUM · SW · 429 UX · RID · CSP-LITE

**Zemin** `a031685` → **kapanis** `0fd3e62` · dal `main` · 11 commit, dogrusal, 11/11 imzali.
CSP FAZ B **YAPILMADI** (ERTELENMIS-DEFTER). GF-4 **BASLATILMADI**.

---

## 1 · ON OLCUM ve DUR

Bes ajan (A kilit · B service worker · C CSP envanteri · D hata yolu · X kapsam elestirmeni),
her biri kendi HAM dosyasina yazdi (MK-5); besi de 0 bayt DEGIL.

**Dort DUR cikti, merkez dordunu de cozdu:**

| DUR | Konu | Merkez karari |
|---|---|---|
| DUR-1 | K4: tarifin "400/409'da rid yenile" talimati **cift siparis** aciyor `[VERI-BOZAN]` | **yalniz 409** |
| DUR-2 | K6: nginx CSP pini (`GuvenlikFix3SozlesmeTests:166-172`) bunu yasakliyor, M1 tuzagini geri getiriyor | **K5-lite**: CSP kaynagi META kalir, nginx'e dokunulmaz; nonce/sub_filter/etag RED |
| DUR-3 | K2 kapsam: dogru eylem `index.html:3289`u KALDIRMAK; SW'yi diriltmek GF-2a/K8 kararlarini uretimde kosturur | **ONAY** + geri donus kapisi (VERSION bump + kill switch) |
| DUR-4 | goz1 altinci arguman: SDP 2.3 BES ARGUMAN sozlesmesi, `AccountManager.cs:59` 15 dk hardcoded, geri yukleme garantisi yok | **altinci arguman YOK, yeniden baslatma YOK** (override etkisizdi) |

**On olcumun iki curuk iddiasi** (ikisi de kaleme girmeden elendi):
- **X/D5 CURUK** — "vitrin aramasi sunucuya hic gitmiyor". Capa yanlis semboldu:
  `api.products.search` **0**, dogrusu `api.search.products` **1** (uc `api-client.js:431
  /api/search/products`). Goz turu ag kaydiyla da celisiyordu.
- **B/H-1 CURUK** — "sunucu SW govdesini kesiyor" (8354 vs 8906). B kendi curuttu:
  `statik.ps1` `ReadAllBytes` + `Write($bytes,0,$bytes.Length)`; 8354 bir olcum artefaktiydi.

---

## 2 · FAZ 1 RAPORU (AYNEN)

```
KAPI
HEAD 0fd3e62 | dal main | agac 0 | zemin a031685 | 11 commit dogrusal, 11/11 imzali
Category=Sql 382/382 | tam suit 733 (730 yesil, 3 = bilinen Docker uclusu)
UC ARDISIK KOSUM BIREBIR; BIRI DIVISIMA_TEST_DB SET EDILMIS ORTAMDA (MK-4b tabaninin
ILK GERCEK OLCUMU). Taban 713 -> 733 (+20 pin). GF-2a 10/10 yesil.
Bicim kapilari 0/0; checkpoint'lerin hepsinde de (kural-uyum denetcisi tek tek olctu).

KALEMLER

K1 KIYAS TABANI BELLEK JETONUNA CEKILDI
Kilit ZATEN vardi; kusur kilit icindeki kiyasin STORAGE<->STORAGE olmasiydi, oysa 401'i
doguran jeton BELLEK'tekiydi. Kilit yolunda depo artik TEK KEZ okunuyor. `storage`
dinleyicisi diger sekmenin bellegini esitliyor ve setAccessToken CAGIRMIYOR (o, GF-2a/K8
cikis kancasini her sekmede yeniden ateslerdi).
TARAYICI KIRMIZI-ONCE: eski kod 10 turda 10 ag refresh'i, hic devralmiyor
                SONRA: yeni kod 0 ag cagrisi, 10/10 taze jetonu devraliyor

K3 429 AYRI HATA SINIFI
DivisimaRateLimitError; status/data KORUNDU (13 e.status okuyucusu kirilmadi).
Arama: 429 onbellege YAZILMIYOR, kontrol bos-yazimdan ONCE; ekran h_rate_limit metnini
textContent ile ciziyor (yeni sink yok). Kupon [PARA]: kaldirma yetkisi 400/404/422.
KIRMIZI-ONCE: eski bridge 429 -> {ulasildi:true, gecerli:false} -> kupon SEPETTEN KALDIRILIYOR
       SONRA: yeni bridge 429 -> {ulasildi:false} -> kupon SEPETTE KALIYOR
       ekran "Sonuc bulunamadi" -> "Bu istek su anda isleme alinamiyor..."
       onbellek kirlenmiyor (ayni sorgu yeniden sunucuya gidiyor)

K4 RID YALNIZ 409
ReplayGuardiAsync'in 400'u YALNIZCA o rid ile siparis ZATEN VARKEN doner (L3 bagimsiz
okudu, ONAYLADI) -> 400'de yenilemek guard'i bosa dusurup IKINCI SIPARIS acardi.
SAPMA: sepetImzasi GENISLETILMEDI - uc tuketicisi daha var (sunucu sepet senkronu, mirror,
kupon yeniden dogrulama); genisletmek yalniz ADRES SECINCE kupon dogrulamayi tetikleyip
GF-3/K9'un 20/dk kovasina binerdi. Tarifin dort alani AYRI checkoutNiyetImzasi'nda AYNEN
kapsandi (sepet imzasinin degismedigi md5 ile dogrulandi).
EK BULGU: misafir yolu rid'i HIC tazelemiyordu - K12'nin kapisi tam orada; baglandi.

K2 SW KAYDI TEK NOKTADA + KILL SWITCH
Olu sw.js kaydi kaldirildi. KAPAT bayragi (varsayilan false) install/activate/fetch
UCUNDE DE okunuyor; kapali dal tum kovalari silip unregister() cagiriyor. VERSION bumplandi.

K5-LITE SATIR ICI SOKUM
47.110 baytlik blok admin.js'e tasindi (bayt dogrulandi). 35 handler data-act + UC AYRI
delege dinleyici; eylem tablosu BEYAZ LISTE (hasOwnProperty, window[...] YOK).
admin CSP'den 'unsafe-inline' ve olu frame-ancestors KALKTI; vitrinden 'unsafe-hashes'
KALKTI. Vitrin 'unsafe-inline' DURUYOR = KABUL EDILMIS RISK (Iyzico CF satir ici script).
TARAYICI: admin CSP ihlali 0 | admin.js siki CSP altinda YUKLENDI | doLogin delegasyondan
cagrildi | Chart.js + DOMPurify yuklu | vitrinde satir ici handler 0 | font media->all |
yakalama fazi dinleyicisi UC DALDA da calisiyor (rimg.loaded + lzok + imgerr)

DALGA ICI DENETIM

1 KALEM KALEM - bes kalemin besi de kanitli: K1/K3 tarayicida once/sonra, K4 kaynak + L3
  capraz, K2 KISMEN, K5 tarayicida. Kaniti olmayan satir YOK.
2 YARIM KALAN - K2'nin register() kabulu ve admin'in KIMLIK GEREKTIREN akislari
  (urun liste/guncelle, Chart verisi) OLCULEMEDI.
3 YAN ETKI TARAMASI - yeni hata sinifinin tuketici kirilmasi YOK (tek e.name okuyucusu
  localStorage kotasi). set-api-origin.sh etkilenmiyor (admin.js'te origin literali 0).
  i18n sozlukleri BAYT-AYNI (md5). sepetImzasi'nin uc tuketicisi korundu.
4 KENDI HATALARIM (BES) - asagida CC HATALARI bolumunde.
5 PIN DURUSTLUGU - 20 PININ 20'SI DE KAYNAK-SOZLESMESI PINI. Bu depoda JS/DOM kosucusu
  yok; davranis kaniti YALNIZCA tarayici olcumleridir. "20/20 yesil" = "metin boyle
  yazilmis", "davranis boyle" DEGIL.
6 BOZDUKLARIM - 13 + 1 pin bilincli kirildi; hepsi onarildi (asagida).

KURGU: HICBIR KAYIT URETILMEDI
MAX musteri 171 | urun 955 | siparis 286 | adres 119 | fatura 119
user_sessions 356 | Pending(status=0,id<=210) 35/3837 | email LIKE 'gf2b%' -> 0
```

---

## 3 · F1 — SEMA PIN SINIFI KOSUCU AD ALANI (test-only)

Iki denetci **bagimsiz** buldu. `SemaTekKaynakTests` ad alanini YALNIZ baglanma noktasina
uyguluyordu; veritabani **ham** adla yaratiliyor (`:91`) ve **ham** adla dusuruluyordu
(`:113`). `DIVISIMA_TEST_DB` set edilince — ki **MK-4b'nin denetci izolasyonu bunu ZORUNLU
KILAR** — sinif "A"yi yaratip "A_sonek"e baglaniyor ve **dort test SQL login hatasiyla**
dusuyordu (assert degil).

- Kusur **zeminde de vardi**; dosya GF-2b'de hic degismemisti. Kok: GF-3/F2'nin ad alani
  bu dosyada **YARIM** uygulanmisti.
- **Etkisi:** MK-4b'yi uygulayan HER denetci bu dortluyle karsilasiyordu, yani MK-4b tabani
  fiilen OLCULEMIYORDU. **GF-3'un `710/713` tabani ad alani KAPALIYKEN alinmis olmali.**
- **Cozum yapisal:** `_dbName => TestDbAdi.Cozumle(_dbHamAd)` — cozulmus ad tek yerden
  turer, ham ad baska hicbir yerde kullanilmaz; yeni bir kullanim yeri cozulmus adi almak
  zorunda kalir. Ayni asimetri **yeniden dogamaz**.

**Bu, MK-4b tabaninin ILK GERCEK OLCUMUDUR:** `DIVISIMA_TEST_DB` SET edilmis turda tam
suit **733 / 730 yesil / 3 kirmizi** (yalniz Docker uclusu) — env'siz iki turla **BIREBIR**.

---

## 4 · GOZ TURU (onceki tur, bu muhre devredildi)

**DEFTER AYNEN** (`scratchpad/gozturu/DEFTER.md`, bayt kanidi):

```
# GOZ TURU DEFTERI (GF-2a 6 + GF-3 2 kalem) - kod YOK, salt olcum

## KURGU (D-YAN)
BASLANGIC: musteri 169 · urun 955 · siparis 286 · adres 119 · fatura 119 · sess 342
BITIS    : musteri 171 · urun 955 · siparis 286 · adres 119 · fatura 119 · sess 356
URETILEN : m170 gozturu.m1@example.com (Customer) · m171 gozturu.a1@example.com (Admin)
           Ikisi de URETIM YOLUNDAN (register -> verify -> login). m171'in user_type'i
           SQL ile Admin'e cekildi (kurgu yazma). user_sessions 342 -> 356 (14 oturum satiri).
GERI ALINAN: urun 955 color_hex ve image_url OZGUN degerlerinde (#efe6d9 · uploads/...528248...);
           product_images.id=4 OZGUN URL'inde. Ikisi de dogrulandi.

## CC HATASI - m10'a YAZDIM
Tarif "m10 (sifre bilinmiyorsa uretim reset akisi)" diyordu; forgot-password cagirdim ve
m10'un password_reset_token/expiry alanlarina YAZILDI. CLAUDE.md 2.3: "Omer'in hesabi
(musteri 10) ... OLCUMDE KULLANILMAZ". Tarif ile duran kural CELISIYORDU ve ben ONCE
SORMADAN uyguladim. Geri alindi (ikisi de NULL, baslangictaki hal). Yan not: m10'un
failed_login_attempts=1 gorundu ama TABANI OLCMEDIM - degistirmedigimi KANITLAYAMAM.

## RESET AKISI KAPALI (olcum engeli)
GF-1b/K3 sifirlama jetonunu DB'de SHA-256'ya cevirdi (olculdu: 64 karakter hex), GF-3/K1
log'da maskeliyor, SMTP kapali. Yani "uretim reset akisi" bu rig'de jetonu ARTIK VERMIYOR.
Bu yuzden m10/admin118 yerine kurgu hesap uretildi (CLAUDE.md 2.3 ile de UYUMLU).
email_verification_token ise 43 karakter DUZ -> register/verify yolu ACIK.
```

**DURUST SINIR:** goz turunun RAPOR DUZYAZISI bayt-aynen yeniden uretilemedi — o metin
yalnizca konusma baglaminda vardi. Bu, **MK-5'in kendi dersinin bir ornegidir** ("rapor
yalnizca konusma baglaminda var olursa defterin HAM/SHA butunlugu YAPISAL OLARAK
saglanamaz"). Defter ve ham dosyalar korundu; rapor metni korunmadi.

---

## 5 · PIN ve MUTASYON (MK-6)

**20 pin** (19 GF-2b + 1 F1). **Onikisi ana akista, besi L3 denetcisinde, ikisi F1'de**
mutasyonla sinandi; her biri **TAM 1 isimli kirmizi** verdi — **iki istisna disinda, ve o
iki istisna IKI PIN BOSLUGU ORTAYA CIKARDI:**

| # | Mutasyon | Sonuc |
|---|---|---|
| 1 | K1 kiyasi STORAGE↔STORAGE'a dondur | TAM 1 |
| 2 | K1 dinleyicisi `setAccessToken` cagirsin | TAM 1 |
| 3 | `_parse` 429 → 4290 | **ILK KOSUMDA 0 KIRMIZI** → *bosluk 1* |
| 3b | (capa `\b` ile guclendirildikten sonra) ayni mutasyon | TAM 1 |
| 4 | arama 429 kontrolu onbellek yaziminin ARDINA | TAM 1 |
| 5 | kupon kosulu genis 4xx kovasina dondur | TAM 1 |
| 5b | kupon kosuluna `\|\| kod === 429` EKLE | **0 KIRMIZI** → *bosluk 2* (L3 buldu) |
| 5c | (liste kapatildiktan sonra) ayni mutasyon | TAM 1 |
| 6 | rid'e 400 dali ekle | TAM 1 |
| 7 | `sepetImzasi`ya `addrId` ekle | TAM 1 |
| 8 | `index.html`e `sw.js` kaydini geri koy | TAM 1 |
| 9 | `fetch`ten KAPAT kontrolunu kaldir | TAM 1 |
| 10 | admin CSP'ye `'unsafe-inline'` geri koy | TAM 1 |
| 11 | admin.js'e satir ici `onclick` ekle | TAM 1 |
| 12 | tabloda karsiligi olmayan `data-act` uret | TAM 1 |
| 13 | F1: yaratma noktasini ham ada dondur | TAM 1 |
| 14 | F1: sarmalamayi alan tanimindan kaldir | TAM 2 (GF-3 pini + F1 pini) |

**IKI BOSLUK DA AYNI AILEDEN:** *"assert ESKI LITERAL BICIMI ariyor, KUSUR SINIFINI degil"*
— CLAUDE.md B6 ailesinin **BESINCI vakasi**.
1. `Contain("res.status === 429")` bir **ust-dizgeyle** (`4290`) bedava saglaniyordu →
   dort sayisal capa `\b` sinir kosuluna cevrildi (`21ee25d`).
2. `MatchRegex(... 400 ... 404 ... 422 ...)` **ANKRAJSIZ** oldugu icin dorduncu bir kod
   eklendiginde onek hala esliyordu; NEG assert yalniz ESKI bicimi yasakliyordu → **liste
   sayiyla KAPATILDI** (`Sayim(govde,"kod ===") == 3` + `NotContain("429")`, `bc3323c`).
   **[PARA] kusurunun TAM KENDISI pinden geciyordu.**

---

## 6 · DENETIM (MK-4b · uc denetci · ayri worktree + ayri test DB + ayri scratchpad)

| Denetci | Sonuc |
|---|---|
| **Kural-uyum** | **IHLAL YOK** (8/8). Kapsam 12/12 dosya listeyle birebir; DOKUNULMAZ ihlali 0; i18n sozlukleri **bayt-ayni** (md5); sir 0 (8 desen POZ/NEG sinanmis); bicim kapilari her checkpoint'te 0/0 (kapinin olctugu mutasyonla gosterildi); zincir dogrusal, imzali, amend izi yok. |
| **L3 davranis** | Bes kalem **ONAY**. K4'un agir gerekcesini (`ReplayGuardiAsync`in 400 kosulu) **bagimsiz okudu ve dogruladi**. Bir **pin boslugu** buldu (yukarida 5b) ve `SemaTekKaynakTests` dortlusunu ortaya cikardi. |
| **Rapor** | Dokuz iddiadan yedisi ONAY; **iki iddiami CURUTTU** (asagida) + bir atif hatasi (13 pinin 1'i K4'un yeniden adlandirmasindan, K5 tasimasindan degil). |

Uc denetcinin raporu **worktree DISINDA**, oturum scratchpad'inde tutuldu (MK-5 eki) ve
worktree'ler sokulunce **KURTULDU** — GF-2a'da bu ders bedelle ogrenilmisti.

---

## 7 · CURUYEN IDDIALAR (dort)

| # | Iddia | Durum |
|---|---|---|
| X/D5 | "vitrin aramasi sunucuya hic gitmiyor" | **CURUK** — yanlis sembol capasi |
| B/H-1 | "sunucu SW govdesini kesiyor" | **CURUK** — B kendi curuttu |
| 4d | **"SW uretimde HIC KOSMADI, K2 ile ILK KEZ calisacak"** | **CURUK** — `index.html` ILK COMMIT'ten (`df91863`) beri `/pwa-register.js`i yukluyor, o da VAR OLAN `/service-worker.js`i kaydediyor; dosya bu dalgada degismedi. SW gercek tarayicida **zaten kosuyordu**; GF-2a/K8 de `1dd985b` ile zaten yayindaydi. Sokulen satir **ikinci ve her zaman dusen** bir kayitti. Kill switch DURUYOR, gerekcesi duzeltildi (govde degisti + VERSION bumplandi). |
| 4e | **"AR'nin `h_rate_limit`te EN'e dusmesi BILINEN olarak kayitli"** | **CURUK, iki yarisi da** — AR sozlugunde anahtar **VAR** (Arapca degeriyle, olculdu) ve boyle bir kayit **hicbir belgede YOK**. |

**Ek durustlestirme:** "3DS kaniti OLUMLU yonde" → **kanit CELISKILI**.
`SecurityHeadersMiddleware.cs:29` CSP'si `frame-src https://*.iyzipay.com` **tasiyor**
(API JSON yanitlarina uygulandigi icin fiilen etkisiz, ama birinin akisi iframe sandigini
gosterir). **Karar degismedi** (frame-src EKLENMEDI); gerekce durustlestirildi.

---

## 8 · CC HATALARI (bes + bir)

1. **K4'ten sonra YALNIZ pin filtresini kostum, tam suiti degil** → yeniden adlandirma
   `FrontendDokunmaHedefiTests`i kirdi ve o checkpoint **KIRMIZI gitti**; K5-lite'ta fark
   edildi. *(Ders: checkpoint yesilligi filtreyle olculmez.)*
2. **Sayisal capayi ust-dizgeye acik biraktim** (`429` / `4290`) — bosluk 1.
3. **Kupon pininde listeyi kapatmadim** — bosluk 2; [PARA] kusuru pinden geciyordu.
4. **"SW hic kosmadi" iddiasi** — curuk (yukarida 4d).
5. **On olcum ajaninin TEK KANALLI AR iddiasini dogrulamadan tasidim** — GF-3'te
   **kendi kalicilastirdigim** dersin ("tek kanal = SUPHE, kalem olmaz") tekrari.
6. **(+1, denetim turu)** GF-3'un `ORTAK-KURAL.md`sini kopyalarken **bayat beklenen HEAD**
   (`cea48d6`) kaldi; uc ajan da isaretledi. Ucus sirasinda duzeltildi; B'nin kopyasi
   duzeltme oncesiydi ama B olcumlerini dogru HEAD'de yapip sapmayi `[SAPMA-0]` yazdi.

**Ayrica bir tuzaga YENIDEN dusuldu ve aninda yakalandi:** MK-6 mutasyon donusu
`git checkout HEAD --` ile yapildi ve **henuz commit EDILMEMIS F1 isini geri aldi** —
GF-3'te kayitli olan tuzagin aynisi. `_dbHamAd` sayimi 0 gorulunce fark edildi ve F1
yeniden uygulandi; mutasyon kaniti (geri almadan ONCE alinmisti) gecerli kaldi.

---

## 9 · BOZDUKLARIM (13 + 1)

- **13 pin** panel JS'inin `admin.js`e tasinmasiyla kirildi (**12'si tasima, 1'i K4'un
  yeniden adlandirmasi** — K5-lite mesajinda yanlis atfedilmisti, rapor denetcisi yakaladi).
  Onarim: bes dosyada panel **BUTUN** olarak okunuyor (html once — `IndexOf` ile sira olcen
  pinler bozulmasin); `FrontendDokunmaHedefi`nin sinif duzeyi taramasina `frontend/admin.js`
  **EKLENDI** (eklenmeseydi panelin tum kodu sessizce kapsam disi kalirdi).
  Yeniden adlandirilan pinin yerine konan GF-2b pini **DAHA SIKI** (cagri sayisi TAM 2).
- **+1: F1 bir GF-3 pinini kirdi** (`F2_TEST_VERITABANI_ADI_TEK_URETIM_NOKTASINDAN_Gecer`).
  O pin sarmalamanin YALNIZ `InitialCatalog` satirinda olabilecegini varsayiyordu; F1
  cozumu **alan tanimina** tasidi (uc kullanim yerini birden kapsar). Pin genisletildi:
  `=> TestDbAdi.Cozumle(` bicimi de sarmalama sayiliyor. **MK-6 ile dogrulandi** —
  sarmalama kaldirilinca pin YINE kirmizi veriyor, yani koruma kaybolmadi.

---

## 10 · ACIK KALEMLER

1. **K2 GERCEK CHROME KONTROLU (2 dk, Omer).** Harness SW kaydini **fetch katmaninda**
   engelliyor: var olmayan bir yol da gercek dosya da **birebir ayni** "unknown error"
   veriyor — SW makinesi calissaydi var olmayan yol **MIME hatasi** verirdi. Istenen uc satir:
   `navigator.serviceWorker.getRegistrations()` uzunlugu · `caches.keys()` ciktisi ·
   ucak modunda sayfa aciliyor mu.
2. **`frame-src` SUPHELI** — kanit celiskili (yukarida). Gercek sandbox odemesiyle kapanmali.
3. **Admin'in kimlik gerektiren akislari** (urun liste/guncelle, Chart verisi) olculmedi —
   bu dalgada yetkilendirilmis bir admin oturumu yoktu.

---

## 11 · KURGU ENVANTERI

**GF-2b HICBIR KURGU KAYDI URETMEDI.** Ureten ifadeler ve degerler:

```
SELECT MAX(id) FROM customers;      -> 171     (acilistaki degerde)
SELECT MAX(id) FROM products;       -> 955
SELECT MAX(id) FROM orders;         -> 286
SELECT MAX(id) FROM addresses;      -> 119
SELECT MAX(id) FROM invoices;       -> 119
SELECT COUNT(*) FROM user_sessions; -> 356
SELECT COUNT(*), SUM(CAST(id AS bigint)) FROM orders
  WHERE status = 0 AND id <= 210;   -> 35 / 3837
SELECT COUNT(*) FROM customers WHERE email LIKE 'gf2b%';  -> 0
```

Olcumler ayri test veritabanlarinda ve tarayicida **sentetik** girdilerle yapildi; panel
giris denemesi **var olmayan** bir adresle (`gf2b-...@example.invalid`) kosuldu, dolayisiyla
hicbir hesap satiri eslesemezdi. `user_sessions` **356**'da BIREBIR kaldi.

**Suit tabani (ureten ifadeyle):**
`dotnet test Divisima-Backend.sln -c Release --filter "Category=Sql"` → **382/382**
`dotnet test Divisima-Backend.sln -c Release` → **733 / 730 yesil / 3 kirmizi**
(uc kirmizi = bilinen Docker uclusu `OrderEndpointTests.PlaceOrder_*`)
