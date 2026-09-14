#!/usr/bin/env bash
# MON-1 / D2 - API AYAKTA KALMA BEKCISI (cron, 5 dk; ops/monitoring.md)
#
# Her tur `/health/ready` (DB dahil bagimliliklar) yoklanir. Davranis:
#   - 200 disi yanit ARDISIK sayilir; ESIK'e (3) ulasildiginda: alarm maili + TEK yeniden
#     baslatma denemesi.
#   - ayni kesintide sonraki turlar YENIDEN BASLATMAZ; saatte bir (12 tur) "hala erisilemiyor"
#     uyarisi gider. Gerekce: surekli restart dongusu, kok sebep DB/disk ise hicbir sey
#     cozmez ve loglari kaybettirir; karar insana birakilir.
#   - kesinti sonrasi ilk 200'de "normale dondu" maili; sayac sifirlanir.
# Tek tur 200 disi (ornegin dagitim sirasinda) alarm URETMEZ - esik bunun icin var.
set -u

DIVISIMA_DIR=${DIVISIMA_DIR:-/opt/divisima}
API_URL=${API_URL:-http://127.0.0.1:5000/health/ready}
STATE_DIR=${STATE_DIR:-/var/lib/divisima-monitoring}
LOG_FILE=${LOG_FILE:-/var/log/divisima-watchdog.log}
ESIK=${WATCHDOG_ESIK:-3}
UYARI_ARALIGI=${WATCHDOG_UYARI_ARALIGI:-12}
MAIL_CMD=${MAIL_CMD:-$DIVISIMA_DIR/ops/monitoring/alarm-mail.sh}

mkdir -p "$STATE_DIR"
DURUM="$STATE_DIR/watchdog.state"

# Ust uste binen turlar sayaci bozmasin (flock yoksa kilitsiz devam - tek cron satiri var).
if command -v flock >/dev/null 2>&1; then
    exec 9>"$STATE_DIR/watchdog.lock"
    flock -n 9 || exit 0
fi

log() { printf '%s %s\n' "$(date -u '+%Y-%m-%dT%H:%M:%SZ')" "$*" >> "$LOG_FILE"; }

mail_at() { # $1 konu, $2 govde
    if ! printf '%s\n' "$2" | bash "$MAIL_CMD" "$1" >> "$LOG_FILE" 2>&1; then
        log "MAIL GONDERILEMEDI: $1"
    fi
}

ARDISIK=0
# Durum dosyasini yalniz bu betik yazar ve yalniz iki tamsayi tasir; yine de bicim dogrulanir.
if [ -r "$DURUM" ]; then
    oku=$(sed -n 's/^ARDISIK=\([0-9][0-9]*\)$/\1/p' "$DURUM")
    [ -n "$oku" ] && ARDISIK=$oku
fi

kod=$(curl -s -o /dev/null -w '%{http_code}' --max-time 10 "$API_URL" 2>/dev/null)
[ -n "$kod" ] || kod=000

if [ "$kod" = "200" ]; then
    if [ "$ARDISIK" -ge "$ESIK" ]; then
        log "IYILESTI kod=200 onceki_ardisik=$ARDISIK"
        mail_at "Divisima ALARM ÇÖZÜLDÜ - API normale döndü" \
            "API /health/ready yeniden 200 donuyor. Kesinti suresi (tur): $ARDISIK x 5 dk. Sunucu: $(hostname)"
    fi
    ARDISIK=0
else
    ARDISIK=$((ARDISIK + 1))
    log "HATA kod=$kod ardisik=$ARDISIK"
    if [ "$ARDISIK" -eq "$ESIK" ]; then
        if docker compose --project-directory "$DIVISIMA_DIR" -f "$DIVISIMA_DIR/docker-compose.prod.yml" restart api >> "$LOG_FILE" 2>&1; then
            sonuc="yeniden baslatma komutu BASARILI"
        else
            sonuc="yeniden baslatma komutu BASARISIZ"
        fi
        log "YENIDEN_BASLATMA $sonuc"
        mail_at "Divisima ALARM - API sağlık kontrolü başarısız" \
            "API /health/ready $ARDISIK ardisik turdur 200 donmuyor (son kod: $kod). TEK yeniden baslatma denendi: $sonuc. Bu kesintide bir daha denenmeyecek. Sunucu: $(hostname). Usul: ops/monitoring.md"
    elif [ "$ARDISIK" -gt "$ESIK" ] && [ $(( (ARDISIK - ESIK) % UYARI_ARALIGI )) -eq 0 ]; then
        mail_at "Divisima ALARM - API hâlâ erişilemiyor" \
            "API /health/ready $ARDISIK ardisik turdur 200 donmuyor (son kod: $kod). Otomatik yeniden baslatma YAPILMADI (kesinti basina bir kez). Sunucu: $(hostname)"
    fi
fi

printf 'ARDISIK=%s\n' "$ARDISIK" > "$DURUM"
