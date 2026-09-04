# 50 · GUVENLIK-FIX-4 (GF-4) MUHRU — TEDARIK ZINCIRI (4 Eylul 2026)

Kapsam: paket / imaj / CI yapilandirmasi. **Uretim C# kodu, `frontend/*`, `Seller*`,
`CLAUDE.md` ve TFM `net8.0` DOKUNULMAZDI** — ihlal 0 (kural-uyum denetcisi olctu).

## ZEMIN ve PUSH

```
zemin        b89955c
PUSH-1       b7b50a7  (K1 tek basina, cift yesil)
PUSH-2       b7b50a7..4976974  (dokuz commit)
kapanis HEAD 4976974
```

Suit tabani `50·GUVENLIK-FIX-4` kapanisinda **Sql 382/382 · tam 743/746** (+13 pin;
uc kirmizi = bilinen Docker uclusu, yerelde Docker YOK). Ureten ifade:
`dotnet test Divisima-Backend.sln -c Release --filter "Category=Sql"` ve filtresiz.

## ON OLCUM — BES DUR

Fan-out: A PAKET · B AUDIT · C CI · D DOCKER · E VENDOR JS · KAPSAM ELESTIRMENI.
Her ajan kendi HAM dosyasina yazdi (MK-5), hepsi worktree DISINDA.

| DUR | konu | merkez karari |
|---|---|---|
| DUR-1 | K3 (`WarningsAsErrors` ile NU19xx kapisi) | **K3 DUSTU.** Tek kalici iz: `Directory.Build.props`ta `NuGetAuditMode=all` ACIKCA, **uyari seviyesi** |
| DUR-2 | K5 `global.json` | **DUSTU.** Yerine **Y6** (imaj referanslari tek kaynak) |
| DUR-3 | K4 (deprecated kapisi sertlestirme) | **DUSTU.** `\|\| true` KALIR + satir yorumu |
| DUR-4 | K1 kapsami | **UYGULANIR** (actions + SHA) |
| DUR-5 | K6 | **K6-lite**: mevcut manifestten okur, ikinci kopya URETMEZ |

**DUR-1'in gerekcesi olculdu:** tarifteki ciplak `<WarningsAsErrors>NU1901;...</...>` SDK
varsayilanini EZIYOR ve **NU1605'i SESSIZCE DUSURUYOR** (ureten ifade
`-getProperty:WarningsAsErrors`: once `;NU1605;SYSLIB0011`, sonra
`NU1901;NU1902;NU1903;NU1904;SYSLIB0011`). Kapi kurulacaksa `$(WarningsAsErrors);` oneki
ZORUNLUDUR.

## YENI BULGULAR (Y1-Y8) — HEPSI ONAYLANDI

| Y | konu | sonuc |
|---|---|---|
| Y1 | `IdentityModel.Protocols*` 7.0.3/6.35.0 | 7.7.3'e hizalandi |
| Y2 | MSAL 4.76.0 / 4.67.2 (iki kok) | 4.77.1'de birlestirildi; **SqlClient DOKUNULMADI** |
| Y3+Y4 | xunit 2.6.2 · Testcontainers.MsSql 3.10.0 | 2.9.3 · 4.14.0 (tek kok: test projesi) |
| Y5 | paket kaynagi + kilitli graf | `NuGet.config` (YENI) · 6 lock · CI `--locked-mode` |
| Y6 | imaj referanslari | dort site tag+digest · Dockerfile sdk/aspnet · compose redis/nginx |
| Y7 | action pinleri | K1'de (19 satir, 40-hane SHA) |
| Y8 | SECURITY.md esleme sayisi | 7 -> **10**, ureten ifadeyle |

## KALEM TABLOSU

| kalem | SHA | ne yapildi | kanit (ureten ifadeyle) |
|---|---|---|---|
| K1 | `b7b50a7` | 19 `uses:` satiri commit SHA pinine | build-and-test uyari 12->11 (delta tam Node20 satiri) · format-check 1->0 |
| K2 | `9e836ec` | Core.csproj'a uc surum pini | `--deprecated --include-transitive` **CriticalBugs 6 -> 0**, kaybolan 6 satir, **yeni gelen 0** |
| K3 | `f9f696a` | xunit + Testcontainers; CS0618 ayni kalemde kapatildi | uyari kodu dagilimi K2 tabaniyla **BIREBIR** (tam derleme); **uc ardisik tur** 382/382 · 730/733 |
| K4 | `38c35d6` | NuGet.config + lock + locked-mode | kaynak 2->1 · lock 6/6 · kapi ayirt-etme **exit 0 -> 1 (NU1004) -> 0** |
| K5 | `fa7ae27` | imaj tag+digest 4 site + 9 pin | pinler 9/9 |
| K6 | `ba318f5` | vendor butunluk kapisi | betik YAML'dan cikarilip kosuldu (2247 B / 61 satir), dort vaka ayirt ediyor |
| K7 | `1b282a4` | SECURITY.md 7->10 + `NuGetAuditMode=all` | pinler 12/12 · NU1903 2->3 proje |
| K7b | `32c4e7b` | DUR-3 satir yorumu | yorumsuz diff **tam 1 satir** (adim adi); 11 `run:` satiri `cmp` ile bayt-ayni |
| DUZ | `db5239b` | cekilemez digest + yanlis kok sebep | asagida |
| FIX-UP | `4976974` | alti denetci bulgusu | pin 13/13 · 8/8 job `timeout-minutes` |

### IDDIA EDILMEYEN KAZANC — ATTRIBUTION OLCULDU

Zeminde **DORT** acik paket vardi; bugun **BIR**.

```
b7b50a7 : AutoMapper 12.0.1 · SSH.NET 2023.0.0 · System.Net.Http 4.3.0 ·
          System.Text.RegularExpressions 4.3.0      (dordu de High)
9e836ec : AYNI DORT  -> K2 acik listeyi DEGISTIRMEDI
f9f696a : yalniz AutoMapper -> UCUNU K3 KAPATTI
4976974 : yalniz AutoMapper (baglayici karar 00a:87)
```

L3 denetcisi bunu **2x2 ayirt edici deneyle** ayristirdi (ESKI/YENI/ONLYXU/ONLYTC):
SSH.NET'i **Testcontainers** bump'i, iki `4.3.0` paketini **xunit** bump'i dusurdu.
K3'un iki yarisi da yuk tasiyor.

**SSH.NET GRAFTAN DUSMEDI** — 2023.0.0 -> **2026.0.0**'a yukseldi ve o surum acik degil
(L3'un kismi itirazi; "acik listesinden dustu, GRAFTAN degil"). Graftan gercekten dusen:
`System.Net.Http 4.3.0` ve `System.Text.RegularExpressions 4.3.0` (`"resolved"` sayimi 0).

**ESIK OLCULDU, TESADUF DEGIL:** SSH.NET icin yamali surum **2026.0.0**'dir;
Testcontainers 4.1.0-4.13.0 araligi 2024.x/2025.x tasir. 4.14.0'in ALTINDA kalinsaydi
advisory KAPANMAZDI.

**IKI `4.3.0` ADVISORY'SI DERLEME GRAFIGI ARTIFAKTIDIR:** `System.Net.Http.dll` ve
`System.Text.RegularExpressions.dll` net8.0 `bin`'de **YOK** (cephe paketleri);
`Renci.SshNet.dll` **VAR** (yalniz test projesinde). Kok: xunit 2.6.2'nin
`xunit.extensibility.core`'u net8.0 icin **netstandard1.1** grubuna dusup
`NETStandard.Library 1.6.1` cekiyor; 2.7.0+ `netstandard2.0` tasiyor.
Somurulebilirlik **OLCEMEDIM** (varlik yoklugu olculdu, o kadar).

## CI KANITI — PUSH-2 (SHA `4976974`)

**CIFT YESIL.** Run izleme SHA bazli (`head_sha=4976974`), nabiz 300 sn, tur basina tek
konsolide cagri.

```
33891017398  CI - Build & Test   completed  success
33891017496  Security CI         completed  success
33891143331  docker (Dependabot) completed  success   <- digest pinlerine ANINDA tepki
33891144992  nuget (Dependabot)  completed  success
33891147320  nuget (Dependabot)  completed  success
```

**BU DALGANIN RISKLI ADIMLARI — ADIM SONUCLARI:**

| adim | job | sonuc |
|---|---|---|
| **Bagimliliklari geri yukle (kilitli graf)** | build-and-test | **success** |
| **Restore (kilitli graf)** | dependency-scan | **success** |
| **Vendor butunluk kapisi** (ILK CI kosumu) | dependency-scan | **success** |
| **Gitleaks (secret taramasi)** | secret-scan | **success** (ADIM sonucundan okundu) |
| Acik bagimlilik KAPISI (uretim projeleri) | dependency-scan | success |
| Kullanimdan kalkmis paket kontrolu (BILGI AMACLI - kapi DEGIL) | dependency-scan | success |
| Bicimlendirme dogrulama whitespace / style (ZORUNLU) | format-check | success / success |
| Model ile migration'lar SENKRON mu (ZORUNLU) | format-check | success |

**TESTCONTAINERS 4.x + DIGEST'LI IMAJ — CI'DA ILK KEZ.** `OrderEndpointTests`in uc testi
yerelde Docker olmadigi icin HIC KOSMAMISTI; CI'da kostular:

| adim | job | sonuc |
|---|---|---|
| **Testler + coverage** (tam suit, uc Docker testi DAHIL) | build-and-test | **success** |
| **Entegrasyon testleri** | tests (Security CI) | **success** |
| TESHIS - basarisiz test ozeti (`if: failure()`) | her ikisi | **skipped** |

Teshis adiminin **skipped** olmasi, o job'da HICBIR testin kirilmadiginin adim-duzeyi
kanitidir. Test SAYILARI CI'dan okunamaz (CLAUDE.md 2) - sayilar yereldendir.
**Bu, dort siteye yazilan digest'in gercekten CEKILEBILDIGINI de kanitlar.**

**K1 NODE24 KANITI:** annotation'larda `Node.js 20` gecisi **0**, `deprecat` gecisi **0**
(POZ kontrol: ayni dosyalarda `"message"` alani 11 kez var, yani dosyalar BOS DEGIL).

**ANNOTATION SALINIMI — DUR YOK.** `build-and-test` 11 warning / **0 failure**:
biri `TestDbKurulum` bilgi notu ("1807 yeniden denemesi bu kosumda HIC ATESLEMEDI"),
onu zeminde de var olan CS8625 (`IEntityRepository.cs` 8 · `EfEntityRepositoryBase.cs` 2).
Kume, kayitli alti-satir salinim kumesinin DISINDA oldugu icin `dosya:satir` incelemesi +
diff kesisimi yapildi (POZ kontrollu): iki dosya da `b7b50a7..HEAD` diff'inde **0**
(POZ: `SECURITY.md` 1, `Divisima.Core.csproj` 1). Seviye `warning` -> DUR yok.
`format-check` ve `dependency-scan` annotation'lari **0/0** — format kapisi CLAUDE.md'nin
sart kostugu ANNOTATION kanalindan okundu ve TEMIZ.

## DALGA ICI DENETIM — UC DENETCI (MK-4b)

Ucu de AYRI worktree, AYRI `DIVISIMA_TEST_DB`, AYRI scratchpad; hepsi `1b282a4` uzerinde.

| denetci | karar | bulgu |
|---|---|---|
| KURAL-UYUM | 7 madde ONAY | **1 IHLAL** (MK-6) · 1 usul bulgusu · 1 onceden var olan CI acigi · 1 OLCEMEDIM |
| L3 TEKNIK | tumu ONAY | 2 kismi ITIRAZ + 1 kucuk ITIRAZ · 1 agir KOR NOKTA |
| RAPOR | **0 uydurma · 0 sayi uyusmazligi · 0 capraz celiski · 0 dokunulmaz ihlali** | 2 ITIRAZ |

**Kural-uyum kanitlari:** uretim `.cs` degisimi **0** (bes uretim projesi ayri ayri; POZ:
ayni desen test projesinde 2 dosya yakaladi) · `frontend/**` 0 · `Seller*` 0 ·
`CLAUDE.md` 0 · TFM yedi tanimin yedisinde `net8.0`, diff'te `TargetFramework` HIC gecmiyor
· SqlClient 5.1.9 / AutoMapper 12.0.1 / FluentAssertions 6.12.0 uc adi da csproj/props
diff'inde HIC gecmiyor · **MK-9: yedi commit'in YEDISI de** whitespace 0 / style 0
(`git archive <sha> | tar -x` ile tam cozum uzerinde; kapi ayirt-etme: iyi blob 0, bozuk
blob 2) · sir taramasi: insan yazimi 462 eklenen satirda 25 yuksek-entropi adayinin
tamami siniflandi, **gercek sir adayi 0**.

**IHLAL-1 (MK-6) — KAYIT.** On olcum ajani B, alti csproj/props kuru-kosum mutasyonunu
`git checkout --` ile geri almis. Kural bu mekanizmayi **KATEGORIK** yasaklar (bedeli
GF-3 ve GF-2b/F1'de iki kez odendi). Hafifleticiler olculdu: ayri worktree, ana agaca
dokunulmadi, geri almadan once `git status` okunmus ve tam olarak kendi 6 dosyasi
dogrulanmis, dosyalar ADIYLA sayilarak geri alinmis, **olculen kayip 0**. Suzgec
durustlugu: 179 kanit dosyasinda `git checkout` 1 gercek kullanim, `restore/stash/reset/
clean` 0, NEG kontrol 0.
**KOK SEBEP (bu turda teshis edildi):** ARSIV-3'te dogan MK-6 kural cumlesi CLAUDE.md
**B4'e girdi ama AJAN ORTAK-KURAL metnine GIRMEDI** — yani SDP 1.8 "kural simetrisi"
fiilen kirilmisti ve ajan kurali HIC GORMEDI. Kural metni ajan sablonuna tasinmalidir.

**L3'un agir KOR NOKTASI ve sonucu:** *"hicbir pin digest'in GERCEKLIGINI dogrulamiyor -
dort siteye de ayni UYDURMA digest yazilsaydi 12 pin de YESIL kalirdi."* Bu bosluk
kapatilirken **gercek kusur bulundu** (asagida, CC-4).

**Usul bulgusu (kural-uyum):** denetciler kosarken ana dala sekizinci commit (`32c4e7b`)
dustu; denetci onayi `1b282a4`'u kapsiyor. `1b282a4..32c4e7b` farki BU TURDA olculdu
(tek kanalli): yorumlar soyulunca **tam 1 satir** (adim adi), 11 `run:` satiri `cmp` ile
bayt-ayni.

## CC HATALARI (6)

1. **Props XML'i bozdum.** `Directory.Build.props` yorumuna `--locked-mode` yazdim; XML
   yorumu **cift tire iceremez**. Onemli olan sonuc: `dotnet restore` bu haldeyken
   **IKI KEZ exit 0** verdi, MSB4024'u HIC basmadi ve alti proje "geri yuklendi" gorundu.
   Hatayi yalniz `dotnet msbuild -getProperty` probu gosterdi.
   **KURAL:** props/targets degisikligi restore'un CIKIS KODUYLA dogrulanmaz; ozellik
   projeye ULASTI MI diye PROBLANIR (POZ: bilinen bir ozellik · NEG: olmayan ozellik -> bos).
2. **Pin capa kirlenmesi.** `Y5_NuGet_Config` asserti `<clear />` dizgesini ariyordu ve
   dizge dosyanin KENDI cok satirli yorumunda geciyordu; satir-oneki soyucusu cok satirli
   XML yorumunu siyirmadigi icin assert **BEDAVA YESIL**di. XML BLOK soyucusu yazildi.
   Mutasyonla kanitlandi: element silindiginde dizge yorumda HALA 1 kez geciyor ve pin
   yine de KIRMIZI veriyor.
3. **DUR-3'un satir yorumu atlandi** — K7b'de kapandi.
4. **[AGIR] mssql digest'i CEKILEMEZ bir degerdi; CI'yi KESIN kirardi.** MCR'nin
   `Docker-Content-Digest` basligi istenen **Accept turune gore DEGISIR**.
   `mssql/server`in manifest LISTESI YOK; yalniz liste turleri istendiginde MCR
   kullanimdan kalkmis **Schema 1** bicimine duser (`manifest.v1+prettyjws`) ve ONUN
   digest'ini dondurur.
   ```
   etiket + yalniz LISTE Accept -> sha256:0730f368...  (Schema 1)
   etiket + TEKIL Accept        -> sha256:ba4c8329...  (Schema 2)
   GET .../manifests/sha256:0730f368... -> 404  (UYDURMA bir digest ile AYNI davranis)
   GET .../manifests/sha256:ba4c8329... -> 200  (echo-back BIREBIR)
   ```
   `sdk:8.0` ve `aspnet:8.0` ETKILENMEDI (manifest LISTELERI VAR).
   **Kokum: digest'i okurken POZ/NEG kontrolu YAPMADIM** — SDP 1.7/1 tam olarak bunu
   yasaklar. Hicbir pin bunu yakalayamaz.
5. **SECURITY.md kok sebep cumlesi olgusal olarak YANLISTI** — ve bir GUVENLIK BELGESINE
   girdi. Sayi (7->10) dogru; aciklama yanlis:
   ```
   YAZDIGIM : "yalniz jenerik bicim sayilmis; Address ve Category atlanmis"
   GERCEK   : eski 7 = Address 1 + Category 1 + Collection 1 + Coupon 2 + Product 2
              Jenerik bicim TAM 5'tir (7 > 5) -> bicim korlugu KISMIYDI; Coupon ve
              Product'in jenerik-OLMAYAN yollari ZATEN sayilmisti.
              Atlanan UC kalem: Address, Category, COLLECTION guncelleme yollari
              (AddressManager.cs:43 · CategoryManager.cs:48 · CollectionManager.cs:64)
   ```
   Agirlastirici: hata, sayim hatasini duzelten commit'in KENDISINDE ve "sayi bir daha
   ezberden guncellenmesin" gerekcesiyle konan paragrafin ICINDE. Rapor denetcisi buldu.
6. **K7b oz-kontrolumde iki gevsek capa** — `package --deprecated` capasi kendi yorum
   satirimi da esledi; `|| true` sayimi dosya geneliydi (11). Ikisi de daraltildi.

**YEDINCI, KAPANIS SONRASI FARK EDILDI (duzeltilmedi — karar merkezin):**
`Directory.Build.props`taki `NuGetAuditMode` yorumu *"varsayilan SDK surumune baglidir …
olculdu, bugun ikisi de direct"* diyor. Ajan B'nin olcumu daha kesin: `all` dali
`NuGet.targets:76-78`de **TFM >= 10.0** sartina baglidir, `net8.0` oldugu icin atesLEMEZ;
SDK 8'de o dal HIC YOKTUR. Yani mekanizma TFM kapilidir, ve **SDK 8 tarafi bu makinede
OLCULEMEZ** (ajan B: "CIKARIM"; rapor denetcisi ayni yeri OLCEMEDIM olarak isaretledi).
Yorum, cikarimi "olculdu" diye sunuyor. Tek cumlelik duzeltme gerekir.

## MERKEZ PREMIS HATALARI (olculdu — sayi degil KAYIT)

1. **`Microsoft.AspNetCore.Http.Abstractions 2.2.0` HAYALET KALEM.** Alti assets
   dosyasinda 0, `--include-transitive`de 0, `*.csproj`ta 0. Tek gectigi yer
   `deps.json`daki **`8.0.0.0` CERCEVE DERLEMESIDIR** (ASP.NET Core shared framework),
   NuGet'teki kullanimdan kalkmis 2.2.0 paketi DEGIL. Iki bagimsiz ajan olctu.
   **"STILL FLAGGED" kaydi CURUDU.**
2. **ACIK PR PREMISI TAMAMEN BAYAT.** Tarif "#3-#7 actions, #11/#12 lisans" diyordu;
   gercek acik kume **#23, #24, #26, #27**. Tarifin andiklarindan HICBIRI acik degildi;
   tek kismi ortusme "actions" -> **#24** (tek PR, 7 guncelleme).
3. **DEPRECATED "KAPISI" HIC KAPI DEGILDI.** `dotnet list package --deprecated` bulgu
   VARKEN de **exit 0** dondurur (ayni sey `--vulnerable` icin de gecerli). Yani
   `|| true` hicbir sey maskelemiyordu; kaldirmak OLMAYAN bir kapiyi VAR SANDIRIRDI.
4. **VENDOR MANIFESTI ZATEN VARDI.** `frontend/vendor/README.txt` sha256 + boyut
   tasiyordu ve gercekle BIREBIR tutuyordu; K6 ikinci kopya URETMEDI, onu OKUDU.
5. **SECURITY.md'nin istenen unsurlari ZATEN VARDI** (tarihli kabul-edilen-risk kaydi,
   maruziyet analizi, uc tetikleyici, telafi edici kontrol). Kapsam elestirmeni: "tarif
   SECURITY.md okunmadan yazilmis gorunuyor". Gercek is, belgedeki **SAYIYI** duzeltmekti.
6. **AUDIT VARSAYILANI TFM KAPILIDIR, SDK KAPILI DEGIL** (bkz. CC yedinci madde).

## OLCEMEDIM (yesile boyanmadi)

- **CI SDK 8.0.x altinda lock dosyalarinin davranisi** push ONCESI olculemedi (bu makinede
  SDK 8 YOK, `global.json` DUR-2'de dusuruldu). Dalganin en buyuk kirmizi riskiydi;
  PUSH-2'de **gerceklesmedi** — her iki `--locked-mode` adimi da `success`.
- **Digest'lerin CI'da fiilen cekildigi** yalnizca "Testler + coverage" adiminin `success`
  olmasindan cikarilir; pull log'u anonim erisime kapalidir.
- **Testcontainers 4.x'in yerel calisma zamani** (Docker yok). Builder + `Build()` +
  `StartAsync` kosuyor (4.x istisna tipi firliyor), container yasam dongusu YALNIZ CI'da.
- **Iki `4.3.0` advisory'sinin SOMURULEBILIRLIGI** (varlik yoklugu olculdu, o kadar).
- **YAML ayristirmasi ilk turda olculemedi** (ortamda ayristirici yok: python/pwsh/node/
  yq/ruby yok, perl'de dort YAML modulu de yok). **Kapatildi:** NuGet'ten YamlDotNet ile
  konsol probu kuruldu; dort workflow da GECERLI, dogrulayici POZ/NEG ile ayirt ediyor
  (bilerek bozulmus kopya `SemanticErrorException` veriyor).

## KURGU ENVANTERI

**GF-4 HICBIR KURGU KAYDI URETMEDI.** MAX'lar GF-2b kapanisiyla **BIREBIR**:
musteri **171** · urun **955** · siparis **286** · adres **119** · fatura **119** ·
`user_sessions` **356** · Pending(status=0, id<=210) **35 / 9 / 210 / 3837**.

**CAPA TUZAGI — KAYIT.** Onceki muhurlerin kullandigi kanit bicimi
(`email LIKE 'gfN%' -> 0`) GF-4 icin **KIRLIDIR**: `email LIKE 'gf4%'` bugun **11** satir
donduruyor (id 55-65). Onbirinin de `created_at` degeri **25 Agustos 2026**, yani
AGUSTOS dalgasinin kurgusu; BU dalganin degil. Collation tuzagi degil
(`COLLATE Latin1_General_BIN2` ile de 11; onek disi eslesme 0).
**DURUST UREten IFADE tarih niteleyicisi ISTER:**
`SELECT COUNT(*) FROM customers WHERE email LIKE 'gf4%' AND created_at >= CAST(GETDATE() AS date);`
-> **0**.

## EMEKLI DEDEKTORLER (bu turda olculdu)

**CR (satir sonu) dedektorleri.** CLAUDE.md B5/S5 `grep -c $'\r'` bicimini zaten emekli
etmisti; bu turda IKI bicim daha emekli oldu — `od` kanitiyla CRLF oldugu KESIN olan bir
dosyada ucu de **0** dondurdu:

```
POZ dosya (od: a \r \n b \r \n)
  tr -cd '\r' | wc -c              -> 2   CALISIYOR (tek gecerli dedektor)
  awk '/\r$/{n++} END{print n+0}'  -> 0   EMEKLI
  grep -c "$(printf '\r')"         -> 0   EMEKLI
NEG dosya (saf LF): tr -cd -> 0
```

Ayrica: **`grep -P` bu kabukta CALISMIYOR** (`-P supports only unibyte and UTF-8
locales`) — tum desenler `-E` ile kurulmalidir (kural-uyum denetcisinin kaydi).

## DEPRECATED LISTESI (4 Eylul 2026 itibariyla, `--deprecated --include-transitive`)

**CriticalBugs: 0.** Kalan kayitlar ve sinif:

```
Other   : Azure.Identity 1.13.2 · Microsoft.Identity.Client.Extensions.Msal 4.67.2 ·
          System.IO.Pipelines 5.0.1 (Other,Legacy)
Legacy  : AutoMapper.Extensions.Microsoft.DependencyInjection 12.0.1 ·
          FluentValidation.AspNetCore 11.3.0 · Polly.Extensions.Http 3.0.0 ·
          SshNet.Security.Cryptography 1.3.0 · System.Collections.Immutable 6.0.0 ·
          System.Text.Json 6.0.10
```

`Microsoft.Identity.Client.Extensions.Msal 4.67.2` icin TARIHLI kayit SECURITY.md'ye
girdi: **GECISLI** (csproj'da dogrudan basvuru 0; lock `"type": "Transitive"`, POZ kontrol
`Microsoft.Identity.Client` = `"Direct"`). Islevi 4.61'den itibaren MSAL'a katildigi icin
ust surumu YOK; ust referans **EKLENMEDI** — eklemek, cozumlenen surumu zorlamadan sahte
bir "dogrudan bagimlilik" yaratirdi. Yeniden degerlendirme: `Azure.Identity` bir ust
surume ciktiginda zincir yeniden olculur.
**"CriticalBugs 0" ile "kullanim disi paket kalmadi" AYNI SEY DEGILDIR.**

## PIN ENVANTERI — `GuvenlikFix4SozlesmeTests` (13 vaka)

Hepsi **KAYNAK-SOZLESME** pinidir ve dosya bunu basinda ACIKCA beyan eder. Gerekce:
"ayni imaj dort yerde birebir" turu bir kural CALISMA ZAMANINDA gozlenemez — dordu ayri
ortamlarda kosar ve hicbiri digerini gormez.

```
Y6_SqlServer_Imaji_DORT_SITEDE_AYNI_TAG_VE_DIGEST        (Theory x4)
Y6_Dockerfile_Taban_Imajlari_DIGESTE_PINLI               (Theory x2)
Y6_Compose_Yardimci_Imajlari_DIGESTE_PINLI               (Theory x2)  <- FIX-UP
Y5_NuGet_Config_TEK_KAYNAK_ve_Devralinanlari_TEMIZLIYOR
Y5_Lock_Dosyasi_ALTI_PROJEDE_DE_VAR_ve_CI_KILITLI_MODDA_RESTORE_EDIYOR
K7_NuGet_Denetimi_GECISLI_PAKETLERI_DE_KAPSIYOR
Y8_Istemci_Girdisinden_Entitye_ESLEME_NOKTASI_SAYISI_KAYNAKTAN
Y8_ProjectTo_KULLANILMIYOR_iddiasi_HALA_DOGRU
```

**SUPERSTRING OLCUTU (kalici desen):** digest'siz dizge tam referansin ONEKIDIR, bu yuzden
duz `Contain` YANILTIR. Dogru olcut `Sayim(digestsiz) == Sayim(tam)` — "her etiket gecisini
bir digest izliyor". L3 denetcisi mutasyonla dogruladi.

**KALDIRILAN PIN (durustluk):** `Y6_Imaj_Referansi_DORT_SITEDE_DE_BIREBIR_AYNI_DIZGE`
kaldirildi. L3 olctu: `HaveCount(4)` derleme zamani sabit dizi uzerinde **TOTOLOJI**,
ikinci assert Theory'nin (a) sartinin BIREBIR kopyasi; `ci.yml` mutasyonunda Theory ile
BIRLIKTE kirildi ama `docker-compose.yml` mutasyonunda Theory kirilirken **YESIL KALDI**.
Bagimsiz ayirt-etme gucu **0** idi; "12 pin" sayisi SISIRILMISTI.

**VAKUM YASAGI TELAFISI:** `Y8_ProjectTo` artik taranan dosya sayisini da assert ediyor
(olculdu: uc projede obj/bin HARIC **332**, DAHIL **352**; esik 300, bilerek olculenin
ALTINDA — amac olagan dosya churn'unde yanlis kirmizi degil, taramanin SIFIRA dusmesini
yakalamak). Mutasyonla kanitlandi: taranan dizin `.cs` icermeyen bir dizinle degistirilince
pin KIRMIZI verdi.

**MK-6 mutasyon turlari:** ana akis 5 + L3 6 + rapor denetcisi 8 = **19 tur**; hepsinde
ISIMLI kirmizi, geri alma YALNIZ olcum yedeginden (`git checkout`/`git stash`
KULLANILMADI), her turden sonra md5 + `git status --porcelain` dogrulandi.

## BILINEN / ACIK

- **Yerel SDK 9.0.305 · CI SDK 8.0.x**, `global.json` YOK (DUR-2). Bugun ayrisma
  gozlenmedi (`--locked-mode` CI'da yesil) ama PINLENMEMISTIR.
- **Dependabot asimetrisi:** `docker` ekosistemi izleniyor ama yalniz kok
  `Dockerfile`/`docker-compose.yml` taranir. Workflow `services.*.image` ve C# icindeki
  digest'i **HICBIR ekosistem tazelemez** — o iki deger ELLE guncellenir. Bakim notu.
- **`frame-src` SUPHELISI** (GF-2b'den devir) hala acik.
- **Cikarilmis artik veritabani:** `DivisimaSemaPin_ef6d0bca` (yarim kalmis bir
  `SemaTekKaynakTests` kosumundan). Zararsiz; D-YAN temizlik kalemi.

---

**GF-4 KAPANDI — `4976974` (cift yesil: 33891017398 · 33891017496).**
Kuyrukta sirada: **GUVENLIK-AV-2** (dar olcum, ultracode YOK).
