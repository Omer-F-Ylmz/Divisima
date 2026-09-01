# 43 · ARSIV-2 MUHRU — PROSEDURLER SKILL'E (SDP + SUREC)

**Zemin:** `4c29f32` · **Tur turu:** DOCS-ONLY (uretim kodu DEGISMEDI) · **Tarih:** 2026-09-01
**Commit'ler:** `669210e` (C1) · `5c30ab4` (C2) — tek push · **MK-11 c usulu**
**Sonuc:** CLAUDE.md **81.057 B -> 53.200 B** (1.426 -> 982 satir), esik 81.920, **PAY 28.720 B**

---

## 0. HEDEF ve SONUC

CLAUDE.md'nin acilis yukunden iki PROSEDUR blogu cikarildi ve Claude Code **proje skill'i**
oldu; dalga tarifiyle yuklenir. Bayt kaybi YOK, metin DEGISMEDI.

```
B2  SDP v1.3   zemin satir 255..591          19.792 B -> .claude/skills/sdp/SKILL.md
SUREC          zemin satir 619..738 EKSI 732-733  8.902 B -> .claude/skills/surec/SKILL.md
B3'un DALGA ICI DENETIM kismi (zemin 592..618, 1.482 B) CLAUDE.md'de KALDI (bayt-ayni)
```

---

## 1. FAZ A — OLCUM (dosya degismedi)

### A1 — `.claude/` dizini
`.claude/` VAR; icinde YALNIZ bos `worktrees/`. Dosya 0, izlenen 0.
`.gitignore` `.claude/` ya da `.claude/skills`'i **dislamiyordu**
(`git check-ignore` BOS; POZ kontrol `bin/x.dll` -> `.gitignore:8` ile dedektor dogrulandi).
CI'da `.claude/` okuyan adim **YOK**: `grep -rho '\.claude' .github/workflows/ | wc -l` -> **0**;
suzgec POZ (`dotnet` ci.yml 20 · `uses:` security.yml 10) / NEG (`ZZZdotnet` 0) ile sinandi,
yani "0" YALANCI DEGIL.

**RISK (tarifte yoktu, FAZ A'da bulundu, merkez onayladi):** `.claude/worktrees/` ignore
EDILMIYORDU. Bugun bos oldugu icin agac temizdi; ama worktree kullanan **her** denetim
dalgasi (MK-4b) tur ortasinda `?? .claude/` uretip **"agac 0" kapisinda YANLIS ALARM**
verir. AV-1'in kural-uyum denetcisi de not etmisti. FAZ B `.claude/skills/**`'i izlenir
hale getirdigi icin bu kalicilasacakti -> `.gitignore` satiri C1'de eklendi.

**Lafzi celiski (kapandi):** MK-11 a) *"Bu dosyalar ... `.claude/` altina tasinmaz"* der;
baglami **arsiv/muhur dosyalaridir**, skill degil. MK-12 metni bu ayrimi ACIKCA yaziyor.

### A2 — SKILL KESFI: **NEGATIF -> merkez karariyla ERTELENDI**
`.claude/skills/deneme/SKILL.md` (242 B, frontmatter + tek satir imza `ARSIV2-A2-IMZA-7Q4KZ`)
olusturuldu. `Skill(skill="deneme")` -> **"Unknown skill: deneme. Did you mean rename?"**
Karar kriteri (govde donerse kanit) -> **DONMEDI**.
**AYIRT EDICI GOZLEM:** arac bir REGISTRY'ye karsi cozumleme YAPTI ve baska bir ad ONERDI
(`rename`) -> **mekanizma CALISIYOR, kayit YOK**. Yani kusur BICIMDE degil, KAYIT ANINDA:
proje skill'leri **oturum basinda** taranir.
`ListSkills` -> `{"results":[]}` (o arac claude.ai skill'lerini listeler, proje skill'lerini
DEGIL; tek basina kanit sayilmadi). Makinede `~/.claude/skills` YOK.
Dosya SILINDI, agac 0.
**MERKEZ KARARI:** kesif olcumu bu oturumda YAPILMAZ; **GUVENLIK-FIX kapisinda** yapilir.

### A3 — Araliklar, bayt, atiflar
```
B2    zemin satir 255..591 · 337 satir · 19.792 B · CR 0 · BOM YOK (23 20 53)
B3    zemin satir 592..738 · 147 satir
SUREC zemin satir 619..738 · 120 satir ·  9.048 B · CR 0 · BOM YOK (23 23 20)
```

> **FAZ A'NIN ANA BULGUSU — AV-1'de isaretci YANLIS BOLUME KONMUSTU.**
> `1.12 GUVENLIK` isaretci satiri zeminde **CLAUDE.md:732-733**, yani **SUREC ICINDE**;
> **B2'de (255..591) DEGIL**. Merkez AV-1'de "B2 sonuna" demisti; capa olarak secilen
> `- **Uretim kodu**:` satiri (729) **SUREC'in son maddesidir**.
> DOGRULAMA: ayni capa B2 araliginda **0**, SUREC araliginda **1** kez geciyor.
> AV-1 dogrulamam yalnizca `grep -c '1.12 GUVENLIK modulu' = 1` idi — **NEREDE** oldugunu
> SORMADI. Bu, **"sayim dogru, konum yanlis"** sinifinin ilk adlandirilmis ornegidir.
> **Merkez karari:** isaretci SUREC'ten cikarilir, B2 stub'inin 3. maddesi olur.
> SUREC govdesi: isaretci DAHIL 9.048 B / 120 satir · **HARIC 8.902 B / 118 satir** (146 B).

**REPO ATIF TABLOSU — DEGISTIRILMEDI:**
| dosya | atif | karar |
|---|---|---|
| `.gitleaksignore:32` | "force-push YASAK (CLAUDE.md SUREC)" | DEGISTIRILMEZ |
| `docs/muhur/41-arsiv-1.md:129` | EK-3: "CLAUDE.md SUREC" 1 atif | ARSIV KAYDI |
| `docs/muhur/*.md` (12) + `INDEX.md` | "SDP" gecisi (muhur metinleri) | ARSIV |
| `Divisima.IntegrationTests/TestDbKurulum.cs:50` | "CLAUDE.md 6c" -> B1, SUREC DEGIL | ILGISIZ |

Suzgec: POZ `MK-11` 4 dosya · NEG `ZZZSDP` 0 dosya.
**EK-3'un "CLAUDE.md SUREC 1 atif" beyani DOGRULANDI** (tek uretim-disi atif
`.gitleaksignore:32`).

### A4 — B0 haritasi, MK-11, MK numarasi
Harita satir 22-24. MK-11 govdesinde (a-e) B2/B3/SDP/SUREC'e **dogrudan atif YOK** — tek
atif harita satiridir, yani delta yuzeyi DAR.
**Mevcut celiski (bu turda kapandi):** B4 basligi "MK-1..**MK-10**" ↔ harita
"B4 MK-1..**MK-11**".
**MK NUMARASI OLCULDU:** MK-9 4 · MK-10 4 · MK-11 6 · **MK-12 0** · MK-13 0 ·
MK-4a 3 · MK-4b 6 (harfliler tam sayi TUKETMEZ). **Siradaki tam sayi MK-12** — merkez
beklentisiyle ORTUSUYOR.
**NEG capa kirlenmesi (yeni tuzak):** `MK-99` -> **1**, 0 DEGIL. Gerekce OLCULDU:
CLAUDE.md'nin MK-10 blogu **kendi negatif-kontrol cumlesini metin olarak tasiyor**
("NEG kontrol: `MK-99` 0 gecis"). Belge kendi suzgecini METINLESTIRMIS. Temiz NEG capasi
`MK-77` (0). Karari degistirmedi.

---

## 2. FAZ B — UYGULAMA

### C1 — `669210e` "ARSIV-2/C1 SDP + SUREC skill'e, worktrees ignore"

**Aralik teyidi (C1 ONCESI):** S2=255 · S3=592 · SUR=619 · B4=739 · isaretci=732 —
FAZ A degerleriyle BIREBIR, kayma YOK.

**Skill dosyalari:**
```
.claude/skills/sdp/SKILL.md    19.910 B / 342 satir  (frontmatter 118 B + BAYT-AYNI govde)
.claude/skills/surec/SKILL.md   9.044 B / 123 satir  (frontmatter 142 B + BAYT-AYNI govde)
ilk satir ikisinde de `---` · CR 0 · BOM YOK (2d 2d 2d)
```

**KANIT — cmp:** frontmatter 5 satir atlanarak govde <-> zemin araligi:
**sdp 0 FARK · surec 0 FARK**.
`surec` govdesi <-> orijinal `619..738` diff: **TAM 2 satir**, ikisi de 1.12 isaretcisi.
1.12 satirlari **KAYNAKTAN cikarildi, elle YAZILMADI** (`sed -n '732,733p'` + `cmp` -> birebir);
gerekce: bu depoda kacis-kaybi ailesinin 6 vakasi var.

**KANIT — bayt denklemi:**
```
81.057 - 19.792 (B2) - 8.902 (SUREC) - 146 (isaretci) + 320 (B2 stub) + 70 (SUREC stub)
= 52.607   ->  OLCULEN 52.607 B   ->  ARTIK 0
satir 1.426 -> 974
```

**CLAUDE.md TEK awk gecisiyle yeniden yazildi** (iki aralik AYNI ANDA). Iki ayri duzenleme
yapilsaydi ikinci aralik KAYARDI; bu yuzden tek gecis secildi. Cikti gecici dosyaya yazildi,
denklem tutunca tasindi (CLAUDE.md kalici kurali: dosya kendi iceriginden turetilerek ustune
YAZILMAZ).

**Stub'lar ve KONUM dogrulamasi (bu dalganin ANA DERSI):**
B2 stub 4 satir / 320 B (3 madde: skill isaretcisi · MK-12 yukleme satiri · **tasinan 1.12
satiri**) · SUREC stub 1 satir / 70 B.
**KONUM POZITIF DOGRULANDI:** B2 stub basligi 263, isaretci 265, DALGA ICI DENETIM 267 ->
`263 < 265 < 267`, isaretci **stub blogunun ICINDE**. Mukerrer yok: isaretci sayimi
yeni CLAUDE.md 1 · `sdp` 0 · `surec` 0.

**`.gitignore` — merkez onayli config istisnasi:**
```
# ARSIV-2 (merkez onayli config istisnasi): denetim worktree artiklari agac-0 kapisini
# kirmasin. .claude/skills/** IZLENIR - bilerek DISLANMADI.
.claude/worktrees/
```
KANIT: POZ `git check-ignore -v .claude/worktrees/x` -> `.gitignore:146:.claude/worktrees/` ·
NEG `git check-ignore -v .claude/skills/sdp/SKILL.md` -> **BOS** (izlenir).

**Kapsam:** 4 dosya (`CLAUDE.md` · `.gitignore` · iki `SKILL.md`), kod uzantili dosya **0**.

### C2 — `5c30ab4` "ARSIV-2/C2 B0 + MK-12"

B0 haritasinda **dort ifade** guncellendi (POZ 1/1/1/1, NEG 0/0):
`"B2 SDP v1.3 -> skill \`sdp\`"` · `"DALGA ICI DENETIM (bayt-ayni) + SUREC -> skill \`surec\`"` ·
`"B4 MK-1..MK-10 (MK-11/12 B0'da)"` · `"B1 ve DALGA ICI DENETIM kaynagindan BAYT-AYNIDIR"`.
**YAN KAZANC:** A4'te kaydedilen B4-basligi ↔ harita celiskisi bu duzeltmeyle **KAPANDI**.

MK-11'e **f) = MK-12 PROSEDUR SKILL'LERI** eklendi (merkez metni). MK-11 a)'nin
`.claude/` yasagini ACIKCA `docs/muhur/` ile sinirliyor -> A1'deki lafzi celiski KAPANDI.

CLAUDE.md 52.607 -> **53.200 B** / 982 satir · **PAY 28.720 B** · CR 0 · fence 22 (cift).

---

## 3. DENETIM — TEK DENETCI, AYRI WORKTREE: **ONAY (6/6)**

| Kalem | Karar | Denetcinin KENDI olcumu |
|---|---|---|
| M1a govde<->aralik (sdp) | **ONAY** | `cmp` **exit 0**, iki taraf 19.792 B / 337 satir |
| M1b surec farki | **ONAY** | `diff` tek hunk, **TAM 2 satir**, ikisi de 1.12 isaretcisi |
| M1c frontmatter | **ONAY** | ikisi de yapisal gecerli (`---` / name / description / `---` / bos satir) |
| M2 bayt denklemi | **ONAY** | `52607 + 19792 + 8902 + 146 - 320 - 70 = 81057` -> **ARTIK 0** |
| M3 kod diff 0 | **ONAY** | kod uzantili **0**; kume TAM 4 dosya (fazla/eksik yok) |
| M4 agac + ignore | **ONAY** | POZ eslesti / NEG eslesmedi; `.claude/` altinda TAM 2 dosya |
| M5 icerik butunlugu | **ONAY** | CR 0 · BOM yok · **fence korunumu 24 = 22 + 2 + 0** · konum pozitif |
| M6 C2 deltasi | **ONAY** | POZ 4/4 · NEG 0/0 (zemin kontrollu) · MK-12 dogru numara |

**Denetcinin EK olcumu (istenmemisti, kendisi ekledi):** B0 haritasinin *kendi* iddiasini
sinadi — zemin `592..618` vs yeni `267..293`, `cmp` **exit 0** (1.482 B) ->
**DALGA ICI DENETIM gercekten BAYT-AYNI**.

**Denetcinin iki PLAN-SAPMASI ve telafisi (durust kayit):**
1. **Worktree ZEMIN commit'inde acilmis** (`4c29f32`), dalga commit'inde degil. Telafi:
   hicbir checkout yapilmadi, **tum olcumler blob'dan** (`git show <sha>:<yol>`) uretildi —
   MK-4b'de celiski avcisinin kullandigi ayni desen.
2. **`check-ignore`'u kendi worktree'sinde kosmak GECERSIZDI** (o worktree'nin `.gitignore`'unda
   kural YOK -> POZ girdi yanlis negatif dondu). Telafi: kural **izole gecici repoda**
   sinandi, proje agacina DOKUNULMADI.
   *Bu, "suzgeci dogru zeminle sinamak" dersinin yeni bir vakasidir.*

### DENETCININ BULGUSU
**BULGU-D1 `[KOZMETIK/MANTIK]` `[LATENT]`** — `sdp/SKILL.md` description'i **SDP
kisaltmasini YANLIS aciyor**:
```
description : "Divisima Sorun Dogrulama Protokolu v1.3 — ..."
govde basligi: "# SDP — SAHADA DOGRULANMIS DENETIM PROTOKOLU v1.3"
```
Description **kesif metnidir** (MK-12 buna dayanir); acilim depoda hicbir yerle ortusmuyor.
**Ana akisin bagimsiz teyidi:** `"Sorun Dogrulama"` skill disinda **0 dosyada**,
`"SAHADA DOGRULANMIS"` **1 dosyada** (skill govdesinin kendisi) geciyor.
**KOK SEBEP:** acilim **merkez tarifinde** boyle yaziliydi ve ana akis onu **birebir**
uyguladi. Olcumu engellemedi -> ITIRAZ DEGIL, BULGU. **Duzeltme karari MERKEZIN.**
Onerilen duzeltme (tek satir): `description: Divisima SDP v1.3 (Sahada Dogrulanmis Denetim
Protokolu) — her dalga/denetim tarifi basinda yuklenir`.

### DENETCININ GOZLEMLERI (kusur degil, kayit)
- **G1 OK BICIMI TUTARSIZ:** B0 haritasi ASCII `->`, stub BASLIKLARI Unicode `→`. Islevsel
  etki yok; capa yazarken tuzak (MK-7).
- **G2 SARKAN CAPRAZ ATIFLAR:** CLAUDE.md'de govdesi ARTIK skill'de olan uc atif kaldi —
  satir 349 "(SDP 1.9)", 573 "SDP 1.7/1", 674 "SDP 1.7/2". MK-12 yuklemeyi zorunlu kildigi
  icin **tasarim geregi**; ama CLAUDE.md TEK BASINA bu numaralari cozemez.
- **G3** Stub satirlarindan sonra bos satir yok; ATX baslik paragrafi kestigi icin
  **render sorunu YOK**.

### DENETCININ KOR NOKTALARI ve ANA AKISIN TELAFISI (SDP 1.11.10-d)
| # | Denetcinin "olcemedim"i | Ana akis ne yapti |
|---|---|---|
| KN1 | `5c30ab4`un **disk checkout'u** olculmedi (yalniz commit icerigi) | **KAPATILDI:** dort dosyanin `git hash-object` degeri `5c30ab4` blob'lariyla **ESIT**; dedektor POZ mutasyonla sinandi (SAPAN dondu) |
| KN3 | **Ana agac** `git status` harness tarafindan reddedildi | **KAPATILDI:** ana akis olctu -> **0 satir**, HEAD `5c30ab4`, dal `main`, tek worktree |
| KN2 | **Skill kesfi** olculmedi | **ACIK — merkez karariyla GUVENLIK-FIX kapisina ertelendi** (A2 negatifti) |
| KN4 | CI/run kaniti yoktu (push edilmemisti) | **KAPATILDI:** asagi, bolum 4 |
| KN5 | **Semantik denetim yapilmadi** — zeminde yanlis olan sey skill'de de yanlistir | **ACIK ve ADIYLA DURUYOR** (BULGU-D1 tam da bu sinifin ornegi) |
| KN6 | `.gitignore` kurali izole repoda sinandi | **ACIK, risk DUSUK** (yeni kural dosyanin son satiri, sonraki negasyon yok) |

---

## 4. PUSH ve RUN KANITI

```
tek push : 4c29f32..5c30ab4  (iki commit: 669210e + 5c30ab4)
run 33554170213  CI - Build & Test  completed / success   (build-and-test · format-check)
run 33554170113  Security CI        completed / success   (codeql · dependency-scan ·
                                                            tests · secret-scan)
total_count 2 · job duzeyi failure 0 · cancelled 0 · NEG kontrol "ZZZfailure" 0
```
**Kalici kurallar dogru kanaldan okundu:**
`secret-scan` **ADIM SONUCUNDAN** -> `Gitleaks (secret taraması)` = **success**
(annotation'dan DEGIL — Gitleaks bulgusunu `warning` seviyesinde basar).
`format-check` **ANNOTATION'DAN** -> **failure seviyeli annotation TOPLAM 0**
(job sonucundan DEGIL — `continue-on-error` job'i yesil gosterir).
**Annotation salinimi:** 39 `warning` (AV-1'de 26 idi), hepsi bilinen kume.
**Diff kesisimi YAPISAL OLARAK BOS:** `git diff --name-only 4c29f32..5c30ab4` -> dort dosya,
**kod uzantili 0**. Izleyici adabi: nabiz 320 sn (>=300), tur basina TEK konsolide cagri.

---

## 5. CC HATALARI (ana akis — durust kayit)

1. **AV-1'DEN DEVRALINAN KONUM HATASI (bu turda bulundu ve duzeltildi).** 1.12 isaretcisi
   B2'ye degil SUREC'e konmustu. Kok sebep: capa (`- **Uretim kodu**:`) **iki bolume de
   yakin gorunuyordu** ve AV-1 dogrulamasi yalnizca **SAYIM** yapti (`grep -c = 1`),
   **KONUM** sormadi. -> **B6 dersi olarak yazildi.**
2. **NEG capa kirlenmesi:** `MK-99` NEG kontrolu **1** dondu, cunku CLAUDE.md MK-10 blogunda
   kendi NEG kontrol cumlesini **metin olarak** tasiyor. Karari degistirmedi ama NEG capasi
   `MK-77`'ye tasindi. -> **B6 dersi olarak yazildi.**
3. **Merkez tarifi "B2 sonuna" derken CLAUDE.md'de `# B2` BASLIGI YOK** (B1/B2/B3
   basliksizdir). AV-1'de harita okunarak cozulmustu; ARSIV-2'de bu, hatanin **kok
   sebebinin bir parcasi** olarak dogrulandi.
4. **BULGU-D1'in kaynagi merkez tarifidir**, ana akis birebir uyguladi. Kayit: bir tarif
   metnindeki olgusal hata, "aynen uygula" disipliniyle **capraz kontrolsuz** gecebiliyor.
5. **A2 negatifi tarifin ONGORDUGU bir daldi** ve merkez karariyla ertelendi — sapma degil,
   plana uygun.

---

## 6. BU TURUN KALICI KAZANIMLARI

- **MK-12** (prosedur skill'leri) B0/MK-11'e f) maddesi olarak girdi.
- **B6'ya iki ders:** capa POZ olcumu "kac" yaninda **"NEREDE"** sorar · **NEG capa dizesi
  belgeye yazilmaz**.
- `.gitignore`'da `.claude/worktrees/` — MK-4b dalgalarinin "agac 0" kapisi artik yanlis
  alarm uretmez.
- B4 basligi ↔ B0 haritasi celiskisi kapandi.
- CLAUDE.md acilis yuku: **81.057 -> 53.200 B**; iki ARSIV turunun toplam etkisi
  **747.240 -> 53.200 B**.

**ACIK KALAN:** skill KESFI (`Skill` tool'unun `sdp`/`surec`i gormesi) **OLCULMEDI** —
GUVENLIK-FIX kapisinda olculecek. Olculene kadar MK-12'nin `Read` fallback'i gecerlidir.

---

## 7. EK — MUHUR FAZINDA BULUNAN CC HATASI (6.) ve DUZELTMESI

**Ders metni KENDI kuralini ayni cumlede IHLAL ETTI.** B6'ya eklenen ikinci ders
("NEG capa dizesi belgeye yazilmaz") ilk yaziminda **yedek capanin adini metne koydu**.
Olcum yakaladi: delta sonrasi dogrulamada `MK-77` taramasi **0 yerine 1** dondu.
Yani ders, tarif ettigi kirlenmeyi **uygularken uretti** — `MK-99`'un basina geleni
yedek capaya da yapmis oldu.

**Duzeltme:** ders metni capa ADI TASIMAYACAK sekilde yeniden yazildi; capa adlari yalniz
BU MUHURDE durur (`43·ARSIV-2 · CC HATALARI`), ve derse **"kural KENDINE DE ISLER"**
cumlesi eklendi. Kirlenen capalar (kayit, CLAUDE.md'ye GIRMEZ): ilk NEG capasi MK-10
blogunun kendi alintisiyla, yedek capa ise bu dersin ilk yazimiyla kirlendi.
**Bundan sonra her tur NEG capasini KULLANMADAN ONCE olcer** — capa listesi tasimak
yerine olcum yapmak tek guvenli yoldur.

**SINIF:** bu, "belgenin kendi suzgecini metinlestirmesi" ailesinin **ikinci** vakasidir
ve ikisi de AYNI TURDA olculdu. Ailenin adi: **kendine gonderme yapan capa kirlenmesi.**
