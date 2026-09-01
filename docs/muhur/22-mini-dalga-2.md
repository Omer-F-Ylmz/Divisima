## MINI DALGA 2 - SUPHELI #18 DUZELTMESI (TAMAMLANDI)

Kullanici karari: #18 launch ONCESI duzeltilir, kapsam sinirli.

### SINIR OLCEREK CIZILDI - HANGI DURUMLAR ONAYA DAHIL?

Rezervasyon durum gecisleri okundu (`TryReserveAsync` / `ConfirmReservation` /
`ReleaseReservation` / `ReleaseExpiredReservations`) ve her durum icin FIZIKSEL stok hali
cikarildi:

| Durum | `reserved_quantity` | `stock_quantity` | Onayda dogru islem |
|---|---|---|---|
| `Active` (0) | TUTULUYOR | dusmemis | atomik gecis + `ConfirmStockAsync` |
| `Confirmed` (1) | serbest | **ZATEN DUSMUS** | DOKUNULMAZ (cift dusum olurdu) |
| `Expired` (3) | serbest (cleanup birakti) | dusmemis | **DAHIL** - dogrudan dusum |
| `Released` (2) | serbest | dusmemis | **DAHIL EDILMEDI** |

**`Released` NEDEN DISARIDA - gerekce FIZIKSEL DEGIL ANLAMSAL (durust duzeltme):**
Fiziksel olarak `Released` ile `Expired` AYNIDIR - `ReleaseReservedAsync` yalniz
`reserved_quantity`'yi azaltir, fiziksel stogu GERI EKLEMEZ (kodda da "fiziksel degismez"
yaziyor). Yani buradaki risk **"cift dusum" DEGIL**. Gercek gerekce su: `Released`i YALNIZCA
`ReleaseReservation` yaziyor ve o da yalniz iki yerden cagriliyor - `IyzicoPaymentManager`in
odeme BASARISIZ dali ve `OrderManager`in siparis IPTAL yolu. Yani `Released` = **"bu siparis
iptal edildi" karari**. Boyle bir rezervasyonun onaya gelmesi bir stok kurtarma senaryosu
degil, bir **DURUM MAKINESI IHLALIDIR**. Stogu orada dusmek (a) kimsenin sevk etmeyecegi bir
siparis icin hayalet kayip yazar, (b) asil hatayi - iptal edilmis siparisin yeniden
onaylanmasini - SESSIZCE ortbas eder.

### YAN BULGU: TELAFI DALI ATOMIK DEGILDI

Eski telafi dali `TryDirectDeductAsync` yapip rezervasyonu **Expired BIRAKIYORDU**. Sorgu
Expired'i hic getirmedigi icin bu gorunmuyordu; ama Expired ARTIK normal bir yol oldugu icin
ikinci bir `ConfirmReservation` cagrisi ayni satiri TEKRAR dusurebilirdi. Bu yuzden her iki
yol da `Active->Confirmed` / `Expired->Confirmed` gecisini KAZANMAK zorunda birakildi.
Yani duzeltme, kendi actigi kapiyi da kapatiyor.

### SESSIZ HICBIR YOL KALMADI - IKI KANAL

`ExpireSonrasiTelafiAsync`: stok varsa dogrudan dusulur; **yoksa**
1. `stock_movements` notu (envanter defteri) - **MEVCUT DAVRANIS AYNEN KORUNDU**,
2. **siparis zaman cizelgesi** (H53 "KRITIK/UYARI" kalibi) - YENI kanal.
Ikincisi eklendi cunku hareket kaydini kimse duzenli okumuyor; #33'te zaten HICBIR satir
yazilmamisti ve sapma aylarca gorunmeyebilirdi. Zaman cizelgesi yazimi BEST-EFFORT
(try/catch + `LogError`): not yazilamazsa onay akisi KIRILMAZ, birinci kanal zaten yazildi.
`StockManager` iki yeni bagimlilik aldi (`IOrderStatusHistoryService`, `ILogger`); dongusel
bagimlilik YOK - `OrderStatusHistoryManager` yalniz DAL'lara bagli (kontrol edildi).

### BILINCLI KIRILAN PIN

`SUPHELI_RezervasyonEXPIRE_Olduysa_Onay_STOK_DUSURMUYOR_ve_UYARI_YAZMIYOR_PINLENIR` ->
`RezervasyonEXPIRE_Olsa_da_Onay_STOK_DUSURUR_ve_HAREKET_YAZAR`.
Eski pin OLCULEN supheli davranisi (stok DUSMEZ + hareket YOK) sabitliyordu; #18 duzelince
envanter sapmasini SAVUNUR hale gelirdi.

YENI PINLER (`WebhookContractTests`):
- `RezervasyonEXPIRE_Olsa_da_Onay_STOK_DUSURUR_ve_HAREKET_YAZAR` - stok duser, `reserved`
  EKSIYE gitmez, TEK hareket satiri yazilir, notu "expire" iceri (cift-anlam kirici: normal
  onay notuyla karismaz) ve rezervasyon **Confirmed**'a gecer (ikinci dusumu engelleyen sey).
- `RezervasyonEXPIRE_ve_STOK_TUKENMISSE_UYARI_ZAMAN_CIZELGESINE_Duser` - stok EKSIYE
  cekilmez, hareket notunda "UYARI" var VE zaman cizelgesinde uyari notu var. Ikinci assert
  olmadan "sessiz hicbir yol kalmaz" iddiasi kanitlanmis olmazdi.
Ikisi de on kosulu GERCEK temizlik yoluyla kuruyor (`ReleaseExpiredReservations`) - sahte
kurgu degil.

### DIS KONTROLU + 5. KONTROL

3 assert ters (uc AYRI test: iki yeni pin + `StockReservationTests.Confirm_IkiKezCagrilinca_CiftDusumYok`)
-> **3 AYRI ISIMLI KIRMIZI** (geri alindi).
5. kontrol: `ConfirmReservation` sorgusu `Active`-only haline dondurulda ->
`RezervasyonEXPIRE_Olsa_da_...` **stok 10 buldu (dusmedi)** ve uyari pini de kirildi -
**siparis #33'te olculen tablonun BIREBIR aynisi**. Diger 21 test YESIL kaldi (mutasyon
kesin olarak lokalize). Geri alindi.

### SIPARIS #33'UN KENDI ENVANTER SAPMASI - OLCULDU, DOKUNULMADI

```
siparis 33  urun 2 / M  quantity 2   rezervasyon status=3 (Expired)  hareket kaydi YOK
siparis 34  urun 2 / M  quantity 3   rezervasyon status=1 (Confirmed) hareket kaydi VAR
product_stocks  urun 2 / M  ->  stock_quantity 10   reserved_quantity 0
```
Yani #34'un 3 adedi dusuldu, **#33'un 2 adedi DUSULMEDI**. Dogru deger 8 olmaliydi.
**Bu GELISTIRME veritabani (`DivisimaDb`) ve sandbox siparisi** - fiziksel mal yok.
Secenekler sunuldu: (A) hicbir sey yapma, (B) duzeltilmis uretim yolunu bir kez kostur,
(C) elle SQL. **KULLANICI KARARI: B.** Gerekcesi: "bulguyu doguran canli artigin, bulgunun
duzeltmesiyle temizlenmesi en durust kapanis"; (C) denetim izi birakmaz, (A) yarim kapanis olur.

### #33 ENVANTER SAPMASI GIDERILDI - CANLI OLCUM (secenek B)

`StockManager.ConfirmReservation(33)` **URETIM KODU** tek seferlik bir kosucuyla cagrildi.
Kosucu DEPO DISINDA (scratchpad) tutuldu ve is bitince SILINDI - commit'e girmesi mumkun
degildi. **ELLE SQL YAZILMADI**: hem stok dusumu hem denetim izi uretim yolunun kendisi
tarafindan uretildi.

```
ONCE
  urun 2 / M   siparis adedi = 2
  stock_quantity = 10   reserved_quantity = 0
  rezervasyon status = 3 (Expired)
  stock_movements(reference_id=33) = 0 satir

BIRINCI CAGRI -> 200 / success=True / "Rezervasyon onaylandı (stok düşüldü)."
  stock_quantity = 8    reserved_quantity = 0      <- 2 adet DUSTU, reserved EKSIYE GITMEDI
  rezervasyon status = 1 (Confirmed)               <- Expired -> Confirmed ATOMIK GECIS
  stock_movements = 1 satir
     tip=2 (Out) adet=2
     not="Ödeme onaylı - rezervasyon expire olmuştu, stok yeniden güvenceye alındı"

IKINCI CAGRI -> 200 / success=True / (ayni mesaj)
  stock_quantity = 8    reserved_quantity = 0      <- DEGISMEDI
  rezervasyon status = 1 (Confirmed)               <- DEGISMEDI
  stock_movements = 1 satir                        <- IKINCI SATIR YAZILMADI
```

**KENDINI SINIRLAMA CANLI TEYIT EDILDI:** ikinci cagri hicbir yan etki uretmedi - rezervasyon
artik `Confirmed` oldugu icin sorgunun (`Active` VEYA `Expired`) disinda kaliyor. Bu, madde
(1)'in "Confirmed DOKUNULMAZ" sinirinin ve yan bulgunun (telafi dalinin atomik gecise
baglanmasi) canlida calistiginin kanitidir.
Not: ikinci cagri da 200/success donuyor - "yapacak is yok" ile "basarili" ayni yaniti veriyor.
Bu idempotent bir onay ucu icin DOGRU davranis (cagiran tekrar denedi diye hata almamali) ve
etkisizligin kaniti YANIT DEGIL, yukaridaki sayaclardir.

