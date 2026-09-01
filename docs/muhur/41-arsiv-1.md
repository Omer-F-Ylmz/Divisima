# ARSIV-1 MUHRU - CLAUDE.md ACILIS YUKU ~187k -> ~19,7k (1 Eylul 2026)

**KOD SHA: `7f8efa7`** (zemin `d8f12dd`; DORT commit TEK push). MK-11 (c) usulunun ILK
uygulamasi: bu muhur docs/muhur/ altina YENI dosya olarak yazildi; CLAUDE.md'ye yalnizca
operatif delta girdi.

```
ARSIV-1 KODU (d8f12dd..7f8efa7, DORT commit tek push)
  CI - Build & Test  run 33518742063  event=push  head_sha=7f8efa7  SUCCESS
  Security CI        run 33518742070  event=push  head_sha=7f8efa7  SUCCESS
ALTI JOB · 71 ADIM -> 69 SUCCESS + 2 skipped (TESHIS) · failure seviyeli adim 0
GITLEAKS ADIM SONUCU (ham ad): "Gitleaks (secret taraması)" -> success · job secret-scan -> success
ANNOTATION 39 (failure 0 · warning 39) - TABAN ile BIREBIR
  yol dagilimi IEntityRepository.cs 24 · .github 9 · EfEntityRepositoryBase.cs 6
```

## COMMIT ZINCIRI

```
949c3e2  ARSIV-1/C1 saf tasima (zemin d8f12dd)
2474135  ARSIV-1/C2 operatif duzen + INDEX + MK-11
e464fac  ARSIV-1/C3 denetci duzeltmeleri (M1 x5, M2-EK)
7f8efa7  ARSIV-1/C4 denetci kozmetik kalintilari (2)
```

**SAPMA:** tarif IKI commit ongoruyordu (C1·C2). Denetci UYUMSUZ (DAR) verince C3 ve C4
eklendi. MK-4 amend yasagi geregi hicbiri amend EDILMEDI.

## OLCUM

```
CLAUDE.md   747.240 B / 12.434 satir / ~186.810 est.token
         -> 78.773 B /  1.403 satir /  ~19.693 est.token      (%89,5 azalma)
butce ≤81.920 B (80 KB) TUTUYOR, pay 3.147 B · CR 0 · BOM 0
BOLUM  B0+B1+B2+B3 45.803 (bayt-ayni cekirdek) · B4 9.971 · B5 3.589 · B6 7.788
       B7 2.664 · B8 4.110 · B9 4.934
ARSIV  42 dosya · 702.847 B · docs/muhur/INDEX.md 42 satirlik esleme tablosu
BAYT DENKLEMI  747.240 = 44.393 (C1 sonrasi CLAUDE.md) + 702.847  -> fark 0
CMP  arsiv 42/42 "0 fark" (POZ/NEG sinamali) · B1/B2/B3 dort turda da 0 fark
```

## MERKEZ KARARLARI (FAZ B ONAY, S1-S6 + EK-1/2/3 + C3)

S1 Muhur 21/22/23 KENDI NN dosyalarina; 00 blogu IKI ardisik dosya (00a 6426-6753 ·
   00b 7062-7483), her biri tek ardisik aralik.
S2 SUREC 7511-7625 -> B3 = 7484-7625 TEK aralik bayt-ayni.
S3 Butce ≤75 KB; C3 turunda ≤80 KB'a genisletildi (asagida).
S4 SIRA 6426-6509 arsive (00a icinde, bayt-ayni); hicbir sey dusmez.
S5 KARARLAR: 11 BAGLAYICI -> B8; 9 tereddutlu kalem YENI SINIF **ERTELENMIS-DEFTER** -> B9.
   SUPHELI: #16 BAGLAYICI, #14/#20 ACIK -> B8.
S6 MF-4 arsive; KUYRUK · DV1-6 · VITRIN-KALAN · kurgu MAX/Pending/MK-3 uclusu B7/B9'a
   bayt-ayni KOPYA (arsivde de kalir; muhur kesilmez).
EK-1 B8'e muhurlerden 7 baglayici karar (409 semantigi · InvoiceManager brut bagi · KDV
   uretim kaynagi · sunucu sozlesmesi kilidi · migration merkez onayi · 64 fatura satiri ·
   InvoiceManager dokunulmazligi).
EK-2 B5'e S5 (CR dedektoru) S1-S4 kalibiyla, POZ/NEG kayitli.
EK-3 kod atif tablosu (asagida).
C3  Butce ≤75 KB -> **≤80 KB** (MK-11 (a) metninde ve KABUL'de). SDP kalir, B6-B8
   daraltilmaz. Gerekce: denetci duzeltmeleri C3 adayini 78.864 B'ye cikardi; kendi kirpma
   YAPILMADI, DUR verildi ve karar merkezden alindi.

## FAZ A OLCUMU (ozet; tam tablo asagidaki INDEX.md'de)

40 muhur + 4 operatif blok olculdu. Kalan uc aralik (orijinal SIRAYLA): 1-229 calisma
kurallari · 7484-7625 dalga ici denetim kurali + SUREC · 8567-8903 SDP v1.2.
6426-7483 blogunun ICINDE UC MUHUR bulundu (21 MINI DALGA · 22 MINI DALGA 2 · 23 KALITE
SUPURMESI DALGA 1) - tarifin "tek dosya" varsayimi olcumle uyusmadi, SORU-1 olarak soruldu.

**SUPHELI ayristirmasi (00b, 22 kalem):** ACIK 2 (#14 X-Api-Version blanket 400 · #20
varsayilan-kapali kural minimal-API) · BAGLAYICI 1 (#16 Webhook:AllowedIps bilincli bos) ·
KAPANMIS 19.
**KARARLAR ayristirmasi (00a, 20 kalem basligi):** BAGLAYICI 11 · ERTELENMIS-DEFTER 9.

## DENETIM (MK-4b, tek kapsama denetcisi, AYRI worktree)

**1. tur (2474135):** M3 B1/B2/B3 cmp · M4 arsiv butunlugu 42/42 + bayt denklemi ·
M5 kapsam sizintisi · M6 butce/CR/BOM -> DORDU DE TEMIZ.
M2 bayt-eslesme orneklem 28 -> 21 tam + 3 composite + 1 beyanli + 3 yeni;
**"Kural METNI uyduran satir YOK."** Denetci kendi suzgec kusurunu yakaladi (yapisal sinir
hatasi 22 yalanci FARK uretiyordu) ve duzeltip yeniden kostu.
**M1 BES BULGU** -> C3'te kapatildi:
```
B2 [EKSIK-KAPSAMA] admin.html/api-client tuzagi YOK, etiket yanlis muhre (37) atifli
                   -> 34·MFIX-B:113-121 HAM blok + etiket duzeltmesi
B3 [EKSIK-KAPSAMA] "KALICI KURAL: KAPSAM ELESTIRMENI ROLU..." tasinmamis
                   -> 34·MFIX-B:126-130 HAM blok
B4 [EKSIK-KAPSAMA] D-YAN 11 muhurde, yalniz 39'unki tasinmis -> kumulatif kaynak listesi
B5 [EKSIK-KAPSAMA] GUVENLIK-AV-1 girdileri 5 kalem eksik -> 39·MANTIK-FIX-3:499-510
M2-EK              bes alinti satir ORTASINDAN basliyor, karar ozunu tasimiyor
                   -> 409 semantigi 37:409-413 TAM blok · 64 fatura 36:255-256 tam cumle
                      · 338/339 37:423-428 tam acilis
```
**2. tur (e464fac):** bes bulgunun ve M2-EK ucununun ICERIGI KAPALI, hepsi kaynagiyla
cmp ESIT · kozmetik uclu TEMIZ · M6/B1/B2/B3 TEMIZ · A4 TEMIZ (42 eklenen satirin 4'u
merkez onayli istisna) · **IKI KOZMETIK KALINTI** -> C4'te kapatildi (1090 etiketi
admin.html'i hala 37'ye atfediyordu; D-YAN listesi 26'yi atliyordu).

## P0 - PUSH ONCESI OLCUMLER

**(a) GITLEAKS FINGERPRINT IZLEME** (deger BASILMADI; CLAUDE.md bolum 1 kirpma kurali)

```
fingerprint            icerik turu       HEAD konumu
19d101f:CLAUDE.md:1137 TAM GUID          CLAUDE.md + 01-oturum-devri.md  (ankraj iyziReferenceCode)
19d101f:CLAUDE.md:1277 TAM GUID          01-oturum-devri.md + 21-mini-dalga.md (ankraj iyziPaymentId)
e6e9b71:CLAUDE.md:2731 jeton-desensiz    CLAUDE.md + 00a + 01 (ankraj localhost)
e6e9b71:CLAUDE.md:2736 jeton-desensiz    CLAUDE.md + 00a + 01 (ankraj localhost)
```
Ankrajlar HEAD'de bulunuyor **ama jetonlar KIRPILMIS**: ankraj satirlarinda tam-GUID sayisi
CLAUDE.md 0 · arsiv 0. Kirpilmis-bicim denklemi: zemin 3 = HEAD 1 + arsiv 2.
d8f12dd..HEAD degisen TUM dosyalarda tam-GUID **0** (POZ kontrol: 19d101f blobunda 2 -
tarayici calisiyor; NEG `ZZZANKRAJ` 0). `.gitleaksignore` fingerprintleri `<SHA>:<dosya>`
bicimli, eski commit'lere bagli -> arsivlemeden ETKILENMEZ.
Yerelde gitleaks YOK; **CI adimi belirleyici oldu ve SUCCESS verdi.**

**(b) C4'TE DOKUNULAN IKI SATIR** - ikisi de YAPISAL (kaynak etiketi ve kumulatif kaynak
listesi; merkez onayli A4 istisnasi). Komsulukdaki HAM bloklar bayt-ayni dogrulandi:
`37:429-435 ORTAM DERSLERI` cmp 0 fark · `39:528-535 D-YAN` cmp 0 fark.
POZ/NEG: bozulmus dilim FARKLI · NEG capa `MK-77` 0 gecis · POZ capa `MK-10` 4 gecis.

## EK-3 KOD ATIF TABLOSU (A6: kod DEGISTIRILMEDI, yalniz kayit)

```
atif turu                   sayi  hedef                              durum
"CLAUDE.md bolum 1"           11  B1 (## 1. Kanit standardi)         KALDI
"CLAUDE.md bolum 6c"          11  B1 (## 6c. KIMLIK vs GORUNTU)      KALDI
"CLAUDE.md bolum 5"            6  B1 (## 5. Bilinen tuzaklar)        KALDI
"CLAUDE.md tuzagi"             3  B1 (## 5)                          KALDI
"CLAUDE.md SUREC"              1  B3 (## SUREC (degismez))           KALDI
"CLAUDE.md'de / 'ye"           4  genel atif, bolum belirtmiyor      -
.gitleaksignore fingerprint    4  <SHA>:CLAUDE.md:<kural>:<satir>    tarihsel, ETKILENMEZ
```
Kaynak dosyalar: `.gitleaksignore` · `database/README.md` · AccountManager · DashboardManager ·
OrderManager · ProductManager(2) · ShipmentManager · NetgsmSmsService · DenetimGizlilik ·
SifrePolitikasi · KanitMaskesi(2) · PostaKutusu · DivisimaDbContext · EfCouponDal · iki
migration · IntegrationTests.
**ARSIVE DUSEN KOD ATFI YOK:** atiflarin hepsi B1 ya da B3'e isaret ediyor, ikisi de
CLAUDE.md'de bayt-ayni kaldi. Satir numarasi kullanan kod atfi da YOK.

## CC HATALARI (6)

```
1 CR dedektoru BOZUKTU (`grep -c $'\r'` bu ortamda NEG girdide de esliyor) - Faz A'da
  "CR 12434" yanlis okundu ve "kabul kriteriyle CELISIYOR" sanildi. `tr -cd '\r' | wc -c`
  ile duzeltildi; S5 olarak suzgec kutuphanesine girdi.
2 Bolum-boyut awk'i B0'i B1+B2+B3 ile toplayip 45.803 gosterdi (yapisal baslik farki).
3 D-YAN listesini duzeltirken satir uzadi ve "ile" kelimesi dustu; ayni commit icinde
  `grep 'ile$'` ile yakalanip duzeltildi.
4 Basliksiz-etiket suzgeci ilk kosumda ayrac sayip "34" dedi; awk ile iki-ust-satir
  kontrolune cevrilip POZ (C2'de 4) / NEG (0) ile sinandi.
5 Fingerprint sinif atayicisi SIRASIZ yazildi: TAM GUID tasiyan satiri "KIRPILMIS" diye
  etiketledi (iki desen de esliyordu, son atama kaziniyordu). Sirali if/elif'e cevrildi.
6 P0-b diff'ini `cut -c1-96` ile kirptim ve "ile" kelimesi kesildi; bir an commit'in bozuk
  oldugunu sandim. Kirpilmamis diff ve HEAD blobu ile dogrulandi: kelime YERINDE.
```

## MK-11 USULUNUN ILK UYGULAMASI (bu muhur)

Bu dosya `docs/muhur/41-arsiv-1.md` olarak YAZILDI; CLAUDE.md'ye yalnizca UC delta girdi:
B9 kuyruguna "ARSIV-1 KAPANDI" satiri · B6'ya basliksiz-etiket suzgeci dersi · INDEX.md'ye
41 satiri. **Dosya-sonu guvenli-ekleme capa deseni KULLANILMADI** (MK-11/c geregi EMEKLI);
ekleme B-basligi capasiyla yapildi.

## KUYRUK

```
1. GUVENLIK-AV-1 (ultracode pilotu)                            <- SIRADA
2. GUVENLIK-FIX (DV1 bas kalem)
3. VITRIN-KALAN
4. FIX-1B
5. ADMIN-FIX
6. IMPORT-FIX
7. FIX-1C
8. LOG-FIX
9. FIX-2
10. FIX-3 / B13
```
