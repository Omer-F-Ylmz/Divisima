#!/usr/bin/env bash
# ══ DAGITIM: STOREFRONT + ADMIN ICIN API ORIGIN'INI YAZ (DALGA-4-FIX-2 / M1) ═══════════
#
# NEDEN VAR (olculen zarar): storefront'un API tabani ve CSP origin'leri KAYNAKTA SABIT
# gomuluydu (5 ayri yerde "http://localhost:5000"). Depo neyse o yayina gidiyordu; LAN /
# staging / uretim adresinden acildiginda istekler kullanicinin KENDI makinesine gidiyor,
# tarayici bunlari engelliyor ve KATALOG BOS geliyordu. Ustelik API tabani ile CSP
# origin'leri ELLE senkron tutuluyordu - biri guncellenip digeri unutulabiliyordu.
#
# NEDEN DAGITIM ADIMI, NEDEN CALISMA ANI DEGIL - OLCULDU:
#   CSP <meta> belge AYRISTIRILIRKEN uygulanir. Calisma aninda DAHA GENIS bir CSP meta'si
#   eklemek politikayi GEVSETMEZ; tarayicida denendi ve istek yine
#   "securitypolicyviolation: connect-src -> http://192.168.x.x:5000/health" ile
#   ENGELLENDI. Yani API tabani runtime'da ayarlanabilirdi ama UC CSP DIREKTIFI
#   ayarlanamazdi - ve sart "hepsi TEK KAYNAKTAN turesin" idi.
#   Cozum: origin dosyaya DAGITIM aninda yazilir (bu betik), CALISMA aninda ise yalnizca
#   DOGRULANIR (index.html / admin.html icindeki tutarlilik guard'i). Bugunku kusur
#   mekanizmanin kendisi degil, DOGRULANMAMIS olmasiydi.
#
# KULLANIM
#   ops/set-api-origin.sh https://api.divisima.com      # yaz
#   ops/set-api-origin.sh --verify                      # yalniz dogrula, DEGISTIRME
#
# YEREL GELISTIRME: depoda commit'li deger "http://localhost:5000"dir; yerelde HICBIR EK
# ADIM GEREKMEZ. Bu betik yalniz dagitimda kosulur.
#
# SENKRON KURALI (KALICI): storefront'un form-action origin'i ile backend'in
# Iyzico:CallbackUrl origin'i AYNI olmalidir - Iyzico checkout formunun sonuc POST'u
# TARAYICIDAN gelir, dolayisiyla hedef origin form-action'da bulunmazsa odeme sonucu
# SESSIZCE kaybolur (E2b'de birebir yasandi). Bu betik yalniz FRONTEND tarafini yazar;
# backend tarafi Iyzico:CallbackUrl ayarindadir ve BIRLIKTE guncellenmelidir.
set -euo pipefail

KOK="$(cd "$(dirname "$0")/.." && pwd)"
FE="$KOK/frontend"
DOSYALAR=("$FE/index.html" "$FE/admin.html" "$FE/api-bridge.js")

hata() { echo "HATA: $*" >&2; exit 1; }

mevcut_origin() {
  grep -o 'name="divisima-api-origin"[^>]*content="[^"]*"' "$FE/index.html" \
    | sed 's/.*content="//; s/"$//' | head -1
}

# Bir CSP direktifinin origin'i tasidigini dogrular. 'self' ozel durumu BILEREK
# desteklenmez: bu betik farkli-origin dagitim icindir, ayni-origin durumunu calisma
# anindaki guard zaten dogru degerlendirir.
direktif_tasiyor_mu() { # <dosya> <direktif> <origin>
  local csp
  csp="$(grep -o '<meta http-equiv="Content-Security-Policy"[^>]*>' "$1" | head -1)"
  [ -n "$csp" ] || return 1
  echo "$csp" | tr ';' '\n' | grep -E "(^|\")[[:space:]]*$2[[:space:]]" | grep -qF "$3"
}

dogrula() { # <origin>
  local o="$1" sorun=0
  for d in img-src connect-src form-action; do
    if direktif_tasiyor_mu "$FE/index.html" "$d" "$o"; then
      echo "  OK   index.html  CSP $d  -> $o"
    else
      echo "  EKSIK index.html  CSP $d  -> $o"; sorun=1
    fi
  done
  for d in img-src connect-src; do
    if direktif_tasiyor_mu "$FE/admin.html" "$d" "$o"; then
      echo "  OK   admin.html  CSP $d  -> $o"
    else
      echo "  EKSIK admin.html  CSP $d  -> $o"; sorun=1
    fi
  done
  for f in "$FE/index.html" "$FE/admin.html"; do
    if grep -q "name=\"divisima-api-origin\"[^>]*content=\"$o\"" "$f"; then
      echo "  OK   $(basename "$f")  api-origin meta -> $o"
    else
      echo "  EKSIK $(basename "$f")  api-origin meta -> $o"; sorun=1
    fi
  done
  return $sorun
}

if [ "${1:-}" = "--verify" ]; then
  o="$(mevcut_origin)"
  [ -n "$o" ] || hata "index.html icinde meta[name=\"divisima-api-origin\"] bulunamadi."
  echo "Beyan edilen origin: $o"
  dogrula "$o" && { echo "SONUC: TUTARLI"; exit 0; } || hata "SONUC: TUTARSIZ - yukaridaki EKSIK satirlarina bak."
fi

YENI="${1:-}"
[ -n "$YENI" ] || hata "kullanim: $0 <origin>   ya da   $0 --verify"
case "$YENI" in
  */) hata "origin sonunda '/' OLMAMALI: $YENI" ;;
esac
echo "$YENI" | grep -qE '^https?://[A-Za-z0-9.-]+(:[0-9]+)?$' \
  || hata "origin bicimi gecersiz (yol/sorgu TASIMAZ): $YENI"
case "$YENI" in
  http://localhost*|http://127.0.0.1*) ;;
  http://*) echo "UYARI: '$YENI' duz HTTP. Uretimde HTTPS olmalidir - backend'in" >&2
            echo "       Iyzico:CallbackUrl fail-fast kontrolu de HTTPS ister (Sprint 8 madde 7)." >&2 ;;
esac

ESKI="$(mevcut_origin)"
[ -n "$ESKI" ] || hata "index.html icinde meta[name=\"divisima-api-origin\"] bulunamadi."

if [ "$ESKI" = "$YENI" ]; then
  echo "Origin zaten '$YENI' - degisiklik yok. Dogrulaniyor:"
  dogrula "$YENI" && { echo "SONUC: TUTARLI"; exit 0; } || hata "SONUC: TUTARSIZ"
fi

echo "Origin degistiriliyor: $ESKI  ->  $YENI"
for f in "${DOSYALAR[@]}"; do
  [ -f "$f" ] || hata "dosya yok: $f"
  # `set -o pipefail` acik: eslesmesi OLMAYAN bir dosyada `grep -o` 1 doner ve boru
  # hatti tumuyle basarisiz sayilir. Sifir gecis NORMAL bir durumdur (or. api-bridge.js
  # artik literal tasimiyor) - bu yuzden acikca yutuluyor. (Betik CALISTIRILARAK
  # dogrulandi; ilk kosumda tam bu satir betigi yarida kesti.)
  n="$( { grep -o "$ESKI" "$f" || true; } | wc -l | tr -d ' ')"
  if [ "$n" -gt 0 ]; then
    tmp="$f.tmp.$$"
    # Origin tam bir dizgedir; duz metin degistirme YETERLI ve KESINDIR.
    ESKI="$ESKI" YENI="$YENI" perl -pe 's/\Q$ENV{ESKI}\E/$ENV{YENI}/g' "$f" > "$tmp"
    mv "$tmp" "$f"
  fi
  echo "  $(basename "$f"): $n gecis"
done

echo "Dogrulaniyor:"
dogrula "$YENI" || hata "SONUC: TUTARSIZ - degisiklik yapildi ama dogrulama gecmedi."

kalan="$(grep -rl "$ESKI" "$FE" --include=*.html --include=*.js 2>/dev/null || true)"
if [ -n "$kalan" ]; then
  echo "UYARI: eski origin '$ESKI' su dosyalarda HALA var:" >&2
  echo "$kalan" >&2
  hata "eski origin tamamen temizlenmedi."
fi

echo "SONUC: TUTARLI"
echo
echo "HATIRLATMA - AYNI YAYINDA YAPILMASI GEREKENLER (ops/deployment-checklist.md):"
echo "  1) backend Iyzico:CallbackUrl origin'i = $YENI  (form-action senkronu)"
echo "  2) frontend/service-worker.js icindeki VERSION bump'i"
