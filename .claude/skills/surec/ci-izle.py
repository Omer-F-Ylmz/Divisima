# CI izleyici (surec skill). Kullanim: python .claude/skills/surec/ci-izle.py <SHA> [--nabiz 300] [--azami-tur 36]
# SHA bazli; tur basina TEK cagri (run listesi), bitince AYNI turda job + adim + annotation.
# JSON ayristirir: grep ile job-id cikarma deseni EMEKLI (MON-1 ve SADE-3'te iki kez bos dondu).
# Cikis: 0 iki workflow success · 1 en az biri success degil · 2 azami turda run yok/bitmedi · 3 HTTP/kota hatasi.
import json, sys, time, urllib.error, urllib.request

DEPO = "https://api.github.com/repos/Omer-F-Ylmz/Divisima"
IS_AKISLARI = ("CI - Build & Test", "Security CI")   # Dependabot run'lari beklenmez


def al(url):
    istek = urllib.request.Request(url, headers={"Accept": "application/vnd.github+json",
                                                 "User-Agent": "divisima-ci-izle"})
    try:
        with urllib.request.urlopen(istek, timeout=30) as yanit:
            return json.load(yanit)
    except urllib.error.HTTPError as h:
        kalan = h.headers.get("X-RateLimit-Remaining")
        print(f"HTTP {h.code} {url} kota_kalan={kalan} - YENIDEN DENENMEZ (kota yandiysa beklenir)")
        sys.exit(3)


def arg(ad, varsayilan):
    return int(sys.argv[sys.argv.index(ad) + 1]) if ad in sys.argv else varsayilan


def main():
    sys.stdout.reconfigure(encoding="utf-8")   # adim adlari Turkce karakter tasir
    if len(sys.argv) < 2 or len(sys.argv[1]) < 7:
        print("kullanim: ci-izle.py <SHA> [--nabiz 300] [--azami-tur 36]")
        return 3
    sha, nabiz, azami = sys.argv[1], arg("--nabiz", 300), arg("--azami-tur", 36)
    nabiz = max(nabiz, 300) if azami > 1 else nabiz   # izleyici adabi: nabiz >= 300 sn

    for tur in range(1, azami + 1):
        runlar = [r for r in al(f"{DEPO}/actions/runs?head_sha={sha}&per_page=50")["workflow_runs"]
                  if r["name"] in IS_AKISLARI]
        biten = [r for r in runlar if r["status"] == "completed"]
        print(f"TUR {tur} {time.strftime('%H:%M:%S', time.gmtime())}Z sha={sha[:7]} run={len(runlar)} biten={len(biten)}", flush=True)
        if len(runlar) >= len(IS_AKISLARI) and len(biten) == len(runlar):
            break
        if tur < azami:
            time.sleep(nabiz)
    else:
        print("RUN YOK ya da BITMEDI (azami tur doldu)")
        return 2

    hepsi_basarili = True
    for r in sorted(runlar, key=lambda x: x["name"]):
        print(f"RUN {r['id']} | {r['name']} | {r['conclusion']} | {r['html_url']}")
        hepsi_basarili &= r["conclusion"] == "success"
        for j in al(f"{DEPO}/actions/runs/{r['id']}/jobs?per_page=100")["jobs"]:
            adimlar = j.get("steps") or []
            kotu = [s["name"] for s in adimlar if s.get("conclusion") not in ("success", "skipped")]
            ann = al(f"{DEPO}/check-runs/{j['id']}/annotations?per_page=100")
            say = {}
            for a in ann:
                say[a["annotation_level"]] = say.get(a["annotation_level"], 0) + 1
            print(f"  JOB {j['id']} | {j['name']} | {j['conclusion']} | adim={len(adimlar)} basarisiz_adim={len(kotu)}"
                  f" | ann failure={say.get('failure', 0)} warning={say.get('warning', 0)} notice={say.get('notice', 0)}")
            for ad in kotu:
                print(f"    ADIM-BASARISIZ {ad}")
            # secret-scan ANNOTATION'DAN DEGIL ADIM SONUCUNDAN okunur (surec kurali)
            for s in adimlar:
                if "Gitleaks" in s["name"]:
                    print(f"    ADIM {s['name']} = {s.get('conclusion')}")
            for a in ann:
                if a["annotation_level"] == "failure":
                    print(f"    ANN-FAILURE {a['path']}:{a.get('start_line')} {a['message'][:160]}")
    return 0 if hepsi_basarili else 1


if __name__ == "__main__":
    sys.exit(main())
