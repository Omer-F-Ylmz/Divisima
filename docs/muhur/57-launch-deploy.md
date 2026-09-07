# 57 · LAUNCH-DEPLOY-1 (LD-1) — SUNUCU KURULUMU

**Zemin:** `6139b84` (56 · LAUNCH GO) · **Sunucu:** Hetzner CPX22 `167.233.254.198` ·
**Alan adı:** `divisima.net` (vitrin `divisima.net` + `www`, API `api.divisima.net`)

> **SIR KURALI (SDP v1.5 · 1.12.6-b):** bu mühürde hiçbir gizli değer yoktur. Anahtarlar
> sunucuda üretilir, ekrana basılmaz; burada yalnızca **anahtar ADLARI**, komutlar ve
> ölçüm sonuçları durur.

---

## FAZ 0 — LF-2: ALAN ADI GEÇİŞİ + SITEMAP KÖKÜ

### 0.1 TARİF ↔ KOD ÇELİŞKİSİ (DUR-rapor → merkez kararı)

Tarif iki şey söylüyordu ve **ikisi birden sağlanamazdı**:

| | Metin |
|---|---|
| **Kısıt** | "frontend'e YALNIZ CSP meta origin satırı" |
| **Kabul ölçütü** | "`divisima.com` yalnız tarihsel mühürlerde kalır — ops/frontend/appsettings'te **0**" |

Ölçüm (üreten ifade `git grep -o "divisima\.com" \| wc -l`): **190 geçiş / 16 dizin**.
`frontend/index.html`in 8 satırından **yalnız 1'i** CSP idi; kalanlar `og:url` · `og:image` ·
iki JSON-LD bloğu (`Organization` + `WebSite` `url`) · ürün şeması · `mailto:` · i18n dizesi.
Kısıt harfiyen uygulansaydı **vitrin canlıda kendi kanonik URL'sini sahip olunmayan bir alan
adı diye ilan edecekti**. Merkez kararı: **ölçüt uygulanır, mühürler hariç hepsi.**

### 0.2 YAPILAN

**29 dosya yalnız yeniden adlandırma · 3 dosya esaslı değişiklik** (üreten ifade:
`git diff -U0 -- <f> | grep '^+[^+]' | grep -cv 'divisima\.net'`).

```
kalan divisima.com (mühür hariç) : 0      [ölçüt karşılandı]
mühürlerdeki divisima.com        : 33     [DEĞİŞMEDİ - MK-11/d]
docs/muhur diff'te               : BOŞ
```

### 0.3 SITEMAP KÖKÜ — CANLI KUSUR KAPANDI (AV-3 / SD-4, GF-7'den öne çekildi)

Ölçülen önceki hal: `Sitemap([FromQuery] string? baseUrl)` + `(baseUrl ?? "<sabit>")`.
**İki kusur birdeydi:**

1. **Sabit yedek yanlış alan adıydı.** `/api/seo/sitemap`e doğrudan gelen her istek, sahip
   olunmayan bir alan adının URL'lerini üretiyordu. nginx'in `?baseUrl=` eki bunu
   **maskeliyordu** — yani kusur yalnızca proxy'yi ATLAYAN çağrılarda görünürdü.
2. **Daha ağırı:** `baseUrl` **istemci girdisiydi** ve `[AllowAnonymous]` bir uçtan doğrudan
   XML gövdesine yazılıyordu — arama motoruna sunulan belgenin içeriğini herhangi bir anonim
   çağıran belirleyebiliyordu, üstelik değer **kaçışlanmıyordu** (XML enjeksiyonu).

**Karar:** sorgu parametresi **tamamen kaldırıldı** (opsiyonel bırakmak aynı kapıyı açık
tutardı) · kök tek kaynaktan `Storefront:BaseUrl` · boşsa **gürültülü 500** · gövdeye giren
değer `SecurityElement.Escape` ile kaçışlanır · nginx'teki maskeleyen `?baseUrl=` eki kalktı.

### 0.4 PİNLER — `LaunchFix2SozlesmeTests` (7 test)

| Pin | Tür |
|---|---|
| `ESKI_ALAN_ADI_YALNIZ_TARIHSEL_MUHURLERDE_KALIR` | kaynak-sözleşme |
| `TARAYICI_CALISIYOR_MUHURLERDE_ESKI_AD_HALA_VAR` | vakum kırıcı + POZ/NEG |
| `Sitemap_KOKU_YAPILANDIRMADAN_OKUR_SABIT_DEGIL` | **davranış** (ayırt edici deney) |
| `Sitemap_STOREFRONT_BASEURL_BOSSA_GURULTULU_DUSER` | **davranış** |
| `Sitemap_KOK_XML_KACISLANIR` | **davranış** |
| `Sitemap_HICBIR_SORGU_PARAMETRESI_KABUL_ETMEZ` | derlenmiş imza (reflection) |
| `Nginx_SITEMAP_PROXYSI_SORGU_EKI_TASIMAZ` | kaynak-sözleşme |

Davranış pinleri **veritabanı GEREKTİRMEZ**: DAL'lar elle yazılmış sahtelerle beslenir
(`00a:180` — launch öncesi yeni paket YASAK, bu yüzden Moq/NSubstitute eklenmedi).
`GetListAsync` dışındaki her üye çağrılırsa **gürültülü** düşer.

**TARAMA EVRENİ = `git ls-files`.** İlk yazımda dosya sistemi geziliyordu ve pin
**yerelde kırmızı / CI'da yeşil** olurdu. Ölçüldü: gezinti dört dosya yakaladı
(`appsettings.Development.json` + üç `logs/*.log`); dördü de **izlenmiyor ve gitignore'da**.
Gelistiricinin yerel ayarı ve çalışma zamanı logları deponun içeriği değildir. Sabit bir
"atla" listesi her yeni dosya türünde sessizce kör nokta açardı (LF-1/B-7'nin aynısı).

### 0.5 MK-6 MUTASYONLARI

| Mut | Ne | BUILD | Sonuç |
|---|---|---|---|
| **1** | sorgu parametresi geri (zorunlu) | **1** | *ayırt edici DEĞİL* — derleme kırıldı, pin ölçülemedi |
| **1b** | sorgu parametresi geri (**opsiyonel**, derlenir) | 0 | 1 isimli kırmızı — `..._SORGU_PARAMETRESI_KABUL_ETMEZ` |
| **2** | 500 yerine sessiz varsayılan (sed ile) | **1** | *sed sözdizimini bozdu* (MK-8) |
| **2b** | aynısı, hassas düzenleyiciyle | 0 | 1 isimli kırmızı — `..._BOSSA_GURULTULU_DUSER` |
| **3** | XML kaçışlama kaldırıldı | 0 | 1 isimli kırmızı — `..._XML_KACISLANIR` |
| **4** | nginx'e `?baseUrl=` geri kondu | 0 | 1 isimli kırmızı — `Nginx_..._SORGU_EKI_TASIMAZ` |
| **5** | izlenen üretim dosyasına eski ad | 0 | 1 isimli kırmızı — `ESKI_ALAN_ADI_...` |
| **6** | kök sabit yazıldı | 0 | 2 isimli kırmızı (ikisi de açıklanır: sabitleme kaçışlamayı da kaldırdı) |

Geri alma **yalnız ölçüm yedeğinden**; `git checkout`/`git stash` **kullanılmadı**;
üç dosyanın md5'i yedekle **birebir**, mutasyon izi **0**.

### 0.6 DOĞRULAMA

```
Biçim kapıları : whitespace 0 · style 0   (style ÖNCE 2 verdi - using sırası; MK-9 yakaladı)
Release build  : 0 hata
Category=Sql   : 415/415 · 415/415 · 415/415   (BİREBİR)
Tam suit       : 841/844 · 840/844 · 841/844
```

**ÜÇ KOŞUM BİREBİR DEĞİL — dürüst kayıt.** #1 ve #3 aynı (bilinen Docker üçlüsü:
`OrderEndpointTests.PlaceOrder_{ValidCart,InsufficientStock,ConcurrentRequests}`; yerelde
Docker yok). **#2'de DÖRDÜNCÜ bir kırmızı** çıktı ve bu kez **ADI YAKALANDI**:
`AuthRateLimitPinTests.AuthPolicy_IstemciBasina_OnIstekten_Sonra_429_VeKovaEndpointlerArasiPaylasilir`
— *"Expected 429 ... but found 401"*. İzole koşumda **3/3 yeşil**; dosya bu dalganın
diff'inde **YOK**. Yani LF-2'nin dokunduğu yüzeyle ilgisiz, tam-suit yükü altında zamana
duyarlı bir test. **`36·MANTIK-FIX-1`'in "İSİMSİZ FLAKE" kaydının aksine bu vaka İSİMLİ** —
ileride aynı test için kanıt zinciri buradan başlar.

### 0.7 KENDİ HATALARIM (FAZ 0)

1. **ZATEN YAPILMIŞ İŞİ YENİDEN YAZDIM.** `AllowedOrigins` yerel-origin fail-fast kapısını
   ve `docker-compose.prod.yml`in `AllowedOrigins__0/1` · `ForwardedHeaders__` · `AdminSeed__` ·
   `Webhook__` satırlarını yazdım — **ikisi de `b382065`'te (LF-1/F-TURU B-4) ZATEN VARDI**.
   HEAD'e bakmadan yazdım. Son durum doğrulandı: kapı **tam 1 kopya**, `Program.cs` ve
   `docker-compose.prod.yml` diff'i **yalnız yeniden adlandırma**. Yani mükerrer kopya
   depoya GİRMEDİ, ama bu tesadüf değil ölçümle saptandı. *("Aynı kuralın ikinci kopyası"
   ailesi — bu depoda 7 kez bedeli ödendi.)*
   **AÇIKLANAMAYAN ARA ÖLÇÜM:** düzenlemeden hemen sonra `git diff --numstat` **22/0**
   okudu; sonraki her ölçümde (yedek dosyası dahil) satır sayısı 867 = HEAD ile aynı.
   Mekanizmayı **açıklayamıyorum**; uydurma bir gerekçe yazmak yerine böyle kaydediyorum.
   Son durum kanıtla doğrulandı.
2. **İki ölü dedektör.** (a) `nslookup` çıktısını `2>/dev/null` ile süzdüm — Windows
   `nslookup` cevap başlığını **stderr'e** yazıyor, süzgeç boşaldı ve **"api.divisima.net A
   kaydı YOK"** diye yanlış okudum; negatif kontrol yakaladı, çapa **ham çıktıdan**
   kopyalandı (MK-7) ve üç ad da `167.233.254.198` çıktı. (b) Döngü içindeki
   `grep -v "^\+\+\+"` BRE/ERE kaçışını karıştırdı, "esaslı değişiklik" sayacı **her dosya
   için 0** dedi; tek başına koşturunca 41 verdi.
3. **Yanlış aritmetik beklentisi.** `git grep -c` **satır** sayar, `grep -o | wc -l`
   **geçiş**; "beklenen 7" yazdım, 10 gördüm. Ölçüm doğruydu, beklentim yanlıştı.

---

## FAZ 1 — SUNUCU SERTLEŞTİRME

### 1.1 TABAN (kurulumdan ÖNCE ölçüldü)

```
hostname          divisima-prod
OS                Ubuntu 26.04.1 LTS        <- TARİF "24.04" DİYORDU (sapma)
kernel            7.0.0-30-generic
RAM / CPU / disk  3809 MB · 2 · 75G (%2)
swap              YOK
ufw               inactive
fail2ban          KURULU DEĞİL
docker            KURULU DEĞİL
sshd              passwordauthentication YES <- TARİF "no teyidi" DİYORDU; DEĞİŞTİRİLDİ
timezone          Etc/UTC                    <- zaten doğru (K11 ile uyumlu)
unattended-upgr.  kurulu
80/443 dinleyen   0
```

**İKİ SAPMA RAPORLANDI:** (a) işletim sistemi 26.04 (codename `resolute`), tarif 24.04
diyordu — Docker deposunun `resolute` süiti **ölçüldü** (`InRelease HTTP=200`, aday sürüm
`5:29.8.0-1~ubuntu.26.04~resolute`), varsayımla kurulmadı. (b) `PasswordAuthentication`
**açıktı**; tarif "teyit et" diyordu, gerçekte **değiştirilmesi** gerekti.

### 1.2 UYGULANAN

| Adım | Kanıt |
|---|---|
| `apt update` + `upgrade` | `APT_UPDATE_EXIT=0` · `APT_UPGRADE_EXIT=0` · reboot-required: hayır |
| **ufw** — ÖNCE izin, SONRA etkinleştirme | `Status: active` · deny(incoming)/allow(outgoing) · 22,80,443/tcp ALLOW |
| **fail2ban** (sshd jail) | `active` · jail canlı: **Total failed 151** (halka açık IP taranıyor) |
| **swap 2 GB** | `/swapfile 2G` · `swap_MB=2047` · fstab kalıcı · `vm.swappiness=10` |
| **sshd sertleştirme** | `passwordauthentication no` · `kbdinteractive no` · `permitrootlogin prohibit-password` |
| **kilitlenme kontrolü** | değişiklikten SONRA **yeni** SSH oturumu açıldı — anahtarla giriş çalışıyor |
| **docker** | `29.8.0` + compose `v5.5.1` · enabled+active · **ayırt edici kanıt:** `hello-world` konteyneri gerçekten koştu |
| **unattended-upgrades** | `-security` + ESM kaynakları etkin · `apt-daily-upgrade.timer` enabled+active · NEG kontrol 0 |

**ufw sırası bilinçli:** `allow 22` **etkinleştirmeden ÖNCE** verildi; ters sıra kilitlenme
demekti. `/etc/sysctl.conf` 26.04'te **yok** — yedek dalım onu yanlışlıkla yarattı, silindi
ve ayar `/etc/sysctl.d/99-divisima.conf`e taşındı.

---

## SIRADAKİ

FAZ 2 (uygulama + `.env`) · FAZ 3 (veritabanı + migration) · FAZ 4 (nginx + TLS) ·
FAZ 5 (kanıt turu). `.env`de Ömer'in elle dolduracağı alanlar `CHANGE_ME` kalır; CC yalnız
`grep -c CHANGE_ME` ile yokluğunu ölçer.
