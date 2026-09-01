# DALGA D - D6 (YEDEK / GERI DONUS TATBIKATI) - DALGA D'NIN SON KALEMI

Runbook bolum 1 "ayda bir restore tatbikati" diyordu ama tatbikat **HIC YAPILMAMISTI**.
Yapildi; olculdu; **runbook'un IKI iddiasi olcumle CURUDU ve runbook DUZELTILDI**
(kullanici sarti: "iddiayi olcume uydur, tersini yapma").

## SINIR - ONCE YAZILDI

**Tatbikat DEV ortaminda yapildi; gercek uretim yedegi YOK.** Uretim donaniminda, gercek veri
hacminde ve differential+log zinciriyle RTO FARKLI olabilir. Asagidaki sayilar dev olcumudur.

## FAZ 1 - MIGRATION'LARIN GERCEK SEMA UZERINDE KOSMASI (D-SEMA'nin iddiasi)

```
CREATE DATABASE DivisimaD6Sema COLLATE Turkish_CI_AS
01_schema.sql (-b -f 65001)   -> exit 0, 633 ms
   FK=56 · tablo=46 (45 + __EFMigrationsHistory) · migration kaydi=12
dotnet ef database update     -> "No migrations were applied. The database is already up to date."
   FK=56 · tablo=46 · migration kaydi=12        (DEGISMEDI)
```

**D-SEMA'NIN IDDIASI OLCUMLE KANITLANDI:** uretilen idempotent script ile migration'lar AYNI
semayi uretiyor; script ile kurulan bir veritabaninda migration **NO-OP**. Sayilar
`ops/deployment-checklist.md`'deki dogrulama maddesiyle (56 FK / 45 tablo) BIREBIR ortusuyor.

## FAZ 2 - YEDEK / GERI YUKLEME TATBIKATI

**SIRA BILINCLI:** yedek ONCE yan bir isimle geri yuklenip DOGRULANDI, veritabani ancak ondan
sonra dusuruldu. Kanitlanmamis bir yedege guvenip veritabanini dusurmek kurtarma denemesi
degil KUMAR olurdu.

```
BACKUP DATABASE (sikistirmasiz)   330 ms   2425 sayfa / 0,068 sn / 19,02 MB
RESTORE VERIFYONLY                "The backup set on file 1 is valid."
YAN ISIMLE geri yukleme           466 ms   -> invariantlar ZEMINLE BIREBIR AYNI
--- KESINTI BASLIYOR ---
DROP DATABASE                   1.693 ms
RESTORE DATABASE                  503 ms
uygulama ayaga kalkma           4.185 ms   (/health 200)
=== TOPLAM KESINTI (RTO) ===    6.382 ms = 6,4 SANIYE
```

Uygulama kesinti penceresini durust olcmek icin **ON DERLENDI** (`--no-build`); uretimde
yayinlanmis ikili zaten hazirdir, `dotnet run`in derleme adimi RTO'ya girmez.

### VERI TUTARLILIGI - 11 INVARIANT, ONCE == SONRA

Dalga 2'nin invariant sorgulari geri yuklemeden ONCE ve SONRA kosuldu; **`diff` FARK
BULMADI**. Kontrol edilenler: satir sayaclari · siparis toplami = kalemler · sadakat defteri =
bakiye · magaza kredisi defteri = bakiye · `reserved_quantity` = aktif rezervasyonlar ·
fatura 1:1 · mukerrer siparis no · negatif deger · yetim satir (4 tablo) · KDV kimligi ·
sema (FK/tablo/collation).

**OLCUT "SIFIR" DEGIL, "ONCE ILE SONRA AYNI".** `I04` (magaza kredisi defteri) **1 ihlal**
tasiyor ve bu ONCEDEN VARDI - (C) guvenlik dalgasindan kalma dev artigi (musteri 23, bakiye
100,00 / defter 400,00). Geri yukleme onu ne duzeltir ne bozar; degismemesi DOGRU sonuctur.

**KENDI OLCUM HATAM (kayit):** ilk `I02` sorgum iptal edilmis kalemleri DISLAMIYORDU ve
**8 yanlis ihlal** sayiyordu. Dalga 2'nin invarianti dogruydu, sorgum yanlisti; duzeltilince
0 cikti. Yikici adimlardan ONCE yakalandi.

### UYGULAMA DOGRULAMASI (geri yukleme sonrasi, gercek uclar)

```
/api/product/filter    200   /api/category/getlist  200
GERCEK GIRIS           200   (token uretildi - parola hash/salt geri yuklemeden SAG cikti)
/api/order/my-orders   200
```

## RUNBOOK'UN CURUYEN IKI IDDIASI (olculdu, DUZELTILDI)

**(1) RPO 15 DAKIKA - BU ORTAMDA IMKANSIZ.**

```
recovery modeli = SIMPLE
BACKUP LOG DivisimaDb ...
   -> Msg 4208: The statement BACKUP LOG is not allowed while the recovery model is SIMPLE.
```

Yani runbook'un "transaction log 15 dakikada bir" satiri ve bolum 3'teki **point-in-time
proseduru (RESTORE LOG ... STOPAT) SIMPLE modelde KOSULAMAZ**. SIMPLE'da gercek RPO, son
full/differential yedekten bu yana gecen suredir - gunluk 03:00 full ile **24 saate kadar**.
DUZELTME: RPO hedefi **KOSULLU** hale getirildi (FULL recovery + 15 dk log yedegi on kosulu),
`ALTER DATABASE ... SET RECOVERY FULL` + ardindan full yedek adimi runbook'a yazildi ve
`ops/deployment-checklist.md`'ye **zorunlu dogrulama maddesi** eklendi.

**(2) SURUM SINIRI - EXPRESS.**

```
edition = Express Edition (64-bit)
BACKUP ... WITH COMPRESSION -> Msg 1844: not supported on Express Edition
```

Express **backup compression** ve **TDE** desteklemiyor; yani runbook'un "yedekler sifreli
olmali (TDE veya backup encryption)" maddesi Express'te KARSILANAMAZ. Checklist'e "SQL Server
surumu Express DEGIL" maddesi eklendi.

**(3) RTO 1 SAAT - KORUNDU ama artik OLCULU.** Dev olcumu 6,4 sn; hedef, uretim donanimi ve
differential+log zinciri icin makul bir TAVAN olarak birakildi ve tatbikat sayilariyla
birlikte runbook'a yazildi.

## RUNBOOK'A EKLENEN TEKRARLANABILIR TATBIKAT (bolum 3b)

Dort adimli prosedur + olcum sablonu yazildi: yedek+VERIFYONLY -> **yan isimle geri yukleme
ve invariant dogrulamasi** -> asil dusurme/geri yukleme -> uygulama + invariant tekrari.
Sira gerekcesiyle birlikte belgelendi.

## TEMIZLIK - KANITLI

```
DivisimaD6Sema      DUSURULDU        DivisimaD6Restore   DUSURULDU
kalan tatbikat DB   0                DivisimaDb          VAR (geri yuklendi, calisiyor)
tatbikat yedegi     SILINDI (xp_delete_file; yedek klasoru ACL korumali oldugu icin
                    dosya sistemi uzerinden erisilemiyor - SQL Server'in kendi araciyla)
portlar 5000/5173   BOS              depo                git status TEMIZ (kod degismedi)
```

## PUSH RAPORU `2bc53c5` - HER IKI WORKFLOW TAMAMEN YESIL

Push `024a1a5..2bc53c5`. Adim bazinda + annotation duzeyinde dogrulandi: `build-and-test`,
`format-check`, `tests`, `codeql`, `secret-scan`, `dependency-scan` - **alti job da SUCCESS**,
**failure seviyeli annotation 0**. Retry annotation'i iki job'da da okundu:
`TestDbKurulum: 1807 yeniden denemesi bu kosumda HIC ATESLEMEDI (0)`.

