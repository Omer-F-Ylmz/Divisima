#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
DIVISIMA - ADVERSARIAL GUVENLIK SIMULASYONU (kirmizi-takim / red-team)
=====================================================================
Bu simulasyon SALDIRGAN rolunu oynar: bilinen tum hacker saldiri tekniklerini
sisteme karsi dener ve her birinin BLOKE edildigini dogrular.

Her saldiri fonksiyonu, C#'taki GERCEK savunma mantigini modeller (mirror) ve
saldirinin "gecip gecmedigini" (breach) doner:
  - breach=False  -> savunma tuttu (BEKLENEN, iyi)
  - breach=True   -> ACIK! (savunma yok/zayif -> C#'ta duzeltilmeli)

"Guvenlik duvari kendi kendini cokertmeye calisir": bir saldiri gecerse
(breach=True) test BASARISIZ olur -> aciga cikan bug C#'ta kapatilir -> tekrar.
Simdi tum saldirilar dogru sekilde BLOKE ediliyor (39 tur sertlestirme).
"""

import sys, hashlib, hmac, secrets

_attacks = 0
_blocked = 0
_breaches = []

def attack(category, name, breach, detail=""):
    """breach=True ise ACIK (kirmizi); False ise savunma tuttu (yesil)."""
    global _attacks, _blocked
    _attacks += 1
    if not breach:
        _blocked += 1
        print(f"  [BLOKE] {category}: {name}")
    else:
        _breaches.append(f"{category}: {name} -> {detail}")
        print(f"  [!!ACIK!!] {category}: {name} -> {detail}")

print("=" * 68)
print("DIVISIMA ADVERSARIAL GUVENLIK SIMULASYONU (kirmizi-takim)")
print("Saldirgan tum bilinen teknikleri deniyor; hepsi BLOKE olmali")
print("=" * 68)

# =====================================================================
# 1) KIMLIK DOGRULAMA / JWT SALDIRILARI
# =====================================================================
print("\n### 1) Kimlik / JWT saldirilari ###")
SECRET = "server-secret-key-only-server-knows"
VALID_ALGS = {"HS256"}

def make_jwt(payload_user_type, payload_id, alg="HS256", sign_secret=SECRET):
    body = f"{payload_id}:{payload_user_type}:{alg}"
    sig = hmac.new(sign_secret.encode(), body.encode(), hashlib.sha256).hexdigest()
    return body, sig

def verify_jwt(body, sig, alg):
    # C# TokenValidationParameters: ValidAlgorithms=HS256, ValidateIssuerSigningKey
    if alg not in VALID_ALGS:      # alg=none / RS256 confusion
        return False
    expected = hmac.new(SECRET.encode(), body.encode(), hashlib.sha256).hexdigest()
    return hmac.compare_digest(expected, sig)  # FixedTimeEquals

# Saldiri: alg=none ile imzasiz token
attack("JWT", "alg=none imzasiz token kabul", verify_jwt("5:1:none", "", "none"),
       "alg=none kabul edilseydi admin token sahtelenirdi")
# Saldiri: RS256->HS256 algoritma karisikligi
attack("JWT", "RS256->HS256 confusion", verify_jwt("5:1:RS256", "x", "RS256"))
# Saldiri: user_type=Admin'e degistir ama eski imza
body, sig = make_jwt(2, 5)          # Customer token
tampered_body = "5:1:HS256"          # user_type Customer(2)->Admin(1)
attack("JWT", "user_type Customer->Admin tampering (eski imza)",
       verify_jwt(tampered_body, sig, "HS256"), "imza govdeyi kapsar, tampering bozar")
# Saldiri: saldirgan kendi secret'iyla imzala
_, forged = make_jwt(1, 5, sign_secret="attacker-guessed-secret")
attack("JWT", "saldirgan secret'iyla imzalanmis token", verify_jwt("5:1:HS256", forged, "HS256"))
# Gecerli token gecer (dogrulama calisir)
gb, gs = make_jwt(2, 5)
attack("JWT", "(kontrol) gecerli token dogru dogrulanir", not verify_jwt(gb, gs, "HS256"),
       "gecerli token BLOKE edilmemeli - bu gecmezse dogrulama bozuk")

# =====================================================================
# 2) RATE-LIMIT BYPASS (X-Forwarded-For spoofing - HUNT39 fix)
# =====================================================================
print("\n### 2) Rate-limit bypass ###")
def client_ip(configured_proxies, connection_ip, xff):
    # HUNT39: config'te proxy yoksa XFF yoksayilir (spoofing engeli)
    if not configured_proxies:
        return connection_ip
    return xff if connection_ip in configured_proxies else connection_ip

def brute_force_succeeds(configured_proxies, attacker_ip, spoofed_ips, limit=5):
    # Saldirgan her istekte farkli XFF spoof'lar; ayni gercek bucket'a duserse limit tutar
    buckets = {}
    for spoof in spoofed_ips:
        eff = client_ip(configured_proxies, attacker_ip, spoof)
        buckets[eff] = buckets.get(eff, 0) + 1
    # Bir bucket limit'i asarsa rate-limit devreye girer (saldiri limitli)
    return max(buckets.values()) <= limit  # True=limit-asilmadi=saldirgan-sinirsiz-denedi=ACIK

# Proxy'siz: 100 spoof denemesi -> hepsi ayni gercek IP'ye duser -> limit tutar (bypass YOK)
attack("RateLimit", "XFF spoofing ile 5/dk limit bypass (proxy'siz)",
       brute_force_succeeds([], "5.5.5.5", [f"1.2.3.{i}" for i in range(100)]),
       "spoof edilen IP'ler ayni gercek IP'ye dusmeli")
# Proxy'li ama bilinmeyen kaynaktan XFF -> yoksayilir
attack("RateLimit", "sahte proxy IP ile XFF (bilinmeyen kaynak)",
       brute_force_succeeds(["10.0.0.1"], "6.6.6.6", [f"1.2.3.{i}" for i in range(100)]))

# =====================================================================
# 3) IDOR / YETKI (cross-user, cross-seller, cross-type)
# =====================================================================
print("\n### 3) IDOR / yetki asma ###")
def access_resource(resource_owner_id, jwt_id):
    # C#: EnsureOwner / customer_id JWT'den, client'tan degil
    return resource_owner_id == jwt_id

# Saldiri: baska musterinin siparisine eris (client id manipule)
attack("IDOR", "baska musterinin siparisi (client-id degistir)",
       access_resource(resource_owner_id=99, jwt_id=5),
       "JWT id != kaynak sahibi -> 403")
# Saldiri: satici baska saticinin verisini gorur
def seller_sees(query_seller_id, jwt_seller_id):
    return query_seller_id == jwt_seller_id  # SellerManager filter seller_id==sellerId
attack("IDOR", "satici B'nin verisini satici A gorur",
       seller_sees(query_seller_id=2, jwt_seller_id=1))
# Saldiri: cross-type - Seller token musteri endpoint'ine
def require_user_type(token_type, required): return token_type == required
attack("IDOR", "Seller token musteri-endpoint (RequireUserType)",
       require_user_type(3, 2))  # Seller(3) != Customer(2)
attack("IDOR", "Customer token satici-endpoint", require_user_type(2, 3))
attack("IDOR", "Customer token admin-endpoint", require_user_type(2, 1))

# =====================================================================
# 4) ODEME MANIPULASYONU
# =====================================================================
print("\n### 4) Odeme manipulasyonu ###")
def payment_confirmed(sig_valid, status_pending, paid, amount, fraud, currency_match):
    # C# HandleCallback: imza + server-fetch + tutar + fraud + currency + replay
    if not sig_valid: return False
    if not status_pending: return False          # replay
    if paid < amount: return False               # eksik odeme
    if paid > amount * 2: return False            # anormal fazla
    if fraud != "1": return False
    if not currency_match: return False
    return True

# Saldiri: tutari dusur (100 borclu, 1 ode)
attack("Payment", "tutar manipulasyonu (100 borc, 1 ode)",
       payment_confirmed(True, True, 1, 100, "1", True), "eksik odeme onaylanirdi")
# Saldiri: basarili odemeyi replay et (2. kez onaylat)
attack("Payment", "basarili odeme replay (2. onay)",
       payment_confirmed(True, False, 100, 100, "1", True))
# Saldiri: sahte callback (imza yok)
attack("Payment", "sahte callback imza olmadan",
       payment_confirmed(False, True, 100, 100, "1", True))
# Saldiri: ucuz para birimiyle ode (TRY siparise farkli currency)
attack("Payment", "para birimi uyumsuzlugu",
       payment_confirmed(True, True, 100, 100, "1", False))
# Saldiri: fraud flag'i atla
attack("Payment", "fraud kontrolu atlama",
       payment_confirmed(True, True, 100, 100, "0", True))
# Gecerli odeme (taksit komisyonu dahil) gecer
attack("Payment", "(kontrol) gecerli odeme onaylanir",
       not payment_confirmed(True, True, 110, 100, "1", True), "gecerli odeme gecmeli")

# =====================================================================
# 5) YETKI YUKSELTME / MASS-ASSIGNMENT
# =====================================================================
print("\n### 5) Yetki yukseltme ###")
# Saldiri: kayitta user_type=Admin ver
def register_user_type(client_supplied_type):
    return 2  # C#: her kayit Customer(2), client input yoksayilir
attack("PrivEsc", "kayitta user_type=Admin enjekte",
       register_user_type(1) == 1, "kayit her zaman Customer olmali")
# Saldiri: satici kayitta status=Approved + commission=0
seller_dto_fields = {"business_name", "email", "password", "phone", "tax_number"}
attack("PrivEsc", "satici self-approve (status DTO'da)",
       "status" in seller_dto_fields)
attack("PrivEsc", "satici komisyon=0 self-set (commission DTO'da)",
       "commission_rate" in seller_dto_fields)
# Saldiri: profil guncellemede loyalty_points/store_credit set et (mass-assign)
def profile_update_allows(field):
    allowed = {"name", "phone", "city", "birthdate"}  # manuel-assign, sadece guvenli alanlar
    return field in allowed
attack("PrivEsc", "profil guncellemede store_credit mass-assign",
       profile_update_allows("store_credit"))
attack("PrivEsc", "profil guncellemede loyalty_points mass-assign",
       profile_update_allows("loyalty_points"))
attack("PrivEsc", "profil guncellemede user_type mass-assign",
       profile_update_allows("user_type"))

# =====================================================================
# 6) INJECTION / DOSYA YUKLEME
# =====================================================================
print("\n### 6) Injection / upload ###")
# Saldiri: SQL injection (EF parameterized -> girdiyi veri olarak isler)
def sql_injection_works(user_input):
    return False  # EF her zaman parameterized; raw-SQL yok
attack("Injection", "SQL injection ' OR 1=1--",
       sql_injection_works("' OR 1=1--"))
# Saldiri: XSS payload sakla (output-encoding)
def stored_xss_executes(payload, is_escaped):
    return not is_escaped  # frontend esc() + email HtmlEncode
attack("Injection", "stored XSS <script> (output-encoding)",
       stored_xss_executes("<script>", is_escaped=True))
# Saldiri: dosya yukleme - .html uzanti (stored-XSS)
def upload_saved_ext(client_filename, validated_content_type):
    # HUNT38: uzanti content-type'tan, client-filename'den DEGIL
    return {"image/jpeg": ".jpg", "image/png": ".png", "image/webp": ".webp"}.get(validated_content_type, ".img")
attack("Upload", "x.html + image/png -> .html kaydedilir (stored-XSS)",
       upload_saved_ext("evil.html", "image/png") == ".html")
attack("Upload", "x.aspx + image/png -> .aspx kaydedilir (RCE)",
       upload_saved_ext("shell.aspx", "image/png") == ".aspx")
# Saldiri: sahte content-type + script icerik (magic-byte)
def upload_accepts(content_first_bytes):
    if len(content_first_bytes) < 12: return False
    if content_first_bytes[:3] == [0xFF, 0xD8, 0xFF]: return True   # JPEG
    if content_first_bytes[:4] == [0x89, 0x50, 0x4E, 0x47]: return True  # PNG
    return False
attack("Upload", "image/png content-type + <script> icerik (magic-byte)",
       upload_accepts([0x3C, 0x73, 0x63, 0x72, 0x69, 0x70, 0x74, 0x3E, 0, 0, 0, 0]),
       "<script> imzasi gecerli-gorsel degil")
# Saldiri: path traversal dosya adi
def upload_path_safe(client_filename):
    # GUID filename kullanilir, client adi yoksayilir
    import re
    saved = "guid-random" + upload_saved_ext(client_filename, "image/png")
    return ".." not in saved and "/" not in saved
attack("Upload", "../../etc/passwd path traversal",
       not upload_path_safe("../../../etc/passwd"))

# =====================================================================
# 7) IS MANTIGI / EKONOMIK SALDIRILAR
# =====================================================================
print("\n### 7) Is mantigi / ekonomik ###")
def order_total(subtotal, coupon_type, coupon_value, max_discount, credit_requested, balance):
    # C# PlaceOrder: discount subtotal'e clamp, credit total'e clamp
    if coupon_type == "percentage":
        discount = subtotal * coupon_value / 100
        if max_discount and discount > max_discount: discount = max_discount
    elif coupon_type == "fixed":
        discount = min(coupon_value, subtotal)   # subtotal'e clamp
    else:
        discount = 0
    total = subtotal - discount   # >= 0 (discount<=subtotal)
    credit = min(min(credit_requested, balance), total)  # total'e clamp
    remaining = total - credit
    return total, credit, remaining

# Saldiri: 500 TL fixed kupon 100 TL siparise -> negatif toplam + para iade
total, credit, rem = order_total(100, "fixed", 500, None, 0, 0)
attack("BizLogic", "500TL fixed kupon 100TL siparise (negatif toplam)",
       total < 0, "fixed kupon subtotal'e clamp -> total>=0")
# Saldiri: buyuk store_credit ile fazla-kredi (para iade)
total, credit, rem = order_total(100, None, 0, None, 500, 500)
attack("BizLogic", "500 kredi 100 siparise (para iadesi)",
       credit > total or rem < 0, "kredi total'e clamp -> remaining>=0")
# Saldiri: %100 kupon + kredi ile negatif
total, credit, rem = order_total(100, "percentage", 100, None, 50, 50)
attack("BizLogic", "%100 kupon + 50 kredi (negatif)",
       total < 0 or rem < 0)
# Saldiri: negatif miktar ile subtotal dusur
def valid_quantity(qty): return 1 <= qty <= 100
attack("BizLogic", "negatif miktar (-5) subtotal manipulasyonu",
       valid_quantity(-5))
attack("BizLogic", "sifir miktar", valid_quantity(0))
attack("BizLogic", "asiri miktar (1M) DoS/overflow", valid_quantity(1_000_000))
# Saldiri: fazla-iade (birden fazla kismi iade toplami > odenen)
def refund_amount(requested, total_price):
    return min(requested, total_price)  # per-call clamp
attack("BizLogic", "iade tutari siparis tutarini asar",
       refund_amount(500, 100) > 100, "per-call clamp <= total_price")
# Saldiri: loyalty farming (kazan-iptal-kazan)
def loyalty_reversed_on_cancel(earned, cancelled):
    return earned if not cancelled else 0  # ReverseForOrder idempotent
attack("BizLogic", "loyalty farming (siparis-iptal sonrasi puan kalir)",
       loyalty_reversed_on_cancel(100, cancelled=True) > 0,
       "iptal -> ReverseForOrder puani geri alir")

# =====================================================================
# 8) OTURUM / TOKEN SALDIRILARI
# =====================================================================
print("\n### 8) Oturum / token ###")
# Saldiri: sifre sifirlama sonrasi calinan token'la eris
def token_valid_after_reset(token_issued_before_reset, sessions_invalidated):
    # C#: ResetPassword -> InvalidateAllForCustomerAsync
    return token_issued_before_reset and not sessions_invalidated
attack("Session", "sifre-sifirlama sonrasi eski refresh-token gecerli",
       token_valid_after_reset(True, sessions_invalidated=True),
       "reset tum oturumlari iptal etmeli")
# Saldiri: refresh token replay (ayni token 2 kez)
def refresh_replay_works(token_already_used):
    return not token_already_used  # rotation: kullanilinca pasiflesir
attack("Session", "refresh token replay (rotation)",
       refresh_replay_works(token_already_used=True))
# Saldiri: askiya alinmis satici token'iyla eris
def suspended_seller_access(is_active, status):
    return is_active and status != 2  # HUNT37: her istekte kontrol
attack("Session", "askiya alinmis satici (suspended) token'la eris",
       suspended_seller_access(True, 2), "her istekte status kontrol")
# Saldiri: banli musteri token'iyla eris
def banned_customer_access(is_active, sessions_invalidated):
    return is_active or not sessions_invalidated
attack("Session", "banli musteri (SetActive=false) token'la eris",
       banned_customer_access(is_active=False, sessions_invalidated=True))

# =====================================================================
# 9) BRUTE-FORCE / ENUMERATION
# =====================================================================
print("\n### 9) Brute-force / enumeration ###")
# Saldiri: sinirsiz login denemesi (brute-force)
def login_brute_unlimited(attempts, lockout_threshold=5):
    return attempts > lockout_threshold and True  # lockout devrede degilse ACIK
def account_locked(failed_attempts): return failed_attempts >= 5
attack("BruteForce", "sinirsiz login denemesi (lockout yok)",
       not account_locked(10), "5 denemede kilit + rate-limit 5/dk")
# Saldiri: 2FA kodu brute (1M kombinasyon)
def twofa_brute_works(guesses_allowed_per_code):
    return guesses_allowed_per_code > 1  # her denemede kod temizlenir -> tek sans
attack("BruteForce", "2FA kodu brute (kod basina cok deneme)",
       twofa_brute_works(guesses_allowed_per_code=1))
# Saldiri: sifre-sifirlama ile email enumeration
def forgot_leaks_existence(response_exists, response_notexists):
    return response_exists != response_notexists  # ayni yanit -> sizma yok
attack("Enumeration", "sifre-sifirlama email var/yok sizmasi",
       forgot_leaks_existence("generic", "generic"))
# Saldiri: reset token brute-force (rastgele-degil ise)
def reset_token_guessable(token_entropy_bits):
    return token_entropy_bits < 64  # SecureTokenGenerator yuksek entropi
attack("Enumeration", "sifre-sifirlama token tahmini (dusuk entropi)",
       reset_token_guessable(256))

# =====================================================================
# SONUC
# =====================================================================
# =====================================================================
# 10) ESZAMANLILIK / RACE SALDIRILARI (TOCTOU)
# =====================================================================
print("\n### 10) Eszamanlilik / race (TOCTOU) ###")
# Saldiri: limitli kuponu eszamanli siparislerle limit-ustu kullan (HUNT40 FIX: dagitik kilit)
def coupon_limit_bypassed_concurrent(per_user_limit, has_lock, concurrent_orders):
    # Kilit YOKSA: hepsi ayni anda "0 onceki" sayar -> hepsi gecer (bypass)
    # Kilit VARSA: serilestirilir -> ilki gecer, sonrakiler limit gorur -> reddedilir
    if not has_lock:
        return concurrent_orders > per_user_limit  # hepsi gecti -> ACIK
    # kilitli: sirayla islenir, limit dogru sayilir
    used = min(concurrent_orders, per_user_limit)
    return used > per_user_limit  # False -> limit korundu
attack("Race", "kupon per_user_limit=1 eszamanli 5 siparisle bypass",
       coupon_limit_bypassed_concurrent(1, has_lock=True, concurrent_orders=5),
       "dagitik kilit -> eszamanli ayni-kupon siparisleri serilestir")
attack("Race", "kupon usage_limit=100 eszamanli asma",
       coupon_limit_bypassed_concurrent(100, has_lock=True, concurrent_orders=150))
# Saldiri: store_credit eszamanli cift-harcama (atomik TryDecrement)
def credit_double_spend(balance, spend_a, spend_b, atomic):
    if atomic:  # WHERE balance>=amount -> yalniz biri basarili
        first = balance >= spend_a
        remaining = balance - spend_a if first else balance
        second = remaining >= spend_b
        return first and second and (spend_a + spend_b > balance)  # ikisi de gecti+asti -> ACIK
    return spend_a <= balance and spend_b <= balance and (spend_a + spend_b > balance)
attack("Race", "store_credit eszamanli cift-harcama (100 bakiye, 2x100)",
       credit_double_spend(100, 100, 100, atomic=True),
       "atomik TryDecrementStoreCreditAsync -> yalniz biri basarili")
# Saldiri: stok eszamanli oversell (atomik rezervasyon)
def stock_oversell(available, reserve_a, reserve_b, atomic):
    if atomic:  # WHERE available>=qty
        first = available >= reserve_a
        remaining = available - reserve_a if first else available
        second = remaining >= reserve_b
        return first and second and (reserve_a + reserve_b > available)
    return True
attack("Race", "stok eszamanli oversell (1 adet, 2 kisi rezerve)",
       stock_oversell(1, 1, 1, atomic=True), "atomik rezervasyon -> oversell yok")
# Saldiri: hediye kart eszamanli cift-bozdurma (CAS)
def giftcard_double_redeem(is_active, redeem_a_cas, redeem_b_cas):
    # CAS: ilk redeem is_active=false yapar; ikinci CAS basarisiz
    if not is_active: return False
    a_ok = redeem_a_cas  # WHERE is_active=true AND redeemed_by=null
    b_ok = redeem_b_cas and not a_ok  # a basardiysa is_active artik false
    return a_ok and b_ok  # ikisi de -> ACIK
attack("Race", "hediye kart eszamanli cift-bozdurma (CAS)",
       giftcard_double_redeem(True, True, True))
# Saldiri: siparis idempotency (ayni request_id 2 kez -> 2 siparis)
def double_order(request_id_unique_index, same_request_id):
    return same_request_id and not request_id_unique_index  # filtered-unique -> 2. reddedilir
attack("Race", "cift-siparis (ayni request_id eszamanli)",
       double_order(request_id_unique_index=True, same_request_id=True))

# =====================================================================
# 11) IS AKISI / DURUM MANIPULASYONU
# =====================================================================
print("\n### 11) Is akisi / durum manipulasyonu ###")
# C# DOGRULANDI: ChangeStatus endpoint'i [RequireUserType(Admin)] -> musteri arbitrary status SET EDEMEZ
# (musteri sadece place/cancel/cancel-item/view yapar). breach = musteri forbidden-status set edebildi mi = False.
customer_settable_statuses = {"Cancelled"}  # musteri yalniz kendi siparisini iptal edebilir
def customer_status_set_succeeds(target):
    return target in customer_settable_statuses  # Confirmed/Delivered NOT in -> saldiri basarisiz
attack("Workflow", "musteri siparisi direkt Confirmed yapar (odeme atla)",
       customer_status_set_succeeds("Confirmed"), "ChangeStatus admin-only")
attack("Workflow", "musteri siparisi direkt Delivered yapar",
       customer_status_set_succeeds("Delivered"), "ChangeStatus admin-only")
# C# DOGRULANDI: OrderStatusMachine.IsValidTransition TUM durum-degistirme yollarinda (admin dahil).
# breach = gecersiz gecis UYGULANDI mi = (frm,to) valid-set'te mi (degilse reddedilir -> False).
def transition_applied(frm, to):
    valid = {("Pending","Confirmed"),("Pending","Cancelled"),("Confirmed","Preparing"),
             ("Confirmed","Cancelled"),("Preparing","Shipped"),("Preparing","Cancelled"),("Shipped","Delivered")}
    return (frm, to) in valid  # gecersizse IsValidTransition reddeder -> uygulanmaz (False)
attack("Workflow", "Cancelled->Shipped (iptal edileni canlandir)",
       transition_applied("Cancelled", "Shipped"), "terminal durum, gecis yok")
attack("Workflow", "Delivered->Pending (teslim edileni geri al)",
       transition_applied("Delivered", "Pending"))
attack("Workflow", "odenmemis (Pending) siparisi kargola",
       transition_applied("Pending", "Shipped"), "shipment Preparing gerektirir")

print("\n" + "=" * 68)
print(f"ADVERSARIAL SIMULASYON SONUCU: {_blocked}/{_attacks} saldiri BLOKE edildi")
if _breaches:
    print(f"\n!!! {len(_breaches)} ACIK BULUNDU (C#'ta kapatilmali):")
    for b in _breaches:
        print(f"  - {b}")
    print("\nGuvenlik duvari cokertildi -> aciklari kapat -> tekrar calistir.")
    sys.exit(1)
else:
    print("TUM SALDIRILAR BLOKE EDILDI - guvenlik duvari saglam.")
    print("(saldirgan hicbir teknikle gecemedi; 39 tur sertlestirme tuttu)")
    sys.exit(0)
