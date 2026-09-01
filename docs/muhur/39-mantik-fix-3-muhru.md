# MANTIK-FIX-3 MUHRU - KVKK/HESAP DURUSTLESIR (30 Agustos 2026)

**KOD SHA'LARI: `add4009` (K1+K2) · `68051f4` (K3) · `ef1e0d8` (K3b) · `a9db3b9` (K4) ·
`5f781df` (K5) · `974ce41` (MK-4b denetim duzeltmeleri) · `322650e` (FF flake-fix)** -
zemin `1b62923`. Bu muhur AYRI ve docs-only bir commit'tir; **kendi SHA'sini ve kendi run
kimliklerini ICEREMEZ** (tavuk-yumurta) - muhrun kendi cift yesili MANTIK-FIX-3 raporunda
verilir. MFIX-1'de kurulan kalip.

**ORTAM UYARISI (Divisima Eki 2.3):** olcumler `goz1` duzeneginde kosuldu; API surecinin
komut satirinda BES ARGUMAN var ve **BUNLAR URUN VARSAYILANI DEGILDIR** -
`--Iyzico:UseRealSdk=false` · `--AdminSeed:Enabled=false` · `--BackgroundJobs:Enabled=false`
· `--RateLimit:AuthPermitLimit=100` · `--MailSettings:Host=`.
Rig artefakti bulgu SANILMADI: K3'te `coupon_usages` 0 kalmasi ve outbox mesajlarinin
islenmemesi arka plan islerinin KAPALI olmasindandir.

---

## (a) MANTIK-FIX-3 KAPANISI

### ON OLCUM (bes ajan: A-E, E = zorunlu kapsam elestirmeni)

**A - KVKK SILME YOLU: CANLI IHLAL BULDU.** Silme akisindaki ALTI okumadan YALNIZ BIRI
global sorgu filtresinden zarar goruyor: `AccountManager.cs:199` `_addressDal.GetListAsync`
filtreyi DELMIYOR ve `DivisimaDbContext.cs:825` `Address` uzerinde `is_active` filtresi
tasiyor -> **pasif adresler kaskada HIC GIRMIYOR.** `CustomerDevice`/`UserSession`/`AuditLog`
FILTRESIZ oldugu icin etkilenmiyor.
**AKTIF IHLAL: 1 SATIR** - adres 55 / musteri 93 (SILINMIS hesap), TAM PII (ad, telefon,
acik adres, sehir, ilce, posta kodu).
**DETERMINISTIK KANIT (alternatif aciklama ELENDI):** ayni silme cagrisi musteri 93'un dort
adresinden UCUNU tek bir zaman damgasiyla anonimlestirdi; 55'in `updated_at`i NULL ve
`updated_at`in depoda TAM IKI yazicisi var, soft-delete o kolona DOKUNMUYOR.
**KR6 GEREGI DOKUNULMADI** - "mevcut hicbir kayit silinmez/anonimlestirilmez". D-YAN'a gitti.
A ayrica ASCII/Turkce tuzagini BIREBIR yeniden uretti: `Turkish_CI_AS` altinda diyakritikli
`N'Silinmis'` ile ASCII bicim FARKLI; yanlis yuklem "PII duruyor" sayisini **4 -> 8** ikiye
katliyordu. A KENDI YANLIS POZITIFINI de yakaladi (musteri 71'i "askiya alinmis" sandi;
e-postasi KALDIRILAN ikizin TIRELI bicimindeydi).
**A'NIN KOR NOKTASI:** test KOSMADI - pin vakumu iddiasi KAYNAK OKUMASINA dayaniyordu;
ana akis o boslugu K1'de R-H1 ve P-H1 kirmizi-oncesiyle KAPATTI.

**B - ABONELIK BAGI: SIRA UYARISI.** Iki abonelik tablosunda da `customer_id` YOK ve silme
akisi onlara HIC dokunmuyor. **KRITIK UYARI: temizligin koprusu `c.email`dir ve
`DeleteAccount` onu satir 164'te ANONIMLESTIRIYOR** - temizlik o satirdan SONRA kosarsa
`deleted_<id>@...` arar, HICBIR SATIR bulamaz ve **hicbir hata da vermez**. B ayrica
"SILME > anonimlestirme" gerekcesini olctu: anonimlestirme `email NOT NULL` + filtreli-UNIQUE
yuzunden Guid'li yer tutucu + jeton yenileme + `is_notified=1` yani UC YAZMA isterdi.
KR2'nin on kosulu ("e-posta duz saklanmiyorsa DUR") karsilandi: DUZ saklaniyor.

**C - HESAP/SIFRE: "UC ZATEN VAR" KESFI.** KR3'un kosullu maddesi ("iptal altyapisi yoksa
EKLENMEZ" + "uc yoksa yazilir") **OLCUMLE DUSTU**: `POST /api/Account/change-password` VAR
ve DOGRU (politika -> mevcut sifre dogrulamasi -> hash -> tum oturumlari kapatma), ustelik
`SifrePolitikasiTests` ile PINLI. **ISTEMCI TARAFI TAMAMEN PINSIZDI.** Yani K3 bir SUNUCU
YAZMA isi degil, ISTEMCI BAGLAMA isiydi. C bulguyu olcülenden GENIS de yapti: kardes
`#pfSave` BAGLI ama bagli oldugu `saveProfileForm` da sunucuya HIC gitmiyor - iki uc de
**0 cagiranli**. (Bu gozlem sonradan K3b'yi dogurdu.)

**D + E - K4'UN COZUMUNU BIRLIKTE BELIRLEDILER.** D: sira degisikligi **FK tarafindan
YAPISAL OLARAK KAPALI** (`orders.customer_id` ve `addresses.customer_id` NOT NULL + FK,
`PlaceOrder` musteri id'sini ARGUMAN olarak alir); ic ice transaction ise `UnitOfWork`in
`_transaction` **TEK ALANI** ve `BeginTransactionAsync`in onu KOSULSUZ ezmesi yuzunden
imkansiz. E ikinci ve daha sinsi boyutu ekledi: `ExecuteInTransactionAsync` **YALNIZCA
ISTISNADA** rollback eder, `PlaceOrder` ise transaction acilmadan ONCE **ON BIR NOKTADAN**
hatayi DONUS DEGERIYLE bildirir - iclerinde MF-3'un hedefledigi gecersiz-kupon vakasi
(`:280`) DA VAR. Yani sarmalayici COMMIT ederdi ve duzeltme **tam da hedefledigi hata
modunda hicbir sey yapmazdi** ("yapilmis gorunup calismayan duzeltme" ailesi).
E ayrica K2'nin tasarimini belirledi: **`PostaKutusu.Kanonik` KULLANILMAZ** - kanonik eksende
BASKA MUSTERILERIN aboneligi silinirdi ve bu veritabaninda CANLI ornek var (bir kanonik kutu
-> UC ayri musteri; baska biri -> IKI musteri).
**B ve E ayni sira tuzagini BAGIMSIZ OLARAK buldu** (capraz dogrulama).

### MERKEZ KARARLARI - GEREKCELERIYLE

| Karar | Icerik | Gerekce / sonuc |
|---|---|---|
| **KR1** | K1 filtre delme YALNIZ KVKK silme yolunda | Filtrenin GENEL davranisi korunur; yeni DAL yuzeyi ACILMADI (`GetListIgnoringFiltersAsync` zaten arayuzdeydi) |
| **KR2** | Abonelik temizligi E-POSTA ESLESMESIYLE, migration YOK | On kosul olculdu: e-posta DUZ saklaniyor -> DUR gerekmedi |
| **KR3** | Iptal altyapisi yoksa EKLENMEZ; e-posta bildirimi eklenmez | Kosullu madde OLCUMLE dustu (uc zaten var). Jeton akibeti YALNIZ OLCULDU |
| **KR4** | K4'un bicimini D olcumu belirler | D+E olcumu iki adi konmus bicimi de ELEDI -> merkez TELAFI SILME'yi secti |
| **KR5** | K5 yalniz GIRDI SINIFI | Kacislama/XSS devir listesi BOS cikti (asagi) |
| **KR6** | KVKK testleri YALNIZ TAZE kurgu musterilerinde | Mevcut hicbir kayit silinmedi/anonimlestirilmedi; adres 55 ihlali DOKUNULMADAN D-YAN'a |
| **N1** | P-H1 mutasyon kaydi "TAM 1 isimli kirmizi (1 ad / 2 vaka, Theory)" | Sayi yuvarlanmadi, iki ayri kirmizi diye sayilmadi |
| **N2** | Hata eslemesi once MAKINE-OKUNUR sinyal; yoksa HAM yanit capasi + cift bicim + kirilganlik kaydi | K3 ve K3b'nin ikisi de bu capaya dayaniyor - sunucu yanit sozlesmesi DEGISTIRILMEDI, istemcide politika kopyasi ACILMADI |
| **N3** | Kurgu kaydi + MAX guncellemesi | Envanter asagida |
| **K4 KESIN BICIM** | Yazimlar kendi bolgesinde kalir; PlaceOrder basarisiz donerse musteri+adres TELAFI SILINIR; sira FK'ya saygili; id'ler elde, e-postayla ARAMA YOK; telafi ATOMIK DEGIL | Iki adi konmus bicim (sira degisikligi / transaction) OLCUMLE engelli cikti |
| **K3b** | Profil kaydetme dursutlesir | **MERKEZ SUREC HATASI KAYDI:** bu karar daha once verilmisti ama BLOK DISINDA kaldigi icin CC'ye ULASMADI; merkez bunu kendi sureç hatasi olarak kaydetti, CC'nin kural-uyum kaydi TEMIZ |

### K1-K5 + K3b - ONCE / SONRA

**K1 - PASIF ADRES ANONIMLESIYOR** (`add4009`)
Kok sebep: kod DOGRU GORUNUYOR, pin YESIL, PII KALIYORDU - global filtre sorguya sessizce
`AND is_active = 1` ekliyordu. Duzeltme `GetListIgnoringFiltersAsync`; delme YALNIZ bu yolda.
**R-H1:** pasif adres fiksturuyle once-durum uretildi; silme sonrasi pasif adresin ad-soyad /
telefon / acik adres / sehir / ilce / posta kodu ALTISI DA temizlendi.

**K2 - ABONELIKLER DE SILINIYOR** (`add4009`)
Silinen hesabin gercek e-postasi iki tabloda KALIYORDU. Iki DAL enjekte edildi, iki
`DeleteWhereAsync` eklendi. **DUZ ESITLIK** (kanoniklestirme YOK - E'nin canli kaniti) ve
**e-posta ANONIMLESTIRILMEDEN ONCE** (B ve E'nin bagimsiz uyarisi).
**R-H2:** silmeden once uc satirin varligi assert edilir; sonra silinenin satirlari GIDER,
**FARKLI e-postali abone AYNEN KALIR** (negatif bacak - "hepsini sil" uygulamasi gecemez).

**K3 - SIFRE DEGISTIRME GERCEKTEN CALISIYOR** (`68051f4`)
`#pfPassSave` api-bridge'de SIFIR gecisti; `index.html`in `savePassForm` govdesi API'ye
gitmeden "Sifren guncellendi" diyordu **ve YEREL "en az 6 karakter" kurali tasiyordu**
(sunucu 8 + karmasiklik istiyor) - hem yalan soyluyor hem YANLIS kural anlatiyordu.
**R-H3 (canli, uc dilde):** yanlis mevcut sifre -> TR "Mevcut sifren hatali." / EN "Your
current password is incorrect." / AR Arapca karsiligi, `tip=err`. Dogru sifre -> 200, alanlar
temizlendi, `tip=ok`. ESKI sifreyle giris **401**, YENI sifreyle **200**.

**K3b - PROFIL KAYDETME GERCEKTEN KAYDEDIYOR** (`ef1e0d8`)
**R-H6 ONCE - YALAN UC KANALDAN KANITLANDI:** ad ve e-posta degistirilip Kaydet ->
toast "Bilgilerin guncellendi" YESIL ONAY isaretiyle · `fetch` sayaci `/api/` istegi **0** ·
DB satiri **DEGISMEDI**. Ayrica form BOS aciliyordu (sunucuda gercek degerler dururken
`pfName=""`, `pfEmail=""`) ve e-posta alani DUZENLENEBILIRDI.
**R-H6 SONRA:** form SUNUCUDAN besleniyor · `/api/account/profile` istegi **1** · ad DEGISTI ·
**e-posta DEGISMEDI** · **phone ve dogum KORUNDU**. Durust hata uc dilde `t-err`.
**IKINCI SAVUNMA HATTI CANLI:** api-bridge yuklenmemis gibi mock dogrudan cagrildi ->
"Bilgilerin guncellenemedi" `t-err`, `/api/` istegi 0.

**K4 - BASARISIZ SIPARISTE TELAFI SILME** (`a9db3b9`)
**R-H4 ONCE:** gecersiz kupon -> 400; DB **musteri 1 / adres 1 / siparis 0 / outbox 1**;
ayni e-posta KUPONSUZ ikinci deneme -> **409** -> tek yanlis kupon kodu e-postayi misafir
checkout'a KALICI KAPATIYOR.
**R-H4 SONRA:** ayni gecersiz kupon -> 400 ve **AYNI MESAJ** (kupon dogrulama noktalari
DEGISMEDI); DB **musteri 0 / adres 0 / siparis 0**; ikinci deneme -> **201**.
**SINIR KONTROLLERI:** GERCEK kayitli e-posta HALA **409** · BASARILI yolda telafi ATESLEMIYOR.

**K5 - MISAFIR ADRES GIRDI SINIFI** (`5f781df`)
Adresi yazan IKI yol vardi, yalniz BIRI dogrulaniyordu.
**R-H5 ONCE:** ALTI gecersiz girdi sinifinin ALTISI da **HTTP 201** aldi ve GERCEK SIPARIS
uretti - telefon BOS, telefon "dfg", sehir BOS, ilce BOS, acik adres BOS, zip 15 karakter.
**R-H5 SONRA:** ALTISI DA **400** ve HER BIRI KENDI MESAJIYLA; musteri satiri **0**
(dogrulama YAZIMLARDAN ONCE kosuyor). **VAKUM KIRICI:** gecerli govde HALA 201.
Mevcut veri etkisi olculdu (85 adres): city bos 1, district bos 1, phone bos 8, phone
rakamsiz 1, uzunluk ihlali 0.

### K3b'NIN IKI ASIMETRISI (tarifte YOKTU, on olcumde cikti)

**(1) FAZLA ALAN = YENI YALAN.** Form e-posta inputu TASIYOR, sunucu DTO'su
(`UpdateProfileRequestDto` = name + phone + birthdate) TASIMIYOR. Naif bir baglama YENI BIR
YALAN uretirdi: kullanici e-postayi degistirir, "guncellendi" gorur, sunucuda hicbir sey
degismez. Korkuluk **markup duzeyinde** kuruldu (alan `readonly`, yani api-bridge yuklenmese
de) ve govdeye HIC KONULMUYOR.

**(2) EKSIK ALAN = PUT-EZ VERI KAYBI.** DTO uc alan tasir, form BIR alan. Yalniz `{name}`
gonderilseydi `phone` ve `birthdate` **NULL yazilirdi** - devir listesindeki **F5 / FIX-1C**
bulgusunun TA KENDISI. Sunucu sozlesmesi bu dalgada degistirilmedigi icin istemci UC ALANI
DA tasimak zorunda: ikisi summary'den yuklenip AYNEN geri gonderiliyor.
Bu, **kendi degisikligimizin acacagi kapiyi kapatmaktir** (MFIX-B/admin.html emsali),
kapsam genislemesi degil.

### KIMLIK BOSLUGU 125/133 - HARD-DELETE'IN IKINCI BAGIMSIZ KANITI

Musteri kimlik dizisinde **125 ve 133 BOSLUKLARI** var (ikisi de `COUNT=0`). 125 = R-H4
SONRA'nin gecersiz kupon denemesi; 133 = R-H5 ONCE'nin "Yetersiz stok" alan bacagi. Ikisi de
K4'un telafi silmesiyle geri alindi. **Aranmayan ama bulunan bir kanit:** kimlik boslugu,
silmenin GERCEKTEN hard-delete oldugunu (soft-delete DEGIL) gosterir - K4 icin bu ZORUNLU,
cunku soft-delete olsaydi 409 kilidi KALKMAZDI.

### K4'UN UC BILINCLI SINIRI (koda ve buraya yazildi)

1. **TELAFI ATOMIK DEGIL.** Iki `DeleteAsync` her biri kendi `SaveChanges`ini yapar. Telafi
   duserse satir KALIR; musteriye PlaceOrder'in hatasi doner (telafi hatasi DEGIL) ve olay
   ADIYLA loglanir. **Denetcinin netlestirmesi:** kismi durum da mumkun - adres SILINMIS,
   musteri KALMIS; o halde e-posta hala kilitli VE adres kaybolmus olur.
2. **ISTISNA YOLU KAPSAM DISI.** `PlaceOrder` throw ederse telafi KOSMAZ; davranis K4
   ONCESIYLE AYNI kalir (regresyon degil, kapatilmamis yol).
3. **DOGRULAMA MAILI OUTBOX SATIRI SILINMIYOR.** Musteri satirindaki jeton musteriyle gider
   ama outbox satirinin KIMLIGI ELDE DEGIL (`ResendVerification` geriye id dondurmuyor) ve
   onu e-postayla aramak "id'ler elde" kuralini delerdi. **Bu turda 3 boyle satir olustu.**
   Silinen bir hesap icin OLU JETONLU dogrulama maili gidebilir - e-postanin KALICI
   kilitlenmesinden kiyasla cok daha hafif.

### PIN TABLOSU

| PIN | SINIF | DIS KONTROLU | MK-6 MUTASYONU |
|---|---|---|---|
| **P-H1** | davranis (SQL), guclendirildi | TAM 1 isimli kirmizi | **ZORUNLU** - TAM 1 isimli kirmizi **(1 ad / 2 vaka, Theory)** |
| **P-H2** | davranis (SQL) | TAM 1 isimli kirmizi | - |
| **P-H3** | davranis (SQL) | TAM 1 isimli kirmizi | - |
| **P-H3c** | KAYNAK SOZLESMESI (durust etiket) | TAM 1 isimli kirmizi | denetci: **PIN ZAYIF** -> guclendirildi, yeniden kosuldu: TAM 1 isimli kirmizi |
| **P-H4** | davranis (SQL), **BILINCLI KIRILDI** | TAM 1 isimli kirmizi | **ZORUNLU** - TAM 1 isimli kirmizi |
| **P-H5** | davranis (SQL), Theory 6 + Fact 1 | TAM 1 isimli kirmizi **(1 ad / 6 vaka, Theory)** | - |
| **P-H6** | KAYNAK SOZLESMESI (durust etiket) | TAM 1 isimli kirmizi | - |

**BILINCLI KIRILAN PIN (merkez ONAYLI):** `MisafirCheckoutTests`in (4) blogu SUPHELI
davranisi sabitliyordu ve **KENDI METNI** "duzeltildigi gun KIRILIR ve o zaman 0'a cevrilir"
diyordu; K4 o gunu getirdi.
- **ESKI:** `MusteriSayisiAsync(eposta2) == 1` + "SUPHELI ... bugunku davranisi PINLER"
- **YENI:** `== 0` + IKI YENI BACAK: (5) telefi sonrasi ayni e-posta ile kuponsuz siparis
  **201** ve siparis sayisi 1 - zarar zincirinin SON halkasi ("sil ama baska yerde kilitle"
  diyen bir uygulama eskisini gecerdi); (6) VAKUM KIRICI: BASARILI yolun siparisi ve
  musterisi YERINDE ("her cagrida sil" uygulamasi kirar).

**HARNESS UYARLAMASI (Sprint 8 madde 10 kalibi):** `SiparisSayisiAsync` musteriyi
`FirstAsync` ile ariyordu; telafi silme musteri satirini geri aldigi icin "musteri yok" ARTIK
NORMAL ve yardimci `Sequence contains no elements` ile patliyordu - pin YANLIS SEBEPTEN
kirmiziydi. `FirstOrDefaultAsync` + "musteri yoksa 0" yapildi. **Uretim kodu DEGIL HARNESS
duzeltildi.**

**YENI SQL SINIFI ACILMADI** (`10d794d` dersi): tum davranis pinleri MEVCUT siniflara,
iki kaynak pini sifir-DDL sinifa eklendi.

### MK-4b KAPANIS DENETIMI - UC ITIRAZ

Tek denetci, AYRI worktree, izole test-DB, MK-4a beyani TUTTU. **SONUC: UYUMSUZ (DAR)** -
kapsam TEMIZ (dokuz dosyanin dokuzu izinli; dokunulmazlarin diff-SATIRI taramasi 0, iki
supheli token'in gecisleri HEPSI YORUM, NEG kontrol 0 / POZ kontrol 15), bilincli sinirlar
DURUST, sozluk disiplini kusursuz. Denetci DORT uretim mutasyonu kostu; **M-D1 ve M-D3
olculen once-durumu BIREBIR uretti.**

- **ITIRAZ-1 `[pin boslugu]` LATENT - KAPATILDI.** P-H3c'nin olcutu YALNIZ `.length<`
  LITERAL BICIMINI ariyordu; denetci handler govdesine sunucunun kuralinin **BIREBIR REGEX
  KOPYASINI** ekledi ve pin **27/27 YESIL** kaldi. Olcut BICIMDEN BAGIMSIZ hale getirildi
  (uc uzunluk bicimi + regex ileri-bakis); ayni mutasyon yeniden kosuldu -> **TAM 1 ISIMLI
  KIRMIZI**.
- **ITIRAZ-2 `[kozmetik]` AKTIF - KISMEN KAPATILDI.** Sokum UC OLU sozluk anahtari birakti
  (`pf_err`, `pf_pass_ok`, `pf_pass_short`; uc dosyada da cagiran 0). **ANAHTARLAR
  KALDIRILMADI** - dalganin SINIRLARI mevcut sozluk anahtarlarini DOKUNULMAZ ilan ediyor.
  Kapatilan sey P-H3c'deki EKSIK IFADE ("bu akista kullanilmaz" -> "HICBIR akista
  kullanilmiyor"). Silme MF-4'e devredildi.
- **ITIRAZ-3 `[mantik]` LATENT - YORUM DUZELTILDI.** `GuestCheckoutValidator`in yorumu
  sozlesmenin "TASINDI"gini soyluyordu; gercekte **KOPYALANDI**. Telefon regex'i artik DORT
  yerde; **sinif K5 ile DOGMADI (3 -> 4)** ve dort kopya arasindaki ayrisma OLCULDU: **SIFIR**.
  Koruyucu tarama pini YOK -> LATENT risk ve iki kalici cozum adayi koda yazildi.

**P-H1'IN YAN KAZANCI:** M-D1 mutasyonunda kirilan assert, YENI pasif-adres assert'i DEGIL,
ONDAN ONCEKI **MEVCUT** `OnlyContain(a => a.phone == null)` assert'i oldu. Yani P-H1'in yeni
fiksturu **MANTIK-AV-1'in "muhurlu bir pini vakuma dusuruyor" bulgusunu TAM ANLAMIYLA
KAPATTI** - o pin artik kusur geri gelirse GERCEKTEN kiriliyor.

### UC OZEL OLCUM

- **JETON AKIBETI:** sifre degisimi oturum satirlarini KAPATIYOR ama degisimden ONCE alinmis
  **ACCESS TOKEN korumali ucta HALA 200 donuyor**. `RevokeAsync` uretimde 0 cagri,
  `user_sessions`ta `jti` kolonu YOK. KR3 geregi iptal altyapisi EKLENMEDI ->
  **GUVENLIK-AV-1 girdisi**.
- **ABONELIKTE SILME > ANONIMLESTIRME:** anonimlestirme `email NOT NULL` + filtreli-UNIQUE
  yuzunden Guid'li yer tutucu + jeton yenileme + `is_notified=1`, yani UC YAZMA isterdi. Bu
  tablolarda `HasQueryFilter` YOK - pasif-adres tuzaginin ikizi burada DOGMAZ.
- **KACISLAMA/XSS DEVIR LISTESI BOS:** tum adres alanlari `esc()` ile basiliyor, `zip_code`
  hicbir yerde basilmiyor, admin panelinde adres ekrani YOK. `[YOKLUK]` taramasi **POZITIF
  (66) ve NEGATIF (0)** kontrollu -> devredilecek bulgu YOK.

### SUIT

| | ONCE (`1b62923`) | SONRA |
|---|---|---|
| `Category=Sql` | 347 / 347 | **356 / 356** |
| Tam suit | 587 (584 + 3) | **598 (595 + 3)** |
| Release | 0 Hata | 0 Hata |
| format (6 proje) | exit 0 | whitespace + style HEPSI exit 0 |

**+11 test: 9'u Sql, 2'si sifir-DDL sinifta.** Tahmin BIREBIR tuttu (P-H2 1 + P-H3 1 +
P-H3c 1 + P-H6 1 + P-H5 7). Kirilan UCU DE tabandaki Docker'li `OrderEndpointTests`
(`PlaceOrder_ConcurrentRequests_NoOverselling` · `PlaceOrder_InsufficientStock_Returns400_And_NoPartialData`
· `PlaceOrder_ValidCart_Returns201_And_DecrementsStock`) - yerelde Docker kapali, CI'da yesil.

### KURGU KAYIT ENVANTERI

```
MUSTERI 120  SILINDI (KVKK silme yolu kosuldu; anonimlestirildi, is_active=0)
MUSTERI 121  SILINDI (ayni)
             -> ikisinin de 2'ser adresi ANONIMLESTIRILMIS halde DURUYOR
                (KVKK dogru davranisi: satir korunur, PII gider)
MUSTERI 122  K3 sifre fiksturu (sifre politikaya uygun bir kurgu degerle degistirildi)
MUSTERI 123  K3b fiksturu
MUSTERI 124  R-H4 ONCE'nin YETIM MISAFIRI - olculen zararin KANITI
MUSTERI 125  YOK - K4 telafi silmesiyle geri alindi (KIMLIK BOSLUGU)
MUSTERI 126  R-H4 SONRA basarili (siparis 269)
MUSTERI 127-132  R-H5 ONCE alti gecersiz sinif -> SIPARIS 270-275 BOZUK ADRESLI (ONCE kanit)
MUSTERI 133  YOK - K4 telafi silmesiyle geri alindi (KIMLIK BOSLUGU)
MUSTERI 134  R-H5 ONCE vakum (siparis 276)
MUSTERI 135  R-H5 SONRA vakum (siparis 277)
ADRES 78-95 (16 satir) · SIPARIS 269-277 (9, TAMAMI Confirmed/COD) · FATURA 102-110
YETIM OUTBOX: 3 dogrulama maili satiri (K4'un bilincli siniri 3)
MAX: musteri 135 · adres 95 · siparis 277 · fatura 110
YENI PENDING: 0 (yasak tutuldu)
```

**MK-3 UCLUSU - UCU DE YENIDEN OLCULDU ve TUTTU:**
```
SELECT COUNT(*), MAX(id) FROM orders WHERE customer_id = 10;              -> 38 / 211
SELECT COUNT(*), MIN(id), MAX(id), SUM(CAST(id AS bigint))
  FROM orders WHERE status = 0 AND id <= 210;                             -> 35 / 9 / 210 / 3837
SELECT COUNT(*), SUM(total_price), STRING_AGG(CAST(status AS varchar),',')
  FROM orders WHERE customer_id = 74 AND id BETWEEN 234 AND 237;          -> 4 / 4698,60 / 0,0,1,1
```

### CC'NIN HATALARI (dalga ici: 7)

1. **ROTA TAHMINI** - `stock-notification` sanildi, dogrusu `api/StockNotification`.
   `price-drop`un GERCEKTEN tireli olmasi yaniltti. **SDP 1.7/2 - bu dalgada 1. dusus.**
2. **P-H2 fiksturu** var olan bir urun ariyordu, sinif her testte DB'yi yeniden kuruyor.
3. **BAYAT TOAST** - AR bacaginda ilk olcum Ingilizce metin gosterdi; toast sinifinda `on`
   YOKTU, yani onceki turdan KALINTIYDI. Kendi kendini yakaladi.
4. **IKI ROTA/IMZA TAHMINI DAHA** (hesap sekmesi rotasi, api-client login imzasi).
   **SDP 1.7/2 - bu dalgada 2. ve 3. dusus.**
5. **FORMAT KAPISI CHECKPOINT'TE KOSULMAMIS** - `add4009` kapidan gecmeden commit'lendi;
   `exit 2` ancak SONRAKI kalemde fark edildi. **MK-9'un gerekcesi.**
6. **MK-6 MUTASYONUNUN ILK TURU GECERSIZDI** - build 2 Hata (MSB3027, kosan API DLL'i
   kilitliyordu) ve `--no-build` BAYAT IKILILERLE kosup "yesil" dedi. Kuralin (b) adimi
   yakaladi. **Kayitli tuzagin tekrari.**
7. **TURKCE COLLATION TUZAGI** - dogrulama sorgusu buyuk harfli e-posta ariyordu; e-posta
   uretimde kucultulerek saklaniyor ve `Turkish_CI_AS` altinda `I` ile `i` AYNI HARF DEGIL,
   yani case-insensitive collation'da bile ESLESMIYOR (olculdu: buyuk harfle 0, kucuk harfle
   1). **Urun DOGRUYDU, SORGU yanlisti.** CLAUDE.md bolum 6c'nin **4. dusus**u.

### ALTI COMMIT (MANTIK-FIX-3)

```
add4009  feat(kvkk): K1+K2 - silme pasif adresi ve abonelikleri de kapsar
68051f4  feat(hesap): K3  - sifre degistirme gercekten calisir
ef1e0d8  feat(hesap): K3b - profil kaydetme gercekten kaydeder
a9db3b9  fix(misafir): K4 - basarisiz sipariste telafi silme
5f781df  feat(adres): K5  - misafir yolunun adres girdi sinifi dogrulanir
974ce41  fix(mf3): MK-4b  - denetim bulgulari (ITIRAZ-1..3)
```

---

## (a-EK) DUR HIKAYESI, PROVENANS ve FF KALEMI

### KRITER ISLEDI - PUSH DURDURULDU

Push turunun 0. adimi "IKI ARDISIK tam dogrulama BIREBIR" istiyordu ve **saglanmadi**:

```
KOSUM 1   598 / 595 / 3      (taban Docker uclusu)
KOSUM 2   598 / 594 / 4      <- DORDUNCU KIRMIZI
KOSUM 3   598 / 595 / 3
```

**DORDUNCU KIRMIZI:** `SemaTekKaynakTests.OLMAYAN_KATEGORIYE_BEDEN_REHBERI_404_DONER_500_DEGIL`,
yigin izi `SemaTekKaynakTests.InitializeAsync`. Hata metni:

```
InvalidOperationException : DIVISIMA_TEST_SQL verildi ancak sema pin ortami
                            hazirlanamadi - ATLANMAMALI.
---- SqlException : Execution Timeout Expired.
-------- Win32Exception : Bekleme islem zamani asildi.
```

**ASSERT DEGIL, KURULUM HATASI.** Sinif SESSIZCE ATLAMADI - taban sinifin "DIVISIMA_TEST_SQL
verildiyse ATLANMAMALI" sozlesmesi DOGRU calisti ve GURULTULU dustu. **Belirti, o tasarim
kararinin DEGER KANITIDIR:** sessiz skip olsaydi flake HIC gorunmez, suit yesil kalir ve
kusur CI'ya tasinirdi.

**SIKLIK:** sinif TEK BASINA **6/6 yesil (3 sn)**, tam suitte **3'te 1**.

**SORUMLULUK OLCUMU (varsayilmadi):** `SemaTekKaynakTests.cs` bu dalgada **DEGISMEDI** ·
veritabani kuran sinif sayisi **ONCE 49 / SONRA 49 - DEGISMEDI** · dalga **SIFIR yeni test
dosyasi** ekledi (`--diff-filter=A` -> 0). Yani **YENI BIR CEKISME KATILIMCISI EKLENMEDI**;
artan tek sey TOPLAM YUK (587 -> 598 test).

### PROVENANS OLCUMU - 0/6, SONUCSUZ-EGILIMLI

Dalga ONCESI commit'te (`1b62923`) tam suit **ALTI KEZ** kosuldu (YERINDE checkout; worktree
DEGIL - **338/339 dersi**: worktree + paylasilan test-DB olcumu KIRLETIR). ALTISINDA DA
**587 / 584 / 3** ve kirilan kume HER SEFERINDE ayni Docker uclusu; SemaTekKaynak timeout'u
**HIC GORULMEDI**.

**YORUM (merkezin kurali):** 0/6 -> **SONUCSUZ-EGILIMLI, KESIN HUKUM YAZILMAZ.** Gozlenen
1/3 oranla alti temiz kosum olasiligi (2/3)^6 = **~%9** - yani 0/6 "onceden yoktu"yu
KANITLAMAZ, "onceden VARDI"yi da desteklemez. Hane atamasi KESIN DEGIL.

### FF KALEMI (`322650e`)

**1. YARDIMCI:** `TestDbKurulum.CollationIleOlusturAsync(masterConn, dbAdi, collation)` -
ham `CREATE`+`DATABASE`+`COLLATE` yazan **TEK EV** artik burasi. Acik `CommandTimeout` **120
sn** (varsayilan 30'un DORT KATI; **SECILMIS SABIT, olculmus esik DEGIL** - gerekce yorumda)
+ **YALNIZ timeout'ta en cok IKI yeniden deneme**, her deneme ADIYLA loglanir.
**"YALNIZ 1807, baska hata yutulmaz" ilkesi GENEL YOLDA AYNEN** - `SilAsync`/`OlusturAsync`
yuklemleri DEGISMEDI; timeout-retry yalnizca bu CREATE sinirinda yasar.

**OLCUM KANALI AYRILDI (kendi acilacak tuzagi kapatti):** CI adimi ciktida
`[TestDbKurulum] 1807` ARIYOR. Zaman asimi denemeleri ayni etiketi kullansaydi "1807
atesledi mi" olcumu KIRLENIRDI -> ayri etiket (`TIMEOUT`) ve ayri sayac
(`ZamanAsimiYenidenDenemeSayisi`) eklendi. **CANLI DOGRULAMA:** uc kosumun ucunde de
`1807` = 0 ve `TIMEOUT` = 0; negatif kontrol (olmayan etiket) 0.

**2. CAGRI YERI:** `SemaTekKaynakTests.InitializeAsync` yardimciya dondu; **ACIK COLLATION
(Turkish_CI_AS) KORUNDU** (6c gerekcesi yerinde).
**TARIF PREMISI OLCUMLE DUZELTILDI:** `ArkaPlanIsleriIzolasyonTests`te donusturulecek cagri
yeri **YOK** - `CREATE DATABASE` orada **TEK GECIS ve YORUM SATIRI**; dosyada
`SqlConnection`/`ExecuteNonQuery`/`EnsureCreated` gecisi **SIFIR** (negatif kontrol:
ayni tarama `SemaTekKaynakTests`te 9 `SqlConnection` buluyor). **Premisin kaynagi CC'nin
kendi DUR raporudur** - orada verilen ham `grep -rln` ciktisi YORUMLARI AYIKLAMIYORDU, yani
merkeze bir SUZGEC ARTEFAKTI iletildi. Ustelik o sinif `10d794d` duzeltmesi geregi **BILEREK
SIFIR DDL** uretir; ona veritabani olusturmak duzeltmeyi GERI ALMAK olurdu.

**3. KAPSAM PINI BICIMDEN BAGIMSIZLASTI (ITIRAZ-1 kalibi):**
`HICBIR_TEST_SINIFI_KURULUM_YARDIMCISINI_ATLAMAZ` artik `EnsureCreated`/`EnsureDeleted`
dizgelerine EK OLARAK "ham olusturma ifadesi yalniz `TestDbKurulum.cs`te" olcutunu de
tasiyor. **YORUMLAR AYIKLANIYOR** - ZORUNLU: aksi halde pin, kuralin ORNEGINI (sifir DDL
ureten sinifi) suclardi.
**MUTASYON KANITI:** gecici ham ifade BASKA bir test dosyasina konuldu -> **TAM 1 ISIMLI
KIRMIZI** ve mesaj IHLAL EDEN DOSYAYI ADIYLA soyledi -> geri alindi, iz 0.

**KABUL:** SemaTekKaynak tek basina **6/6** · kapsam pini + TestDbKurulum pinleri **5/5** +
mutasyon kaniti · Release **0** · alti projede format **exit 0** · **UC ARDISIK tam dogrulama
BIREBIR** (598 / 595 / 3, kirilan kume ayni Docker uclusu). **SUIT SAYISI 598 SABIT** - yeni
test EKLENMEDI. Kapsam DAR: uc dosya, hepsi test altyapisi.

### PUSH TURUNUN CC HATALARI (3)

1. **SUZGEC HATASI - KENDI KENDINI YAKALADI (SDP 1.7/1).** Provenans kosumlarinda kirilan
   adlari cikaran desen (duz FQN taramasi) **500 adet CA1707 DERLEME UYARISINI** da yakaladi
   (uyarilar test METOT ADLARINI iceriyor) ve kosum 1 "onlarca kirmizi" gorundu. Onceki
   turlarda calismasinin sebebi ARTIMLI derlemede uyari URETILMEMESIYDI; checkout TAM DERLEME
   zorladi. Desen `[xUnit.net ...] <FQN> [FAIL]` capasina cevrildi ve DORT girdiyle SINANDI:
   POZ (4 kirmizi) -> tam 4, POZ (3 kirmizi) -> tam 3, NEG bos -> 0, **NEG 500 uyarili
   dosya -> 3** (uyari adlarini ELEDI).
2. **PIN KENDI MESAJINA TAKILDI - AYNI TUZAGIN BESINCI TEKRARI.** Yeni kapsam olcutu ilk
   kosumda `found {"TestDbKurulumTests.cs"}` ile kirmizi verdi: ARAMA desenini bolerek
   yazmistim ama **ASSERT MESAJINDA ifadeyi BITISIK** yazmistim (dizge literali, yorum degil -
   yorum ayiklayici onu ELEMEZ). Dosyanin KENDI idiyomu (deseni bolerek yazma) mesaja da
   uygulandi.
3. **PUSH IKIYE BOLUNDU.** C provenans olcumunun DONUSUNDE `git checkout 974ce41` yapildi,
   yani **SHA ile**; dogrusu `git checkout main` idi. HEAD DETACHED kaldi, FF commit'i DALA
   DEGIL detached HEAD'e dustu ve `git push origin main` yalniz ALTI commit'i itti. **KAPI
   KONTROLU YETERSIZDI:** HEAD SHA'si, agac, zincir, fark, worktree ve stash dogrulandi ama
   **"HEAD BIR DAL UZERINDE MI"** sorusu SORULMADI. Teshis push ciktisinin beklenen SHA'yi
   soylememesiydi. Onarim: `git checkout main` + `git merge --ff-only` (temiz ileri sarma) +
   ikinci push. **SAPMA: merkez "YEDI commit TEK push" istemisti; gerceklesen IKI PUSH
   (alti + bir).** Force-push YASAK oldugu icin birlestirilemez.

---

## (b) KALICI KURALLAR + DERSLER

### MK-9 (YENI MIKRO-KURAL)

**"Bicim kapilari (whitespace + style) her checkpoint commit'inden ONCE kosulur; kapidan
gecmemis commit checkpoint sayilmaz."**

**Gerekce OLCULDU:** `add4009` bicim kapisindan gecmeden commit'lendi; whitespace kapisi
**exit 2** (10 hata, hepsi tek dosyada 16-bosluk girinti) ancak BIR SONRAKI kalemde fark
edildi. Kapi dalga sonunda kosulursa, arada atilan her checkpoint "gecmis gibi" gorunur ve
`git bisect` okunabilirligi bozulur.
**NUMARA:** mevcut en yuksek tam sayi MK-8 idi (MK-4a/MK-4b harflidir, tam sayi TUKETMEZ) ->
**MK-9** atandi; merkezin beklentisiyle ORTUSUYOR.

### ANNOTATION KURALI INCELMESI (hipotezden OLCULMUS OLGUYA)

Annotation sapmasi **YALNIZ** bilinen alti-satir kumesiyse - `EfEntityRepositoryBase.cs`
satir **45 / 50 / 60 / 61 / 88 / 96** - **ve o dosya diff'te yoksa**, TEK SATIR
"bilinen salinim" notu yeterlidir. Kumenin **DISINA** tasan her sapma `dosya:satir`
incelemesi + diff kesisimi (pozitif kontrollu) ister; `failure` seviyesi -> **DUR**.

**Bu kume VITRIN-FIX-2 kaydiyla (CLAUDE.md 8407-8408) BIREBIR ayni alti satirdir** ve orada
"kaybolan" olarak kaydedilmisti. Yani "yuzeye-cikarma artefakti" artik bir HIPOTEZ DEGIL,
IKI BAGIMSIZ KOSUMDA OLCULMUS BIR OLGUDUR: GitHub check-run basina annotation sayisini
sinirlar ve hangi ornegin yuzeye ciktigi kosumdan kosuma degisir.

### KALICI DERSLER

- **PIN OLCUTU TEK LITERAL BICIME BAGLANMAZ** - esdeger bicimler olcute girer.
  **AILE SAYACI: 4. VAKA.** (1) MFIX-2/M-P8 "assert ESKI LITERAL BICIMI ariyordu, KUSUR
  SINIFINI degil" · (2) MANTIK-FIX-2R/B2-B3 "`innerHTML = ` bosluksuz bicimi kaciriyordu" ·
  (3) MF-3/ITIRAZ-1 "`.length<` regex bicimli kopyayi kacirdi" · **(4) MF-3/FF: kapsam pini
  HAM `CREATE DATABASE`i GORMUYORDU** - `EnsureCreated` dizgesine baglanmisti ve o cagri yeri
  yeniden denemeden YARARLANAMIYORDU.
- **FORM <-> DTO ALAN ESLEMESI BAGLAMADAN ONCE OLCULUR.** Formda FAZLA olan alan bir YALAN
  uretir (kullanici degistirir, sunucu gormez); EKSIK olan alan PUT-ez semantiginde SESSIZ
  VERI KAYBI uretir. K3b'de ikisi de vardi ve ikisi de ancak ON OLCUMDE gorundu.
- **BAYAT-TOAST DERSI (bayat-ikili ailesinin UI bicimi):** tarayici olcumunden ONCE onceki
  turun kalintisi TEMIZLENIR ya da tazeligi DOGRULANIR. AR bacaginda bir an "sozluk
  calismiyor" sanildi; toast sinifinda `on` yoktu, yani metin ONCEKI turdan kalmisti.
- **TR-SERBEST-METIN HATA ESLEME KIRILGANLIGI (N2).** K3 ve K3b'nin **IKISI DE** ayni capaya
  dayaniyor: sunucu iki durumu da 400 + serbest TR metinle donduruyor, MAKINE-OKUNUR sinyal
  YOK. Capalar HAM YANITTAN kopyalandi ve CIFT BICIM (Turkce harf katlama) ile kuruldu.
  Sunucu metni degisirse esleme duser ve kullanici NOTR mesaji gorur - yanlis degil, yalnizca
  daha az yardimci. Kalici cozum bir HATA KODU alanidir (devirde).
- **ROTA ADLARI ASIMETRIK - API HARITASI OLCULUR, TAHMIN EDILMEZ.** `price-drop` TIRELI ama
  `StockNotification` DEGIL; bu asimetri bu dalgada bir tahmin hatasi dogurdu. SDP 1.7/2'ye
  bu dalgada **UC dusus** oldu.
- **ORTAM DERSI:** `schtasks /End` sureci **OLDURMEZ**; PID ile `Stop-Process` gerekir. Kosan
  API `Divisima.Bussiness.dll`i kilitler -> `MSB3027`; build ONCESI durdurulur, SONRASINDA
  yeniden baslatilir ve bes arguman TEYIT EDILIR.
- **DEFTER TEKILLIGI:** tur kayitlari tarifte ADI GECEN deftere yazilir.
- **MK-4b DENETCISININ URETIM-MUTASYONLU PIN AVI ARTIK STANDART UYGULAMADIR** - IKI DALGADA
  UST USTE GERCEK BOSLUK buldu (MANTIK-FIX-2R'de B2/B3, MF-3'te ITIRAZ-1). Denetci, ana
  akisin pinini kendi worktree'sinde mutasyonla sinamadikca "pin saglam" denmez.
- **MERKEZ KURALI:** CC'yi baglayan her karar **YALNIZ BLOKTA** iletilir. K3b karari daha once
  verilmis ama blok disinda kaldigi icin CC'ye ulasmamisti; merkez bunu KENDI SUREC HATASI
  olarak kaydetti.
- **SUZGEC KUTUPHANESI - S1 GIRDISINE NOT:** POZ fiksturu `rson.json`. `cib9c` altindaki
  suzgecsiz listeleme (`runs.json`) **KULLANILMAZ** - filtresiz oldugu icin S1'i 10 dondurur.

---

## (c) DEVIRLER

### MANTIK-FIX-2R HANESINE AYRI KAYIT

**"push+muhur turu: 8 hata"** - MANTIK-FIX-2R'nin dalga ici **10** hatasiyla
BIRLESTIRILMEDEN, AYRI bir kalem olarak durur.

### GUVENLIK-AV-1 GIRDILERI

- **Access token iptali** - sifre degisiminden sonra eski access token YASIYOR
  (`RevokeAsync` uretimde 0 cagri, `user_sessions`ta `jti` kolonu YOK).
- **Hata kodu birlestirme** - TR serbest metin capalarinin kirilganligi (K3 + K3b ayni capa).
- **K4 telafisinin ATOMIKLESTIRILMESI** - bugun iki ayri `SaveChanges`; kismi durum mumkun.
- **`ExecuteDeleteAsync` <-> transaction ROLLBACK olcumu** - K2 `DeleteWhereAsync`i
  transaction ICINDE cagiriyor; rollback davranisi SINANMADI (denetcinin kor noktasi).
- **`guest_name` UZUNLUK DOGRULAMASI YOK** - uye yolu `MaximumLength(120)` istiyor, misafir
  yolunda sinir yok ve `full_name` kolonu 150 karakter; uzun ad EF insert'te 500 uretir.
  Manager'in KENDI dogrulama bolgesine ait oldugu icin bu dalgada dokunulmadi.
  **FIX GUVENLIK-FIX ADAYI.**

### MF-4 DEVIRLERI

- **Misafir istemcisi sunucunun HAM TR metnini gosteriyor** (`er.textContent = e.message`) -
  ONCEDEN VAR, K5'in yeni mesajlari o sizinti yuzeyini GENISLETTI. Ikinci bir hata-eslemesi
  kopyasi acmamak icin dokunulmadi.
- **Uc olu sozluk anahtarinin silinmesi:** `pf_err` · `pf_pass_ok` · `pf_pass_short`.
- **TELEFON-KURAL TARAMA PINI** - dort kopya bugun BIREBIR ayni (ayrisma 0), ama koruyucu
  tarama YOK. **Ortak `RuleBuilder` karari GUVENLIK-AV-1 SONRASINA.**

### BILINCLI STATUKO

- **Pasif musterinin ikinci silme denemesi 404 aliyor** - `GetAsync` global filtre altinda
  calisiyor. ONCEDEN VAR OLAN davranis; **kabul edildi, is ACILMADI.**
- **Retry yuklemi tasarim notu:** timeout sinifi artik **CREATE sinirinda** karsilaniyor;
  genel yoldaki **"yalniz 1807"** BILINCLI STATUKODUR ve degismedi.

### D-YAN TEMIZLIK LISTESINE

- **CANLI KVKK IHLALI: adres 55 / musteri 93** (silinmis hesap, TAM PII) - KR6 geregi bu
  dalgada DOKUNULMADI; duzeltme YENI silmelerde gecerli.
- Bu dalganin kurgusu: musteri 120-135 (120/121 SILINDI; 125/133 KIMLIK BOSLUGU),
  adres 78-95, siparis 269-277 (**270-275 BOZUK ADRESLI - R-H5 ONCE kaniti**),
  fatura 102-110, 3 yetim outbox satiri.

---

## (d) KUYRUK

```
1. MF-4 [VITRIN + i18n]                                        <- SIRADA
2. GUVENLIK-AV-1 (ilk genisletilmis-tarama pilotu; tetik kelimesini prompt'a merkez ekler)
3. GUVENLIK-FIX paketi
4. FIX-1B
5. ADMIN-FIX
6. IMPORT-FIX
7. FIX-1C
8. LOG-FIX
9. FIX-2
10. FIX-3 / B13
```

---

