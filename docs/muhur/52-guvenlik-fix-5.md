# 52 · GÜVENLİK-FIX-5 — A09 İZ/ATIF + MİSAFİR BÜTÜNLÜĞÜ + MASKE

**Zemin `b90805c` (51·AV-2 mührü) → `027a88a`. Beş commit, 29 dosya. LAUNCH ÖNCESİ TEK DALGA.**
İki LAUNCH BLOKER'ın ikisi de kapandı (SD-7 misafir bütünlüğü · SC-1 A09 iz/atıf).

---

## 1. ÖN ÖLÇÜM — İKİ DUR RAPORU

Altı ajan (A · B · C · D · E + kapsam eleştirmeni) dağıtıldı; **salt ölçüm**, kod yazılmadı.
Ajan C ve kapsam eleştirmeni **DUR** çıkardı; merkez on iki kararla çözdü (bölüm 2).

### DUR RAPORU 1 — Ajan C (misafir yolu)

- **Tarif ↔ kod çelişkisi:** tarif *"full_name 120 üye ile"* diyordu; **`GuestCheckoutDto`'da
  `full_name` alanı YOK**. Misafirdeki karşılık `guest_name` ve **tek değer İKİ kolona**
  yazılıyor: `customers.name` (NVARCHAR MAX) ve `addresses.full_name` (150). Ad çelişkisi,
  politika çelişkisi değil. **Migration GEREKMEZ** (120 < 150, 200 = 200).
- **`Guid.TryParse` üç şeyi kırıyor:** `MisafirCheckoutTests` GF-3/K12 replay pini ·
  `frontend/api-bridge.js` yedek dalı `co-<ts>-<8kar>` (GUID DEĞİL, `FrontendDokunmaHedefi`
  ile **bilinçli korunuyor**, frontend DOKUNULMAZ) · DB'de dolu 122 `request_id`'nin **54'ü
  GUID değil** ve GUID-dışı olanın en yenisi GUID olanınkinden DAHA YENİ.
- **SD-7 zinciri canlı kapandı:** `guest_name` 151 → HTTP 500, yetim müşteri **id 179**, iki
  yanlı kanıt (yanmış e-posta 409 vs temiz e-posta 400). **BİLİNÇLİ SINIR 3 ilk kez ölçüldü**
  (telafi çalışan dalda 1 ölü outbox satırı kalıyor).
- `:503-504` sarmalaması iç içe transaction ÜRETMİYOR; `AccountManager` aynı kalıbı bugün
  üretimde koşuyor. **Şart:** sarmalama mevcut `catch` İÇİNDE olmalı.

### DUR RAPORU 2 — Kapsam eleştirmeni (tarifin kendisinin açacağı kapı)

- **K7 imza premisi:** üç bağımsız kayıt sağlayıcının imza GÖNDERMEDİĞİNİ söylüyor; canlı
  repro'da **boş `X-Iyz-Signature` başlığı** (sağlayıcının gerçek biçimi) imza dalını
  ATLIYOR. K7 uygulansaydı **tüm callback+webhook 400** olurdu ve sipariş #33 kesintisi
  (para Iyzico'da SUCCESS, bizde Pending) geri gelirdi. AV-2 bunu **SE-5 / DÜŞÜK /
  "ilgisiz (belge)"** diye sınıflamıştı — tarif bir BELGE bulgusunu üretim davranışına
  yükseltiyordu.
- **`EnsureOwner` YOK** — bilinçli silinmiş, karar iki yerde kayıtlı; sahiplik-404 yüzeyi
  yedi manager'a dağılmış, biri DOKUNULMAZ `InvoiceManager`.
- **403 katman engeli** (`Divisima.Core` ProjectReference 0) · **`OnRejected` 0 geçiş** ·
  **webhook allowlist dalı ULAŞILAMAZ** (`AllowedIps: []`, `00b:229` bağlayıcı).
- **SC-12 tarifte YOK** (outbox düz jeton) · **SE-3 tarifte YOK**.
- **İki merkez sayısı yeniden üretilemiyor** (bölüm 9).

---

## 2. MERKEZ KARARLARI (12)

| # | Karar |
|---|---|
| D1 | **K7 KOD DÜŞER.** SE-5 → KABUL EDİLMİŞ RİSK: kimlik doğrulama = token ile retrieve otoritesi. İmza VARSA ve bozuksa 400 sürer (statüko) + bu dal OLAY yazar. |
| D2 | `request_id` kapısı: uzunluk ≤80 + `[A-Za-z0-9._-]`; **GUID şartı YOK**. |
| D3 | `guest_name` hedef 100 (`customers.name` üye ölçütü), ölçüm **sanitize sonrası**; diğer misafir alanları ilgili üye validator sınırlarıyla; **ortak RuleBuilder AÇILMAZ** — kopya değil aynı sabitlere referans. |
| D4 | Sahiplik olayı **Order + Payment** ile sınırlı; `EnsureOwner` GERİ GELMEZ; kalan yedi nokta BİLİNEN. |
| D5 | **403 olayı DÜŞER** (katman engeli) → BİLİNEN gerekçeli. Yeni yüzey YOK. |
| D6 | 429 olayı Redis middleware reject dalında; `customer_id` NULL KABUL; **metot enjeksiyonu**; örnekleme `TryAddAsync(ip+uç, 60 sn)`; severity Warning. Atıf yarısı kapanmıyor — kayıt. |
| D7 | Webhook allowlist + satıcı login → matriste **"YAPISAL ULAŞILAMAZ / Seller 0 satır"**, kapandı YAZILMAZ. S-C hedefi revize: **8 H → 3 H**. |
| D8 | K1 doldurma **`SecurityEventManager` İÇİNDE**; imza ve 7 çağrı yeri DEĞİŞMEZ; IP 60'a çekilir. |
| D9 | K6 = **`ITextFormatter` maske sarmalayıcı** (yeni paket yok). GF-3 pini KALIR; kapsam yorumu revize. |
| D10 | `KanitMaskesi` **GENİŞLETİLMEZ**; ayrı `LogMetniMaskesi`. |
| D11 | SC-12 bu dalgada **yalnız saklama**; payload şifreleme → GF-6. **K9** olarak eklendi. |
| D12 | SE-3 K5'e: müşteriye giden metin sabit, teknik ayrıntı yalnız maskeli logda. |

---

## 3. KALEMLER (K7 DÜŞTÜ)

| Kalem | Ne yapıldı |
|---|---|
| **K1** | Doldurma `SecurityEventManager` içinde; imza ve yedi çağrı yeri DEĞİŞMEDİ. IP sınırı 64 → **60** (`security_events.ip_address` 60). Okuma+kırpma tek noktada: **`IstemciBilgisi`** (üçüncü kopya açılmadı). `AuthManager`'ın "ip/ua zaten tutuluyor" yorumu **YANLIŞTI** — düzeltildi. |
| **K2** | Kayıtsız e-posta girişi · **kilitli hesap dalı** (simetri) · logout iki dal · sahiplik (`IdorAttempt`, Order+Payment) · 429 (örneklemeli) · bozuk imza. `InvalidateAllForCustomerAsync` dönüş değeri artık KULLANILIYOR. |
| **K3** | `SavedChangesAsync` + `SaveChangesFailedAsync`; ölçüt **`IsTemporary`** (`action=="Added"` değil); re-entrancy bayrağı. Migration YOK. |
| **K4** | `guest_name` ≤100 **sanitize sonrası** · `request_id` ≤80 + karakter sınıfı, **misafir VE üye** yolunda · telafi **tek transaction**, sarmalama **`catch` içinde**. |
| **K5** | Ödeme jetonu maskeli · **SE-3**: müşteriye giden not SABİT METİN. |
| **K6** | `MaskeliFormatter` (`ITextFormatter`) her iki sink'te; **yeni paket YOK**. Enricher yolu KAPALI (`LogEvent.Exception` readonly — ölçüldü). |
| **K8** | `SECURITY.md` 12 sapma · `ops/serilog-siem.md` koda eşitlendi · props `NuGetAuditMode` gerekçesi (GF-4 hata 7). |
| **K9** | Outbox `status` 0/2, jeton ömrü + 24 sa sonra silinir. Migration YOK. |
| **F1** | `detail` kolon genişliğine kırpılır (BULGU-L3-1). |
| **F2** | Pin kapsamı: yeni `GuvenlikFix5SozlesmeTests` (davranış + kaynak pinleri). |
| **F3** | 17 bayat satır atfı kaldırıldı; telefon pini tuzağı gevşetildi; `InputSanitizer` yorumu düzeltildi. |
| **F4** | C-2: e-posta ≤200, üye + misafir aynı sabite bakar. |

**BAĞLAYICI KARARLAR (kayıt):**
`52·GF-5·K1` IP/UA doldurma **iş katmanında değil olay servisinde**; sınır 60 (iki kolonun DARI).
`52·GF-5·K2` Sahiplik olayı kapsamı **Order+Payment**; 429'da `customer_id` NULL **kabul edilmiş sınır**.
`52·GF-5·K4` `request_id` kapısı **BİÇİM değil TAŞIYICI** sınırlar; GUID şartı ASLA.
`52·GF-5·D1` İmzasız webhook **404 statüko** — KABUL EDİLMİŞ RİSK (otorite retrieve zinciri).
`52·GF-5·D10` `KanitMaskesi` ölçütü genişletilmez; çerçeve metinleri **ayrı** `LogMetniMaskesi`.

---

## 4. S-C KAPSAMA MATRİSİ — ÖNCE / SONRA

**ÖNCE (üreten ifadeyle yeniden sayıldı):** `E=8 · H=8 · KISMEN=6` (22 satır).
> **MERKEZ SAYI HATASI:** `51·AV-2` mührü **iki yerde** "TAM BOŞLUK 10 · KISMEN 5 · TAM 7"
> diyor. Toplam doğru (22), **bölünme YANLIŞ**. Tablo ile özet çelişiyor; rapor denetçisi
> bağımsız doğruladı. Doğru taban **H=8**.

**SONRA: `H=3`** — hepsi BİLİNEN:
1. **403 yetki reddi** — katman engeli (`Divisima.Core` ProjectReference 0, handler'da red dalı yok).
2. **Webhook IP allowlist reddi** — dal sevk edilen yapılandırmada **ULAŞILAMAZ** (`00b:229`).
3. **Satıcı login başarısız/kilit** — `SellerAuthManager`, **Seller'a 0 satır**.

**KAPANAN 5:** kayıtsız login · logout · sahiplik 404 · 429 · bozuk imza.

---

## 5. REPRO (9, üretim yolundan, canlı rig)

| # | Ölçüm |
|---|---|
| R-5.1 | Kayıtsız e-posta girişi → olay YAZILIYOR; `ip=::1`, ua DOLU, `customer_id` NULL. **Aynı tabloda** eski satırlar ip/ua NULL — önce/sonra tek ölçümde. |
| R-5.2 | Logout → `Logout/Info`, "etkilenen satır: 2" (eskiden ATILAN dönüş değeri). |
| R-5.3 | Başkasının siparişine ödeme → **404 STATÜKO** + `IdorAttempt`, istek sahibi (182) atfediliyor. |
| R-5.4 | 429 → **5 × 429 → TAM 1 satır** (örnekleme canlı kanıtlandı). |
| R-5.5 | **SD-7 LAUNCH BLOKER**: `guest_name` 151 → ÖNCE 500 + yetim müşteri / ŞİMDİ **400**, müşteri ve adres MAX DEĞİŞMEDİ. |
| R-5.6 | `request_id`: `co-...` (GUID DEĞİL) → **201** · 81 karakter → 400 · boşluk/eğik çizgi → 400. |
| R-5.7 | 13 yeni `Added` denetim satırı, **0 negatif**; eski kütle 2986 DEĞİŞMEDİ (ileriye dönük). |
| R-5.8 | **K6**: aynı SQL kırpma hatası ÜÇ dump'ta; sızan değer **202 → 11 karakter**. Ön ölçümde bu satırlarda maske eki **0**'dı. Tablo/kolon adı GÖRÜNÜR (teşhis korundu). |
| R-5.9 | İmzasız **404 statüko** · boş başlık **404** · bozuk imza (gövde ve başlık) **400 + OLAY**; detayda jeton YOK. |

**R-5.10** K9 pinine çevrildi (DB'siz yüklem pini). Canlı simülasyon: ilk koşum **294** satır siler,
**13** taze satırı KORUR, **98** işlenmiş satıra DOKUNMAZ.

---

## 6. PİN ve MUTASYON (MK-6)

**Eklenen pin: 34 test** (`RateLimitPathScopeTests` +4 · `GuvenlikFix5SaklamaTests` 2 ·
`GuvenlikFix5SozlesmeTests` 28). Taban 743/746 → **777/780**.

| Mutasyon | Sonuç |
|---|---|
| MUT-1 severity `Warning`→`Info` | **TAM 1** |
| MUT-2 örnekleme kapısı etkisiz | **TAM 1** |
| MUT-3 anahtardan IP çıkarma | 2 (pin 2 anahtar dizgesiyle, pin 3 davranışla — ikisi de kasıtlı) |
| MUT-4 `SatirGuvenli` sökümü | **TAM 1** |
| MUT-5 K9 yüklemi `status==1` | **TAM 1** |
| MUT-F1 `detail` eşiği 2 katına | 2 (F1'in davranış + kaynak pini) |
| MUT-F2a `guest_name` sınırı 100→120 | **TAM 1** |
| MUT-F2b `RequestIdEnUzun` 80→81 | **0 → PİN KUSURU YAKALANDI** (aşağıda) |
| MUT-F2c `LogMetniMaskesi` truncate kuralı sökümü | **TAM 1** |
| MUT-F4 `EPosta` 200→300 | 2 (F4'ün şema + davranış pini) |

> **MK-6'NIN KAZANCI — PİN KENDİNİ KAYDIRIYORDU.** MUT-F2b **hiç yakalanmadı**: davranış pini
> sınırlarını `GirdiSinirlari.RequestIdEnUzun`den TÜRETİYORDU, yani sabit değişince pin de
> kaydı. Pin **KURALI** ölçüyordu, **DEĞERİ** ölçmüyordu. İki yeni kaynak pini değeri
> **ŞEMAYA** çapaladı (`orders.request_id` ve `customers.email` `HasMaxLength`). Aynı
> mutasyon **önce 0, sonra TAM 1** kırmızı verdi — kapı ayırt-etme kanıtı.

**BOZDUKLARIM (yerine konan pin aynı şeyi koruyor, ikisi de DAHA SIKI — rapor denetçisi mutasyonla doğruladı):**
- Telefon pini: *"dört LİTERAL aynı"* → *"tüm siteler AYNI DEĞERE çözünür"* (referans + literal
  iki biçimi de tarar). `Distinct().HaveCount(2)` **ileriye dönük tuzaktı** — Seller yarın
  sabite bağlanırsa bir İYİLEŞTİRMEYİ cezalandırırdı; gevşetildi, ayırt edicilik
  *"literal kopya ARTAMAZ"* assertine taşındı. Vakum-kırıcı niceleyici çapası artık sabitten türer.
- Sahiplik pini: 200 karakterlik pencere → **yüklemden sonraki İLK `return`**.

---

## 7. DENETİM (MK-4b — üç denetçi, ayrı worktree + ayrı test DB + ayrı scratchpad)

| Denetçi | Sonuç |
|---|---|
| **L3** (çift-kör) | 12 iddianın **12'si ONAY**, itiraz 0, ölçemedim 0. Tek kanallı iddia YOK. **BULGU-L3-1** çıkardı. |
| **Rapor denetçisi** | 13 hüküm ONAY + 1 kısmi itiraz (dosya sayımı). **UYDURMA ADAYI: YOK.** İki onarılmış pini mutasyonla sınadı: *"ikisi de daha sıkı"*. On bir kolon genişliği iddiasının **on biri de** `sys.columns` ile tuttu. |
| **Kural-uyum** | M-1/2/3/4/7/8 ONAY. **M-5** (pin kapsamı) ve **M-9** (bayat atıf) İTİRAZ. Biçim kapısı için ayırt-etme kanıtı aldı (bozuk dosya → exit 2). |

**Denetim bulguları F1-F4 ile kapatıldı.** L3 ve kural-uyum, pin kapsamı bulgusunu **bağımsız
olarak** çıkardı (aynı sonuca üç ayrı yoldan varıldı).

---

## 8. KENDİ HATALARIM (CC) — 6

1. **Ölü dedektör:** maske ekini `grep -o $'\u2026'` ile saydım, 0 çıktı, POZ kontrolü yoktu.
   Sınandı: POZ girdide de 0. Çalışan ifade `LC_ALL=C grep -o "$(printf '\xe2\x80\xa6')"`.
2. **Yanlış sütun:** S-C tablosunda 4. sütunu saydım; doğrusu 3.
3. **Blok penceresi:** satır aralığı bir kaydı (ayraç içeride, son satır dışarıda) —
   *"blok penceresi YOKLUK KANITI DEĞİLDİR"* tuzağı. İçeriğe çapalayınca düzeldi.
4. **Props XML'i `--` ile kırıldı — GF-4'ün 1. hatasının BİREBİR TEKRARI.** `msbuild -getProperty`
   probu yakaladı; `restore` sessizce geçerdi.
5. **`grep -c` sıfırda exit 1** döndürüp `&&` zincirini kırdı; testler sessizce koşmadı,
   fark edilmesi log dosyasının YOKLUĞUNA kaldı.
6. **17 bayat satır atfı** (F3 ile kapatıldı) + **çapa kirlenmesi**: `NotContain("action == \"Added\"")`
   asserti kendi YORUMUMDA geçtiği için ilk yazımda kırmızı verdi — **GF-4'ün 2. hatasının
   tekrarı**; kaynak pinleri artık YORUMSUZ metin üzerinde koşuyor.

**İki sayı düzeltmesi (rapor denetçisi):** değişen dosya "20+4" DEĞİL **27** (o an); pin tabanı
"78 metot / 103 vaka" **GF-5 ÖNCESİ** taban.

---

## 9. MERKEZ HATALARI (kayıt)

1. **S-C bölünmesi 10/5/7** — gerçek `E=8 · H=8 · KISMEN=6`; `51·AV-2` mührü iki yerde yanlış.
2. **"88 GF pini"** — hiçbir üreten ifadeyle çıkmıyor. Gerçek: **78 metot / 103 vaka** (zemin).
   AV-2'deki `88/88` bir **koşum çıktısıdır**, kalem sayısı değil. `GuvenlikFix4SozlesmeTests`
   tarifte "13" diyordu; gerçek **8 metot / 9 vaka**.
3. **K7 imza premisi** — tarif imzayı zorunlu kılıyordu; ölçüm sağlayıcının imza GÖNDERMEDİĞİNİ
   gösterdi. Uygulansaydı **tüm callback+webhook 400** olurdu. K7 düştü.
4. **`full_name` adı** — tarif üye alan adını misafir yoluna taşımıştı; misafirdeki karşılık
   `guest_name` ve tek değer İKİ kolona yazılıyor.

---

## 10. KURGU ENVANTERİ (D-YAN)

MAX müşteri **179 → 184**: `178` (B ajanı `gf5.b.1@`) · `179` (C ajanı `gf5.guest.1@`,
**SD-7 yetimi — eski kodun ürünü**) · `182` (`gf5.1@`) · `184` (`gf5.guest.5@`).
**`180`/`181`/`183` SAF KİMLİK BOŞLUĞU** (0 satır — ölçüldü).
Sipariş **287** · adres **126** · fatura **120** · `user_sessions` **372** ·
`security_events` **46** (altısında ip+ua DOLU; önce 0/40) · `audit_logs` 4328.

```
SELECT COUNT(*), MIN(id), MAX(id), SUM(CAST(id AS bigint))
  FROM orders WHERE status = 0 AND id <= 210;     ->  35 / 9 / 210 / 3837   BİREBİR
```

Depoda **0 yetim adres / 0 yetim sipariş**. Müşteri 184 **tam üretim imzası** taşıyor
(adres + sipariş + fatura + kalem + rezervasyon). Elle `INSERT` YOK, şema değişikliği YOK.

---

## 11. SIR HİJYENİ — MEKANİK KAPI (MK-4b eki, YENİ KALICI KURAL)

**Fan-out ÖNCESİ ve SONRASI `/tmp` + `%TEMP%` envanteri (ad+bayt) alınır; fark varsa dosya
ADLARI rapora yazılır, İÇERİK OKUNMAZ, silinir.**

**ÖLÇÜLDÜ — `/tmp` = `%TEMP%` = `C:\Users\pc\AppData\Local\Temp`, yani canlı Windows geçici
dizini.** Dalga başındaki "19 artefakt → 0" kapısı bu kökün yalnız bir kısmını görüyormuş.

| | adet | boyut |
|---|---|---|
| Silinen dalga artefaktı (dar küme: `gf*`, `b*.pl`, `z*`, `wt*`, `yeni_*`, `zemin_*`) | **178** | 45.9 MB |
| Silinen dalga artefaktı (ek küme: `k*`, `t*`, `v*`, `r*.json`, `sw*.body`, `*.pl` …) | **368** | 13.8 MB |
| **TOPLAM SİLİNEN** | **546** | **59.7 MB** |
| DOKUNULMADI — üçüncü taraf | 2337 `Microsoft.NET.Workload_*` · 137 GUID adlı · 18 `wct*.tmp` · 12 `mat-debug-*` · 14 `gx_assistant_installer_*` · ~16 `dd_*` | — |

**`yeni_keys.txt` — 182 bayt, OKUNMADAN silindi.** (Ad şüphe uyandırıyordu; kural içerik
okumayı yasaklıyor.)

**`/tmp/.ses` KAYDI:** AV-2'de "SİLİNİR" kararı almıştı ama **vardı** ve mtime'ı
`2026-09-05 01:04` — yani **GF-5 fan-out penceresinin İÇİNDE**. Bir şey onu yeniden üretiyor;
silmek kalıcı çözüm değil. O turda (kural değişmeden önce) silindi. **Merkez kararı: üretici
ÖLÇÜLMEZ (kapsam dışı, büyük ihtimalle oturum aracı); mekanik kapıda İSTİSNA — yalnız mtime +
bayt raporlanır, silinmez, içerik okunmaz.**

**Ajan sapmaları:** altı ajanın **dördü** 1.12.6-(b) sınırını zorladı (B ve C `/tmp`'ye yazıp
sildi, kapsam eleştirmeni `/tmp/scmat.txt` yazıp sildi, D `appsettings.Development.json`'ı tam
dump edip dev anahtarlarını EKRANA getirdi — rapora/diske girmedi). AV-2'de üç ajandı:
**kural sıkılaştırıldığı turda oran ARTTI.** Depoya hiçbir şey sızmadı (üç denetçi de doğruladı).

---

## 12. DOĞRULAMA

- **Build** `dotnet build Divisima-Backend.sln -c Release` → exit 0, **0 Hata**
  (`tail -1` ALDATIR; sayım `grep -cE ': error '`).
- **Biçim kapıları** `whitespace` exit 0 · `style` exit 0. Kural-uyum denetçisi ayırt-etme
  kanıtı aldı: bozuk boşluklu dosya → **exit 2**, silinince → exit 0.
- **Üç ardışık tam doğrulama BİREBİR** (biri `DIVISIMA_TEST_DB=GF5Kapanis`):
  `Category=Sql` **382/382** · tam **777/780**. Üç kırmızı = **bilinen Docker üçlüsü**
  (kök sebep log'dan: `DockerUnavailableException`; `which docker` → CLI YOK).
- **DOKUNULMAZ ihlali 0** — üç denetçi bağımsız, POZ kontrollü: `frontend/` 0/21 · `Seller`
  0/19 · `Migrations/` 0/31 · `InvoiceManager` 0/1 · `CLAUDE.md` 0/1 · `.claude/skills/` 0/2.
  `SellerRegisterRequestValidator` blob'u **bayt-aynı**.
- **Yeni migration 0 · yeni paket 0** (altı `packages.lock.json` md5 ÖNCE=SONRA).

---

## 13. BİLİNEN KALEMLER (GF-5 sonrası)

1. **S-C `H=3`** — 403 yetki reddi · webhook IP allowlist (yapısal ulaşılamaz) · satıcı login
   (Seller 0 satır).
2. **429 olayında `customer_id` NULL** — middleware `UseAuthentication`'dan ÖNCE koşuyor;
   A09'un "atıf" yarısı bu satırda kapanmıyor. Aşağı taşımak rate limit'i kimlik doğrulamanın
   ARDINA koyardı.
3. **Sahiplik olayı kapsamı Order + Payment** — kalan yedi manager (Address · Invoice ·
   Return · Shipment · PriceDrop · StockNotification …) iz YAZMIYOR.
4. **İmzasız webhook 404 statüko** — KABUL EDİLMİŞ RİSK; otorite retrieve zinciri.
5. **SignalR `admins` grubu BOŞ** — `JoinAdminGroup()` çağıranı 0; Critical alarmlar hiçbir
   insana ULAŞMIYOR. Okuyucu launch sonrası.
6. **Telefon deseninin bir literal kopyası** `SellerRegisterRequestValidator`'da kalıyor
   (Seller DOKUNULMAZ). Sayaç 4 kopya → 1 sabit + 1 literal, **0 DEĞİL**.
7. **`GuvenlikFix5SaklamaTests` yüklemi LINQ-to-Objects olarak koşuyor**, SQL'e çevrilmiyor —
   EF'in çeviremeyeceği bir yüklem yazılsa pin yeşil kalır, üretim çalışma anında patlar
   (kural-uyum denetçisinin kör nokta kaydı).

---

## 14. GF-6 (LAUNCH SONRASI)

`SC-12` outbox payload şifreleme/özetleme (SA-1 ile birlikte; `AesEncryptionProvider` bugün
tek anahtarlı ve çözemediği değeri olduğu gibi döndürüyor) · `SA-1`/`SA-2` at-rest kurcalama +
anahtar rotasyonu · `SB-1` 2FA dalında CAS geri alma · `SD-1`/`SD-2`/`SD-4` anonim uç
sözleşmesi · `SC-3` SIEM okuyucusu.
