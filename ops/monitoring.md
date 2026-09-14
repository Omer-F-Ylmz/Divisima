# İzleme ve Alarm (MON-1)

> Bu belge **ne izlendiğini, alarmın hangi kanaldan geldiğini, eşikleri, susturma usulünü ve
> yanlış alarmda ne yapılacağını** anlatır. Değer (adres, parola, IP) içermez; değerler
> sunucudaki `.env`dedir.

## 1. Ne izleniyor — tek bakışta

| # | Ne | Nerede koşar | Sıklık | Eşik / tetik | Kanal |
|---|---|---|---|---|---|
| D1 | **Kritik güvenlik olayı** (`security_events`) | API içinde, Hangfire `kritik-olay-alarm` | 5 dk | son turdan beri izlenen beş tipte **en az bir `Critical`** | uygulama outbox → SMTP |
| D2 | **API ayakta mı** (`/health/ready`) | sunucu cron, `ops/monitoring/watchdog.sh` | 5 dk | **3 ardışık** 200-dışı yanıt | konak SMTP (`alarm-mail.sh`) |
| D3 | **Kaynaklar**: disk · DB veri dosyası · konteyner · bugünkü yedek | sunucu cron, `ops/monitoring/daily-report.sh` | günde 1 (07:00 UTC) | disk **%85** · DB **8192 MB** · `unhealthy`/çalışmayan · bugün `divisima-*.bak.age` yok | konak SMTP |
| D4 | **Hata görünürlüğü**: son 24 saat `[ERR]`/`[FTL]` log satırı | aynı günlük özet | günde 1 | eşik yok — sayı raporlanır | konak SMTP |

**Alıcı TEK kaynaktır:** `.env` → `DIVISIMA_ALARM_EMAIL`. Uygulama bunu
`Monitoring__AlarmEmail` olarak alır (`docker-compose.prod.yml`), betikler aynı anahtarı
`.env`den okur.

**Konu satırları** (posta filtresi bunlara kurulur):

| Konu | Anlamı |
|---|---|
| `Divisima ALARM - N kritik güvenlik olayı` | D1 |
| `Divisima ALARM - API sağlık kontrolü başarısız` | D2: 3. ardışık hata, **tek** yeniden başlatma denendi |
| `Divisima ALARM - API hâlâ erişilemiyor` | D2: aynı kesinti sürüyor, saatte bir; yeniden başlatma **yok** |
| `Divisima ALARM ÇÖZÜLDÜ - API normale döndü` | D2: kesinti bitti |
| `Divisima ALARM - günlük özet: N sorun` | D3/D4: en az bir eşik aşıldı ya da bir kalem **ölçülemedi** |
| `Divisima günlük özet - normal` | D3/D4: her şey eşik altında |

> **Normal günde de mail gelir — bilinçli.** Gelmeyen günlük özet, alarm kanalının kendisinin
> (cron, SMTP kimliği, betik) bozulduğunun **tek** işaretidir. 07:30 UTC'de özet yoksa kanal
> bozuktur (bkz. §5).

## 2. D1 — kritik olay alarmı (uygulama içi)

- **İzlenen tipler:** `PaymentAfterTerminal` · `RefreshTokenReuse` · `PaymentSignatureInvalid` ·
  `AccountLocked` · `ProductImportRejected`. Tetik yalnız **`Critical`**tir (bugün
  `PaymentAfterTerminal`, `RefreshTokenReuse`, `AccountLocked`); aynı turdaki `Warning`
  satırları özete **sayı olarak** girer ama tek başına mail üretmez.
- **Gövde:** olay tipi · sayı · ilk 3 kayıt id'si + inceleme SQL'i. IP, müşteri, `detail`
  **taşımaz** — outbox payload'ı DB'de düz durur ve mail üçüncü taraf sağlayıcıdan geçer.
- **İmleç** Hangfire deposundaki `divisima:kritik-olay-alarm` hash'inde (`son_id`). Migration yok;
  uygulama yeniden başlayınca kaybolmaz.
- **İlk koşum** tabanı kurar, geçmişi bildirmez.
- **Yerleşme payı 60 sn:** henüz commit'i yerleşmemiş olabilecek satır o turda okunmaz, bir sonraki
  turda okunur. Gecikme bütçesi: olay → en geç ~5 dk (tur) + 1 dk (pay) + 1 dk (outbox) + SMTP.
- **Alıcı boşsa** mail yazılmaz, imleç **ilerlemez** (alıcı verilince bekleyenler gider) ve her tur
  `MON-1 ALARM KANALI KAPALI` **ERROR** logu düşer → günlük özetin ERROR sayısında görünür.
- **Sınır (dürüst kayıt):** 60 sn'den uzun açık kalan bir transaction'ın satırı kaçabilir.
  `ops/deployment-checklist.md` 20. adımın günlük SQL sorgusu bu yüzden **yedek kanal** olarak
  durur.

## 3. D2 — watchdog

- `/health/ready` DB dahil bağımlılıkları yoklar; `/health/live` yalnız süreci (kullanılmaz —
  süreç ayakta ama DB yokken site yine çalışmaz).
- 1. ve 2. hata alarm **üretmez** (dağıtım sırasındaki yeniden başlatma bunun için tolere edilir).
- 3. ardışık hatada: `docker compose ... restart api` **bir kez** + alarm maili.
- Aynı kesintide yeniden başlatma **tekrarlanmaz**; her 12 turda (1 saat) bir uyarı gider.
  Gerekçe: kök sebep DB/disk ise döngüsel restart hiçbir şeyi çözmez, logları kaydırır.
- İlk 200'de "ÇÖZÜLDÜ" maili, sayaç sıfırlanır.
- Durum: `/var/lib/divisima-monitoring/watchdog.state` · Log: `/var/log/divisima-watchdog.log`
  (logrotate: haftalık, 8 hafta).

## 4. D3/D4 — günlük özet

| Kalem | Ölçüm | Eşik (ortamdan ezilir) |
|---|---|---|
| Disk | `df -P /` doluluk % | `DISK_ESIK=85` |
| DB veri dosyası | `DivisimaDb*.mdf/.ndf` toplamı (volume yolu) — **log dosyası sayılmaz**, Express'in 10 GB sınırı veri dosyası içindir | `DB_ESIK_MB=8192` |
| Konteyner | compose projesinde `running` olmayan ya da `(unhealthy)` | — |
| Yedek | bugün (UTC) değişmiş `divisima-*.bak.age` | ≥ 1 |
| Hata | son 48 saatte değişmiş `divisima-*.log` dosyalarında, son 24 saatin `[ERR]`/`[FTL]` satırları | bilgi |

**Ölçülemeyen kalem SORUN sayılır** — dizin yoksa "normal" yazılmaz.

### Serilog dosya saklaması (D4 ölçümü)

`Program.cs` File sink: günlük rotasyon + **100 MB'da parçala** (`rollOnFileSizeLimit`) ·
**`retainedFileCountLimit: 30`** · `logs_data` volume'ünde. Tarifin "14 gün" hedefi **uygulanmadı**:
saklama **zaten tanımlı** (tarif "yoksa ayarlanır" diyordu) ve 30 değeri DALGA C / C4 kararıdır.
**Dikkat:** sınır **dosya** sayısıdır, gün değil — 100 MB'ı aşan gürültülü bir gün birden çok
parça üretir ve daha eski günleri erken siler. Disk üst sınırı ~3 GB'tır.

## 5. Kurulum (sunucuda, bir kez) ve "kanal çalışıyor mu" kanıtı

```bash
cd /opt/divisima
grep -c '^DIVISIMA_ALARM_EMAIL=' .env                      # -> 1 (değer BASILMAZ)
install -m 644 ops/monitoring/divisima-monitoring.cron /etc/cron.d/divisima-monitoring
install -m 644 ops/monitoring/logrotate-divisima-monitoring /etc/logrotate.d/divisima-monitoring
logrotate -d /etc/logrotate.d/divisima-monitoring           # hata yok
bash ops/monitoring/daily-report.sh                         # -> gelen kutusunda özet GELDİ
```

- `/etc/cron.d/` dosya adında **nokta olmamalı** (uzantılı ad cron tarafından sessizce yok sayılır).
- Uygulama alıcıyı ancak konteyner yeniden yaratılınca görür: `.env` değişikliğinden sonra
  `docker compose ... up -d --force-recreate api` (kod değişmedi → imaj kurulmaz).

## 6. Susturma usulü

| Durum | Yapılacak | Geri alma |
|---|---|---|
| Planlı bakım (API bilerek kapalı) | `chmod -x` **değil** — cron satırı `/bin/bash` ile çağırır. `/etc/cron.d/divisima-monitoring` içindeki watchdog satırını `#` ile kapat | satırı aç; ilk 200'de sayaç sıfırlanır |
| Günlük özette bilinen/kabul edilmiş eşik (ör. disk %86, temizlik planlandı) | eşiği **geçici** yükselt: cron satırında `DISK_ESIK=90 /bin/bash ...` | satırı eski haline getir, **tarih notu düş** |
| D1'de bilinen olay seli (ör. kaba kuvvet denemesi `AccountLocked` üretiyor) | kapatılmaz — imleç her tur ilerler, **tur başına tek mail** gelir. Kaynağı engelle (fail2ban/nginx) | — |
| D1'i tamamen kapatmak | `.env` → `DIVISIMA_ALARM_EMAIL=` + `--force-recreate api`. **ERROR logu her tur düşer** — bilinçli | adresi geri yaz + `--force-recreate api`; bekleyen olaylar bildirilir |

Susturma **her zaman geri alma adımıyla birlikte** yazılır; kalıcı susturma bir alarm kanalını
sessizce öldürmektir.

## 7. Yanlış alarm geldiğinde

1. **Önce ölç, sonra sustur.** D2 alarmı: `curl -s -o /dev/null -w '%{http_code}' http://127.0.0.1:5000/health/ready`
   (**`curl -I` KULLANMA** — CLAUDE.md rig notu) + `docker compose ... ps` + `tail -50 /var/log/divisima-watchdog.log`.
2. **D1 alarmı:** gövdedeki SQL ile satırlar okunur. `AccountLocked` tek bir kullanıcının parola
   unutması olabilir — yanlış alarm değil, **düşük öncelikli** gerçek olaydır. `PaymentAfterTerminal`
   **her zaman** elle iade incelemesi ister.
3. **D3 "yedek üretilmedi":** `ls -l /var/backups/divisima/` + `grep divisima-backup /var/log/syslog`.
   03:00 işi 07:00'den önce bitmemiş olabilir mi — dosya zamanını kontrol et.
4. **"ÖLÇÜLEMEDİ":** volume yolu değişmiş olabilir (`docker volume inspect <proje>_mssql_data`).
   Betik varsayılanı proje adı `divisima` varsayar; farklıysa cron satırına `COMPOSE_PROJE=...`.
5. Yanlış alarmın **kök sebebi** bulunduysa eşik/betik değişikliği **dalga** olarak yapılır
   (pin: `Mon1SunucuBetikleriTests`); sunucuda elle betik düzenlenmez — bir sonraki dağıtım ezer.
