DOMPurify - okuma katmani HTML sanitizasyonu (E3, iki katmanli savunmanin ikinci ayagi)

  Dosya   : purify.min.js
  Surum   : 3.4.14
  Kaynak  : https://raw.githubusercontent.com/cure53/DOMPurify/3.4.14/dist/purify.min.js
            (resmi cure53/DOMPurify deposu, surum etiketine SABITLENMIS - mirror/CDN degil)
  Boyut   : 29204 bayt
  SHA-256 : c2f26ea4fc0d88141c9aa430eb515ac86fce59418ceebd85fa475b87a8d6c3e6
  Lisans  : Apache-2.0 ve MPL-2.0 (dosyadaki @license basligi KORUNDU, silinmedi)

NEDEN YEREL DOSYA: storefront CSP'si "script-src 'self'" - CDN'den yuklemek zaten engellenir.
Ayrica ucuncu taraf bir CDN'e bagimli olmak, sanitizasyon katmanini dis bir kesintiye acik hale
getirirdi.

GUNCELLEME: yeni surumu ayni sekilde surum etiketinden cek, SHA-256'yi ve boyutu bu dosyada
guncelle. Surum atlanirken CHANGELOG'daki kirilma notlari okunmali (ALLOWED_TAGS/ALLOWED_ATTR
sozlesmesi api-bridge.js icindeki guvenliHTML() fonksiyonunda).
