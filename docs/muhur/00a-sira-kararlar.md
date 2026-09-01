## SIRA

0. **KALITE SUPURMESI KAPANDI - LAUNCH'I BLOKE EDEN TEKNIK KALEM KALMADI.**
   Kanit SHA: **`dbaa763`** (her iki workflow tamamen yesil, alti job'da failure seviyeli
   annotation SIFIR). Bes olcum dalgasi ve duzeltmeleri, kapanan/acik kalan kalemlerin tam
   listesi ve kapanisin saha kaniti icin **yukaridaki "KAPANIS KAYDI" bolumune** bak -
   acik kalemlerin GUNCEL ve TEK dogru listesi ORASIDIR; bu madde yalnizca isaret eder.
   Ozetle acik kalanlar (HICBIRI BLOKE ETMEZ): SUPHELI #14 · G4 (satici modulu ON KOSULU) ·
   M2/M4/M5/M6/M7/M8/M9 · B5 · B13 · launch-sonrasi defterin tamami.
0b. **LAUNCH-FIX FAZI SURUYOR.** Bes dalga planlandi: A ilk musteri zinciri, B operasyon
   yuzeyi, C yayin altyapisi, D gercek veri provasi, E olu yuzey karari.
   **DALGA A TAMAMLANDI** (`8818f19` - A1 mail altyapisi + A2 sifremi unuttum + A2-FIX sifre
   politikasi + A3 misafir checkout + A4 tek para birimi).
   **DALGA B TAMAMLANDI** (`8e46337` - her iki workflow tamamen yesil, alti job'da failure
   seviyeli annotation SIFIR) - admin panelinin HIC ACILMAMIS bes ekrani surulup B1..B5
   kapatildi; ayrintisi "DALGA B - OPERASYON YUZEYI" bolumunde.
   **DALGA C TAMAMLANDI** (`d5993ea` - her iki workflow tamamen yesil, alti job'da failure
   seviyeli annotation SIFIR) - C1 storefront'u sunan tanim, C2 gorsel kaliciligi, C3 ilk
   admin, C4 arka plan is hatalari + log saklama, C5 paylasim/sitemap, C6 Update
   transaction'i + kargo bekleyen listesi; ayrintisi "DALGA C - YAYIN ALTYAPISI" bolumunde.
   **DALGA D DEVAM EDIYOR - ALTI KALEMDEN YALNIZ BIRI (D2) BITTI.** D2 (yetim stok +
   `FK_product_stocks_product_id`) tamamlandi ve pinlendi; **D1 karari ALINDI ama
   UYGULANMADI**, **D4 yalniz STATIK okundu**, **D3/D5/D6 HIC BASLANMADI**. Ayrintisi
   "DALGA D - GERCEK VERI PROVASI" bolumunde.
   **D-SEMA (YALNIZ OLCUM) TAMAMLANDI ve D-SEMA-FIX UYGULANDI** - kullanici karari
   **secenek (a): TEK DOGRULUK KAYNAGI EF MIGRATIONS**. `01_schema.sql` artik
   `dotnet ef migrations script --idempotent` CIKTISI (elle bakim bitti, `generate_schema.py`
   silindi); 44 dogrulanmis FK gercek migration'a tasindi ve 8 ad kisa bicime cekildi
   (toplam **53 FK**, hepsi NO_ACTION); model<->migration kayma kapisi CI'ya eklendi;
   deployment checklist'e DB saglama bolumu, runbook'a guncel migration notu girdi.
   Ayrintisi "D-SEMA (YALNIZ OLCUM) ve D-SEMA-FIX" bolumunde.
   **D1, D4, D5 TAMAMLANDI.** D1: gorsel sizintisi kapandi (test host'u gecici WebRoot'a
   yaziyor), 3 yetim DB satiri URETIM YOLUYLA silindi, 131 yetim dosya temizlendi.
   D4: idempotency'nin UC olculmus kusuru duzeltildi (capraz kullanici, anahtar yakma,
   olu replay dali) + dorduncu bulgu (IDistributedCache yalniz Redis dalinda kayitliydi).
   D5: canli Redis turu OLCULMEDI (Docker/Redis bu makinede YOK - staging'e ertelendi), ama
   rate limit AYRISMASI duzeltildi: kova tanimlari TEK KAYNAKTAN, iki yol da her zaman
   devrede, cifte sayim OLMADIGI uctan uca olculdu.
   **D3 (GERCEK OLCEK PROVASI) TAMAMLANDI - YALNIZ OLCUM, KOD DEGISMEDI.** 400 urunluk
   isaretli seed kuruldu, olculdu, TAMAMEN silindi (silinme kanitli, yetim 0).
   Dalga 3'un YAPI pinleri olcekte de TUTUYOR (sorgu sayisi satir sayisindan bagimsiz).
   **YENI BULGU (ISLEV-KIRAN, DUZELTILMEDI): storefront katalogun yalnizca ILK 24 URUNUNU
   cekiyor; kalan %94'e gezinerek ULASILAMIYOR.** Eksik indeks kalemi KAPANMADI - 403 urunde
   DMV'nin canliligi bile gosterilemedi (sinir kesinlesti, esik cok daha yukarida).
   **BULGU AYNI DALGADA DUZELTILDI (kullanici karari: SIMDI DUZELT) - bkz. "D3-FIX".**
   Gercek sayfalama + kategori rotasinda sunucu tarafli `category_id` + slug uzaylarinin
   hizalanmasi; ayni hacimde olculdu: **24 -> 403 urune ulasilabilir**, ilk yukleme maliyeti
   DEGISMEDI. Ayrica retry gorunurlugu icin iki workflow'a `::warning::` adimi eklendi
   (calistirilarak dogrulandi). Ayrinti "DALGA D - D3" ve "D3-FIX" bolumlerinde.
   **D6 (YEDEK/GERI DONUS TATBIKATI) TAMAMLANDI -> DALGA D KAPANDI.**
   Tatbikat HIC YAPILMAMISTI; yapildi ve runbook'un IKI iddiasi olcumle CURUDU, runbook
   DUZELTILDI: (a) **RPO 15 dk SIMPLE recovery'de IMKANSIZ** (`BACKUP LOG` -> Msg 4208) ->
   hedef KOSULLU hale getirildi + checklist'e zorunlu `FULL` dogrulamasi; (b) **Express
   Edition** backup compression/TDE desteklemiyor (Msg 1844) -> "yedekler sifreli olmali"
   Express'te karsilanamaz. **RTO dev'de 6,4 sn olculdu** (1 saatlik hedef tavan olarak
   korundu; tatbikat DEV ortaminda - uretim donaniminda farkli olabilir).
   Veri tutarliligi: 11 invariant, ONCE == SONRA, `diff` FARK BULMADI. Migration'lar gercek
   sema uzerinde kosuldu: script ile kurulan DB'de `dotnet ef database update` **NO-OP**
   (56 FK / 45 tablo) - D-SEMA'nin iddiasi KANITLANDI. Ayrinti "DALGA D - D6" bolumunde.
   **DALGA D'NIN ALTI KALEMI DE KAPANDI: D1 · D2 · D3 · D4 · D5 · D6.**
   **DALGA D RESMEN KAPANDI** (kanit SHA `2bc53c5`) - tam kayit "DALGA D KAPANIS KAYDI"
   bolumunde: alti kalem + D-SEMA + iki CI kirmizisinin kok sebebi + acik kalanlarin TEK
   listesi. **Ardindan TAKSONOMI isi de kapandi** (menu veritabanindan uretiliyor).
   **KOD TARAFINDA LAUNCH'I BLOKE EDEN IS KALMADI** - kanit SHA **`f9634cc`** (her iki
   workflow yesil, alti job'da failure annotation SIFIR). Kapanan fazlarin tablosu ve acik
   kalanlarin TEK listesi icin **"KAPANIS KAYDI - KOD TARAFINDA LAUNCH'I BLOKE EDEN IS
   KALMADI"** bolumune bak - acik kalemlerin GUNCEL ve TEK dogru listesi ORASIDIR.
   Siradaki faz IRL: domain karari, canli Iyzico basvurusu, hosting/DNS, gercek mail turu,
   gercek katalog aktarimi. **Kapsami kullanici ayrica verecek - YENI IS BASLATILMAZ.**
1. **TEKNIK DEFTERDE ACIK KALEM KALMADI - TEK ISTISNA SUPHELI #14** (surum okuyucusu
   kirilganligi, genel) ve o da **LAUNCH SONRASI**. #15, #17 ve **#18** KAPANDI; #16 BILINCLI
   olarak bos birakildi; siparis #33 hem odeme hem envanter tarafinda TEMIZLENDI.
   **ISIMSIZ FLAKE - KAPANDI (ACIKLANDI, Dalga D).** Yerelde bir kez gorulen ve adi
   yakalanamayan 4. kirminin kok sebebi `cd51a52` CI kirmizisinda ADIYLA olculdu: her test
   host'u kosulsuz bir Hangfire sunucusu calistirip dakikalik outbox isini testlerin kendi
   drenajiyla YARISTIRIYORDU. `BackgroundJobs:Enabled` ile kapatildi ve pinlendi. Kayitlar
   SILINMEDI, tarihsel iz olarak duruyor (bkz. MINI DALGA 2).
   **HALA ACIK:** `RefreshCookieContractTests.Cerez_Secure_...` (ADI OLAN flake) - Hangfire
   yarisi onun icin yalnizca bir ADAY, belirtisi eslesmiyor; CI'da tekrar ederse SUPHELI acilir.
3. **Sema kapanis dalgasi** - kalan tek aday: **gift-card expiry**
   (`refunded_amount` Sprint 6'da kapandi; seller migration DEGIL - `sellers` ve
   `seller_id` zaten `InitialCreate`'te)
4. **E4b** (musteri askiya alma, kategori, CMS ekranlari) - launch sonrasi olabilir

## KARARLAR (kapanmis)

- **AutoMapper: 12.0.1'de KAL, bump YOK.** Advisory (CVE-2026-32933) okundu, maruziyet
  olculdu, maruz DEGILIZ. Gerekce ve yeniden degerlendirme tetikleyicileri
  `SECURITY.md` "Kabul Edilen Riskler" bolumunde. **Onemli:** yamali surumler 15.1.1/
  16.1.1'dir ve AutoMapper 15+ **RPL-1.5 veya ticari lisansa** gecmistir; 12/13/14
  MIT ama ucu de ayni advisory kapsamindadir (olculdu). "MIT kalarak yamalanmak" mumkun degil.
- **Seller modulu**: dokunma, veri duzeyinde kapali, migrate/seed yok.
  **ZORUNLU ON KOSUL (GUVENLIK DALGASI / G4): modul acilmadan ONCE satici refresh token'i
  httpOnly cereze tasinmali.** Olculdu: `SellerAuthManager.cs:101` refresh token'i YANIT
  GOVDESINDE donuyor - Sprint 8 madde 6 bunu YALNIZ musteri yolunda duzeltmisti. Bugun
  ERISILEMEZ (`Seller:RegistrationEnabled=false` -> gecerli govdeyle kayit 403, `sellers`
  tablosu 0 satir), bu yuzden GUVENLIK-FIX dalgasinda DOKUNULMADI ve pin de YAZILMADI
  (var olmayan bir yuzeyi pinlemek yanlis guvence olurdu). Modul acilirken musteri
  tarafindaki cerez sozlesmesi (`OturumCerezleriniYaz` + CSRF double-submit) satici
  tarafina da tasinir ve `RefreshCookieContractTests` kalibinda pinlenir.
  **IKINCI ON KOSUL (GUVENLIK-FIX-2 eki): `SellerAuthManager.Login` kilit kontrolunu SIFRE
  DOGRULAMASINDAN ONCE yapiyor** - musteri tarafinda SUPHELI #19 olarak kapatilan oracle'in
  aynisi. Bugun uretemez (`sellers` 0 satir -> her giris `seller == null` dalina duser), ama
  modul acilir acilmaz uretir. Musteri tarafindaki sira (dogrula -> kilitliyse ve sifre DOGRU
  ise 403, degilse 401 + sayac artirma YOK) satici tarafina da tasinir ve pinlenir.
- **invoice_number**: entegrator (Nilvera) numarasi esas, bizimki ic referans - degisiklik yok.
- **Launch sonrasi defteri** (simdi is yok): gift-card expiry, 2FA enrollment ucu,
  step-up `auth_time` refresh'te sifirlanmasi, loyalty oransal geri alma + referral
  clawback, Dashboard tam-tablo agregalari. **Dusen kalem:** Http.Abstractions 2.2.0
  (hicbir csproj'de referans yok).
  **YENI KALEM (Dalga 2 / B13 - kullanici karari): TERK EDILMIS PENDING SIPARISLERE TTL.**
  Olculdu: 17 Pending siparis, HEPSI 24 saatten eski (en eski 20 Agustos). Rezervasyonlar
  serbest (5 dk'lik `reservation-cleanup` calisiyor - suresi gecmis Active rezervasyon 0), stok
  ve kupon limitleri guvende; ama bu siparisler musterinin "Siparislerim" ekraninda SONSUZA
  KADAR "Onay bekliyor" duruyor ve onlari iptale ceken bir arka plan isi YOK. Aday: 24-48 saat
  sonra otomatik iptal + bildirim. **POLITIKA URUN KARARIDIR, kullanici sonra verecek.**
  **GUVENLIK-FIX-4 EKI: B13 artik misafir siparis guard'inin TAMAMLAYICISIDIR.** Guard,
  kanonik posta kutusunda ACIK siparis sayisini sinirlar; TTL olmadigi icin o siparisler
  kendiliginden kapanmaz ve kurbanin MISAFIR yolu esik dolu kaldigi surece acilmaz
  (kayit/giris yolu ACIK - bilincli, bkz. GUVENLIK-FIX-4 "guard'in ters yuzu"). TTL gelirse
  ikisi birlikte kendini toparlar.
  **YENI KALEM (D3 - kullanici karari): EKSIK INDEKS ESIGI, GERCEK HACIMDE TEKRAR BAKILACAK.**
  Dalga 3 "62 uruncuk veride DMV oneri uretmemis olabilir" diye durust bir sinir koymustu.
  D3'te 403 urunle tekrar olculdu ve sinir KAPANMADI, yalnizca KESINLESTI: DMV yine 0 oneri
  verdi ve **DMV'nin canli oldugu bile gosterilemedi** - KASITLI indekssiz esitlik sorgulari
  da oneri uretmedi. Sebep olculdu: uc sorgulari kosum basina 10-18 MANTIKSAL OKUMA yapiyor;
  403 satirlik tablo birkac sayfa, hicbir indeks bunu yenemez. Yani esik 400'un COK USTUNDE.
  **KORLEMESINE INDEKS EKLENMEZ** (kullanici sarti). Gercek katalog hacmi olustugunda
  (ya da bilincli olarak cok daha buyuk bir seed'le) `sys.dm_db_missing_index_*` ve
  `sys.dm_exec_query_stats` yeniden okunur.
  **[KAPANDI - "TAKSONOMI" bolumu] GEZINME TAKSONOMISI VERITABANINDAN URETILMIYORDU.**
  D3'te olculdu, Dalga D'den sonra kullanici karariyla kapatildi (gercek katalog aktarimindan
  ONCE gerekiyordu). Menu artik `/api/category/getlist` yanitindan uretiliyor (EK ISTEK YOK),
  taninmayan rota sessizce `tumu`ya yeniden yazilmak yerine 404'e dusuyor, alt kategoriler
  sunucudan geliyor ve kategori yoksa menu bos gorunmuyor. Ayrinti "TAKSONOMI" bolumunde.
  **YENI KALEM (Dalga 3 / P4 - kullanici karari): ISTEMCI TARAFI ONBELLEK.**
  Olculdu: hesap sekmeleri arasi her gecis yeniden cekiyor; AYNI siparis detayini kapatip acmak
  2 istek daha (order/get + order/timeline). Tazelik acisindan savunulabilir bir tercih, ama
  olculmus ve ucretsiz bir kazanc kapisi.
  **YENI KALEM (Dalga 3 / P2 kalani - kullanici karari): index.html'in SATIR ICI 704 KB
  script + 142 KB style BLOKU BOLUNMESI.** DALGA-3-FIX yalniz (a) harici script'lere `defer`
  ve (b) fontun render-bloklamamasini yapti; render-bloklayan kaynak 5 -> 0 oldu. Ama belge
  hala 883 KB ve %95'i satir ici kod. Bolme AYRI bir is: dis dosyalara cikarma + onbelleklenebilir
  hale getirme + CSP'nin `unsafe-inline` bagimliliginin gozden gecirilmesi birlikte ele alinmali.
  **YENI KALEM (dalga-1-fix eki - kullanici karari): TURKCE KLAVYEDE YAZILAN E-POSTA.**
  `KimlikDizgesi.KanonikKod` (Turkce harf katlamasi) BILEREK e-postaya UYGULANMIYOR - e-posta
  kullanicinin KENDI kimligidir, oradaki karakteri sessizce degistirmek kimlik verisini yeniden
  yazmak olur. Sonuc: adresini Turkce klavyede `İ`/`ı` ile yazan kullanici, kayitta yazdigi
  harfle giris yapmak zorunda. Invariant casing bu ikisini katlamaz. Karar kullanicinin.
  **YENI KALEM (GUVENLIK-FIX / G2 eki - kullanici karari): SABIT-ZAMANLI KAYIT.**
  G2 kayit ucunun YANIT sizintisini kapatti (var olan ve yeni adres birebir ayni 201 + ayni
  govde) ama ZAMANLAMA kanalini kapatmaz: yeni kayit yolu hash + INSERT + riza satirlari
  yazar, var olan yol yalniz bir e-posta gonderir. OLCULDU: 400 yolu 9 ms, 201 yolu 14 ms
  (duzeltme sonrasi 49 ms / 56 ms). Fark kucuk ve aglar uzerinden gurultuye gomulur, ama
  yerel/hizli bir ag uzerinde istatistiksel olarak ayrilabilir. Sabit-zamanli kayit AYRI bir
  istir (her iki yolda da ayni is birimini harcamak ya da yaniti sabit bir sureye yaymak).
  **Kullanici karari: launch-sonrasi deftere.**
  **YENI KALEM (Sprint 8 madde 8 eki - kullanici karari): RFC 2606 ust alan adlarini KAYITTA
  reddetme.** Kayit validatoru FluentValidation'in permisif `.EmailAddress()` kuralini kullaniyor
  ve `.test` / `.example` / `.invalid` / `.localhost` adreslerini KABUL EDIYOR; gercek Iyzico
  reddediyor (E2b'de olculdu), yani o adresle uye olan musteri HIC kart odemesi yapamiyor.
  Sprint 8'de AYIRT EDILEBILIR MESAJ eklendi (init hatasinda sebep soyleniyor, saglayicinin ham
  metni sizdirilmiyor) ve bu YETERLI goruldu. Validatoru sikilastirmak ayri bir URUN karari:
  gecerli ama alisilmadik adresleri kapida cevirmek gercek musteri kaybettirebilir.
  **Sprint 8'e GIRMEZ.**
  **YENI KALEM (Dalga 4 / M10-M11 eki - kullanici karari): CIKISLI KULLANICIYA DOGRUDAN
  GIRIS KATMANI.** Bugun "Sepeti Onayla" cikisli kullaniciyi `#/odeme`ye dusuruyor ve orada
  "Siparisi tamamlamak icin giris yapmalisin" + "Giris yap" gorunuyor. Bu davranis
  DEGISTIRILMEDI ve gerekcesi Dalga 4 bolumunde: sepet icerigi KORUNUYOR (E2'de pinli),
  odeme sayfasi ozeti tekrar gosteriyor, cekmecenin acik kalmasi "bir sey olmadi" hissini
  ARTIRIRDI. Gercek kusur cekmece degil, hedef sayfanin tek eyleminin ortulu olmasiydi (M11)
  ve o KAPANDI. Yine de "cikisli kullaniciyi ara bir sayfaya dusurmek yerine dogrudan giris
  katmanini acmak" savunulabilir ve muhtemelen daha az adimli bir URUN karari. LAUNCH ONCESI
  DEGIL.
  **YENI KALEM (Dalga 4 eki - kullanici karari): JS/DOM TEST KOSUCUSU (Playwright vb.).**
  Olculdu: depoda JS/DOM kosucusu YOK, dolayisiyla TARAYICI SEMANTIGI (hit-test, CSS
  ozgullugu, `elementFromPoint`) CI'da pinlenemiyor - M10'un kok sebebi tam da bu katmandaydi.
  Bugunku telafi YETERLI goruldu: 7 kaynak sozlesmesi pini (`FrontendDokunmaHedefiTests`) +
  depoya konan tekrarlanabilir olcum betigi (`frontend/test/mobil-erisilebilirlik.js`).
  **LAUNCH ONCESI EKLENMEZ:** yeni bir bagimlilik `dependency-scan` kapsamina girer ve tarayici
  ikilisi indiren bir kosucu CI suresini/yuzeyini buyutur - launch oncesi alinacak risk degil.
  **YENI KALEM (GUVENLIK DALGASI 2 / #1+#2 - kullanici karari): MISAFIR CHECKOUT
  ENUMERATION ve COP COD SIPARISI.** Olculdu: `POST /api/guest-checkout/place` kayitli bir
  e-postaya **409** ("Bu e-posta kayitli. Lutfen giris yapin."), kayitsiz olana **201** doner -
  yani anonim bir saldirgan kimlik dogrulamasi olmadan "bu adres musteri mi" sorusunu sorabilir
  (G2'de `/api/auth/register`da KAPATTIGIMIZ kanalin aynisi). 201 yolu ayrica musteri satiri +
  **SIPARIS** + kurbana dogrulama maili uretir.
  **KARAR: LAUNCH SONRASI.** Gerekce kullanicinin: **409 hesap ele gecirmeyi ENGELLIYOR** (var
  olan hesabin ustune yazilamiyor) ve onu kaldirmak daha buyuk bir riski acar; G2 kalibini
  (ayni yanit + gercegi e-postayla soyle) uygulamak misafir akisinin TASARIMINI degistirir ve
  su an gereksiz risk. **10/dk/IP yeterli hafifletme** (olculdu: 11. istek 429).
  **YENI KALEM (GUVENLIK DALGASI 2 / B5 eki - kullanici karari): `failed-jobs` PII RISKI
  GERCEK MAIL TURUNDA YENIDEN OLCULECEK.** `GET /api/dashboard/failed-jobs` yalniz
  `id/event_type/retry_count/error/created_at/processed_at` donuyor (payload BILINCLI olarak
  disarida, `error` ayrica `KanitMaskesi`nden geciyor) ve mevcut tek hata metninde e-posta yok.
  **DURUST SINIR: PII tasiyan bir hata metni bu ortamda URETILEMEDI** (SMTP kapaliydi), yani
  risk teorik kaldi. SMTP acildiginda (bkz. "GERCEK MAIL TURU - BEKLIYOR") gercek bir gonderim
  hatasi uretilip `error` alaninin ne tasidigi OLCULMELI - saglayici hata metinleri alici
  adresini tasiyabilir.
  **YENI KALEM (GUVENLIK DALGASI 2 yan gozlemi - DOKUNULMADI): `frontend/pwa/` DIZINI OLU.**
  Olculdu: index.html `/manifest.json`, `/pwa-register.js` ve `/service-worker.js`i KOK'ten
  yukluyor; `pwa/` altindaki dort dosyaya (manifest.json, offline.html, service-worker.js,
  sw-register.js) referans veren **hicbir sey yok**. GUVENLIK-FIX-3'un deny kurallari onlari
  BILEREK kapsamiyor - ic dokuman degiller (`pwa/README.md` yalniz `.md` oldugu icin kapandi).
  Mukerrer/bayat bir yuzeydir; silmek AYRI bir karardir.
- **Iyzico'nun TELEMETRI alan adlari CSP'de ACILMAZ (kalici karar).** `countly.iyzico.com`
  ve `*.ingest.tr.sentry.io` (o120955...). Iyzico checkout formu kendi
  Countly analitigine baglaniyor (`campaign_banner_enabled`, `checkout_radio_button_layout_updated`
  gibi A/B bayraklari) ve Sentry hata toplamaya. Ucuncu taraf izleme; engellendiklerinde
  form yine ciziliyor ve odeme akisi calisiyor. Resmi Iyzico ALAN ADLARI (static / api /
  cdn / merchantgw / consumerapigw.iyzipay.com) E2b de OLCULEREK acildi - tahminle degil,
  her tur konsoldaki ihlal ve canli iyziInit yapilandirmasi okunarak.
- **Auth modeli**: mevcut hibrit korunuyor (access localStorage + refresh httpOnly
  cookie + kosullu CSRF). Backend ile uyumlu oldugu dogrulandi.
- **`EnableRetryOnFailure`: S7'de ACILMADI.** S7 engeli kaldirdi (IyzicoPayment artik
  `ExecuteInTransactionAsync` kullaniyor) ama bayragi acmak AYRI bir karar ve alinmadi.
  Acmadan once `Program.cs` yorumundaki DIGER manager'lar (OrderManager, GiftCard,
  Loyalty, Referral, Return, StoreCredit) da tasinmali - aksi halde onlarin manuel
  `BeginTransaction` cagrilari retry stratejisi tarafindan REDDEDILIR.
- **SPRINT 8 = E FAZI SONRASI LAUNCH-ONCESI ZORUNLU DALGA (ON UC KALEM).**
  **COMMIT BOLUNMESI ONAYLI (kullanici karari): UC COMMIT** - guvenlik (6+7+9),
  dogruluk (1+2+3+4+11+13), yuzey (5+10+12+8). Hepsi **TEK PUSH, TEK RUN**.
  Gerekce: madde 6 pinleri BILINCLI kiriyor; tek dev commit'te bir regresyon `git bisect`
  ile ayristirilamazdi, ayrica onlarca dosyalik tek commit okunamazdi.
  **KALEM SIRASI (onayli):** 9-kurulum -> 6 -> 7 -> 11 -> 1 -> 2 -> 3 -> 13 -> 4 ->
  5 -> 10 -> 12 -> 8 -> 9-dogrulama.
  Simdi is yok; E fazi bitince kosulur. Sira onceligi (6) guvenlik oldugu icin ustte.

  1. **Kupon `used_count` idempotency** (outbox'in on kosulu). `IncrementCouponUsageWithRetry`
     duz sayac artisi; at-least-once bir mekanizmada FAZLA sayar. Cozum adaylari:
     `coupon_usages` satirlarindan turetmek ya da `(coupon_id, order_id)` unique indeks +
     artisi insert basarisina baglamak.
  2. **`InvoiceManager.GenerateForOrder` siparis DURUMU guard'i** - Cancelled/Pending
     siparise fatura kesilmez + pinler.
  3. **`PaymentConfirmed` outbox'a tasima** (altyapi hazir: `outbox_messages` +
     `OutboxService` + `OutboxProcessor` atomik claim/reclaim + `Cron.Minutely`).
     Kazanci: B bolgesi hatasi sessiz kalmak yerine yeniden denenir; maliyeti eventual
     tutarlilik (~1 dk) ve at-least-once idempotentlik zorunlulugu. **Outbox karari o gun.**
  4. **`LocalImageStorage`: CWD yerine `WebRootPath`.** Pin: yazma ile statik servis FARKLI
     calisma dizininde bile ortusur (test host'undaki `UseContentRoot` hizalamasina gerek
     kalmadan yesil).
  5. **Storefront `filter` yolu `category_name` + `total_stock` + `sizes` DOLDURUR**
     (DTO zenginlestirme). Duzeltme sonrasi istemcideki 6-esmanli detay telafisi
     (`api-bridge.js enrichAll`) KALDIRILIR ve pinler guncellenir.
     **KAPSAMA EKLENDI (kullanici karari): `my-orders` DTO zenginlestirme.** Ayni kok
     eksiklik: liste yolu ince DTO donduruyor, istemci her satir icin ek cagri yapiyor.
     E3'te `ReturnResponseDto`'nun **urun adi tasimadigi** da olculdu (yalniz `product_id`);
     iade listesi urun adini KATALOGDAN cozmek zorunda kaliyor. O da bu kalemin icinde.
  6. **ONCELIKLI (GUVENLIK): refresh token gercekten httpOnly cookie'ye tasinir.**
     `SetRefreshTokenCookie` GERCEKTEN kullanilir - login/refresh cookie YAZAR, refresh ucu
     cookie'den OKUR, logout siler; istemci uyarlanir; CSRF double-submit devreye girer.
     Eski govde-tabanli sozlesme pinleri BILINCLI kirilir, yenileri ayni commit'te gelir.
  7. **`Iyzico:CallbackUrl` uretim FAIL-FAST listesine eklenir.** (E2b'de olculdu)
     `Program.cs` satir 43-84'teki blok ConnectionStrings / TokenOptions:SecurityKey /
     Encryption:Key / MailSettings:Host kontrol ediyor; `Iyzico:CallbackUrl` YOK. Uretimde
     bos kalirsa HER kart odemesi init'te 400 ile duser ve musteri yalniz "Odeme
     baslatilamadi." goruru - E2b'de bu belirti birebir olculdu. Tam fail-fast konusu.
  8. **Kayit e-posta validatorunun Iyzico kabul kurallariyla uyumu INCELENIR (rapor).**
     (E2b'de olculdu) Gercek Iyzico `@divisima.test` adresini "email hatali format ile
     gonderilmistir" ile REDDEDIYOR; ayni musteri example.com ile 200 aliyor. Yani bizim
     kabul ettigimiz bir e-posta ile uye olan musteri HIC odeme yapamaz. Ayrica init-400
     dalinda kullaniciya AYIRT EDILEBILIR mesaj verilmesi degerlendirilir (bugun yalniz
     "Odeme baslatilamadi." goruluyor). Duzeltme YAPILMADI - turlar example.com hesabiyla suruldu.
  9. **WEBHOOK TUNEL DOGRULAMASI - LAUNCH ONCESI ZORUNLU** (E2b'de statusu YUKSELTILDI).
     Onceden "ayri bir dogrulama" olarak deftere yazilmisti; E2b bunun GERCEK bir senaryo
     oldugunu OLCTU. Siparis `DVS20260821-6958D22788`: odeme Iyzico sandbox'ta ALINDI,
     sonucu tasiyan form POST'u storefront CSP'si (`form-action 'self'`) tarafindan
     ENGELLENDI, callback HIC ATESLENMEDI, siparis PENDING kaldi. Uretimde bu "para gitti,
     siparis yok" demektir. Tasarimda tek telafi `POST /api/payment/webhook` (bant-disi
     bildirim, ayni HandleCallback mantigi, idempotent) - ama disaridan erisilebilir bir
     tunel olmadan HIC dogrulanmadi. Kapsam: public tunel -> Iyzico panelinde webhook
     adresi -> kaybolan callback senaryosu -> webhook'un siparisi Confirmed'a tasidigi
     OLCULUR. CSP senkron kurali (form-action = Iyzico:CallbackUrl origin'i)
     `appsettings.Development.example.json` icindeki `//Iyzico` aciklamasina yazildi.
  10. **BILDIRIM ABONELIKLERI: `unsubscribe` + "aboneliklerim" uclari.** (E3'te olculdu,
     kullanici karariyla deftere alindi) Backend'de YALNIZ `subscribe` var; tum controller'lar
     tarandi, abonelikten CIKMA ve "hangi aboneliklerim var" uclari YOK. Sonuc: kullanici
     kurdugu stok/fiyat bildirimini goremiyor ve KAPATAMIYOR. E3 istemcisi bunu gizlemiyor -
     abonelik TEK YONLU kuruluyor ve ekranda geri alma sozu verilmiyor; kalici cozum backend.
     Kapsam: `stock-notification/unsubscribe`, `price-drop/unsubscribe`, ikisi icin "benim
     aboneliklerim" listesi + Hesabim'da bir sekme.
  11. **`SuccessDataResult<string>` BELIRSIZLIGININ KOKTEN COZUMU.** (E3'te olculdu; iki
     cagri E3'te duzeltildi, KOK SEBEP ACIK) `T = string` oldugunda `(T data)` ile
     `(string message)` ayni imzaya duser ve C# generic OLMAYAN adayi secer; tek argumanli
     cagri veriyi MESSAGE'a yazar, `Data` null kalir ve `Success` true oldugu icin hata
     SESSIZ olur. E3 yalniz iki cagri yerini `data:` adlandirilmis argumana cevirdi -
     **YENI yazilacak tek argumanli bir string cagrisi yine sessizce bozuk olur.**
     Aday cozumler: (i) kurucu setini yeniden tasarlamak (`(string message)` kurucusunu
     kaldirip yerine `SuccessDataResult<T>.WithMessage(...)` gibi ayirt edilebilir bir
     fabrika koymak), (ii) tek-argumanli-string kullanimini yasaklayan bir analyzer/kural.
     Depo taramasi (E3, referans): `SuccessDataResult<string>` **4 cagri** -
     `OrderManager.cs`, `ReferralManager.cs` (ikisi de duzeltildi),
     `GiftCardManager.cs:43`, `ProductImageManager.cs:83` (iki argumanli, ETKILENMEZ).
  12. **PAYLASIM BAGLANTILARININ BASLIGI (kapsam OLCUMLE DARALDI).**
     **DUZELTME: "router'a rota eklenmesi" GEREKMEDI - rota ZATEN VARDI** (`index.html:2077`).
     E3'teki teshis yanlisti; ayrinti SUPHELI #10'da. Gercek is iki kalemdi ve yapildi:
     (a) `setDocTitle()`in `urun` dali olmadigi icin baslik "Sayfa Bulunamadi" kaliyordu -
         ustelik router bu fonksiyonu `openDetail`den SONRA cagirip dogru basligi eziyordu;
     (b) katalog yarisi - acilistaki router mock PRODUCTS ile kosuyordu.
     Olculen sonuc: baslik "Sayfa Bulunamadi · Divisima" -> "Siyah Midi Elbise · Divisima".

     Eski (YANLIS) kapsam metni, kayit icin: (E3'te olculdu,
     kullanici karariyla LAUNCH ONCESINE alindi - "paylasilan linklerin 404'u launch'a
     tasinmaz") `index.html:2154` `shareUrl(id)` -> `#/urun/<id>` uretiyor ve urun
     kartindaki WhatsApp / Facebook / X / Pinterest / "baglantiyi kopyala" secenekleri bu
     adresi paylasiyor; ama urun detayi bir ROTA DEGIL, `openDetail(id)` ile acilan bir
     MODAL ve router `#/urun` yolunu TANIMIYOR. Olculdu: `location.hash = "#/urun/1"` ->
     sayfa basligi **"Sayfa Bulunamadi · Divisima"**. Kapsam: router'a `#/urun/:id` yolu
     eklenir ve **katalog yuklendikten SONRA** `openDetail(id)` cagrilir (E3'te olculen
     katalog yarisi burada da gecerli - erken cagri MOCK urunu acardi), ardindan ELLE
     DOGRULAMA: paylasilan bir baglantiyi temiz sekmede acmak dogru urunu acmali.
     Bkz. SUPHELI #10.
  13. **KULTUR PINLEME.** (E3 run'inda CANLI ORTAMDA kanitlandi - bkz. SUPHELI #13;
     kullanici karariyla SUPHELI'den KALEME yukseltildi, DOGRULUK commit'ine girer)
     Uygulama hicbir yerde kultur pinlemiyor; para/tarih bicimlendirmesi kostugu kabin
     yereline gore degisiyor. GitHub kosucusu (invariant) fatura tutarini `1,049.70`
     olarak bastigi icin bu davranis ORTAMDA gorunur oldu.
     **MAGAZA TEK PAZARLI (TR / TRY) - tasarim buna gore.**
     Kapsam: (a) tasarim OLCEREK kurulur - aday `Program.cs`'te TEK NOKTA `tr-TR`
     pinlemesi (`CultureInfo.DefaultThreadCurrentCulture` + `DefaultThreadCurrentUICulture`);
     `RequestLocalization` alternatifi de olculur ve secim gerekcesiyle yazilir.
     (b) TUM `:N2` / `:C` / tarih bicimlendirme yuzeyi taranir (fatura HTML'i tek yer
     degil - e-posta sablonlari, PDF/e-fatura alanlari, log satirlari dahil).
     **PIN: fatura govdesi KOSUCU KULTURUNDEN BAGIMSIZ olarak `tr` bicimiyle cikar** -
     test kendi thread kulturunu invariant'a cekip yine `1.049,70` gormeli, yani pin
     CI'da da (invariant kosucuda) gecerli olmali. Dis kontrolu: pinleme kaldirilinca
     pin KIRILMALI.

