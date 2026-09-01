# DALGA D KAPANIS KAYDI (25 Agustos 2026)

**DALGA D RESMEN KAPANDI.** Kapanisi kanitlayan SHA: **`2bc53c5`** (her iki workflow tamamen
yesil, alti job'da failure seviyeli annotation SIFIR).

## ALTI KALEM

| Kalem | Konu | Sonuc |
|---|---|---|
| **D1** | Gorsel yukleme sizintisi + yetim satirlar | Test host'u UCUNCU bir koke yaziyor (`UseWebRoot`); 3 yetim DB satiri URETIM YOLUYLA silindi, 131 yetim dosya OLCULEN IMZAYLA temizlendi. Depo kirliligi 0. |
| **D2** | Yetim `product_stocks` + referans butunlugu | `FK_product_stocks_product_id` (RESTRICT, olcumle secildi); 120 yetim ISPATLI SEKILDE ATIL olanlar silindi, migration Sprint 6 kalibiyla. |
| **D3** | Gercek olcek provasi (400 urun) | YALNIZ OLCUM. Dalga 3'un YAPI pinleri olcekte de tuttu. **ISLEV-KIRAN bulgu:** storefront katalogun ilk 24 urununu cekiyordu -> **D3-FIX** ile 403/403 urune ulasilir oldu. |
| **D4** | Idempotency | UC olculmus kusur duzeltildi (capraz kullanici, anahtar yakma, olu replay dali) + DORDUNCU bulgu: `IDistributedCache` yalniz Redis dalinda kayitliydi, filtre dev/test/CI'da HIC calismiyordu. |
| **D5** | Redis / rate limit | Canli Redis turu OLCULEMEDI (Docker/Redis yok -> staging). Ama AYRISMA duzeltildi: kova tanimlari TEK KAYNAKTAN, iki yol da her zaman devrede, cifte sayim OLMADIGI uctan uca olculdu. |
| **D6** | Yedek / geri donus tatbikati | Tatbikat HIC YAPILMAMISTI. RTO dev'de **6,4 sn**; 11 invariant ONCE == SONRA. **Runbook'un IKI iddiasi curudu ve duzeltildi.** |

## ARADA CIKAN UC BUYUK KALEM

**D-SEMA (tek dogruluk kaynagi EF migrations).** D2'de acilan "44 FK farki" bulgusu once
YALNIZ-OLCUM turuyle incelendi, sonra kullanici karariyla (secenek a) uygulandi:
`01_schema.sql` artik `dotnet ef migrations script --idempotent` CIKTISI (`generate_schema.py`
SILINDI), 47 dogrulanmis FK gercek migration'a tasindi (toplam **56 FK**, hepsi NO_ACTION),
model<->migration kayma kapisi CI'ya eklendi. **D6'da KANITLANDI:** script ile kurulan bir
veritabaninda `dotnet ef database update` **NO-OP** doner.

**CI KIRMIZISI 1 - `cd51a52`: HANGFIRE YARISI.** Her test host'u kosulsuz bir Hangfire
sunucusu calistirip `outbox-processor` isini DAKIKADA BIR kosuyor ve testlerin KENDI
drenajiyla yarisiyordu (CI'da `retry_count` 1 yerine 2). `BackgroundJobs:Enabled` ile
kapatildi. **CLAUDE.md'de kayitli ISIMSIZ FLAKE'lerin de aciklamasi budur** (kayitlar
silinmedi, "aciklandi" olarak isaretlendi).

**CI KIRMIZISI 2 - `10d794d`: `model` KILIDI.** SQL Server `CREATE/DROP DATABASE`'i `model`
uzerinden serilestirir; depoda 46 sinif kendi veritabanini kuruyor (136 DDL cagrisi).
Eklenen 47. katilimci **hic kullanmadigi** bir veritabani kuruyordu ve bedeli BES BASKA
SINIFIN dusmesi oldu. Iki katman: (A) o sinif artik sifir DDL uretiyor, (B) `TestDbKurulum`
ile **1807'ye ozel** yeniden deneme. **Yesilin sebebi AYRISTIRILDI** - retry gorunurluk adimi
her kosumda `1807 ... HIC ATESLEMEDI (0)` diyor, yani kurtaran sey (A)'ydi; retry duran bir
emniyet agi.

## ACIK KALANLAR - TEK LISTE (hicbiri Dalga D'ye ait degil)

| Kalem | Nerede kapanir |
|---|---|
| Canli **Redis** turu (dagitik kilit, blacklist, idempotency'nin Redis yolu, dagitik sayac) | staging |
| **k6** yuk turu (`ops/load-test/k6-smoke.js`) | staging |
| **Eksik indeks esigi** - 403 urunde DMV'nin canliligi bile gosterilemedi; korlemesine indeks EKLENMEZ | gercek katalog hacmi |
| **SUPHELI #14** - `X-Api-Version` ayristirilamazsa TUM API blanket 400 | launch sonrasi |
| **SUPHELI #20** - varsayilan-kapali yetki kurali controller'larla sinirli (bugun BOSLUK YOK, testte kapatildi) | launch sonrasi |
| **G4 + satici kilit sirasi** | satici modulu acilmadan ONCE (on kosul) |
| **Gercek mail turu** (SPF/DKIM/DMARC, gelen kutusu) | domain/hosting karariyla |
| **B13** terk edilmis Pending siparislere TTL · **B5** uc kapsami · **P2-inline bolme** · **P4** istemci onbellegi · launch-sonrasi defterin tamami | launch sonrasi |

**KOD TARAFINDA LAUNCH'I BLOKE EDEN IS KALMADI.** Siradaki faz IRL: domain karari, canli
Iyzico basvurusu, hosting/DNS, gercek mail turu ve gercek katalog aktarimi.

---

