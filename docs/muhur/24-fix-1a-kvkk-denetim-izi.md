# FIX-1A - KVKK & DENETIM IZI EKSENI (25 Agustos 2026)

Zemin `d434906`. FAZ 1'in olcum raporundan gelen UC kalem: **F1** (+F10/F11/F12 katlandi),
**F2**, **F3**. Diger bulgulara (F4, F5, F6, F7, F8, F9, F13) BU TURDA DOKUNULMADI.
Migration/sema degisikligi YOK.

## ADIM 0 - ON OLCUM (kod yazmadan)

**(a) `audit_logs`in KENDISI denetlenmiyor - redaksiyon kendi kendini beslemez.**
`AuditInterceptor` iki ayri kapiyla disliyor: `entry.Entity is AuditLog` ve
`_ignored = { AuditLog, OutboxMessage }`. Yani redaksiyon guncellemeleri YENI audit satiri
URETMEZ. F3 bu kanit uzerine kuruldu.

**(b) NEGATIF `entity_id` F3'U BLOKE ETMIYOR - KANITLANDI, VARSAYILMADI.**
```
action    n     changes_NULL  changes_DOLU  entity_id_NEGATIF  entity_id_POZITIF
Added     1226  1226          0             1226               0
Modified   397  0             397           0                  397
Deleted      0  -             -             -                  -
```
`SerializeChanges` `Modified` disinda `null` donuyor; dolayisiyla PII tasiyan HER satir
`Modified` ve entity_id'si GERCEK. `Deleted` 0 (fiziksel silme interceptor'a hic ulasmiyor -
`DataRetentionJob` `ExecuteDeleteAsync` ile change-tracker'i atliyor).

**(c) MUSTERININ AUDIT AYAK IZI - EKSENLER ve SIRA KARARI.**
`changes` DOLU olan tablolar ve alan adlari olculdu:
```
Customer        78 satir (max 2286 bayt)  password_hash/salt, two_factor_secret, ...
UserSession     33 satir                  refresh_token, ip_address, device
CustomerDevice   3 satir                  device_token
Address         14 satir                  full_name, phone, full_address, title, city, district, zip_code
Order/Invoice/CartItem/Payment            -> MUSTERI PII'SI TASIMIYOR (id/tutar/durum/
                                             siparis-fatura no/sirket unvani/vergi no)
`user_id` ekseninin bu DORT tablo DISINDA kalani: 0 satir
```
Redaksiyon ekseni bu yuzden **ENTITY**tir; ticari kayda (yasal saklama) DOKUNULMAZ.
**SIRA: ANONIMLESTIR -> SONRA REDAKTE ET.** Gerekce: her anonimlestirme `UpdateAsync`i
interceptor uzerinden YENI bir audit satiri uretir ve o satirin `old` degerleri TAM DA
silinen PII'yi tasir. Redaksiyon once kosulsaydi silme isleminin KENDI izi redakte
edilmemis kalirdi - FAZ 1'de olculen zararin ta kendisi. Id'ler her iki sirada da
cozulebilir (olculdu: silinmis hesapta `addresses`/`user_sessions`/`customer_devices`
satirlarinin `customer_id`si KORUNUYOR).

**(d) PIN KANALI - VAKUM KANITLANDI ve KANAL DEGISTIRILDI.**
`Program.cs:182` DbContext'i `.AddInterceptors(sp.GetRequiredService<AuditInterceptor>())`
ile kaydediyor. Test fabrikalari ise `DbContextOptions<DivisimaDbContext>` kaydini KALDIRIP
duz `UseSqlServer(ConnStr)` ile yeniden kuruyor - interceptor'i DUSURUYOR. Bu desen
`DalgaBFactory`ye ozgu DEGIL: **42 test dosyasi** ayni sekilde yazilmis. Yani
**`AuditInterceptor` bugune kadar HICBIR test host'unda kosmadi.** F2/F3 pinleri duz bir
fabrikaya yazilsaydi VAKUM olurlardi. Kanal `AuthorizationIdorTests.IdorFactory`de
DEGISTIRILDI (interceptor geri baglandi); 42 fabrikanin genel duzeltmesi **[HAVALE->FAZ 6]**.

**(e) MEVCUT PINLERIN BAGIMLILIGI.** `audit_logs`a dokunan tek test dosyasi
`DalgaBOperasyonTests` ve satirlari KENDISI tohumlayip yalnizca DTO ALAN ADLARINI assert
ediyor - interceptor ciktisina bagli DEGIL. `DeleteAccount` cagiran iki pin
`AuthorizationIdorTests`te; ikisi de `deleted_` e-posta kalibi ve bos parola ozeti
bekliyor - konsolidasyon yonu bu yuzden `AccountManager` tarafi secildi (asagi).

## F1 - TEK SILME UYGULAMASI (konsolidasyon)

**KOK DERS (tarihli): ayni kuralin IKINCI KOPYASI, ALTINCI KEZ.** Onceki ornekler:
B10 (onay yan etkileri kart disi yollarda yoktu), D5 (rate limit kovalari iki yerde),
K7 (yol->kova eslesmesi oznitelikle ayrisiyordu), Faz 0/K1 (olu ETag oneki),
D-SEMA (sema iki kaynaktan). Bu yuzden cozum "eksik kopyayi da duzeltmek" DEGIL,
**KOPYAYI KALDIRMAK** oldu.

- `AuthManager.DeleteAccount` govdesi **SILINDI**; `IAuthService.DeleteAccount` de kaldirildi
  (derleme, baska cagri yeri OLMADIGININ kanitidir - Sprint 8 madde 11 kalibi).
  Yan etki: `AuthManager`in `ICacheService` bagimliligi OLU kaldi ve kaldirildi.
- `AuthController.DeleteAccount` artik `IAccountService.DeleteAccount`e delege ediyor.
  **ROTA DEGISMEDI** - `frontend/api-client.js:258`in cagirdigi `/api/auth/account`
  calismaya DEVAM EDIYOR, yalnizca davranisi `/api/Account/delete` ile BIRLESTI.
- **YON SECIMI KAYNAK OLCUMUYLE:** `AccountManager` secildi cunku (a) dogru adres kaskadi
  ZATEN oradaydi, (b) iki mevcut pin o ucun davranisini sabitliyor, (c) `IAddressDal` zaten
  enjekte. Eksik uc parca (SecurityEvent / cihaz / city-district-zip) oraya TASINDI.

**KONSOLIDE SILME NE YAPAR (hepsi TEK TRANSACTION icinde):**
musteri anonimlestirme -> adres defteri (**city/district/zip_code DAHIL - F11**) ->
cihaz baglari (**device_token YOK EDILIR - F10**) -> oturum iptali -> **denetim izi
redaksiyonu (F3)** -> `SecurityEvent(AccountDeleted)` (**F12 - artik HER IKI YOLDAN DA**).
Cache dusurme transaction'in DISINDA (geri alinabilir bir kaynak degil; rollback'te
gereksiz bir DB okumasina mal olur, tersi silinen hesaba 60 sn erisim demektir).

**F10 KARARI OLCUME DAYALI:** `is_active=false` YETMEZ - `device_token` KALICI bir cihaz
tanimlayicisidir ve deger durdukca silinen hesap bir cihazla eslestirilebilir kalir. Satir
SILINMIYOR (denetim/gecmis korunur), token `deleted-{Guid:N}` ile degistiriliyor.
Guid ZORUNLU: `IX_customer_devices_device_token` FILTRESIZ UNIQUE'tir; sabit bir yer tutucu
ikinci silmede cakisir ve silme ucunu 500'e dusururdu.

**PAROLA ALANI TEK BICIME INDI:** `Array.Empty<byte>()`. Gerekce olculdu -
`HashingHelper.VerifyPasswordHash` `CryptographicOperations.FixedTimeEquals` kullaniyor ve
uzunluk farkinda GUVENLE `false` donuyor, yani bos ozet hicbir parolayla dogrulanamaz.
Rastgele ozet (AuthManager ikizinin yaptigi) DB'de ve denetim izinde gecerli bir kimlik
bilgisinden AYIRT EDILEMEZ; bos dizi "kimlik bilgisi YOK" der.

**STEP-UP PENCERESI HIZALANDI (10 dk).** Once `/api/Account/delete` 30, `/api/auth/account`
10 istiyordu. Iki rota ayni isi yapiyorsa ayni kapiyi da istemelidir - yoksa konsolidasyon
yarim kalir ve saldirgan gevsek olani secer. **Yeni deger UYDURULMADI**, iki mevcut
sozlesmenin SIKI olani alindi.

## F2 - DENETIM IZI MASKELEME

**TEK KAYNAK: `Divisima.Core/Security/DenetimGizlilik.cs`.** Iki liste + kapsam:
- `SirAlanlari` -> denetim kaydina **HIC GIRMEZ** (deger de, uzunluk da, ozet de, kirpilmis
  hali de). Degistiyse yalnizca sabit `[REDACTED]` isareti yazilir. Kapsam olcumle belirlendi:
  `password_hash`, `password_salt`, `two_factor_secret`, `two_factor_code`,
  `email_verification_token`, `password_reset_token`, **`refresh_token`** (UserSession, 33
  satirda olculdu), **`device_token`** (CustomerDevice), **`token`** (Payment - depo bunu zaten
  `KanitMaskesi` ile maskeliyor; denetim izinde ciplak birakmak ayni kurali bir kanal oteden
  delerdi).
- `KisiselAlanlar` -> normal yazilir, SILMEDE redakte edilir.
- `RedaksiyonTablolari` -> `Customer / Address / UserSession / CustomerDevice`.
Eslesme **OrdinalIgnoreCase** (bolum 6c: alan adi MAKINE dizgesidir; tr-TR pinli uygulamada
`ToLower()` `I` -> `ı` yapar ve `IpAddress` gibi bir ad eslesmeden KACARDI).

**`changes` ARTIK YALNIZ GERCEKTEN DEGISEN ALANI TASIR.** Eski kod `p.IsModified`
filtreliyordu ve NIYETI dogruydu; ama `EfEntityRepositoryBase.UpdateAsync` ->
`Context.Set<T>().Update(entity)` cagiriyor ve EF'in `Update()`u varligi TUM ALANLARIYLA
Modified isaretliyor. Sonuc 35 alanlik tam-varlik payload'iydi (olculdu: Customer
satirlarinda 2286 bayta kadar). Olcut artik `OriginalValue != CurrentValue` - yani DAL'in
nasil kaydettiginden BAGIMSIZ. `byte[]` alanlar `SequenceEqual` ile karsilastiriliyor
(referans esitligi `row_version` gibi alanlari her kayitta "degismis" gosterirdi).
OLCULEN SONUC: change-password'un urettigi payload **35 alan -> 2 alan**.

**FAZ 6'YA DOKUNULMADI:** negatif `entity_id`, `Added` satirlarinin bos `changes`i,
`user_id` NULL'lari ve 42 fabrikanin interceptor'siz kaydi BU COMMIT'TE DEGISMEDI.

## F3 - SILMEDE DENETIM IZI REDAKSIYONU

`Divisima.Core/Security/DenetimRedaksiyonu.cs`. **SATIR SILINMEZ** - id / action / entity_id /
created_at / user_id ve **ALAN ADLARI** korunur; yalnizca DEGERLER isaretle degistirilir.
Boylece "su tarihte su alan degisti" izi ayakta kalir, "neydi / ne oldu" gider.

- Kapsam ADIM 0(c)'deki dort eksen; ticari kayit DISARIDA (PII tasimadigi olculdu).
- Sira: **anonimlestirme SONRASI** (gerekce ADIM 0(c)'de) - boylece silme isleminin KENDI
  urettigi audit satirlarini da kapsar.
- **Redaksiyon basarisizsa silme COMMIT EDILMEZ**: tamami `IUnitOfWork.ExecuteInTransactionAsync`
  icinde. Manuel `BeginTransaction` DEGIL - `Program.cs`in kendi notu `EnableRetryOnFailure`
  acilirsa manuelin REDDEDILECEGINI soyluyor.
- Ayristirilamayan / beklenmedik bicimli payload GECIRILMEZ, tamami isarete cevrilir.
  Gerekce: KVKK yolunda "anlayamadim, oldugu gibi biraktim" kabul edilemez; ama tek bozuk
  satir yuzunden silmeyi KALICI olarak bloke etmek de dogru degil - fail-safe yon PII'nin
  GITMESIDIR.

## PINLER (`AuthorizationIdorTests`, +4 test / 5 vaka - YENI VERITABANI ACILMADI)

10d794d dersi geregi yeni SQL sinifi ACILMADI; pinler `DeleteAccount` pinlerinin zaten
bulundugu sinifa eklendi ve o sinifin fabrikasi interceptor'li hale getirildi.

- `SILME_HANGI_UCTAN_GELIRSE_GELSIN_TUM_PII_KANALLARINI_Kapatir` (**Theory: iki rota**) -
  musteri + adres (city/district/zip DAHIL) + cihaz (token YOK EDILMIS) + oturum + SecurityEvent
  (TAM 1) + cache (eldeki token ANINDA 401). Vakum kirici: silmeden ONCE her kanalin
  GERCEKTEN dolu/acik oldugu ayri ayri assert ediliyor.
- `DENETIM_IZI_SIR_ALANI_TASIMAZ_ve_YALNIZ_DEGISEN_ALANI_Tasir` - **interceptor'li host**.
  Vakum kirici: denetim satiri GERCEKTEN uretilmis olmali. Cift-anlam kirici: sifre degisimi
  `email`/`name`/`loyalty_points` alanlarini payload'a KOYMAMALI.
- `SILME_SONRASI_DENETIM_IZINDE_PII_KALMAZ_ama_SATIR_SILINMEZ` - vakum kirici (silmeden once
  redakte edilmemis deger BULUNMALI), cift-anlam kirici (satir sayisi AZALMAMALI + `action`
  ve `entity_id` korunmali) + ham metin kontrolu (acik adres ve ad-soyad HICBIR satirda
  gecmemeli).
- `REDAKSIYON_YALNIZ_SILINEN_MUSTERIYE_DOKUNUR_BASKASININ_IZI_BOZULMAZ` - **IZOLASYON**.
  A silinir, B SILINMEZ; B'nin denetim izi ONCE/SONRA **id -> `changes` haritasi olarak
  BIREBIR** karsilastirilir. **OLCUT SATIR SAYISI DEGIL ICERIKTIR** - redaksiyon zaten satir
  silmiyor, dolayisiyla sayi esitligi ZAYIF bir olcuttur: degeri isaretle degistiren bir
  tasma sayiyi hic degistirmeden B'nin PII'sini yok ederdi ve sayi bazli bir pin bunu
  GORMEZDI. Ayni olcut `Address` ve `CustomerDevice` icin de uygulanir (B'nin adresi
  `full_name`/`full_address`/`city`/`district`/`phone` ile ve `is_active=true` olarak
  DURUYOR; B'nin `device_token`i YOK EDILMEMIS). IKI VAKUM KIRICI: (1) B'nin izi silmeden
  ONCE gercekten redakte edilmemis kisisel deger tasiyor olmali, (2) AYNI KOSUMDA A'nin
  adi denetim izinden GITMIS olmali - yoksa redaksiyon HIC calismasa da pin yesil kalirdi.

**PIN ADI DUZELTILDI (davranis degismedi):** `DeleteAccount_StepUpISTENMEZ_...` ->
`DeleteAccount_STEP_UP_TAZE_TOKENLA_GECER_...`. Eski ad YANLIS BIR SOZLESME IDDIA EDIYORDU:
uc `[RequireRecentAuth]` TASIYOR; test geciyordu cunku `TestAuthHelper` hemen once giris
yapiyor ve `auth_time` TAZE. Yorumda ayrica NE OLCMEDIGI yazildi (pencere DOLDUGUNDA
reddedildigini olcmez).

**KIRILAN PIN YOK.**

## DIS KONTROLU + 5. KONTROL

**DIS (TAM KAPSAMA, orneklem yok):** DORT yeni testin her birinde bir assert ters cevrildi ->
**4 AYRI ISIMLI KIRMIZI** (Theory iki vaka verdigi icin toplam 5 kirmizi). Geri alindi, 19/19.
(Izolasyon pini ayri bir turda ters cevrildi ve ADIYLA kirmizi verdi.)

**5. KONTROL - UC URETIM MUTASYONU** (her birinde (a) dosyaya indi mi, (b) temiz build,
(c) kirmizi yoksa ONCE "uygulanmadi" suphesi elenir):

| Mutasyon | Sonuc | Uretilen once-durum |
|---|---|---|
| **M1** kara listeden `password_hash` cikarildi | **ONCE 0 KIRMIZI -> PIN ZAAFI** (asagi); pin duzeltildikten sonra **TAM 1 KIRMIZI** | denetim izinde ciplak parola ozeti |
| **M2** adres anonimlestirmesi kaldirildi | **IKI UCTAN DA KIRMIZI** (Theory 2 vaka) + mevcut kaskad pini = 3 | F1'in olculen once-durumu |
| **M3** denetim izi redaksiyonu kaldirildi | **TAM 1 KIRMIZI** | F3'un olculen once-durumu |
| **M4** redaksiyon sorgusunun MUSTERI FILTRESI kaldirildi (`if (!bizeAit) continue;`) | **TAM 1 KIRMIZI** (izolasyon pini) | redaksiyonun eksen disina tasmasi - B'nin izi de silinirdi |

**M1 BIR PIN ZAAFI ORTAYA CIKARDI ve DUZELTILDI (durust kayit).** Ilk yazimda pin bir alanin
sir olup olmadigini `DenetimGizlilik.SirMi`e - yani TEST ETTIGI KAYNAGA - soruyordu. Alan
kara listeden cikarilinca assert onu ATLIYOR ve mutasyon **0 kirmizi** veriyordu. Kuralin
(c) adimi geregi once "mutasyon uygulanmadi" ihtimali elendi ((a) marker dosyada, (b) build
0 hata), sonra pin yeniden yazildi: sir alanlari listesi artik PIN'IN KENDISINDE, ayrica
kaynaktan TAMAMEN BAGIMSIZ bir assert eklendi (musterinin GERCEK parola ozeti/tuzunun
base64'u hicbir `changes` satirinda gecmemeli). Mutasyon tekrarlandi -> TAM 1 KIRMIZI.
Bu, 5. kontrolun bir pini eledigi IKINCI vaka (ilki D2).

Tum mutasyonlar geri alindi; kod tarafinda `MUTASYON-M*` izi **0 dosya**.

## DEFTER

**RATE LIMIT GERCEGI (checklist + [HAVALE->FAZ 8]).** `RateLimit` bolumu
`appsettings.json` ve `appsettings.Development.json`in **HICBIRINDE YOK**; yalniz
`.example.json`da duruyor. Bolum yoksa `RateLimitPolitikasi.Olustur` sessizce KOD
VARSAYILANINA duser (auth 10 / payment 10 / global 100) ve checklist'in "esikler ayarlandi"
maddesi YINE KARSILIKSIZ kalir. D5 iki yolun AYRISMASINI kapatmisti; bu madde ayarin VAR
OLDUGUNU kapatir. Iki checklist maddesi eklendi.
NOT: FAZ 1 prompt'undaki "auth kovasi 5/dk" onculu de bu yuzden bugun GECERLI DEGIL -
D5'ten sonra deger yapilandirmadan geliyor ve varsayilan **10**.

**BU TURDA DOKUNULMAYANLAR (devir listesi):**
- **F4** - erisim jetonu iptali YOK; `ITokenBlacklist.RevokeAsync` uretimde SIFIR cagri.
  logout / change-password / G1 zincir iptali sonrasi access token 15 dk daha calisiyor.
- **F5** - profil guncellemede `birthdate` sessizce siliniyor, `phone=""` kabul ediliyor.
- **F6** - bildirim tercihi `ConsentRecord` yazmiyor; kayittan sonra pazarlama rizasi
  VERILEMIYOR (ekran "acik" derken `MarketingGate` kapali).
- **F7** - capraz hesap cihaz kaydi 500 veriyor ve mesru sahibin push'unu kalici olduruyor.
  **MERKEZ KARARI (uygulanmadi, kayit):** token basina TEK SATIR; rebinding TEK
  TRANSACTION'da `customer_id`yi GUNCELLER (pasifle+ekle DEGIL) + `SecurityEvent` yazar.
  **Migration YOK** - `IX_customer_devices_device_token` oldugu gibi kalir.
- **F8** - step-up (`RequireRecentAuth`) refresh ile sinirsiz tazeleniyor; calinmis refresh
  cerezi geri alinamaz hesap silmeye yetiyor. (FIX-1A pencereyi 10 dk'ya hizaladi ama bu
  bosluk ACIK.)
- **F9** - reddedilen adres istegi cagiranin kendi varsayilanini dusuruyor.
- **F13** - soft-delete edilmis adreste `is_default=True` kaliyor.

## YEREL DOGRULAMA

333/333 `Category=Sql` · tam suitte **552 basarili / 555** (kirilan 3'un UCU DE Docker'li
`OrderEndpointTests` - yerelde Docker kapali, CI'da yesil) · Release **0 hata** ·
whitespace **exit 0** · style **exit 0**.

Taban FIX-0'da 550 idi; +5 yeni vaka (Theory iki rota + uc Fact).

**SURECTE YASANAN (kayit):** `dotnet format style` yine `IMPORTS` hatasi verdi -
`Divisima.Core.DataAccess` using'i `Divisima.Core.Security`ten SONRA eklenmisti. Bu depoda
UCUNCU kez (Dalga A ve A2-FIX'te de olmustu). `dotnet format style --include <dosya>` ile
duzeltildi ve iki kapi da yeniden dogrulandi.

## KAPSAM SAPMASI - STEP-UP PENCERESI MERKEZE SORULMADAN INDIRILDI (26 Agustos 2026)

**KAYIT: bu bir KURAL IHLALIDIR, yon kabul edildi ama surec kayda geciyor.**

FIX-1A'da hesap silme step-up penceresi `/api/Account/delete` ucunda **30 dk -> 10 dk**
indirildi. Bu bir **DAVRANIS DEGISIKLIGIDIR** ve **F8'in alanina girer** (F8 = step-up'in
refresh ile sinirsiz tazelenmesi); F8 o turun kapsaminda **DEGILDI** ("BU TURDA YOK -
dokunma" listesindeydi). Karar **merkeze SORULMADAN** verildi ve uygulandi.

Gerekce dogru ama YETERSIZDI: konsolidasyon iki rotayi tek uygulamaya indirdigi icin iki
FARKLI sozlesme (30 ve 10) arasinda secim yapmayi ZORUNLU kildi ve siki olan alindi; yeni
bir deger de uydurulmadi. **Yapilmasi gereken:** secimi merkeze goturmek, cevabi beklemek.

**KALICI KURAL:** konsolidasyonda iki sozlesme CAKISIRSA hangisinin kalacagi **MERKEZDEN**
sorulur. "Ikisinden birini secmek zorundaydim" bir yetki degildir - tam tersine, secim
gerektigi an merkeze gitmenin ta kendisidir. Ayni sey bir kalemi konsolide ederken BASKA
bir bulgunun alanina girildiginde de gecerlidir.

Yon kullanici tarafindan KABUL EDILDI (26 Agustos 2026); kayit surec disiplini icin duruyor.

## REDAKSIYON N+1'IN BUGUNKU SINIRI - OLCULDU (26 Agustos 2026)

`AccountManager.DenetimIziniRedakteEtAsync` satir basina bir `UpdateAsync` (dolayisiyla bir
`SaveChanges`) yapiyor ve tumu TEK transaction icinde. "Uzun gecmisli bir hesapta silme
pratikte imkansiz hale gelir mi" sorusu SAYIYLA yanitlandi.

OLCUM (dev veritabani, eksen FIX-1A ile AYNI: entity + entity_id, `user_id` ekseni DEGIL -
yani gercekten silmede dolasilacak satir kumesi):

```
EN AGIR 5 HESAP (redaksiyon kapsamindaki satir sayisi)
  customer_id  toplam  Customer  Address  UserSession  CustomerDevice
        66        17       7        4          3             3
        10        12       2        0         10             0
        23        11       1        1          9             0
        12         5       2        1          2             0
        35         5       3        0          2             0

DAGILIM: 54 hesap | en agir 17 | en hafif 1 | ortalama 2,37 | toplam kapsam satiri 128
```

**SONUC: en agir hesap 17 satir - kullanicinin koydugu 100 esiginin COK ALTINDA.**
Toplu guncellemeye cevirme KARARI ALINMADI; kalem **defter kaydiyla KAPANDI**.

**DURUST SINIR (olculmemis bir iddia yazilmadi):** 17 sayisi **bu dev veritabaninindir**.
Buyume surucusu `UserSession/Modified` satirlaridir - her refresh rotasyonu bir tane uretir
(en agir ikinci hesapta 12 satirin 10'u bu). Uretimde yillarca kullanilan bir hesapta bu
sayi cok daha yuksek olabilir; **olculmedi**. Yeniden bakma tetikleyicisi: gercek trafikte
tek bir hesabin kapsam satiri 100'u asarsa.

**YAN OLCUM - EKSEN COZULEMEYEN (YETIM) AUDIT SATIRLARI: bugun 0.**
```
UserSession (oturum satiri artik YOK)   : 0
Address / CustomerDevice / Customer     : 0
```
Ama bu YAPISAL OLARAK GECICIDIR: `DataRetentionJob` `user_sessions` satirlarini 90 gun
sonra siliyor, `audit_logs` satirlarini ise HIC silmiyor. Oturum satiri gittiginde
redaksiyonun eksen cozumu (`user_sessions` JOIN'i) o audit satirina **ULASAMAZ** ve
o satir redakte EDILMEDEN kalir. Bugun 0 cikmasinin sebebi dev veritabaninin 90 gunluk
pencereyi henuz doldurmamis olmasidir - kusur yok demek DEGILDIR.
**[HAVALE->FAZ 8]** - bu turda DOKUNULMADI.

## [HAVALE->FAZ 8] DataRetentionJob DENETIM IZI BIRAKMIYOR

`DataRetentionJob` uc tabloyu (`user_sessions` / `outbox_messages` / `security_events`)
`DeleteWhereAsync` -> `ExecuteDeleteAsync` ile siliyor. Bu cagri EF change-tracker'i
**ATLAR**, dolayisiyla `AuditInterceptor` HIC calismaz ve **silinen hicbir sey denetim
izine dusmez**.

KANIT (FIX-1A on olcumu, dev veritabani): `audit_logs`ta `action='Deleted'` satir sayisi
**0** - depoda bugune kadar denetim izine dusmus TEK BIR silme kaydi yok.

Iki ayri sonucu var:
1. Saklama isinin ne sildigi geriye donuk **denetlenemiyor** (kim/ne zaman/kac satir).
2. Yukaridaki N+1 kaydinda yazili yetim-satir riski buradan doguyor.

Bu turda **DOKUNULMADI**; kalem FAZ 8'e (dagitim/altyapi) havale edildi.

## FIX-1A KAPANIS KAYDI (26 Agustos 2026)

**KANIT SHA: `a244160`** - her iki workflow tamamen yesil, adim + annotation duzeyinde
dogrulandi.

```
CI - Build & Test  run 32899208023  event=push  head_sha=a244160  SUCCESS  (6dk04sn)
Security CI        run 32899208038  event=push  head_sha=a244160  SUCCESS  (3dk45sn)

format-check     10/10 SUCCESS  (whitespace + style + migration SENKRON - ucu de ZORUNLU)
build-and-test   16 adim: 15 SUCCESS, 1 skipped (TESHIS - yalniz if: failure() kosar)
tests            13 adim: 12 SUCCESS, 1 skipped (TESHIS)
codeql 11/11 · dependency-scan 10/10 · secret-scan 5/5
  Gitleaks (secret taramasi) SUCCESS  <- ADIM SONUCUNDAN (bolum 7); "Leaks detected" 0
ALTI JOB'DA failure SEVIYELI ANNOTATION: 0
TestDbKurulum 1807 yeniden denemesi: HIC ATESLEMEDI (0) - iki test job'inda da
```

**YENI UYARI URETILMEDI - ama kanit AILE DUZEYINDE KAPANMADI.** Toplam 39 == 39 ve dort
ailenin dordu de birebir esit; buna karsilik `Job|aile|Path` duzeyinde kume farki BOS
CIKMADI (4 "yeni" / 4 "kaybolan"). `dosya:satir` duzeyine inildi: ikisi de `nullable`
ailesinde ve yalnizca IKI DOSYA ARASINDA yer degistirmis
(`IEntityRepository.cs` 20 -> 24, `EfEntityRepositoryBase.cs` 10 -> 6, **toplam 30 sabit**).
`git diff --name-only d434906..a244160` ile dogrulandi: **her iki dosya da bu commit'te
DEGISMEDI**. `codeql` job'i iki kosumda da TAM 12 annotation tasiyor -> bu bir ANNOTATION
YUZEYE-CIKARMA/KIRPMA ARTEFAKTIDIR, yeni uyari DEGIL. Bu commit'in ekledigi iki yeni Core
dosyasi ve dokuz degisen dosyanin HICBIRI tek bir uyari uretmedi.

**KAPANAN KALEMLER: F1 (+F10 cihaz bagi, +F11 city/district/zip, +F12 SecurityEvent),
F2 (denetim izi maskeleme), F3 (silmede redaksiyon).**

**BEKLENTI KARSILASTIRMASI (push ONCESI yazilmisti):** ne prompt'un tahmini (komsu
testler artik audit satiri yaziyor) ne de benim revize beklentim (yeni pinlerin CI
maliyeti / `model` kilidi baskisi) TUTTU. `AuditInterceptor`in ILK KEZ bir test host'unda
kosmasi hicbir yan etki uretmedi; `AuthorizationIdorTests` 15 yerine 19 test kosmasina
ragmen 1807 sifir kez atesledi.

## KALICI KURAL - IZLEYICI / OLCUM ARACI SOZLESMESI (26 Agustos 2026)

**Uzun sure donen bir izleyicinin CIKIS KOSULU, o makinede VARLIGI KANITLANMIS bir araca
dayanmalidir. Hata yutan bir yedek (`|| echo ...`, `2>/dev/null`, `try/catch`) cikis
kosulunu BESLEYEMEZ - yutulan hata sonsuz donguye donusur.**

BEDELI ODENDI (`a244160` izleyicisi): cikis kosulu `python` ile JSON ayristiriyordu, bu
makinede `python` YOK ve `|| echo "?"` devreye girdi. `TAMAM` hep `"?"` oldu, karsilastirma
HIC eslesmedi ve izleyici, kosumlar bitmis olmasina ragmen ~3 saat dondu. Ustelik ayni
dongu `grep` ile `run: 2` sayisini DOGRU sayiyordu - ama o deger cikis kosulunda
KULLANILMIYORDU. Yani sinyal elde vardi, karar yolu bozuktu.

**KURAL:** izleyici baslatilmadan ONCE cikis kosulu, sonucu BILINEN bir girdiyle bir kez
dogrulanir (or. "bu ifade zaten bitmis bir run icin 'tamam' diyor mu?"). Yedek yol yalniz
GURULTU icin olabilir, KARAR icin degil.

**CAPRAZ REFERANS - "YAPILMIS GORUNUP CALISMAYAN DUZELTME" AILESI.** Bu, depoda tekrar
eden bir siniftir; dordu de ayni desendir (kod yazildi, sessizce etkisiz kaldi, ancak
BAGIMSIZ bir olcum yakaladi):
- **`Identity.Name`** (D4 / GUVENLIK-FIX-4): idempotency kapsamina "kullanici" eklendi ama
  JWT o claim'i yazmiyordu -> herkes `"anon"` kovasinda kaldi. Pin yakaladi.
- **`IDistributedCache`** (D4): `[Idempotency]` filtresi `cache == null` gorup SESSIZCE
  devre disi kaliyordu; yorumu "in-memory'ye duser" diyordu, YANLISTI.
- **Mutasyonlarin HIC UYGULANMAMASI** (Dalga D): `powershell -File` yurutme politikasina
  takildi, uc mutasyon dosyaya inmedi ve testler "hepsi yesil" dedi -> "mutasyon lokalize"
  diye YANLIS rapor yazilacakti. Kural bu yuzden var: (a) dosyaya indi mi, (b) temiz build,
  (c) kirmizi yoksa ONCE "uygulanmadi" suphesi.
- **IZLEYICININ CIKIS KOSULU** (bu kalem).
Ortak panzehir AYNIDIR: **mekanizmanin CALISTIGINI, sonucu bilinen bir girdiyle BIR KEZ
gozle.** "Kod orada" kanit degildir.

## KALICI KURAL - ANNOTATION KARSILASTIRMASI (26 Agustos 2026)

**"Bu commit yeni uyari uretmedi" iddiasi AILE/SAYI duzeyinde KAPANMAZ.**

Toplam ve aile dagilimi esit olabilir ama kume farki BOS OLMAYABILIR. Kume farki bos
degilse **`dosya:satir` duzeyine inilir** ve farkin dustugu dosyanin bu commit araliginda
degisip degismedigi **`git diff --name-only <onceki>..<yeni> -- <dosya>`** ile dogrulanir.

- Dosya DEGISMISSE -> gercekten yeni uyaridir, raporlanir.
- Dosya DEGISMEMISSE -> annotation yuzeye-cikarma/kirpma artefaktidir (GitHub check-run
  basina annotation sayisini sinirlar; hangi ornegin yuzeye ciktigi kosumdan kosuma
  degisebilir). Bu durumda "yeni uyari yok" denir ama **NEDEN** de yazilir.

Bu adim `a244160` turunda YANLIS bir "yeni uyari" raporunu ONLEDI: fark 4/4 gorunuyordu,
inceleyince iki DOKUNULMAMIS dosya arasindaki yer degistirme cikti.

## FIX-1B DEVIR LISTESI (tek yerde, kaybolmasin)

- **F4 + F8 ZINCIRI (asil is).** F4: erisim jetonu iptali YOK -
  `ITokenBlacklist.RevokeAsync` uretimde SIFIR cagri; logout / change-password / G1 zincir
  iptali sonrasi access token 15 dk daha calisiyor. F8: step-up (`RequireRecentAuth`)
  refresh ile SINIRSIZ tazeleniyor - calinmis bir refresh cerezi geri alinamaz hesap
  silmeye yetiyor. Ikisi ayni zincirin iki ucu.
- **KARA LISTE AD-TAM-ESLESMESINDEN DESEN BAZLIYA.** Bugun `DenetimGizlilik.SirAlanlari`
  bir AD FOTOGRAFIDIR; `*token*` / `*secret*` / `*hash*` / `*salt*` desenlerine cevrilmeli.
  Yani adi listede OLMAYAN yeni bir sir alani VARSAYILAN OLARAK redakte edilmeli ve bunu
  gosteren bir pin yazilmali (bugun tersi: listede yoksa CIPLAK yazilir).
- **`refresh_token` / `device_token`in YAZMA ANINDAKI maskesi DAVRANISLA pinlenecek.**
  FIX-1A'da bu ikisi yalniz liste-uyeligi + F3 (silme sonrasi redaksiyon) tarafinda kapali;
  yazma anindaki maske `Customer` satirlari uzerinden pinlendi, `UserSession`/
  `CustomerDevice` uzerinden DEGIL.
- **`KisiselAlanlar` ve `RedaksiyonTablolari` da AD/TABLO FOTOGRAFIDIR** - sir listesiyle
  AYNI kirilganlik. FAZ 4/5 yeni bir PII yuzeyi getirdiginde (or. yeni bir iletisim ya da
  fatura-disi kisisel alan) SESSIZCE kapsam disi kalirlar. Kural haline gelmeli.
- **GERIYE DONUK YOL YOK - SIRA BAGIMLILIGI.** Redaksiyon YALNIZ silme aninda kosuyor.
  FIX-1A canliya CIKMADAN once silinen bir hesabin PII'si `audit_logs`ta KALICIDIR (dev
  veritabaninda FAZ 1'in sildigi hesaplarda MEVCUT - olculdu). Yani uretimde ILK GERCEK
  KVKK silmesinden ONCE bu surumun canlida olmasi gerekir; aksi halde o silme yarim kalir
  ve geriye donuk bir telafi yolu YOKTUR. `ops/deployment-checklist.md`'ye madde olarak
  da eklendi.

---

