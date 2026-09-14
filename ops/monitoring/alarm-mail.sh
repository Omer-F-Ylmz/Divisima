#!/usr/bin/env bash
# MON-1 - SUNUCU TARAFI ALARM MAILI (watchdog.sh ve daily-report.sh ortak kanali)
#
# Kullanim:  printf '%s\n' "govde" | alarm-mail.sh "Divisima ALARM - konu"
#
# NEDEN UYGULAMA OUTBOX'I DEGIL: bu kanalin en onemli ani API'nin COKTUGU andir; outbox'i
# isleyen Hangfire o anda da olu. Mail bu yuzden konaktan, dogrudan SMTP ile gider.
# YENI BAGIMLILIK YOK: yalniz curl (konakta kurulu) ve bash.
#
# DEGERLER TEK KAYNAKTAN: uygulamanin kullandigi `.env` anahtarlari okunur. `.env` `source`
# EDILMEZ - bosluk tasiyan degerler (`Divisima <...>`, `User Id=`) kabugu kirar (57·LD-1 dersi).
# Varsayilanlar `docker-compose.prod.yml` ile AYNIDIR (Mon1SunucuBetikleriTests pinler).
#
# SIR HIJYENI: SMTP parolasi komut satirina (ps ile gorunur) ve diske YAZILMAZ; curl'e
# surec ikamesiyle (/dev/fd) yapilandirma olarak verilir. Betik hicbir degeri ekrana basmaz.
set -u

ENV_FILE=${ENV_FILE:-/opt/divisima/.env}
SMTP_PORT=${SMTP_PORT:-587}

konu=${1:-}
if [ -z "$konu" ]; then
    echo "alarm-mail: konu verilmedi" >&2
    exit 2
fi
if [ ! -r "$ENV_FILE" ]; then
    echo "alarm-mail: $ENV_FILE okunamiyor" >&2
    exit 2
fi

# Son eslesen satirin degeri, `docker compose` ile AYNI kurallarla (sunucuda compose v5.5.1
# `config` ciktisiyla olculdu, belgeden alinmadi): CR soyulur; tirnakliysa yalniz tirnak ICI
# alinir (icteki `#` korunur, kapanan tirnaktan sonrasi atilir); tirnaksizsa BOSLUK + `#`
# yorum baslatir (bosluksuz `a#b` ve sekme + `#` degerin parcasidir) ve sondaki bosluklar kesilir.
env_oku() {
    local satir deger
    satir=$(grep -E "^[[:space:]]*$1=" "$ENV_FILE" | tail -n 1 | tr -d '\r')
    deger=${satir#*=}
    case $deger in
        \"*) deger=${deger#\"}; deger=${deger%%\"*} ;;
        \'*) deger=${deger#\'}; deger=${deger%%\'*} ;;
        *)   deger=${deger%% \#*}
             deger=${deger%"${deger##*[! ]}"} ;;
    esac
    printf '%s' "$deger"
}

host=$(env_oku DIVISIMA_SMTP_HOST)
kullanici=$(env_oku DIVISIMA_SMTP_USER)
parola=$(env_oku DIVISIMA_SMTP_PASSWORD)
gonderen=$(env_oku DIVISIMA_SMTP_FROM)
alici=$(env_oku DIVISIMA_ALARM_EMAIL)
[ -n "$kullanici" ] || kullanici="no-reply@divisima.net"
[ -n "$gonderen" ] || gonderen="Divisima <no-reply@divisima.net>"

if [ -z "$alici" ] || [ -z "$host" ] || [ -z "$parola" ]; then
    echo "alarm-mail: DIVISIMA_ALARM_EMAIL / DIVISIMA_SMTP_HOST / DIVISIMA_SMTP_PASSWORD eksik - mail GONDERILMEDI" >&2
    exit 3
fi

# Zarf adresi: "Ad <adres>" biciminden yalniz adres.
zarf=$gonderen
case $gonderen in
    *\<*\>*) zarf=${gonderen#*<}; zarf=${zarf%%>*} ;;
esac

govde=$(cat)
konu_b64=$(printf '%s' "$konu" | base64 | tr -d '\n')
ileti=$(printf 'From: %s\nTo: %s\nSubject: =?UTF-8?B?%s?=\nDate: %s\nMIME-Version: 1.0\nContent-Type: text/plain; charset=UTF-8\nContent-Transfer-Encoding: 8bit\n\n%s\n' \
    "$gonderen" "$alici" "$konu_b64" "$(LC_ALL=C date -R)" "$govde")

# curl yapilandirma dizgesi: ters bolu ve cift tirnak kacirilir.
kacir() {
    local s=${1//\\/\\\\}
    printf '%s' "${s//\"/\\\"}"
}

printf '%s' "$ileti" | curl --silent --show-error --max-time 30 --ssl-reqd --crlf \
    --url "smtp://$host:$SMTP_PORT" \
    --mail-from "$zarf" --mail-rcpt "$alici" \
    --upload-file - \
    --config <(printf 'user = "%s"\n' "$(kacir "$kullanici:$parola")")
