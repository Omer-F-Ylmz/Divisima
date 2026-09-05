# 54 · ARSIV-4 (CLAUDE.md KESIMI, zemin 1d67cf6)

**Amac:** CLAUDE.md'yi `<=60.000 B`a indirmek. Butce **81.920 DEGISMEZ**.
**Usul:** `49·ARSIV-3` AYNEN - oz tek satir + isaretci; tam metin ilgili muhurde.
**HICBIR BAGLAYICI KARAR KAYBOLMAZ:** karar envanteri ONCE/SONRA `comm` iki yon BOS
(ureten ifade ve sonuc bolum 3'te).
**Bu dosya, CLAUDE.md'den KESILEN satirlarin BAYT-AYNI kopyasidir** (bolum 2) ve
FAZ A olcum tablolarini tasir (bolum 1).

---

## 1. FAZ A — OLCUM TABLOLARI

### 1.1 BOLUM BAYTLARI (ureten ifade: `awk 'NR>=a && NR<=b' CLAUDE.md | wc -c`)

```
ONSOZ      (1-2)            5 B
B0+B1-B3   (3-277)     17.608 B      DOKUNULMAZ (kanit standardi, push/kod sinirlari, SQL, assert)
B2/SUREC   (278-309)    1.872 B      DOKUNULMAZ (skill isaretcileri)
B4         (310-557)   12.722 B      DOKUNULMAZ (MK-1..MK-10 bayt-ayni)
B5         (558-644)    4.267 B      KESIM: EMEKLI girdileri
B6         (645-856)   10.893 B      KESIM: aile tek satir, her ders <=1 satir
B7         (857-989)    8.244 B      KESIM: kurgu MAX tek satir + taban tek satir
B8         (990-1088)  10.397 B      KESIM: 50·GF-4 ve 52·GF-5 -> oz tek satir
B9         (1089-1288) 12.917 B      KESIM: BILINEN/KAPANANLAR/ERT-DEFTER/VITRIN/D-YAN/kuyruk
--------------------------------------
TOPLAM                 78.925 B      (dosya olcumu BIREBIR)
```

### 1.2 B8 SATIR ENVANTERI (990-1088; ureten ifade: `grep -cF -- "- \`<onek>"`)

```
onek         satir   bayt     karar
00a:            11   1.101    DEGISMEZ (merkez kararlari)
36·MANTIK        1     118    DEGISMEZ (EK-1)
37·MANTIK        2     235    DEGISMEZ (EK-1)
38·MANTIK        1     126    DEGISMEZ (EK-1)
39·MANTIK        1     275    DEGISMEZ (EK-1)
44·GF-1·         5     616    ZATEN OZ (ARSIV-3 kalibi)
45·GF-1b         6     691    ZATEN OZ
46·GF-2a         5     582    ZATEN OZ
47·GF-3          8     909    ZATEN OZ
48·GF-2b         5     610    ZATEN OZ
50·GF-4          4     752    -> OZ TEK SATIR + isaretci
51·AV-2          2     563    KALIR (launch bloker olcutu + kapsam kurali)
52·GF-5          3   1.749    -> OZ TEK SATIR + isaretci
00b:             3     323    DEGISMEZ (acik supheli)
NEG kontrol (uydurma muhur oneki)  0    (dedektor calisiyor)
```

### 1.3 B6 DERS / AILE ENVANTERI (645-856; 13 `kaynak:` etiketi, 19 alt baslik)

```
blok                                                      bayt
Aile sayaclari (olculdu, tahmin degil)                     336
DENETIM (MK-4) - UC DENETCI, IKISI GERCEK KUSUR BULDU      680
Annotation salinimi (hipotezden olculmus olguya)           843
MF-4 push turunun dort dersi                                94
PUSH TURUNUN EK CC HATASI (1)                              401
KURGU KAYIT ENVANTERI (baslik artigi)                       26
Rig kor noktalari                                           54
RIG KOR NOKTASI - KAYDA IKI EKLEME                         909
Ortam tuzaklari                                            965
Ortam tuzagi - rota asimetrisi                             454
Ortam tuzagi - runtime sozluk enjeksiyonu (+ 4 ic blok)  2.731
Suzgec dersi - basliksiz kaynak etiketi (ARSIV-1)          447
Dalga dersleri - AILE GRUPLARI                           2.875

AILE SAYACLARI (KORUNUR, ureten ifadeyle olculdu):
  KACIS-KAYBI AILESI 3 gecis · AILE SAYACI 1 · AILE GRUPLARI 1 ·
  ALTINCI ORNEK 2 · "4. VAKA" 1 · "5. vaka" 1 · NEG (uydurma aile adi) 0
```

### 1.4 B9 ALT-BLOK BAYTLARI (1089-1288)

```
alt-blok                                          bayt
Kuyruk (merkez metni, AV-1 muhru)                7.164   <- KAPANANLAR 2.054 + BILINEN 1.715 icinde
Devir ID'leri (baslik)                             175
DEVIR ID'LERI (govde)                            1.190
VITRIN-KALAN (baslik)                               83
VITRIN-KALAN (YENI KUYRUK KALEMI - TEK LISTE)    1.502
ERTELENMIS-DEFTER (yeni sinif, ARSIV-1/S5)       1.830
AV-2 GIRDILERI (39·MF-3'ten acik kalanlar)         910
POZ kontrol: "KAPANANLAR" 1 gecis · NEG (onekli uydurma capa) 0
```

### 1.5 B7 / B5 KESIM HEDEFLERI

```
B7 (8.244 B)                                      bayt
  Olcum duzenegi (goz1) - bes arguman              415   KALIR (her rapor anmak zorunda)
  Kurgu envanteri ve muhurler (baslik)             125
  KURGU KAYIT ENVANTERI                          6.537   -> kurgu MAX tek satir + taban tek satir
  D-YAN temizlik listesi                           467   -> B9'daki D-YAN isaretcisiyle birlesir
  D-YAN devri (MF-4)                               665   -> ayni
  ("<dalga> HICBIR KURGU KAYDI URETMEDI" satiri 9 · "Suit tabani" satiri 6)

B5 (4.267 B)
  EMEKLI blok sayisi 6 · toplam 1.410 B  -> BU DOSYAYA (bolum 2.1); CLAUDE.md'de
  yalniz CALISAN ifadeler (S1..S6, 6 ureten ifade) KALIR. NEG (onekli capa) 0.
```

---

## 2. CLAUDE.md'DEN KESILEN SATIRLAR (BAYT-AYNI)

### 2.1 B5 — EMEKLI SUZGEC GIRDILERI (CLAUDE.md-den KESILDI)

```
**EMEKLI:** `^      "id":` - ic ice `jobs` nesnelerini de sayiyordu; ayni NEG girdisinde
**1** donduruyor (bugun olculdu).

---
**EMEKLI:** `Basarili!|Basarisiz!` - cikti Turkce oldugu icin ayni dosyada **0** esliyor
(bugun olculdu). Capa `Toplam:` HAM CIKTIDAN KOPYALANDI (MK-7).

---
**EMEKLI:** awk tabanli satir-desenli cikarici - ic ice JSON'da depo id'sini ve aktor adini
run alani saniyordu; **karar icin HIC kullanilmadi**.

---
**EMEKLI:** `[0-9]{10,}` gibi HANE-SAYISINA dayali cikarici - 10 haneli DEPO ID'sini
(`1338865652`) run kimligi saniyordu; MANTIK-FIX-1 push turunda birebir yasandi.
`html_url` capasi kimligi YAPISAL olarak konumlandirir, uzunluk tahminine dayanmaz.

---
**EMEKLI:** `grep -c $'\r' <dosya>` - bu ortamda `$'\r'` BOS DIZEYE cozunuyor ve HER
SATIRI esliyor; ayni NEG girdide (saf LF) de 2 dondu, yani dedektor BOZUKTU. Ilk ARSIV-1
olcumunde CLAUDE.md "12434 satirin 12434'unde CR" gorundu ve "kabul kriteri CR 0 ile
CELISIYOR" sanildi; `tr -cd` ile yeniden olculunce CR baytinin 0 oldugu, celiskinin
OLMADIGI cikti. skill `sdp` · 1.7/1'in bu turdaki kazanci.

---
**EMEKLI:** `grep -oiF` - bu kabukta `-o` + `-i` + `-F` BIRLIKTE HICBIR SEY dondurmuyor;
ayni POZ girdide **0** doner. 27 ankrajlik bir kor-nokta taramasi bu dedektorle
"27/27 sifir" verdi ve SSRF disindaki **26 sonuc YANLISTI**. Kapsam elestirmeni "27/27
sifir makul degil" deyip POZ kontrol kosunca yakalandi (`51·AV-2`).
---

---
```

### 2.2 B7 — KURGU KAYIT ENVANTERI + D-YAN BLOKLARI (CLAUDE.md-den KESILDI)

```
## KURGU KAYIT ENVANTERI

**MF-4 UYGULAMA FAZI HICBIR YENI KAYIT URETMEDI**; Omer'in hesabi (musteri 10) ve kabul
turu kayitlari KULLANILMADI.
MAX musteri **168** · siparis **286** · adres **119** · fatura **119** · Pending(id>210)
**10** — kaynak `42·GUVENLIK-AV-1 · KURGU`
**GF-1 KOD FAZI hicbir kurgu kaydi uretmedi** (MAX'lar ve MK-3 uclusu push aninda BIREBIR).
Kapanis fazinda goz1'de TEK kayit uretildi: musteri **169** `gf1.1@example.com` (uretim
yolundan: register -> verify -> login) + `consent_records` 1 + `user_sessions` 340-342.
**MAX musteri 168 -> 169**; siparis/adres/fatura/Pending DEGISMEDI.
Tek sema degisikligi `user_sessions.auth_time` kolonudur (`44·GUVENLIK-FIX-1`).
**GF-1b HICBIR KURGU KAYDI URETMEDI** - testler ayri CI/sinif veritabanlarinda kostu, dev
DB'ye YALNIZ OKUMA yapildi. MAX musteri **169** BIREBIR kaldi; `user_sessions` **342**
(K3 geriye donuk ozetleme YAPMADI, bu satirlar fiilen OLU oturum - `45·GUVENLIK-FIX-1b`).
Suit tabani `45·GUVENLIK-FIX-1b` kapanisinda **Sql 378/378 · tam 641/644** (3 kirmizi =
bilinen Docker uclusu); ureten ifade `dotnet test ... --filter "Category=Sql"` ve filtresiz.
**GF-2a HICBIR KURGU KAYDI URETMEDI** (olculdu: `email LIKE 'gf2a%'` 0 · `name LIKE '%GF2A%'
COLLATE Latin1_General_BIN2` 0). Olcumler tarayicida SENTETIK girdilerle yapildi, DB'ye
YAZILMADI. MAX'lar BIREBIR: musteri **169** · urun **955** · siparis **286** ·
`user_sessions` **342** · Pending(status=0, id<=210) **35/3837**.
Suit tabani `46·GUVENLIK-FIX-2a` kapanisinda **Sql 378/378 · tam 651/654** (+10 pin).
**GF-3 HICBIR KURGU KAYDI URETMEDI** (olculdu: `email LIKE 'gf3%'` -> 0; olcumler ayri test
veritabanlarinda ve ikinci API surecinde SENTETIK girdilerle yapildi). MAX'lar BIREBIR:
musteri **169** · urun **955** · siparis **286** · adres **119** · fatura **119** ·
`user_sessions` **342** · Pending(status=0, id<=210) **35/3837**.
Suit tabani `47·GUVENLIK-FIX-3` kapanisinda **Sql 382/382 · tam 710/713** (+59 pin;
uc kirmizi = bilinen Docker uclusu). Ureten ifade: `dotnet test ... --filter "Category=Sql"`
ve filtresiz.
**GF-2b HICBIR KURGU KAYDI URETMEDI** (olculdu: `email LIKE 'gf2b%'` -> 0; olcumler ayri
test veritabanlarinda ve tarayicida SENTETIK girdilerle yapildi, panel giris denemesi VAR
OLMAYAN bir adresle kosuldu). MAX'lar goz turu kapanisiyla BIREBIR: musteri **171** ·
urun **955** · siparis **286** · adres **119** · fatura **119** · `user_sessions` **356** ·
Pending(status=0, id<=210) **35/3837**.
Suit tabani `48·GUVENLIK-FIX-2b` kapanisinda **Sql 382/382 · tam 730/733** (+20 pin;
uc kirmizi = bilinen Docker uclusu). Ureten ifade:
`dotnet test Divisima-Backend.sln -c Release --filter "Category=Sql"` ve filtresiz.
**GF-4 HICBIR KURGU KAYDI URETMEDI.** MAX'lar GF-2b kapanisiyla BIREBIR (musteri 171 ·
urun 955 · siparis 286 · adres 119 · fatura 119 · `user_sessions` 356 · Pending 35/3837).
**CAPA TUZAGI - KAYIT:** onceki muhurlerin `email LIKE 'gfN%' -> 0` kanit bicimi GF-4 icin
KIRLIDIR - `'gf4%'` bugun **11** satir donduruyor (id 55-65) ve onbiri de **25 Agustos
2026** tarihli AGUSTOS dalgasinin kurgusudur (collation tuzagi DEGIL; BIN2 ile de 11).
Durust ureten ifade tarih niteleyicisi ister: `... AND created_at >= CAST(GETDATE() AS date)`
-> **0**.
Suit tabani `50·GUVENLIK-FIX-4` kapanisinda **Sql 382/382 · tam 743/746** (+13 pin;
uc kirmizi = bilinen Docker uclusu, yerelde Docker YOK). Ureten ifade ayni.
**GF-5 KURGU (`52·GUVENLIK-FIX-5`)** - ureten ifadeleriyle:
`SELECT MAX(id) FROM customers` -> **184** (178 `gf5.b.1@` · 179 `gf5.guest.1@` **SD-7 yetimi,
ESKI kodun urunu** · 182 `gf5.1@` · 184 `gf5.guest.5@`; **180/181/183 SAF KIMLIK BOSLUGU**,
`SELECT COUNT(*) ... WHERE id BETWEEN 180 AND 183 AND id<>182` -> 0).
siparis **287** · adres **126** · fatura **120** · `COUNT(*) user_sessions` **372** ·
`security_events` **46** (altisinda ip+ua DOLU; GF-5 oncesi 0/40) · `audit_logs` 4328.
`SELECT COUNT(*),MIN(id),MAX(id),SUM(CAST(id AS bigint)) FROM orders WHERE status=0 AND
id<=210` -> **35 / 9 / 210 / 3837 BIREBIR**. Yetim adres 0 · yetim siparis 0 (depo geneli);
musteri 184 TAM uretim imzasi tasiyor. Elle INSERT YOK, sema degisikligi YOK.
Suit tabani `52·GUVENLIK-FIX-5` kapanisinda **Sql 382/382 · tam 777/780** (+34 pin;
uc kirmizi = ayni bilinen Docker uclusu). Ureten ifade ayni.
**AV-2 DORT KURGU KAYDI URETTI (SALT OLCUM turu, hepsi URETIM YOLUNDAN):** musteri
**172** `av2.sb.1@` · **174** `av2.sf.1@` · **175** `av2.sd.2@` · **177** `av2.sc.1@`.
URETIM IMZASI: dordunde de `password_hash` **69** / `password_salt` **16** (GF-1/K6 v2
zarfi). **173 ve 176 SAF IDENTITY BOSLUGUDUR** - bes FK tablosunda (`addresses`, `orders`,
`consent_records`, `user_sessions`, `carts`) 0 yetim satir.
**D-YAN:** musteri **175 MISAFIR YOLUNDAN dogdu** (register DEGIL; SD-7'nin 151-karakter
reprosu), bu yuzden `consent_records` **0** tasiyor ve o e-posta misafir checkout'tan
KALICI olarak disaniyor. Temizlik karari merkezin.
MAX'lar kapanista: musteri **177** · urun **955** · siparis **286** · adres **119** ·
fatura **119** · `user_sessions` **369** · Pending(status=0, id<=210) **35/9/210/3837**
(uc olcumde de BIREBIR; `orders`'a Pending URETILMEDI).
**GF-3 TABANI AD ALANI KAPALIYKEN ALINMIS (kayit):** `SemaTekKaynakTests` kosucu ad alanini
yalniz baglanma noktasina uyguluyordu; yaratma ve dusurme HAM adi kullaniyordu. Bu yuzden
`DIVISIMA_TEST_DB` SET edildiginde - ki MK-4b bunu ZORUNLU kilar - dort test SQL login
hatasiyla dusuyordu ve MK-4b tabani fiilen OLCULEMIYORDU. GF-2b/F1 ile yapisal olarak
kapatildi; **733/730 tabani ilk kez env SET edilmis turda da dogrulandi.**

**TEK YAZMA - URETIM YOLUNDAN:** K2 kanitini almak icin musteri 102'nin
(`mfix1.once@example.com`, MANTIK-FIX-1 kurgusu) sifresi **uretim yolundan** sifirlandi:
`POST /api/auth/forgot-password` 200 -> jeton `customers.password_reset_token`'dan okundu ->
`POST /api/auth/reset-password` 200 -> `POST /api/auth/login` 200. Elle SQL YOK. (Kurgu sifre
degeri muhre GIRMEZ; "politikaya uygun kurgu" olarak anilir.)

**MK-3 UCLUSU BIREBIR TUTTU (ureten ifadeleriyle):**
```
SELECT COUNT(*), MAX(id) FROM orders WHERE customer_id = 10;              -> 38 / 211
SELECT COUNT(*), MIN(id), MAX(id), SUM(CAST(id AS bigint))
  FROM orders WHERE status = 0 AND id <= 210;                             -> 35 / 9 / 210 / 3837
SELECT COUNT(*), SUM(total_price), STRING_AGG(CAST(status AS varchar),',')
  FROM orders WHERE customer_id = 74 AND id BETWEEN 234 AND 237;          -> 4 / 4698,60 / 0,0,1,1
```


## D-YAN temizlik listesi

kaynak: 39·MANTIK-FIX-3_MUHRU · D-YAN TEMIZLIK LISTESINE

### D-YAN TEMIZLIK LISTESINE

- **CANLI KVKK IHLALI: adres 55 / musteri 93** (silinmis hesap, TAM PII) - KR6 geregi bu
  dalgada DOKUNULMADI; duzeltme YENI silmelerde gecerli.
- Bu dalganin kurgusu: musteri 120-135 (120/121 SILINDI; 125/133 KIMLIK BOSLUGU),
  adres 78-95, siparis 269-277 (**270-275 BOZUK ADRESLI - R-H5 ONCE kaniti**),
  fatura 102-110, 3 yetim outbox satiri.


## D-YAN devri (MF-4)

kaynak: 40·MANTIK-FIX-4_MUHRU · DV2 (D-YAN'a devredilen)

DV2  Yetim musteri 153 ve 155 + siparis 270-275 (bozuk adresli, R-H5 ONCE kaniti) -> D-YAN
DV3  429 UC AYRI KAYNAKTAN (cop-misafir guard'i · Redis rate-limit · yerlesik limiter -
---


D-YAN: AV-1 kurgusu m159-168 `gav1.*` · `user_sessions` 331-339 · `review_helpful_votes` 1 ·
admin 118 sifre sifirlama · 14 satirlik yan etki tablosu -> `42·GUVENLIK-AV-1 · KURGU ENVANTERI`

**D-YAN bloklari kumulatiftir; en guncel liste 39·MANTIK-FIX-3'tedir. Onceki bloklar
su arsiv dosyalarinda: 26 · 27 · 30 · 31 · 32 · 33 · 34 · 35 · 36 · 37 · 38 (INDEX.md ile
cozulur).**
```

### 2.3 B6 — DERS BLOKLARI (CLAUDE.md-den KESILDI, oz satirlar CLAUDE.md-de kaldi)

```

## Aile sayaclari (olculdu, tahmin degil)

kaynak: 37·MANTIK-FIX-1_MUHRU

**KACIS-KAYBI AILESI - ALTINCI ORNEK:** P22'de `"\s+"` heredoc'ta `"\s+"`ya indi ve C#
**CS1009** verdi; `sed`/`perl` duzeltmeleri de ayni kacisi yedi. **KACISSIZ COZUME gecildi**
(regex yerine duz `Replace` zinciri). Kayitli dersin bir kez daha dogrulanmasi.

## DENETIM (MK-4) - UC DENETCI, IKISI GERCEK KUSUR BULDU

kaynak: 39·MANTIK-FIX-3_MUHRU

  **AILE SAYACI: 4. VAKA.** (1) MFIX-2/M-P8 "assert ESKI LITERAL BICIMI ariyordu, KUSUR
  SINIFINI degil" · (2) MANTIK-FIX-2R/B2-B3 "`innerHTML = ` bosluksuz bicimi kaciriyordu" ·
  (3) MF-3/ITIRAZ-1 "`.length<` regex bicimli kopyayi kacirdi" · **(4) MF-3/FF: kapsam pini
  HAM `CREATE DATABASE`i GORMUYORDU** - `EnsureCreated` dizgesine baglanmisti ve o cagri yeri
  yeniden denemeden YARARLANAMIYORDU.
- **FORM <-> DTO ALAN ESLEMESI BAGLAMADAN ONCE OLCULUR.** Formda FAZLA olan alan bir YALAN
  uretir (kullanici degistirir, sunucu gormez); EKSIK olan alan PUT-ez semantiginde SESSIZ

## Annotation salinimi (hipotezden olculmus olguya)

kaynak: 39·MANTIK-FIX-3_MUHRU

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


## MF-4 push turunun dort dersi

kaynak: 40·MANTIK-FIX-4_MUHRU · PUSH TURUNUN EK CC HATASI

## PUSH TURUNUN EK CC HATASI (1)

**YONLENDIRME SIRASI.** `dotnet test ... 2>&1 > dosya` yazildi; bu, stderr'i ESKI stdout'a
(terminale) baglar ve stdout'u dosyaya yonlendirir - yani `[FAIL]` satirlari log dosyasina
GIRMEZ. TUR1'de kirilan adlar yalniz EKRAN CIKTISINDAN okunabildi. Dogrusu `> dosya 2>&1`;
TUR2 ve TUR3 duzeltilmis bicimde kosuldu ve adlar log'dan `comm`/`diff` ile karsilastirildi.

## KURGU KAYIT ENVANTERI

## Rig kor noktalari

kaynak: 40·MANTIK-FIX-4_MUHRU

## RIG KOR NOKTASI - KAYDA IKI EKLEME

Dalga 4'ten beri kayitli olan "harness compositing yapmiyor" siniri bu turda IKI YENI
bicimde karsimiza cikti:

1. **CSS TRANSITION ILERLEMIYOR.** K6'nin mobil olcumunde `.filter-side` elemanina `.open`
   sinifi eklendi ve transform **DEGISMEDI** (700 ms beklendigi halde). Sebep: `requestAnimationFrame`
   ateslemedigi icin `transition:transform .32s` hic ilerlemiyor. `transition:none !important`
   ile tekrarlanarak dogru degerler alindi (AR kapali +343.2, acik `none`).
   **KURAL: gecise bagli hicbir geometri olcumu DOGRUDAN alinmaz.**
2. **JS/DOM KOSUCUSU YOK.** Bu dalganin ALTI pini de KAYNAK SOZLESMESI pinidir; tarayici
   semantigi (hit-test, CSS ozgullugu, computed style, `elementFromPoint`) CI'da
   pinlenemiyor. Davranis kaniti YALNIZCA muhurdeki tarayici olcumleridir. Dalga 4'ten beri
   acik kalem (yeni bagimlilik + `dependency-scan` kapsami).


## Ortam tuzaklari

kaynak: 37·MANTIK-FIX-1_MUHRU (sqlcmd -I · tireli rota · 338/339)

**ORTAM DERSLERI (kalici):** `sqlcmd` bu ortamda **QUOTED_IDENTIFIER kapali** baslar ve
filtreli indeksi olan tabloya `UPDATE` **Msg 1934** ile duser -> **`-I` bayragi ZORUNLU** ·
`gift-card` rotasi **TIRELI** (`api/gift-card`) ve `GiftCardCreateDto` **yalniz `amount`**
tasir · `schtasks` Git Bash'ten cagrilinca yol cozumleme bozulur, **PowerShell** uzerinden
cagrilir · build ONCESI API sureci DURDURULUR (MSB3027/MSB3021 DLL kilidi), SONRASINDA
yeniden baslatilir ve bes arguman TEYIT EDILIR. Dordu de KAYNAKTAN okunarak cozuldu.


**338/339** gorundu; ADI YAKALANMADI (grep deseni mesaji disarida birakti). Ayni anda alinan
tam suit 575/578 (yani 4 degil 3 kirmizi) - **TUTARSIZ**. Worktree kaldirildiktan sonra iki
ardisik kosum 339/339. En olasi aciklama paylasilan test veritabanlari (kural-uyum M2-2'de
`already exists` cakismasi olctu) **ama BU ISPAT DEGIL**.


## Ortam tuzagi — rota asimetrisi

kaynak: 39·MANTIK-FIX-3_MUHRU (rota asimetrisi)


1. **ROTA TAHMINI** - `stock-notification` sanildi, dogrusu `api/StockNotification`.
   `price-drop`un GERCEKTEN tireli olmasi yaniltti. **skill `sdp` · 1.7/2 - bu dalgada 1. dusus.**
2. **P-H2 fiksturu** var olan bir urun ariyordu, sinif her testte DB'yi yeniden kuruyor.
3. **BAYAT TOAST** - AR bacaginda ilk olcum Ingilizce metin gosterdi; toast sinifinda `on`

## Ortam tuzagi — runtime sozluk enjeksiyonu

kaynak: 36·MANTIK-AV-1_MUHRU (runtime sozluk enjeksiyonu)

**(ii) SOZLUK BUTUNLUGU KAYNAKTAN PINLENIR, RUNTIME'DAN DEGIL.**
`api-bridge` calisma aninda sozluge anahtar **enjekte ediyor** (bugun 4 tane).
`788 kaynak + 4 enjekte = 792 runtime`. **Runtime uzerinden kurulan bir "sozluk butunlugu"
pini YALANCI YESIL verir** - kaynakta eksik bir anahtar, calisma aninda enjekte edilen bir
anahtarla MASKELENIR. MFIX-3b muhrundeki `792/792` bir RUNTIME olcumudur; dogrudur ama
**KAYNAK butunlugunu KANITLAMAZ**.

---


### Denetim duzeltmesi (ARSIV-1/C3) — eksik tasinan kalici kayitlar

kaynak: 34·MFIX-B_MUHRU · ZORUNLU KAPSAM EKI (admin.html tuzagi)


```
admin.html:306  duzenleme formu stok satirlarini ANONIM detay ucundan dolduruyor
admin.html:376  ayni degerleri geri POST ediyor
ProductManager.cs:292  onu FIZIKSEL kolona yaziyor
=> K1 TEK BASINA gonderilseydi: admin 937'yi acip YALNIZ ADINI degistirip kaydettiginde
   fiziksel 10 -> 4 duser, rezerve 6 kalir, available -2 -> 0 olurdu.
   Dalga B'nin "tam-varlik map -> sessiz veri kaybi" sinifinin BIREBIR tekrari.
```

#### admin.html tuzagi — KALICI KURAL (kapsam elestirmeni)

kaynak: 34·MFIX-B_MUHRU · ZORUNLU KAPSAM EKI (kalici kural)

**KALICI KURAL (bu vakadan dogdu): KAPSAM ELESTIRMENI ROLU, ON OLCUM FAN-OUT'UNUN
ZORUNLU UYESIDIR.** Gorevi bulgu aramak degil, **verilen tarifin kendisinin acacagi kapiyi**
aramaktir. Bu turda merkezin K1 tarifi, bes bagimsiz okuyucu ve ana akis - **dordu birden**
kacirdi; tek eleştirmen rolu yakaladi.


#### Isimsiz flake — durust kayit

kaynak: 37·MANTIK-FIX-1_MUHRU · DURUST KAYIT - ISIMSIZ FLAKE (tam acilis)

**DURUST KAYIT - ISIMSIZ FLAKE:** denetciler kosarken alinan BIR `Category=Sql` kosumunda
**338/339** gorundu; ADI YAKALANMADI (grep deseni mesaji disarida birakti). Ayni anda alinan
tam suit 575/578 (yani 4 degil 3 kirmizi) - **TUTARSIZ**. Worktree kaldirildiktan sonra iki
ardisik kosum 339/339. En olasi aciklama paylasilan test veritabanlari (kural-uyum M2-2'de
`already exists` cakismasi olctu) **ama BU ISPAT DEGIL**.


#### Kacis-kaybi sayaci (HAM)

kaynak: 40·MANTIK-FIX-4_MUHRU · CC HATALARI (kacis-kaybi sayaci, HAM)

**8. HATA KACIS-KAYBI AILESINE GIRMEZ - OLCULDU.** O vakada kaynak dosyada `'⌂'`
KACIS OLARAK yazili, ben gercek karakteri (⌂) aradim: **kayip yok, eslesme bicimi farki**.
Ailenin sayaci `git log -S` ile olculdu: `"KACIS-KAYBI AILESI - DORDUNCU ORNEK"` 1 commit ·
`"... ALTINCI ORNEK"` 1 commit (`a5add91`) · `"... BESINCI ORNEK"` ve `"... YEDINCI ORNEK"`
**0 commit** (NEG kontrol `ZZZINCI` 0). **Sayac ALTINCI'da KALIR.**
**KAYIT:** MK-4b denetcisinin MUT-3b turunda gercek bir kacis-kaybi yasandi (`sed` ters bolu
## Suzgec dersi — basliksiz kaynak etiketi (ARSIV-1)

Bir etiketin yapisal basligi olup olmadigini `grep -B1 "^kaynak: " | grep -c "^--$"`
ile saymak AYRAC sayar, etiket saymaz: ilk kosumda 34 dedi. Dogru ifade iki-ust-satiri
kontrol eder: `awk '/^kaynak: /{ if (p2 !~ /^#/) n++ } { p2=p1; p1=$0 } END{print n+0}'`.
POZ (C2/2474135) 4 · NEG (`^ZZZkaynak:`) 0 · C4 sonrasi 0.
kaynak: ARSIV-1 denetim turu, muhurde 41·ARSIV-1 · CC HATALARI 4

## Dalga dersleri — AILE GRUPLARI (tam metin muhurde, kesilenler 49'da)

**A · CAPA / ESLESME BICIMI (5 vaka)**
- Capa POZ olcumu "kac" yaninda "NEREDE" sorar - sayim dogru/konum yanlis. -> 43
- Indeks/kisit sayimi DOSYA-GENELI grep ile; blok penceresi YOKLUK KANITI DEGILDIR. -> 44
- Sink sayimi eslesme-bicimi kusuru tasiyabilir: satir sonunda biten atama gorunmez. -> 46
- Assert KUSUR SINIFINI pinler, ESKI LITERAL BICIMINI degil (5. vaka). -> 48
- NEG capa dizesi belgeye YAZILMAZ; NEG kontrolu raporda/muhurde anilir. -> 43

**B · TEK KANAL / KAYNAK BUTUNLUGU (4 vaka)**
- **YORUM != OLCUM - IKI TUR UST USTE.** `AuthManager.cs:468` "ikisi de zaten
  security_events'te tutuluyor" diyor (olculdu: TUTULMUYOR, ip/ua 0/40) ve
  `AuditLogController` yorumu "40 controller icinde TEK ornek (olculdu)" diyor
  (olculdu: `SeoController` ayni kusuru tasiyor ve iddiadan ALTI GUN once oradaydi). -> 51
- Tek kanalli on olcum bulgusu = SUPHE; tarife KALEM OLMAZ. -> 47
- BILINEN listesi B8 fragmanlarindan KURULMAZ; 00a/00b tam metni okunur. -> 42
- RUNTIME SOZLUK = DB METNI; kaynak okuyana "sabit" gorunur, DEGILDIR. -> 46

**C · GERI ALMA / DOSYA GUVENLIGI (3 vaka)**
- Yeni test/pin dosyasi yazilmadan ONCE yol YOKLUGU olculur; `git status`ta `M` = DUR. -> 47
- MK-6 mutasyon dongusu `git status --porcelain` BOS DEGILSE CALISMAZ. -> 47
- Kirmizi-once geri almada `git stash` KULLANILMAZ; olcum yedegi + elle geri alma + md5. -> 45

**D · SIR HIJYENI (1 vaka)**
- Ham yanit dokumleri diske MASKELI yazilir; ajan ortak kurali "diske yazilmaz"i da icerir. -> 42

**E · URETIM DAVRANISI (1 vaka)**
- `ExecuteUpdateAsync` `AuditInterceptor`i ATLAR; CAS yolunda denetim kaydi ELLE yazilir. -> 45

**F · RIG / OLCUM DUZENEGI (7 vaka)**
- **goz1 API'sinin saglik ucu `/health` (200); `/api/health` YOKTUR (404).** Surec adi
  `Divisima.API` DEGIL **`dotnet`**tir. PowerShell `Invoke-WebRequest` 404'te ISTISNA atar -
  iki dedektor birden "rig kalkmadi" der, oysa rig KOSUYORDUR. -> 51
- `Directory.Build.props` XML'i BOZUKKEN `dotnet restore` exit 0 verir ve MSB4024 BASMAZ;
  ozellik projeye ULASTI MI sorusu yalniz `msbuild -getProperty` probuyla yanitlanir. -> 50
- MCR digest'i Accept turune gore DEGISIR: manifest LISTESI olmayan imajda liste turleri
  istenirse Schema 1 doner ve o digest CEKILEMEZ; digest her zaman GET ile dogrulanir. -> 50
- CR dedektoru olarak YALNIZ `tr -cd '\r' | wc -c` calisir; `awk '/\r$/'` ve
  `grep -c "$(printf '\r')"` bu kabukta 0 doner (`grep -P` de calismaz). -> 50
- Harness fetch katmani SW kaydini engeller; SW kabulu GERCEK CHROME ister. -> 48
- Chrome/CDP rig: `--user-data-dir` %LOCALAPPDATA% altinda (temp'te Cache Storage KIRIK);
  offline kaniti SUNUCUYU DURDURARAK alinir - `emulateNetworkConditions` SW'yi KAPSAMAZ. -> 49
- `register()` OK, SW KAYDI DEMEK DEGILDIR; kanit `getRegistrations` + `active` + controller. -> 49
```

### 2.4 B8 — 50·GF-4 ve 52·GF-5 KARAR SATIRLARI (CLAUDE.md-de OZ TEK SATIRA indi)

```
- `50·GF-4·K1` Tum GitHub action'lari 40-hane COMMIT SHA'sina pinli + surum yorumu; major yukseltme de bu usulle. -> 50
- `50·GF-4·K4` Paket kaynagi TEK (`NuGet.config` + `<clear />`) · her projede `packages.lock.json` · CI `restore --locked-mode` (CI SDK 8'de YESIL kosuldu). -> 50
- `50·GF-4·K5` Imaj referansi TEK KAYNAK: dort site ayni tag+digest, pinle zorunlu. Digest **Schema 2 POZ/NEG cozucuyle** alinir (etiketten okunan deger TEK BASINA gecersiz; digest'le geri cekilip echo-back sinanir). -> 50
- `50·GF-4·K7` AutoMapper 12.0.1 KALIR (lisans degisimi **15.0.0**); `NuGetAuditMode=all` UYARI seviyesi; deprecated adimindaki `\|\| true` BILINCLIDIR (o komut bulguda da exit 0 verir, kaldirmak olmayan bir kapiyi var sandirir). -> 50

- `52·GF-5` **OLAY YUZEYI:** kayitsiz **ve kilitli** hesap girisi · logout (iki dal) · sahiplik ihlali `IdorAttempt` **kapsam DUZELTILDI (`53·AV-3`): cagri yeri IKI - `IyzicoPaymentManager`(`order`) + `OrderManager`(`address`); "Order+Payment" YANLISTI** (bes uctan yalniz `payment/initialize` iz birakiyor) · 429 **ornekleme ip+uc basina 60 sn**, `customer_id` NULL **kabul edilmis sinir** (middleware `UseAuthentication`'DAN ONCE) · bozuk imza. **IMZASIZ webhook 404 STATUKO = KABUL EDILMIS RISK** (otorite retrieve zinciri; K7 DUSTU - saglayici imza GONDERMIYOR, uygulansaydi tum callback+webhook 400 olurdu). ip/ua **`SecurityEventManager` ICINDE** doldurulur; sinir 60 = iki kolonun DARI. `detail` kolon genisligine KIRPILIR. -> 52
- `52·GF-5` **MISAFIR/UYE GIRDI SINIRLARI TEK KAYNAK `GirdiSinirlari`** (sabit DEGERLER; ortak RuleBuilder ACILMAZ - Seller'a kapsam tasmasin, o kendi literalini korur). `guest_name` <=100 **olcum SANITIZE SONRASI** (`Sanitize` UZATMAZ - bes `Replace(...,"")`+`Trim`; `HtmlEncode` AYRI metot ve bu yolda cagrilmiyor). `request_id` <=80 + `[A-Za-z0-9._-]`, **GUID SARTI ASLA** (dolu 122 degerin 54'u GUID DEGIL; frontend yedek dali `co-...` uretir ve PINLI). E-posta <=200. Sinir degerleri **SEMAYA capalanir**, sabite DEGIL. -> 52
- `52·GF-5` **LOG MASKESI GLOBAL:** Serilog'un IKI sink'i de `MaskeliFormatter` (`ITextFormatter`) uzerinden yazar - **enricher yolu KAPALI** (`LogEvent.Exception` readonly, olculdu) ve yeni paket GEREKMEDI. Cerceve metinleri (SQL "Truncated value", EF `@pN=`) AYRI `LogMetniMaskesi`de; **`KanitMaskesi` olcutu GENISLETILMEZ** (`KanitMaskesiTests` sozlesmesi korunur). GF-3'un "elle `ex` gecirilmez" sozlesmesi SURER - formatter onun YERINE gecmez, ARKASINA eklenir. -> 52
```

### 2.5 B9 — KUYRUK / KAPANANLAR / BILINEN / DEVIR / VITRIN-KALAN / ERT-DEFTER (CLAUDE.md-den KESILDI)

```

## Kuyruk (merkez metni, AV-1 muhru)

**GUVENLIK-FIX BOLUMLEMESI (merkez karari — KAYIT):**
```
GF-1 KIMLIK/OTURUM [backend, migration olasi]: DV1 (BAS) · C-1 · C-2 · B-1 · B-2 · C-4
GF-2a ISTEMCI KACIS [frontend]: D-1 · D-2 · D-3 · D-4 · D-5 · D-10 · D-11 · D-6 · D-8
GF-3 SIZINTI/YAPILANDIRMA/LIMIT [backend config]: E-2 · E-3 · B-09 failed-jobs · E-1a ·
     E-5 · E-4 · E-6 · F-1 · F-2 · A-3
GF-2b CSP [frontend, D-7]: 11 satir ici script disa + unsafe-inline/unsafe-hashes/blob sokum
GF-4 TEDARIK ZINCIRI [CI/paket]: G-2 · G-5 · G-6 · G-4 · G-3 · G-1 = 12.0.1 KALIR
BILINEN/KABUL EDILMIS RISK: C-3 (00a:101) · D-9 · E-1b · Webhook:AllowedIps bos · hibrit jeton
BASKA KUYRUGA: A-2 -> VITRIN-KALAN 8 · F-3 -> IMPORT-FIX
```

**KAPANANLAR** (tam metin muhurde, kesilen satirlar 49'da): ARSIV-1 `c6721b7`/41 ·
AV-1 `c6721b7`/42 · ARSIV-2 `4c29f32`/43 · GF-1 `189ce81`/44 · GF-1b `00b012f`/45 ·
GF-2a `1dd985b`/46 · GF-3 `33cac2e`/47 · GF-2b FAZ 1 `0fd3e62`/48 ·
GF-4 TEDARIK ZINCIRI `4976974`/50 (cift yesil: run 33891017398 · 33891017496) ·
**GUVENLIK-AV-2 (SALT OLCUM) `ce54d0c` zemininde /51** ·
**GF-5 A09 IZ/ATIF + MISAFIR BUTUNLUGU + MASKE `027a88a`/52 — LAUNCH BLOKER 2/2 KAPANDI**
(SD-7 misafir butunlugu · SC-1 A09 iz/atif). K7 DUSTU (D1).
**S-C KAPSAMA MATRISI: `H=8` -> `H=3`, uCu de BILINEN** - 403 yetki reddi (katman engeli:
`Divisima.Core` ProjectReference 0) · webhook IP allowlist reddi (dal sevk edilen
yapilandirmada YAPISAL OLARAK ULASILAMAZ, `00b:229`) · satici login (Seller'a 0 satir).
**ONCEKI TABANIN BOLUNMESI YANLISTI:** `51·AV-2` iki yerde "10/5/7" diyor; tablodan yeniden
sayilinca `E=8 · H=8 · KISMEN=6` (toplam 22 dogru, bolunme yanlis).
**PROVENANS DUZELTMESI (AV-2'de olculdu):** AV-2'nin kapsami `42·GUVENLIK-AV-1`de
"at-rest sifreleme · 2FA/TOTP · TOCTOU/ExecuteUpdateAsync · A09 · olay isleyicileri ·
13 anilmayan controller (Comparison/Collection ham entity suphesi) **· Stock yuzeyi**"
diye yaziliydi; son parca CLAUDE.md'ye TASINMAMISTI (olculdu: muhur 1, CLAUDE.md 0).
Etkisi sifirdi (Stock zaten 13'un uyesi) ama tasima kaybi GERCEKTI - **geri konuldu**.
**D-7 KISMEN**: admin TAM, vitrin `'unsafe-inline'` KABUL EDILMIS RISK; **CSP FAZ B YOK** -> ERT-DEFTER.
**GUVENLIK-AV-3 DAR (SALT OLCUM) `533f935` zemininde /53 — NO-GO 3** (T1-B1 uye `request_id`
replay'i · T1-B2 adressiz siparis · T1-B4 COD parasiz "odenmis").
Olcut **(B)**: `51·AV-2` disjunktlarina **"davranis kaniti bulunan"** on sarti eklendi; T4-F1
(UNIQUE) ve T4-F2 (rowversion) **ADAY KUTUSUNDA** - migration ister, GF-6'da
kirmizi-once denenir, hit yoksa GF-7. **KOR-30 GENISLEDI, YER DEGISTIRMEDI: 19 derinlemesine ·
4 yalniz canli yetki · 5 yalniz kaynak eleme · 2 ilan edilmis kapsam disi.**

**KUYRUK (AV-3 sonrasi yeniden dizildi):**

1. **ARSIV-4** (docs, hedef `<=60 KB`) <- SIRADA. Tarif merkezden.
2. **GF-6 LAUNCH ONCESI:** **6a** uye yolu butunlugu (T1-B1 · T1-B2 · T1-B3 · T1-B4 - TEK KOK:
   misafir yolunun kazandigi kapilar uyeye tasinmamis) · **6b** durum makinesi (T4/S-1 iptal
   edilmis siparisi dirilten callback + T4-F5 iki elle kopya - TEK KOK: durum yazimi
   `IsValidTransition`'dan gecmiyor) · **6c** X-2 hub `RequireAuthorization` (tek satir + pin) ·
   **6d** T2-1 `product/import` transaction + satir siniri + tip kontrolu ·
   **6e** T4-F1/T4-F2 **kirmizi-once denemesi**.
3. **LAUNCH GO/NO-GO TURU**.
4. **GF-7 (LAUNCH SONRASI):** AV-3'un 6b/6c/6d kalani + olu/yaniltici yuzey grubu (`53`/bolum 9) ·
   SC-12 outbox payload
   sifreleme/ozetleme (SA-1 ile birlikte - `AesEncryptionProvider` bugun TEK ANAHTARLI ve
   cozemedigi degeri OLDUGU GIBI donduruyor, yani sifreleme once SA-2'yi ister) ·
   SA-1/SA-2 at-rest kurcalama + anahtar rotasyonu · SB-1 (2FA dalinda CAS geri alma) ·
   SD-1/SD-2/SD-4 anonim uc sozlesmesi · SC-3 SIEM okuyucusu.
5. Launch SONRASI digerleri: VITRIN-KALAN (10 kalem) · FIX-1B · ADMIN-FIX · IMPORT-FIX ·
   FIX-1C · LOG-FIX · FIX-2 · FIX-3/B13

Iki BILINEN kalem (`53·GUVENLIK-AV-3`):
- **REZERVASYON BIRIKMESI - DEV RIG'TE `BackgroundJobs:Enabled=false`.** 197 rezervasyonun
  **186'si suresi dolmus ama DURUYOR**; `available` KALICI dusuk ve sonraki her olcum turunun
  stok sayimi kirlenir. Bayrak BILINEN'dir, **birikmenin olcusu DEGILDIR** (sinir genislemesi).
  **PROD CHECKLIST: `BackgroundJobs:Enabled=true` -> IRL listesi.**
- **KOR EKSENLER OWASP'TA: A02 · A03 · A05 · A04.** A03 AV-2'de `BOSLUK (istemci)` diye adiyla
  kaydedilmisti; AV-3'te de kor, gerekcesi **`frontend/*` DOKUNULMAZ** - yasak yuzeyde
  birakilmis bosluk. A04 **IKINCI KEZ** hicbir goreve girmedi.

Bes BILINEN kalem (ayni-saniye jeton penceresi · miras oturumda step-up · 342 olu oturum ·
IP davranis kaniti yok · K4 gecikmeli aile iptali) TAM METINLE `docs/muhur/45-guvenlik-fix-1b.md`
icinde; kesilen satirlar bayt-aynen `docs/muhur/49-arsiv-3.md`de.

Iki BILINEN kalem (`51·GUVENLIK-AV-2`):
- **SignalR "admins" alarmi BOS GRUBA yayin yapiyor.** `SecurityEventManager.cs:39-40` ->
  `Clients.Group("admins")`; gruba katilim `NotificationHub.JoinAdminGroup()` ile olur ve
  CAGIRANI YOK (frontend'de `signalr|hubconnection|/hubs` **0** gecis, POZ kontrol backend 9).
  **Okuyucu LAUNCH SONRASI** - alarm kanalinin kendisi GF-5'te duzelmez.
- **SC-3 belge ayrismasi GF-5'te DOCS DUZELTMESIYLE kapanir.** `ops/serilog-siem.md`
  Elasticsearch/Seq + alerting anlatiyor; gercekte sink yalniz Console+File, `Siem:` anahtari
  0 gecis, belgenin sekiz olay tipinin **BESI kodda YOK**. Kod degil BELGE yanlis;
  duzeltme GF-5'in docs yarisidir.

**B-27 KAPANDI (AV-2, 4 Eylul 2026)** - `/api/payment/callback` artik `payment` kovasinda
(`PaymentController.cs:29` sinif duzeyi `[EnableRateLimiting("payment")]`); canli sinir
kosulu **10 gecer / 11. istek 429**, iki denetci AYRI AYRI olctu. **`00b:247` arsivi
DEGISMEZ (MK-11/d)** - kayit burada.

Iki BILINEN kalem (`50·GUVENLIK-FIX-4`):
- **Yerel SDK 9.0.305 / CI SDK 8.0.x, `global.json` YOK** (DUR-2'de dusuruldu). Ayrisma
  bugun gozlenmedi - `--locked-mode` CI'da (SDK 8) YESIL kosuldu - ama PINLENMEMISTIR.
- **Dependabot `docker` ekosistemi yalniz kok `Dockerfile`/`docker-compose.yml`i tarar.**
  Workflow `services.*.image` ve C# icindeki digest literallerini HICBIR ekosistem
  tazelemez; o iki deger ELLE guncellenir (bakim notu).

Dort BILINEN kalem (`lockout_end` YEREL · kismi iptal sonrasi replay 400 · logout bayat cerezle
200 · `expiration` artik `Z` bicimli) TAM METINLE `docs/muhur/47-guvenlik-fix-3.md` icinde;
kesilen satirlar bayt-aynen `docs/muhur/49-arsiv-3.md`de.

**`frame-src` SUPHELISI - GERCEK SANDBOX ODEMESI.** Kanit celiskili: vitrin meta'sinda hic
yokken 3DS uctan uca surulmus, ama `SecurityHeadersMiddleware:29` `frame-src
https://*.iyzipay.com` tasiyor.

**GOZ TURU:** 8 kalem olculdu (`48·GF-2b · GOZ TURU`, `49·ARSIV-3 · K2`); acik yalniz
`frame-src` (gercek sandbox odemesi).

Uc BILINEN kalem (Google Fonts SRI yasak · `admin.html` kendi `imgUrl()` kopyasi · panelde
`guvenliHTML`/`guvenliYaz` cagirani yok) TAM METINLE `docs/muhur/46-guvenlik-fix-2a.md`
icinde; kesilen satirlar bayt-aynen `docs/muhur/49-arsiv-3.md`de.

## Devir ID'leri

**DURUM:** DV1 KAPANDI `44·GF-1` · DV3 KAPANDI `47·GF-3` · DV2 D-YAN · DV4-6 kayit.

kaynak: 40·MANTIK-FIX-4_MUHRU · DEVIR ID'LERI (bayt-ayni KOPYA)

## DEVIR ID'LERI

```
DV1  request_id REPLAY YOLU K4 TELAFISINDEN KACIYOR [VERI-BOZAN] - GuestCheckoutManager:263
     telafi kosulu `!siparisSonuc.Success`; replay dali Success=TRUE donduruyor -> telafi
     ATESLEMIYOR. Yetim musteri+adres VE o e-postanin misafir checkout'ta KALICI 409'u.
     -> GUVENLIK-FIX'in BAS KALEMI
DV2  Yetim musteri 153 ve 155 + siparis 270-275 (bozuk adresli, R-H5 ONCE kaniti) -> D-YAN
DV3  429 UC AYRI KAYNAKTAN (cop-misafir guard'i · Redis rate-limit · yerlesik limiter -
     sonuncusunun GOVDESI BOS) + 500 yolunun RFC 7807 zarfinda `message` alani YOK
     -> GUVENLIK-AV-1 girdisi
DV4  Suzgec sayaci 9 -> 8; MANTIK-FIX-1'in "8 -> 2" kaydi BAYAT (git show 4d8d4c2 ile
     dogrulandi: o gun `old` YALNIZ old_price'tan geliyordu ve olcum O KODLA tutarliydi)
DV5  "Ayni kuralin ikinci kopyasi" ailesinin 6. vakasi (K5'in yuttugu iki esleme kopyasi)
     + merkez payi: tekil satir / bayat numara kayitlari
DV6  index.html:50 BILINCLI-'ltr' arkeolojisi - `git log -S "setAttribute('dir','rtl')"`
     HICBIR COMMIT bulmuyor; hem RTL CSS'i hem 'ltr' sabitlemesi ILK COMMIT'ten (df91863)
     yan yana duruyor. Yazar RTL destegini YAZMIS ama ACMAMIS.
```

## VITRIN-KALAN

kaynak: 40·MANTIK-FIX-4_MUHRU · VITRIN-KALAN (bayt-ayni KOPYA)

## VITRIN-KALAN (YENI KUYRUK KALEMI - TEK LISTE)

```
1. i18n TAZELEME UCLUSU - dil degisimi sekme basligini, a11y panelini ve komut paletini
   tazelemiyor (uc yuzey de "bir kez kur" kalibinda). ONCEDEN DE BOYLEYDI.
2. K6 KOZMETIK 3 - .sup-panel transform-origin · .sup-msg radius · .achip/.pwa-pill padding
3. K7 MESAJ/NotEmpty AYRISMASI - dort validator'da regex AYNI ama mesaj metni ve NotEmpty
   kullanimi FARKLI ("Gecerli bir telefon girin." vs "Gecerli telefon giriniz.")
4. BULGU-3 KALAN BES SATIR - fmtDay · couponUI · showLegal · accStatus · accOrders
5. POPULAR_L - AR'da Turkce arama etiketleri (`POPULAR_L[lang]||POPULAR_L.tr`)
6. showLegal CMS - AR kullanici sozlesme metnini Turkce goruyor; sebep SOZLUK DEGIL,
   `contents` tablosunda AR karsiliginin olmamasi (icerik isi, i18n isi degil)
7. A-1 arama collation/LOWER() — `42·GUVENLIK-AV-1 · A-1`
8. A-2 (AV-1'den) — `42·GUVENLIK-AV-1`
9. `placeholder=ceviri("...")` ON DORT yerde DIZGE ICINDE kalmis — `ceviri(` CAGRILMIYOR,
   duz metin basiliyor; 14 input'ta placeholder BOZUK. [MANTIK]/[UX], XSS DEGIL.
   Ureten ifade: `grep -c 'placeholder=ceviri(' frontend/api-bridge.js` -> 14.
   kaynak `46·GUVENLIK-FIX-2a · SUPHE-6`
10. Anonim katalog yanitinda **`Pragma: no-cache` + `Cache-Control: private, max-age=60`
    CELISKISI** suruyor. GF-3/K7 yalniz KIMLIK yarisini duzeltti (kimlikli uc artik
    `no-store` aliyor); anonim yolda iki baslik hala birbiriyle celisiyor.
    kaynak `47·GUVENLIK-FIX-3 · S3`
```

## ERTELENMIS-DEFTER (yeni sinif, ARSIV-1/S5)

Acilmaz; yalniz HAM kalem basliklari + 00a atfi. Tam metin arsivde.

- `00a:111` **YENI KALEM (Dalga 2 / B13 - kullanici karari): TERK EDILMIS PENDING SIPARISLERE TTL.**
- `00a:136` **YENI KALEM (Dalga 3 / P4 - kullanici karari): ISTEMCI TARAFI ONBELLEK.**
- `00a:140` **YENI KALEM (Dalga 3 / P2 kalani - kullanici karari): index.html'in SATIR ICI 704 KB
- `00a:145` **YENI KALEM (dalga-1-fix eki - kullanici karari): TURKCE KLAVYEDE YAZILAN E-POSTA.**
- `00a:150` **YENI KALEM (GUVENLIK-FIX / G2 eki - kullanici karari): SABIT-ZAMANLI KAYIT.**
- `00a:158` **YENI KALEM (Sprint 8 madde 8 eki - kullanici karari): RFC 2606 ust alan adlarini KAYITTA
- `00a:166` **YENI KALEM (Dalga 4 / M10-M11 eki - kullanici karari): CIKISLI KULLANICIYA DOGRUDAN
- `00a:192` **YENI KALEM (GUVENLIK DALGASI 2 / B5 eki - kullanici karari): `failed-jobs` PII RISKI
- `00a:200` **YENI KALEM (GUVENLIK DALGASI 2 yan gozlemi - DOKUNULMADI): `frontend/pwa/` DIZINI OLU.**
- `48·GUVENLIK-FIX-2b` **YENI KALEM (kullanici karari): CHECKOUT FORMU IZOLE IFRAME'E
  (`srcdoc` + kendi CSP'si) -> VITRIN `script-src` STRICT.** Bugun vitrin CSP'sinde
  `'unsafe-inline'` KABUL EDILMIS RISK olarak duruyor; tek gerekce `embedCheckoutForm`un
  saglayici satir ici script'ini calistirmasi. Odeme formu kendi CSP'li iframe'ine alinirsa
  vitrin `'unsafe-inline'`siz kalabilir. **Tasarim launch SONRASI.** (CSP FAZ B bu kalemin
  ustune kurulur; GF-2b'de YAPILMADI.)
- `50·GUVENLIK-FIX-4` **YENI KALEM (kullanici karari): TFM `net8.0` -> `net9.0`/`net10.0`.**
  GF-4'te TFM DOKUNULMAZDI (yukseltme LAUNCH SONRASI). **TETIKLEYICI: .NET 8 EOL,
  Kasim 2026.** Yukseltme `global.json` yoklugunu (yerel SDK 9 / CI SDK 8 ayrismasi) ve
  NuGet audit varsayilanini da (`all` dali TFM >= 10.0 sartina bagli) birlikte etkiler.

## AV-2 GIRDILERI (39·MF-3'ten acik kalanlar)

- **`guest_name` UZUNLUK DOGRULAMASI YOK** - misafir yolunda sinir yok, `full_name` kolonu
  150 karakter; uzun ad EF insert'te 500 uretir. **FIX adayi.**
- **`ExecuteDeleteAsync` <-> transaction ROLLBACK** OLCULMEDI (K2 onu transaction ICINDE cagiriyor).
- **Hata kodu birlestirme** - TR serbest metin capalarinin kirilganligi (K3 + K3b ayni capa).
- **Ortak RuleBuilder** - K7 mesaj/NotEmpty ayrismasi (dort validator, regex ayni, metin farkli).
- **K4 TELAFISININ ATOMIKLESTIRILMESI** - GuestCheckoutManager telafisi IKI AYRI
  DeleteWhereAsync (adres, sonra musteri, :503-504) ve TRANSACTION YOK; ilki gecip
  ikincisi duserse KISMI DURUM olusur. Uretim kodu bu kalemi ADIYLA deftere havale
  ediyor (GuestCheckoutManager.cs:313). BILINCLI SINIRLAR 2-3 (istisna yolunda telafi
  kosmaz · outbox satiri silinmez) kod :313 yorumunda, defterde ilk kez burada.
```
---

## 3. KARAR ENVANTERI — ONCE / SONRA (KAYIP 0)

**Ureten ifade (POZ/NEG sinanmis):**
```
{ grep -oE '00[ab]:[0-9]+' CLAUDE.md ;   grep -oE '[0-9]{2}·[A-Za-z0-9-]+(·[A-Za-z0-9-]+)?' CLAUDE.md ; } | sort -u
POZ girdi ("00a:87 ve 44·GF-1·K1 ile 00b:197") -> 3 capa
NEG girdi (capa tasimayan duz metin)           -> 0 capa
```

```
ONCE  (zemin 1d67cf6) : 89 capa
SONRA (bu commit)     : 91 capa
comm -23 (KAYBOLAN)   : 0   <- BOS, sart saglandi
comm -13 (EKLENEN)    : 50·GF-4 54·ARSIV-4 
```

**EKLENEN IKI CAPANIN GEREKCESI** (kayip DEGIL, YENI):
- `50·GF-4` — dort alt karar (`50·GF-4·K1/K4/K5/K7`) TEK SATIRDA toplandi; alt capalarin
  DORDU DE satirda ADIYLA duruyor, birlesik satir kendi capasini da kazandi.
- `54·ARSIV-4` — bu dosyanin kendisi.

**BOYUT:** CLAUDE.md **78.925 B -> 59.990 B** (kesim **18.935 B**, %23). Hedef `<=60.000`
SAGLANDI; butce **81.920 DEGISMEDI** (kalan 21.930).
**B0 / B1-B3 / B2-SUREC / B4 DOKUNULMADI:** HEAD'in ilk 557 satiri ile `cmp` **0 fark**
(32.207 B = 32.207 B). B8'in `00a` blogu da `cmp` ile BIREBIR (14 satir / 1.148 B);
EK-1 (36-39) ve `00b:` satirlari BIREBIR.
