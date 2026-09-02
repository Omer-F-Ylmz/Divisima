# 46 · GUVENLIK-FIX-2a (GF-2a) MUHRU — ISTEMCI KACIS (3 Eylul 2026)

Zemin `2a74cbd` -> kapanis `1dd985b`. Uc commit: `8279474` (kalemler) · `8b860ea` (denetim
duzeltmeleri) · `1dd985b` (F1). Push `2a74cbd..1dd985b`, cift yesil.
**GOZ TURU BEKLIYOR (6 kalem) — sonuc GF-3 muhrune.**

---

## KAPANIS TABLOSU — 8 KOK / 26 KALEM

Kapanis **KOK BAZLIDIR**: ayni alani birden cok yerde yamamak "ayni kuralin ikinci
kopyasi" ailesini acardi (bu depoda YEDI KEZ bedeli odendi).

| Kok | Alan | Duzeltme yeri (TEK) | Ikizler / ek konumlar |
|---|---|---|---|
| KOK-1 | `color_hex` -> `style` | `ph()` + `guvenliRenk` | color-mix cagri yerleri x2 · cmp-swatch |
| KOK-2 | `brand` -> `<span>` | `ph()` (KOK-1 ile **ayni satir**) | `:2056` zaten kacisliydi |
| KOK-3 | `image_url` -> `src` | `api-client.js resolveUrl` (**politikanin tek yeri**) | 6 render yolu oradan gecer; `admin:742` zaten kacisliydi |
| KOK-4 | hata mesaji -> toast | `_toastStep` + `toastUndo` ISKELET DOM | 3 cagri yerinde 4 on-`esc()` SOKULDU |
| KOK-5 | `coupon.code` | `:2694` | `:2630` · `api-bridge:1904` |
| KOK-6 | `t('cat_*')` = **HAM DB METNI** | 6 sink + slug nitelikleri + `cmp_cat` | sozluk ve `kategoriEtiketiKaydet` DOKUNULMADI |
| KOK-7 | admin alanlari | `product_name` · `order_number` · `r.size` · `onclick`->`data-*` | `:476` · `:233` · `:655` |
| KOK-8 | `it.size` | `cartMeta` + `cmp_sizes` + **ckey nitelikleri (F1)** | `api-bridge:1870` |

Arti dort bagimsiz kalem: **K1** admin purify (vendor'dan, CSP'ye dokunmadan) ·
**K8** SW iki kova + `/api/` network-only + cikis kancasi · **K9** Chart.js SRI ·
**K10** `navigator.locks` sekmeler-arasi tek refresh.

---

## ON OLCUM DUR ve MERKEZIN 10 KARARI

Bes ajanli fan-out (A sink tablosu · B kacis envanteri · C+D SW/CDN · E refresh · Z kapsam
elestirmeni) tarifin altini oydu; DUR verildi, kod yazilmadan.

| # | Karar | Sonuc |
|---|---|---|
| a | E-1 icin KOD ACILIR = K10 `navigator.locks`, desteklenmeyende fail-safe | uygulandi |
| b | `api-client.js` kilidi GEVSER (K3 · K8 cikis kancasi · K10) | uygulandi - ikinci kopya ACILMADI |
| c | `data:` politikasi = `resolveUrl` sozlesmesi (raster+base64 KABUL, digerleri RED) | uygulandi |
| d | K5 DAR: iskelet DOM + `:2620` `esc()` sokumu; "6 cagri yeri" DUSER | uygulandi |
| e | K4 hex `[0-9a-fA-F]{3,4,6,8}`; color-mix MUAFIYET DEGIL, dar desenle kabul | uygulandi |
| f | K7 hedefi `r.size` (etiket duzeltildi) + `admin:432` ikizi kapsama | uygulandi |
| g | Google Fonts SRI **YASAK** (UA'ya gore degisir) | uygulandi, KABUL EDILMIS RISK |
| h | KOK-6 KAPSAMDA; sozluk DOKUNULMAZ, kacis SINK'te | uygulandi |
| i | `embedCheckoutForm` `s.text` -> **GF-2b'ye DEVIR**, kod yok | devredildi |
| j | 24 kalem / 8 kok TAMAMI kapsamda; `admin:658` `data-*`+listener | uygulandi |

**Tarifin duzeltilen uc hatasi (on olcum bulgusu):** K7'nin `admin.html:448` etiketi yanlisti
(urun adi YOK, ham alan `r.size`) · K5'in "6 api-bridge cagri yeri" YANLIS IRTIFAYDI (gercek
sink TEK) · K9'un "varsa diger CDN"i Google Fonts'a isaret ediyordu ve SRI eklemek SITEYI
KIRARDI.

---

## DAVRANIS KANITI (salt kaynak DEGIL)

Tarif "JS/DOM kosucusu YOK" diyordu; `goz1` statik sunucusu (`schtasks \DivisimaGoz1Statik`)
duruyordu ve KULLANILDI. **ORTAM UYARISI (SDP 2.3):** API sureci KOSMUYORDU; olcumler yalniz
istemci fonksiyonlarina yapildi, bes arguman bu olcumu ETKILEMEZ.

| Kalem | Olcum | Sonuc |
|---|---|---|
| K4 renk | 7 girdi POZ/NEG (hex 3/4/6/8, BUYUK harf, color-mix, nitelik kacisi, `javascript:`, 5 hane) | gecerliler AYNEN, tehlikeliler `#d9cfc2` |
| K2 marka | `<img src=x onerror=alert(1)>` yuku | ciktida `&lt;img`, DOM'da `<img>` **0** |
| K5 toast | yuklu `toastUndo` + `toast` | GERI AL butonu **YASIYOR**, `<img>` 0, cift kacis YOK, ikon `<span class="err">⚠</span>` |
| KOK-8 | `cartMeta({size:'<b>M</b>'})` | `&lt;b&gt;` VAR |
| K3 | 10 girdi (4 POZ / 6 NEG) | https/goreli/png/webp AYNEN; `javascript:`, `data:text/html`, `data:image/svg+xml`, `//evil.com`, `vbscript:`, `file:` -> **bos dize** |
| K1 | admin `guvenliHTML('<p onclick=alert(1)>ok<script>alert(2)</script></p>')` | `<p>ok</p>` · DOMPurify **3.4.14** · purify indeks 1 < api-client 2 |
| K9 | `window.Chart` yuklendi | **AYIRT EDICI**: yanlis hash olsaydi tarayici BLOKLARDI |
| K10 | `navigator.locks` destegi · `_refreshAgCagrisi` · `_okuAccessToken` | ucu de VAR |

**Bir olcum kusuru duzeltildi:** ilk turda `toast` ikonu "yok" gorundu. Sebep OLCULDU -
`_toastRun` kuyruk bayragi acikti, toast KUYRUGA girdi. Bayrak temizlenince ikon GORUNDU.
KUSUR DEGIL; varsayimla "kirik" yazilmadi.

---

## L3 DAVRANIS DENETCISI TABLOSU (worktree `gf2a-wt/d1`, HEAD `8279474`)

| Kalem | Karar | Olcum |
|---|---|---|
| R-1 renk allowlist | ONAY | 31/31 · tehlikeli gecen 0 · 5/7 hane reddediliyor |
| R-2 marka kacisi | ONAY | img 0, **NEG kontrol 1** (dedektor ayirt edici) |
| R-3 URL sema | ONAY | 32/32 · RED = bos dize · akis DUSMUYOR |
| R-4 toast (a-d) | ONAY | buton yasiyor, callback **tam 1 kez**, cift kacis yok |
| R-5 sanitizer | ONAY | vendor 3.4.14 · fail-closed **3 modda** |
| R-6 SW (a,b,c) + istemci (d) | ONAY | `/api/` put'a ULASAMIYOR · 2 kova · kabuk korunuyor |
| R-6(d) SW yarisi | **OLCEMEDIM** | `register()` otomasyon profilinde dusuyor (rig) |
| R-7 SRI | ONAY | hash birebir + **ayirt edici deney** |
| R-8 refresh kilidi | ONAY | uc es zamanli cagri -> **1 ag cagrisi**; taze jeton varsa **0** |
| R-9 kapsam disi | ONAY | 651/654 · kalan 3 = bilinen Docker uclusu |

**L3'un kendi uc hatasi (durust kayit, ucu de karari bozabilirdi):** SRI deneyi ilk turda
GECERSIZDI (storefront'ta sinyal SRI'den degil CSP'den geliyordu; `admin.html`de tekrarlandi) ·
"292 kirmizi" bir WORKTREE ARTIFAKTIYDI (`TokenOptions:SecurityKey` gitignore'lu kaynaktan;
ayirt edici deneyle 292 -> 3) · `&&` zinciri NEG grep'in exit 1'inde kirildi.

---

## PIN ve MUTASYON TABLOSU (10 pin + F1)

10 pin, **KOK BASINA**. Hepsi KAYNAK-SOZLESME pinidir ve dosya bunu KENDI basliginda
BEYAN EDER - L3 "iddia DURUST, oldurdugunden fazlasini iddia etmiyor" dedi.

| Mutasyon | Hedef | Sonuc |
|---|---|---|
| MUT-1 | `esc(brand)` -> ham | TAM 1 ISIMLI KIRMIZI |
| MUT-2 | `data:` daraltmasi kaldirildi | TAM 1 ISIMLI KIRMIZI |
| MUT-3 | SW `/api/` yanitini yeniden onbellekler | TAM 1 ISIMLI KIRMIZI |
| MUT-4 | admin `esc(p.product_name)` -> ham | TAM 1 ISIMLI KIRMIZI |
| MUT-5 | kilit adi bozuldu | TAM 1 ISIMLI KIRMIZI |
| **MUT-P1** | `_HEX_RE` -> `/^#.*$/` (denetci bulgusu) | **once 10/10 YESIL**, pin duzeltildikten sonra TAM 1 KIRMIZI |
| **MUT-F1** | dort `ckey` niteligi ham hale | TAM 1 ISIMLI KIRMIZI |

Her mutasyonda: (a) dosyaya INDI mi · (b) TEMIZ BUILD `grep " Hata"` · (c) beklenen pin.
Geri almalar **md5 ile** dogrulandi. Yedek + elle geri alma (stash DEGIL - GF-1b dersi).

---

## DENETCI BULGULARI (bes) — hepsi kapatildi

**B1 [EN AGIR] URETIM KODUNDA CURUYEN IDDIA.** `api-client.js`e "OLCULEN ONCE-DURUM: bu
metot `javascript:` semasini GECIRIYORDU ... vitrinde calisan kod elde ediyordu" yazilmisti.
**YANLIS.** Eski kod:
```js
if (/^(https?:)?\/\//i.test(u) || /^data:/i.test(u)) return u;
return this.baseUrl... + (u.startsWith("/") ? u : "/" + u);
```
`javascript:alert(1)` IKISINE DE uymaz, dorduncu dala duser ve `host/javascript:alert(1)`
olur - bir **YOL**, calisan kod DEGIL. Gercek eski aciklik IKIYDI: protokol-goreli
`//evil.com` HAM donuyordu, ve `data:` KOSULSUZDU. **Duzeltme GECERLIYDI, GEREKCE YANLISTI.**
Ayrica `data:` muafiyetinin FAVICON ornegi KENDINI CURUTUYORDU (o tip artik reddediliyor ve
favicon bu metottan gecmiyor) - sokuldu.

**B2 KOK-6 KAPANMAMISTI.** `row(t('cmp_cat'), p => t('cat_'+p.cat), ...)`; `row()` hucreyi
HAM birlestirip `cmpBody.innerHTML`e veriyor. **Agirlastirici:** bir ALT SATIRI (`cmp_color`)
tam da ilk commit'te degistirilmisti - komsu kod duzenlenirken kacirildi.

**B2b ANA AKISIN KENDI BULDUGU UCUNCU GAP** (denetciler ISARETLEMEDI): `row()` hucrelerinin
tamami taranirken `cmp_sizes` de `p.sizes.join(' · ')` ile HAM cikti. KOK-8'in karsilastirma
tablosu yarisi.

**P-1 MK-6 BOSLUGU — IKI DENETCI BAGIMSIZ BULDU, biri CALISTIRARAK gosterdi.**
`Contain("0-9a-fA-F")` asserti DOSYA GENELIYDI ve dizge 13 kez geciyordu; `_HEX_RE`yi tek
basina `/^#.*$/` yapan mutasyon **10/10 pini YESIL geciyordu**. L3 mutasyonun ZARARSIZ
OLMADIGINI da olctu: `#fff" onload="alert(1)` girdisi mutantta **CANLI `onload` niteligi**
uretiyor. Davranis saglamdi ama KORUYAN PIN YOKTU (MK-6'nin `effective_price` kalibi).
Assert SABITIN KENDI TANIM SATIRINA baglandi + hane kumesi tek tek + `.*` NEG kontrolu.

**S1 ANA AKISIN GETIRDIGI REGRESYON.** Eski `_tryRefresh` govdesi IIFE+try/catch idi ve
**ASLA REDDEDEMEZDI**. `navigator.locks.request` reddedebilir; cagiran `_request` onu
try/catch SIZ await ediyor - red 401 yolunu DUSURUP istisna firlatirdi. `.catch(() => false)`
ile eski sozlesme geri getirildi ve PINLENDI.

**B3 (F1)** `cart` bir **Map**tir, `forEach(it,k)` ANAHTAR verir; anahtar `ckey(id,size,color)`
ve `size`/`color` DB kaynaklidir. Dort nitelik bunu HAM tasiyordu. **ANA AKIS BU BULGUYU AZ
KALSIN CURUTUYORDU** - `forEach(it,k)` imzasina bakip "k dizi indeksidir" diye elemisti;
`cart.get(k)` ve `var cart=new Map` olcumu aksini gosterdi. **Eleme gerekcesi OLCUME DEGIL
IMZAYA dayaniyordu.**

**Kucuk itirazlar (sayi) - HEPSI KABUL:** "UC on-`esc()`" -> **4** (3 cagri yerinde) ·
"kacis ALTI SINK'te" -> **7** · "`caches.open(CACHE).put` dort yerde" -> 4 `open` / **3** `put` ·
"depodaki TEK uzak script" -> gtag da dinamik enjekte (bugun inert) · "backend'le AYNI KUMEYI
kabul eder" -> KARAKTER SINIFI ayni, **UZUNLUK kumesi BILINCLI DAHA GENIS**.

**DENETCILERIN OLUMLU BULGUSU:** capa kirlenmesi ailesinin **7. VAKASI MEKANIK OLARAK
ONLENDI**. Iki NEG igne kaynakta yalniz DUZELTME YORUMLARININ icinde geciyor; bu turda
eklenen `KodSatirlari` siyiricisi olmasa IKI PIN YANLIS KIRMIZI verirdi. Aile insan
disiplinine birakilmadan onlendi.

---

## KABUL EDILMIS RISK / BILINEN

- **Google Fonts SRI YASAK** — `css2` yaniti User-Agent'a gore DEGISIR, sabit hash YOKTUR;
  eklemek SITEYI KIRAR. `font-src` allowlist'i **GF-2b**'nin isi.
- **`admin.html` kendi `imgUrl()` kopyasini tasiyor** — guvenlik acigi DEGIL (http(s)
  disindaki her sey `API_BASE` onekiyle mutlaklasiyor) ama davranisi `resolveUrl`den
  AYRISIYOR (`data:image/png` panelde bozulur) ve PINSIZ. ONCEDEN vardi, kod YAZILMADI.
- **`guvenliYaz` ayrismasi** — POLITIKA (`guvenliHTML`) OZDES; ayrisan CIZIM yardimcisidir:
  arite 2 vs 3, fail-closed dali `textContent` vs `innerHTML`, metin sabit literal vs
  `ceviri(...)`. Panel surumu **STRICT OLARAK DAHA GUVENLI** (bridge `hataMetni`yi kacissiz
  `innerHTML`e koyuyor). Gerekce koda YAZILDI, kod DEGISTIRILMEDI.
- **Panelde `guvenliHTML`/`guvenliYaz` cagiran YOK** — sarmalayici bugun bir kusuru
  KAPATMIYOR, sozlesmeyi hazir tutuyor. **Kod bunu KENDISI BEYAN EDIYOR**; L3 bagimsiz
  olctu ve "iddia DURUST, gizlenen bir sey yok" dedi.
- **`embedCheckoutForm` `s.text` yeniden calistirici** -> **GF-2b**, D-9 KABUL EDILMIS RISK
  satiri "eval esdegeri dahil" diye genisler.

---

## AV-1 KAYITLARININ DUZELTMESI

**SINK SAYIMI 131 -> 145 SATIR / 155 OLAY.** AV-1'in ureten ifadesi
`grep -coE 'innerHTML[[:space:]]*[+]?=[^=]'` **14 GERCEK SINK KACIRIYOR**: atama isaretinin
SATIR SONUNDA bittigi (prettier satir kirilmasi) yerler. Duzeltilmis ifade `([^=]|$)`.
Fark olculdu: index +2 · api-bridge +12. **AV-1 YANLIS SAYMADI - IFADE eksikti**
(eslesme-bicimi-farki ailesi). AV-1'in 131'i KENDI ifadesiyle BIREBIR tutuyor.

**D-4'un `admin.html:742` ikizi CURUDU.** O satir bugun `esc(imgUrl(i.image_url))`;
`git log -S'esc(imgUrl('` TEK commit donuyor (`fb2b046` = ozelligin ILK commit'i), yani
**HICBIR ZAMAN kacissiz OLMAMIS**. Bir duzeltme hedefi degil, KACISLI KARSIT ORNEK.

---

## GOZ TURU GEREKIR (6 kalem) — sonuc GF-3 muhrune

JS/DOM kosucusu depoda YOK; asagidakiler GERCEK API ve/veya GERCEK COKLU SEKME ister.

| # | Sayfa | Yapilacak | Beklenen |
|---|---|---|---|
| 1 | vitrin + panel, IKI SEKME | Ikisinde de oturum ac, jetonun suresi dolsun, ayni anda korumali istek at | **TEK** refresh gider; kullanici CIKMAZ. (L3 `locks`'u stub'layarak olctu; gercek coklu sekme DENENMEDI) |
| 2 | vitrin | Cikis yap, DevTools > Application > Cache Storage | `divisima-api-*` kovasi BOS/silinmis; `divisima-shell-*` DURUYOR. (SW'nin KENDI tarafi OLCULMEDI - `register()` otomasyon profilinde dusuyor) |
| 3 | vitrin | Agi kes, sayfayi yenile | Uygulama ACILIR (kabuk onbellekte); veri gelmez |
| 4 | vitrin, urun karti | `image_url`i `data:image/png;base64,...` olan bir urun | Gorsel GORUNUR (raster data: KABUL) |
| 5 | vitrin, urun detay | Kucuk renk kutucuklarina tikla | Shade onizlemesi CALISIR (`color-mix` reddedilmez) |
| 6 | panel | Panel sekmesini ac | Chart grafigi CIZILIR (SRI'li Chart.js yuklendi) |

---

## CC HATALARI (SEKIZ + sayi hatalari)

1. **Kacak `<script>` etiketi** — K9 yorumunu degistirirken `admin.html`e fazladan bir
   `<script>` birakildi. Fark edilip duzeltildi.
2. **Bes pin capasi EZBERDEN yazildi (MK-7 ihlali)** — `createElement("button")` vs
   `'button'`, `resolveUrl`un SINIF METODU olmasi, kilit cagrisinin IKI SATIRA bolunmus
   olmasi. Ham kaynaktan alinip duzeltildi.
3. **CAPA KIRLENMESI 5. ve 6. VAKA** — duzeltme yorumlari taranan dizgeleri METIN olarak
   tasidi. Bu kez yeniden yazmakla YETINILMEDI: **YAPISAL COZUM** konuldu (`KodSatirlari`
   yorum siyirici). Denetci olctu: siyirici olmasa IKI PIN yanlis kirmizi verirdi.
4. **Siyiricinin ilk hali kod yedi** — regex icindeki `\/\//` ve `"//"` dize literalini
   yorum sandi. Guard genisletildi (`:` ve `\` oncesi kesme YOK) ve POZ assertler HAM
   govdeye tasindi; sinir testin ICINE yazildi.
5. **URETIM KODUNDA CURUYEN IDDIA** (B1, yukarida) — "olculen" etiketli bir gerekce
    olcumle curudu.
6. **KOK-6 KOMSU SATIRI DUZENLERKEN KACIRILDI** (B2) — `cmp_color` duzeltilirken bir ust
   satirdaki `cmp_cat` gorulmedi.
7. **WORKTREE, DENETCI HALA KOSARKEN SOKULDU** — L3 ilk raporunu vermisti ama BITMEMISTI;
   `gf2a-wt/d1` altindan kayboldu ve ICINE yazdigi rapor gitti. **Sonuc etkilenmedi**
   (butun olcumler sokumden ONCE tamamlanmisti, kanit loglari scratchpad'de sag kaldi);
   kaybedilen tek sey servis edilen dosyalarin md5 kimliginin IKINCI kez dogrulanmasiydi.
   **Kok sebep:** "denetci raporunu verdi" ile "denetci BITTI" AYNI SEY DEGIL.
   Rapor kurtarildi: `scratchpad/DENETIM-L3.md` 32.495 B / 604 satir.
   Sir kontrolu yapildi: `TokenOptions` 4 gecis, DORDU DE ANLATIM, DEGER YOK; uc uzun
   base64 dizgesi commit SHA, Chart.js SRI hash'i ve denetcinin AYIRT EDICI DENEY icin
   KASTEN urettigi YANLIS hash - ucu de SIR DEGIL.
8. **GERCEK BULGU AZ KALSIN CURUTULDU** (B3/F1) — `forEach(it,k)` imzasina bakilip "k dizi
   indeksidir" denilerek elendi; `cart` bir **Map** oldugu icin `k` ANAHTARDI. Eleme
   gerekcesi OLCUME DEGIL IMZAYA dayaniyordu. Ayrica F1'in `replace_all`i BESINCI bir yeri
   (`:2367`, sabit sozluk anahtarlari) de degistirdi - KAPSAM DISI oldugu icin geri alindi.

**SAYI HATALARI (commit mesajlari DEGISTIRILMEDI - denetciler o SHA'lar uzerinde kostu,
MK-4 amend YOK; kayit burada):** "UC on-`esc()`" -> 4 · "ALTI sink" -> 7 ·
"`caches.open(CACHE).put` dort yerde" -> 4 `open` / 3 `put` · "depodaki TEK uzak script" ->
gtag da var · "backend'le AYNI KUMEYI kabul eder" -> karakter sinifi ayni, uzunluk kumesi
bilincli daha genis.

---

## KURGU ENVANTERI

**GF-2a HICBIR KAYIT URETMEDI — OLCULDU.** Tarifteki `GF2A-XSS <img src=x onerror=alert(1)>`
urunu ve `gf2a.<n>@example.com` hesabi **ACILMADI**; olcumler tarayicida SENTETIK girdilerle
yapildi ve DB'ye YAZILMADI.

```
SELECT COUNT(*) FROM customers WHERE email LIKE 'gf2a%';                        -> 0
SELECT COUNT(*) FROM products WHERE name LIKE '%GF2A%' COLLATE Latin1_General_BIN2; -> 0
SELECT MAX(id), COUNT(*) FROM customers;                                        -> 169 / 149
SELECT MAX(id), COUNT(*) FROM products;                                         -> 955 / 35
SELECT MAX(id) FROM orders;                                                     -> 286
SELECT COUNT(*) FROM user_sessions;                                             -> 342
SELECT COUNT(*), SUM(CAST(id AS bigint)) FROM orders WHERE status=0 AND id<=210; -> 35 / 3837
```
MK-3 uclusu ve tum MAX'lar `45·GUVENLIK-FIX-1b` kapanisiyla **BIREBIR**.

---

## OLCUMLER

**UC ARDISIK TAM DOGRULAMA BIREBIR** (`1dd985b`, seri, worktree'siz):
`Category=Sql` **378/378** · tam suit **651/654** · kirilan listeler uc turda `diff` ile ayni
(bilinen Docker uclusu, kok sebep ham ciktida `Docker is either not running`).
Release build **0 Hata** · `dotnet format whitespace` exit **0** · `style` exit **0** ·
degisen her dosya CR 0 / TAB 0 / sonda-bosluk 0.
Test envanteri `2a74cbd` 641 -> `1dd985b` **651** (+10, KAYBOLAN 0).

**PUSH:** `2a74cbd..1dd985b` · run `33695617786` (CI - Build & Test) + `33695617783`
(Security CI) — ikisi de `completed/success`, **basarisiz ADIM 0**, alti job'da
**failure-annotation 0**. `format-check` ve `secret-scan` ADIM SONUCUNDAN okundu.

---

## RIG NOTU

`goz1` statik sunucusu `curl -I` (HEAD) istegini KALDIRAMIYOR: baglantiyi resetliyor ve ayni
zincirdeki sonraki istek de baglanamiyor. **Sonraki turlarda `curl -I` KULLANILMAZ.**
