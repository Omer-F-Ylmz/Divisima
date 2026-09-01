# D-SEMA (YALNIZ OLCUM) ve D-SEMA-FIX - TEK DOGRULUK KAYNAGI EF MIGRATIONS

Dalga D'nin D2 kaleminde acilan "44 FK farki" bulgusu, kullanici karariyla ONCE yalniz-olcum
turu (D-SEMA), sonra da duzeltme dalgasi (D-SEMA-FIX) olarak ele alindi. Dalga D'nin kalan
kalemleri (D1 uygulama, D3, D5, D6) bu karardan SONRAYA birakildi.

## D-SEMA - OLCUM

Dort saglama yolu AYNI sunucuda kuruldu, kataloglar `sys.*`'ten okundu, dordu de is bitince
DROP edildi. Dev veritabaninda YALNIZ `SELECT` kosuldu.

```
                    A(dokumandaki komut)  A2(dosyanin niyeti)  B(EF model)  C(EF migration)
  tablo                    44                   44                 45            45
  FK                       17                   54                  9             9
  indeks (PK haric)         6                   71                 75            75
```

- **B ile C BIT BIREBIR AYNI** - tablo/FK/kolon/indeks dordunde de fark satiri **0**.
  Dev `DivisimaDb` = B + 11 Hangfire tablosu + 2 Hangfire FK'si. EF'in iki yolu birbiriyle
  ve dev'le TAM MUTABIK; ayrisan taraf TEK: sema dosyasi.

### D-S1 (AGIR) DOKUMANDAKI KOMUT SESSIZCE SAKAT SEMA KURUYORDU

`database/README.md`'deki komutta `-b` YOKTU ve dosyada `GO` YOKTU. Satir 635'teki
`FK_orders_payment_id` tip uyumsuzlugundan patliyor (`Msg 1778` + `Msg 1750`) ve **BATCH'I
DUSURUYOR**; sonrasindaki **37 FK ve 65 indeks HIC olusmuyordu**. `sqlcmd` yine de **EXIT 0**
donuyordu - operator "basarili" goruyordu.

**DURUST SINIR:** is invarianti tasiyan BES UNIQUE indeksin BESI DE 635'ten ONCE beyan
edilmis, dolayisiyla OLUSUYORLARDI. Kaybedilen sey referans butunlugu ve sorgu indeksleriydi,
korumalar degil.

### D-S2 (AGIR, SESSIZ) KODLAMA BOZULMASI BIR SAVUNMAYI ETKISIZ KILIYORDU

Dosya UTF-8 BOM'SUZ; dokumandaki komutta `-f 65001` YOKTU. Dosyadaki ASCII disi **TEK SQL
satiri** (587) tam da kritik olandi:

```
repo    : WHERE reason = N'Referans ödülü (davet edilen)'
kurulan : ([reason]=N'Referans Ã¶dÃ¼lÃ¼ (davet edilen)')     <- BOZUK
EF (B)  : ([reason]=N'Referans ödülü (davet edilen)')       <- DOGRU
```

`UX_store_credit_referee_reward` VAR, adi dogru, UNIQUE gorunuyor - ama uygulamanin yazdigi
HICBIR satirla eslesmiyor. Sprint 8'in "davet edilen odulu musteri basina TEK" ikinci savunma
hatti, dosyadan kurulmus bir veritabaninda **GORUNMEZ SEKILDE YOKTU**.

### D-S3 PATLAYAN FK ANLAM OLARAK DA YANLISTI

`Order.payment_id` bir `string?` ve **Iyzico'nun PaymentId'sini** tutuyor
(`IyzicoPaymentManager.cs:361`); `payments` tablosuna FK DEGIL. Kok sebep
`database/generate_schema.py:66`: FK'lar **ADLANDIRMA KURALINDAN** (`<x>_id -> <x>s(id)`)
cikariliyordu, modelden degil.

### D-S4 DOSYA ARTIK URETILEN BIR CIKTI DEGILDI

`generate_schema.py` ILK COMMIT'ten beri hic degismemis (19 Agustos); `01_schema.sql` **BES
ayri commit'te ELLE** duzenlenmis. "Yeniden uret" calisan bir islem DEGILDI.

### D-S5 107 KOLON FARKI

| Sinif | Adet | Ornek |
|---|---|---|
| A daha GENIS (blanket `NVARCHAR(256)`) | 85 | `products.color_hex` A:256 / B:9 |
| **A daha DAR -> calisma ani TASMASI** | **20** | `customers.name` A:256 / B:MAX · `outbox_messages.error` A:256 / B:1000 |
| **TIP UYUMSUZ** | 1 | `customers.gender` A:`nvarchar(256)` / B:`int` |
| NULLABILITY | 1 | `invoice_items.product_name` A:NULL / B:NOTNULL |

Ayrica `sellers` tablosu + `products.seller_id` + `order_items.seller_id` dosyada YOKTU.

### D-S6 FK ADLANDIRMASI SISTEMATIK AYRISIYORDU

Ortak 9 iliskinin **8'inin adi farkliydi** (`FK_addresses_customer_id` <->
`FK_addresses_customers_customer_id`). Tek eslesen `FK_product_stocks_product_id` - cunku D2'de
BILEREK hizalanmisti. Iki yol ayni veritabanina uygulanirsa ayni kolonlarda **MUKERRER kisit**
olusurdu.

### HANGISI DOGRU - 54 ADAYIN TAMAMI GERCEK VERIYE KARSI TARANDI

```
IHLAL VAR               :  1
TEMIZ ve TABLO DOLU     : 35     (127 / 102 / 55 / 54 satirlik tablolar dahil)
TABLO BOS (kanit YOK)   : 18
```

Tek ihlal `FK_consent_records_customer_id` (89 satirin 6'si) ve izi surulebilir: altisi da
musteri **14 ve 15**'e ait - Dalga 1'in `EmailKanonikNormalizasyon` migration'inin sildigi iki
sondaj hesabi. Yani **120 yetim `product_stocks` ile AYNI SINIF**: uygulamanin degil, KENDI
BAKIM MIGRATION'IMIZIN urettigi yetim. Ikinci kez.

**45 fazla FK uygulamayi KIRMIYOR** (olculdu): uretim kodunda fiziksel silme YALNIZ yaprak
satirlarda (`wishlist_items`, `price_drop_subscriptions`, `product_images`,
`recently_viewed_products`, `stock_notification_requests`); `DataRetentionJob` yalniz
`user_sessions` / `outbox_messages` / `security_events` siliyor. Hesap silme = anonimlestirme,
urun silme = soft-delete. **Hicbir uretim yolu ebeveyn satir silmiyor.**

### RISK - OLCUMLERIMIZ HANGI YOLDAYDI

```
Testlerde EnsureCreated : 46 cagri      Testlerde Migrate : 0 cagri
```

294 SQL pini, Dalga 2 invariantlari, guvenlik dalgasinin silme/guncelleme testleri - **hepsi
B (EF MODEL) uzerinde** kostu. CI de ayni testler. **Sema dosyasi yolu hicbir test ya da CI
job'i tarafindan HIC kosulmadi.** Ayrica **model<->migration kaymasini tutan hicbir sey yoktu**
(olculdu: o gun senkrondu ama koruma YOKTU).

## D-SEMA-FIX - KULLANICI KARARI: SECENEK (a)

**Tek dogruluk kaynagi EF migrations.** Alti kalem uygulandi.

### (1) 01_schema.sql ARTIK URETILEN ARTEFAKT

`dotnet ef migrations script --idempotent` ciktisi + basinda "URETILMIS DOSYA - ELLE
DUZENLEMEYIN" baslik blogu (yeniden uretme komutu, uygulama komutu, `-b` ve `-f 65001`'in
gerekcesi). `generate_schema.py` **SILINDI** (D-S4).

`database/README.md` yeniden yazildi. **DURUST KAYIT:** `sqlite_schema.sql` de ayni (kaldirilan)
uretecten cikiyordu; MSSQL semasi EF'e tasindigi icin ikisi artik **esdeger DEGIL**. Dosya
oldugu gibi birakildi ama README'de "bu simulasyonlar artik semanin kaniti DEGILDIR" uyarisi
var - eski "ayni entity siniflarindan uretildigi icin esdegerdir" cumlesi YALAN olurdu.

### (2) 44 FK GERCEK MIGRATION'A TASINDI + 8 YENIDEN ADLANDIRMA

`20260824124039_ReferansButunluguTekMerkez`. Sonuc: **53 FK**, hepsi `NO_ACTION`, hepsi
`FK_<tablo>_<kolon>` KISA biciminde.

```
 9  zaten EF'te vardi -> yalniz ADI kisa bicime cekildi (8 yeniden adlandirma +
    D2'nin product_stocks FK'si, o zaten kisaydi)
28  veri kaniti VAR (cocuk tablo dolu, ihlal 0)
16  veri kaniti YOK (tablo bos) -> YAZMA YOLU OKUNARAK dogrulandi
 1  orders.payment_id       -> TASINMADI (D-S3: anlamsiz, tip de uyumsuz)
 1  consent_records.customer_id -> TASINMADI (kullanici karari, madde 3)
```

**16 "kanitsiz" kalem icin ne yapildi:** her birinin TEK yazicisi bulundu ve okundu. Hepsi
kimligi token'dan ya da dogrulanmis bir DTO'dan aliyor; sentinel (0) ya da dis sistem
referansi kullanan YOK. `ProductAttributeManager` ebeveyni ZATEN dogruluyor (404).
`DeviceController` `dto.customer_id`'yi token'dan EZIYOR. Tip uyumu ayrica olculdu (int -> int).

**ON KONTROL (Sprint 6 kalibi):** 44 iliskinin tamami taranir; yetim varsa **HICBIR SATIR
SILINMEDEN** `RAISERROR` - hangi kaydin dogru oldugu karari operatorundur.

### (3) consent_records: 6 YETIM SILINMEDI, FK KONMADI

Kullanici karari. Gerekce: KVKK'da riza kaydi, hesap silindikten sonra da "su kisi su tarihte
suna riza verdi" kaniti olarak saklanmasi GEREKEBILIR; silmek kaniti yok etmek olurdu.
Hukuki gorus alininca yeniden degerlendirilir. **Kaynagi kayitta:** bunlari uygulama degil
BIZIM bakim migration'imiz uretti (ikinci kez - Dalga 1'de de olmustu).

### (4) MODEL<->MIGRATION KAYMA KAPISI CI'DA

`ci.yml` / `format-check` -> `dotnet ef migrations has-pending-model-changes` (ZORUNLU).
**CIKIS KODU DAVRANISI OLCULDU, VARSAYILMADI** - ve CI'nin kullanacagi surumle (8.0.30, izole
bir `--tool-path`'e kurulup) iki dalda da kosuldu: senkron -> **exit 0**, kayma -> **exit 1**.
PATH acikca `$GITHUB_PATH`'e ekleniyor (global araclarin dizini kosucuda PATH'te olmayabilir).

### (5) DEPLOYMENT CHECKLIST'E "VERITABANI SEMASI" BOLUMU

Bugune kadar checklist'te **DB saglama maddesi HIC YOKTU**. Eklendi: iki uygulama yolu
(EF / script), `Turkish_CI_AS` sarti, kurulum sonrasi dogrulama (53 FK / 45 tablo), sira
(sema -> seed -> uygulama -> frontend) ve **ayricalik ayrimi** - uygulamanin calisma zamani
DB kullanicisinin DDL yetkisi YOK ve uygulama acilista migrate ETMEZ.

### (6) RUNBOOK'TAKI BAYAT SATIR DUZELTILDI

`ops/backup-dr-runbook.md:49` "Bu projede henuz migration yok" diyordu - **on bir migration
bayatlamisti**.

### SUREC ICINDE CIKAN VE DUZELTILEN IKI SEY

**(a) 02_seed.sql EF SEMASINA UYMUYORDU (onceden kirikti, FK'lardan DEGIL).**
`products.average_rating`, `products.review_count` ve `coupons.per_user_limit` **NOT NULL**
oldugu halde seed'de YOKTU: urun/stok/kupon INSERT'leri `Msg 515` ile dusuyor, yalniz
kategoriler yaziliyor ve script **kosulsuz "Seed tamamlandi: 3 kategori, 3 urun, 5 stok..."**
basip **EXIT 0** ile bitiyordu. Yani seed de sema gibi YALAN SOYLUYORDU. Eksik kolonlar
eklendi ve sona **sayilari DOGRULAYAN** bir blok kondu (tutmuyorsa `RAISERROR`).

**(b) SizeGuideManager KATEGORI VARLIK KONTROLU (kapsam notu - URETIM KODU).**
44 FK'dan yalnizca BIRI mevcut bir uca davranis degisikligi getiriyordu: `SizeGuideManager.Upsert`
`dto.category_id`'yi DOGRULAMADAN yaziyordu, yani var olmayan bir kategori SESSIZCE yetim satir
uretiyordu. FK eklendigi an ayni girdi **HTTP 500** olurdu - kendi degisikligimiz operatore
anlasilmaz bir hata dondururdu. Ayni katmandaki `ProductAttributeManager` idiyomu eklendi
(404 + `Messages.CategoryNotFound`). Bu, "supheli davranisi duzeltme" degil, **kendi
degisikligimizin actigi kapiyi kapatmaktir**; yine de uretim kodu oldugu icin ACIKCA raporlandi.

### TEST KURGULARI URETIME UYDURULDU (5 sinif)

53 FK yururluge girince **18 test kirildi** ve hepsi ayni sinifti: kurgular uretimin ASLA
uretmeyecegi satirlar yaziyordu (`orderId: 5001`, `orderId: 0`, `product_id = 42`).
**Sprint 8 madde 10 kalibi uygulandi: kisit GEVSETILMEDI, KURGU duzeltildi.**
Yeni ortak yardimci `TestVeriKurgusu` (`GercekSiparisAsync` / `GercekUrunAsync`);
`StockReservationTests` (8), `StockConcurrencyTests` (4), `ClaimBeforeSendTests` (3),
`AdminStockAndImageTests` (1) baglandi.

**BU, FK'LARIN URETIM UYUMLULUGUNUN ASIL KANITIDIR:** kurgular duzeltildikten sonra
**299/299 `Category=Sql`** yesil - yani 53 FK yururlukteyken suitteki HER gercek yazma yolu
geciyor.

## PINLER (`SemaTekKaynakTests`, 5)

- `URETILEN_SCRIPT_KURAR_ve_IKINCI_KOSUMDA_HATA_VERMEZ` - **DAVRANIS**: script gercek SQL
  Server'da kosar, sonra AYNI script IKINCI kez kosar. Vakum kirici: once GERCEKTEN kurmus
  olmali (>40 tablo, 53 FK) ve GO ile bolunmus olmali (>50 batch - tek batch'e sikismis bir
  script ilk hatada gerisini SESSIZCE atlar, D-S1'in ta kendisi).
- `SEED_URETILEN_SEMAYA_UYAR_ve_SESSIZ_YARIM_KALMAZ` - **DAVRANIS** + cift-anlam kirici:
  "hata firlamadi" yetmez, satirlarin GERCEKTEN yazildigi (3/3/5/2) olculur.
- `FK_KUMESI_KARARLA_ORTUSUR_IKI_DISLAMA_UYGULANMIS_HEPSI_RESTRICT` - **DAVRANIS**: 53
  iliskinin tamami BIREBIR karsilastirilir (liste CANLI KATALOGDAN uretildi, elle yazilmadi);
  iki dislamanin uygulandigi ve hicbir FK'nin CASCADE olmadigi AYRI AYRI assert edilir.
- `OLMAYAN_KATEGORIYE_BEDEN_REHBERI_404_DONER_500_DEGIL` - **DAVRANIS** + vakum kirici
  (gecerli kategori KABUL edilmeli - "her seyi reddet" bu pini gecemez).
- `SEMA_DOSYASI_URETILMIS_ARTEFAKT_ve_MIGRATIONLARLA_SENKRON` - **ARTEFAKT SOZLESMESI**:
  baslik blogu, `-b` / `-f 65001`, `generate_schema.py`'nin YOKLUGU ve **her migration
  kimliginin script'te bulundugu** (bayat artefakti yakalar).

**BILINCLI DEGISTIRILEN PIN (1):** `DalgaDVeriButunluguTests.KISIT_ADI_DEPLOY_SEMA_DOSYASIYLA_ORTUSUR`
kirilmadi ama **VAKUM KIRICISI** guncellendi: `CREATE TABLE product_stocks` ->
`CREATE TABLE [product_stocks]`. Uretilen script tablolari koseli parantezle yazar; assert'in
OLCTUGU sey degismedi, yalnizca aradigi bicim. **KIRILAN PIN YOK.**

## DIS KONTROLU + 5. KONTROL

**DIS:** 5 assert ters cevrildi (BES AYRI test) -> **5 AYRI ISIMLI KIRMIZI**. Geri alindi.

**5. KONTROL - IKI URETIM MUTASYONU:**

| Mutasyon | Sonuc | Olculen once-durum |
|---|---|---|
| **M1** SizeGuideManager kategori guard'i kaldirildi | `OLMAYAN_KATEGORIYE_...` KIRMIZI: `SqlException ... FK_size_guide_entries_category_id` (yani **HTTP 500**). Diger 4 pin YESIL - lokalize. | Guard olmadan FK'nin ureteceği ham 500 |
| **M2** `01_schema.sql` BAYAT surumle degistirildi (son migration YOK) | DORT pin KIRMIZI; `ilk.fk` **9 bulundu** (beklenen 53) - yani **karar oncesi durumun BIREBIR kendisi**. `SEED_...` yesil kaldi. | EF'in 9 FK'si vs dosyanin iddiasi |

Ikisi de geri alindi; kalinti yoklugu ayrica dogrulandi.

## DALGA ICI DENETIM - D-SEMA-FIX

**KENDI HATALARIM (dort):**
1. **Migration'i PowerShell here-string ile yazarken `═` karakterleri CIFT KODLANDI** ve dosyaya
   `â•â•` olarak indi. Yani D-S2'nin (kodlama bozulmasi) minyaturu, onu duzelttigim dalgada
   BENIM ELIMDE tekrarlandi. Yakalandi, satir tumuyle ASCII'ye cevrildi.
2. **Ilk dogrulama betigimde `Uygula ... | Out-Null` yazdim** ve fonksiyonun cikti satirlarini
   da yuttum; seed'in `exit=1` verdigi ILK TURDA GORULMEDI. Ikinci, acik kodlu kosumda cikti.
   **DERS: dogrulama betiginde cikti YUTULMAZ.**
3. **Pin'i once TEK bir veritabani adiyla yazdim**; dort test ayni ada baglanip birbirinin DB'sini
   dusurdu ("Cannot open database ... login failed"). Test BASINA Guid'li ad ile duzeltildi.
4. **`dotnet ef migrations script --to` diye bir secenek varsaydim** - yok; dogru sozdizimi
   `script <from> <to>`. Betik gurultulu dustu, duzeltildi.

**YARIM KALAN:** yok - alti kalemin alticisi da uygulandi ve kanitlandi.

**YAN ETKI TARAMASI:** `SizeGuideManager` kurucusu degisti -> Autofac `RegisterType<>` ile
kayitli ve `ICategoryDal` de kayitli; **CANLI DOGRULANDI**: uygulama ayaga kaldirilip
`GET /api/size-guide/category/1` -> **200**. `generate_schema.py`'ye kalan tek referans
README'deki "kaldirildi" aciklamasi. `LedgerAndRevenueSpecTests`'teki "yayin semasinda FK var
(01_schema.sql)" yorumu BAYATLADI ve duzeltildi. **Bilincli birakilan tekrar:** ayni dosyadaki
`SiparisSatiriKurAsync`, `TestVeriKurgusu.GercekSiparisAsync` ile ayni isi yapiyor;
birlestirmek kapsam disi sayildi.

**PIN DURUSTLUGU:** bes pinin **dordu DAVRANIS** (gercek SQL Server'da script kosuluyor, gercek
katalog ve gercek manager cagrisi olculuyor), biri artefakt sozlesmesi. Onceki dalgalardaki
"kaynak sozlesmesi" agirligi bu dalgada TERSINE dondu.

**BOZDUKLARIM:** kirilan pin YOK; bir pinin vakum kiricisi guncellendi (yukarida).

## ACIK KALANLAR (D-SEMA sonrasi, karar bekleyen)

- **Dosyanin HIC TANIMLAMADIGI dort GERCEK iliski** (olculdu, EKLENMEDI - kapsam disiydi):
  `invoice_items.invoice_id`, `invoice_items.product_id`, `review_helpful_votes.review_id`
  (ureteç `review_id -> reviews` ariyordu, tablo `product_reviews`), ve `sellers` kapali
  oldugu icin `products.seller_id` / `order_items.seller_id`. `stock_movements.reference_id`
  DOGRU sekilde disarida (polimorfik referans).
- **6 yetim `consent_records`** - hukuki gorus sonrasi yeniden degerlendirilecek.
- **`sqlite_schema.sql` ve Python simulasyonlari** artik elle bakimli ve semanin kaniti degil.

---

