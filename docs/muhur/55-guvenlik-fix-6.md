# 55 · GUVENLIK-FIX-6 (LAUNCH ONCESI: UYE YOLU + DURUM MAKINESI + HUB + ICE-AKTARIM)

**Zemin:** `3095568f151c40cb6cb1c929a8bc1fbf1824bc1f`
**Kapanis:** `6fa0d6caa56da53c47663aee89a82ec5df11bb7d` — dokuz commit, TEK push.
**Kaynak:** `53·GUVENLIK-AV-3` (T1-B1..B4 · T4/S-1 · T4-F5 · X-2 · T2-1/T2-2/T2-5/T2-6 ·
T4-F1/F2 aday) + `54·ARSIV-4` §6.1 (N-1 probu).
**ORTAK KOK (merkez tespiti, dogrulandi):** misafir yolunun KAZANDIGI kapilar UYE yoluna
TASINMAMISTI.

---

## 1. KOMUT ZINCIRI (dokuz commit)

```
7e84b50 K1-K3, K5-K7: uye yolu butunlugu + durum makinesi + hub + ice-aktarim
f08c04f PIN DUZELTMESI: R6_1 sepeti AYNI yapildi (MK-6 turunda BEDAVA DOGRU cikti)
0b4939e K5 DURUSTLUK: iki YANLIS yorum duzeltildi (MK-6 turunda olculdu)
e7dd658 K7 PIN EKI: istek tavani <-> merkezi sabit ayrisma pini
b92abd3 F1+F3: COD para anlami Delivered'da + cift iade [PARA] KAPANDI
7550d26 F2 (S-1): terminal siparise gelen basarili odeme UCUNCU DURUM doner
0bc5d50 F2 vitrin: odeme sonuc sayfasi `review` durumunu TANIR (kapsam istisnasi)
d45ef1d F4: pin bulgulari kapatildi + F1-F3 pinleri
6fa0d6c F4 EK: satir siniri DEGERI ayrica pinlendi (MUT-20 boslugu)
```

---

## 2. K0 KAPI EKLERI

**(a) RIG IKILISI — ILK OLCUMDE BAYAT, T5-0 DERSI UCUNCU KEZ.**
`Divisima.API.dll` mtime **1788565837** < son KOD commit'i `027a88a` (**1788570216**) <
HEAD commit'i (**1788623535**). AYRICA: **`goz1` duzenegi bu makinede YOK** — dizin yok,
5000/5001/5173'te LISTENING yok. Yani tarifin varsaydigi canli rig KANALI HIC YOKTU ve
butun R-6.x olcumleri test host'u uzerinden kosuldu.
Kapatildi: Debug rebuild (0 Hata / 1603 Uyari) -> API **1788628695** · Core **1788628688** ·
Bussiness **1788628694**, ucu de zemin commit zamanini GECTI.
Suzgec sinamasi: `GirdiSinirlari` POZ `Divisima.Core.dll`=**1** (Bussiness=0, API=0 — sinif
Core'da) · NEG `ZZZGirdiSinirlari`=**0**.

**(b) N-1 PROBU — KANIT KAYBI KAPANDI.**
Izole EF konsolu (net8.0 + EF Core 8.0.30, DB `GF6_N1_Probe`, DEPO DISI), N=5:

```
T1 begin -> ExecuteDeleteAsync -> ROLLBACK : donen=5 · KALAN=5
T2 begin -> ExecuteDeleteAsync -> COMMIT   : donen=5 · KALAN=0
T3 transaction YOK -> ExecuteDeleteAsync   : donen=5 · KALAN=0
```

**SONUC: `ExecuteDeleteAsync` AMBIENT TRANSACTION'A KATILIR.** Uc dal da karar kriterine
BIREBIR uydu. `GuestCheckoutManager.MisafirKayitlariniTelafiSilAsync`in GF-5/K4 atomiklik
iddiasi DOGRULANDI. AV-2'nin `S-B` ajaninin sohbette beyan edip muhre TASIMADIGI olcum
(merkez hatasi olarak kayitli) boylece KAPANDI. **Kod yazilmadi, migration yok.**

**(c) TMP ENVANTERI.** ONCE **495** -> SONRA **645**. **TEK KOK**: `/tmp` ile `%TEMP%`
AYNI inode (`2251799813792332`) — ilk raporda "birlesik 990" yazilmisti, **CIFT SAYIMDI**,
rapor denetcisi yakaladi ve duzeltildi. Yeni 110 dalga-ilgili dosyanin ~90'i `dotnet`
workload logu; **45'i DENETCILERIN scratchpad DISINA yazdigi calisma dosyasi**
(SDP 1.12.6-b / MK-5 ihlali — D2 ve D3). Jeton suzgeci (uzunluk>=16 + rakam + kucuk harf,
POZ/NEG sinanmis) ile tarandi: **gercek sir 0**. Dosyalar silindi.

---

## 3. K1-K8 (ILK TUR)

| Kalem | Ne yapildi | Kanit |
|---|---|---|
| **K1** (D1) | `ReplayGuardiAsync` · `AyniSiparisMiAsync` · `SepetAnahtari` · `SiparisBuEpostayaMiAitAsync` -> `SiparisReplayGuardi` **TASINDI** (kopya DEGIL). Sahiplik ekseni PARAMETRE: misafir=E-POSTA, uye=`customer_id` | MUT-1b TAM 1 · MUT-12 3 kirmizi (GF-6 + misafir ikisi) |
| **K2** (D2) | `address_id` ZORUNLU (validator 400 + manager savunmasi); 404 + `IdorAttempt` AYNEN; `OrderSnapshot.shipping_address` DOLDURULUYOR (kirpma URETIM NOKTASINDA, kolon 500) | MUT-2c · MUT-3 · MUT-13, ucu de TAM 1 |
| **K3** (D4) | `GirdiSinirlari.GecerliOdemeYontemleri` {0,1,2}; degerler URETIMDEN turetildi | MUT-4 TAM 1 |
| **K4** (D3) | **ILK TURDA DUR** — bkz. bolum 4. Bagimsiz parcasi sevk edildi: `order/place` artik `payment` kovasinda (10/dk) | MUT-14 TAM 1 |
| **K5** (D5) | `OrderManager.DurumYaz` tek kapi; Iyzico'nun IKI yazim yeri terminal siparisi DIRILTMEZ, `payments` satiri Success KAYDEDILIR, `PaymentAfterTerminal` (Critical) yazilir | MUT-5 · MUT-6b TAM 1 |
| **K6** (D6) | `MapHub<NotificationHub>(...).RequireAuthorization()` | MUT-7 TAM 1 (KAYNAK pini) |
| **K7** (D7) | satir siniri 5000 · dosya 5 MB + uzanti/content-type · formul enjeksiyonu · ad/marka uzunlugu · HEPSI-YA-DA-HICBIRI · tek OZET `ProductImportRejected` olayi | MUT-8/9/10/11/15/22 |
| **K8** (D8) | Olcum (kod yok) — bkz. bolum 5 | gecici prob, silindi |

---

## 4. K4 / D3 — ILK TURDAKI DUR VE F1'DEKI COZUM

### 4.1 DUR gerekcesi (uc bagimsiz olcum)

**(a)** `Divisima.Core.csproj` icinde `Divisima.Entity` referansi **0** (Entity -> Core;
ters yon DONGU olur). `PaidOrderSpec` `Order` tipini **GOREMEZ** -> orada
`Expression<Func<Order,bool>>` **YAZILAMAZ**; sinif YALNIZ ilkel (`byte`) gorur.
**(b)** `PaidStatuses|IsPaidStatus|IsSoldItem` KOD (yorum AYIKLANMIS) cagri sitesi **17**,
sinif **7**; **14 site / 6 sinif KAPSAM DISI** (Seller · Dashboard · Merchandising ·
Recommendation · Coupon · Referral).
**(c)** Bu 17 sitenin **0**'i `payment_type` okuyor. NEG capa `ZZZPaidOrderSpec` -> 0.
**Tarifin andigi `ProductReviewManager` ZATEN `Delivered` sart kosuyor ve `PaidStatuses`
KULLANMIYOR** — yani "duzelecek tuketici" degil.

**MERKEZ HATASI (kayit):** "7 tuketici kendiliginden duzelir" premisi kodla UYUSMUYORDU.

### 4.2 F1 cozumu (DAR)

- **Core:** `PaidOrderSpec.IsPaid(byte status, byte paymentType)` — COD ise yalniz
  `Delivered`, aksi mevcut kural. `KapidaOdemeTuru = 1` · `KapidaOdenmisDurum = Delivered`.
- **EF yuzu:** `Divisima.Entity/Specifications/OdenmisSiparisSpec` (Entity, Core'u GORUR).
  Iki bicim **TAM MATRIS PINI** ile bagli: gecerli HER (durum x odeme turu) ciftinde
  `IsPaid` ile derlenmis yuklem karsilastirilir. MUT-16 TAM 1 kirmizi.
- **Gecen siteler:** referans odulu (`ReferralManager`) + sadakat kazanimi.
- **Sadakat:** `PaymentConfirmedSideEffects` **BOLUNMEDI**. COD'da `Confirmed` dalinda
  kazanim ATLANIR; `Delivered` yaziminda (`OrderManager.ChangeOrderStatus` **ve**
  `ShipmentManager`in teslimat dali) AYNI `PaymentConfirmed` olayi YENIDEN yazilir. Dort
  adimin ucu idempotent oldugu icin fiilen yalniz sadakat kosar. Durum **SIPARIS
  SATIRINDAN** okunur, olaydan DEGIL (outbox'ta bekleyen eski satirlar varsayilana dusmesin).
- **Fatura zamanlamasi DEGISMEDI** (muhasebe karari).

### 4.3 KUPON LIMITLERI ESKI KURALDA KALDI — TARIFDEN SAPMA, OLCULMUS GEREKCE

Dort kupon sitesi (global + kisi-basi, hem onizleme hem enforcement) `OdenmisSiparisSpec`e
gecirildi ve **OLCULDU**: `CouponRaceTests.SonHakkaSekizParalelIstek_KuponYALNIZ_BIRINDE_Uygulanir`
reddedilen kumesini **BOS** buldu — yani `usage_limit=1` bir kuponu **SEKIZ es zamanli COD
siparisinin HEPSI** aldi.

**KOK SEBEP:** COD siparisi `Pending` **DOGMAZ**, `Confirmed` dogar. "Odenmis" olcutunden
`Confirmed` cikinca COD icin sayilacak **HICBIR durum kalmaz**; "taze bekleyen Pending" dali
da COD'da **HIC ATESLEMEZ**. Sonuc: `usage_limit` COD yolunda **YAPISAL OLARAK
UYGULANAMAZ** hale geliyordu — T1-B4 kapatilirken **YENI bir [PARA] bypass'i** aciliyordu.

**AYRIM (kalici):** kupon limiti "para ALINDI MI" degil **"kupon hakki hala CANLI MI"**
sorusudur. Gecis bu yuzden YALNIZ **musteriye PARA CIKISI** yapan sitelerde kaldi.
Sapma raporlandi; karar merkezindir.

---

## 5. K8 — ONCE / SONRA (gecici prob, depoya HIC commit edilmedi)

```
ONCE (kilit YOK):
  T4-F1 (UNIQUE)      TUR=48  HIT=41   ve  TUR=48  HIT=35   (iki bagimsiz kosum)
  T4-F2 (rowversion)  TUR=48  HIT=48
  AYIRT EDICI KONTROL: SERI kosum  TUR=8  KAYIP=0
SONRA (F3 kilidi):
  T4-F1               TUR=48  HIT=0     <-- [PARA] KAPANDI
  T4-F2               TUR=48  HIT=48    <-- GF-7 (gerekceli istisna)
```

Ayirt edici seri kontrol, farkin **eszamanliliktan** geldigini gosterir ("tasarim geregi
ezme" degil). Prob dosyasi olcumden sonra SILINDI; ham arsiv scratchpad'de.

### 5.1 T4-F2 — LAUNCH BLOKER OLCUTU ISTISNASI (gerekce + tetikleyici)

`51·AV-2` olcutu `[VERI-BOZAN]` + davranis kaniti = LAUNCH BLOKER der; T4-F2 ikisini de
saglar. **ISTISNA VERILDI (merkez karari), gerekce:** para alanlari ZATEN atomiktir
(H27 `store_credit` CAS, `TryDecrementStoreCreditAsync`, `TryAddRefundedAmountAsync`);
kayip yalnizca **profil alanlarinda** ve **ayni hesabin kendi es zamanli guncellemesinde**
olusur. **TETIKLEYICI: GF-7 ILK KALEM** (rowversion migration).

---

## 6. F2 — S-1: BU DALGANIN ACTIGI YUZEY, F-TURUNDA KAPATILDI

**OLCULEN ONCE-DURUM (L3 denetcisi buldu, ana akis KENDI komutuyla dogruladi):**
`IyzicoPaymentManager` icinde `if (odemeGecerli) ... return (OK, PaymentSuccess)`,
`if (terminalAtlandi) return (BadRequest, PaymentFailed)` satirindan **ONCE** geliyordu —
yani `terminalAtlandi` bayragi BASARILI dalda **hicbir yaniti degistirmiyordu**.
`PaymentController` da `status = (Item1 == OK ? "success" : "failed")` ile yonlendiriyordu.
Musteri **IPTAL kalan** siparis icin "Odeme basarili, siparisiniz onaylandi." + `success`
ekrani goruyordu.

**GF-6 ONCESINDE bu tutarsizlik YOKTU** — K5 oncesi siparis `Confirmed`a DIRILIYORDU, yani
200 "tutarli"ydi. Yuzeyi **bu dalga acti**.

**COZUM (secenek a):** UCUNCU DURUM. Yanit **200 KALIR** (para GERCEKTEN alindi; 400
"paran cekilmedi" dedirtir ve iade talebini geciktirirdi) ama **MESAJ ayrisir**:
`Messages.PaymentReceivedOrderCancelled`. `PaymentController` o mesaji **ORDINAL**
karsilastirmayla tanir ve **`status=review`** yonlendirir. Basarisiz-dal + terminal hali
DEGISMEDI (400). **SIRA SOZLESMESI PINLI**: terminal kontrolu basarili donusun USTUNDE
olmalidir (MUT-18 TAM 1).

**VITRIN (kapsam istisnasi, AYRI commit `0bc5d50`):** OLCULDU — `odemeBasariliMi("review")`
false doner ve sayfa **BASARISIZ** gibi gosteriyordu ("Odeme Tamamlanamadi"). Yedi satirlik
ek: sabit TR metin, **SOZLUK DOKUNULMADI** (i18n kaydi VITRIN-KALAN'a). Baslik IKI YUZEYDE
de ayni kaynaktan gelir (ekran + sekme) — MFIX-3'un ayrisma tuzagi yeniden ACILMASIN diye.

---

## 7. F3 — T4-F1 [PARA] KAPANISI (migration YOK)

- `POST /api/return/create` -> `[Idempotency]` (ag tekrari/cift-tik).
- `ReturnManager.CreateReturn` -> **KALEM BASINA dagitik kilit** `return:{order_item_id}`
  (15 sn) ve kilit ICINDE **TAZE (no-tracking)** okuma. Emsal: `payment-order:{id}` ve
  `coupon:{kod}`. CLAUDE.md bolum 5: `GetListAsync` TRACKED'dir — kilitten sonraki okuma
  TAZE olmazsa koruma SESSIZCE olu kalirdi (MUT-19c TAM 1).

**"AKTIF IADE VARSA 409" KONTROLU EKLENMEDI — TARIFDEN SAPMA, OLCULMUS GEREKCE:**
uygulandi ve MEVCUT BIR PINI KIRDI —
`ReturnFlowTests.IadeMiktari_SiparisEdilenAdedi_ASAMAZ_KismiIadeSonrasiKalanKadar`
("kalan 1 adet de iade edilebilmeli"). Yani kural, 2 adetlik kalemde ONCE 1 SONRA kalan 1
adedi iade etmeyi — urunun PINLENMIS mesru davranisini — engelliyordu. Yarisi kapatan sey
kontrol degil **SERILESTIRMEDIR**; K8 SONRA olcumu (HIT=0) bunu gosterir.

---

## 8. MUTASYON TABLOSU (MK-6) — 24 KOSUM

```
MUT-1   sahiplik yuklemi kaldirildi           -> 0 KIRMIZI  [PIN KUSURU BULUNDU]
MUT-1b  ayni mutasyon, pin duzeltildikten     -> TAM 1 (R6_1)
MUT-2c  adres kapisi devre disi               -> TAM 1 (R6_3a)
MUT-3   shipping_address = null                -> TAM 1 (K2_SNAPSHOT)
MUT-4   payment_method kurali devre disi      -> TAM 1 (R6_5)
MUT-5   Iyzico terminal kapisi devre disi     -> TAM 1 (GF6_K5_IPTAL_EDILMIS...)
MUT-6   DurumYaz kapisi devre disi            -> 0 KIRMIZI [PIN SINIRI BEYAN EDILDI]
MUT-6b  dogrudan atama geri getirildi         -> TAM 1 (K5_DURUM_YAZIMI_TEK_KAPIDAN_GECER)
MUT-7   MapHub RequireAuthorization silindi   -> TAM 1 (KAYNAK) · R6_7 YESIL KALDI
MUT-8   CSV satir siniri devre disi           -> TAM 1 (R6_8a)
MUT-9   formul kapisi devre disi              -> TAM 1 (R6_8b)
MUT-10  hepsi-ya-da-hicbiri devre disi        -> 4 KIRMIZI (ortak mekanizma - beklenen)
MUT-11  ad uzunlugu devre disi                -> TAM 1 (K7_AD_UZUNLUGU)
MUT-12  replayed = false                      -> 3 KIRMIZI (GF-6 + MISAFIR ikisi)
MUT-13  IdorAttempt cagrisi kaldirildi        -> TAM 1 (R6_3b)
MUT-14  payment kovasi oznitelig i silindi    -> TAM 1 (K4_ORDER_PLACE_PAYMENT_KOVASINA_BAGLI)
MUT-15  istek tavani 5 MB -> 9 MB             -> TAM 1 (K7_ISTEK_TAVANI...)
MUT-16  IsPaid'in COD dali devre disi         -> TAM 1 (F1_ODENMIS_OLCUTU_IKI_BICIMDE_de_AYNI)
MUT-17  COD sadakat atlamasi devre disi       -> 3 KIRMIZI (SideEffectSingleEntry ucusu)
MUT-18  terminal kontrolu devre disi          -> TAM 1 (F2_TERMINAL_ODEME_UCUNCU_DURUM...)
MUT-19  kilit -> var olmayan yardimci         -> BUILD DUSTU (dongu dogru davrandi)
MUT-19b kilit anahtari sabitlestirildi        -> BUILD DUSTU
MUT-19c no-tracking -> tracked okuma          -> TAM 1 (F3_IADE_KALEM_BASINA_KILITLI...)
MUT-20  CsvSatirEnCok 5000 -> 3               -> 0 KIRMIZI [PIN BOSLUGU -> deger AYRICA pinlendi]
MUT-20b satir siniri `>` -> `>=`              -> TAM 1 (R6_8a - off-by-one YAKALANDI)
MUT-21  ShipmentManager Shipped guard'i kalkti-> TAM 1 (K5 kaynak pini, F4'te genisletildi)
MUT-22  marka uzunlugu devre disi             -> TAM 1 (K7_MARKA_UZUNLUGU)
```

Her mutasyon: **(a)** `git diff --numstat` ile YAZILDIGI · **(b)** build exit 0 ·
**(c)** geri alma SONRASI `git status --porcelain` = 0 dogrulandi.
Geri alma **YALNIZ olcum yedeginden** — `git checkout` / `git stash` KULLANILMADI.
Dongu, agac KIRLIYSE **calismaz**; is bu yuzden mutasyondan ONCE lokal commit'e alindi (MK-4).

### 8.1 KAYNAK PINLERININ KIRMIZI-ONCE KANITI (mutasyon YERINE git blob)

```
"var duplicate = await _orderDal.GetAsync(o => o.request_id == dto.request_id)"
    3095568 -> 1   ·   HEAD -> 0   ·   NEG capa ZZZduplicate 3095568 -> 0
"order.status = (byte)OrderStatusEnum."   DEPO GENELI (yorumsuz)
    3095568 -> 8   ·   HEAD -> 4
"order.status = (byte)OrderStatusEnum."   YALNIZ OrderManager.cs
    3095568 -> 4   ·   HEAD -> 0
SepetAnahtari · GuestCheckoutManager: HAM metinde 2 (ikisi de YORUM) · yorumsuz metinde 0
```

Son satir, kural-uyum denetcisinin **en guclu pozitif kanit**i saydigi olgudur: `Should().Be(0)`
asserti, yorum ayiklama olmasaydi **YANLIS KIRMIZI** verirdi — yani `KodSatirlari` dekoratif
degil, **testin gecmesi ona bagli** (MK-8 EK / GF-5 dersi).

### 8.2 "TEK KAPI" IDDIASININ DURUST SINIRI

Depo genelinde `OrderStatusMachine.IsValidTransition` cagri sitesi **7**. `order.status`a
yazan **4** site kaldi: `IyzicoPaymentManager` :429/:485 (bu dalgada terminal kapisi eklendi)
ve `ShipmentManager` :65/:118 — ikincisi **GF-6 ONCESINDEN** makine korumali (:50 ve :116).
Yani semantik iddia depo genelinde DOGRU; `DurumYaz` ise `OrderManager`a OZELDIR.
Pin ilk yazimda `ShipmentManager`i taramiyordu — **PIN KAPSAMI boslugu, guvenlik boslugu
DEGIL** — ve F4'te kapatildi (MUT-21 TAM 1).

---

## 9. SUIT VE KAPILAR

```
Release build            : 0 Hata / 1651 Uyari
Category=Sql             : 411/411
TAM SUIT x3 ARDISIK      : 806/809 · 806/809 · 806/809   (ucuncusu DIVISIMA_TEST_DB=FIN3)
Uc kosumda da AYNI uc kirmizi: OrderEndpointTests.PlaceOrder_{ValidCart,InsufficientStock,
  ConcurrentRequests} = BILINEN Docker uclusu (yerelde Docker YOK), her biri 1 ms
Bicim kapilari           : whitespace exit 0 · style exit 0
  AYIRT ETME KANITI      : bilerek bozuk dosya -> exit 2 · temizlenince -> exit 0
Yeni test                : 655 - 632 = 23 (ilk tur) + F-turu ekleri
```

---

## 10. DENETCILER (MK-4b: ayri worktree + ayri test DB + ayri scratchpad)

**L3 DAVRANIS (ilk tur):** DOKUZ REPRO'nun DOKUZU DA **ONAY**, itiraz YOK. Kendi gecici test
sinifini yazdi, `GuvenlikFix6SozlesmeTests`i HIC ACMADI (cift-korluk). Iki uretim
mutasyonuyla ayirt etme kaniti uretti: sahiplik yuklemi etkisiz -> **T1-B1 BIREBIR geri
geldi**; terminal kapisi etkisiz -> **T4/S-1 BIREBIR geri geldi**. Misafir yolu **86/86**
yesil. **TEK GOZLEM: S-1** (bolum 6).

**RAPOR DENETCISI:** **UYDURULMUS OLCUM BULUNMADI.** Uc DUR onerisi (rapor zemini bayat ·
T4 yonlendirmesi · LAUNCH BLOKER durumu eksik), dort duzeltme (`x3` gercekte x2 · tmp cift
sayimi · capa kapsami · "dort hata" gercekte bes), bir eksik bolum (DALGA ICI DENETIM).
**HEPSI KABUL EDILDI.** Pin durustlugu: `R6_8a` ADI YALAN SOYLUYORDU (3 satir, sinir HIC
denenmiyordu) · `R6_7` pozitif kontrolsuz · `K5` pini `ShipmentManager`i okumuyor · D7'nin
uc kapisi pinsiz. **Dordu de F4'te kapatildi.**

**KURAL-UYUM DENETCISI:** SEKIZ BASLIGIN SEKIZI DE **ONAY**; kod tarafinda kural ihlali
BULAMADI. Diff kapsam beyan/olcum `comm -3` = **0 satir**; alti yasak yuzey (frontend ·
Seller · InvoiceManager · Migrations · CLAUDE.md · .claude) **hepsi 0**. Migration **0**.
Sir **0** (1722 diff satiri + 167 scratchpad dosyasi, metin+ikili). Satir numarasi atfi:
543 yorum satirinda **0**. Gecici prob hic commit edilmemis. Uc bulgu: **B-1** denetim
hedefi bayat (KAPATILDI) · **B-2** tarif kapsam listesi eksigi · **B-3** `KodSatirlari`
yalniz satir basi `//` ayikliyor (LATENT; blok yorum 7/7 dosyada 0).

**MALIYET:** 4 denetci · ~1M jeton · ~250 arac cagrisi · ~1 sa. **Dordunun dordu de gercek
kusur buldu** — kalibrasyon kaydi.

---

## 11. CC (ASISTAN) HATALARI — BES

1. **R6_1 pini BEDAVA DOGRUYDU.** MUT-1'de yakalandi (0 kirmizi): B kendi urununu
   gonderiyordu, reddi ureten sey SAHIPLIK degil SEPET FARKIYDI. Sepet AYNI yapildi.
2. **`CancelItem` yorumu "on kosul KALDIRILDI" diyordu; KALDIRILMAMISTI.**
   YORUM != OLCUM ailesinin DORDUNCU vakasi.
3. **`Program.cs` yorumuna `MapControllers().RequireAuthorization()` ifadesi yazildi.**
   GF-1/K5a pini gecis sayisini sayiyor ve 3 gorup KIRMIZI verdi — pin DOGRU davrandi
   (MK-8 EK dersi).
4. **BAYAT IKILI (IKI KEZ).** (i) Silinen K8 probu Debug ciktisinda KALDI (Release build
   Debug'i tazelemez); suit 805 gorundu, aritmetikle yakalandi (23 yeni test, +25 gorundu).
   (ii) F-turunda `dotnet build | grep` zincirinde grep exit 0 dondugu icin **BUILD DUSTUGU
   HALDE** "BUILD OK" yazildi ve `--no-build` test ESKI ikiliyle kostu. Ikisi de ayni aile:
   **BUILD CIKIS KODU AYRICA KONTROL EDILIR.**
5. **`[RequestSizeLimit]` yorumu "bu pin var" diyordu, pin YOKTU.** Pin yazildi (MUT-15).

---

## 12. MERKEZ HATALARI (kayit, kalibrasyon icin)

1. **D3 premisi:** "PaidOrderSpec'te `Expression` yazilir" varsayimi — `Divisima.Core`,
   `Divisima.Entity`yi GOREMEZ (ters yon dongu). EF yuzu AYRI bir dosya gerektirdi.
2. **"7 tuketici kendiliginden duzelir":** kodla uyusmuyordu — 17 site / 7 sinif, 14'u
   kapsam DISI, hicbiri `payment_type` okumuyor; ayrica anilan `ProductReviewManager` bu
   kurali HIC kullanmiyor.
3. **Tarif kapsam listesi eksigi:** `Messages.cs` ve `AutofacBusinessModule.cs` adiyla
   sayilmamisti; ikisi de KACINILMAZ (yalniz EKLEME: 13/0 ve 3/0) — kural-uyum denetcisi
   "ihlal degil, TARIF EKSIGI" dedi.
4. **T5-0 tekrari:** tarif "rig ikilisi HEAD ile eslesir" diyordu; **rig HIC YOKTU**
   (goz1 dizini yok, port yok). Kanal varsayimi olculmeden yazilmisti.
5. **F3'un "aktif iade 409" kurali** mevcut bir pini kiriyordu (bolum 7).
6. **F1'in kupon siteleri** yeni bir [PARA] bypass'i aciyordu (bolum 4.3).

---

## 13. BILINEN / KABUL EDILMIS RISK (DURUM sutunlu — SDP 1.12.8)

| # | Kalem | DURUM |
|---|---|---|
| 1 | Raporlama siteleri (Dashboard · Merchandising · Recommendation · Seller) ESKI `PaidStatuses` kuralinda — COD siparisi ciro/siralama/oneride hala `Confirmed`da sayilir | **ACIK** (GF-7) |
| 2 | Terminal siparise gelen odemenin IADESI **ELLE** — otomatik iade `RefundManager` uzerinden gider, kapsam disiydi | **ACIK** (GF-7) |
| 3 | `health` uclari BILINCLI anonim (`AllowAnonymous` ile ISARETLI) — orkestratör probe'lari kimlik tasimaz | **BAGLAYICI** |
| 4 | T4-F2 (kayip guncelleme) LAUNCH BLOKER olcutu ISTISNASI — gerekce ve tetikleyici bolum 5.1 | **ACIK** (GF-7 ilk kalem) |
| 5 | `first_order_only` kuponu ESKI kuralda — COD `Confirmed`da "tamamlanmis siparis" sayilir (self-harm, somuru yonu YOK) | **ACIK** (GF-7) |
| 6 | `KodSatirlari` yalniz satir basi `//` ayikliyor — satir-sonu ve blok yorum ayiklanmiyor (bugun somurulmuyor: blok yorum 7/7 dosyada 0) | **ACIK** |
| 7 | `SignalR "admins"` alarmi BOS GRUBA yayin yapiyor (`JoinAdminGroup()` cagirani YOK) — AV-2'den devir | **ACIK** |

---

## 14. LAUNCH GO/NO-GO

`53·AV-3`in **NO-GO 3**'unden:

```
T1-B1 uye request_id replay'i    -> KAPANDI (K1)
T1-B2 adressiz siparis           -> KAPANDI (K2)
T1-B4 COD parasiz "odenmis"      -> KAPANDI (F1 - DAR kapsam, bolum 4.2/4.3)
```

Ayrica **T4-F1 (cift para iadesi) KAPANDI** (F3, K8 onces/sonrasi 41-35 -> 0).
**T4-F2 GF-7'ye gerekceli istisnayla devredildi.**

**SIRADAKI IS: LAUNCH GO/NO-GO TURU.**
