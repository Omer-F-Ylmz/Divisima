# 47 · GUVENLIK-FIX-3 (GF-3) MUHRU — SIZINTI / YAPILANDIRMA / LIMIT / KALINTI (3 Eylul 2026)

**AD AYRIMI (B0 haritasina da islendi):** bu paket **GF-3 (Eylul 2026)**; zemindeki
`GuvenlikFix3SozlesmeTests.cs` ise **GUVENLIK-FIX-3 (Agustos 2026)** dalgasinin DAGITIM
YUZEYI pinleridir (nginx/CSP/clickjacking, alti pin). Iki dalga ayni kisaltmayi tasiyor;
bu dalganin pin dosyasi konuya gore ayrildi: `GuvenlikFix3SizintiSozlesmeTests.cs`.

## KAPI

```
skill sdp   .claude/skills/sdp/SKILL.md    19.928 B
skill surec .claude/skills/surec/SKILL.md   9.044 B
zemin cea48d6 · dal main · agac 0 satir
DORT YESIL AGDAN YENIDEN OLCULDU (merkez kapi notu):
  1dd985b -> 33695617783 · 33695617786   completed/success
  cea48d6 -> 33696665728 · 33696665751   completed/success
  zemin adim sonuclari: 33696665728 = 4 job / 42 success / 0 failure / 1 skipped
                        33696665751 = 2 job / 27 success / 0 failure / 1 skipped
  sekiz job kimliginde failure-annotation 0/0 · NEG kontrol "zzzyok" 0
```

## ON OLCUM (alti ajan, salt okuma, ortak kural metni SDP 1.8)

```
A-log 30.969 · B-yapilandirma 41.255 · C-limit 30.919 · D-kalinti 47.839 ·
E-dogrulama 34.437 · X-kapsam 45.324  = 230.743 B
```

Altisi da `[PLAN]` blogu + POZ/NEG suzgec sinamasi + KOR NOKTA basligi + kendi `wc -c`
olcumunu tasidi (MK-5). Kapsam elestirmeni ZORUNLU uye olarak kosuldu ve uc DUR adayini
tek basina cikardi (K11 `lockout_end` · K8 · K12 kupon kiyasi).

## DUR-1..DUR-8 ve MERKEZ KARARLARI

```
DUR-1 K8/F-2 ZATEN DUZELTILMIS -> K8 DUSTU. Program.cs:479-495 kendi yorumunda kosulsuz
      Clear()'in eski hata oldugunu ve duzeltildigini yaziyor; trustedProxies bosken ASP.NET
      varsayilani (KnownProxies=127.0.0.1 + KnownNetworks=127.0.0.0/8) duruyor -> spoofing
      BUGUN ENGELLI. R-3.7 dustu.
DUR-2 K11 KAPSAMI [VERI-BOZAN][OTURUM] -> lockout_end KAPSAM DISI + BILINEN. Uc okuyucu
      (AuthManager:281 · AccountManager:150 · SellerAuthManager:72 - ucuncusu DOKUNULMAZ);
      kismi gecisin IKI YONU DE hasar verir (kilit ANINDA gecersiz ya da 3sa15dk).
      K11 YALNIZ expires_at / created_at / exp yollari; AuthManager:590 audit DOKUNULMAZ.
DUR-3 K3/B-09 HEDEFSIZ -> K3 DUSTU; B-09 kaydi "CURUDU" olarak duzeltildi. Yedi Hangfire
      kaydi PARAMETRESIZ (RecurringJob 8 / Enqueue 0) -> is argumaninda PII OLAMAZ.
DUR-4 K2 <-> DOKUNULMAZ celiskisi -> ops/serilog-siem.md TEK DOSYA ISTISNASI (docs).
DUR-5 K12 semantigi -> kiyas: e-posta + sepet kalemleri (iptal kalemleri DISLANIR) + kupon
      KANONIK deger, NULL == "" normalizasyonu. 400 sizintisiz. 400 dongusu (api-bridge:2352
      rid yalniz basarida yenilenir) -> GF-2b kalemi, BILINEN.
DUR-6 K11 YANIT GOVDESI -> JwtHelper DAHIL, BILINCLI KABUL: login yaniti `expiration` UTC (Z)
      olur; frontend TUKETMIYOR (0 gecis), hicbir pin OLCMUYORDU (0). SellerLoginResponseDto
      DOLAYLI etkilenir - Seller KODUNA dokunulmadi, DEGERIN BICIMI degisti (Seller 0 satir).
DUR-7 K14 PREMISI BOS -> K14 DUSTU, R-3.12 dustu. Uc kanal: paket XML "default is false" ·
      7855 istek satiri / `?` tasiyan 0 (POZ kontrol verify-email 86 · unsubscribe 2 ·
      Search 73) · `token=` 0 gecis. **HATA MERKEZIN** (tek kanalli bulgu tarife kalem oldu).
DUR-8 TIMELINE NOTU -> F1'de KAPATILDI (asagida).
```

## D1-D8 SECILEN YOL

```
D1 KanitMaskesi IKINCI DAL: '@' jeton karakter kumesine alindi. Onceden ayracti, bu yuzden
   "omer@example.com" iki KISA parcaya bolunup 16 esiginin ALTINDA kaliyor ve e-posta HIC
   maskelenmiyordu. Yeni dal: ilk 2 + "***@" + alan. Alan adi TESHIS icin GORUNUR kalir.
   ETIKET AYRIMI (pin yakaladi): '=' jeton karakteri oldugu icin "to=omer@..." TEK parcadir;
   son '=' isaretine kadar olan kisim ONEK sayilir -> "to=om***@example.com".
   ASIMETRI BILINCLI: jeton dalina etiket ayrimi UYGULANMAZ - orada '=' base64 DOLGUSUDUR ve
   son '='den bolmek "abcd==" gibi bir jetonu TUMDEN sizdirirdi.
D2 Yer-tutucu taramasi TEK dongude YEDI hassas anahtara; jwtKey'e OZEL kontrol KALDIRILDI
   (ikinci kopya acilmadi). + BILINEN-PUBLIC deger deny-list'i (IKI SHA-256 ozeti; degerler
   kaynaga GIRMEZ). Kapi Production'a KOSULLU - kosulsuz olsaydi CI'in iki job'i acilista
   kirilirdi. Yedinci giris `Encryption:Key` CHANGE_ME DEGIL BOS DIZEDIR (ust-kume).
D3 HSTS tek kaynak nginx: app.UseHsts() KALDIRILDI.
D4 "hassas" kovasi 20/dk IP basina; YERLESIK (AddPolicy) ve DAGITIK (KovaSec) taraf BIRLIKTE.
D5 Rotasyon TEK transaction (CAS + denetim + INSERT); logout ayni CAS yardimcisinda.
D6 DAR UTC: expires_at · created_at · JWT exp/nbf. Yazan-okuyan CIFTLER BIRLIKTE tasindi.
D7 Replay olcutu = e-posta + sepet coklu kumesi + KANONIK kupon; migration GEREKMEDI.
D8 ProductUpdateRequestValidator (Add ile BIREBIR regex) + CSV satir dogrulamasi.
```

## K1-K13 ONCE / SONRA

```
K1  Iyzico odeme jetonu + reset jetonu log'a HAM      -> maskeden gecer
    (MailLinkBuilder'in IKI metodu da; tarif birini anmisti, otekisi ayni desende ve
     "yolVeSorgu" adiyla SORGU DIZESI aliyor)
K2  musteri e-postasi DUZ; istisna NESNESI ham        -> om***@alan; istisna METNI maskeli
K3  DUSTU (premis bos)
K4  Subject'te CRLF -> sahte log satiri               -> SatirGuvenli; baslik ve log TEK yerden
K5  alti CHANGE_ME degerinden BESI kapiyi geciyordu   -> yedi anahtar + iki public ozet
K6  HSTS UC kaynaktan; api'de IKI STS basligi         -> tek kaynak nginx
K7  admin urun listesi "private, max-age=60" aliyordu -> ETag YOK, no-store KALIR
K8  DUSTU (zaten duzeltilmis)
K9  global 100/dk disinda sinir YOK                   -> 20/dk; 21. istek 429 (CANLI OLCUM)
K10 uc ayri commit noktasi; aile iptali BEST-EFFORT   -> tek transaction; DETERMINISTIK
K11 oturum/jeton YEREL eksende                        -> UTC; expiration Kind=Utc
K12 replay'de YALNIZ e-posta karsilastiriliyordu      -> + sepet + kanonik kupon; 400 sizintisiz
K13 Update validator YOK; CSV dogrulamiyor            -> ikisi de Add ile BIREBIR
K14 DUSTU (premis bos - MERKEZ HATASI)
```

## F1 / F2 (KAPANIS)

```
F1 DUR-8: order_status_history.note artik SABIT METIN. KIRMIZI-ONCE OLCULDU - sentetik SMTP
   hatasiyla timeline yanitinda alici adresi BIREBIR gorundu:
     "... Son hata: SMTP 550 alici reddedildi: gf3f1.kurban@example.com"
   Not SILINMEDI, METNI sabitlendi (gorunurluk korunur, teknik ayrinti YALNIZ maskeli log'da).
   "admin bildirimi" ibaresi ve deneme sayisi kaldirildi. "KRITIK:" oneki BILINCLI korundu.
   S1 OutboxProcessor not-yazma catch'i K2 kalibina alindi (istisna NESNESI gecilmiyor).
   S2 IyzicoClient CF-init ve refund LogWarning'lerinde result.ErrorMessage maskeden gecer
      (KAYNAK-SOZLESME pini - L3 mock modda kostugu icin OLCEMEDI, DURUST BEYAN).
   S4 color_hex regex ankraji '$' -> '\z' UC YERDE BIRDEN (Add + Update + CSV). '$' .NET'te
      sondaki TEK '\n'i KABUL EDER; "#112233\n" gecerli sayiliyordu. Davranis pini iki
      validator'i da kosturur.
   BILINCLI KIRILAN PIN: SMTP_KALICI_PATLARSA_..._KRITIK_Notu_Duser asserti
      Contains("onay e-postasi") ariyordu. YERINE KONAN DAHA GUCLU: sabit metnin kendisi +
      "Son hata:" YOK + "@" YOK -> hem eski degeri (gorunurluk) hem F1'in yeni degerini korur.
F2 MK-4b RIG BULGUSU (iki denetci BAGIMSIZ buldu): DIVISIMA_TEST_SQL'in Database= parcasi
   denetciyi IZOLE ETMIYORDU - 47 uretim noktasi InitialCatalog'u SABIT DbName ile eziyordu.
   Iki kosucu ayni anda calisinca "Database 'X' already exists" ile 157-335 SAHTE kirmizi.
   TEK VERITABANINA GECILMEDI (CLAUDE.md bolum 4: sinif basina ayri DB - xUnit paralel kosar);
   sinif basina ayrilik KORUNDU, uzerine KOSUCU AD ALANI eklendi: DIVISIMA_TEST_DB varsa
   sinif adina son ek olur. Degisken YOKSA ad AYNEN kalir - CI ve yerel akis ETKILENMEZ.
   47 nokta TestDbAdi.Cozumle'ye baglandi; "master" baglantilari BILINCLI disarida.
```

## PIN / MUT TABLOSU

```
+59 pin (taban 654 -> 713). Yeni pin dosyasi GuvenlikFix3SizintiSozlesmeTests.cs.
MUT-1  IyzicoClient  Maskele(token) -> token            -> TAM 1  K1_IYZICO...
MUT-2  MailLinkBuilder Maskele(hashYolu) -> hashYolu     -> TAM 1  K1_MAIL...
MUT-3  SmtpMail     Maskele(message.To) -> message.To    -> TAM 1  K2_MUSTERI...
MUT-4  ExceptionMw  LogError(ex, ... geri kondu          -> TAM 1  K2_ISTISNA...
MUT-5  AdminSeeder  Maskele(email) -> email              -> TAM 1  K2_ADMIN...
MUT-6  KanitMaskesi '@' cikarildi                        -> 7 kirmizi, HEPSI e-posta pini
                                                            (jeton pinleri + bes kaynak pini YESIL)
MUT-7  KanitMaskesi SatirGuvenli etkisizlestirildi       -> 3 kirmizi, yalniz CRLF/TAB
MUT-8  K5 listeden Captcha cikarildi                     -> 2 kirmizi (kaynak + TAM O anahtar)
MUT-9  K5 deny-list BIRINCI ozeti bozuldu                -> TAM 1
MUT-9b K5 deny-list IKINCI ozeti bozuldu                 -> ILK HALDE 0 KIRMIZI (MK-6 BOSLUGU,
                                                            rapor denetcisi buldu); pin Theory'ye
                                                            cevrildi -> 2 kirmizi
MUT-10 K6 UseHsts geri kondu                             -> TAM 1
MUT-11 K7 kimlik ayrimi kaldirildi                       -> 2 kirmizi (kaynak + DAVRANIS)
MUT-12 K9 KovaSec'ten hassas dali kaldirildi             -> TAM 1
MUT-13 K9 Coupon ucundan oznitelik kaldirildi            -> TAM 1
MUT-14 K10 logout filtreli okumaya donduruldu            -> TAM 1
MUT-15 K10 logout CAS cagrisi kaldirildi                 -> TAM 1
MUT-16 K12 sepet karsilastirmasi devre disi              -> TAM 1
MUT-17 K12 iptal kalemleri DAHIL edildi                  -> ILK TURDA 0 KIRMIZI (MK-6 BOSLUGU);
                                                            kaynak asserti eklendi -> TAM 1
MUT-18 K13 CSV renk dogrulamasi kaldirildi               -> TAM 1
MUT-19 F2 tek dosyada sarmalayici kaldirildi             -> TAM 1
TOPLAM 20 mutasyon; IKISI ilk turda BOSLUK gosterdi, IKISI DE KAPATILDI.
Her mutasyonda: md5 degisimi + build hata sayisi + geri alma md5 dogrulandi (SUREC 5. kontrol).
```

## IKI SUREC SUTUNU (R-3.6 — CANLI KESTREL)

```
Duzenek: Divisima.API.exe (Release), port 5199, ayni dev DB, Development ortami,
Start-Process ile AYRIK. BES ARGUMAN - RateLimit override'i BILINCLI YOK
(goz1'in besinci argumani --RateLimit:AuthPermitLimit=100 KULLANILMADI ki gercek limit kossun;
 goz1'in kendi bes-arguman sozlesmesine DOKUNULMADI, ALTINCI arguman EKLENMEDI).

POST /api/Coupon/validate x24 ayni IP:
  400 x20, sonra 429 x4  -> ILK 429 = 21. ISTEK. "hassas" = 20/dk DOGRULANDI.
429 govdesi iki FARKLI kupon koduyla BIREBIR AYNI ->
  {"success":false,"message":"Cok fazla istek. Lutfen biraz sonra tekrar deneyin."}
  Kod gecerliligi 429 uzerinden AYIRT EDILEMEZ (F-1'in enumerasyon kanali kapandi).
C-2 KAPISI AYIRT EDILDI: govde DOLU -> 429'u DAGITIK middleware uretti, yani KovaSec
  "hassas"i TANIDI. Yalniz yerlesik taraf acilsaydi etkin limit global 100/dk olur ve govde
  BOS duserdi. TEK OLCUM IKI UYGULAMAYI AYIRT EDIYOR.
CIFT-ANLAM KIRICI: kapsam disi uc GET /api/product/get/1 x24 -> 429 sayisi 0, tek kod 200.
KOVA PAYLASIMI: kupon kovasi doluyken GET /api/Search/products ILK istekte 429.
Surec durduruldu; kurgu kaydi URETILMEDI.
```

## MK-4b DENETIM — UC DENETCI, UC AYRI WORKTREE, UC AYRI TEST DB

Ucu de `pwd` + `git rev-parse HEAD` beyanini BEKLENENLE BIREBIR verdi (MK-4a). Raporlar
oturum scratchpad'inde (81.180 B), **worktree ICINDE DEGIL** (MK-5 eki). Worktree sokumu
ucu de TAMAMLANMA sinyali verdikten SONRA yapildi (MK-4b eki).

```
KURAL-UYUM (27.413 B) : 1 IHLAL + 1 OLCEMEDIM, IKISI DE DUZELTILDI.
  IHLAL 4.1: pin yorumundaki "ALTI CHANGE_ME + jwtKey = 7" ARITMETIGI YANLIS - jwtKey zaten
    TokenOptions:SecurityKey ve o altinin ICINDE; yedinci giris Encryption:Key ve o CHANGE_ME
    DEGIL BOS DIZE (appsettings.json:39). Guvenlik etkisi YOK, GEREKCE kusuru.
  OLCEMEDIM 4.9: "113 yigin satirinda 5" ureten ifadesi yoktu -> koda yazildi.
  ONAY: kapsam (26 dosya; seller 0 · frontend 0 · csproj 0 · migration 0 · ops/CI 0 -
    POZ kontrol: ayni desenler depoda 19/20/6/31 dosya esliyor) · migration yok ·
    sir hijyeni (DENETCI UC compose/workflow degerini KENDI hashleyip iki ozetle BIREBIR
    eslestirdi - ozetlerin dogrulugu BAGIMSIZ kanitlandi) · capa kirlenmesi (siyirici 11
    vakayla sinandi) · vakum (27 metotta pozitif kosul) · kultur · MK-9 · CR/BOM (26/26).
RAPOR (27.652 B)      : UYDURMA YOK. Bes kusur; ilk dordu duzeltildi.
  1 CURUYEN IDDIA: "maskelenen besi de derleyici uretimi ad" - yeniden uretildi, 5'in 1'i
    derleyici uretimi, DORDU gercek metot adi; yorumun KENDI ornegi iddiayi curutuyordu.
    OLCULEN ZARAR: GetListNoTrackingAsync ve GetListIgnoringFiltersAsync AYNI "2.GetLis..."
    dizesine iniyor - CLAUDE.md bolum 5 tuzaginin iki cercevesi log'da AYIRT EDILEMEZ.
  2 CELISKI: iptal-dislamasinin "mesru replay'i kurtardigi" yaziliydi; dogrusu TERSI.
  3 BAYAT SAYI: "9 yer / 7 dosya" -> K9 sonrasi 14 yer / 11 dosya (yorumun KENDI ifadesiyle).
  4 MK-6 BOSLUGU: deny-list'in IKINCI ozeti PINSIZ -> kapatildi.
  5 :265 cift sayim -> 1d1759d'te ZATEN duzeltilmisti (denetci ca0c683'e bakiyordu).
  KANIT GUCU: K6 ve K13 TEK KANALLI (ikisi de durustce beyan edildi) - cok kanalli
    K5/K7/K9/K11/K12 ile AYNI GUVEN SIRASINA KONMAZ. K9 turun EN GUCLUSU (canli Kestrel).
L3 (26.115 B)         : 8/9 ONAY (R-3.8 BES turun BESI). 1 ITIRAZ -> DUR-8, F1'de kapatildi.
  Suit: Sql 381/381 · tam 692/695 - ana akisla BIREBIR. Docker uclusunun ham sebebi
  DockerEndpointAuthConfig (bagimsiz dogrulandi).
```

## SUIT UCLUSU (F1 + F2 SONRASI, TEMIZ PENCERE)

```
TUR 1/2/3  Category=Sql 0/382/0/382  ·  tam suit 3/710/0/713   UCU DE BIREBIR
kirilanlar ucunde de ayni: OrderEndpointTests uclusu = BILINEN Docker uclusu
Release build 0 hata · whitespace 0 · style 0 · agac 0 satir · dal main
Taban: Sql 378 -> 382 (+4) · tam 654 -> 713 (+59)
```

## KOD DIFF ve KURGU

```
Onikili commit zinciri: cea48d6..bde07bf
  db4c9df K1+K2+K4 · 12601b1 K5+K6+K7 · dc6885c K9 · ba4bca8 K10 · 7fa186b K11 ·
  ba64d1e K12+K13 · ca0c683 K12 pin · 1d1759d kural-uyum duzeltmeleri ·
  3ddbeb4 L3+rapor duzeltmeleri · e811cf1 F1 · ef91453 F2 · bde07bf F1 eki
Entity/Entities · DivisimaDbContext · Dal/Migrations -> DEGISEN DOSYA 0 -> migration YOK.
DOKUNULMAZ ihlali YOK: Seller* 0 · frontend/ 0 · csproj 0 · migration 0 · CSP/nginx disi ops 0.
KURGU: HICBIR KAYIT URETILMEDI. musteri 169 · urun 955 · siparis 286 · adres 119 ·
fatura 119 · user_sessions 342 · Pending(status=0,id<=210) 35/3837 ·
`email LIKE 'gf3%'` -> 0. Acilistaki degerlerle BIREBIR.
```

## SUPHELI S1-S7 DURUMLARI

```
S1 OutboxProcessor istisna NESNESI           -> F1'de KAPATILDI
S2 IyzicoClient result.ErrorMessage maskesiz -> F1'de KAPATILDI (kaynak pini; L3 olcemedi)
S3 Pragma:no-cache + max-age=60 celiskisi    -> SUPHELI KALIR -> VITRIN-KALAN 10
S4 color_hex regex '$' ankraji               -> F1'de KAPATILDI (uc yerde birden)
S5 Maske iki EF cercevesini ayni dizeye indiriyor -> KAYIT (yeni sezgisel ACILMADI)
S6 K12 iptal-dislamasi olcutu SIKILASTIRIR   -> KABUL (merkez olcutu); kismi iptal sonrasi
   replay 400 = BILINEN
S7 Logout bayat cerezle 200 doner, oturum kapatmaz -> BILINEN (GF-1b D-1 semantigi)
```

## CC HATALARI — DORT (+ MERKEZ HATASI K14) + IKI EK

```
1 K14'u TEK KANALLI bir on olcum bulgusuna dayanarak "en yuksek hacimli sizinti" diye tarife
  tasidim. A raporunda "CALISTIRILMADI, L3 dogrulamali" UYARISI VARDI ve TASIMADIM.
  (Merkez bunu KENDI hatasi olarak da kaydetti - tek kanalli bulgu kalem oldu.)
2 Var olan bir pin dosyasinin (GuvenlikFix3SozlesmeTests.cs, 16.880 B, alti pin) USTUNE
  YAZDIM. `git status`un "A" yerine "M" gostermesinden yakalandi; kayip YOK, md5 birebir
  geri kondu, commit amend edildi (push/denetci oncesi).
3 MK-6 mutasyon dongusunde geri alma `git checkout --` ile yapiliyordu; K5/K6/K7 HENUZ
  COMMIT'LENMEMISTI ve dongu URETIM KODUNU SILDI. K1/K2/K4'u TAM DA bunun icin
  commit'lemistim - kendi kurdugum onkosulu kendim ihlal ettim. Donguye "git status bos
  degilse CALISMA" kapisi konuldu.
4 ExceptionMiddleware yorumuna CURUYEN BIR IDDIA yazdim ("maskelenen besi de derleyici
  uretimi ad") ve yorumun KENDI ORNEGI onu curutuyordu. Rapor denetcisi yakaladi.
EK-1 F1'de `\z` degisimini sed ile yaptim ve SED TERS BOLUYU YEDI - regex `})z"` oldu, yani
  literal 'z' bekler hale geldi ve dogrulamayi KIRARDI. Build oncesi yakalandi. MK-8'in tam
  ihlali; KACIS-KAYBI ailesinin yeni vakasi, bu kez DUZELTME YAZMA aninda.
EK-2 F1 commit'inde bicim kapisini ayni cagrida kosup SONUCUNU KONTROL ETMEDEN commit ettim;
  whitespace exit=2 idi. MK-9'un tam ihlali. Duzeltilip amend edildi; sonraki commit'lerde
  kapi sonucu commit'ten ONCE kontrol edildi.
AYRICA denetciler iki BAYAT/CELISKILI yorumumu ve bir ARITMETIK hatami duzeltti.
CAPA KIRLENMESI YENI BICIMI: F2 pininde aranan desenler pinin KENDI dosyasinda DIZGE
LITERALI olarak duruyordu; yorum siyiricisi literalleri KAPSAMAZ ve tarayici kendini "ihlal"
buldu. Kural-uyum denetcisi bu boslugu ONCEDEN uyarmisti. Yapisal cozum: tarayici dosya
kapsamdan cikarilir.
```

## RIG BULGUSU (MK-4b) — F2'DE KAPATILDI

```
DIVISIMA_TEST_SQL'in Database= parcasi denetciyi IZOLE ETMIYORDU: 47 uretim noktasi
InitialCatalog'u SABIT DbName ile eziyor. Iki denetcinin kosumlari birbirini kirletti
("already exists" -> 157-335 SAHTE kirmizi). MK-4b worktree'yi ayirdi, TEST DB'sini AYIRAMADI.
Ayrica worktree'de appsettings.Development.json olmadigi icin 292 test fail-fast'e takiliyor
(CI bunu ci.yml:67 ile veriyor). Denetci scratchpad'i de PAYLASILMISTI.
-> F2 ile DIVISIMA_TEST_DB ad alani eklendi; kalan iki madde B4/MK-4b ekine yazildi.
```

## GOZ TURU BEKLIYOR — SEKIZ KALEM

```
GF-2a'dan alti: iki sekmede tek refresh · cikis sonrasi onbellek · offline acilis ·
  data:image/png gorsel · shade onizlemesi · admin Chart
GF-3'ten iki: (7) ARAMA UCU ARTIK 100/dk YERINE 20/dk - goz1 sozlesmesi KIRILMADI
  (RateLimit:HassasPermitLimit yapilandirilabilir) ama tur oncesi bilinmezse "sebepsiz 429"
  gorunur · (8) K13'un UCTAN UCA kaniti (gecersiz hex ile PUT -> 400; CSV satiri reddi)
```

## KANIT GUCU

```
COK KANALLI : K5 · K7 · K9 (EN GUCLU - canli Kestrel) · K11 · K12
TEK KANALLI : K6 (HSTS - nginx bu makinede ayaga kaldirilamaz) ·
              K13 (uctan uca alinmadi, goz turune devredildi)
Ikisi de pin dosyasinda ACIKCA BEYAN EDILDI ve cok kanallilarla AYNI GUVEN SIRASINA KONMAZ.
```
