# DALGA D - GERCEK VERI PROVASI (DEVAM EDIYOR - ALTI KALEMDEN BIRI BITTI)

**DURUM: KISMI.** Kullanici kapsami alti kalem olarak verdi (D1..D6). Bu commit'te
**YALNIZ D2** tamamlandi; D1 karari alindi ama UYGULANMADI, D4 yalniz STATIK okundu,
D3/D5/D6 HIC BASLANMADI. Dalga KAPANMADI - kalan kalemler sema ayrismasi karari sonrasina
birakildi (gerekce: hepsi VERITABANI uzerinde olcum yapiyor ve hangi semanin gercek oldugu
belli degilken olcum YANILTICI olur).

## D2 - YETIM STOK SATIRLARI + REFERANS BUTUNLUGU (TAMAMLANDI)

### OLCULEN ONCE-DURUM (dev veritabani)

```
yetim product_stocks satiri     : 120   (40 ayri product_id, 3..182)
yetimde reserved_quantity > 0   : 0
yetime bagli stock_reservations : 0
yetime bagli stock_movements    : 0
yetime bagli order_items        : 0
products -> product_stocks FK   : YOK (EF ile kurulan veritabaninda)
```

KAYNAK: Dalga 3'un performans seed temizligi urun satirlarini DOGRUDAN sildi, stok
satirlarini BIRAKTI. **URETIM YOLUNDAN GELMEDI** - `ProductManager.Delete` SOFT-delete'tir
(`is_active=false`); depoda fiziksel silme yapan kod yolu YOK (tarandi).

### KULLANICI KARARI: FK EKLE (secenek 2)

Gerekce kullanicinin kendi sozleriyle: bugun uretimde fiziksel silme yolu olmamasi yarin da
olmayacagi anlamina gelmez; pin kirildiginda hasar coktan olusmus olur. Ayni tabloda ayni
gece filtresiz UNIQUE indeks varsayiminin bedeli zaten odenmisti (Dalga B: urunun TUM
bedenlerini kaybettiren guncelleme). Ayrica yakinda gercek katalog toplu aktarimi geliyor.

### SILME DAVRANISI: RESTRICT - OLCUMLE SECILDI

`products`a isaret eden **MEVCUT IKI FK de** (`product_reviews`, `order_items`) `NO_ACTION`
tasiyor - yani deponun kendi konvansiyonu zaten "silmeyi ENGELLE".
**CASCADE REDDEDILDI:** uretimde silme SOFT oldugu icin cascade normal isleyiste **HIC
ATESLENMEZ**; yalnizca dogrudan-SQL fiziksel silmede ateslenir ve tam da durdurulmasi gereken
anda stok gecmisini **SESSIZCE goturur**.

### MIGRATION - SPRINT 6 KALIBI, UC ADIM

`20260824104731_YetimStokReferansButunlugu`:
1. **ON KONTROL:** bagli kaydi olan (rezerve adet / rezervasyon / hareket / siparis kalemi)
   bir yetim varsa **HICBIR SATIR SILINMEDEN** `RAISERROR`. Boyle bir satiri silmek, hala ona
   isaret eden bir gecmisi sessizce yok etmek olurdu.
2. **TEMIZLIK:** yalnizca ISPATLI SEKILDE ATIL yetimler silinir (kosul, kontrolun TAM TERSI).
3. **FK:** `IF NOT EXISTS` guard'li ham SQL.

`Down()` guard'li `DROP`; silinen yetimler GERI GETIRILMEZ (hangi urune ait olduklari bilgisi
zaten kayipti ve hicbir kayit onlara isaret etmiyordu).

### AD SEMA DOSYASIYLA HIZALANDI (DALGA ICI DENETIM BULGUSU)

Denetim olctu: `database/mssql/01_schema.sql` bu FK'yi **ZATEN tanimliyor** (satir 653, ad
`FK_product_stocks_product_id`). Yani kisit "yeni" DEGIL; **EKSIK OLAN EF TARAFIYDI**.
Iki sonuc:
- **AD** sema dosyasindakiyle AYNI secildi. EF'in urettigi varsayilan
  (`FK_product_stocks_products_product_id`) FARKLIYDI; sema dosyasindan kurulmus bir
  veritabaninda migration **IKINCI, GEREKSIZ** bir kisit yaratirdi (SQL Server ayni kolonlarda
  mukerrer FK'ya izin verir - sessiz israf).
- `AddForeignKey` yerine **GUARD'LI ham SQL**: kisit zaten varsa atlanir. Boylece IKI SAGLAMA
  YOLU DA ayni tek kisitta bulusur.

`DivisimaDbContext`te `HasOne<Product>().WithMany()` - **navigation EKLENMEDI**, entity duz
kaliyor; yalniz `HasConstraintName` + `OnDelete(Restrict)`.

### CANLI KANIT (dev veritabani, migration sonrasi)

```
FK                : FK_product_stocks_product_id | NO_ACTION
yetim satir       : 0        (once 120)
toplam stok satiri: 7
yetim INSERT      : DB REDDETTI | SQL 547 | mesajda kisit adi FK_product_stocks_product_id
guard turu        : "ZATEN VAR - ATLANDI (mukerrer kisit olusmadi)" | FK sayisi 1
```

### YAN ETKI TARAMASI (kullanicinin 4. sarti)

```
Test kurgusu     : 18 "new ProductStock" - HEPSI gercek p.id/urun.id kullaniyor
Uretim kodu      : 3 yer (ProductManager Add / ImportFromCsv / Update) - hepsi az once
                   yazilmis product.id
02_seed.sql      : urunler stoklardan ONCE ekleniyor, product_id'ler uyumlu
Tam suit         : FK ONCESIYLE AYNI
Urun SILEN betik : repoda YOK
```

**KIRILAN MEVCUT BETIK YOK.** Dalga 3'un urun silen betigi scratchpad'deydi ve silinmisti.
Bundan sonra urun silen bir bakim betigi yazilirsa stok satirlarini da silmek ZORUNDA -
bu dogru davranistir.

### PINLER (`DalgaDVeriButunluguTests`, 4)

- `YETIM_STOK_SATIRI_EKLEMEK_..._REDDEDILIR` - **DAVRANIS**: `DbUpdateException` ->
  `SqlException 547` ve mesajda kisit ADI aranir (cift-anlam kirici: baska bir kisit ihlali
  bu pini gecemez)
- `URUNU_FIZIKSEL_SILMEK_REDDEDILIR_YETIM_URETEN_YOL_KAPALI` - **DAVRANIS**: 120 yetimi
  URETEN gercek yol (dogrudan SQL ile `DELETE FROM products`) DB tarafindan reddedilir;
  vakum kirici (stok satiri gercekten yazilmis olmali) + cift-anlam kirici (reddedilen silme
  HICBIR satiri bozmamali - yarim silinmis durum tam olarak kacinilan sey).
  **BU PIN ILK YAZIMDA ZAYIFTI ve 5. KONTROLDE YAKALANDI** - bkz. KENDI HATALARIM #5.
- `FK_SILME_DAVRANISI_RESTRICT_CASCADE_DEGIL` - **DAVRANIS** (`sys.foreign_keys`)
- `KISIT_ADI_DEPLOY_SEMA_DOSYASIYLA_ORTUSUR` - **KAYNAK SOZLESMESI**; davranis kaniti canli
  guard turudur (mukerrer kisit olusmadi). Tarama YORUM SATIRLARINI AYIKLAR - bu pin ilk
  yazimda kendi migration yorumundaki alintiya takildi (bkz. KENDI HATALARIM).

**KIRILAN PIN YOK.**

## DALGA ICI DENETIM - D2 (kuralin ILK uygulamasi)

Kural ayni dalgada CLAUDE.md'ye yazildi ve HEMEN uygulandi; **iki gercek bulgu cikardi**.

**KENDI HATALARIM (bes):**
1. **FK'nin ZATEN TANIMLI oldugunu olcmedim.** "FK yok" tespitini YALNIZ EF veritabanindan
   yaptim; `01_schema.sql` satir 653'te zaten vardi.
2. Bunun sonucu **YANLIS AD** - sema dosyasindan kurulmus bir DB'de mukerrer kisit olusurdu.
   Denetimde yakalandi, ad hizalandi + guard eklendi, canli dogrulandi.
3. **D1'de BAYAT SAYI:** plani kapsama denetimindeki "59 dosya" ile kurdum, gercek **79**
   (aradaki fark sonraki test kosumlari).
4. **AYNI PIN TUZAGINA IKINCI KEZ dustum.** Dalga B'de "kaynak tarayan bir pin kendi
   belgeledigi kalibi da tarar" dersini CLAUDE.md'ye yazmistim; D2 pini tam buna takildi
   (migration yorumu kullanilmayan EF adini gerekce olarak ALINTILIYOR). Yorum satirlari
   ayiklanarak duzeltildi.

5. **VAKUM PINI YAZDIM ve 5. KONTROL YAKALADI.** Ilk `YETIM_PRODUCT_STOCKS_SATIRI_SIFIR`
   pini taze bir `EnsureCreated` veritabaninda yalnizca "yetim sayisi 0" olcuyordu - ve o
   sayi **FK KALDIRILSA BILE 0 kalirdi**, cunku test hicbir yetim URETMIYORDU. Uretim
   mutasyonunda birebir gorulduu: diger uc pin kirmizi olurken bu YESIL kaldi. Bolum 6'nin
   VAKUM YASAGI ihlaliydi. Pin, 120 yetimi URETEN gercek yolu olcecek sekilde yeniden
   yazildi (`URUNU_FIZIKSEL_SILMEK_REDDEDILIR_YETIM_URETEN_YOL_KAPALI`) ve mutasyonda artik
   KIRILIYOR. **Denetim kurali, yazildigi ilk dalgada kendi pinlerimden birini elemis oldu.**

Ek: `dotnet ef migrations remove` ozel migration GOVDEMI SILDI; fark edilip yeniden yazildi.
**DERS: govdesi elle yazilmis bir migration `remove` edilmeden ONCE yedeklenir.**

**DERS (YENI, BAYAT IKILI TUZAGININ UCUNCU BICIMI): `Copy-Item` ZAMAN DAMGASINI KORUR.**
Dis kontrolunu geri alirken dosya yedekten `Copy-Item` ile geri konuldu; kaynak dosyanin
LastWriteTime'i da geri geldigi icin **MSBuild dosyayi guncel sandi ve DERLEMEDI**. Sonraki
mutasyon turu, TERS CEVRILMIS assert'leri tasiyan ESKI ikiliyle kostu ve "4 kirmizi" verdi -
mutasyonun gercek etkisi (3 kirmizi + 1 yesil) gizlendi. Fark edildi (dosya temizdi ama hata
mesaji `Did not expect ...` diyordu, yani flip'in kendisi), `touch` + yeniden derleme ile
tur tekrarlandi. **KURAL: yedekten geri alinan her kaynak dosyanin zaman damgasi
TAZELENIR (`touch`), sonra derlenir.** Bu, CLAUDE.md'de zaten yazili olan `--no-build` ve
"API kosarken build" tuzaklarinin UCUNCU bicimidir - ucunun de belirtisi AYNI: bir onceki
kosumun sonucunun tekrarlanmasi.

## DIS KONTROLU + 5. KONTROL (D2)

**DIS:** 4 assert ters cevrildi (DORT AYRI test) -> **4 AYRI ISIMLI KIRMIZI**. Geri alindi.

**5. KONTROL - URETIM MUTASYONU:** `DivisimaDbContext`teki ProductStock FK yapilandirmasi
(`HasOne<Product>()...HasConstraintName(...)`) KALDIRILDI. Testler `EnsureCreated` ile
modelden veritabani kurdugu icin bu, FK'yi gercekten yok eder.

```
YETIM_STOK_SATIRI_EKLEMEK_..._REDDEDILIR              KIRMIZI  (DbUpdateException GELMEDI)
URUNU_FIZIKSEL_SILMEK_REDDEDILIR_..._YOL_KAPALI       KIRMIZI  (SqlException GELMEDI - yani
                                                                DELETE BASARILI, yetim URETILDI:
                                                                120 satirin kok sebebi BIREBIR)
FK_SILME_DAVRANISI_RESTRICT_CASCADE_DEGIL             KIRMIZI  (sys.foreign_keys BOS)
KISIT_ADI_DEPLOY_SEMA_DOSYASIYLA_ORTUSUR              YESIL    (kaynak artefaktlari mutasyona
                                                                girmedi - mutasyon LOKALIZE)
```

Mutasyon geri alindi ve FK yapilandirmasinin geri geldigi + `[MUTASYON]` kalintisi olmadigi
ayrica dogrulandi.

## YEREL DOGRULAMA (D2)

294/294 `Category=Sql` · tam suitte **475 basarili / 478** (kirilan 3'un UCU DE Docker'li
`OrderEndpointTests`; yerelde Docker kapali, CI'da yesil) · Release **0 hata** ·
whitespace **exit 0** · style **exit 0**.

## DENETIMIN CIKARDIGI YENI BULGU - SEMA AYRISMASI (KARAR BEKLIYOR)

```
database/mssql/01_schema.sql (belgelenmis deploy varligi) : 55 FK / 35 tablo
EF migrations ile kurulan DB (dev + CI)                   : 11 FK / 10 tablo
                                                     FARK : 44 FK
```

`database/README.md` sema dosyasini "43 tablo + 55 FK" diye tanimliyor,
`ops/backup-dr-runbook.md` onu sema kurmak icin alternatif yol gosteriyor ve uygulama
**ACILISTA MIGRATE ETMIYOR** (olculdu). Yani hangi yolla kuruldu ise veritabaninin
referans butunlugu TAMAMEN FARKLI - ve bugune kadarki TUM olcumlerimiz EF yolunda, yani
**FK'siz** olanda yapildi.

D2 bu ayrismanin **TEK BIR SATIRINI** kapatti. **KULLANICI KARARI: kalan kalemler oncesinde
"D-SEMA" adli YALNIZ-OLCUM turu kosulacak.**

## ACIK KALANLAR (Dalga D)

- **D1** gorsel yukleme/goruntuleme: OLCULDU, DEGISTIRILMEDI. `product_images` 3 satir,
  diskte 79 dosya, **KESISIM BOS** (3 DB satirinin dosyasi yok, 79 dosyanin DB satiri yok);
  79 dosyanin TAMAMI 64 bayt = testin sahte PNG'si, tarihler 21-24 Agustos'a yayiliyor ->
  **AKTIF SIZINTI: her test kosumu yeni dosya birakiyor.** Kullanici karari alindi (uretim
  yoluyla temizlik + test host'unda `UseWebRoot` gecici dizin + `DisposeAsync` temizligi;
  **SART: Sprint 8 madde 4 pini KIRILMAYACAK, `UseContentRoot(CWD)` hizalamasi GERI GELMEYECEK**).
  **HENUZ UYGULANMADI.**
- **D3** gercek olcek provasi (300-500 urun): HIC BASLANMADI.
- **D4** idempotency-key: YALNIZ STATIK OKUMA. Yan gozlem (canli dogrulanmadi): anahtar
  kapsami `key|path|user` ve `user = Identity.Name ?? "anon"` -> **misafir uclarda TUM
  anonimler ayni kovada**. Canli tur ve pin YOK.
- **D5** Redis acik kosum: HIC BASLANMADI.
- **D6** yedek/geri donus tatbikati: HIC BASLANMADI. (Sema ayrismasi kararinin dogal
  bulusma noktasi - iki saglama yolunu KARSILASTIRAN kalem odur.)

---

