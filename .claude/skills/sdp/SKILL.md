---
name: sdp
description: Divisima Sahada Doğrulanmış Denetim Protokolü v1.4 — her dalga/denetim tarifi başında yüklenir
---

# SDP — SAHADA DOGRULANMIS DENETIM PROTOKOLU v1.4 (KALICI; v1.1 27 Agustos 2026, v1.2 28 Agustos 2026, v1.4 5 Eylul 2026)

**Bu bolum BAGLAYICIDIR: bundan sonraki her CC isi bu protokole uyar.**
v1.0 MTUR-OLCUM turunda sahada surulda; her v1.1 maddesi O TURDA OLCULEN bir
surtunmeye dayanir ve gerekcesi maddenin yaninda yazilidir. Iki parcadir:
proje-bagimsiz CEKIRDEK ve depoya ozgu DIVISIMA EKI.

## 1. SDP-CEKIRDEK v1.2 (PROJE-BAGIMSIZ)

### 1.1 ORANTILILIK
Denetim derinligi RISKLE orantilidir; amac toren degil, **YANLIS RAPORUN
IMKANSIZLASMASI**. Maliyet olculur ve sonraki surum kalibre edilir.

| Seviye | Ne zaman | Denetim bicimi |
|---|---|---|
| **L1** kaynak tespiti | "kod boyle yaziyor" turu iddialar | Denetci, satir iddialarinin **>=%50 RASTGELE** orneklemini KENDI actigi dosyalardan dogrular; rastgelelik yontemi kayda gecer |
| **L2** davranis/canli | "sistem boyle davraniyor" turu iddialar | TAM BAGIMSIZ denetci; paketteki HER kaniti KENDI komutuyla yeniden uretir. **Kopyala-onayla GECERSIZ**: kendi komut ciktisi olmayan onay sayilmaz |
| **L3** kritik | para · stok · oturum · durustluk | **CIFT-KOR**: denetci ana akis sonuclarini GORMEDEN, yalniz gorev tanimi + kendi planiyla olcer; sonuclar sonra kiyaslanir, her fark tek tek kapatilir |

Is turu -> seviye: para/stok/oturum/durustluk = **L3** · davranissal = **L2** ·
salt kaynak okumasi = **L1**.

### 1.2 KANIT DEFTERI (tek gercek kaynak, APPEND-ONLY)
Satir silinmez/degistirilmez. Duzeltme:
`[KALEM][sira-DUZELTME] SUPERSEDES sira-n + gerekce`.

SEMA: `[KALEM][sira][SINIF][GUVEN] IDDIA | KOMUT | CIKTI-OZETI | HAM: yol | SHA | SAAT`
SINIFLAR: `K`=kaynak · `C`=canli · `D`=DB · `A`=ag · `J`=journal/denetim
GUVEN: KESIN / YUKSEK / ORTA / DUSUK — **DUSUK tek basina fix dayanagi OLAMAZ.**

Zorunlu kayit turleri:
- **ON-KAYIT**: her kalemde OLCUMDEN ONCE `[KALEM][PLAN]` — sorular + komut taslagi +
  **KARAR KRITERI** ("X gorursem kirik, Y gorursem saglam"). Sapma serbest ama
  `[PLAN-SAPMA]` gerekceli. **Bu zorunluluk AJAN SEMALARINA GOMULUR** (bkz 1.3).
- **ANLIK GORUNTU**: AYRI bir kayit turudur ve **ON-KAYIT kurali KAPSAMINDA DEGILDIR**.
  *(v1.1 — gerekce: MTUR ara kapisi anlik goruntuleri "plansiz olcum" sayip YANLIS ihlal
  uretti.)*
- **YOKLUK**: "temiz/yok" da bir IDDIADIR — `[YOKLUK]` + tarama kapsami + komut +
  **negatif kontrol kaniti** sart.
- Denetci/hakem gorev promptlari da deftere girer (verbatim ya da yol).
- **FINAL RAPOR YALNIZ DEFTERDEN TURETILIR.**

### 1.3 ROLLER ve SEMA ZORUNLULUGU
- **ANA AKIS** olcer, bulgu paketi uretir.
- **DENETCI (L1/L2/L3)** dogrular. Karar: `ONAY` / `ITIRAZ`(gerekce + KENDI kaniti) /
  `OLCEMEDIM`. Ayrica **PLAN-UYUM** kontrolu: sonuc PLAN'daki karar kriteriyle mi
  verilmis; kriter degistiyse `[PLAN-SAPMA]` gerekcesi var mi.
- **HAKEM**: yalniz cozulmeyen itirazda, 1 tur, iki tarafin kanitini gorur, kararini
  KENDI olcumuyle verir.
- **RAPOR DENETCISI** (final): taslagi DEFTERE karsi satir satir — (a) defterde olmayan
  iddia = **UYDURMA ADAYI, en agir**, (b) rapora girmeyen kritik bulgu, (c) sayi
  uyusmazligi, (d) gruplar arasi capraz tutarlilik, (e) denetim matrisi <-> defter
  eslesmesi, (f) **SUPERSEDES zinciri** — rapor gecersiz kilinmis bir satira dayaniyor mu.
  *(v1.1 (f) — gerekce: MTUR'da muhurlu bir kanit, defterlenmemis bir olcumle
  DEGISTIRILMISTI; rapor denetcisi yakaladi.)*
- **KURAL-UYUM DENETCISI** (final): baslangic anlik goruntusune karsi KENDI komutlariyla;
  **git diff KAPSAM TARAMASI** (yalniz beklenen dosyalar degismis mi) ve **cift-kor
  izolasyon kaniti** dahil.
- **SEMA KURALI (v1.1):** `plan` alani **TUM ajan semalarinda ZORUNLUDUR** (yalniz L3'te
  degil), karar kriteri dahil. *(Gerekce: MTUR'da L1 kaynak semasinda zorunlu olmadigi
  icin YEDI kalem plansiz kaldi ve ara kapi bunu ihlal olarak buldu.)*

### 1.4 AKIS ve SONLANMA
olcum -> BULGU PAKETI deftere -> denetci -> `ONAY`/`ITIRAZ`/`OLCEMEDIM` -> itirazda
yeniden olcum. **Ana <-> denetci EN FAZLA 2 TUR** -> **HAKEM** (1 tur) -> cozulmezse
**CEKISMELI** -> merkez.

BUTCELER: denetci yazmali tekrari kalem basina 1 · hakemde 1 · ana akis kalem basina en
fazla 2 derinlesme turu, sonrasi "KISMEN OLCULDU + neden".
**PLANSIZ AJAN DAGITILAMAZ**: dagitim plani (kim, ne, hangi seviye) ONCE deftere.

### 1.5 ARA KAPILAR (her grup sonunda)
1. **DEFTER BUTUNLUK BOTU**: her satirin HAM dosyasi mevcut + SHA tutuyor · her kalemde
   PLAN satiri olcumden ONCE · PLAN'siz olcum satiri yok (anlik goruntuler HARIC) ·
   suzgec sinamalari kayitli.
2. **GRUP ICI CAPRAZ TUTARLILIK**: grubun kalemleri birbiriyle celisiyor mu (2-3 satir).
3. **CHECKPOINT + 2 satir mini-retro.**

### 1.6 BULGU BICIMI
- **SIDDET**: `[PARA]` / `[VERI-BOZAN]` / `[OTURUM]` / `[DURUSTLUK]` / `[MANTIK]` /
  `[UX]` / `[KOZMETIK]` + **AKTIF|LATENT** + tek satir maruziyet.
  **`[MANTIK]` (v1.2, GEZGIN modulunden):** kod DOGRU calisirken bile SACMA / CELISKILI /
  YANILTICI olan sey. Onceki liste bunu ifade edemiyordu.
- **KOR NOKTA**: her kalem kapanisinda "bu olcumun goremeyecekleri" 1-2 satir; denetci
  ekleyebilir.
- **REPRO**: davranissal her bulguya NUMARALI yeniden-uretim blogu (temiz kosullar +
  adimlar + beklenen KIRIK sonuc). **Fix dalgasinin once/sonra olcumu BIREBIR bununla
  kosar.**
- **BULGU PAKETINE GOMULU NOT (v1.1):** "Satir numarasi kaymasi ITIRAZ DEGILDIR —
  denetci iddianin OZUNU dogrular, kaymayi NOT eder." *(Gerekce: MTUR'da bu, prompt'a
  elle eklenmek zorunda kaldi.)*

### 1.7 OLCUM DISIPLINI
1. **SINIFLANDIRICI ONCE BILINEN GIRDIYLE SINANIR** — her eslestirme dizgesi/grep/cikis
   kosulu, KARAR icin kullanilmadan once bilinen-POZITIF **ve** bilinen-NEGATIF girdiyle
   sinanir; sinama deftere. Hata yutan bir yedek (`|| echo`, `2>/dev/null`, try/catch)
   KARAR besleyemez.
2. **AD/ROTA/KOLON KAYNAKTAN OKUNUR, TAHMIN EDILMEZ** — okundugu yer yazilir.
3. **CALISMA ORTAMI OLCULUR — ZORUNLU ILK ADIM (v1.1).** Kosan surecin **KOMUT SATIRI**,
   ortam degiskenleri ve gizli yapilandirma katmanlari (user-secrets vb.) olculur ve
   deftere gecer. *(Gerekce: MTUR'da BES komut satiri argumani — odeme modu, arka plan
   isleri, posta host'u, rate limit, admin seed — URUN DAVRANISI SANILIYORDU; olculunce
   IKI iddia birden duzeldi.)*
4. **AYIRT EDICI DENEY (v1.1, kalip):** kok sebebi, tahminin ONCEDEN AYRISTIGI iki
   girdiyle **TEK olcumde** sina. *(Gerekce: MTUR'da misafir sepetinin kok sebebi,
   "mock katalogda olan" ve "yalniz gercek katalogda olan" iki kalemle tek yenilemede
   ispatlandi.)*
5. **DINAMIK VERI**: canli sayilar ZAMAN DAMGALI. Denetci fark gorunce ONCE yazma
   envanterine bakar — kurgu kayit farki itiraz sebebi DEGILDIR; itiraz yalniz ayni
   kosulda YENIDEN URETILEMEYEN iddiaya.

### 1.8 KURAL SIMETRISI (v1.1)
Ana akis ve TUM denetciler **TEK ORTAK KURAL METNINI** alir (ayni yasak listesi).
*(Gerekce: MTUR'da "user-secrets okuma" yasagi yalniz kaynak ajanlarina verilmisti;
denetci onu okudu — yalniz uzunluk olctu, deger basmadi, sizinti olmadi — ama asimetri
kayda gecti.)*

### 1.9 IZOLASYON (v1.1)
L3 cift-kor izolasyonu **PROMPT duzeyinde YETMEZ**; teknik olarak da saglanir: ajanlara
ayri calisma dizini verilir, ana akisin ara dosyalarina erisim yolu ACILMAZ ve kural-uyum
denetcisi transkriptleri tarayarak **izolasyon kaniti** uretir (pozitif kontrollu).

### 1.10 RETRO ve SURUMLEME
Her tur sonunda: ne iyi calisti · ne surtundu · changelog onerileri. Surum numarasi artar;
her madde degisikligi OLCULEN bir surtunmeye dayanir. **DENETIM MALIYETI RAPORLANIR**
(ajan sayisi, tur sayisi, plan sapmasi, ara kapi bulgusu) — kalibrasyon icin.

### 1.11 GEZGIN TURU (v1.2 - MANTIK-AV-1'in ilk uygulamasindan turetildi)

#### 1.11.1 NE ZAMAN KOSULUR
Gezgin turu, bir urun yuzeyi "kabul edildi" sayildiktan SONRA ve bir sonraki fix dalgasi
BASLAMADAN once kosulur. Amaci regresyon aramak DEGIL, **kabul turunun goremedigini**
gormektir: kabul turu MADDE LISTESI izler, gezgin tur **kullanici gibi dolasir**.

**IKI AV BIRDEN, ESIT AGIRLIKTA:**
- **(a) BUG** - kod yanlis calisiyor.
- **(b) MANTIK/TUTARSIZLIK** - kod DOGRU calisiyor ama sonuc SACMA, CELISKILI ya da
  YANILTICI. Bu ikincisi klasik test disiplininin YAPISAL kor noktasidir: pin de, tip
  sistemi de, CI de "bu ekran yalan soyluyor" demez.

#### 1.11.2 PERSONA KALIBI
Gezgin turu **personalarla** kosulur. Persona = bir kullanici niyeti + o niyetin dogal
yolu. En az uc, tipik olarak bes persona; her biri AYRI ajan, AYRI defter, AYRI kurgu hesabi.

| Persona | Niyet | Ozellikle avladigi |
|---|---|---|
| **A - MISAFIR** | kimlik dogrulamadan satin almak | misafir sinirlari, cikmaz yollar, anonim uc davranisi |
| **B - YASAM DONGUSU** | kayittan cikisa tum uye yolu | durum makinesi, ekranlar arasi tutarlilik, yarim kalan islem |
| **C - PARA** | her kurusu sorgulamak | matematik, kupon, esik, yuvarlama, vaat-fiyat uyusmazligi |
| **D - DIL/BICIM** | urunu kendi dilinde kullanmak | ceviri bosluklari, tarih/sayi/para bicimi, RTL, state korunumu |
| **E - SUPHECI SAYI** | ekrandaki her sayinin kaynagini istemek | ayni buyuklugun iki yerde farkli olmasi, defter-sayac ayrismasi |

**PERSONA KURALI:** her persona kendi niyetinin DISINA cikmaz. Kapsam cakismasi
kacinilmazdir ve ISTENIR - iki personanin ayni seyi BAGIMSIZ bulmasi capraz dogrulamadir.

#### 1.11.3 HER EKRANDA ALTILI TUTARLILIK LISTESI
Gezgin, dokundugu HER ekran/uc icin alti soruyu AYRI AYRI sorar ve deftere yazar:
1. **SAYI** - ayni buyukluk iki yerde farkli mi?
2. **DIL/BICIM** - secili dile aykiri metin / tarih / sayi / para birimi?
3. **VAAT<->DAVRANIS** - buton/metin NE VAAT EDIYOR, gercekte NE OLUYOR?
4. **DURUM MAKINESI** - durum ADLARI ve GECISLERI mantikli ve TUM DILLERDE tutarli mi?
5. **BOS/UC** - 0 sonuc, bos liste, cok uzun ad, buyuk adet, sinir degeri, negatif.
6. **MATEMATIK** - toplama / indirim / yuvarlama / kurus.

Alti sorunun **hepsi** yazilir; "ilgisiz" yaniti da bir yanittir. Boylece KAPSAM MATRISI
(ekran x persona x soru) mekanik olarak dolar ve neyin OLCULMEDIGI gorunur.
**KAPSAM MATRISI KELIME SAYIMIYLA URETILMEZ** - personalarin KENDI kapsam tablolarindan
derlenir (MANTIK-AV-1'de kelime sayimi denendi ve YANILTICI cikti).

#### 1.11.4 YANLIS-POZITIF ELEME (ZORUNLU ON ADIM)
Gezgin turu BASLAMADAN once **BILINCLI KARARLAR LISTESI** derlenir ve TUM personalara
verilir. Dort kaynaktan beslenir: olcum duzeneginin kendisi (test bayraklari, mock modlar,
kapali arka plan isleri) · veri zemini (test katalogu, bekleyen temizlik artiklari) ·
bilincli urun kararlari (kabul edilen riskler, ertelenen kalemler) · zaten kuyruktaki
paketlenmis bulgular.

Kural: listedeki bir sey **BULGU DEGILDIR**. Paketlenmis bir bulgu bagimsiz yeniden
kesfedilirse **"BILINEN - capraz dogrulama"** etiketiyle TEK SATIR yazilir; sayilmaz ama
SINIRINI genisleten kisim YENI bulgudur. "Bilincli mi emin degilim" kalanlar **SORU**
listesine gider - bulgu ile soru KARISTIRILMAZ.

**LISTENIN HER MADDESI TUR BASINDA YENIDEN OLCULUR** ya da acikca **"BAYAT OLABILIR"**
etiketi tasir. Gerekce olculdu: MANTIK-AV-1'de listeye onceki muhurden kopyalanan bir
madde ("product_images BOS") gercekte 30 satir/30 dosya cikti; o madde yalnizca
YANLIS NEGATIF uretebilecegi icin zarar vermedi, ama listenin 23 maddesinden yalniz biri
yeniden olculmus oldu ve bu turun SISTEMATIK kor noktasi olarak kayda gecti.

**GEREKCE:** bu eleme yapilmazsa gezgin turunun ciktisinin buyuk kismi "mock odeme
calismiyor", "mail gelmedi", "urun adlari sacma" gibi ZATEN BILINEN duzenek gercekleriyle
dolar ve gercek bulgular gurultuye gomulur.

#### 1.11.5 ARAC PAYLASIMI VE SERILESTIRME
Personalar paralel kosar; **ancak paylasilan ve durum tasiyan araclar serilestirilir.**
Tarayici bunun kanonik ornegidir: ayni origindeki tum sekmeler `localStorage`'i PAYLASIR,
dolayisiyla bes personanin oturum/sepet durumu birbirini bozar. (MANTIK-AV-1'de olculdu:
CORS `AllowedOrigins` tek origine acik oldugu icin ayri origin acmak da mumkun degildi.)

**KURAL:** paylasilan durumlu bir arac varsa personalar ondan MEN EDILIR ve her persona
raporunun sonunda **ARAC DOGRULAMA LISTESI** verir:
```
TD-<PERSONA>-<n> | EKRAN | ADIMLAR | OLCULECEK IFADE | BEKLENEN(saglam) | BEKLENEN(kirik)
```
Ana akis bu listeleri SERILESTIREREK kosar. Persona iddiasini arac sonucuna BAGLI
birakmaz: elindeki kaynak/API/DB kaniti kadar konusur, arac olcumunu **EK KANIT** ister.

#### 1.11.6 CIKTI DISIPLINI
Her bulgu: gozlem + **numarali REPRO** + kok-sebep adayi `dosya:satir` + siddet +
AKTIF/LATENT + persona + **kor nokta**.
- **`[MANTIK]` siddet sinifi bu modulle CEKIRDEGE GIRER** (bkz. 1.6): kod dogru
  calisirken bile sacma/celiskili/yaniltiici olan sey. Onceki siddet listesi bunu ifade
  edemiyordu.
- **AKTIF/LATENT AYRIMI OLCULUR, VARSAYILMAZ.** Bir uydurma icerik DOM'da duruyor olabilir
  ama kullanicinin gordugu ANDA yerini baska bir seye birakiyor olabilir; bu ayrim ancak
  KULLANICININ IZLEDIGI YOL kosularak yapilir.

#### 1.11.7 KOK SEBEP BIRLESTIRME
Gezgin turu cok sayida YUZEYSEL belirti uretir. Rapor yazilmadan once **belirtiler kok
sebebe gore GRUPLANIR**. Bir kok sebep birden cok belirti acikliyorsa fix dalgasi
BELIRTILERI degil KOKU hedefler.
**OLCULEN ORNEK (MANTIK-AV-1):** dort ayri belirti - bos sepet onerilerinde uydurma urun ·
sitenin kendi navigasyonunda iki 404 · sekiz koleksiyon sayfasinin bos olmasi · ayni
rotanin gelis yoluna gore 24 ya da 33 urun gostermesi - TEK kokten cikti: vitrin dosyasi
hala ESKI MOCK TAKSONOMISINI ve 18 MOCK URUNU tasiyor; onceki dalga yalniz MENUYU
veritabanina baglamisti.

#### 1.11.8 FIX DALGASI ESLEMESI
Gezgin turu **SALT OLCUMDUR**; fix baslatmaz. Cikti su sekilde dalgalara doner:

| Bulgu sinifi | Hedef dalga |
|---|---|
| `[PARA]` / `[VERI-BOZAN]` / `[OTURUM]` | **ONCELIKLI** kendi dalgasi |
| `[DURUSTLUK]` (uydurma icerik, yanlis vaat) | launch-bloker sinifi; ilk dalga |
| `[MANTIK]` - tek kokten cikan grup | KOK BASINA tek dalga (belirti basina DEGIL) |
| `[UX]` / `[KOZMETIK]` | biriktirilip tek pakette |
| SORU listesi | merkeze; karar sonrasi dalgaya |

Dalga bolumlemesi **MERKEZDEN**; gezgin yalniz siniflandirir ve onceliklendirir.
**SIRALAMA KENDI OLCUTUNE UYMAK ZORUNDADIR** - MANTIK-AV-1'de "PARA > YASAL/VERI >
DURUSTLUK > UX" olcutu yazildigi halde bir `[UX]` kalemi sekizinci siraya konup bir
`[PARA]` kalemi listeye HIC alinmamisti; rapor denetcisi bunu yakaladi.

#### 1.11.9 GEZGIN TURUNUN KENDI KOR NOKTALARI (durust kayit zorunlulugu)
Gezgin raporu su ucunu ACIKCA yazar:
1. **Arac sinirlari** - olcum riginin YAPISAL olarak goremedikleri.
2. **Kosulmayan yollar** - onkosulu bugunku veriyle saglanamayan senaryolar.
3. **Onlenen yanlis bulgular** - "bulgu sandim, olcunce degilmis" kalemleri. Bunlar
   RAPORDAN SILINMEZ; gezgin turunun kalibrasyonu bu kayitlarla yapilir.

#### 1.11.10 GEZGIN TURUNUN DENETIM KAPISI
Gezgin turu **IKI denetciyle** kapanir; ikisi de personalarin sonuclarini gormeden kendi
komutlariyla olcer.

**RAPOR DENETCISI** - taslak bulgu kumesini DEFTERLERE karsi satir satir tarar (1.3'un
(a)-(f) maddeleri) ve ayrica **KANIT GUCU TABLOSU** uretir: her yuksek riskli bulgu kac
BAGIMSIZ KANALDAN (kaynak / API / DB / arac) dogrulanmis. Tek kanalli bir bulgu, cok
kanalli bir bulguyla ayni siddet sirasina KONMAZ.

**KURAL-UYUM DENETCISI** - turun kendi kurallarina uydugunu olcer. Salt-olcum turlarinda
zorunlu maddeler: kod degismedi · veri tabanina yalniz okuma · muhurler ureten ifadesiyle ·
dokunulmaz hesaplar/kayitlar kullanilmadi · kurgu envanteri raporlarla BIREBIR · sir
sizintisi · arac yasaklarina uyum.

##### 1.11.10-a URETIM IMZASI
"Veri tabanina yalnizca okuma yapildi" iddiasi DOGRUDAN gozlenemez (elle yazilan bir
`UPDATE` denetim izine dusmeyebilir). Bunun yerine **URETIM IMZASI** olculur: turda olusan
satirlarin URETIM YOLUNDAN geldigi, o yolun URETTIGI YAN ETKI ZINCIRIYLE kanitlanir.
Ornek: bir siparis satirinin yaninda kalem, rezervasyon, stok hareketi, fatura ve durum
gecmisi satirlari da olusmus olmalidir; elle bir `INSERT` bunlari uretmez. Kimlik ureteci
varsa (siparis numarasi vb.) BICIMI de kaynaktan okunup karsilastirilir.
**Kesin kanit degildir, IKI KANALLI guclu kanittir - raporda boyle yazilir.**

##### 1.11.10-b DOKUNULMAZ KAYITLARIN ICERIGI DE OLCULUR
Bir kaydin "korundugu" iddiasi SAYI ile kapanmaz. Onceki muhurler o kayitlarin ICERIGINI
(durum, tutar, zaman damgasi) not etmisse, denetci onlari da karsilastirir. Gerekce:
sayisi degismeden icerigi degismis bir kayit, yalnizca sayan bir kontrolden GORUNMEDEN
gecer. (MANTIK-AV-1'de kabul turunun dort kaydi saat damgasina kadar eslendi.)

##### 1.11.10-c PERSONA IZOLASYONUNUN AMPIRIK OLCUMU
Teknik izolasyon (ayri calisma dizini) uygulanamadigi turlarda, izolasyonun FIILEN tutup
tutmadigi **capraz atif sayimiyla** olculur: her personanin defterinde DIGER personalarin
adi kac kez geciyor. Beklenen 0; sayim **pozitif kontrollu** yapilir (personanin KENDI adi
bulunmali). Bu, mekanizmanin yerine gecmez ama korudugu degerin saglanip saglanmadigini
gosterir.
**KURAL:** kod URETEN bir dalgada teknik izolasyon (MK-4) ZORUNLUDUR; SALT-OLCUM turunda
ampirik olcum kabul edilebilir ve raporda ACIKCA "mekanizma uygulanmadi, korudugu deger
olculdu" diye yazilir.

##### 1.11.10-d DENETCININ KOR NOKTASI ANA AKISA GERI DONER
Denetci "sunu OLCEMEDIM" dediginde, ana akis o boslugu KAPATMAYA CALISIR ve sonucu deftere
yazar. Kapatamiyorsa bosluk RAPORDA ADIYLA durur. (MANTIK-AV-1 ornegi: denetci ham API
dokumlerini sir taramasina dahil etmedigini yazdi; ana akis tum scratchpad agacini - 47
dosya, uzanti farketmeksizin, suzgec sinanmis - tarayip boslugu kapatti.)

### 1.12 GUVENLIK TURU MODULU — TAM METIN ARSIVDE, BURADA v1.4 DELTASI

**TAM METIN:** `42·GUVENLIK-AV-1 · SDP 1.12` (`docs/muhur/42-guvenlik-av-1.md`, bolum 6).
Guvenlik dalgalarinda ORADAN okunur (MK-11/b somut gerekce). Arsiv BAYT-SABITTIR (MK-11/d);
asagidaki uc madde arsivi DEGISTIRMEZ, onu **DEGISTIREREK TAMAMLAR** ve celiski halinde
**BU METIN GECERLIDIR**. Ucu de GUVENLIK-AV-2 turunda OLCULEN bir surtunmeye dayanir.

**1.12.2 EKI (v1.4) — ON KOSUL EKSENININ TANIMI.**
ON KOSUL, bulguyu **KIM TETIKLEYEBILIR** sorusunu yanitlar; **okuyucu ya da etkilenen taraf
DEGILDIR.**
*Gerekce OLCULDU (AV-2):* SIEM/izleme boslugu bulgusu "kimse OKUMUYOR" gerekcesiyle
`KIMLIKSIZ-UZAK` etiketlendi ve siddeti TAM DA O EKSENDEN sisirilerek YUKSEK'e cikti. Bir
TESPIT BOSLUGUNUN saldirgan on kosulu YOKTUR; dogru deger `ilgisiz`. Ayni defterde ayni
eksen IKI FARKLI kurala gore dolduruldu (baska bir bulguda "tetikleyici anonim ama bedeli
sorusturma odiyor" -> `KIMLIKLI`). Rapor denetcisi siddeti ORTA'ya dusurdu.

**1.12.6-(b) EKI (v1.4) — "DISKE YAZILMAZ" GECICI DOSYAYI DA KAPSAR.**
Jeton/parola/anahtar bicimli hicbir deger diske yazilmaz; **gecici dosya, "yaz-kullan-sil"
kalibi ve scratchpad DISINDAKI HICBIR YOL** (ozellikle `%TEMP%` / `/tmp` kokleri) istisna
DEGILDIR. Kabuk degiskeni yasar; gerekiyorsa cagrilar TEK cagriya toplanir.
*Gerekce OLCULDU (AV-2):* uc ajan bu siniri kaydirdi - ikisi jetonu yazip SILDI (denetci
silindigini bagimsiz dogruladi), ucuncusunun yazdigi jeton-bicimli dosya **SILINMEDEN KALDI
ve SAHIPSIZDI** (alti defterin hicbiri anmiyordu; ancak kural-uyum denetcisi yakaladi).
Ek olcum: o degerdeki BUYUK HARFLI GUID'i `KanitMaskesi` **zaten maskelemezdi**
(`char.IsLower` sarti) - yani "maske nasilsa yakalar" varsayimi da YANLIS.

**1.12.10 EKI (v1.4) — KAPSAM MATRISI KUMULATIFTIR.**
Her AV turu, kapsam matrisini (**uc/controller x TUR**) muhre **KUMULATIF** yazar; sonraki
AV turu **onceki turun KOR KUMESINDEN baslar**. Kapsamin YER DEGISTIRMESI kapsam
GENISLEMESI SAYILMAZ.
*Gerekce OLCULDU (AV-2):* AV-1'in anilmayan 13 controller'i ile AV-2'nin anilmayan 17'sinin
**KESISIMI 0** cikti - iki kume TAMAMEN AYRIK; 40 controller'in **30'u en az bir turda kor
kaldi** ve iki turde de kapsanan yalniz 10. Her tur "dar" tarif edildi ama **"NEYE GORE
dar" YAZILMADIGI** icin derinlesme ve korlesme ayni anda oldu, kimse gormedi.

## 2. DIVISIMA EKI v1.2 (DEPOYA OZGU)

### 2.1 FIX DALGALARINA ESLEME
Mevcut pin disiplini (pin + dis kontrolu + 5. kontrol/mutasyon) **KORUNUR**; SDP onun
YANINA eklenir:

| Mevcut | SDP eki |
|---|---|
| PIN yazilir | — |
| DIS KONTROLU (assert ters -> isimli kirmizi) | — |
| 5. KONTROL (uretim mutasyonu) | — |
| — | **DAVRANIS DENETCISI (L3 cift-kor)**: dalganin ONCE/SONRA REPRO bloklarini ana akisin sonuclarini GORMEDEN yeniden uretir |
| — | **RAPOR DENETCISI**: dalga raporunu deftere karsi tarar |
| — | **KURAL-UYUM DENETCISI**: `git diff` KAPSAM taramasi — dalga kapsami disinda dosya degismis mi |

KURAL: bir FIX dalgasinda her REPRO blogu, olcum turunda yazilan **NUMARALI blokla
BIREBIR ayni adimlari** kosar; fix "once kirik / sonra saglam" olarak AYNI komutla
gosterilir.

### 2.2 IZLEYICI SOZLESMESININ SDP ICINDEKI YERI
Bolum "KALICI KURAL - IZLEYICI / OLCUM ARACI SOZLESMESI" maddesi, SDP CEKIRDEK 1.7/1'in
OZEL BIR HALIDIR. Ayni aile: **mekanizmanin CALISTIGI, sonucu bilinen bir girdiyle BIR KEZ
gozlenir.** Depoda bu ailenin bes ornegi kayitli (`Identity.Name` · `IDistributedCache` ·
uygulanmayan mutasyonlar · izleyici cikis kosulu · MTUR'daki grep hane-sayisi/diakritik
tuzaklari).

### 2.3 GOZ ORTAM KURALLARI
- Ortam `scratchpad/goz1/` altindaki `schtasks` gorevleriyle kalkar (`DivisimaGoz1Api`,
  `DivisimaGoz1Statik`). `Start-Process` bu ortamda OLUR (bkz. GOZ-FIX muhru).
- **`api-baslat.cmd` BES ARGUMAN VERIYOR ve bunlar URUN VARSAYILANI DEGILDIR:**
  `--Iyzico:UseRealSdk=false` · `--BackgroundJobs:Enabled=false` · `--MailSettings:Host=`
  · `--AdminSeed:Enabled=false` · `--RateLimit:AuthPermitLimit=100`.
  **HER OLCUM RAPORU BU LISTEYI ANMAK ZORUNDADIR** — aksi halde duzenek artifakti urun
  kusuru sanilir (MTUR'da iki kez sanildi).
- Build ONCESI gorev DURDURULUR (kosan API DLL'leri kilitler -> MSB3027), SONRASINDA
  yeniden baslatilir. **Her mutasyon/dis turu oncesi YENIDEN DERLENIR** (bayat-ikili
  kurali).
- Omer'in hesabi (musteri 10, `e2b.sandbox@example.com`) ve verileri OLCUMDE KULLANILMAZ;
  tum yazmali senaryolar kurgu hesapla ve TAMAMI envantere.
- Ekran goruntusu bu panelde ALINAMIYOR; yerlesim SAYISAL olculur
  (`getBoundingClientRect`, `elementFromPoint`).

---

