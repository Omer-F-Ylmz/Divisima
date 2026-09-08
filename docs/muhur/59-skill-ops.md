# 59 · SKILL-OPS — IKI PROJE SKILL'I (`ops` · `vitrin`)

**Zemin:** `4fcb999` (58 · LF-5 dagitim kaydi) · **Kapsam:** yalniz `.claude/skills/` ve
`docs/muhur/`. **Kod YOK, test YOK, uretim dosyasi YOK** — docs-only dalga.

---

## 1. ENVANTER (tarif maddesi 1) — bu oturumda GORUNEN skill/eklenti

Olculdu, hatirlanmadi: `~/.claude/plugins/installed_plugins.json` +
`~/.claude/plugins/known_marketplaces.json` + `~/.claude/skills/` + `.claude/skills/`.

| Beklenen | Durum | Kanit / gerekce |
|---|---|---|
| `csharp-lsp` | **KURULU ama SKILL LISTESINDE YOK** | Onbellek agacinda yalniz `LICENSE` + `README.md` var; `.claude-plugin/plugin.json` **YOK**, `skills/` dizini **YOK**. Bu eklenti bir **dil sunucusu saglayicisidir**, skill saglamaz — LSP araci icin `csharp-ls` ayrica `dotnet tool install -g` ile kurulmalidir (README'de yazili). **Kurulum YAPILMADI** (tarif "kurulum YOK, rapor" diyor). |
| `dotnet-data` · `dotnet-aspnetcore` · `dotnet-test` · `dotnet-nuget` | **GORUNUYOR** | `dotnet-agent-skills` pazar yeri, dortu de kurulu (0.1.5 / 0.1.1 / 0.2.19 / 0.1.1). |
| `superpowers` | **GORUNUYOR** | 6.3.0; 12 skill oturum listesinde. |
| `frontend-craft` | **GORUNUYOR** | Kullanici skill'i (`~/.claude/skills/frontend-craft`). |
| `ui-ux-pro-max` | **GORUNUYOR** | Kullanici skill'i. |
| `skill-creator` | **GORUNUYOR** | `claude-plugins-official`, bu dalgada YAZIM KILAVUZU olarak kullanildi. |

**Negatif kontrol (bu dalganin tek dogal olani):** `csharp-lsp` skill listesinde **0 kez**
geciyor — yani "eklenti kurulu = skill gorunur" cikarimi YANLIS, ve skill listesi gercekten
`skills/` dizini olan eklentileri gosteriyor.

**Kurulum yapilmadi, marketplace eklenmedi, hicbir eklenti guncellenmedi.**

## 2. YAZILAN IKI SKILL

| Dosya | Bayt | Sinir | md5 |
|---|---|---|---|
| `.claude/skills/ops/SKILL.md` | **11.768** | <= 12 KB (12.288) | `49fcd6878bc40c497d12a3aa33f4b87b` |
| `.claude/skills/vitrin/SKILL.md` | **8.024** | <= 8 KB (8.192) | `104f1d8961d1668026c63654ef8fdee1` |

Ikisi de **saf LF** (`tr -cd '\r' | wc -c` -> **0**; S5 suzgeci).

**`ops`** — 57·LD-1 + 58·LF-5 + `ops/deployment-checklist.md` + `ops/backup-dr-runbook.md`
damitmasi: erisim/makine · **dagitim sirasi** (sema -> Hangfire semasi -> Redis -> `.env` ->
imaj -> origin -> frontend -> nginx -> kanit) · `.env` ve **imaj tazeligi**
(`--force-recreate` kodu TASIMAZ) · dokuz sessiz tuzak tablosu (**KnownProxies ag gecidi** ·
**Redis primitif ayrismasi** · healthcheck araci · **SMTP login != gonderen** · `MailSettings:Host` ·
Express/TDE · SIMPLE recovery · SW bump · `BackgroundJobs`) · **geri donus** · **soft-launch
kapisi / goz turu** · **acilis gunu** · kanit yazma.

**`vitrin`** — `index.html` / `admin.html` / `api-bridge.js` / `api-client.js` /
`service-worker.js` sozlesmeleri: uc bagimsiz yukleme ve `esc`/`guvenliHTML`/`guvenliYaz`
uclemesi · **`resolveUrl` TEK POLITIKA NOKTASI** (+ `admin.js`in kendi `imgUrl()` kopyasi
BILINEN olarak) · fail-closed DOMPurify · **SOZLUK DOKUNULMAZ** + ankrajli mukerrer taramasi +
`placeholder=ceviri(...)` tuzagi + MK-1 cerceve taramasi · **cift palet tema kurali** ·
**SW `VERSION` bump kurali** · dagitim yuzeyi + **Basic Auth muafiyet listesi** · kultur.

**IKISI DE `sdp`/`surec`e ATIF YAPAR, ONLARI TEKRAR ETMEZ** — push/olcum/rapor disiplini ve
denetim protokolu o iki dosyada kalir; buradaki metin yalniz *bu iki alana ozgu* sozlesmedir.
`vitrin` ayrica `frontend-craft`a isaret eder (CLAUDE.md: UI'a dokunan her iste o da yuklenir).

## 3. `surec`e TEK SATIR EK

`git diff --numstat` -> **2 / 0** (tek madde, iki satira sariyor):

```
- **Ek skill yuklemesi**: deploy/ops tarifi basinda `ops` yuklenir; `frontend/` dosyalarina
  dokunan dalgada `vitrin` yuklenir.
```

`surec/SKILL.md` yeni md5 `be544a8ae0a0f9f23797203bfd93373a` · 9.173 B.

## 4. KANIT — DIS KONTROLUNUN BU DALGADAKI KARSILIGI

Bu dalgada **pin yok** (test kodu uretilmedi), dolayisiyla assert ters cevirme YAPILAMAZ.
Yerine gecen sey **harness'in kendi kesif + servis kanali**:

| Kanal | ops | vitrin |
|---|---|---|
| **KESIF** (frontmatter ayristi, skill listesine girdi) | EVET | EVET |
| **SERVIS** (`Skill` araciyla yuklendi, govde donduruldu) | EVET | EVET |
| **NEGATIF KONTROL** | `csharp-lsp` — kurulu eklenti, `skills/` dizini yok, listede **0** | ayni |

**DURUST SINIR:** bu, skill'in *icerigi dogru* oldugunu DEGIL, *yuklenebildigini* gosterir.
Icerigin dogrulugu, damitildigi dort zemin belgeye karsi okunarak saglandi (bolum 5/1).
Skill metninin bir sonraki dalgada gercekten ise yaradigi ancak o dalgada olculebilir.

## 5. DALGA ICI DENETIM

### 1. KALEM KALEM

| Kalem | Ne olculdu | Kanit | Neyi koruyor |
|---|---|---|---|
| Envanter | `installed_plugins.json` (9 eklenti) · `known_marketplaces.json` (2 pazar) · iki skill dizini · `csharp-lsp` agac dokumu | dosya icerikleri okundu | — (salt rapor) |
| `ops/SKILL.md` | 11.768 B · CR 0 · kesif+servis | `wc -c` · `tr -cd '\r'` · `Skill` yuklemesi | md5 muhurde |
| `vitrin/SKILL.md` | 8.024 B · CR 0 · kesif+servis | ayni | md5 muhurde |
| `surec/SKILL.md` eki | +2/-0 satir | `git diff --numstat` | yeniden yuklendi, kesif tekrarlandi |
| Sir taramasi | iki skill'de jeton bicimli deger **0** | `grep -oE "[A-Za-z0-9+/=_.:-]{16,}" \| grep [0-9] \| grep [a-z]` -> yalniz **iki dosya yolu** (`docs/muhur/57-...`, `58-...`) | gitleaks kapisi |
| IP taramasi | tek IP literali **`127.0.0.1`** (loopback, sir degil); sunucu IP'si ve ag gecidi degeri **YAZILMADI** | `grep -nE "([0-9]+\.){3}[0-9]+"` | "deger yok, yalniz usul" olcutu |
| Pin carpismasi | `.claude/skills` ya da `SKILL.md` tarayan test **0** | `grep -rn` `Divisima.IntegrationTests/` | mevcut suit etkilenmiyor |

**Kaniti olmayan satir YOK.** "Degistirdim ama surmedim" durumu YOK.

### 2. YARIM KALAN VAR MI

**YOK.** Tarifin dort maddesi (envanter · iki skill · `surec` eki · muhur+commit) tamam.
Tarifin ISTEMEDIGI ve BILEREK YAPILMAYANLAR: `skill-creator`in eval/benchmark dongusu
kosulmadi (docs-only tek push tarifiyle ve "Agent kullanma" kisitiyla celisirdi; kilavuzu
YAZIM kurallari icin kullanildi), aciklama optimizasyonu (`run_loop.py`) kosulmadi,
hicbir eklenti kurulmadi/guncellenmedi.

### 3. YAN ETKI TARAMASI

- `.claude/skills/` **yeni bir yuzey degil** — `sdp` ve `surec` zaten oradaydi; iki dizin
  eklendi, mevcut ikisine dokunulmadi (`sdp` md5 degismedi, diff'te YOK).
- **`surec` tuketicileri:** her dalga tarifi. Eklenen satir bir **kisit degil yonlendirme**;
  mevcut hicbir maddeyi degistirmiyor, sirasini bozmuyor.
- **CI:** bicim kapilari `dotnet format Divisima-Backend.sln` kosuyor — markdown **kapsam
  disi**. `secret-scan` (Gitleaks) depo geneli; sir taramasi yukarida.
- **Depoya giren dosyalar:** yalniz `.claude/skills/{ops,vitrin}/SKILL.md` +
  `.claude/skills/surec/SKILL.md` + `docs/muhur/59-skill-ops.md` + `docs/muhur/INDEX.md`.
  **Calisma agacindaki `.agents/` ve `AGENTS.md` bu dalgadan ONCE de takipsizdi ve
  STAGE EDILMEDI** (oturum basi `git status` kaydi bunu gosteriyor).

### 4. KENDI HATALARIM

1. **`Get-ChildItem`siz bir `find` ile `csharp-lsp` agacini tararken `.in_use/` PID
   dosyalari ciktiyi doldurdu ve komut exit 1 verdi** (`plugin.json` yok -> `cat` dustu ->
   zincir kirildi). Ilk okumada "eklenti bozuk" diye yorumlamaya yaklastim; ikinci, filtreli
   tarama gercek olguyu verdi: **eklentinin skill'i yok, bozuk degil.** Yanlis teshis
   RAPORA GIRMEDI cunku ikinci kanal kosuldu.
2. **"Brevo login" kalemini once yazacak kanit bulamadim.** Depoda `Brevo` yalnizca iki
   yerde geciyor ve ikisi de saglayici adini anmaktan ibaret — bir "login tuzagi" kaydi
   YOK. Uydurmak yerine olculebilen sey yazildi: `docker-compose.prod.yml`de
   `DIVISIMA_SMTP_USER` **gonderen adresine varsayilan aliyor**, yani login != gonderen olan
   saglayicilarda `.env`de ACIKCA verilmelidir. Iddia compose satirina dayanir, hatiraya
   degil; "Brevo boyle davranir" cumlesi **kullanicinin saha bilgisidir ve skill'de
   saglayici-genel bicimde yazildi.**
3. **Ilk plan `csharp-ls` kurulumunu dogrulamaya gidiyordu**; tarif "kurulum YOK" dedigi
   icin durdum. Kurulu olup olmadigini da OLCMEDIM — olcum kendi basina zararsizdi ama
   rapora deger katmazdi, kayit olarak burada duruyor.

### 5. PIN DURUSTLUGU

**Bu dalga PIN URETMEDI ve uretmemeliydi:** korunacak sey bir *davranis* degil bir *belge*.
Yerine gecen kanit (kesif + servis + negatif kontrol) bolum 4'te ve **sinirlari yazili**.
Bir skill dosyasinin varligini/boyutunu pinleyen bir test yazmak kolay olurdu ama
**kaynak-sozlesmesinin en zayif turu** olurdu: dosyanin *icerigi* bozulsa bile yesil kalirdi.
Yazilmadi; gerekce bu satirdir.

### 6. BOZDUKLARIM

**HICBIR PIN KIRILMADI** — dokunulan uc dosyanin (iki yeni skill + `surec`) hicbirini
tarayan bir test yok (bolum 5/1 taramasi). Silinen/degistirilen davranis yok; `surec`e
yalniz EKLEME yapildi, mevcut madde metinlerine dokunulmadi.

---

## 6. DAGITIM DURUMU

Bu dalga **canli sunucuya dokunmaz**; `.claude/skills/` ve `docs/` uretim artefakti degildir.
Sunucudaki durum `57·LD-1` + `58·LF-5`teki gibi kalir (soft-launch kapisi ACIK, hukuki
metinler 4/6, `AdminSeed` acik).
