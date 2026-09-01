---
name: surec
description: Divisima dalga sureci (degismez) — merkez koordinasyon, DUR, push/muhur usulu; her tarif basinda yuklenir
---

## SUREC (degismez)

- **Tek push -> tek run -> tek rapor.** Commit/push karari HER ZAMAN kullanicidan gelir.
- **FORCE-PUSH YASAK (kalici).** Gecmisi yeniden yazmak paylasilan `main`'i bozar, tum
  klonlari ayristirir ve daha once verilen HER run raporunun SHA'sini gecersiz kilar -
  raporlarin kanit degeri SHA'ya bagli oldugu icin bu, gecmis butun kaniti curutur.
  Depoya yanlislikla giren bir sey varsa cozum: ileriye donuk maskeleme + gerekiyorsa
  **DAR KAPSAMLI** `.gitleaksignore` fingerprint'i (bkz. `.gitleaksignore` basligi).
  Gercek bir kimlik bilgisi sizarsa yol farklidir: once **iptal/rotasyon**, sonra karar -
  o durumda gecmis yeniden yazmak gundeme gelebilir ve karar kullanicinindir.
- **Push on-onayinin dort kosulu**: (a) `Category=Sql` yerel komut yesil,
  (b) tam suit yesil, (c) Release build 0 hata, (d) o sprintin pinlerinde dis kontrolu
  (>=3 assert ters cevir -> isimli kirmizi gozle -> geri al).
- **Test sayilari CI'dan OKUNAMAZ.** Job log'u anonim erisime 403, Summary imza istiyor,
  annotation yalniz `Failed` satirlari tasiyor, check-run `output` bos (dordu de denendi).
  Kanit = **adimin SUCCESS olmasi** + yerelde `ci.yml`'dan cikarilan komutun verdigi sayi.
- **`secret-scan` TERSINE: ANNOTATION'DAN DEGIL ADIM SONUCUNDAN OKUNUR (kalici kural).**
  Gitleaks bulgusunu **`warning`** seviyeli bir annotation olarak basiyor
  ("Leaks detected, see job summary for details"); job'da `failure` seviyeli annotation
  **SIFIR** kaliyor. Yani annotation'a bakan bir okuyucu bu job'i YESIL sanir. Tek durust
  sinyal **adim sonucu** (`Gitleaks (secret taramasi)` = FAILURE). Ayrintili bulgu listesi
  Summary'de ve SARIF artefaktinda; ikisi de imza istiyor (artefakt indirme anonim **401**,
  `code-scanning/alerts` anonim **401** - ikisi de olculdu). Kok sebep bu yuzden **depo
  taramasiyla** bulunur, kanit kanalindan degil.
- **`format-check` JOB SONUCUNDAN DEGIL ANNOTATION'DAN OKUNUR (kalici kural).** Adim
  `continue-on-error` altindaysa job YESIL, adim sonucu da API'de `success` gorunur; tek
  durust sinyal `check-runs/{job_id}/annotations` icindeki `annotation_level: failure`
  satiridir. E2b run raporunda bu ortaya cikti: format adimi en az E2'den beri exit 2
  veriyordu ve onceki raporlarda "SUCCESS" olarak gecmisti (job duzeyinde dogru, adim
  duzeyinde yaniltici). Format dalgasinda kapi sertlestirildi (`continue-on-error` kaldirildi),
  ama kural genel: **`continue-on-error` tasiyan HER adim annotation'dan okunur.**
- **Sunucular `Start-Process` ile AYRIK baslatilir.** `dotnet run` ve statik sunucu bash arka
  planindan baslatilirsa kabuk oturumu kapaninca SESSIZCE olurler (E2b'de ikisi de yasandi;
  API logu hatasiz kesildi, storefront'ta SW eski sayfayi servis edip kesintiyi gizledi).
  Uzun sureli izleyici ikisinin sagligini da yoklamali.
- **DIS/MUTASYON KONTROLUNDEN ONCE `Divisima.API.exe` DURDURULUR (iki kez bedeli odendi).**
  API kosarken `dotnet build`, bagimli projelerin (Bussiness/API) ciktilarini yazamaz ve
  **SESSIZCE ESKI IKILILERLE** devam eder: `dotnet test --no-build` bir ONCEKI kosumun
  sonucunu birebir tekrarlar. Mini dalgada tam bu yasandi - mutasyon kosumu, diş kontrolu
  kosumunun ciktisinin AYNISINI verdi ve mutasyon uygulanmamis gibi gorundu.
  **TESHIS:** build ciktisinda `tail -1` ALDATIR (yalniz "Geçen Süre" satirini gosterir);
  her zaman `grep " Hata"` ya da `grep "error"` ile bakilir.
- **`--no-build` ile kosulan test, DEGISTIRILEN kodu DOGRULAMAZ.** Format dalgasinda bir kez
  yasandi: `dotnet format` 116 dosyayi degistirdi, `dotnet build` calisan API yuzunden dosya
  kilidiyle 8 hata verdi, ama `--no-build` testler ESKI ikililerden gecip yesil gorundu.
  Kod degistiyse ONCE temiz build, SONRA test.
- **BIR DOSYA KENDI ICERIGINDEN TURETILEREK USTUNE YAZILMAZ (KALICI - GUVENLIK-FIX-3'te
  bedeli odendi).** Kabuk `>` yonlendirmesini komut CALISMADAN ONCE acar ve hedefi **budar**.
  Yani `awk ... CLAUDE.md > CLAUDE.md` ya da girdisi hedeften tureyen (`cp hedef yedek &&
  awk ... yedek > hedef`) her zincir, ARADAKI HERHANGI BIR ADIM DUSERSE hedefi **SIFIR BAYT**
  birakir. GUVENLIK-FIX-3'te birebir yasandi: bir onceki komutta `awk` girdi dosyasini
  bulamayip dustu, ama `> CLAUDE.md` coktan calismisti - **6670 satirlik dosya sifirlandi**.
  Kurtaran sey `git checkout -- CLAUDE.md` oldu (calisma agaci o dosya icin TEMIZDI).
  **KURAL:** cikti **GECICI bir dosyaya** yazilir, **satir sayisi/boyutu DOGRULANIR**, ancak
  ondan sonra hedefin ustune tasinir:
  ```
  awk ... CLAUDE.md > $T/yeni && N=$(wc -l < $T/yeni) && [ "$N" -gt <esik> ] \
     && cp $T/yeni CLAUDE.md || echo "IPTAL - dosyaya DOKUNULMADI"
  ```
  **YEDEGIN VAR OLDUGU DA DOGRULANIR** - yedegi alan komut basarisiz olduysa "yedek var"
  varsayimi ikinci bir kayip uretir. Ayrica: takip edilmeyen (untracked) bir dosyada bu hata
  **GERI ALINAMAZ** - git kurtarmaz.
- **COK SATIRLI KOD BLOKLARI BETIKLE DEGISTIRILMEZ (KALICI - FLAKE-FIX'te bedeli odendi).**
  `perl -0pi -e 's|...|...|'` ile cok satirli bir C# blogunu degistirmek, desen bir karakter
  bile kaymissa dosyayi SESSIZCE BOZAR. FLAKE-FIX'in M1 mutasyonunda birebir yasandi:
  `Program.cs`'in `using` blogu govde ile birlesti ve build **82 hata** verdi; test o turda
  BAYAT IKILILERLE kosup 1 kirmizi verdigi icin sonuc GECERSIZ oldu ve ancak
  **"(b) TEMIZ BUILD"** adimi sayesinde yakalandi. **KURAL:** cok satirli kod degisikligi
  hassas duzenleme araciyla yapilir; betik kullanildiysa (a) `[MUTASYON]` izi, (b) BUILD HATA
  SAYISI ve (c) `git diff --stat` ile "yalniz amaclanan degisiklik" DOGRULANIR. Ayni tuzagin
  markdown karsiligi capa benzersizligidir (GUVENLIK-FIX-4) ve dosya budama karsiligi
  yonlendirmedir (GUVENLIK-FIX-3) - ucu de AYNI aile: DUZENLEME SONRASI DOGRULAMA.
- **5. KONTROLUN KENDISI DOGRULANIR (KALICI - kullanici karari, Dalga D).**
  5. kontrolun sonucu ("mutasyon lokalize kaldi") ancak mutasyon GERCEKTEN uygulandiysa
  anlamlidir. Dalga D'de uc mutasyon **HIC UYGULANMADI** (`powershell -File` yurutme
  politikasina takildi) ve testler "14 basarili" dedi - yani rapor "mutasyon lokalize"
  diye YANLIS yazilacakti. Fark edilmesi kalinti kontrolune, yani TESADUFE kalmisti.
  Bundan sonra HER uretim mutasyonunda, sirayla:
  - **(a) YAZILDI MI:** mutasyonun dosyaya gercekten indigi `grep` / `git diff` ile
    DOGRULANIR. "Betik hata vermedi" kanit DEGILDIR.
  - **(b) TEMIZ BUILD:** mutasyondan sonra derleme yapilir ve `grep " Hata"` / `grep "error"`
    ile bakilir (`tail -1` ALDATIR). `--no-build` ile kosulan test degistirilen kodu
    dogrulamaz; `Copy-Item` zaman damgasini korudugu icin geri alinan dosya `touch`lanir.
  - **(c) BEKLENEN PIN KIRMIZI OLMADIYSA:** bu **"mutasyon lokalize"** DEGIL,
    **"MUTASYON UYGULANMADI"** suphesidir. ONCE bu ihtimal elenir; ancak (a) ve (b)
    kanitlandiktan sonra "lokalize" sonucu yazilabilir.
  Ayni kural DIS KONTROLU icin de gecerlidir (ters cevrilen assert dosyaya indi mi).
- **TEST, URUNUN GERCEK KAYNAKLARINA DOKUNMAZ (KALICI - kullanici karari, Dalga D).**
  Bir test altyapisi eklenirken sorulacak soru: **bu, gelistiricinin ya da uretimin GERCEK
  kaynagina mi yaziyor?** Kaynak = depo agaci, gelistirici veritabani, kullanici secret'lari,
  gercek dosya sistemi, dis saglayici. Cevap "evet" ise test AYRI, atilabilir bir koke
  yonlendirilir. Ayni sinif **UC KEZ** cikti ve ucu de sessizdi:
  - **wwwroot sizintisi (D1):** her kosum depo agacina 64 baytlik sahte PNG birakiyordu
    (96 dosya birikmisti). Cozum: `UseWebRoot(TestWebRoot.Yol)` - UCUNCU bir kok.
  - **user-secrets sizintisi (Dalga C):** `WebApplicationFactory` Development'ta user-secrets
    yukledigi icin `AdminSeed:Enabled=true` her test host'una siziyordu - sonuc MAKINEYE gore
    degisiyordu. Cozum: `TestHostConfig`te varsayilan `false`.
  - **Hangfire dev DB'ye yaziyordu (Dalga D):** her test host'u kosulsuz bir arka plan
    sunucusu acip GELISTIRICININ veritabanina recurring job tanimi yaziyor ve dakikalik
    outbox isini testlerin drenajiyla YARISTIRIYORDU (CI kirmizisi `cd51a52`).
    Cozum: `BackgroundJobs:Enabled=false`.
  Ortak belirti: **yerelde yesil, CI'da kirmizi** (ya da tersi) - yani sonuc ORTAMA bagli
  hale gelir ve pin yalan soyler. Yeni yazilan her test altyapisi bu soruyla gecer.
- **Izleyici adabi**: nabiz >= 300 sn, tur basina TEK konsolide cagri, kota yandiysa bekle.
  Dependabot run'i beklenmez - asil iki workflow (CI + Security) yeter.
- **PAT veya tarayici eklentisi ASLA istenmez.**
- **Yerel SQL**: `DIVISIMA_TEST_SQL` her zaman set edilir (skip modu kullanilmaz);
  dizgede `Database=` bulunmalidir. LocalDB cokmus durumda ve **`sqllocaldb delete`
  YASAK** (ayni ornekte baska bir projenin `GarajimDb` veritabani var). Tam ornek
  (`Server=localhost`) kullaniliyor.
- **Uretim kodu**: yalniz kullanicinin acikca izin verdigi kalemlerde. Kapsam disi
  bulgular duzeltilmez, **SUPHELI DAVRANISLAR** basligiyla raporlanir.


---

---

