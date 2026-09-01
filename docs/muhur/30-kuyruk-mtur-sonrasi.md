# KUYRUK (MTUR sonrasi, merkez karari)

```
1. MFIX-1   F-M3a (tek gercek checkout, mock sokum/delege) + F-M3f (request_id oturum
            basina) + F-M3b (dil degisimi) + F-M8 istemci ucu        <- SU AN
2. MFIX-2   F-M9 kararlari: KALDIR x6 · teslimat GERCEK ADRESE · beden tablosu GERCEK
            BEDENLERE · taksit satiri KALDIR · F-M6 (index.html:2301) · F-M7 overlay ·
            F-M1-H3 (istemci tazeleme)
3. MFIX-3   F-M4 (misafir sepeti) · F-M5 (hesaba-ozgu favori) · F-M2 (api-bridge
            bypass'i sozluge + AR 2 anahtar) · F-M3g (istemci query duzeltmesi)
4. MFIX-B   [BACKEND] F-M1-H2 available DTO · gecersiz kupon siparis yolunda REDDEDILIR
            ya da GORUNUR UYARI · place yanitina order_number · outbox Host-bos ->
            Failed+error
5. FIX-1B   F4 + F8 zinciri, kara liste desenlesmesi, sentetik yazma pinleri
6. ADMIN-FIX
7. IMPORT-FIX   [KRITIK YOL - katalogda gercek urun 0]
8. FIX-1C   F5 · F6 · F7 · F9 · F13 · D-YAN dev-veri temizligi
9. LOG-FIX  bes ham log satiri -> KanitMaskesi
10. FIX-2   B-6 · C-1 · G5 · B-5 · D-3
11. FIX-3 / B13   kupon geri bildirimi · terk edilmis Pending TTL
```

**D-YAN TEMIZLIK LISTESINE EKLENENLER:** uc sifir-degerli kupon (`E2TEST`,
`DALGABOLCUM`, `PANELDEN30` - tipi Yuzde, degeri 0.00, hepsi aktif) · musteri 74'un kurgu
siparisleri (213-217) · **test urunleri envanteri** (35 urunun tamami; `temizle.ps1`
scratchpad'de HAZIR ama KOSULMAMIS, 30 urun hala aktif).

---

