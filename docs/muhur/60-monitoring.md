# 60 · MON-1 — İZLEME VE ALARM (launch öncesi, kod + ops)

**Zemin:** `fe9e8c5` · **Push'lanan kod:** `6d1b015` (CI 34892439671 + Security 34892439830, ikisi de
success) · **Dağıtım:** sunucu `39f4c5b` → `6d1b015`, API imajı yeniden kuruldu · **Tarih:** 14 Eylül 2026.
Usul ve eşikler: `ops/monitoring.md`. Defter (oturum scratchpad'i, depoya girmedi): `mon1/defter.md`.

---

## 1. NE KURULDU

| # | Kalem | Yol |
|---|---|---|
| D1 | `security_events`in **tek otomatik okuyucusu**: Hangfire `kritik-olay-alarm` (*/5). Beş tip izlenir, tetik yeni `Critical`, mail mevcut outbox'tan, gövde yalnız tip·sayı·ilk 3 id. İmleç Hangfire hash'i (migration yok). | `Divisima.Bussiness/Jobs/KritikOlayAlarmJob.cs` · `Divisima.API/Services/HangfireAlarmImleci.cs` |
| D2 | Watchdog (cron */5): 3 ardışık 200-dışı → mail + TEK restart, saatlik uyarı, ÇÖZÜLDÜ; `flock` kilidi. | `ops/monitoring/watchdog.sh` |
| D3/D4 | Günlük özet (07:00 UTC): disk %85 · DB veri dosyası 8192 MB · konteyner · bugünkü yedek · 24 sa ERR/FTL; ölçülemeyen = SORUN; normal günde de mail. | `ops/monitoring/daily-report.sh` |
| — | Konak maili: curl SMTP, `.env` source edilmez, parola argv'de yok. | `ops/monitoring/alarm-mail.sh` |
| D5 | Belge · checklist 0c · ops skill 3 satır · SECURITY.md/serilog-siem.md ekleri | `ops/monitoring.md` |

**Bilinçli kapsam sapmaları (kullanıcı KABUL):** `Program.cs` "yalnız log/health" dışında Hangfire iş
kaydı + imleç kaydı (tüm recurring kayıtlar zaten orada, Bussiness Hangfire'a bağımlı değil) ·
`Dockerfile` `mkdir -p /app/logs` (Serilog 7 gün sessizce yazamadı — canlı ölçüm).

## 2. CANLI BULGULAR (dağıtımdan ÖNCE, salt-okur)

- **Log volume BOŞTU:** `divisima_logs_data` 7 gün 0 dosya, kök root:root 755; konteyner (uid 999)
  içinden `touch` → **Permission denied**. Kök: Dockerfile C2 kalıbını uploads için uyguluyor, logs
  için uygulamıyordu. **D4'ün okuyacağı log hiç yoktu.**
- DB veri dosyası adı `Divisima.mdf` (varsayım `DivisimaDb*` idi — ölçülemedi üretirdi).
- Tek compose dosyasıyla restart, prod+db çiftiyle **aynı** konteyner id'sini çözer · `flock` var ·
  RCSI **kapalı** (`is_read_committed_snapshot_on=0`).
- `.env` ayrıştırma kuralları `docker compose config` (v5.5.1) ile 6 durumda ölçüldü.

## 3. TURLAR VE KARARLAR

| Tur | Denetim bulgusu | Karar |
|---|---|---|
| 1 | L3: **B1** imleç en büyük id'ye atlıyor (küçük id'li taze satır kaybolur) · **B2** alıcı boşken biriken Critical tabana yutulur · B3 taze log yokken "normal" · B4 `.env` yorum/boşluk · B5 konu≠tablo · kural-uyum: kapsam, kör grep, SECURITY.md çelişkisi · rapor: kör pin adayı | hepsi düzelt |
| 2 | YB-1 sayı sınırı kalkınca disk tavanı kalktı · YB-2 gelecek tarihli satır zinciri tıkar · YB-3 env gramer kenarları · YB-4 chown komutu yok | 14 gün + 40 dosya · atla+WARNING · bilinen risk · komut yaz |
| 3 | **N1** atlanan gelecek tarihli Critical için HİÇ mail yok · N2 tolerans yok · N3 grep yorumdaki `@` · N5 pin "40" "400"ü eşliyor · N6 belge | etiketle özete gir · `[^#]*@` · ankraj |
| 4 | ORTA+ **yok**; düşük/bilgi → §6 BİLİNEN | push |

## 4. PİN ve MUTASYON

**31 Mon1 yürütmesi** (29 test metodu: `Mon1KritikOlayAlarmTests` 14 · `Mon1SunucuBetikleriTests` 9 ·
`Mon1DagitimSozlesmeTests` 6, teori satırlarıyla 31). Davranış pinleri gerçek SQL, gerçek Hangfire deposu ve gerçek bash ile koşar; kaynak-sözleşme
olanlar: Program.cs kaydı, Dockerfile, Serilog parametreleri, tek-kaynak/cron/logrotate, dürüst sınır
cümlesi — davranış ayağı §5 canlı kanıttadır.

**M1–M41** koşuldu; her biri isimli kırmızı. **KÖR ÇIKAN ALTI PİN** (hepsi sıkılaştırılıp aynı mutasyonla
kırmızı gösterildi): M2 (maske sızan detayı kırptı, tam dizge arandı) · M13 (test `.env`'indeki tek
tırnak `source`u erken düşürdü) · M17 (log içeriği ölçülmüyordu) · M20 (`#` ile kapatılmış cron
satırı) · M35 (bölüm çapası kelimeyi ölçülen listede buldu) · M37 (virgülsüz `40` → `400`).
Dış kontrolü 3/3 isimli kırmızı. Geri almalar yalnız ölçüm yedeğinden, `cmp`/blob eşitliğiyle.

**Doğrulama (her tur üç ardışık, birebir):** `6d1b015` → build 0 hata · `Category=Sql` **438/438** ·
tam **890/893** (üç kırmızı = bilinen Docker üçlüsü `OrderEndpointTests`). Zemin 859/862 → +31 pin.

## 5. CANLI KABUL (üretim, 14 Eylül 2026 UTC)

| Kabul | Ölçüm |
|---|---|
| İş üretimde koşuyor | recurring `kritik-olay-alarm` `*/5`, LastJobId dolu · hash `son_id=1032` (= ölçülen MAX id) · log "imleç ilk kez kuruldu: 1032" → en az yetkili kullanıcı hash'e **yazabiliyor** |
| Alıcı yokken kanal kapalı | 20:45/20:50/20:55 `[ERR] MON-1 ALARM KANALI KAPALI` — günlük özetin ERROR sayacında **3** göründü (D1↔D4 ayırt edici) |
| **1 · Sentetik Critical → mail** | `security_events` id 1033 (20:59:53) → iş 21:00:08 outbox 27 → **21:01:09 Processed**, error NULL; payload konu "Divisima ALARM - 1…", "AccountLocked \| 1 \| 1033", detail **0** geçiş. Olay → SMTP kabulü **~76 sn**. Satır sonra silindi (1 satır, kalan 0). |
| **2 · Watchdog** | cron turu 21:05:01 state yazdı · kilit tutulurken tur **değişmedi** (flock) · `docker stop` → ARDISIK 1/2/3 → **"YENIDEN_BASLATMA … BASARILI"** + mail → ready 200 (6 sn) → **"IYILESTI onceki_ardisik=3"** + ÇÖZÜLDÜ maili · `MAIL GONDERILEMEDI` 0 |
| **3 · Eşik** | normal koşum: konu "normal", disk %15 · DB 72 MB · 3 konteyner · bugün 1 yedek · `DISK_ESIK=10 DB_ESIK_MB=50` → **"Divisima ALARM - günlük özet: 2 sorun"**, rc 0 |
| **Log dosyası** | yeni imajla volume kökü **999:999**, `touch` → YAZILABILIR (chown **gerekmedi** — ölçüldü), `divisima-20260914.log` **oluştu** |

> **DÜRÜST SINIR:** SMTP kabulü (outbox `Processed`, curl rc 0) CC'nin kanadıdır; **gelen kutusuna
> teslim** (5 mail: normal özet · kritik olay · watchdog ALARM · ÇÖZÜLDÜ · 2 sorun) Ömer'in gözüyle
> teyit edilir.

## 6. BİLİNEN (DURUM sütunlu)

| Kalem | Durum |
|---|---|
| Y1 gelecek tarihli satır yerleşme payını atlar; **RCSI açıkken** açık tx'teki düşük id'li Critical kalıcı kaybolabilir (REPRO var). Üretimde RCSI **kapalı** ölçüldü. **Tetikleyici: RCSI açılırsa yeniden açılır.** | LATENT / ACIK |
| Y2 etiket pini önek assert'i — yerleşmiş satırı etiketleyen mutasyon yeşil kaldı | ACIK |
| YB-3/Y3 `env_oku` compose gramerinin tamamı değil; ölçülmeyen kenarlar `ops/monitoring.md` dürüst sınırda adıyla; grep ile 7 vakada ayrışır | BAĞLAYICI (bilinen risk) |
| Y4 çok sayıda gelecek tarihli satırda gövde/WARNING sınırsız · Y5 checklist'te mükerrer "Disk planlaması" cümlesi | ACIK (bilgi) |
| N4 konteyner çalışmıyorken chown `":"` exit 0 (belgedeki `touch` kanıtı yakalar) | BAĞLAYICI (bilgi) |
| 60 sn'den uzun açık transaction'ın satırı alarmdan kaçabilir → günlük `PaymentAfterTerminal` SQL'i **yedek kanal** olarak KALIR | BAĞLAYICI |
| `logrotate -d <tek dosya>` "insecure permissions" hatası verir; gerçek günlük yol (ana conf, `su root adm`) **0 hata** — belge komutu bu mühürle düzeltildi | KAPALI |

## 7. DALGA İÇİ DENETİM

1. **Kalem kalem:** her kalem §1/§4/§5'te ölçüm + pin + canlı kanıtla; kanıtsız satır yok. Gelen kutusu teslimi CC tarafından ölçülemez (§5 sınırı).
2. **Yarım kalan:** yok. Satır içi bilinenler §6.
3. **Yan etki:** compose'a opsiyonel `Monitoring__AlarmEmail` (`:-`, açılışı bozmaz) · LF-1 olay tipi sayacı 14'te (job yeni `LogAsync(` çağrısı eklemez, yalnız okur) · mevcut pinler tam suitte yeşil · checklist 20. adım korunur (yedek kanal) · Serilog saklaması 30 dosya → 14 gün + 40 dosya.
4. **Kendi hatalarım:** B1/B2 tasarım kusurları · DB dosya adı ve log dizininin çalıştığı ölçülmeden varsayıldı · YB-1 disk tavanı kaybı görülmedi · altı kör pin · M38'de (a) diff grep deseni hatalı (kırmızılar mutasyonu gösterdi) · ilk üçlü doğrulama DI pini için durduruldu (PLAN-SAPMA) · bağlantı kopması sonrası mutasyon grubu yarım sanıldı, ölçülünce bilinçliydi · belge logrotate kontrolü yanlış komut.
5. **Pin dürüstlüğü:** kaynak-sözleşme pinleri §4'te adıyla; davranış kanıtları §5.
6. **Bozduklarım:** hiçbir mevcut pin kırılmadı (tam suit farkı yalnız +31 yeni pin).

**Push sayısı: 2** — kod (`6d1b015`, çift yeşil) + bu kayıt (canlı kanıt push'tan sonra üretilebildiği için; LF-5 dağıtım kaydı emsali).
