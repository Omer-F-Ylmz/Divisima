# A2-FIX - SIFRE POLITIKASI TEK MERKEZDEN (SUPHELI #21 KAPANDI)

Kullanici karari: #21 duzeltilir, ama A3'le AYNI commit'te degil - AYRI kucuk commit.

## OLCUM: DORT KOPYA VARDI (kullanicinin verdigi UC UCTAN FAZLASI)

Kapsam "uc uc" olarak verilmisti; tarama DORDUNCU bir kopya cikardi:

```
POST /api/auth/register            8 + buyuk + kucuk + rakam   CustomerRegisterRequestValidator
POST /api/seller/auth/register     AYNI KURALIN BIREBIR KOPYASI (dorduncu kopya)
POST /api/account/change-password  YALNIZCA >= 6, karmasiklik YOK
POST /api/auth/reset-password      HICBIR KONTROL YOK - dogrudan hash'leniyordu
```

Bir politika ancak **EN ZAYIF girisi kadar** gucludur ve en gevsek olan (reset-password),
**EN KOLAY ulasilan** yoldu. A2 bu akisi arayuze bagladigi icin kapi her musteriye acilmisti.

## YAPILAN

**Yeni merkez:** `Divisima.Core/Security/SifrePolitikasi.cs`.
`Dogrula(sifre)` -> `null` (gecerli) ya da **IHLAL EDILEN ILK kuralin OZEL mesaji**.
Genel bir "sifre gecersiz" mesaji SECILMEDI: kullanici hangi kurali cignedigini bilmezse
deneme yanilmaya duser. Bu mesajlar kayit ucunda zaten gosteriliyordu; degisen tek sey artik
DORT ucta da ayni olmalari.

**Dort giris de merkeze baglandi** - satici kopyasi DAHIL. O kural zaten BIREBIR ayniydi,
yani davranis DEGISMIYOR; ama dorduncu kopyayi birakmak "TEK MERKEZ" iddiasini bosa dusururdu.
Satici modulu bugun kapali (`Seller:RegistrationEnabled=false`).

**change-password icin bu bir SIKILASTIRMADIR** (6 -> 8 + karmasiklik) ve bilinclidir: ayni
hesabin sifresini belirleyen iki yolun farkli guc istemesi savunulabilir degil.

### IKI OLCUME DAYALI TASARIM KARARI

1. **`char.IsUpper` / `char.IsLower`, `[A-Z]`/`[a-z]` regex'i DEGIL.** Eski regex Turkce
   `Ş`/`ş` harflerini GORMUYORDU ve Turkce harfli sifre kullanan musteriyi gereksizce
   zorluyordu. Kural GEVSEMEDI, **KAPSAMI GENISLEDI** - uzunluk ve rakam sartlari aynen
   duruyor. (CLAUDE.md bolum 6c ile celiski YOK: orada yasak olan kimlik dizgesinde KULTURLU
   DONUSTURME; burada yapilan SINIFLANDIRMA ve kultur bagimsiz.)
2. **Politika kontrolu JETON DOGRULAMASINDAN ONCE kosuyor.** Jeton TEK KULLANIMLIK; zayif bir
   sifre denemesi onu HARCAMAMALI, yoksa kullanici yeniden "sifremi unuttum" yapmak zorunda
   kalirdi. Ayrica pinlendi.

### TEMIZLIK (ayni commit)

- `Messages.PasswordTooShort` (`"Şifre en az 6 karakter olmalıdır."`) **SILINDI** - hem OLU
  kaldi hem metni artik YALAN olurdu. Derleme olu oldugunu kanitladi (Sprint 8 madde 11 kalibi).
- `ResetPassword`'un basindaki **ULASILAMAZ ikinci bos-token kontrolu** silindi.
- A2'de yazilan istemci yorumu ("sunucuda hicbir kural yok") artik YANLIS olacagi icin
  duzeltildi. Istemci kurali sunucudan **bir tik KATI** (ASCII regex): yanlis pozitif uretmez,
  yalniz Turkce harfli bir sifreyi istemcide reddedip sunucuda kabul ettirebilir.
  **Ters yonde bosluk YOK** - kritik olan da bu.

## BILINCLI KIRILAN PIN

`LaunchFixMailZinciriTests.SUPHELI_SifreSifirlamada_SUNUCU_TARAFI_SIFRE_POLITIKASI_YOK_PINLENIR`
kaldirildi. Bozuk davranisi (reset-password'un `"abc"` sifresini 200 ile kabul etmesi) KABUL
EDILMIS gibi sabitliyordu; kural duzelince duzeltmeyi KIRARDI. Yerine gerekcesi yazildi.

## YENI PINLER (`SifrePolitikasiTests`, 11)

- `MERKEZ_IHLAL_EDILEN_ILK_KURALIN_OZEL_MESAJINI_Doner` (Theory x5 - bos / kisa / buyuksuz /
  kucuksuz / rakamsiz)
- `MERKEZ_GECERLI_SIFREYI_KABUL_Eder` - **vakum kirici** ("her seyi reddet" de Theory'yi gecerdi)
- `MERKEZ_TURKCE_BUYUK_KUCUK_HARFI_DE_SAYAR`
- `ZAYIF_SIFRE_UC_UCTA_DA_REDDEDILIR`
- `GECERLI_SIFRE_UC_UCTA_DA_KABUL_EDILIR` - **cift-anlam kirici** + sifirlama sonrasi YENI
  sifreyle giris 200 (sifirlama KOZMETIK degil)
- `ZAYIF_SIFRE_SIFIRLAMA_JETONUNU_HARCAMAZ`
- `HICBIR_UC_KENDI_SIFRE_KURALINI_TANIMLAMAZ` - SINIF DUZEYI kaynak taramasi; **BESINCI** bir
  kopya eklenirse kirilir (vakum kirici: taramanin gercekten dosya okudugu da assert ediliyor)

## DIS KONTROLU + 5. KONTROL

**DIS:** 6 assert ters -> **BES AYRI ISIMLI test kirmizi** (Theory'lerle 9 vaka). Geri alindi.

**5. KONTROL - kullanicinin sarti birebir karsilandi:**
- **M1** (reset-password'den politika cagrisi kaldirildi): zayif sifre **200 ile KABUL** edildi -
  #21'in olculen zararinin ta kendisi. `ZAYIF_SIFRE_UC_UCTA_DA_REDDEDILIR` ve
  `ZAYIF_SIFRE_SIFIRLAMA_JETONUNU_HARCAMAZ` kirildi; diger 9 pin YESIL kaldi (lokalize).
- **M2** (change-password'de merkez kaldirilip eski `>= 6` kurali geri kondu): **TAM 6
  KARAKTERLIK** `Aa1234` sifresi **200 ile GECTI** - eski davranisin birebir aynisi.
Ikisi de geri alindi.

## YEREL DOGRULAMA

269/269 `Category=Sql` · tam suitte **430 basarili / 433** (kirilan 3'un UCU DE Docker'li
`OrderEndpointTests`) · Release 0 hata · whitespace + style **exit 0**.

**SURECTE YASANAN (kayit - AYNI TUZAK IKINCI KEZ):** `dotnet format style` yine `IMPORTS`
hatasi verdi - `sed -i '1i using ...'` ile dosya BASINA eklenen using satirlari siralamayi
bozuyor. Dalga A'da da yasanmisti. **DERS: bu depoda `using` satiri `sed` ile dosya basina
EKLENMEZ; eklendiyse hemen ardindan `dotnet format style --include <dosya>` kosulur.**

---

