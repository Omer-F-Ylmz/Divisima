# MANTIK-AV-1 MUHRU - GEZGIN TURU (SALT OLCUM) ve SDP v1.2 (28 Agustos 2026)

**ZEMIN SHA: `0655178`** - tur boyunca **KOD DEGISMEDI, COMMIT ATILMADI, BUILD ALINMADI.**
Bu muhur AYRI ve docs-only bir commit'tir; **kendi SHA'sini ve kendi run kimliklerini
ICEREMEZ** (tavuk-yumurta) - muhrun kendi cift yesili MANTIK-AV-1 raporunda verilir.
MFIX-1'de kurulan kalip.

**ORTAM UYARISI (Divisima Eki 2.3):** olcumler `goz1` duzeneginde kosuldu; API surecinin
komut satirinda BES ARGUMAN var - `--Iyzico:UseRealSdk=false` · `--AdminSeed:Enabled=false` ·
`--BackgroundJobs:Enabled=false` · `--RateLimit:AuthPermitLimit=100` · `--MailSettings:Host=`.
**Bunlar URUN VARSAYILANI DEGILDIR.** Bu tur, o bes argumani bir YANLIS-POZITIF ELEME
LISTESINE cevirip personalara VEREREK kosuldu (bkz. SDP 1.11.4).

## KAPI

`git fetch` -> `HEAD = origin/main = 0655178`, `0 0`, agac 0 satir, worktree 1, stash 0.
Dort run CANLI teyit edildi: `31802e1` (CI - Build & Test SUCCESS + Security CI SUCCESS)
ve `0655178` (ikisi de SUCCESS); `head_sha` prefix iki turda da TUTTU.

**KAPI SUZGECI BIR KUSUR YAKALADI:** run nesnelerinin 4 boslukla girintili oldugunu
VARSAYMISTIM; bilinen-pozitif girdi (onceki turun yerel `runs*.json` dosyasi) HICBIR SATIR
dondurmedi. Girinti OLCULDU: **6 BOSLUK**. Duzeltilip yeniden sinandi - iki pozitif dosya
2/2 dogru, uc negatif (bos, gecersiz, sentetik-failure) dogru, ve ic ice nesne adlari
(commit yazari, depo adi) SIZMADI. SDP 1.7/1 bir kez daha is gordu.

## OLCUM DUZENI

**YEDI AJAN:** bes persona (GEZ-A misafir alisverisci · GEZ-B girisli musteri yasam
dongusu · GEZ-C kupon/fiyat avcisi · GEZ-D dil/bicim gezgini · GEZ-E supheci sayi
denetcisi) + iki denetci (rapor · kural-uyum). Her personanin AYRI defteri, AYRI kurgu
hesabi ve ZORUNLU MK-4a beyani vardi.

**TARAYICI SERILESTIRILDI - GEREKCE OLCULDU:** CORS `AllowedOrigins` YALNIZ
`http://localhost:5173`'e acik (canli preflight: `127.0.0.1:5173` ve `localhost:5174`
Allow-Origin ALMIYOR). Ek origin acmak config + CSP + `set-api-origin.sh` degisikligi
demekti ve salt-olcum turunda YASAKTI. Ayni origindeki tum sekmeler `localStorage`
paylastigi icin bes personanin oturum/sepet durumu birbirini bozardi. **KARAR:** personalar
KAYNAK + API + DB ile PARALEL kostu (tarayici YASAK), tarayici dogrulamalari ana akis
tarafindan SERILESTIRILEREK kosuldu. Bu, SDP 1.11.5'in dogdugu vakadir.

**DEFTER:** ana defter + bes persona defteri = **1167 satir**, append-only.

## SONUC: 65 BULGU · 25 "BILINEN - CAPRAZ DOGRULAMA" · 23 SORU

| Persona | Bulgu | Defter |
|---|---|---|
| GEZ-A misafir alisverisci | 14 | 158 satir |
| GEZ-B girisli yasam dongusu | 17 | 57 satir |
| GEZ-C kupon/fiyat avcisi | 7 | 231 satir |
| GEZ-D dil/bicim gezgini | 12 | 305 satir |
| GEZ-E supheci sayi denetcisi | 6 | 332 satir |
| ANA AKIS (tarayici yalniz onda) | 9 | 84 satir |

## EN RISKLI 10 (rapor denetcisinin itiraziyla DUZELTILMIS sira)

Olcut: **PARA > YASAL/VERI > DURUSTLUK > UX** · AKTIF > LATENT · bagimsiz kanal sayisi.
Kanallar: KYN kaynak · API canli HTTP · DB sqlcmd · TRY tarayici.

| # | Bulgu | Sinif | Kanal | Ozu |
|---|---|---|---|---|
| 1 | **Vitrin `sale_price`'i HIC okumuyor** | `[PARA][DURUSTLUK]` | **4** | Ekran "Toplam 2.499,50 TL" + "Ucretsiz kargo kazandin!" derken sunucu **1.924,50 + 49,90 kargo** tahsil ediyor. 8 urun. **IKI KATMANLI KOK:** `ProductListResponseDto`'da alan YOK **+** istemci detaydakini de okumuyor |
| 2 | **Kargo fatura kalemine gomuluyor** | `[PARA]` | 3 | `invoice_items`'ta 75 kalem `adet x birim <> satir toplami`; **64'unde** fark TAM `shipping_cost`. Yorum "yuvarlama artigi" diyor, yazilan KARGO. Ayni satir e-fatura saglayiciya gider |
| 3 | **Fatura ekrani KDV'yi sabit %20 yaziyor** | `[PARA]` | 3 | `invoices` tablosu HIC okunmuyor. DB: `0.20->73`, **`0.10->11`**, `0.1416->1`. **19 sapma = 12 oran + 7 iptal artigi** |
| 4 | **Magaza kredisi `total_price`'a dusulmuyor** | `[PARA]` | 2 | 4/4 sipariste `899,90+49,90-100,00` iken `total_price=949,80`. Siparis DTO'sunda `store_credit_used` YOK -> checkout 849,80, sonuc ekrani 949,80 |
| 5 | **KVKK: pasif adres anonimlesmiyor** | `[VERI-BOZAN]` | 3 | Kod DOGRU GORUNUYOR; `HasQueryFilter(e => e.is_active)` pasif satiri sessizce eliyor. **Ve MUHURLU BIR PINI VAKUMA DUSURUYOR** |
| 6 | **KVKK: abonelik e-postasi kaliyor** | `[VERI-BOZAN]` | 2 | Silinen hesabin gercek adresi iki tabloda duruyor; o tablolarda `customer_id` YOK -> silme yolu onlari YAPISAL OLARAK bulamiyor |
| 7 | **"Sifreyi Guncelle" butonu OLU** | `[DURUSTLUK][OTURUM]` | 2 | `onclick === null`; iki butona tiklamada API sayaci **6->6->6**. Uclar var, istemci sarmalayicisi var, arayuz HIC cagirmiyor |
| 8 | **Arama sonucu katalogu zehirliyor** | `[VERI-BOZAN]` | 3 | `search` 947'yi `total_stock:0, sizes:[]` donduruyor; `filter` **23, [S,M,L]**. Stoklu urun "Tukendi" + disabled buton, kayit oturum boyunca KALICI |
| 9 | **Misafir kuponu sessizce dusuyor** | `[PARA]` | 3 | Misafir yolu `coupon_code: ""` SABIT; uye yolu ayni dosyada kuponu gonderiyor. Musteri sepette indirimi gorup TAM FIYAT oduyor |
| 10 | **Iade sebebi haritasi kaydirilmis** | `[VERI-BOZAN]` | 2 | Musteri "Beden uymadi" seciyor, DB'ye **"Begenmedim"** yaziliyor; 5. secenek sunucuda YOK -> 400 |

**HEMEN ALTINDAKI DORTLU:** odeme ekraninda **7 bozuk placeholder** (`ceviri("b_ad_soyad")`
ekranda, UC DILDE DE - Turkce kullaniciyi da vuruyor) · bulten **%10 kupon vaadi** (muhurlu
bir fixin REGRESYONU, kontrollu once/sonra deneyiyle) · basarisiz misafir siparisinin
e-postayi **kalici yakmasi** (409 kilidi) · admin dusuk-stok sayacinin FIZIKSEL olmasi
(satilabiliri <=5'e dusmus 5 satir operatore HIC gorunmuyor, 2 PASIF urun sayaci sisiriyor).

## ANA AKISIN KENDI BULGULARI ve MOCK-TAKSONOMI KOK BIRLESTIRMESI

Tarayici yalnizca ana akista oldugu icin gorsel/etkilesim katmani onun kapsamindaydi:
sitenin KENDI navigasyonu **iki 404** uretiyor (`#/kategori/dis` ana sayfa karosu,
`#/kategori/ust` footer) · **dort uydurma marka elcisi** (Elif Yildiz, Derya Aksoy, Selin
Yaman, Nur Celik - bas harf avatari ve kisisel secki metniyle) · **sekiz koleksiyon sayfasi
pratikte bos** ("Elif'in Seckisi" 1 parca ve o da TUKENDI) · ayni rota **gelis yoluna gore
24 ya da 33 urun** yukluyor · ekran okuyucu canli bolgesi her rotada **"0 sonuc"** diyor ·
`index.html`'de **dokuz yerde** `lang==='tr' ? TR : EN` ikili dili -> **ARAPCA KULLANICI
INGILIZCE goruyor** (api-bridge'de 0 gecis; kusur index.html artigi) · bayat/yanlis yorum
("indirim rozetleri ve `sale_price` gosterimi AYNEN KALIYOR" - olcum: istemci `sale_price`'i
HIC gostermiyor) · ve IKINCI DERECE BULGU: KVKK pasif-adres boslugu **muhurlu bir pini
vakuma dusuruyor**.

**KOK BIRLESTIRME (SDP 1.11.7'nin dogdugu vaka):** yukaridaki DORT belirti - iki 404 ·
dort uydurma elci ve sekiz bos koleksiyon · bos sepet onerilerindeki uydurma urunler ·
24-vs-33 ayrismasi - **TEK KOKTEN** cikiyor: `frontend/index.html` hala **ESKI MOCK
TAKSONOMISINI** (`dis`/`ust`/`alt`/`aksesuar`/`gomlek`/`bluz`/`gunluk`/`abiye`) ve
**18 MOCK URUNU** (`{id:1,name:'Saten Midi Elbise',price:1299}`, `{id:5,'Kemerli
Trenckot',price:2199,old:2799}` ...) tasiyor. Koleksiyon `pick` yuklemleri o eski sozcuk
dagarcigini test ediyor; gercek katalog urunlerinin `cat` degeri DB SLUGU
(`goz1-aksesuar`, `e4a-kategori`) ve `sub` alani YOK - kesisim yalniz `elbise`.
Taksonomi dalgasi **yalniz MENUYU** veritabanina baglamisti.
**Fix dalgasi BELIRTILERI degil KOKU hedefler.**

## IKI DENETCI

**KURAL-UYUM DENETCISI: UYUMLU (M1-M9, dokuzu da ONAY, ITIRAZ YOK).**
En degerli iki katkisi:
- **URETIM IMZASIYLA IKI KANALLI DOGRULAMA:** "DB'ye yalniz SELECT" iddiasi dogrudan
  gozlenemez, bu yuzden 12 yeni siparisin `order_number`'inin URETECIN kalibina uydugunu
  (12/12) VE yaninda 16 `order_items` / 16 rezervasyon / 16 stok hareketi / 12 fatura /
  24 timeline / 895 `audit_log` yan etkisinin olustugunu olctu - elle bir `INSERT` bunlari
  URETMEZ. Bu, SDP 1.11.10-a'nin dogdugu vakadir.
- **KABUL TURU KAYITLARININ ICERIK ESLEMESI:** ana akis yalniz SAYIYI (4/4) olcmustu;
  denetci CLAUDE.md'deki kabul turu SAAT DAMGALARINI DB ile karsilastirdi ve dordunu de
  birebir esledi (Pending 949.70 01:01:13 · Pending 949.70 01:02:28 · COD 1519.60 01:06:24 ·
  COD 1279.60 01:06:41). SDP 1.11.10-b bundan dogdu.
- **Kendi kor noktasini yazdi** (ham API dokumlerini sir taramasina dahil etmedigini);
  ana akis boslugu KAPATTI - 47 dosyalik agac tarandi, jeton **0**, iki eslesme de kurgu
  sifresinin `.sh` degisken tanimi ve ikisi de DEPO DISINDA. SDP 1.11.10-d bundan dogdu.

**RAPOR DENETCISI: UYUMSUZ (DAR) - ON DUZELTME.**
En riskli YEDI bulgunun YEDISINI DE bagimsiz olarak yeniden uretti.
**HICBIR BULGU CURUMEDI; UYDURMA BULGU YOK.** Uyumsuzluk BULGULARDA DEGIL, SAYILARDA ve
UZLASTIRMA SATIRLARINDA: dort sayi defterden uretilemiyordu, bir capraz-dogrulama iddiasinin
defterde izi yoktu, bir curutme metodolojik olarak gecersizdi. On duzeltmenin **hepsi kabul
edildi**; dordu ana akis tarafindan KENDI komutlariyla yeniden uretildi.

**MALIYET:** 7 ajan · ~3,58M token · 491 arac cagrisi · 0 ajan hatasi, 0 bos sonuc.

## MUHURLU BIR PININ VAKUMA DUSMESI (ikinci derece bulgu)

FIX-1A muhrundeki `SILME_HANGI_UCTAN_GELIRSE_GELSIN_TUM_PII_KANALLARINI_Kapatir` pini,
silme yolunun adres kanalini **YALNIZ AKTIF adresle** kuruyor. `AccountManager` filtresiz
`GetListAsync` yaziyor ama `DivisimaDbContext`teki `Entity<Address>().HasQueryFilter(e =>
e.is_active)` sorguya `AND is_active = 1` ekliyor. Yani **KOD DOGRU GORUNUYOR, PIN YESIL,
PII KALIYOR**. Bu, bu deponun "YAPILMIS GORUNUP CALISMAYAN DUZELTME" ailesinin en sessiz
ornegidir (oncekiler: `Identity.Name` · `IDistributedCache` · uygulanmayan mutasyonlar ·
izleyici cikis kosulu · cift tanimli `sepetImzasi`).
**PIN MANTIK-FIX-3'te PASIF-ADRES FIKSTURUYLE GUCLENDIRILECEK.**

## KURGU KAYIT ENVANTERI

**Musteri 92-101** (10) · **siparis 241-252** (12, **TAMAMI COD/Confirmed - YENI PENDING
YOK**) · **adres 55-64** (10) · **fatura 74-85** (12).
**TUREV SATIRLAR (kural-uyum denetcisinin ekletti):** 16 `order_items` · 16
`stock_reservations` · 16 `stock_movements` · 24 `order_status_histories` ·
**90 ISLENMEMIS OUTBOX MESAJI** (`--BackgroundJobs:Enabled=false` yuzunden `Pending`) ·
18 oturum · 3 `wishlist_items` · 2 `cart_items`.

**PRATIK NOT (D-YAN icin):** `invoices -> orders` FK'si **RESTRICT**. Bu yuzden D-YAN
temizliginde **SIPARIS SILINMEDEN ONCE FATURA ELE ALINIR**; ters sira SQL 547 ile duser.

**MUHURLER (MK-3, URETEN IFADESIYLE):**
```
SELECT COUNT(*), MAX(id) FROM orders WHERE customer_id = 10;              -> 38 / 211  SABIT
SELECT COUNT(*), MIN(id), MAX(id), SUM(CAST(id AS bigint))
  FROM orders WHERE status = 0 AND id <= 210;                             -> 35 / 9 / 210 / 3837
SELECT COUNT(*) FROM orders WHERE customer_id=74 AND id BETWEEN 234 AND 237; -> 4  (ICERIK de esit)
```
Omer'in hesabi ve kabul turu kayitlari **KULLANILMADI, SILINMEDI**; mevcut Pending
siparislere DOKUNULMADI.

## CC'NIN ON BIR HATASI (durust kayit; dordunu rapor denetcisi yakaladi)

**EN AGIRI - ASCII/TURKCE YUKLEM TUZAGI.** "Pasif adreste PII kalan **5** satir" dedim;
gercek deger **1**. Yuklemimi `full_name <> 'Silinmis'` (ASCII) yazdim; gercekte yazilan
deger **`Silinmiş`** (Turkce S harfiyle) - yani DOGRU ANONIMLESTIRILMIS DORT SATIRI da
"PII kalan" saydim. **Bu deponun defalarca bedelini odedigi ASCII/Turkce tuzagini, tam da
onu DENETLERKEN yaptim.** Ustelik GEZ-B'nin dogru "1"iyle aramdaki farki **DINAMIK VERIYE
YIKARAK** kapatmistim - SDP'nin dinamik-veri kuralini bir **KACIS** olarak kullandim.
Ayrica "AKTIF adreste anonim olmayan 0" ifadem de yanlisti; gercekte **1** (eski ikiz
artigi, D-YAN listesine). **IRONI KAYDA GECER:** disiplini en zayif persona (GEZ-B, defterinde
suzgec sinamasi 0) DOGRU sayiyi verdi; onu "duzelten" ana akis yanlis saydi.

**IKINCISI - GECERSIZ CURUTME (788 vs 792).** GEZ-D'nin sozluk sayimi sorusunu tarayicida
`Object.keys(T).length = 792` olcerek "kapattim" ve *"runtime olcumu ayristiricidan
USTUNDUR"* dedim. Denetci curuttu, ben dogruladim: `api-bridge.js` calisma aninda
`window.T[key]` ve `window.AR[key]` **ENJEKTE EDIYOR**. Statik `cat_` anahtari 8, DB aktif
slug 5, kesisim yalniz `elbise` -> **DORT** yeni anahtar. **788 (KAYNAK) + 4 (ENJEKTE) =
792 (RUNTIME).** Ikimiz de dogru olcmusuz, FARKLI BUYUKLUKLERI. Soru KAPANMADI, YANITLANDI.

Kalan dokuz: "71'inde fark tam kargo" (dogrusu **64**) · GEZ-B'nin B2'yi bagimsiz yakaladigi
iddiasi (defterinde iz YOK, GERI CEKILDI) · GEZ-C'ye olmayan bir SUPERSEDES atfetmem ·
rota tahmini (`#/iletisim`, `#/sss` - gercekleri `#/sozlesme/...` ve IKISI DE calisiyor) ·
"paylasilan sepet baglantisi geri yuklemiyor" iddiasi (tasarim ONAY BANNERI, calisiyor) ·
`addToCart` imzasini yanlis hatirlamam (kod dogruydu, ONCEKI CAGRIM yanlisti) · yanlis
onkosulla olcum · dar PLAN suzgeci (GEZ-C/GEZ-E'nin `[PLAN-K1]` bicimini goremedi) ·
kendi yanlis-pozitif listemdeki **B2 maddesinin bayat olmasi**. Ayrica bir `printf` bicim
dizgesi hatasi defterin bir satirini kesti - **SUPERSEDES ile onarildi**, append-only korundu.

## YENI KALICI DERSLER

**(i) TURKCE ICERIK HEDEFLEYEN SQL/METIN YUKLEMLERI CIFT BICIMLE YAZILIR.**
Diyakritikli **ve** ASCII varyant birlikte aranir, ve yuklem **bilinen-pozitif** girdiyle
sinanir. Bu, MFIX-3b'de i18n envanteri icin konulan "cift yontem" kuralinin **SQL'e ve
genel metin yuklemlerine GENELLENMESIDIR**. Bedeli bu turda odendi: `<> 'Silinmis'` yuklemi
dort dogru satiri hatali saydi ve bulguyu **5 kat abartti**.

**(ii) SOZLUK BUTUNLUGU KAYNAKTAN PINLENIR, RUNTIME'DAN DEGIL.**
`api-bridge` calisma aninda sozluge anahtar **enjekte ediyor** (bugun 4 tane).
`788 kaynak + 4 enjekte = 792 runtime`. **Runtime uzerinden kurulan bir "sozluk butunlugu"
pini YALANCI YESIL verir** - kaynakta eksik bir anahtar, calisma aninda enjekte edilen bir
anahtarla MASKELENIR. MFIX-3b muhrundeki `792/792` bir RUNTIME olcumudur; dogrudur ama
**KAYNAK butunlugunu KANITLAMAZ**.

**(iii) YANLIS-POZITIF ELEME LISTESININ HER MADDESI TUR BASINDA YENIDEN OLCULUR** ya da
acikca **"BAYAT OLABILIR"** etiketi tasir. Vaka: listeye onceki muhurden kopyalanan
"`product_images` BOS" maddesi gercekte **30 satir / 30 dosya / 30 `image_url`** cikti.
O madde yalnizca YANLIS NEGATIF uretebilecegi icin zarar vermedi (hicbir persona gorsel
eksenine dokunmadi - olculdu, 5/5 defterde 0 gecis), ama listenin 23 maddesinden **yalniz
biri** yeniden olculmus oldu.

**(iv) RIG KOR NOKTASI - KALICI KAYIT.** Harness sayfasi **compositing yapmiyor**:
`document.visibilityState = "hidden"`, `requestAnimationFrame` HIC atesLEMIYOR (olcum 30 sn
zaman asimina dustu, arac "Browser pane is currently hidden" dedi). **SONUC: GECISE (CSS
transition) BAGLI HICBIR GORUNURLUK/GEOMETRI OLCUMU GECERLI DEGILDIR.** DOM / metin / sayi /
durum olcumleri GECERLIDIR (layout kosuyor). **BU SINIR BIR YANLIS BULGUYU ONLEDI:** sepet
cekmecesi `.cart.on` sinifini aldigi halde `transform` baslangic degerinde takili kaliyordu;
CSSOM okundu, kaynak DOGRU cikti, kilitleyen sey RIG'di - urun kusuru olarak RAPORLANMADI.
**EK KAVEAT:** zaman asimina dusen bir `javascript_exec` **KISMEN CALISMIS OLABILIR**; durum
sifirlanmadan ikinci olcum yapilmaz (bir kez yanlis `CMP_MAX=2` sonucuna goturdu).

## SDP v1.2 - GEZGIN MODULU CEKIRDEGE ALINDI

`scratchpad/mantikav/SDP-GEZGIN-TASLAK.md` (162 satir) taslagi **cekirdege 1.11 olarak
alindi**. Taslaktan farklar, gerekceleriyle:

| Fark | Gerekce |
|---|---|
| 1.11.3'e "KAPSAM MATRISI KELIME SAYIMIYLA URETILMEZ" cumlesi EKLENDI | Ana akis kelime sayimiyla matris cikarmayi denedi, yontem YANILTICI cikti (`[ANA][57]`) |
| 1.11.4'e "listenin HER maddesi tur basinda yeniden olculur ya da BAYAT OLABILIR etiketi tasir" EKLENDI | B2 vakasi (yeni ders iii) |
| 1.11.5'e CORS olcumu gerekce olarak EKLENDI | Serilestirmenin NEDEN zorunlu oldugu, tahmin degil olcum |
| 1.11.7'ye dorduncu belirti (24-vs-33) EKLENDI | Ana akis o belirtiyi taslaktan SONRA olctu |
| 1.11.8'e "SIRALAMA KENDI OLCUTUNE UYMAK ZORUNDADIR" EKLENDI | Rapor denetcisinin (b) itirazi |
| 1.11.10-c'ye "kod URETEN dalgada MK-4 ZORUNLU" acikca yazildi | MK-4 ile baglanti kuruldu |
| Numaralandirma `##`/`###` -> `###`/`####` | Cekirdek bolum hiyerarsisine uyum (bicimsel) |

**`[MANTIK]` SIDDET SINIFI CEKIRDEK TAKSONOMIYE (1.6) EKLENDI:** kod dogru calisirken bile
SACMA / CELISKILI / YANILTICI olan sey. Onceki liste bunu ifade edemiyordu ve bu turun
65 bulgusunun onemli bir kismi tam olarak bu sinifta.

**SURUM: v1.1 -> v1.2.** RETRO: v1.1 iki FIX dalgasinda (MFIX-B, MFIX-3b) ve bir salt-olcum
turunda (MANTIK-AV-1) suruldu. v1.2'nin getirdigi tek buyuk yapi **GEZGIN MODULU**dur;
gerekcesi olculdu: kabul turu ve pin disiplini "bu ekran yalan soyluyor" diyemiyor, ve bu
turun en agir bulgularinin cogu (uydurma icerik, vaat-davranis uyusmazligi, ayni buyuklugun
iki yerde farkli olmasi) o bosluktan cikti. Dort mikro-kural (1.11.10-a..d) tamamen
denetim kapisinda OLCULEN surtunmelerden dogdu.

## DALGA BOLUMLEMESI (MERKEZ KARARI) - KUYRUGA

**MANTIK-FIX-1 `[PARA/DURUSTLUK]`** - `sale_price` UCTAN UCA (backend liste DTO'suna alan +
istemci TUM yuzeylerde etkin fiyat + kargo esigi ETKIN ara toplamdan) · magaza kredisi
`total_price` + DTO + ekran · misafir kuponu · kupon-tazeleme (bilinen ucluden) · bulten
%10 sozluk sokumu · "2.000 TL uzeri" metin netlestirme.

**MANTIK-FIX-2 `[FATURA]`** - kargo AYRI KALEM · KDV `invoices.tax_rate`'ten · fatura ekrani
i18n. **64 bozuk `invoice_items` satiri D-YAN'a** (veri temizligi, fix degil).

**MANTIK-FIX-3 `[KVKK/HESAP]`** - pasif adres anonimlestirme **+ FIX-1A pin guclendirme
(pasif-adres fiksturu)** · abonelik e-postalari · sifre butonu · misafir e-posta yanmasi
(transaction) · adres UPDATE sanitizasyon.

**MANTIK-FIX-4 `[VITRIN TUTARLILIK + i18n]`** - arama zehirlenmesi (backend zenginlestirme +
istemci ezme korumasi + casing) · **mock-taksonomi SOKUMU** · iade haritasi + enum `Diger` ·
7 placeholder · `catd_` · `dir=rtl` · 9 TR/EN ikili · timeline kod-anahtarlari · `aria-live` ·
rozet/cekmece senkron.

Admin dusuk-stok yuklemi **ADMIN-FIX** kaydina.

**KUYRUK DEVAMI:** FIX-1B -> ADMIN-FIX -> IMPORT-FIX (katalog gelirse ONE CEKILIR) ->
FIX-1C -> LOG-FIX -> FIX-2 -> FIX-3/B13.

**D-YAN TEMIZLIK LISTESINE EK:** MANTIK-AV-1'in kurgu kayitlari (musteri 92-101, siparis
241-252, adres 55-64, fatura 74-85, 90 islenmemis outbox mesaji) · **AKTIF bir adreste duran
eski-ikiz PII artigi** · 64 bozuk `invoice_items` satiri. Onceki dalgalarin kayitlariyla
(213-240, musteri 74-91) birlikte TEK temizlik isinde ele alinir; **fatura siparisten ONCE**.

---

