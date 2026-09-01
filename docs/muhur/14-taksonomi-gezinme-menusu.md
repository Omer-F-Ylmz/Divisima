# TAKSONOMI - GEZINME MENUSU VERITABANINDAN URETILIR (launch oncesi kucuk is)

D3'un "gezinme taksonomisi veritabanindan uretilmiyor" bulgusu, kullanici karariyla
**gercek katalog aktarimindan ONCE** kapatildi.

## OLCULEN ONCE-DURUM

```
index.html NAV (SABIT) : yeni · elbise · ust · alt · dis · aksesuar · indirim
veritabani slug'lari   : elbise · e4a-kategori          <- KESISIM: yalniz "elbise"
index.html:2015        : if(!CAT_INFO[cat]&&!navBySlug[cat])cat='tumu';   <- SESSIZ YENIDEN YAZIM
```

Iki yonlu zarar: (a) veritabaninda VAR olan ama navda olmayan kategoriye ROTA YOKTU -
`#/kategori/d3olcek-3` **sessizce `#/kategori/tumu`ya yeniden yaziliyordu**; (b) navda VAR
ama veritabaninda OLMAYAN kategori (`ust`/`alt`/`aksesuar`) "gecerli" sayilip **BOS bir
kategori sayfasi** ciziyordu. Gercek katalog aktarildiginda (a) HER kategori icin gecerli
olacakti - musteri aktarilan hicbir kategoriye gezinerek ulasamazdi.

## YAPILAN (hepsi `frontend/api-bridge.js`; index.html'e DOKUNULMADI)

**1) MENU SUNUCUDAN.** `menuyuVeritabanindanKur()` - `NAV` / `navBySlug` / `CAT_INFO` /
`MAINS` kategori ucunun yanitindan YENIDEN KURULUR (uzerine eklenmez; eklenseydi eski
slug'lar "gecerli" kalirdi). Sonra `renderNav` + `renderMob` + `renderPills` tekrar cizilir.

**EK ISTEK YOK - OLCULDU:** `/api/category/getlist` ZATEN ilk yuklemede cagriliyor; menu AYNI
yanittan uretiliyor. Ilk yukleme **2 istek** (once de 2'ydi).

**2) TANINMAYAN ROTA 404'E DUSER.** `showCategory` sarmalandi; gecerlilik `navBySlug` +
sentetik gorunumlerden hesaplanir, degilse uygulamanin KENDI `show404()`'u cagrilir.
`setDocTitle` de sarmalandi - router basligi `showCategory`DEN SONRA yazdigi icin 404'te
"Sayfa Bulunamadi" olmaliydi.

**3) ILK YUKLEME YARISI KAPATILDI - OLCUMLE.** Sarmalayicilar asenkron kategori yuklemesinden
sonra baglaniyor; `defer` yuzunden index.html'in satir ici router'i DAHA ONCE kosuyor ve
adresi yeniden yaziyor. Yani sarmalayici baglandiginda "taninmayan rota" bilgisi KAYBOLMUS
oluyordu. Olculdu:

```
navigation.name -> ".../index.html?v=...#/kategori/olmayan"   (ORIJINAL)
location.href   -> ".../index.html?v=...#/kategori/tumu"      (YENIDEN YAZILMIS)
```

Kaynak `location.hash` DEGIL **gezinme kaydinin adresi** secildi - o, belge hangi adresle
getirildiyse onu tasir. `defer`i kaldirmak da bir cozumdu ama Dalga 3'un olcumle kazandigi
"render-bloklayan kaynak 5 -> 0" iyilesmesini geri alirdi.

**4) 404 SAYFASININ KATEGORI SATIRI.** `show404` sarmalandi: "populer kategoriler" satiri
gercek kategorilerden uretilir. **Bu bir OLCUM BULGUSUDUR:** kategori yokken o satir SABIT
bes slug tasiyordu ve hepsi artik 404'e dusuyordu - yani 404 sayfasi kullaniciyi BASKA BIR
404'e gonderiyordu. Kategori yoksa HER ZAMAN GECERLI olan sentetik gorunumlere dusuyor.

### ALT KATEGORILER - OLCULDU, UYDURULMADI

`CategoryResponseDto` **ZATEN** `sub_categories` tasiyor ve `CategoryManager.GetList` onu
dolduruyor; `sub_categories` tablosu BOS ve onlar icin AYRI BIR UC YOK. Yani sozlesme MEVCUT.
Gecici olarak iki alt kategori eklenip **canli olculdu**: mega menu kendiliginden cizildi
(`#/kategori/elbise/taksonomi-abiye` calisti, 404 YOK), satirlar silinince menu eski haline
dondu. Uydurma bir alt-kategori kaynagi EKLENMEDI.

### YEDEK DAVRANIS - MENU BOS GORUNMEZ (olculdu ve gerekcelendirildi)

`tumu` / `yeni` / `indirim` **VERITABANI KATEGORISI DEGILDIR** - bellekteki urunler uzerinden
turetilen ISTEMCI TARAFI GORUNUMLERDIR. Bu yuzden yedek, uydurma bir liste degil; zaten
DB'ye bagli olmayan gorunumlerdir. Iki kategori de `is_active=0` yapilip **canli olculdu**:

```
menu           : Yeni Gelenler · İndirim      (BOS DEGIL)
ana sayfa pill : Tümü
#/kategori/tumu -> 6 kart · #/kategori/yeni -> 6 kart   (gorunumler GERCEKTEN calisiyor)
404 kategori satiri -> Tümü / Yeni Gelenler / İndirim  (hicbiri OLU DEGIL)
```

## OLCUM - ONCE / SONRA

```
                                ONCE                          SONRA
ilk yukleme API istegi          2                             2          (DEGISMEDI)
menu kaynagi                    SABIT dizi (index.html)       /api/category/getlist
menude gorunen                  yeni/elbise/ust/alt/dis/...   Yeni Gelenler · E4a Kategori ·
                                                              Elbise · İndirim
#/kategori/elbise (DB'de VAR)   calisir                       calisir, 1 filter istegi
#/kategori/ust (DB'de YOK)      BOS kategori sayfasi          404 + "Sayfa Bulunamadı"
#/kategori/olmayan              sessizce -> #/kategori/tumu   404, ADRES KORUNUR
dogrudan acilan bilinmeyen rota sessizce -> tumu              404 (yaris kapatildi)
alt kategori (DB'de varsa)      -                             mega menude KENDILIGINDEN
```

## PINLER

**Davranis (SUNUCU, `StorefrontCatalogContractTests`e EKLENDI - yeni veritabani ACILMADI):**
- `KategoriUcu_MENUNUN_DAYANDIGI_ALANLARI_Doner` - `slug` / `name` / `sub_categories`
  alanlari sozlesmede olmali (vakum kirici: liste gercekten dolu olmali).

**Kaynak sozlesmesi (ISTEMCI, `KatalogSayfalamaSozlesmeTests`):**
- `MENU_VERITABANINDAN_URETILIR_SABIT_TAKSONOMI_KULLANILMAZ` - `NAV`/`CAT_INFO`/`MAINS`
  yeniden kurulur, uc cizici tekrar cagrilir, **kategori ucu TEK KEZ cagrilir** (ek istek
  yasagi) ve fonksiyonlar yalniz TANIMLI degil CAGRILMIS da olmali.
- `TANINMAYAN_ROTA_SESSIZCE_YENIDEN_YAZILMAZ_404E_DUSER` (cift-anlam kirici: sentetik
  gorunumler GECERLI kalmali - "her seyi 404'e dusur" yanlis duzeltmedir)
- `KATEGORI_YOKSA_MENU_BOS_GORUNMEZ`
- `ALT_KATEGORILER_SUNUCUDAN_GELIR_UYDURULMAZ` (cift-anlam kirici: sabit alt slug'lar
  istemciye KOPYALANMAMIS olmali)

**KIRILAN PIN YOK.** Pin siniri Dalga 4 / Dalga A ile ayni: JS/DOM kosucusu yok, istemci
tarafi kaynak sozlesmesiyle tutuluyor; davranis kaniti yukaridaki tarayici olcumleridir.

## DIS KONTROLU + 5. KONTROL

**DIS:** 6 assert ters -> **5 AYRI ISIMLI KIRMIZI** (iki flip ayni teste dustu; >=3 sarti
saglandi ve BES yeni pinin hepsi kirmizi oldu). Geri alindi, 16/16 yesil.

**5. KONTROL - DORT URETIM MUTASYONU:**

| Mutasyon | Kirilan pin | Uretilen once-durum |
|---|---|---|
| M1 404 satiri kategori yokken SABIT slug'lara duser | `KATEGORI_YOKSA_MENU_BOS_GORUNMEZ` | 404 -> yine 404 (olu baglantilar) |
| M2 `show404()` cagrisi kaldirildi | `TANINMAYAN_ROTA_..._404E_DUSER` | sessiz `tumu` yeniden yazimi |
| M3 alt kategoriler kaynakta sabitlendi | `ALT_KATEGORILER_..._UYDURULMAZ` | uydurma alt menu |
| M4 `init`ten `menuyuVeritabanindanKur()` cagrisi kaldirildi | `MENU_VERITABANINDAN_URETILIR_...` | menu sabit taksonomiye doner |

Dordunde de TAM 1 pin kirmizi (lokalize). Geri alindi; `[MUTASYON]` izi **0 dosya**.

**M4 BIR PIN BOSLUGU ACTI ve KAPATILDI:** ilk halinde pinler fonksiyonun VAR OLDUGUNU
olcuyordu, CAGRILDIGINI degil - cagriyi kaldiran mutasyon HICBIR pini kirmiyordu ve menu
sessizce sabit taksonomiye donuyordu. "Tanim + cagri = en az iki gecis" asserti eklendi ve
mutasyon TEKRARLANARAK kirmizi oldugu dogrulandi.
**DURUST KAYIT:** M4'un ILK denemesi de kirmizi vermedi - ama sebebi pin degil MUTASYONUN
KENDISIYDI (cagri yerine konan yorum fonksiyon adini HALA iceriyordu, yani sayim degismedi).
Yeni kuralin (c) adimi geregi once bu ihtimal elendi, mutasyon duzeltildi, sonra sonuc yazildi.

## YEREL DOGRULAMA

316/316 `Category=Sql` · tam suitte **508 basarili / 511** (kirilan 3'un UCU DE Docker'li
`OrderEndpointTests`) · Release 0 hata · whitespace + style **exit 0**.

---

