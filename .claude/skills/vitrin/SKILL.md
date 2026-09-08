---
name: vitrin
description: Divisima on yuz sozlesmeleri — index.html / admin.html / api-bridge.js / api-client.js / service-worker.js icin esc-resolveUrl-guvenliHTML politikasi, SOZLUK DOKUNULMAZ kurali, SW VERSION bumpi, tema token cifti ve Basic Auth muafiyet listesi. frontend/ altinda tek satir bile degistiren HER iste yuklenir (metin, placeholder, renk, gorsel URL'i, yeni ekran, i18n anahtari dahil) — bu dosyadaki kurallarin hepsi bir kez CANLIDA kirildi ve hicbirini test yakalamadi.
---

# vitrin — on yuz sozlesmeleri

**Iliski:** `surec` ve `sdp` olcum/push disiplinini verir, bu skill onlari TEKRAR ETMEZ.
Gorsel/tasarim kararlari `frontend-craft`in isidir — CLAUDE.md geregi UI'a dokunan her iste
o da yuklenir. Burada yalniz **degistirilirse sessizce kirilan sozlesmeler** vardir.

**Rig kor noktasi (once bunu bil):** JS/DOM kosucusu YOKTUR. Tarayici semantigi CI'da
pinlenemez; frontend pinleri **kaynak-sozlesmesidir** ve yalniz metin tarar. Bu yuzden
(a) her kaynak-sozlesme pini **mutasyonla** sinanir, (b) tarama **yorumlari ayiklanmis**
metin uzerinde yapilir — aranan dizge kendi aciklayici yorumunda gecerse assert bedava
dogru olur ya da yanlis ateslenir (ikisi de yasandi).

## 1. Uc dosya, uc bagimsiz yukleme

`index.html` + `api-client.js` + `api-bridge.js` vitrindir; `admin.html` + `api-client.js` +
`admin.js` paneldir. **Panel `api-bridge.js`i YUKLEMEZ.** Bu yuzden `esc`, `guvenliHTML` ve
`guvenliYaz` uc dosyada birden tanimlidir ve bu **mukerrer degil zorunludur**. Kural: bir
politikayi degistiriyorsan **hepsini** degistir; politikayi yalniz birinde degistirmek iki
farkli guvenlik davranisi uretir.

`guvenliYaz` bilincli olarak ayrisir: panel surumu 2 parametreli ve fail-closed dalinda
`textContent` kullanir (bridge surumu 3 parametreli ve `innerHTML`). **Politika ozdestir,
ayrisan sey cizim yardimcisidir ve panel surumu daha dardir** — esitlemeye calisma.

## 2. URL politikasi tek yerdedir

`DivisimaAPI.resolveUrl` (`api-client.js`) **alti cagri yolunun tek ortak noktasidir**.
Politikayi render katmanina tasimak alti kopya acar — "ayni kuralin ikinci kopyasi" bu depoda
yedi kez bedeli odenmis ailedir.

Kabul: mutlak `http(s)` · dar `data:` (**yalniz raster goruntu + `;base64`**) · goreli yol
(API tabanina cozulur). Red: `data:image/svg+xml` (script tasir) · protokol-goreli `//` ·
sema tasiyan her sey. **Red = BOS DIZGE**, atma ya da `null` degil: cagiranlar donen degeri
dogrudan `src`e koyar, bos dizge kirik-gorsel gosterir ve akisi DUSURMEZ.

**BILINEN (kapatilmadi):** `admin.js` kendi `imgUrl()` kopyasini tasir ve yalniz mutlak-http
kontrolu yapar; sema reddi YOKTUR. Panele yeni bir gorsel yuzeyi eklerken **o kopyayi
genisletme** — ayni politikaya bagla ya da bulguyu raporla.

## 3. HTML enjeksiyonu: iki katman, fail-closed

Yazma katmani sunucudadir; `guvenliHTML` **ikinci kalkandir** — depoda icerik bir sekilde
kirli kalsa bile (eski kayit, dogrudan SQL, baska yazma yolu) tarayicida CALISMAMASINI
saglar. `innerHTML`e giden HER dis kaynakli HTML buradan gecer.

DOMPurify yuklenmemisse **`null` doner ve HTML ENJEKTE EDILMEZ.** Sessizce ham HTML basmak
iki katmanli savunmayi tek katmana indirirdi. DOMPurify `/vendor/purify.min.js`ten ve
**bridge'ten ONCE** yuklenir; script sirasini degistirme.

Sunucudan gelmeyen, sablon literaliyle kurulan her metin `esc()`ten gecer. Bir yerde
`innerHTML` goruyorsan sorun: **icerideki her dis deger esc'li mi, yoksa govde
`guvenliHTML`den mi geliyor?** Ucuncu bir yol yoktur.

## 4. SOZLUK DOKUNULMAZ

Sozluk `index.html` icindeki `T` (TR/EN cifti) ve `AR` nesneleridir; `t(k)` onlari okur,
`applyI18n` `data-i18n` / `data-i18n-ph` tasiyan dugumleri gezer, `api-bridge.js`teki
`ceviri(anahtar, yedek)` ayni mekanizmaya `window.t` uzerinden baglanir.

- **Merkez karari: sozluge DOKUNULMAZ.** Yeni metin gerekiyorsa sabit TR yazilir ve i18n
  karsiligi **VITRIN-KALAN**'a kaydedilir. Bugun alti metin bu durumda (dogrulama kutusu).
- **Yeni anahtar eklemeden once ANKRAJLI mukerrer taramasi** yapilir. JS'te son tanim kazanir;
  ayni adi tasiyan eski bir anahtar ekranda **mevcut metni** cikarir ve degisiklik "olmamis"
  gorunur. Ciplak alt-dizge sayimi yetmez — `X:` deseni `onek_X:` icinde de esler.
- **`ceviri(...)` dizge ICINDE cagrilmaz.** `placeholder=ceviri("...")` bicimi tirnaksiz
  yazildigi icin fonksiyon **hic calismaz** ve kullanici ham metni gorur; on dort yerde
  yasandi. Dogrusu `placeholder="${esc(ceviri("..."))}"` kalibidir.
- Kullaniciya gosterilen hata metni **sunucudan** gelir; istemci kendi metnini uydurmaz —
  ikinci bir dogruluk kaynagi acmak, iki taraf ayrisinca yalan bir ekran uretir.

**Cerceve taramasi (MK-1):** bir fonksiyon/blok **sokuyorsan** giris noktalarinda tanimsiz
fonksiyon taramasi zorunludur: `applyI18n` · `setLang` · `setCur` · `refreshPrices` ·
`setTheme`. Bir kez bir sokum iki komsu fonksiyonu goturdu ve **dil degistirme bozuldu**;
hicbir pin yakalamadi.

## 5. Tema token'lari — cift palet kurali

Renkler `:root` (acik) + `html[data-theme="dark"]` (koyu) + `html[data-contrast="high"]`
katmanlarindan gelir. Iki kural:

1. **Bir token secerken HER IKI paleti de oku.** `--btn` ve `--ink` acik temada AYNI degerdir;
   koyu temada dogru olan bir eslesme acik temada **gorunmez metin** uretir. Bir kez tam bunun
   aynasi yasandi: koyu temadaki kusur duzeltilirken acik temaya ayni kusur yaziliyordu.
2. **Kutuya arka plan veriyorsan metin rengini de ver.** Sabit acik renk (`#fff`, `#faf8f5`)
   yazilip metin rengi verilmeyen kutular koyu temada `--ink`i miras alir ve **beyaza yakin
   metin beyaza yakin kutuda** kalir. Sabit renk yerine sayfanin kendi token'i kullanilir.

Tema secimi `localStorage`da (`dvs_theme`) ve `<head>`teki senkron betikle **ilk boyamadan
once** uygulanir; dil (`dvs_lang`) ayni betikte `dir`/`lang` yazar. O betigi asagi tasima —
flash uretir.

## 6. Service worker

`VERSION` sabiti tek kaynaktir; `CACHE` ve `API_CACHE` adlari ondan turer, `activate` eski
onbellekleri **secerek** siler. Iki kova bilinclidir: `/api/` **network-only**, cikista yalniz
API kovasi silinir (kabuk kovasi silinseydi cevrimdisi acilis da giderdi).

**Bump kurali:** `kodTasiyorMu` dali navigasyonu ve `.html`/`.js`yi **network-first** yapar,
yani bump unutulsa bile kod tasiyan dosyalarin yeni surumu gelir. Risk **cache-first**
varliklardadir. Kural: `manifest.json`, ikonlar ya da fontlardan biri degistiyse **bump
ZORUNLU**; yalniz `.js`/`.html` degistiyse atlanabilir ve **atlandigi raporda YAZILIR**.

`register()` cagrisinin donmesi **SW KAYDI DEMEK DEGILDIR**; kanit `getRegistrations` +
`active` + controller uclusudur.

## 7. Dagitim yuzeyi

- **API tabani TEK KAYNAKTAN gelir:** `meta[name="divisima-api-origin"]`. `api-bridge.js`te
  sessiz bir `localhost` yedegi YOKTUR — bos taban gorunur sekilde bozuktur, sessiz yanlis
  taban degildir (yanlis taban istekleri kullanicinin KENDI makinesine yollar).
- Origin degisikligi **elle yapilmaz**: `ops/set-api-origin.sh` tek girdiden hem meta'yi hem
  CSP'nin `img-src`/`connect-src`/`form-action` direktiflerini yazar; `--verify` exit 0.
  Dagitim sirasi ve gerekcesi `ops` skill'inde.
- **API 0 urun donerse mock'a DUSULMEZ** — acik bir "katalog bos" durumu gosterilir. Yalan
  veri gostermek bos vitrin gostermekten kotudur.
- **Basic Auth muafiyet listesi** (soft-launch kapisi acikken): `manifest.json` ·
  `service-worker.js` · `pwa-register.js` · `robots.txt` · `/icons/` · `favicon*`.
  **`index.html` MUAF DEGILDIR.** Gerekce ve kapinin kendisi `ops` skill'inde.
- Panel CSP'si `'unsafe-inline'` **tasimaz**; vitrinin `'unsafe-inline'`i **kabul edilmis
  risktir**. Panele satir ici script ekleme — CSP'yi gevsetmek gerekir ve o karar merkezindir.

## 8. Kultur

Bicimlenmis sayi/tarih dizgesi teste **elle yazilmaz** (yerel `tr-TR`, kosucu invariant).
Kimlik dizgesinde (kupon/hediye kodu, e-posta, URL yolu) kulturlu casing YASAK; kultur yalniz
insan-gorunur bicimlendirmede kullanilir. Ayrintili gerekce CLAUDE.md bolum 6 ve 6c'de.
