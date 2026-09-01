# DALGA D - D3 (GERCEK OLCEK PROVASI) - YALNIZ OLCUM

Kod DEGISMEDI (`git status` temiz). Olcum dev veritabaninda yapildi, seed sonunda TAMAMEN
silindi ve silindigi OLCUMLE kanitlandi.

## OLCUM DUZENEGI ve SINIRLARI (once yazildi)

- **k6 BU MAKINEDE YOK** (olculdu). `ops/load-test/k6-smoke.js` kosulamadi; yuk turu
  **"OLCULMEDI -> staging"** olarak kaydedilir - D5'in canli Redis kalemiyle AYNI RAFTA.
  Yerine elle harness: 30 tekrarli HTTP + `Stopwatch` (p50/p95) + yanit boyutu.
- **Sorgu sayimi EF komut logundan.** `appsettings.json` `Microsoft.EntityFrameworkCore`i
  `Warning`e kisiyor; olcum icin ortam degiskeniyle acildi
  (`Serilog__MinimumLevel__Override__Microsoft.EntityFrameworkCore.Database.Command=Information`).
  Sayim, log dosyasinin BAYT OFSETI isaretlenip aradaki `Executed DbCommand` satirlari
  sayilarak yapildi (mutlak sayim acilis tohumlamasini da katardi).
- **RATE LIMIT OLCUM ICIN YUKSELTILDI** (`RateLimit__GlobalPermitLimit=100000` vb.). 30
  tekrarli tur global kovayi (100/dk) yakiyordu ve ilk turda **429** alindi. Olculen sey rate
  limit DEGIL; bu bir olcum artefaktidir ve bilincli olarak kayitta. Yan kazanc: D5'te
  merkezilestirilen `RateLimitPolitikasi` sayesinde tek ayar iki yolu da etkiledi.
- **Arka plan isleri kapatildi** (`BackgroundJobs__Enabled=false`) - dakikalik outbox isi
  sorgu sayimini kirletmesin. Bu, ayni dalgada eklenen bayragin ilk pratik kullanimi.
- **GORSEL URETILMEDI** (kullanici sarti): D1 az once temizlendi, tekrar kirletilmedi.
  Kosum sonrasi `Divisima.API/wwwroot/uploads/products` = **0 dosya**.

## SEED (isaretli, geri alinabilir)

Marker: `products.brand='D3OLCEK'`, `categories.slug LIKE 'd3olcek-%'`,
`orders.order_number LIKE 'D3OLCEK-%'`.

```
kategori 8 · urun 400 (toplam katalog 403) · stok satiri 1400
stoksuz urun 40 (%10) · indirimli urun 100 (%25) · beden/urun 2..5 (XS..XL)
siparis 40 · siparis kalemi 80   (siparis toplamlari KALEMLERDEN turetildi)
```

## ONCE / SONRA (30 tekrar, ayni makine, ayni surec)

```
UC              ONCE (3 urun)                          SONRA (403 urun / 1400 stok / 40 siparis)
                sorgu  p50      p95      bayt          sorgu  p50      p95      bayt
filter s=1        -      -        -        -             4,0   20,1ms   23,4ms      417
filter s=24     4,0   23,5ms   26,7ms      927           4,0   28,6ms   30,6ms    6.778
filter s=60     4,0   25,5ms   31,3ms      927           4,0   42,7ms   47,7ms   16.763
search          1,0   19,4ms   20,9ms      606           1,0   19,7ms   22,8ms    6.017
admin getlist   3,0   23,6ms   26,2ms      928           3,0   43,6ms   46,5ms   27.848
dashboard       3,0   22,7ms   27,5ms      173           3,0   22,8ms   27,0ms      177
my-orders       1,0   18,1ms   20,6ms       74           1,0   18,4ms   20,7ms    5.136
```

**DALGA 3'UN YAPI PINLERI OLCEKTE DE TUTUYOR - SORGU SAYISI SATIR SAYISINDAN BAGIMSIZ.**
`filter` size=1/24/60 -> **4/4/4** (403 urunle); `my-orders` 0 sipariste 1 sorgu, 40
sipariste **yine 1**; `admin getlist` 3; `dashboard` 3. Liste uclari kalem basina ek sorgu
ATMIYOR - "N+1 yok" iddiasi 3 uruncuk bir veride degil, 403 uruncuk bir veride de gecerli.

**SURE PAYLOAD'A BAGLI, KATALOG BOYUTUNA DEGIL:** en net kanit `filter s=1` -> 403 urunluk
katalogda **20,1 ms** (3 urunlukteki s=24 ile ayni buyukluk). Buyuyen tek sey donen govde.

## EKSIK INDEKS - DALGA 3'UN ACIK SORUSU HALA ACIK (durust kayit)

Dalga 3 sunu yazmisti: *"Eksik indeks onerisi: SIFIR (sinir: DMV gercek planlardan beslenir,
62 uruncuk veride SQL Server hicbir indeksi onermeye deger bulmamis olabilir)."*
403 urunle tekrar olculdu:

```
sys.dm_db_missing_index_details (bu DB)        -> 0 oneri
sys.dm_db_missing_index_details (TUM DB'ler)   -> 0 oneri
SQL Server acilisi 12:32 (saatlerdir ayakta, DMV sifirlanmis degil)
```

**"0 ONERI" BURADA KANIT DEGIL - VE BUNU OLCTUM.** DMV'nin canli oldugunu gostermek icin
KASITLI olarak indekssiz esitlik sorgulari kosuldu (`products.color_hex`,
`product_stocks.reserved_quantity`, iki tablolu join) -> **YINE 0 oneri**. Yani bu veri
hacminde SQL Server, indekssiz bir tarama icin bile oneri URETMIYOR; dolayisiyla "oneri yok"
ile "indeks gerekmiyor" AYNI SEY DEGIL.

**SEBEP OLCULDU** (`sys.dm_exec_query_stats`): uc sorgularinin tamami **kosum basina 10-18
mantiksal okuma** yapiyor. 403 satirlik `products` ve 1407 satirlik `product_stocks` yalnizca
birkac sayfa; tam tarama zaten ~18 sayfa okumak demek ve hicbir indeks bunu yenemez.

**SONUC: esik 400 urunun COK USTUNDE.** Dalga 3'un kalemi kapanmadi, yalnizca SINIRI
KESINLESTI. Korlemesine indeks EKLENMEDI (kullanici sarti).

## STOREFRONT GERCEK HACIMDE - YENI BULGU (ISLEV-KIRAN, DUZELTILMEDI)

Temiz sayfa yuklemesi (arama yapmadan, tarayicida olculdu):

```
ilk yukleme API istegi : 2   (/api/category/getlist + /api/product/filter)
bellege giren urun     : 24     <- VERITABANINDA 403
"Daha Fazla Yukle"     : ana sayfada YOK (0 tiklama)
kategori rotalari      : 0 EK ISTEK  (#/kategori/yeni, /elbise, /elbise/gunluk, /elbise/abiye)
kategori dagilimi      : 8 kategorinin HER BIRINDE 3 urun   <- DB'de her birinde ~50
sayfa agirligi         : 173 KB (7 kaynak)
```

**KOK SEBEP KAYNAKTA DOGRULANDI** (`frontend/api-bridge.js:211` `loadCatalog`):
`{ page: 1, size: CATALOG_PAGE_SIZE }` - `CATALOG_PAGE_SIZE = 24`, **sayfa 2 HIC istenmiyor**
ve `replaceProducts(mapped)` bellekteki katalogu bu 24 urunle DEGISTIRIYOR. Kategori
sayfalari, filtreler ve "Daha Fazla Yukle" hep bu 24 urun uzerinde ISTEMCI TARAFINDA calisiyor.

**URETIMDEKI ANLAMI:** gercek bir katalogla musteri, urunlerin **yalnizca ilk 24'unu**
gezebilir; kalan **379'una (%94) gezinerek ULASILAMAZ**. Tek kacis yolu arama - o GERCEKTEN
API'ye gidiyor (`/api/search/products`, 1 istek).

**3 URUNLUK VERIDE GORUNMEZDI** - D3'un varlik sebebi tam olarak budur.

**DUZELTILMEDI** (ev kurali: kapsam disi bulgu duzeltilmez, karar kullanicinindir).
Aday cozumler: (i) `loadCatalog`a gercek sayfalama baglamak ("Daha Fazla Yukle" bittiginde
sonraki sayfayi API'den cekmek), (ii) kategori rotasinin `category_id` ile SUNUCUYA filtre
gondermesi (bugun istemci tarafinda suzuyor), (iii) sonsuz kaydirma. Ucu de storefront isi;
backend ZATEN sayfali (`total_count`/`total_pages` donuyor, Dalga 3'te eklendi).

## TEMIZLIK - KANITLI

**FK SILME SIRASINI GERCEKTEN DAYATIYOR (canli kanit):** urunler stoklardan ONCE silinmeye
calisildi -> **SqlException 547**, kisit adi `FK_product_stocks_product_id` (D2'de eklenen FK).
Yani D2'nin koydugu koruma canli calisiyor.

Dogru sirayla silindi ve zemin BIREBIR geri geldi:

```
silinen: order_items 80 · orders 40 · product_stocks 1400 · products 400 · categories 8
SONRA  : products 3 (zemin 3) · product_stocks 7 (zemin 7) · categories 2 (zemin 2)
         orders 54 (zemin 54)
ARTIK  : D3OLCEK urun 0 · d3olcek kategori 0 · D3OLCEK siparis 0
YETIM  : yetim stok satiri 0 · yetim siparis kalemi 0
DEPO   : git status TEMIZ · D3OLCEK/d3_seed/statik.ps1 izi 0 dosya
GORSEL : wwwroot/uploads/products 0 dosya (D1 temizligi korundu)
PORT   : 5000 ve 5173 BOS (iki sunucu da durduruldu)
```

**IKI OLCUM HESABI SILINMEDI - BILINCLI:** `d3.admin.*` / `d3.musteri.*` hesaplarinin
**6 riza kaydi** var ve `consent_records`ta FK YOK (bkz. D-SEMA karari). Silmek, bakim
migration'larimizin IKI KEZ yaptigi hatayi - yetim riza kaydi uretmeyi - tekrarlardi.
Ustelik uretimin kendi yolu hesap silme degil ANONIMLESTIRMEDIR. Hesaplar dev veritabaninda
duran diger onlarca test hesabiyla ayni statude birakildi.

