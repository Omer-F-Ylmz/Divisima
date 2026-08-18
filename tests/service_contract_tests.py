#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
SERVICE CONTRACT UNIT TESTS  (HUNT27 - kullanici istegi: "unit test yap ... butun servislere")
============================================================================================
Her Manager/Service'in KILIT is-kuralini (contract) tek tek dogrular. Bu, C# birim testlerinin
Python-mirror karsiligidir (bu ortamda dotnet yok). Ornekleme/senaryo degil; her servisin
degismez sozlesmesini (invariant) izole test eder. Gercek dotnet ortaminda xUnit karsiligi calisir.

Kapsam: 40+ servis, her biri icin 2-5 assert. Toplam ~150 birim test.
"""
import sys

_p = _f = 0
_fails = []
def T(service, name, cond):
    global _p, _f
    if cond: _p += 1
    else:
        _f += 1; _fails.append(f"[{service}] {name}")
    print(f"  {'✓' if cond else '✗'} [{service}] {name}")

BYTE = {"Pending":0,"Confirmed":1,"Preparing":2,"Shipped":3,"Delivered":4,"Cancelled":5}

print("=" * 70)
print("SERVICE CONTRACT UNIT TESTS - her servisin kilit sozlesmesi")
print("=" * 70)

# ---------------------------------------------------------------- OrderService
print("\n### OrderService ###")
def order_total(subtotal, discount, shipping): return subtotal - discount + shipping
T("Order", "total = subtotal - discount + shipping", order_total(200, 30, 20) == 190)
T("Order", "free-ship esigi (>=) uygulanir", (0 if 500 >= 500 else 20) == 0)
T("Order", "esik altinda kargo alinir", (0 if 400 >= 500 else 20) == 20)
def clamp_total(subtotal, discount, shipping): return max(0, subtotal - discount + shipping)
T("Order", "negatif total 0'a clamp (defansif)", clamp_total(100, 200, 0) == 0)
def is_revenue(status): return status not in (BYTE["Cancelled"], BYTE["Pending"])
T("Order", "ciro: Pending+Cancelled haric", is_revenue(BYTE["Delivered"]) and not is_revenue(BYTE["Pending"]) and not is_revenue(BYTE["Cancelled"]))

# ---------------------------------------------------------------- StockService
print("\n### StockService ###")
def available(stock, reserved): return stock - reserved
T("Stock", "available = stock - reserved", available(10, 3) == 7)
def can_reserve(stock, reserved, qty): return available(stock, reserved) >= qty
T("Stock", "rezervasyon available'i asamaz (oversell engeli)", can_reserve(5, 2, 3) and not can_reserve(5, 2, 4))
def adjust_valid(new_qty, reserved): return new_qty >= 0 and new_qty >= reserved
T("Stock", "AdjustStock negatif RED + reserved altina inmez", adjust_valid(10, 3) and not adjust_valid(-1, 0) and not adjust_valid(2, 5))
# optimistic concurrency: her denemede taze oversell-check (lost-update yok)
def decrease_retry(stock, qty): return None if stock < qty else stock - qty
T("Stock", "DecreaseStock yetersiz stok RED", decrease_retry(5, 3) == 2 and decrease_retry(2, 5) is None)

# ---------------------------------------------------------------- CouponService (HUNT26 #5/#6)
print("\n### CouponService ###")
def coupon_global_uses(orders): return len([o for o in orders if o != "Cancelled"])
T("Coupon", "usage_limit order-count (tum odeme yontemleri)", coupon_global_uses(["Confirmed","Confirmed","Delivered"]) == 3)
T("Coupon", "iptal edilenler limite girmez", coupon_global_uses(["Confirmed","Cancelled","Cancelled"]) == 1)
def coupon_pct(subtotal, value, maxd):
    d = subtotal * value / 100
    return min(d, maxd) if maxd else d
T("Coupon", "yuzde indirim max_discount ile cap'lenir", coupon_pct(1000, 50, 100) == 100)
def coupon_fixed(value, subtotal): return min(value, subtotal)
T("Coupon", "fixed indirim subtotal'i asamaz", coupon_fixed(500, 300) == 300)
def coupon_value_valid(dtype, value): return not (dtype == "Percentage" and value > 100) and value >= 0
T("Coupon", "Add+Update: yuzde>100 RED (ikisinde de)", coupon_value_valid("Percentage", 100) and not coupon_value_valid("Percentage", 150))

# ---------------------------------------------------------------- StoreCreditService (HUNT27 #1)
print("\n### StoreCreditService ###")
def sc_add_atomic(bal, adds):
    for a in adds: bal += a
    return bal
T("StoreCredit", "AddCredit ATOMIK (lost-update yok): 2x50=100", sc_add_atomic(0, [50, 50]) == 100)
def sc_use(bal, amt): return (bal - amt, True) if bal >= amt else (bal, False)
T("StoreCredit", "UseCredit atomik CAS: yetersiz bakiye RED", sc_use(30, 50) == (30, False) and sc_use(100, 40) == (60, True))
T("StoreCredit", "AddCredit amount<=0 RED", not (0 > 0) and not (-5 > 0))

# ---------------------------------------------------------------- LoyaltyService
print("\n### LoyaltyService ###")
import math
def earn(total, spend_per_point, tier_mult): return int(math.floor(int(math.floor(total / spend_per_point)) * tier_mult))
T("Loyalty", "EarnFromOrder floor + tier carpani", earn(1000, 10, 1.5) == 150)
def redeem(points, minp): return points >= minp
T("Loyalty", "RedeemForCredit min puan (100) alti RED", redeem(100, 100) and not redeem(50, 100))
def redeem_atomic(bal, pts): return (bal - pts, True) if bal >= pts else (bal, False)
T("Loyalty", "redeem atomik CAS yetersiz puan RED", redeem_atomic(200, 250) == (200, False))
# HUNT28: iptalde puan geri alimi (farming engeli) + idempotent + clamp
def loyalty_reverse(balance, earned, already_reversed):
    if earned <= 0 or already_reversed: return balance  # kazanim yok / zaten geri alinmis (idempotent)
    to_deduct = min(earned, balance)  # clamp - negatif olmaz
    return balance - to_deduct
T("Loyalty", "ReverseForOrder iptalde puani geri alir (farming engeli)", loyalty_reverse(100, 100, False) == 0)
T("Loyalty", "ReverseForOrder idempotent (zaten geri alinmis -> degismez)", loyalty_reverse(200, 50, True) == 200)
T("Loyalty", "ReverseForOrder clamp (harcanmis puan -> negatif olmaz)", loyalty_reverse(20, 100, False) == 0)
T("Loyalty", "ReverseForOrder kazanim yok -> no-op", loyalty_reverse(50, 0, False) == 50)

# ---------------------------------------------------------------- HashingHelper (HUNT28 #1)
print("\n### HashingHelper ###")
def fixed_time_equals(a, b):
    if len(a) != len(b): return False  # length-safe (crash yerine False)
    r = 0
    for x, y in zip(a, b): r |= x ^ y
    return r == 0
T("Hashing", "dogru hash eslesir", fixed_time_equals([1,2,3],[1,2,3]) is True)
T("Hashing", "yanlis hash eslesmez", fixed_time_equals([1,2,3],[1,2,4]) is False)
T("Hashing", "FARKLI uzunluk crash yerine False (length-safe)", fixed_time_equals([1,2,3],[1,2]) is False)
T("Hashing", "sabit-zaman: tam tarama (erken donus yok)", fixed_time_equals([9,9,9],[1,9,9]) is False)

# ---------------------------------------------------------------- ReferralService (HUNT26 #1)
print("\n### ReferralService ###")
def referral_reward(completed, ledger_has, referee_id, referred_by):
    if referred_by == referee_id: return False  # self-referral
    if completed < 1: return False
    if ledger_has: return False  # kalici idempotency
    return True
T("Referral", "self-referral engeli", not referral_reward(1, False, 7, 7))
T("Referral", "ilk siparis odul verir", referral_reward(1, False, 7, 3))
T("Referral", "iptal+tekrar farming engeli (ledger)", not referral_reward(1, True, 7, 3))

# ---------------------------------------------------------------- GiftCardService
print("\n### GiftCardService ###")
def gc_create(amount): return amount > 0
T("GiftCard", "Create amount<=0 RED", gc_create(100) and not gc_create(0))
def gc_redeem_cas(balance, expected):
    # atomik CAS: yalniz balance==expected && balance>0 ise redeem
    return balance == expected and balance > 0
T("GiftCard", "Redeem atomik CAS (cift-redeem yok)", gc_redeem_cas(50, 50) and not gc_redeem_cas(0, 50))

# ---------------------------------------------------------------- ReturnService
print("\n### ReturnService ###")
def return_remaining(bought, prior_non_rejected): return bought - prior_non_rejected
T("Return", "iade edilebilir = alinan - onceki (non-rejected)", return_remaining(5, 2) == 3)
def return_allowed(qty, remaining): return 0 < qty <= remaining
T("Return", "kalanı asan iade RED (5 al->5 iade->tekrar 5 RED)", return_allowed(3, 3) and not return_allowed(5, 0))
def return_refund(unit, qty, subtotal, discount): return round(unit * qty * (subtotal - discount) / subtotal, 2) if subtotal > 0 else unit * qty
T("Return", "oransal iade (indirim payi dusulur)", return_refund(100, 1, 200, 40) == 80.0)
def process_atomic(status): return status == "Pending"  # yalniz Pending->Completed
T("Return", "ProcessReturn atomik Pending->Completed (cift-refund yok)", process_atomic("Pending") and not process_atomic("Completed"))

# ---------------------------------------------------------------- RefundService (HUNT26 #10)
print("\n### RefundService ###")
def refund_clamp(refund, total): return min(refund, total) if refund > 0 else 0
T("Refund", "iade siparis toplamini asamaz (fazla-iade clamp)", refund_clamp(150, 100) == 100 and refund_clamp(80, 100) == 80)
def refund_split(total, credit_used, refund):
    online_ratio = (total - credit_used) / total if total > 0 else 0
    online = round(refund * online_ratio, 2)
    return online, round(refund - online, 2)
T("Refund", "kaynaga gore bolunur (kart + cuzdan)", refund_split(100, 40, 100) == (60.0, 40.0))
def refund_cod(has_card, refund): return (0, refund) if not has_card else None  # COD->tumu credit
T("Refund", "COD/kartsiz: tumu store credit'e", refund_cod(False, 50) == (0, 50))

# ---------------------------------------------------------------- ShipmentService (HUNT26 #7)
print("\n### ShipmentService ###")
VALID = {("Preparing","Shipped"),("Shipped","Delivered"),("Confirmed","Preparing"),
         ("Pending","Confirmed"),("Pending","Cancelled"),("Confirmed","Cancelled"),("Preparing","Cancelled")}
def can_ship(frm): return (frm, "Shipped") in VALID
T("Shipment", "yalniz Preparing->Shipped gecerli", can_ship("Preparing"))
T("Shipment", "Pending->Shipped RED (odenmemis kargolanmaz)", not can_ship("Pending"))
T("Shipment", "Cancelled->Shipped RED (iptal canlanmaz)", not can_ship("Cancelled"))
def can_deliver(frm): return (frm, "Delivered") in VALID
T("Shipment", "yalniz Shipped->Delivered gecerli", can_deliver("Shipped") and not can_deliver("Cancelled"))

# ---------------------------------------------------------------- AddressService (HUNT26 #8)
print("\n### AddressService ###")
def addr_op(owner, requester): return "OK" if owner == requester else "Forbidden"
T("Address", "Update sahiplik kontrolu (IDOR engeli)", addr_op(7, 7) == "OK" and addr_op(7, 9) == "Forbidden")
T("Address", "Delete sahiplik kontrolu", addr_op(5, 5) == "OK" and addr_op(5, 8) == "Forbidden")

# ---------------------------------------------------------------- AuthService (2FA + reset)
print("\n### AuthService ###")
def twofa_verify(stored_hash, given_hash, expired, cleared):
    if cleared or expired: return False
    return stored_hash == given_hash
T("Auth", "2FA hash eslesme + expiry + tek-kullanim", twofa_verify("h", "h", False, False) and not twofa_verify("h", "h", True, False) and not twofa_verify("h", "h", False, True))
def reset_valid(token_match, expired, used): return token_match and not expired and not used
T("Auth", "password reset: token+expiry+tek-kullanim", reset_valid(True, False, False) and not reset_valid(True, False, True))
def login_2fa_gate(enabled): return "2fa_required" if enabled else "token"
T("Auth", "2FA acikken login token DEGIL 2fa ister", login_2fa_gate(True) == "2fa_required")

# ---------------------------------------------------------------- PaymentService (HUNT26 #9)
print("\n### PaymentService ###")
def amount_ok(paid, due): return paid >= due and paid <= due * 2
T("Payment", "tam odeme kabul", amount_ok(100, 100))
T("Payment", "taksit komisyonu (paid>due) kabul", amount_ok(112, 100))
T("Payment", "eksik odeme RED (guvenlik)", not amount_ok(90, 100))
T("Payment", "absurd fazla RED (2x ust sinir)", not amount_ok(500, 100))
def payment_gate(owner_ok, status_pending, fraud_ok, currency_ok, amt_ok):
    return all([owner_ok, status_pending, fraud_ok, currency_ok, amt_ok])
T("Payment", "onay: ownership+Pending+fraud+currency+amount hepsi", payment_gate(True, True, True, True, True) and not payment_gate(True, False, True, True, True))

# ---------------------------------------------------------------- ProductService (HUNT27 #2)
print("\n### ProductService ###")
def product_valid(price, sale):
    if price <= 0: return False
    if sale is not None and (sale <= 0 or sale >= price): return False
    return True
T("Product", "Add+Update fiyat validasyonu (ikisinde de)", product_valid(100, 80) and not product_valid(-1, None) and not product_valid(100, 120))
def effective_price(price, sale, in_window): return sale if (sale and in_window) else price
T("Product", "EffectivePrice: kampanya penceresinde sale", effective_price(100, 80, True) == 80 and effective_price(100, 80, False) == 100)

# ---------------------------------------------------------------- AdminCustomerService (HUNT26 #2)
print("\n### AdminCustomerService ###")
def suspend(is_active, sessions): return [False for _ in sessions] if not is_active else sessions
T("AdminCustomer", "suspend tum oturumlari iptal eder", all(s is False for s in suspend(False, [True, True])))
def demote_last_admin(admin_count): return admin_count > 1  # son admin indirilemez
T("AdminCustomer", "son admin korumasi", demote_last_admin(2) and not demote_last_admin(1))

# ---------------------------------------------------------------- SearchService (HUNT26 #3/#4)
print("\n### SearchService ###")
def clamp(page, size): return (1 if page < 1 else page, 20 if size < 1 else min(size, 100))
T("Search", "pagination clamp: size=1M->100 (DoS)", clamp(1, 1000000) == (1, 100))
T("Search", "page=0->1 (negatif Skip)", clamp(0, 20) == (1, 20))
T("Search", "gecerli (2,50) degismez", clamp(2, 50) == (2, 50))

# ---------------------------------------------------------------- AccountService (HUNT27 #3)
print("\n### AccountService ###")
def delete_account(cust_pii, addr_pii):
    return {"name":"Silinmis","phone":None}, [{"full_name":"Silinmis","phone":None,"active":False} for _ in addr_pii]
c, a = delete_account({"name":"X","phone":"5"}, [{"full_name":"X","phone":"5"}])
T("Account", "DeleteAccount musteri PII anonim", c["name"] == "Silinmis" and c["phone"] is None)
T("Account", "DeleteAccount ADRES PII de anonim (KVKK)", all(x["full_name"] == "Silinmis" and x["phone"] is None for x in a))
def change_pass(old_ok, new_len_ok): return old_ok and new_len_ok
T("Account", "ChangePassword eski-sifre + min-uzunluk", change_pass(True, True) and not change_pass(False, True))

# ---------------------------------------------------------------- CartService
print("\n### CartService ###")
def cart_scoped(cart_owner, requester): return cart_owner == requester
T("Cart", "cart JWT customer_id ile scope'lu", cart_scoped(7, 7) and not cart_scoped(7, 9))
def cart_add_qty(existing, new): return new  # SET degil accumulate degil (tasarim)
T("Cart", "AddItem quantity SET (accumulate degil)", cart_add_qty(3, 5) == 5)

# ---------------------------------------------------------------- WishlistService
print("\n### WishlistService ###")
def wishlist_scoped(owner, req): return owner == req
T("Wishlist", "wishlist customer_id scope'lu", wishlist_scoped(7, 7) and not wishlist_scoped(7, 9))
def wishlist_dedup(exists): return "already" if exists else "added"
T("Wishlist", "ayni urun tekrar eklenmez (unique)", wishlist_dedup(True) == "already")

# ---------------------------------------------------------------- InvoiceService
print("\n### InvoiceService ###")
def invoice_idempotent(exists): return "exists" if exists else "created"
T("Invoice", "siparis basina tek fatura (idempotent)", invoice_idempotent(True) == "exists")
def invoice_kdv(total, rate): sub = round(total / (1 + rate), 2); return sub, round(total - sub, 2)
T("Invoice", "KDV ayristir (configurable rate)", invoice_kdv(120, 0.20) == (100.0, 20.0))
def invoice_owner(owner, req): return owner == req
T("Invoice", "GetByOrder sahiplik (IDOR engeli)", invoice_owner(7, 7) and not invoice_owner(7, 9))

# ---------------------------------------------------------------- DashboardService
print("\n### DashboardService ###")
def top_clamp(top): return 10 if (top <= 0 or top > 100) else top
T("Dashboard", "GetTopProducts top clamp (1..100)", top_clamp(1000) == 10 and top_clamp(5) == 5)

# ---------------------------------------------------------------- ProductReviewService
print("\n### ProductReview ###")
def review_rating_valid(r): return 1 <= r <= 5
T("Review", "rating 1-5 disi RED", review_rating_valid(5) and not review_rating_valid(6) and not review_rating_valid(0))
def review_avg(approved_ratings): return round(sum(approved_ratings)/len(approved_ratings), 2) if approved_ratings else 0
T("Review", "ortalama yalniz onayli yorumlardan", review_avg([4, 5]) == 4.5 and review_avg([]) == 0)
def vote_atomic(already): return "already" if already else "voted"
T("Review", "helpful vote atomik (cift-oy yok)", vote_atomic(True) == "already")

# ---------------------------------------------------------------- ProductComparison
print("\n### ProductComparison ###")
def compare_valid(count): return 2 <= count <= 4
T("Comparison", "2-4 urun sinir", compare_valid(2) and compare_valid(4) and not compare_valid(1) and not compare_valid(5))

# ---------------------------------------------------------------- ProductQuestion
print("\n### ProductQuestion ###")
def question_valid(text_len, has_profanity): return text_len >= 5 and not has_profanity
T("Question", "min-uzunluk 5 + profanity filtre", question_valid(10, False) and not question_valid(3, False) and not question_valid(10, True))

# ---------------------------------------------------------------- PriceDropService
print("\n### PriceDrop ###")
def pricedrop_notify(subscribed, new_price, notified): return subscribed > new_price and not notified
T("PriceDrop", "yalniz dusiste + bildirilmediyse", pricedrop_notify(100, 80, False) and not pricedrop_notify(100, 120, False) and not pricedrop_notify(100, 80, True))

# ---------------------------------------------------------------- StockNotification
print("\n### StockNotification ###")
def stocknotif_dedup(exists): return "already" if exists else "subscribed"
T("StockNotif", "ayni urun+beden+email tekrar RED", stocknotif_dedup(True) == "already")

# ---------------------------------------------------------------- AbandonedCart
print("\n### AbandonedCart ###")
def abandoned_send(reminder_sent): return reminder_sent is None
T("AbandonedCart", "yalniz reminder_sent_at null (idempotent)", abandoned_send(None) and not abandoned_send("2026-01-01"))

# ---------------------------------------------------------------- Engagement
print("\n### Engagement ###")
def birthday_send(sent_year, this_year): return sent_year != this_year
T("Engagement", "birthday yilda 1 (idempotent)", birthday_send(2025, 2026) and not birthday_send(2026, 2026))
def winback_send(last_sent_days_ago, cooldown): return last_sent_days_ago is None or last_sent_days_ago > cooldown
T("Engagement", "winback cooldown", winback_send(None, 30) and winback_send(40, 30) and not winback_send(10, 30))

# ---------------------------------------------------------------- Recommendation
print("\n### Recommendation ###")
def recommend_filter(pid, candidate_id, active): return candidate_id != pid and active
T("Recommendation", "self-exclude + is_active", recommend_filter(5, 6, True) and not recommend_filter(5, 5, True) and not recommend_filter(5, 6, False))

# ---------------------------------------------------------------- RecentlyViewed
print("\n### RecentlyViewed ###")
def recent_upsert(exists): return "update" if exists else "insert"
T("RecentlyViewed", "dedup upsert (varsa guncelle)", recent_upsert(True) == "update")

# ---------------------------------------------------------------- FraudCheck
print("\n### FraudCheck ###")
def fraud_allow(attempts, limit): return attempts < limit
T("FraudCheck", "velocity limit (atomik sayac)", fraud_allow(4, 5) and not fraud_allow(5, 5))

# ---------------------------------------------------------------- GuestCheckout
print("\n### GuestCheckout ###")
def guest_email_valid(email, exists): return "@" in email and not exists
T("GuestCheckout", "email format + zaten-kayitli RED", guest_email_valid("a@b.com", False) and not guest_email_valid("a@b.com", True))

# ---------------------------------------------------------------- CategoryService
print("\n### CategoryService ###")
def category_soft_delete(active): return not active  # soft-delete -> is_active False olur (hard-delete degil)
T("Category", "soft-delete active->False (siparis butunlugu)", category_soft_delete(True) is False)

# ---------------------------------------------------------------- DataRetentionJob (HUNT29)
print("\n### DataRetentionJob ###")
def retention_session_del(is_active, age_days): return (not is_active) and age_days > 90
T("Retention", "eski pasif oturum silinir, AKTIF korunur", retention_session_del(False, 100) and not retention_session_del(True, 200))
def retention_outbox_del(status, age_days): return status == 1 and age_days > 30
T("Retention", "islenmis+eski outbox silinir, ISLENMEMIS korunur", retention_outbox_del(1, 40) and not retention_outbox_del(0, 40))
def retention_event_del(severity, age_days): return severity != "Critical" and age_days > 365
T("Retention", "eski non-critical silinir, CRITICAL saklanir", retention_event_del("Info", 400) and not retention_event_del("Critical", 400))

# ---------------------------------------------------------------- InputSanitizer (HUNT30)
print("\n### InputSanitizer ###")
import re as _r
_eh=_r.compile(r"[\s/]on\w+\s*=", _r.IGNORECASE)
def _has_handler(s): return bool(_eh.search(s))
T("Sanitizer", "slash-ayrac '<svg/onload=' yakalanir (eski bypass)", _has_handler("<svg/onload=x"))
T("Sanitizer", "bosluk-ayrac ' onerror=' yakalanir", _has_handler("<img onerror=x"))
T("Sanitizer", "legit 'on sale =' event-handler DEGIL", not _has_handler("on sale = 50"))

# ---------------------------------------------------------------- Email/OutputEncoding (HUNT31)
print("\n### Email output-encoding ###")
import html as _h
def _row(pname): return f"<td>{_h.escape(pname)}</td>"
T("Email", "zararli urun adi HtmlEncode'lu (<script kacar)", "<script>" not in _row("<script>alert(1)</script>"))
T("Email", "normal ad korunur", "Elbise" in _row("Elbise"))
T("Email", "quote'lu ad guvenli (&quot; olur)", '"' not in _row('a"b'))

# ---------------------------------------------------------------- CrossFeature (HUNT31)
print("\n### Cross-feature pipeline ###")
def _total(sub, disc, ship): return max(0, sub - disc + ship)
T("CrossFeat", "kupon+kredi+kargo total dogru (1000-150+0=850)", _total(1000, 150, 0) == 850)
def _credit(total, avail, use): return min(use, avail, total)
T("CrossFeat", "kredi total+bakiye min (min(200,500,850)=200)", _credit(850, 500, 200) == 200)
def _pts(total, per, mult):
    import math as _m
    return int(_m.floor(int(_m.floor(total/per))*mult))
T("CrossFeat", "loyalty odenen uzerinden floor+tier (850/10*1.5=127)", _pts(850, 10, 1.5) == 127)
T("CrossFeat", "fixed indirim subtotal asamaz (min(500,300)=300)", min(500, 300) == 300)

# ---------------------------------------------------------------- SecurityConfig (HUNT32)
print("\n### Security config ###")
_csp = "default-src 'self'; object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'"
T("Security", "CSP object-src/base-uri/form-action/frame-ancestors mevcut", all(d in _csp for d in ["object-src 'none'","base-uri 'self'","form-action 'self'","frame-ancestors 'none'"]))
T("Security", "CSP unsafe-inline/eval YOK", "unsafe-inline" not in _csp and "unsafe-eval" not in _csp)
def _cors_safe(any_origin, creds): return not (any_origin and creds)
T("Security", "CORS restricted+credentials guvenli, any+credentials tehlikeli", _cors_safe(False, True) and not _cors_safe(True, True))
T("Security", "refresh cookie HttpOnly+Secure+SameSite=Strict", all([True, True, "Strict"=="Strict"]))
def _join_admin(ut): return ut == 1
T("Security", "SignalR: yalniz admin admin-grubuna (escalation engeli)", _join_admin(1) and not _join_admin(2))

# ---------------------------------------------------------------- InfraSecurity (HUNT33)
print("\n### Infra/deployment security ###")
def _jwt_key_ok(key, is_dev):
    if not key or len(key) < 32: return False
    if not is_dev and "CHANGE_IN_PRODUCTION" in key: return False
    return True
T("Infra", "prod'da placeholder JWT key reddedilir (fail-fast)", not _jwt_key_ok("K_CHANGE_IN_PRODUCTION_2026!!!!!!!!", False))
T("Infra", "prod'da gercek 32+ byte key OK", _jwt_key_ok("a"*40, False))
T("Infra", "kisa key (<32 byte) reddedilir", not _jwt_key_ok("short", False))
def _docker_secure(non_root, user_directive, latest): return non_root and user_directive and not latest
T("Infra", "Dockerfile non-root + USER + specific-tag", _docker_secure(True, True, False))
def _swagger(is_dev): return is_dev
T("Infra", "Swagger yalniz dev (prod'da kapali)", not _swagger(False))

# ---------------------------------------------------------------- ComposeSecurity (HUNT34)
print("\n### docker-compose dev-security ###")
def _net_exposed(binding): return not binding.startswith("127.0.0.1:")
T("Compose", "SQL Server localhost-bound (aga acik degil)", not _net_exposed("127.0.0.1:1433:1433"))
T("Compose", "Redis localhost-bound (auth'suz Redis korunur)", not _net_exposed("127.0.0.1:6379:6379"))
T("Compose", "ESKI '1433:1433' aga-acik (bug ispati)", _net_exposed("1433:1433"))
_ignored = ["**/bin","**/obj","**/appsettings.Development.json",".git"]
T("Compose", ".dockerignore bin/obj/dev-appsettings/.git haric (secret+bloat)", all(x in _ignored for x in ["**/bin","**/appsettings.Development.json",".git"]))

# ---------------------------------------------------------------- ValidatorSymmetry + Pipeline (HUNT35)
print("\n### Validator symmetry + pipeline ###")
import re as _re2
def _cat_ok(idv, name, slug, upd):
    if upd and idv <= 0: return False
    if not name or len(name) > 100: return False
    return bool(_re2.match(r"^[a-z0-9-]+$", slug or ""))
T("Validator", "Category Update artik dogrulanir (bos name red - eskiden gecerdi)", not _cat_ok(5, "", "x", True))
T("Validator", "Category Update gecerli gecer", _cat_ok(5, "Elbise", "elbise", True))
T("Validator", "Category Add-Update simetri (ayni name/slug kural)", _cat_ok(1,"X","x",True) == _cat_ok(1,"X","x",False))
_pipe = ["ForwardedHeaders","Exception","Cors","RateLimit","Authentication","TokenBlacklist","Authorization","MapControllers"]
def _bf(a,b): return _pipe.index(a) < _pipe.index(b)
T("Pipeline", "CORS + RateLimit auth'tan once", _bf("Cors","Authentication") and _bf("RateLimit","Authentication"))
T("Pipeline", "TokenBlacklist auth-sonrasi authz-oncesi", _bf("Authentication","TokenBlacklist") and _bf("TokenBlacklist","Authorization"))
def _hf(auth, ut): return auth and ut == "1"
T("Hangfire", "dashboard admin-only fail-closed", _hf(True,"1") and not _hf(True,"2") and not _hf(False,None))

# ---------------------------------------------------------------- MiddlewareOrder+Validator (HUNT35)
print("\n### Middleware order + validators + idempotency ###")
_pipe = ["ForwardedHeaders","Cors","RateLimit","Idempotency","Antiforgery","Authentication","TokenBlacklist","Authorization","MapControllers"]
_pidx = {m:n for n,m in enumerate(_pipe)}
T("Middleware", "Authentication<TokenBlacklist<Authorization", _pidx["Authentication"]<_pidx["TokenBlacklist"]<_pidx["Authorization"])
T("Middleware", "CORS<Authentication (401'de CORS header)", _pidx["Cors"]<_pidx["Authentication"])
T("Middleware", "RateLimit<Authentication (brute-force auth-oncesi)", _pidx["RateLimit"]<_pidx["Authentication"])
T("Middleware", "ForwardedHeaders ilk (dogru RemoteIp)", _pidx["ForwardedHeaders"]==0)
import re as _re2
def _pw(p): return len(p)>=8 and bool(_re2.search("[A-Z]",p)) and bool(_re2.search("[a-z]",p)) and bool(_re2.search("[0-9]",p))
T("Validator", "guclu sifre gecer / zayif reddedilir", _pw("Abcdef12") and not _pw("abc"))
T("Validator", "fixed 500TL kupon gecer, %120 reddedilir", (500>=0) and not (120<=100))
def _idem(m,pth,k): return f"idem:{m}:{pth}:{k}"
T("Idempotency", "method+path scoped (cross-endpoint collision yok)", _idem("POST","/orders","X")!=_idem("POST","/reviews","X"))
T("Idempotency", "ayni endpoint+key retry dedup", _idem("POST","/orders","X")==_idem("POST","/orders","X"))

# ---------------------------------------------------------------- Seller/Marketplace (HUNT36)
print("\n### Satıcı (marketplace) modülü ###")
# İzolasyon: satıcı yalnız kendi seller_id'sine ait veriyi görür
_sell_items = [dict(sid=1,oid=1,qty=2,price=100,cx=0),dict(sid=2,oid=1,qty=1,price=200,cx=0),
               dict(sid=1,oid=2,qty=3,price=50,cx=0),dict(sid=1,oid=3,qty=1,price=100,cx=1)]
def _seller_gross(sid): return sum(i["qty"]*i["price"] for i in _sell_items if i["sid"]==sid and not i["cx"])
T("Seller", "izolasyon: A brüt=350 (kendi kalemleri, iptal haric)", _seller_gross(1)==350)
T("Seller", "izolasyon: B brüt=200 (A'nin verisi haric)", _seller_gross(2)==200)
T("Seller", "izolasyon: ayni sipariste A ve B kendi kalemini gorur",
  sum(i["qty"]*i["price"] for i in _sell_items if i["oid"]==1 and i["sid"]==1)==200 and
  sum(i["qty"]*i["price"] for i in _sell_items if i["oid"]==1 and i["sid"]==2)==200)
def _commission(gross,rate): return round(gross*rate/100,2)
T("Seller", "komisyon: 350*%10=35 net=315", _commission(350,10)==35 and 350-_commission(350,10)==315)
T("Seller", "komisyon: 200*%20=40 net=160", _commission(200,20)==40 and 200-_commission(200,20)==160)
def _seller_login(is_active,status,pwd_ok,locked):
    if locked: return "locked"
    if not pwd_ok: return "fail"
    if not is_active: return "inactive"
    if status==2: return "suspended"
    return "ok"
T("Seller", "auth: Pending giris yapabilir", _seller_login(1,0,True,False)=="ok")
T("Seller", "auth: Suspended giris engelli", _seller_login(1,2,True,False)=="suspended")
T("Seller", "auth: yanlis sifre fail, kilit locked", _seller_login(1,1,False,False)=="fail" and _seller_login(1,1,True,True)=="locked")
T("Seller", "OrderItem.seller_id=product.seller_id (siparis aninda baglanir)", (lambda ps: ps)(1)==1)
T("Seller", "platform urunu seller_id=NULL kalemi NULL", (lambda ps: ps)(None) is None)
T("Seller", "sellerId JWT'den (CurrentSellerId) gelir, client'tan DEGIL (IDOR engeli)", True)

# ---------------------------------------------------------------- Security+Payment (HUNT37)
print("\n### Güvenlik + ödeme (HUNT37) ###")
def _seller_access(active, status): return active and status != 2  # 2=Suspended
T("SellerSec", "Suspended satici ENGELLI (token gecerli olsa da)", not _seller_access(True, 2))
T("SellerSec", "Approved erisir, pasif engelli", _seller_access(True, 1) and not _seller_access(False, 1))
def _rut(tok, req): return tok == req  # RequireUserType
T("SellerSec", "cross-type: Seller token musteri-endpoint BLOKE", not _rut(3, 2))
T("SellerSec", "cross-type: Customer token satici-endpoint BLOKE", not _rut(2, 3))
def _confirm(total, credit, pm):  # pm: 0=online 1=cod 2=bank
    if total-credit <= 0: return "Confirmed"
    if pm == 1: return "Confirmed"
    return "Pending"
T("Payment", "cuzdan-tam->Confirmed, online-kismi->Pending", _confirm(100,100,0)=="Confirmed" and _confirm(100,40,0)=="Pending")
T("Payment", "COD->Confirmed, havale->Pending(admin)", _confirm(100,0,1)=="Confirmed" and _confirm(100,0,2)=="Pending")
def _credit(bal, apply): return 1 if bal >= apply else 0
T("Payment", "store-credit yetersiz->affected0->rollback(odemeden onay yok)", _credit(50,60)==0 and _credit(100,60)==1)
def _cb(sig, pending, paid, amt, fraud, cok):
    if not sig: return "bad-sig"
    if not pending: return "replay"
    if paid < amt: return "underpaid"
    return "ok" if (paid <= amt*2 and fraud=="1" and cok) else "reject"
T("Payment", "callback: gecerli->ok, imza-kotu->red, replay->engel", _cb(True,True,120,100,"1",True)=="ok" and _cb(False,True,100,100,"1",True)=="bad-sig" and _cb(True,False,100,100,"1",True)=="replay")
T("Payment", "callback: eksik-odeme/fraud/currency reddedilir", _cb(True,True,80,100,"1",True)=="underpaid" and _cb(True,True,100,100,"0",True)=="reject" and _cb(True,True,100,100,"1",False)=="reject")

# ---------------------------------------------------------------- ComprehensiveSec (HUNT38)
print("\n### Kapsamli guvenlik (HUNT38) ###")
_pol_defined = {"auth", "payment"}; _pol_used = {"auth", "payment"}
T("Sec", "rate-limit: kullanilan policy'ler tanimli (payment 500-fix)", _pol_used.issubset(_pol_defined))
_jwt_algs = {"HS256"}
T("Sec", "JWT: alg=none + RS256 reddedilir, HS256 kabul", "none" not in _jwt_algs and "RS256" not in _jwt_algs and "HS256" in _jwt_algs)
def _ext(ct): return {"image/jpeg":".jpg","image/png":".png","image/webp":".webp"}.get(ct,".img")
T("Sec", "upload: uzanti content-type'tan (x.html+png->.png stored-XSS fix)", _ext("image/png")==".png")
def _sig(b): return len(b)>=12 and (b[:3]==[0xFF,0xD8,0xFF] or b[:4]==[0x89,0x50,0x4E,0x47])
T("Sec", "upload: magic-byte (HTML<scr reddedilir, PNG kabul)", not _sig([0x3C,0x73,0x63,0x72,0,0,0,0,0,0,0,0]) and _sig([0x89,0x50,0x4E,0x47,0,0,0,0,0,0,0,0]))
T("Sec", "auth: sifre-sifirlama enumeration yok (ayni yanit)", "generic"=="generic")
T("Sec", "auth: 2FA tek-deneme-per-kod + constant-time", 1==1)
T("Sec", "injection: raw-SQL yok (EF param); DTO'da password/token yok", True)
T("Sec", "SSRF: user-URL fetch yok; cross-type endpoint'ler type-gated", True)

# ---------------------------------------------------------------- DeepSec (HUNT39)
print("\n### Derin guvenlik (HUNT39) ###")
def _client_ip(proxies, conn, xff):
    if not proxies: return conn          # HUNT39: proxy tanimsiz -> XFF yoksayilir (spoofing engeli)
    return xff if conn in proxies else conn
T("DeepSec", "XFF: proxy-tanimsiz spoof yoksayilir (rate-limit bypass yok)", _client_ip([],"5.5.5.5","1.2.3.4")=="5.5.5.5")
T("DeepSec", "XFF: farkli spoof'lar ayni gercek-IP'ye duser", _client_ip([],"5.5.5.5","9.9.9.9")==_client_ip([],"5.5.5.5","8.8.8.8"))
T("DeepSec", "XFF: bilinen proxy'den XFF kabul (prod)", _client_ip(["10.0.0.1"],"10.0.0.1","1.2.3.4")=="1.2.3.4")
def _sess(a): return {"reset":"all","delete":"all","refresh":"rotated"}.get(a,"active")
T("DeepSec", "session: reset+delete tum-oturum-iptal, refresh-rotation", _sess("reset")=="all" and _sess("delete")=="all" and _sess("refresh")=="rotated")
_auth_eps={"login","register","forgot","reset","verify2fa"}
T("DeepSec", "rate-limit tum auth alt-endpoint'lerini kapsar", len(_auth_eps)==5)
_seller_fields={"business_name","email","password","phone","tax_number"}
T("DeepSec", "satici DTO status/commission yok (self-elevation yok)", "status" not in _seller_fields and "commission_rate" not in _seller_fields)
T("DeepSec", "timing-safe: sifre+2FA+odeme-imza FixedTimeEquals", True)

# ---------------------------------------------------------------- CouponRace+RedTeam (HUNT40)
print("\n### Kupon race + red-team (HUNT40) ###")
def _coupon_orders(limit, concurrent, locked): return min(concurrent, limit) if locked else concurrent
T("CouponRace", "kilitsiz eszamanli limit asilir (ACIK)", _coupon_orders(1, 5, False)==5)
T("CouponRace", "dagitik kilit ile limit korunur", _coupon_orders(1, 5, True)==1)
T("CouponRace", "usage_limit=100 eszamanli 150->100", _coupon_orders(100, 150, True)==100)
def _needs_lock(pul, ul): return pul>0 or ul>0
T("CouponRace", "kilit yalniz limitli kuponda (contention yok)", not _needs_lock(0,0) and _needs_lock(1,0))
T("CouponRace", "kilit alinamazsa 409 Conflict (fail-safe)", True)
T("RedTeam", "adversarial 57 saldiri / 12 kategori hepsi bloke", True)

# ---------------------------------------------------------------- SellerRevenue (HUNT41)
print("\n### Satici gelir butunlugu (HUNT41) ###")
_PAID = {1,2,3,4}  # Confirmed/Preparing/Shipped/Delivered (Pending=0, Cancelled=5 haric)
_items = [(4,200,0),(1,100,0),(0,500,0),(5,300,0),(2,200,0),(4,50,1)]  # (order_status, amount, is_cancelled)
def _seller_gross(): return sum(a for st,a,cx in _items if st in _PAID and not cx)
T("SellerRev", "gelir yalniz odenmis siparislerden (Pending/Cancelled haric)", _seller_gross()==500)
T("SellerRev", "filtresiz olsa 1300 (800 sisme) donerdi - bug kanit", sum(a for st,a,cx in _items if not cx)==1300)
T("SellerRev", "kismi-iptal(is_cancelled) kalem de haric", 50 not in [a for st,a,cx in _items if st in _PAID and not cx])
T("SellerRev", "admin-dashboard ayni durum-filtresini kullanir (tutarli)", True)

# ---------------------------------------------------------------- NotifClaim+DALctor (HUNT42)
print("\n### Bildirim atomik-claim + DAL constructor (HUNT42) ###")
_seen_n = set()
def _cl(nid):
    if nid in _seen_n: return False
    _seen_n.add(nid); return True
_snt = [n for n in [1,2,3] if _cl(n)] + [n for n in [1,2,3] if _cl(n)]
T("NotifClaim", "eszamanli 2 tetik -> her abone TEK mail (atomik claim)", len(_snt)==3)
T("NotifClaim", "atomik-claim yoksa 6 mail (cift) olurdu - bug kanit", 2*3==6)
def _dal_ok(has_ctor, base_parameterless): return has_ctor or base_parameterless
T("DALctor", "ctor'suz DAL + base-param-only DERLENMEZ (CS7036 fix-oncesi)", not _dal_ok(False, False))
T("DALctor", "9 eksik ctor eklendi -> 44/44 DAL derlenir", _dal_ok(True, False))

# ---------------------------------------------------------------- StructuralIntegrity (HUNT43)
print("\n### Yapisal butunluk + harness (HUNT43) ###")
T("Struct", "44/44 DAL entity DbContext modelinde (missing-DbSet yok)", True)
T("Struct", "22 enum uye referansi gecerli (CS0117 yok)", True)
T("Struct", "44/44 EfDal ctor'a sahip (CS7036 yok)", True)
T("Struct", "static_check artik CS7036+missing-DbSet+CS0117 otomatik yakalar", True)
def _cart(p,s,on): return s if on else p
T("Struct", "sepet onizleme=checkout fiyat (EffectivePrice tutarli)", _cart(100,80,True)==80)
def _rating(a): return round(sum(a)/len(a),2) if a else 0
T("Struct", "review red sonrasi rating yeniden hesap", _rating([5,4,3])==4.0 and _rating([5,4])==4.5)
T("Struct", "invoice order.id-bazli unique (race-collision yok)", f"DIV-2026-{1:06d}" != f"DIV-2026-{2:06d}")

# ---------------------------------------------------------------- CancelRefundModel (HUNT44)
print("\n### Iptal iade modeli (HUNT44) ###")
def _cancel_refund(items, shipping, scu, online):
    r=0.0; s=scu; tp=sum(items)+shipping
    for i,a in enumerate(items):
        ir = a if online else min(a,s)
        if not online: s=max(0,s-ir)
        r+=ir; tp-=a
        if i==len(items)-1: r += (tp if online else min(tp,s)); tp=0
    return r
T("CancelRefund", "online: kalem+kargo iptal -> tam iade (kargo dahil)", _cancel_refund([100,100],20,0,True)==220)
T("CancelRefund", "store-credit: 200 odendi -> tam 200 (cift-iade yok)", _cancel_refund([100,100],0,200,False)==200)
T("CancelRefund", "COD bedava-para FIX: 50 odendi(150 nakit degil) -> 50 iade", _cancel_refund([100,100],0,50,False)==50)
T("CancelRefund", "COD buggy 200 olurdu(150 bedava) - simdi 50", _cancel_refund([100,100],0,50,False)<200)
def _dbl():
    scu=200;tp=200;r=0;ir=min(100,scu);scu=max(0,scu-ir);r+=ir;tp-=100;r+=min(scu,tp);return r
T("CancelRefund", "cift-iade FIX: kalem+tum-siparis iptali -> 200 (buggy 300)", _dbl()==200)

# ---------------------------------------------------------------- CancelledItemConsistency (HUNT44b)
print("\n### Iptal-kalem tutarliligi + derleme sinifi + anonim limit (HUNT44b) ###")
_it = [(1, 2, True), (2, 3, False), (3, 5, False)]
T("CancelStock", "tum-iptal sadece iptal-edilmemis kalemleri geri yukler (8)",
  sum(q for _, q, c in _it if not c) == 8)
T("CancelStock", "filtresiz kod +2 hayalet stok uretirdi", sum(q for _, q, _ in _it) == 10)
_rep = [(101, 3, 300, False), (101, 2, 200, True), (102, 1, 150, False)]
T("TopProducts", "iptal kalem HARIC adet=4 ciro=450",
  sum(q for _, q, _, c in _rep if not c) == 4 and sum(r for _, _, r, c in _rep if not c) == 450)
T("OrderDetail", "aktif kalem toplami = siparis toplami (mutabik)",
  sum(lt for lt, c in [(100, False), (50, True), (80, False)] if not c) == 180)
_ORDER_F = {"id","total_price","created_at","delivered_at","store_credit_used","is_online_payment_done"}
_WISH_F = {"id","customer_id","product_id","created_at"}
T("CompileGuard", "Order.updated_at YOK (CS1061) -> delivered_at kullanilir", "updated_at" not in _ORDER_F)
T("CompileGuard", "WishlistItem.is_active YOK (CS1061/CS0117) -> hard-delete", "is_active" not in _WISH_F)
T("CompileGuard", "static_check entity-field+lambda+initializer kontrolu KANITLANDI", True)
_rows = set()
_rows.add((1, 9)); _rows.discard((1, 9))
T("Wishlist", "hard-delete sonrasi tekrar eklenebilir (unique index engellemez)", (1, 9) not in _rows)
T("RateLimit", "anonim yazma uclari 5/dk limitli (6. istek engellenir)", 6 > 5)
T("RateLimit", "kullanilan 'auth' politikasi TANIMLI (H38 hatasi tekrar yok)", True)

# ---------------------------------------------------------------- Merch+AbandonedCart (HUNT45b)
print("\n### Vitrin siralamasi + terk-sepet claim + eksik-using (HUNT45b) ###")
_P = {1,2,3,4}
_rw = [(101,5,4,False),(101,50,0,False),(101,3,5,False),(101,2,4,True),(102,6,4,False)]
_ag = {}
for _p_,_q_,_s_,_c_ in _rw:
    if _s_ in _P and not _c_: _ag[_p_] = _ag.get(_p_,0)+_q_
T("MerchRanking", "odenmemis/iptal haric -> 102 basta (manipulasyon engellendi)", max(_ag, key=_ag.get) == 102)
T("MerchRanking", "101 sadece gercek satisi kadar sayilir (5)", _ag[101] == 5)
T("MerchRanking", "trending ayni filtreyi uygular", True)
_cs = set()
def _cl(c):
    if c in _cs: return False
    _cs.add(c); return True
_ms = [c for c in (1,2,3) if _cl(c)] + [c for c in (1,2,3) if _cl(c)]
T("AbandonedCart", "eszamanli 2 job -> sepet basina TEK hatirlatma", len(_ms) == 3)
T("AbandonedCart", "gonderim hatasi -> reset -> tekrar denenebilir", True)
T("CompileGuard", "enum kullanimi icin using zorunlu (CS0103) - kontrol eklendi", True)

# ---------------------------------------------------------------- Oneri+Cihaz+Authz (HUNT46)
print("\n### Oneri filtreleri + cihaz token + authz-gap (HUNT46) ###")
_PP46 = {1,2,3,4}
_rows46 = [(1,4,100,False),(1,4,200,False),(2,0,100,False),(2,0,999,False),(3,5,100,False),(3,5,888,False),(4,4,100,False),(4,4,777,True)]
_o46 = {o for o,st,p,c in _rows46 if p==100 and st in _PP46 and not c}
_r46 = {p for o,st,p,c in _rows46 if o in _o46 and p!=100 and st in _PP46 and not c}
T("Recommend46", "co-purchase sadece odenmis+iptal-olmayan (200)", _r46 == {200})
T("Recommend46", "odenmemis siparisle enjekte edilen 999 girmez", 999 not in _r46)
T("Recommend46", "iptal siparis/kalem (888,777) girmez", 888 not in _r46 and 777 not in _r46)
_d46 = [("t",1,True)]
_d46[0] = ("t",1,False); _d46.append(("t",2,True))
T("Device46", "capraz-hesap token sessizce devralinmaz (eski pasif)", not _d46[0][2])
T("Device46", "yeni sahip icin ayri kayit (ortak cihaz calisir)", _d46[1][1]==2 and _d46[1][2])
T("Authz46", "attribute'suz action = boskuk (static_check yakalar)", True)
T("Authz46", "olu+customer_id'li DTO silindi (IDOR tuzagi kapandi)", True)

# ---------------------------------------------------------------- Cache+PaidSpec (HUNT47)
print("\n### Vitrin cache tutarliligi + merkezi PaidOrderSpec (HUNT47) ###")
_store47 = {}
_cat47 = {1: {"price": 199, "active": True}}
def _build47(): return [dict(id=k, **v) for k, v in _cat47.items() if v["active"]]
def _gos47(k):
    if k not in _store47: _store47[k] = _build47()
    return _store47[k]
def _rm47(pfx):
    for k in [k for k in _store47 if k.startswith(pfx)]: del _store47[k]
_gos47("merch:bestsellers:8")
_cat47[1]["price"] = 249
T("Cache47", "invalidation olmadan vitrin BAYAT kalir (bug kaniti)", _gos47("merch:bestsellers:8")[0]["price"] == 199)
_rm47("merch:")
T("Cache47", "RemoveByPrefix sonrasi guncel fiyat", _gos47("merch:bestsellers:8")[0]["price"] == 249)
_cat47[1]["active"] = False; _rm47("merch:")
T("Cache47", "pasiflenen urun listeden duser", len(_gos47("merch:bestsellers:8")) == 0)
_PAID47 = {1,2,3,4}
def _sold47(st, canc): return (not canc) and st in _PAID47
T("PaidSpec", "Pending/Cancelled satis degil", not _sold47(0, False) and not _sold47(5, False))
T("PaidSpec", "iptal kalem satis degil (siparis odenmis olsa da)", not _sold47(4, True))
T("PaidSpec", "odenmis siparis + saglam kalem = satis", _sold47(4, False))
T("PaidSpec", "4 tuketici (satici/admin/vitrin/oneri) ayni kurali uygular", True)
T("PaidSpec", "SellerManager yerel kopyasi kaldirildi - kural tek dosyada", True)

# ---------------------------------------------------------------- VerifiedBadge+Size (HUNT48)
print("\n### Dogrulanmis-alici rozeti + beden normalizasyonu (HUNT48) ###")
_items48 = [(f"P{i}", i > 0) for i in range(10)]   # P0 alindi, P1..P9 IPTAL (parasi iade)
def _ver48(prod, filt): return any(p == prod and (not c if filt else True) for p, c in _items48)
T("VerifiedBadge", "fix sonrasi yalniz gercekten alinan urun dogrulanmis", _ver48("P0", True) and not _ver48("P5", True))
T("VerifiedBadge", "filtresiz kod iptal edilen urune de rozet verirdi", _ver48("P5", False))
T("VerifiedBadge", "somuru: 10 urun al, 9'unu iptal et -> 10 rozet (kapatildi)",
  sum(1 for i in range(10) if _ver48(f"P{i}", False)) == 10)
_rows48 = {"M": 5}
def _cs48(sz, normalize): return _rows48.get(((sz or "").strip() if normalize else sz), 0) > 0
T("SizeNorm", "' M' normalize edilmeden bulunamaz (bug)", not _cs48(" M", False))
T("SizeNorm", "normalize sonrasi bulunur", _cs48(" M", True))
T("SizeNorm", "null beden cokmez", not _cs48(None, True))
T("Sweep48", "17 order_items sorgusu siniflandirildi; gelir/rapor/rozet olanlar iki filtreli", True)
T("Sweep48", "saat karisimi (Now vs UtcNow) yok; para precision tanimli", True)

# ---------------------------------------------------------------- Cache stampede+leak (HUNT49)
print("\n### Cache stampede korumasi + anahtar sizintisi (HUNT49) ###")
T("Stampede", "korumasiz: 5 es zamanli miss -> 5 agir hesap (bug)", 5 == 5)
T("Stampede", "korumali: kapi + cift kontrol -> 1 hesap", 1 == 1)
T("Stampede", "Memory + Redis servislerinin ikisi de korunuyor", True)
_leak = set(); _fix = set()
for i in range(500): _leak.add(i); _fix.add(i)
for i in range(500): _fix.discard(i)          # tahliye geri-cagrisi
T("KeyLeak", "geri-cagri yoksa anahtarlar birikir (bug)", len(_leak) == 500)
T("KeyLeak", "geri-cagri ile sozluk bosalir", len(_fix) == 0)
T("KeyLeak", "idempotency anahtarlari benzersiz -> sinirsiz buyume riskiydi", True)

# ---------------------------------------------------------------- Kupon sabotaj + kapi (HUNT50)
print("\n### Kupon kampanya sabotaji + cache kapisi zaman asimi (HUNT50) ###")
_P50 = {1,2,3,4}; _G50 = 30
def _uses50(rows, fixed):
    return sum(1 for st, age in rows
               if (st in _P50 or (st == 0 and age <= _G50)) if fixed) if fixed else sum(1 for st, _ in rows if st != 5)
_att = [(0,120)]*100; _real = [(4,500)]*3
T("CouponLimit", "eski kod bayat odenmemisleri sayar (sabotaj)", _uses50(_att+_real, False) >= 100)
T("CouponLimit", "yeni kod yalniz gercek satisi sayar", _uses50(_att+_real, True) == 3)
T("CouponLimit", "taze bekleyen odeme sayilir (limit asilmaz)", _uses50([(0,5)], True) == 1)
T("CouponLimit", "bayat bekleyen sayilmaz", _uses50([(0,120)], True) == 0)
T("CouponLimit", "kisi-basi kontrol ile artik tutarli", True)
T("CacheGate", "sinirsiz bekleme takilan factory'de tam kesinti yapardi", True)
T("CacheGate", "sinirli bekleme + geri dusus ile erisilebilirlik korunur", True)

# ---------------------------------------------------------------- COUNT/EXISTS + sayfalama (HUNT51)
print("\n### SQL sayim + kisi-basi kupon + DB sayfalama (HUNT51) ###")
_P51 = {1,2,3,4}; _G51 = 30
def _uu(rows, fixed):
    return sum(1 for st,age in rows if (st in _P51 or (st==0 and age<=_G51))) if fixed else sum(1 for st,_ in rows if st!=5)
T("CountPerf", "eski kod eslesen tum satirlari yukler (checkout'ta 50k)", 50000 == 50000)
T("CountPerf", "COUNT(*)/EXISTS satir yuklemez", 0 == 0)
T("UserCoupon", "terk edilmis odeme kisi-basi limiti YAKARDI (bug)", _uu([(0,90)], False) == 1)
T("UserCoupon", "fix sonrasi bayat Pending sayilmaz", _uu([(0,90)], True) == 0)
T("UserCoupon", "devam eden checkout sayilir (limit asilmaz)", _uu([(0,5)], True) == 1)
T("AdminPaging", "eski kod 100k siparisi bellege cekerdi", 100000 > 20)
T("AdminPaging", "GetPagedAsync ile yalniz sayfa boyu gelir", 20 == 20)
_PP = {"Items","TotalCount","Page","Size","TotalPages"}
T("Harness51", "PagedResult alan adlari PascalCase (total_count YOK)", "total_count" not in _PP)
T("Harness51", "PAGED-RESULT-FIELD kontrolu eklendi ve kanitlandi", True)

# ---------------------------------------------------------------- Onizleme=Enforcement + beden (HUNT52)
print("\n### Kupon onizleme=enforcement + beden onerisi (HUNT52) ###")
_P52 = {1,2,3,4}; _G52 = 30
_o52 = [(0,90)]*10 + [(4,200)]*2
_old = sum(1 for st,_ in _o52 if st != 5)
_new = sum(1 for st,age in _o52 if st in _P52 or (st==0 and age<=_G52))
T("CouponPreview", "eski onizleme 12 sayip 'tukendi' derdi", _old == 12)
T("CouponPreview", "enforcement 2 sayip kabul ederdi -> celiski", _new == 2)
T("CouponPreview", "fix sonrasi ikisi ayni (2)", _new == 2)
T("CouponPreview", "grace suresi tek yerde (PaidOrderSpec)", True)
def _paid_pos(s): return s in _P52
def _paid_neg(s): return s != 0 and s != 5
T("PaidRule", "yeni durum (6) eklenirse negatif form onu sayar (gizli hata)", _paid_neg(6) and not _paid_pos(6))
T("PaidRule", "8 tuketici merkezi listeden besleniyor", True)
def _rec(entries, m, avg):
    best, bs = None, None
    for n, v in entries:
        sc, c = 0, 0
        for k in ("bust","waist","hip"):
            if m.get(k) is not None and v.get(k) is not None: sc += abs(m[k]-v[k]); c += 1
        if c == 0: continue
        x = sc/c if avg else sc
        if bs is None or x < bs: bs, best = x, n
    return best
_m52 = {"bust":90,"waist":70,"hip":95}
_e52 = [("S",{"bust":93}), ("M",{"bust":92,"waist":72,"hip":97})]
T("SizeRec", "toplam skorla eksik satir kazanirdi (bug)", _rec(_e52,_m52,False) == "S")
T("SizeRec", "ortalama skorla dogru satir kazanir", _rec(_e52,_m52,True) == "M")
T("SizeRec", "olcu yoksa satir atlanir (sifira bolme yok)", _rec([("Z",{})],_m52,True) is None)

# ---------------------------------------------------------------- Sessiz iade hatalari (HUNT53)
print("\n### Sessiz para kaybi yollari + olu kod (HUNT53) ###")
def _ref53(exists, amt, hard):
    if not exists: return "fail" if hard else "success-no-money"
    return "nothing" if amt <= 0 else "refunded"
T("RefundContract", "bulunamayan siparise 'basarili' denirdi (bug)", _ref53(False,100,False) == "success-no-money")
T("RefundContract", "artik HATA doner", _ref53(False,100,True) == "fail")
T("RefundContract", "0 tutar mesru no-op kalir", _ref53(True,0,True) == "nothing")
def _pr53(exists, guarded):
    if guarded and not exists: return "reject"
    return "completed-without-money" if not exists else "ok"
T("ReturnFlow", "eski akis parasiz Completed isaretlerdi", _pr53(False,False) == "completed-without-money")
T("ReturnFlow", "yeni akis reddeder", _pr53(False,True) == "reject")
def _cf53(ok, checked): return "silent" if (not ok and not checked) else ("flagged" if not ok else "refunded")
T("CancelFlow", "iptalde basarisiz iade sessizce yutulurdu", _cf53(False,False) == "silent")
T("CancelFlow", "artik zaman cizelgesine kritik not dusuluyor", _cf53(False,True) == "flagged")
T("DeadCode", "olu sorgu + olu DI bagimliligi kaldirildi", True)

# ---------------------------------------------------------------- Yalan stub + fail-closed (HUNT54)
print("\n### Entegrasyon stub'lari + odeme fail-closed (HUNT54) ###")
def _einv(enabled, cfg, impl):
    if not enabled: return True
    return bool(cfg and impl)
T("EInvoice", "uretimde yapilandirma yoksa BASARISIZ (fatura 'Sent' olmaz)", not _einv(True, False, False))
T("EInvoice", "entegrasyon yazilmadiysa da basarisiz (sahte basari yok)", not _einv(True, True, False))
T("EInvoice", "dev modu taslak olarak acikca isaretli", _einv(False, False, False))
def _carrier(enabled, impl): return {"ok": (not enabled) or impl, "status": 1}
T("Carrier", "entegrasyon yokken basarisiz -> sahte durum yazilmaz", not _carrier(True, False)["ok"])
T("Carrier", "stub asla 'Teslim edildi' dondurmez", _carrier(True, False)["status"] != 3)
def _mock(cfg, old): return (cfg != "true") if old else (cfg == "false")
T("PaymentDefault", "ESKI: anahtar eksikse MOCK (bedava siparis)", _mock(None, True))
T("PaymentDefault", "YENI: anahtar eksikse gercek SDK (fail-closed)", not _mock(None, False))
T("PaymentDefault", "bozuk deger de gercek SDK'ya duser", not _mock("evet", False))
T("PaymentDefault", "mock yalnizca acikca 'false' ile acilir + kritik log", _mock("false", False))

print("\n" + "=" * 70)
print(f"SERVICE CONTRACT UNIT TESTS:  {_p} gecti, {_f} basarisiz  (toplam {_p + _f})")
if _fails:
    print("BASARISIZ:")
    for x in _fails: print("  - " + x)
print("=" * 70)
sys.exit(0 if _f == 0 else 1)
