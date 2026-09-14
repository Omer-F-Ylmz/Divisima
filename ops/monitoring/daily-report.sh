#!/usr/bin/env bash
# MON-1 / D3 + D4 - GUNLUK KAYNAK ve HATA OZETI (cron 07:00 UTC; ops/monitoring.md)
#
# Her gun TEK mail. Esik asilirsa konu "Divisima ALARM - gunluk ozet: N sorun", asilmazsa
# "Divisima günlük özet - normal". Normal gunde de mail GIDER: gelmeyen ozet, alarm kanalinin
# (cron / SMTP / betik) kendisinin bozuldugunun TEK isaretidir.
#
# OLCUMLER:
#   D3 disk      : kok dosya sistemi doluluk yuzdesi                (esik DISK_ESIK, %85)
#   D3 DB boyutu : Divisima* veri dosyalari (.mdf/.ndf) toplam MB   (esik DB_ESIK_MB, 8192;
#                  Express siniri 10 GB VERI dosyasi icindir, log dosyasi sayilmaz)
#   D3 konteyner : projede calismayan ya da unhealthy konteyner
#   D3 yedek     : bugun (UTC) uretilmis sifreli yedek (divisima-*.bak.age)
#   D4 hata      : API log dosyalarinda son 24 saatteki [ERR] ve [FTL] satirlari
# Olculemeyen bir kalem SORUN sayilir - "olcemedim" sessizce "normal" okunmaz.
#
# ESIK TESTI: esikler ortamdan ezilir, ornek: DISK_ESIK=1 bash daily-report.sh
set -u

DIVISIMA_DIR=${DIVISIMA_DIR:-/opt/divisima}
COMPOSE_PROJE=${COMPOSE_PROJE:-divisima}
DISK_ESIK=${DISK_ESIK:-85}
DB_ESIK_MB=${DB_ESIK_MB:-8192}
DB_VERI_DIZIN=${DB_VERI_DIZIN:-/var/lib/docker/volumes/${COMPOSE_PROJE}_mssql_data/_data/data}
DB_DOSYA_ONEKI=${DB_DOSYA_ONEKI:-Divisima}
LOG_DIZIN=${LOG_DIZIN:-/var/lib/docker/volumes/${COMPOSE_PROJE}_logs_data/_data}
YEDEK_DIZIN=${YEDEK_DIZIN:-/var/backups/divisima}
YEDEK_DESEN=${YEDEK_DESEN:-divisima-*.bak.age}
MAIL_CMD=${MAIL_CMD:-$DIVISIMA_DIR/ops/monitoring/alarm-mail.sh}

sorun=0
satirlar=""
ekle() { satirlar="${satirlar}$1"$'\n'; }
sorun_ekle() { sorun=$((sorun + 1)); ekle "[SORUN] $1"; }

# D3 - disk
disk=$(df -P / 2>/dev/null | awk 'NR==2 { gsub("%", "", $5); print $5 }')
if [ -z "$disk" ]; then
    sorun_ekle "Disk /: OLCULEMEDI"
elif [ "$disk" -ge "$DISK_ESIK" ]; then
    sorun_ekle "Disk /: %$disk (esik %$DISK_ESIK)"
else
    ekle "[OK]    Disk /: %$disk (esik %$DISK_ESIK)"
fi

# D3 - DB veri dosyasi boyutu
if [ -d "$DB_VERI_DIZIN" ]; then
    # Ad CANLIDA olculdu: Divisima.mdf (+ Divisima_log.ldf). master/model/msdb ayni dizinde, sayilmaz.
    db_bayt=$(find "$DB_VERI_DIZIN" -maxdepth 1 -type f \( -iname "$DB_DOSYA_ONEKI*.mdf" -o -iname "$DB_DOSYA_ONEKI*.ndf" \) -printf '%s\n' 2>/dev/null \
        | awk '{ t += $1; n++ } END { if (n) printf "%d", t }')
fi
if [ -z "${db_bayt:-}" ]; then
    sorun_ekle "DB veri dosyasi: OLCULEMEDI ($DB_VERI_DIZIN)"
else
    db_mb=$((db_bayt / 1024 / 1024))
    if [ "$db_mb" -ge "$DB_ESIK_MB" ]; then
        sorun_ekle "DB veri dosyasi: $db_mb MB (esik $DB_ESIK_MB MB; Express siniri 10240 MB)"
    else
        ekle "[OK]    DB veri dosyasi: $db_mb MB (esik $DB_ESIK_MB MB)"
    fi
fi

# D3 - konteynerler
if kaplar=$(docker ps -a --filter "label=com.docker.compose.project=$COMPOSE_PROJE" --format '{{.Names}} {{.State}} {{.Status}}' 2>/dev/null); then
    kotu=$(printf '%s\n' "$kaplar" | awk 'NF && ($2 != "running" || $0 ~ /\(unhealthy\)/)')
    if [ -z "$kaplar" ]; then
        sorun_ekle "Konteynerler: projede ($COMPOSE_PROJE) konteyner BULUNAMADI"
    elif [ -n "$kotu" ]; then
        sorun_ekle "Konteynerler: calismayan/unhealthy -> $(printf '%s' "$kotu" | awk '{print $1}' | paste -sd, -)"
    else
        ekle "[OK]    Konteynerler: $(printf '%s\n' "$kaplar" | awk 'NF' | wc -l) konteyner calisiyor, unhealthy yok"
    fi
else
    sorun_ekle "Konteynerler: docker SORGULANAMADI"
fi

# D3 - bugunku yedek
bugun=$(date -u '+%Y-%m-%d')
yedek=$(find "$YEDEK_DIZIN" -maxdepth 1 -type f -name "$YEDEK_DESEN" -newermt "$bugun 00:00:00 UTC" 2>/dev/null | wc -l)
if [ "$yedek" -ge 1 ]; then
    ekle "[OK]    Yedek: bugun $yedek sifreli yedek ($YEDEK_DIZIN)"
else
    sorun_ekle "Yedek: bugun ($bugun UTC) $YEDEK_DESEN URETILMEDI ($YEDEK_DIZIN)"
fi

# D4 - son 24 saatin ERR/FTL sayisi. Serilog DosyaSablonu: "yyyy-MM-dd HH:mm:ss.fff zzz [LVL] ..."
# Zaman damgasi konteyner yerel saatidir (UTC); kesim de UTC. Yalniz son 48 saatte
# degismis dosyalar okunur (14 gunluk saklamanin tamami taranmaz).
taze_log=0
[ -d "$LOG_DIZIN" ] && taze_log=$(find "$LOG_DIZIN" -maxdepth 1 -type f -name 'divisima-*.log' -mmin -2880 2>/dev/null | wc -l)
if [ -d "$LOG_DIZIN" ] && [ "$taze_log" -eq 0 ]; then
    # Dizin var ama son 48 saatte yazilmis dosya yok: sayim "0" DEGIL, OLCULEMEDI. Uygulama hic log
    # yazmiyorsa (MON-1'de canlida olculdu: volume root:root, Serilog 7 gun yazamadi) tek isaret budur.
    sorun_ekle "API log: son 48 saatte log dosyasi YOK ($LOG_DIZIN) - hata sayimi OLCULEMEDI"
elif [ -d "$LOG_DIZIN" ]; then
    kesim=$(date -u -d '24 hours ago' '+%Y-%m-%d %H:%M:%S')
    sayim=$(find "$LOG_DIZIN" -maxdepth 1 -type f -name 'divisima-*.log' -mmin -2880 -print0 2>/dev/null \
        | xargs -0 -r cat 2>/dev/null \
        | awk -v k="$kesim" 'substr($0, 1, 19) >= k && substr($0, 1, 2) == "20" { if ($4 == "[ERR]") e++; else if ($4 == "[FTL]") f++ } END { printf "%d %d", e, f }')
    ekle "[BILGI] API log son 24 saat: ERROR ${sayim% *} · FATAL ${sayim#* }"
else
    sorun_ekle "API log: dizin OKUNAMADI ($LOG_DIZIN) - hata gorunurlugu YOK"
fi

if [ "$sorun" -gt 0 ]; then
    konu="Divisima ALARM - günlük özet: $sorun sorun"
else
    konu="Divisima günlük özet - normal"
fi
govde="Divisima gunluk ozet - $(date -u '+%Y-%m-%d %H:%M') UTC - sunucu $(hostname)
${satirlar}
Esikler ve susturma usulu: ops/monitoring.md"

printf '%s\n%s\n' "$konu" "$govde"
printf '%s\n' "$govde" | bash "$MAIL_CMD" "$konu"
