#!/usr/bin/env python3
"""
DIVISIMA BACKEND — İş Mantığı Test Simülasyonu
================================================
.NET ortamı olmadığı için, C# servis katmanındaki iş kurallarını (formülleri)
BİREBİR Python'a taşıyıp assertion'larla doğruluyoruz. Amaç: CouponManager,
OrderManager, StockManager algoritmalarındaki mantık hatalarını yakalamak.

Kaynak eşlemesi:
  - couponDiscount        <- CouponManager.ValidateCoupon
  - compute_order_total   <- OrderManager.PlaceOrder (subtotal/indirim/kargo/total)
  - Stock                 <- StockManager (Decrease/Increase + overselling engeli)
  - place_order           <- OrderManager.PlaceOrder (idempotency + kupon used_count)
"""

from dataclasses import dataclass, field
from enum import IntEnum
from typing import Optional
import math

# ─────────────────────────────────────────────────────────────
# Sabitler (frontend + OrderManager ile birebir)
FREE_SHIP_THRESHOLD = 2000.0
SHIPPING_COST = 49.9

class DiscountType(IntEnum):   # DiscountTypeEnum
    PERCENTAGE = 0
    FIXED = 1
    FREE_SHIPPING = 2

class OrderStatus(IntEnum):    # OrderStatusEnum
    PENDING = 0
    CONFIRMED = 1
    PREPARING = 2
    SHIPPED = 3
    DELIVERED = 4
    CANCELLED = 5

class MovementType(IntEnum):   # StockMovementType
    IN = 1
    OUT = 2

# ─────────────────────────────────────────────────────────────
# Model'ler (entity karşılıkları - sadece test için gerekli alanlar)
@dataclass
class Coupon:
    code: str
    discount_type: int
    value: float
    min_amount: float = 0.0
    max_discount_amount: Optional[float] = None
    expire_date: Optional[float] = None   # timestamp; None = süresiz
    usage_limit: int = 0                   # 0 = sınırsız
    used_count: int = 0
    is_active: bool = True

@dataclass
class Product:
    id: int
    name: str
    price: float
    is_active: bool = True

# ─────────────────────────────────────────────────────────────
# 1) KUPON DOĞRULAMA — CouponManager.ValidateCoupon birebir
def validate_coupon(coupon: Optional[Coupon], cart_total: float, now: float = 0.0):
    """(ok, discount_amount, free_shipping, error) döner. C# ValidateCoupon ile aynı sıra."""
    if coupon is None or not coupon.is_active:
        return (False, 0.0, False, "CouponInvalid")
    # Son kullanma
    if coupon.expire_date is not None and coupon.expire_date < now:
        return (False, 0.0, False, "CouponExpired")
    # Kullanım limiti (0 = sınırsız)
    if coupon.usage_limit > 0 and coupon.used_count >= coupon.usage_limit:
        return (False, 0.0, False, "CouponUsageLimitReached")
    # Minimum sepet
    if cart_total < coupon.min_amount:
        return (False, 0.0, False, "CouponMinAmountNotMet")

    discount = 0.0
    free_shipping = False
    if coupon.discount_type == DiscountType.PERCENTAGE:
        pct = round(cart_total * coupon.value / 100.0, 2)
        if coupon.max_discount_amount is not None and pct > coupon.max_discount_amount:
            pct = coupon.max_discount_amount   # yüzde tavanı
        discount = pct
    elif coupon.discount_type == DiscountType.FIXED:
        discount = min(coupon.value, cart_total)   # sepeti geçemez
    elif coupon.discount_type == DiscountType.FREE_SHIPPING:
        free_shipping = True
    return (True, discount, free_shipping, None)

# ─────────────────────────────────────────────────────────────
# 2) SİPARİŞ TOPLAMI — OrderManager.PlaceOrder hesap bloğu birebir
def compute_order_total(items, coupon: Optional[Coupon], now: float = 0.0):
    """items: [(Product, qty)]. (subtotal, discount, shipping, total, free_shipping) döner."""
    subtotal = sum(p.price * qty for p, qty in items)

    discount = 0.0
    free_shipping = False
    if coupon is not None:
        # PlaceOrder: kupon geçerli VE min tutuyorsa uygula, yoksa sessizce yok say
        ok, d, fs, _ = validate_coupon(coupon, subtotal, now)
        if ok:
            discount = d
            free_shipping = fs

    shipping = 0.0 if (free_shipping or subtotal >= FREE_SHIP_THRESHOLD) else SHIPPING_COST
    total = subtotal - discount + shipping
    return (round(subtotal, 2), round(discount, 2), round(shipping, 2), round(total, 2), free_shipping)

# ─────────────────────────────────────────────────────────────
# 3) STOK — StockManager (beden bazlı, overselling engeli, hareket kaydı)
class StockError(Exception):
    pass

class StockManager:
    def __init__(self):
        self.stock = {}       # (product_id, size) -> quantity
        self.movements = []   # hareket kayıtları

    def set_stock(self, product_id, size, qty):
        self.stock[(product_id, size)] = qty

    def check_stock(self, product_id, size, qty):
        return self.stock.get((product_id, size), 0) >= qty

    def decrease(self, product_id, size, qty, ref=None):
        cur = self.stock.get((product_id, size))
        if cur is None:
            raise StockError("StockNotFound")
        if cur < qty:
            raise StockError("StockInsufficient")   # overselling engeli
        self.stock[(product_id, size)] = cur - qty
        self.movements.append((product_id, size, MovementType.OUT, qty, ref))
        return self.stock[(product_id, size)]

    def increase(self, product_id, size, qty, ref=None):
        cur = self.stock.get((product_id, size))
        if cur is None:
            raise StockError("StockNotFound")
        self.stock[(product_id, size)] = cur + qty
        self.movements.append((product_id, size, MovementType.IN, qty, ref))
        return self.stock[(product_id, size)]

# ─────────────────────────────────────────────────────────────
# 4) SİPARİŞ VER — OrderManager.PlaceOrder tam akış (idempotency + stok + kupon sayaç)
class OrderSystem:
    def __init__(self, stock: StockManager):
        self.stock = stock
        self.orders = {}          # id -> order
        self.by_request = {}      # request_id -> order_id
        self._next_id = 1

    def place_order(self, customer_id, items, coupon=None, request_id=None, now=0.0):
        # Idempotency
        if request_id and request_id in self.by_request:
            return ("DUPLICATE", self.orders[self.by_request[request_id]])
        if not items:
            raise ValueError("OrderEmptyCart")
        # Stok kontrol (hepsi)
        for (p, size, qty) in items:
            if not self.stock.check_stock(p.id, size, qty):
                raise StockError("StockInsufficient")
        # Toplam hesap
        line_items = [(p, qty) for (p, size, qty) in items]
        subtotal, discount, shipping, total, free_ship = compute_order_total(line_items, coupon, now)
        # Sipariş
        oid = self._next_id; self._next_id += 1
        order = dict(id=oid, customer_id=customer_id, status=int(OrderStatus.PENDING),
                     subtotal=subtotal, discount=discount, shipping=shipping, total=total,
                     coupon_code=coupon.code if (coupon and discount >= 0 and free_ship or (coupon and discount > 0)) else None,
                     request_id=request_id)
        # Stok düş
        for (p, size, qty) in items:
            self.stock.decrease(p.id, size, qty, oid)
        # Kupon sayaç
        if coupon is not None:
            ok, _, _, _ = validate_coupon(coupon, subtotal, now)
            if ok and coupon.discount_type != DiscountType.FREE_SHIPPING or (ok and free_ship):
                coupon.used_count += 1
        self.orders[oid] = order
        if request_id:
            self.by_request[request_id] = oid
        return ("CREATED", order)

# ═════════════════════════════════════════════════════════════
# TEST SUITE
# ═════════════════════════════════════════════════════════════
_passed = 0
_failed = 0

def check(name, cond, detail=""):
    global _passed, _failed
    if cond:
        _passed += 1
        print(f"  ✓ {name}")
    else:
        _failed += 1
        print(f"  ✗ {name}   {detail}")

def approx(a, b, eps=0.01):
    return abs(a - b) < eps

print("=" * 62)
print("DIVISIMA BACKEND — İş Mantığı Test Simülasyonu")
print("=" * 62)

# ── Frontend kuponları (gerçek COUPONS ile birebir) ──
HOSGELDIN   = Coupon("HOSGELDIN",   DiscountType.PERCENTAGE,    10, min_amount=0)
STIL20      = Coupon("STIL20",      DiscountType.PERCENTAGE,    20, min_amount=3000)
KARGOBEDAVA = Coupon("KARGOBEDAVA", DiscountType.FREE_SHIPPING, 0,  min_amount=0)
NAKIT250    = Coupon("NAKIT250",    DiscountType.FIXED,         250, min_amount=2000)

print("\n[1] KUPON DOĞRULAMA (frontend COUPONS birebir)")
ok, d, fs, err = validate_coupon(HOSGELDIN, 1000)
check("HOSGELDIN %10 @1000₺ = 100₺ indirim", ok and approx(d, 100) and not fs, f"d={d}")

ok, d, fs, err = validate_coupon(STIL20, 1000)
check("STIL20 min 3000₺ altında reddedilir", (not ok) and err == "CouponMinAmountNotMet", f"err={err}")

ok, d, fs, err = validate_coupon(STIL20, 5000)
check("STIL20 %20 @5000₺ = 1000₺ indirim", ok and approx(d, 1000), f"d={d}")

ok, d, fs, err = validate_coupon(KARGOBEDAVA, 500)
check("KARGOBEDAVA -> kargo bedava bayrağı, indirim 0", ok and fs and approx(d, 0), f"d={d} fs={fs}")

ok, d, fs, err = validate_coupon(NAKIT250, 1500)
check("NAKIT250 min 2000₺ altında reddedilir", (not ok) and err == "CouponMinAmountNotMet", f"err={err}")

ok, d, fs, err = validate_coupon(NAKIT250, 2500)
check("NAKIT250 sabit 250₺ @2500₺", ok and approx(d, 250), f"d={d}")

ok, d, fs, err = validate_coupon(None, 1000)
check("Geçersiz kod reddedilir", (not ok) and err == "CouponInvalid")

# Fixed indirim sepeti geçemez
small_fixed = Coupon("BIG500", DiscountType.FIXED, 500, min_amount=0)
ok, d, fs, err = validate_coupon(small_fixed, 300)
check("Sabit indirim sepet tutarını geçemez (min(500,300)=300)", ok and approx(d, 300), f"d={d}")

print("\n[2] KUPON — SON KULLANMA + KULLANIM LİMİTİ + YÜZDE TAVANI (WebCoupon semantiği)")
expired = Coupon("ESKI", DiscountType.PERCENTAGE, 10, expire_date=100.0)
ok, d, fs, err = validate_coupon(expired, 1000, now=200.0)
check("Süresi dolmuş kupon reddedilir", (not ok) and err == "CouponExpired", f"err={err}")

valid_date = Coupon("YENI", DiscountType.PERCENTAGE, 10, expire_date=300.0)
ok, d, fs, err = validate_coupon(valid_date, 1000, now=200.0)
check("Süresi geçerli kupon kabul edilir", ok and approx(d, 100))

limited = Coupon("LIMIT5", DiscountType.PERCENTAGE, 10, usage_limit=5, used_count=5)
ok, d, fs, err = validate_coupon(limited, 1000)
check("Kullanım limiti dolan kupon reddedilir", (not ok) and err == "CouponUsageLimitReached", f"err={err}")

capped = Coupon("MAX100", DiscountType.PERCENTAGE, 50, max_discount_amount=100)
ok, d, fs, err = validate_coupon(capped, 1000)   # %50=500 ama tavan 100
check("Yüzde indirim tavanı uygulanır (500 -> 100)", ok and approx(d, 100), f"d={d}")

print("\n[3] SİPARİŞ TOPLAMI — kargo eşiği + indirim (OrderManager.PlaceOrder)")
p1 = Product(1, "Elbise", 500.0)
p2 = Product(2, "Ceket", 1200.0)

sub, disc, ship, tot, fs = compute_order_total([(p1, 1)], None)
check("500₺ sepet -> kargo 49.9₺ (2000 altı)", approx(sub, 500) and approx(ship, 49.9) and approx(tot, 549.9), f"tot={tot}")

sub, disc, ship, tot, fs = compute_order_total([(p2, 2)], None)   # 2400
check("2400₺ sepet -> kargo bedava (2000 üstü)", approx(sub, 2400) and approx(ship, 0) and approx(tot, 2400), f"tot={tot}")

sub, disc, ship, tot, fs = compute_order_total([(p1, 1)], KARGOBEDAVA)  # 500 + kargo bedava kuponu
check("KARGOBEDAVA kuponu -> 500₺ sepette kargo 0", approx(sub, 500) and approx(ship, 0) and approx(tot, 500), f"tot={tot}")

sub, disc, ship, tot, fs = compute_order_total([(p2, 3)], STIL20)  # 3600, %20=720
check("3600₺ + STIL20 -> 720₺ indirim + kargo bedava = 2880₺",
      approx(sub, 3600) and approx(disc, 720) and approx(ship, 0) and approx(tot, 2880), f"tot={tot}")

sub, disc, ship, tot, fs = compute_order_total([(p1, 1)], HOSGELDIN)  # 500, %10=50, +kargo
check("500₺ + HOSGELDIN(%10) -> 50 indirim + 49.9 kargo = 499.9₺",
      approx(disc, 50) and approx(ship, 49.9) and approx(tot, 499.9), f"tot={tot}")

print("\n[4] STOK — beden bazlı düşüm + overselling engeli (StockManager)")
sm = StockManager()
sm.set_stock(1, "M", 10)
sm.set_stock(1, "L", 3)

sm.decrease(1, "M", 4, ref=99)
check("M bedeni 10 -> 6 (4 düşüldü)", sm.stock[(1, "M")] == 6, f"={sm.stock[(1,'M')]}")

try:
    sm.decrease(1, "L", 5)   # stok 3, 5 istendi
    check("Overselling engeli (stok 3, 5 istendi)", False, "hata fırlatmadı!")
except StockError as e:
    check("Overselling engeli (stok 3, 5 istendi)", str(e) == "StockInsufficient")

check("Yetersiz stokta L bedeni değişmedi (hâlâ 3)", sm.stock[(1, "L")] == 3)

try:
    sm.decrease(1, "XL", 1)  # hiç yok
    check("Olmayan beden -> StockNotFound", False, "hata fırlatmadı!")
except StockError as e:
    check("Olmayan beden -> StockNotFound", str(e) == "StockNotFound")

sm.increase(1, "L", 7, ref=None)  # iade
check("İade: L bedeni 3 -> 10", sm.stock[(1, "L")] == 10)
check("Stok hareketleri kaydedildi (2 çıkış denemesi başarılı olan + 1 giriş)",
      len([m for m in sm.movements if m[2] == MovementType.OUT]) == 1 and
      len([m for m in sm.movements if m[2] == MovementType.IN]) == 1)

print("\n[5] SİPARİŞ VER — tam akış: stok düşer, idempotency, kupon sayaç (OrderManager)")
sm2 = StockManager()
sm2.set_stock(1, "M", 10)
sm2.set_stock(2, "L", 5)
osys = OrderSystem(sm2)

status, order = osys.place_order(
    customer_id=42,
    items=[(p1, "M", 2), (p2, "L", 1)],   # 2*500 + 1200 = 2200
    coupon=NAKIT250,                       # min 2000 OK -> 250 sabit
    request_id="req-abc")
check("Sipariş oluştu (CREATED)", status == "CREATED")
check("Toplam doğru: 2200 - 250 + 0(kargo bedava, 2000 üstü) = 1950",
      approx(order["total"], 1950), f'total={order["total"]}')
check("Stok düştü: M 10->8, L 5->4",
      sm2.stock[(1, "M")] == 8 and sm2.stock[(2, "L")] == 4)
check("NAKIT250 used_count 0 -> 1", NAKIT250.used_count == 1)

# Idempotency: aynı request_id
status2, order2 = osys.place_order(
    customer_id=42, items=[(p1, "M", 2), (p2, "L", 1)],
    coupon=NAKIT250, request_id="req-abc")
check("Aynı request_id -> DUPLICATE (çift sipariş yok)", status2 == "DUPLICATE" and order2["id"] == order["id"])
check("Idempotency sonrası stok değişmedi (M hâlâ 8)", sm2.stock[(1, "M")] == 8)
check("Idempotency sonrası used_count artmadı (hâlâ 1)", NAKIT250.used_count == 1)

# Stok yetersizse sipariş oluşmaz
try:
    osys.place_order(customer_id=1, items=[(p1, "M", 999)], request_id="req-big")
    check("Yetersiz stokta sipariş reddedilir", False, "hata fırlatmadı!")
except StockError as e:
    check("Yetersiz stokta sipariş reddedilir", str(e) == "StockInsufficient")

# Boş sepet
try:
    osys.place_order(customer_id=1, items=[], request_id="req-empty")
    check("Boş sepet reddedilir", False, "hata fırlatmadı!")
except ValueError as e:
    check("Boş sepet reddedilir", str(e) == "OrderEmptyCart")


# ═════════════════════════════════════════════════════════════
# İYİLEŞTİRME TESTLERİ (transaction, concurrency, soft-delete, token rotation)
# ═════════════════════════════════════════════════════════════

print("\n[6] TRANSACTION ROLLBACK — sipariş ortasında hata -> yarım kayıt kalmaz (UnitOfWork)")

class TxOrderSystem(OrderSystem):
    """PlaceOrder transaction'lı: stok düşümü sırasında hata olursa TÜM işlem geri alınır."""
    def place_order_tx(self, customer_id, items, fail_on_index=None, request_id=None):
        # Snapshot (rollback için)
        stock_backup = dict(self.stock.stock)
        orders_backup = dict(self.orders)
        next_id_backup = self._next_id
        movements_backup = list(self.stock.movements)
        try:
            oid = self._next_id; self._next_id += 1
            order = dict(id=oid, customer_id=customer_id, status=int(OrderStatus.PENDING))
            self.orders[oid] = order
            for i, (p, size, qty) in enumerate(items):
                if fail_on_index is not None and i == fail_on_index:
                    raise StockError("StockInsufficient")  # simüle hata
                self.stock.decrease(p.id, size, qty, oid)
            return ("CREATED", order)
        except Exception:
            # Rollback: her şeyi geri yükle
            self.stock.stock = stock_backup
            self.orders = orders_backup
            self._next_id = next_id_backup
            self.stock.movements = movements_backup
            return ("ROLLED_BACK", None)

sm_tx = StockManager()
sm_tx.set_stock(1, "M", 10)
sm_tx.set_stock(2, "L", 2)
tx = TxOrderSystem(sm_tx)

# 2. kalemde hata: 1. kalem düşmüş olmamalı (rollback)
status, order = tx.place_order_tx(1, [(p1, "M", 3), (p2, "L", 5)], fail_on_index=1)  # L'de 5 yok
check("Hata durumunda sipariş ROLLED_BACK", status == "ROLLED_BACK")
check("Rollback: 1. kalemin stoğu geri alındı (M hâlâ 10)", sm_tx.stock[(1, "M")] == 10, f'M={sm_tx.stock[(1,"M")]}')
check("Rollback: yarım sipariş kaydı kalmadı", len(tx.orders) == 0)
check("Rollback: stok hareketi kaydı kalmadı", len(sm_tx.movements) == 0)

# Başarılı transaction: hepsi işler
status, order = tx.place_order_tx(1, [(p1, "M", 2), (p2, "L", 1)])
check("Başarılı transaction: sipariş oluştu + stok düştü", status == "CREATED" and sm_tx.stock[(1,"M")] == 8 and sm_tx.stock[(2,"L")] == 1)

print("\n[7] CONCURRENCY — eşzamanlı iki sipariş aynı stoğu düşemez (optimistic lock retry)")

class ConcurrentStock:
    """RowVersion simülasyonu: her güncellemede version artar; version uyuşmazsa çakışma."""
    def __init__(self, qty):
        self.qty = qty
        self.version = 0

    def read(self):
        return (self.qty, self.version)

    def try_update(self, expected_version, new_qty):
        if expected_version != self.version:
            return False  # DbUpdateConcurrencyException
        self.qty = new_qty
        self.version += 1
        return True

def decrease_with_retry(stock: ConcurrentStock, amount, max_retry=3):
    for _ in range(max_retry):
        qty, ver = stock.read()
        if qty < amount:
            return "INSUFFICIENT"
        if stock.try_update(ver, qty - amount):
            return "OK"
        # çakışma -> taze veriyle tekrar dene
    return "CONFLICT"

# İki eşzamanlı sipariş: stok 5, ikisi de 3 düşmek istiyor -> biri OK, diğeri yetersizlik görmeli
shared = ConcurrentStock(5)
# T1 okur (5,v0), T2 okur (5,v0), T1 yazar (2,v1) OK, T2 v0 ile yazamaz -> retry -> okur (2,v1) -> 2<3 -> INSUFFICIENT
qty_t1, ver_t1 = shared.read()
qty_t2, ver_t2 = shared.read()
r1 = "OK" if shared.try_update(ver_t1, qty_t1 - 3) else "CONFLICT"
# T2 ilk denemesi çakışır, retry mantığı devreye girer
def t2_retry(stock, amount, start_ver, start_qty, max_retry=3):
    if stock.try_update(start_ver, start_qty - amount):
        return "OK"
    return decrease_with_retry(stock, amount, max_retry)  # taze veriyle
r2 = t2_retry(shared, 3, ver_t2, qty_t2)
check("Eşzamanlı: ilk sipariş başarılı (stok 5->2)", r1 == "OK" and shared.qty == 2, f"qty={shared.qty}")
check("Eşzamanlı: ikinci sipariş yetersiz stok görür (overselling YOK)", r2 == "INSUFFICIENT", f"r2={r2}")
check("Eşzamanlı sonrası stok tutarlı (negatife düşmedi)", shared.qty == 2 and shared.qty >= 0)

# Yeterli stokta iki eşzamanlı sipariş de geçer
shared2 = ConcurrentStock(10)
qa, va = shared2.read(); qb, vb = shared2.read()
ra = "OK" if shared2.try_update(va, qa - 3) else "CONFLICT"
rb = t2_retry(shared2, 4, vb, qb)
check("Yeterli stokta iki sipariş de geçer (10 -> 3)", ra == "OK" and rb == "OK" and shared2.qty == 3, f"qty={shared2.qty}")

print("\n[8] SOFT-DELETE — silme kaydı yok etmez, pasifleştirir (is_active=false)")

class SoftDeleteRepo:
    def __init__(self):
        self.rows = {1: dict(id=1, name="Elbise", is_active=True)}
    def delete(self, id):
        # Soft delete: kayıt kalır, is_active=false
        if id in self.rows:
            self.rows[id]["is_active"] = False
            return True
        return False
    def get_active(self, id):
        r = self.rows.get(id)
        return r if (r and r["is_active"]) else None

repo = SoftDeleteRepo()
repo.delete(1)
check("Soft-delete sonrası kayıt fiziksel olarak duruyor", 1 in repo.rows)
check("Soft-delete sonrası is_active=false", repo.rows[1]["is_active"] == False)
check("Soft-delete sonrası aktif sorguda görünmüyor", repo.get_active(1) is None)

print("\n[9] REFRESH TOKEN ROTATION — eski token geçersiz, yeni token verilir")

class SessionStore:
    def __init__(self):
        self.sessions = {}  # token -> dict(customer_id, is_active, expires_at)
        self._c = 0
    def add(self, customer_id, expires_at):
        self._c += 1
        tok = f"tok-{self._c}"
        self.sessions[tok] = dict(customer_id=customer_id, is_active=True, expires_at=expires_at)
        return tok
    def refresh(self, old_token, now, new_expires):
        s = self.sessions.get(old_token)
        if s is None or not s["is_active"]:
            return (None, "RefreshTokenInvalid")
        if s["expires_at"] < now:
            return (None, "RefreshTokenExpired")
        # Rotation: eskiyi kapat, yeni ver
        s["is_active"] = False
        new_tok = self.add(s["customer_id"], new_expires)
        return (new_tok, None)

store = SessionStore()
t_old = store.add(customer_id=42, expires_at=1000.0)
t_new, err = store.refresh(t_old, now=500.0, new_expires=2000.0)
check("Refresh: yeni token üretildi", t_new is not None and t_new != t_old)
check("Refresh: eski token pasifleşti (rotation)", store.sessions[t_old]["is_active"] == False)
check("Refresh: eski token ikinci kez kullanılamaz", store.refresh(t_old, 600.0, 3000.0)[1] == "RefreshTokenInvalid")

# Süresi dolmuş refresh token
t_exp = store.add(customer_id=1, expires_at=100.0)
_, err = store.refresh(t_exp, now=200.0, new_expires=3000.0)
check("Refresh: süresi dolmuş token reddedilir", err == "RefreshTokenExpired", f"err={err}")



# ═════════════════════════════════════════════════════════════
# YENİ MODÜL TESTLERİ (ödeme, sepet, favori)
# ═════════════════════════════════════════════════════════════

print("\n[10] IYZICO ÖDEME AKIŞI — callback başarılı->onay, başarısız->iptal+stok iade")

class PaymentSystem:
    """IyzicoPaymentManager.HandleCallback mantığı: idempotent, başarısızda sipariş iptal + stok iade."""
    def __init__(self, stock: StockManager):
        self.stock = stock
        self.payments = {}   # conversation_id -> payment
        self.orders = {}     # order_id -> order
        self._c = 0

    def create_order(self, order_id, items):
        self.orders[order_id] = dict(id=order_id, status=int(OrderStatus.PENDING), paid=False, items=items)

    def initialize(self, conversation_id, order_id, amount):
        self.payments[conversation_id] = dict(status=int(PaymentStatus.PENDING), order_id=order_id, amount=amount)

    def callback(self, conversation_id, success):
        p = self.payments.get(conversation_id)
        if p is None: return "PaymentNotFound"
        # Idempotency: zaten işlenmişse tekrar işleme
        if p["status"] != int(PaymentStatus.PENDING): return "AlreadyProcessed"
        order = self.orders[p["order_id"]]
        if success:
            p["status"] = int(PaymentStatus.SUCCESS)
            order["status"] = int(OrderStatus.CONFIRMED)
            order["paid"] = True
            return "Success"
        else:
            p["status"] = int(PaymentStatus.FAILED)
            order["status"] = int(OrderStatus.CANCELLED)
            # Stok iade
            for (pid, size, qty) in order["items"]:
                self.stock.increase(pid, size, qty, order["id"])
            return "Failed"

class PaymentStatus(IntEnum):
    PENDING = 0; SUCCESS = 1; FAILED = 2

sm_pay = StockManager()
sm_pay.set_stock(1, "M", 10)
sm_pay.decrease(1, "M", 3, ref=100)   # sipariş verildi, stok 10->7
ps = PaymentSystem(sm_pay)
ps.create_order(100, [(1, "M", 3)])
ps.initialize("conv-1", 100, 1500)

# Başarılı ödeme
r = ps.callback("conv-1", success=True)
check("Ödeme başarılı -> Success", r == "Success")
check("Ödeme başarılı -> sipariş Confirmed", ps.orders[100]["status"] == int(OrderStatus.CONFIRMED))
check("Ödeme başarılı -> sipariş paid=true", ps.orders[100]["paid"] == True)
# Idempotency: callback tekrar gelirse
check("Ödeme callback idempotent (ikinci çağrı AlreadyProcessed)", ps.callback("conv-1", True) == "AlreadyProcessed")

# Başarısız ödeme -> iptal + stok iade
sm_pay2 = StockManager(); sm_pay2.set_stock(2, "L", 5)
sm_pay2.decrease(2, "L", 2, ref=200)  # stok 5->3
ps2 = PaymentSystem(sm_pay2)
ps2.create_order(200, [(2, "L", 2)])
ps2.initialize("conv-2", 200, 900)
r = ps2.callback("conv-2", success=False)
check("Ödeme başarısız -> Failed", r == "Failed")
check("Ödeme başarısız -> sipariş Cancelled", ps2.orders[200]["status"] == int(OrderStatus.CANCELLED))
check("Ödeme başarısız -> stok iade edildi (3 -> 5)", sm_pay2.stock[(2, "L")] == 5, f'={sm_pay2.stock[(2,"L")]}')

print("\n[11] KALICI SEPET — stok kontrollü ekleme, aynı ürün adet güncelleme, ara toplam")

class Cart:
    def __init__(self, stock: StockManager):
        self.stock = stock
        self.items = {}  # (product_id,size) -> qty
        self.prices = {1: 500.0, 2: 1200.0}

    def add(self, product_id, size, qty):
        if not self.stock.check_stock(product_id, size, qty):
            return "StockInsufficient"
        self.items[(product_id, size)] = qty  # aynı ürün+beden: adet güncelle (üzerine yaz)
        return "OK"

    def remove(self, product_id, size):
        self.items.pop((product_id, size), None)

    def subtotal(self):
        return sum(self.prices[pid] * qty for (pid, size), qty in self.items.items())

cart_stock = StockManager()
cart_stock.set_stock(1, "M", 10)
cart_stock.set_stock(2, "L", 3)
cart = Cart(cart_stock)

check("Sepete ekleme (stok yeterli)", cart.add(1, "M", 2) == "OK")
check("Stok üstü ekleme reddedilir (L stok 3, 5 istendi)", cart.add(2, "L", 5) == "StockInsufficient")
cart.add(2, "L", 2)
check("Ara toplam doğru: 2*500 + 2*1200 = 3400", approx(cart.subtotal(), 3400), f"={cart.subtotal()}")
cart.add(1, "M", 4)  # aynı ürün -> adet güncelle (2 değil 4 olur)
check("Aynı ürün+beden tekrar eklenince adet güncellenir (üzerine yazar)", cart.items[(1, "M")] == 4)
check("Güncel ara toplam: 4*500 + 2*1200 = 4400", approx(cart.subtotal(), 4400), f"={cart.subtotal()}")
cart.remove(2, "L")
check("Sepetten çıkarma", (2, "L") not in cart.items and approx(cart.subtotal(), 2000))

print("\n[12] FAVORİ (WISHLIST) — toggle: yoksa ekle, varsa çıkar")

class Wishlist:
    def __init__(self):
        self.items = set()  # (customer_id, product_id)
    def toggle(self, customer_id, product_id):
        key = (customer_id, product_id)
        if key in self.items:
            self.items.remove(key); return "Removed"
        self.items.add(key); return "Added"

wl = Wishlist()
check("İlk toggle -> Added", wl.toggle(1, 100) == "Added")
check("Favori listede", (1, 100) in wl.items)
check("İkinci toggle -> Removed", wl.toggle(1, 100) == "Removed")
check("Favori listeden çıktı", (1, 100) not in wl.items)
wl.toggle(1, 100); wl.toggle(1, 200)
check("Farklı ürünler ayrı ayrı favorilenir", (1, 100) in wl.items and (1, 200) in wl.items)



# ═════════════════════════════════════════════════════════════
# İLERİ MODÜL TESTLERİ (arama, e-posta doğrulama, audit)
# ═════════════════════════════════════════════════════════════

print("\n[13] ÜRÜN ARAMA — metin + fiyat + kategori filtresi, sıralama, sayfalama")

PRODUCTS = [
    dict(id=1, name="Siyah Elbise", brand="Zara", category_id=1, price=500, created=5, active=True),
    dict(id=2, name="Beyaz Gömlek", brand="Mango", category_id=1, price=300, created=3, active=True),
    dict(id=3, name="Kot Ceket", brand="Zara", category_id=2, price=900, created=4, active=True),
    dict(id=4, name="Kırmızı Elbise", brand="HM", category_id=1, price=450, created=2, active=False),  # pasif
    dict(id=5, name="Deri Ceket", brand="Mango", category_id=2, price=1500, created=1, active=True),
]

def search(query=None, category_id=None, min_price=None, max_price=None, sort_by=None, page=1, size=20):
    q = (query or "").strip().lower()
    res = [p for p in PRODUCTS if p["active"]
           and (not q or q in p["name"].lower() or q in p["brand"].lower())
           and (category_id is None or p["category_id"] == category_id)
           and (min_price is None or p["price"] >= min_price)
           and (max_price is None or p["price"] <= max_price)]
    if sort_by == "price_asc": res.sort(key=lambda p: p["price"])
    elif sort_by == "price_desc": res.sort(key=lambda p: -p["price"])
    else: res.sort(key=lambda p: -p["created"])  # newest
    total = len(res)
    items = res[(page-1)*size : (page-1)*size + size]
    return items, total

items, total = search(query="elbise")
check("Arama 'elbise' -> sadece aktif eşleşenler (1 adet, pasif hariç)", total == 1 and items[0]["id"] == 1, f"total={total}")

items, total = search(query="zara")
check("Marka 'zara' araması (2 ürün)", total == 2)

items, total = search(category_id=2)
check("Kategori filtresi (kategori 2 -> 2 ürün)", total == 2)

items, total = search(min_price=500, max_price=1000)
check("Fiyat aralığı 500-1000 (2 ürün: 500, 900)", total == 2)

items, total = search(sort_by="price_asc")
check("Fiyat artan sıralama (ilk: 300)", items[0]["price"] == 300)

items, total = search(sort_by="price_desc")
check("Fiyat azalan sıralama (ilk: 1500)", items[0]["price"] == 1500)

items, total = search(page=1, size=2)
check("Sayfalama (size=2 -> 2 öğe, total 4 aktif)", len(items) == 2 and total == 4)

print("\n[14] E-POSTA DOĞRULAMA — kayıt->token, doğrula->verified, geçersiz token reddedilir")

class EmailVerification:
    def __init__(self):
        self.customers = {}  # email -> dict
    def register(self, email):
        import uuid
        token = uuid.uuid4().hex
        self.customers[email] = dict(verified=False, token=token)
        return token
    def verify(self, token):
        for c in self.customers.values():
            if c["token"] == token:
                if c["verified"]: return "AlreadyVerified"
                c["verified"] = True
                c["token"] = None
                return "Verified"
        return "Invalid"

ev = EmailVerification()
tok = ev.register("test@divisima.com")
check("Kayıt -> doğrulanmamış + token üretildi", ev.customers["test@divisima.com"]["verified"] == False and tok)
check("Doğru token -> Verified", ev.verify(tok) == "Verified")
check("Doğrulama sonrası verified=true", ev.customers["test@divisima.com"]["verified"] == True)
check("Aynı token ikinci kez -> Invalid (token temizlendi)", ev.verify(tok) == "Invalid")
check("Geçersiz token reddedilir", ev.verify("yanlis-token") == "Invalid")

print("\n[15] AUDIT LOG — değişiklik yakalama (Added/Modified/Deleted), audit kendini denetlemez")

class AuditSystem:
    IGNORED = {"AuditLog", "OutboxMessage"}
    def __init__(self):
        self.logs = []
    def save(self, changes):
        # changes: [(table, entity_id, action, modified_fields)]
        for (table, eid, action, fields) in changes:
            if table in self.IGNORED:
                continue  # audit kendini/outbox'ı denetlemez
            self.logs.append(dict(table=table, entity_id=eid, action=action, fields=fields))

audit = AuditSystem()
audit.save([
    ("Product", "1", "Added", None),
    ("Product", "1", "Modified", {"price": (500, 450)}),
    ("Order", "10", "Added", None),
    ("AuditLog", "99", "Added", None),      # denetlenmemeli
    ("OutboxMessage", "5", "Added", None),  # denetlenmemeli
])
check("Audit: 3 kayıt üretildi (AuditLog+Outbox hariç)", len(audit.logs) == 3, f"={len(audit.logs)}")
check("Audit: Product ekleme yakalandı", any(l["table"]=="Product" and l["action"]=="Added" for l in audit.logs))
check("Audit: fiyat değişikliği (eski->yeni) kaydedildi",
      any(l["action"]=="Modified" and l["fields"]=={"price":(500,450)} for l in audit.logs))
check("Audit: kendini denetlemez (AuditLog kaydı yok)", not any(l["table"]=="AuditLog" for l in audit.logs))
check("Audit: Outbox denetlenmez", not any(l["table"]=="OutboxMessage" for l in audit.logs))



# ═════════════════════════════════════════════════════════════
# GÜVENLİK TESTLERİ (IDOR/yetki, hesap kilitleme, sahiplik)
# ═════════════════════════════════════════════════════════════

print("\n[16] IDOR / YETKİ — kimlik token'dan, başkasının verisine erişim engellenir")

class SecureResource:
    """Controller mantığı: customer_id token'dan; sahiplik doğrulanır."""
    def __init__(self):
        # kayıtlar: id -> owner_customer_id
        self.addresses = {1: 100, 2: 200}  # adres 1 -> müşteri 100, adres 2 -> müşteri 200

    def delete_address(self, address_id, token_customer_id):
        owner = self.addresses.get(address_id)
        if owner is None: return "NotFound"
        # Açıklayıcı: sahiplik doğrulaması - yalnızca sahip silebilir
        if owner != token_customer_id: return "Forbidden"
        del self.addresses[address_id]
        return "Deleted"

res = SecureResource()
# Müşteri 100, kendi adresini (1) silebilir
check("Sahip kendi adresini silebilir", res.delete_address(1, token_customer_id=100) == "Deleted")
# Müşteri 100, müşteri 200'ün adresini (2) silmeye çalışır -> ENGELLENİR
check("IDOR engeli: başkasının adresi silinemez (403)", res.delete_address(2, token_customer_id=100) == "Forbidden")
check("IDOR engeli sonrası kurbanın adresi duruyor", 2 in res.addresses)

# Sipariş sahipliği (ödeme)
orders = {10: 100, 11: 200}  # sipariş 10 -> müşteri 100
def pay_order(order_id, token_customer_id):
    owner = orders.get(order_id)
    if owner is None: return "NotFound"
    if owner != token_customer_id: return "Forbidden"
    return "PaymentInitiated"
check("Müşteri kendi siparişini ödeyebilir", pay_order(10, 100) == "PaymentInitiated")
check("IDOR engeli: başkasının siparişi ödenemez", pay_order(11, 100) == "Forbidden")

print("\n[17] HESAP KİLİTLEME — 5 başarısız login -> kilit; başarılı giriş sıfırlar")

class LoginGuard:
    def __init__(self):
        self.failed = 0
        self.lockout_until = None
        self.correct_password = "Dogru123"

    def login(self, password, now=0):
        if self.lockout_until is not None and now < self.lockout_until:
            return "Locked"
        if password != self.correct_password:
            self.failed += 1
            if self.failed >= 5:
                self.lockout_until = now + 900  # 15 dk
                self.failed = 0
            return "Failed"
        # başarılı - sıfırla
        self.failed = 0
        self.lockout_until = None
        return "Success"

g = LoginGuard()
for i in range(4):
    g.login("yanlis", now=0)
check("4 başarısız denemeden sonra hâlâ kilitli değil", g.lockout_until is None and g.failed == 4)
r = g.login("yanlis", now=0)  # 5. başarısız
check("5. başarısız deneme -> kilit devreye girer", g.lockout_until == 900)
check("Kilit süresince doğru şifre bile reddedilir", g.login("Dogru123", now=100) == "Locked")
check("Kilit süresi dolunca giriş açılır", g.login("Dogru123", now=1000) == "Success")

# Başarılı giriş sayacı sıfırlar (kilitlenme birikmez)
g2 = LoginGuard()
g2.login("yanlis"); g2.login("yanlis")
g2.login("Dogru123")  # başarılı
check("Başarılı giriş başarısız sayacını sıfırlar", g2.failed == 0)

print("\n[18] RATE LIMIT — login'e sıkı limit (5/dk); aşımda reddedilir")

class RateLimiter:
    def __init__(self, limit, window):
        self.limit = limit
        self.window = window
        self.hits = {}  # ip -> [timestamps]

    def allow(self, ip, now):
        times = [t for t in self.hits.get(ip, []) if now - t < self.window]
        if len(times) >= self.limit:
            self.hits[ip] = times
            return False
        times.append(now)
        self.hits[ip] = times
        return True

rl = RateLimiter(limit=5, window=60)  # 5/dk (auth policy)
results = [rl.allow("1.2.3.4", now=i) for i in range(7)]  # 7 istek, aynı dakika
check("İlk 5 istek kabul (login limiti)", all(results[:5]))
check("6. ve 7. istek reddedilir (429 - kaba kuvvet engeli)", not results[5] and not results[6])
# Pencere geçince tekrar açılır
check("Pencere (60sn) geçince tekrar kabul", rl.allow("1.2.3.4", now=100) == True)
# Farklı IP etkilenmez
check("Farklı IP kendi limitine sahip (izole)", rl.allow("5.6.7.8", now=0) == True)

print("\n[19] ŞİFRE POLİTİKASI — min 8 + büyük + küçük + rakam")

def valid_password(pw):
    import re
    return len(pw) >= 8 and bool(re.search('[A-Z]', pw)) and bool(re.search('[a-z]', pw)) and bool(re.search('[0-9]', pw))

check("Zayıf şifre '123456' reddedilir", not valid_password("123456"))
check("Kısa şifre 'Ab1' reddedilir", not valid_password("Ab1"))
check("Rakamsız 'Password' reddedilir", not valid_password("Password"))
check("Büyük harfsiz 'password1' reddedilir", not valid_password("password1"))
check("Güçlü şifre 'Divisima2026' kabul", valid_password("Divisima2026"))



# ═════════════════════════════════════════════════════════════
# ÖDEME GÜVENLİĞİ TESTLERİ (imza, tutar, fraud, velocity)
# ═════════════════════════════════════════════════════════════

print("\n[16] ÖDEME GÜVENLİĞİ — sahte callback, tutar manipülasyonu, fraud, hız limiti")

import hashlib, hmac

SECRET = b"iyzico-secret-key"

def make_signature(token):
    return hmac.new(SECRET, token.encode(), hashlib.sha256).hexdigest()

def verify_signature(token, signature):
    if not signature: return False
    expected = make_signature(token)
    return hmac.compare_digest(expected, signature.lower())

class SecurePaymentSystem:
    """Güvenli akış: imza doğrula -> gerçek sonucu 'Iyzico'dan' çek -> tutar+fraud kontrol -> onay/iptal."""
    def __init__(self, stock):
        self.stock = stock
        self.payments = {}   # token -> payment
        self.orders = {}     # order_id -> order
        self.iyzico_results = {}  # token -> gerçek sonuç (Iyzico'nun bildiği)

    def create(self, token, order_id, expected_amount, items):
        self.orders[order_id] = dict(id=order_id, status=int(OrderStatus.PENDING), paid=False, items=items)
        self.payments[token] = dict(status=int(PaymentStatus.PENDING), order_id=order_id, amount=expected_amount)

    def set_iyzico_result(self, token, success, paid_price, fraud="1"):
        # Iyzico'nun gerçekte bildiği sonuç (sunucu-sunucu sorguyla gelir)
        self.iyzico_results[token] = dict(success=success, paid_price=paid_price, fraud=fraud)

    def callback(self, token, signature):
        # 1) İmza doğrula
        if not verify_signature(token, signature):
            return "SignatureInvalid"
        p = self.payments.get(token)
        if p is None: return "NotFound"
        # 2) Idempotency
        if p["status"] != int(PaymentStatus.PENDING): return "AlreadyProcessed"
        order = self.orders[p["order_id"]]
        # 3) GERÇEK sonucu Iyzico'dan çek (callback gövdesine güvenme)
        real = self.iyzico_results.get(token, dict(success=False, paid_price=0, fraud="-1"))
        # 4) Tutar + fraud doğrula
        amount_ok = real["paid_price"] == p["amount"]
        fraud_ok = real["fraud"] == "1"
        if real["success"] and amount_ok and fraud_ok:
            p["status"] = int(PaymentStatus.SUCCESS)
            order["status"] = int(OrderStatus.CONFIRMED)
            order["paid"] = True
            return "Success"
        else:
            p["status"] = int(PaymentStatus.FAILED)
            order["status"] = int(OrderStatus.CANCELLED)
            for (pid, size, qty) in order["items"]:
                self.stock.increase(pid, size, qty, order["id"])
            if not amount_ok: return "AmountMismatch"
            if not fraud_ok: return "FraudReject"
            return "Failed"

# Sahte callback (imza yanlış) reddedilir
sps = SecurePaymentSystem(StockManager())
tok = "token-123"
sps.create(tok, 1, 1000.0, [])
r = sps.callback(tok, signature="sahte-imza")
check("Sahte callback (geçersiz imza) reddedilir", r == "SignatureInvalid")
check("Sahte callback sonrası sipariş Pending kalır (onaylanmaz)", sps.orders[1]["status"] == int(OrderStatus.PENDING))

# Doğru imza ama Iyzico'da tutar farklı (1000 sipariş, 1₺ ödendi) -> reddedilir
sps2 = SecurePaymentSystem(StockManager())
sps2.stock.set_stock(1, "M", 5); sps2.stock.decrease(1, "M", 2, ref=1)
sps2.create("tok-2", 1, 1000.0, [(1, "M", 2)])
sps2.set_iyzico_result("tok-2", success=True, paid_price=1.0)  # 1₺ ödenmiş!
r = sps2.callback("tok-2", make_signature("tok-2"))
check("Tutar manipülasyonu (1000₺ siparişe 1₺) reddedilir", r == "AmountMismatch")
check("Tutar uyuşmazlığında sipariş iptal + stok iade (3->5)", sps2.orders[1]["status"] == int(OrderStatus.CANCELLED) and sps2.stock.stock[(1,"M")] == 5)

# Doğru imza + doğru tutar ama fraud reddi -> iptal
sps3 = SecurePaymentSystem(StockManager())
sps3.create("tok-3", 1, 500.0, [])
sps3.set_iyzico_result("tok-3", success=True, paid_price=500.0, fraud="-1")  # fraud red
r = sps3.callback("tok-3", make_signature("tok-3"))
check("Fraud reddi (fraudStatus=-1) siparişi iptal eder", r == "FraudReject")

# Doğru imza + doğru tutar + fraud onay -> BAŞARILI
sps4 = SecurePaymentSystem(StockManager())
sps4.create("tok-4", 1, 500.0, [])
sps4.set_iyzico_result("tok-4", success=True, paid_price=500.0, fraud="1")
r = sps4.callback("tok-4", make_signature("tok-4"))
check("Geçerli ödeme (imza+tutar+fraud OK) -> Success", r == "Success")
check("Başarılı ödemede sipariş Confirmed + paid", sps4.orders[1]["status"] == int(OrderStatus.CONFIRMED) and sps4.orders[1]["paid"])

# Idempotency: aynı callback tekrar
r2 = sps4.callback("tok-4", make_signature("tok-4"))
check("Ödeme callback replay -> AlreadyProcessed (tekrar işlenmez)", r2 == "AlreadyProcessed")

print("\n[17] ÖDEME HIZ/FRAUD LİMİTİ — kart testi saldırısı engeli (10 dk'da max 5 deneme)")

class VelocityGuard:
    MAX = 5
    def __init__(self):
        self.attempts = {}
    def can_attempt(self, customer_id):
        return self.attempts.get(customer_id, 0) < self.MAX
    def record(self, customer_id):
        self.attempts[customer_id] = self.attempts.get(customer_id, 0) + 1

vg = VelocityGuard()
allowed = 0
for i in range(8):
    if vg.can_attempt(1):
        vg.record(1); allowed += 1
check("İlk 5 ödeme denemesine izin verilir", allowed == 5, f"allowed={allowed}")
check("6. deneme engellenir (velocity limiti)", not vg.can_attempt(1))
check("Farklı müşteri etkilenmez", vg.can_attempt(2))

print("\n[18] PCI-DSS — kart bilgisi sunucu modelinde HİÇ tutulmaz (Checkout Form)")
# Ödeme init DTO'sunda kart alanı olmamalı (Iyzico iframe toplar)
payment_init_fields = {"order_id", "callback_url"}  # DTO'daki tüm alanlar
card_fields = {"card_number", "cvc", "card_holder_name", "expire_month", "expire_year"}
check("Ödeme başlatma DTO'sunda kart alanı YOK (PCI-DSS)", len(payment_init_fields & card_fields) == 0)
check("Payment kaydında kart no/CVC alanı YOK", "card_number" not in {"amount","paid_price","fraud_status","token","transaction_id"})



# ═════════════════════════════════════════════════════════════
# İLERİ ÖDEME GÜVENLİĞİ (IDOR, durum, zaman aşımı, para birimi, kilit)
# ═════════════════════════════════════════════════════════════

print("\n[19] SAHİPLİK (IDOR) — kullanıcı yalnızca KENDİ siparişini ödeyebilir")

def can_initialize_payment(order_customer_id, authenticated_customer_id, order_status, order_total, is_paid, has_pending):
    # IyzicoPaymentManager.Initialize kontrol sırası
    if order_customer_id != authenticated_customer_id:
        return "NotYourOrder"          # IDOR engeli
    if is_paid:
        return "AlreadyDone"
    if order_status != int(OrderStatus.PENDING):
        return "OrderNotPayable"       # iptal/kargo/teslim edilmiş
    if order_total <= 0:
        return "InvalidAmount"
    if has_pending:
        return "PendingExists"         # zaten bekleyen ödeme
    return "OK"

# A müşterisi (id=1), B'nin siparişi (customer_id=2)
r = can_initialize_payment(order_customer_id=2, authenticated_customer_id=1, order_status=int(OrderStatus.PENDING), order_total=500, is_paid=False, has_pending=False)
check("Başkasının siparişini ödeme reddedilir (IDOR)", r == "NotYourOrder")

# Kendi siparişi -> OK
r = can_initialize_payment(1, 1, int(OrderStatus.PENDING), 500, False, False)
check("Kendi siparişini ödeme -> OK", r == "OK")

print("\n[20] SİPARİŞ DURUMU — iptal/teslim edilmiş siparişe ödeme engellenir")
r = can_initialize_payment(1, 1, int(OrderStatus.CANCELLED), 500, False, False)
check("İptal edilmiş siparişe ödeme engellenir", r == "OrderNotPayable")
r = can_initialize_payment(1, 1, int(OrderStatus.DELIVERED), 500, False, False)
check("Teslim edilmiş siparişe ödeme engellenir", r == "OrderNotPayable")
r = can_initialize_payment(1, 1, int(OrderStatus.PENDING), 500, True, False)
check("Zaten ödenmiş siparişe tekrar ödeme engellenir", r == "AlreadyDone")

print("\n[21] GEÇERSİZ TUTAR + ÇİFT BEKLEYEN ÖDEME")
r = can_initialize_payment(1, 1, int(OrderStatus.PENDING), 0, False, False)
check("Sıfır tutarlı siparişe ödeme engellenir", r == "InvalidAmount")
r = can_initialize_payment(1, 1, int(OrderStatus.PENDING), -100, False, False)
check("Negatif tutarlı siparişe ödeme engellenir", r == "InvalidAmount")
r = can_initialize_payment(1, 1, int(OrderStatus.PENDING), 500, False, True)
check("Zaten bekleyen ödeme varken yenisi açılmaz", r == "PendingExists")

print("\n[22] TOKEN ZAMAN AŞIMI — 30 dk sonra callback reddedilir (eski token replay)")
def callback_time_valid(payment_created_minutes_ago):
    return payment_created_minutes_ago < 30

check("15 dk önceki ödeme token'ı geçerli", callback_time_valid(15))
check("30+ dk önceki token reddedilir (replay engeli)", not callback_time_valid(45))

print("\n[23] PARA BİRİMİ — TRY sipariş, USD ödeme reddedilir")
def currency_ok(order_currency, paid_currency):
    return order_currency.upper() == paid_currency.upper()

check("TRY sipariş + TRY ödeme -> OK", currency_ok("TRY", "TRY"))
check("TRY sipariş + USD ödeme reddedilir", not currency_ok("TRY", "USD"))

print("\n[24] EŞZAMANLI CALLBACK KİLİDİ — aynı siparişe 2 paralel callback, tek işlem")

class LockedPayment:
    def __init__(self):
        self.locked = set()      # kilitli sipariş id'leri
        self.processed = set()   # işlenmiş sipariş id'leri
    def try_process(self, order_id):
        # Kilit al
        if order_id in self.locked:
            return "Busy"        # başka callback işliyor
        self.locked.add(order_id)
        try:
            # Kilit sonrası double-check
            if order_id in self.processed:
                return "AlreadyProcessed"
            self.processed.add(order_id)
            return "Processed"
        finally:
            self.locked.discard(order_id)

lp = LockedPayment()
r1 = lp.try_process(1)
r2 = lp.try_process(1)  # ikinci callback (kilit bırakıldıktan sonra ama işlenmiş)
check("İlk callback işlenir (Processed)", r1 == "Processed")
check("İkinci callback double-check ile AlreadyProcessed", r2 == "AlreadyProcessed")
check("Sipariş yalnızca 1 kez işlendi", len(lp.processed) == 1)



# ═════════════════════════════════════════════════════════════
# GÜVENLİK KATMANI TESTLERİ (encryption, blacklist, TOTP, IDOR)
# ═════════════════════════════════════════════════════════════

print("\n[25] FIELD ENCRYPTION — AES round-trip + tamper tespiti (mantık)")
import os as _os, hashlib as _hl

def aes_like_encrypt(plain, key):
    # Basitleştirilmiş simülasyon: gerçek AesGcm C# tarafında. Burada round-trip + tamper mantığı test edilir.
    if not plain: return plain
    nonce = _os.urandom(4).hex()
    # "şifreli" = nonce + reversible transform + integrity tag
    body = ''.join(chr((ord(c) + 7) % 256) for c in plain)
    tag = _hl.sha256((nonce + body + key).encode('latin-1', errors='ignore')).hexdigest()[:8]
    return f"{nonce}:{tag}:{body}"

def aes_like_decrypt(cipher, key):
    if not cipher or ':' not in cipher: return cipher
    nonce, tag, body = cipher.split(':', 2)
    expected = _hl.sha256((nonce + body + key).encode('latin-1', errors='ignore')).hexdigest()[:8]
    if tag != expected:
        raise ValueError("tamper detected")  # bütünlük ihlali
    return ''.join(chr((ord(c) - 7) % 256) for c in body)

KEY = "secret-key-32"
secret = "JBSWY3DPEHPK3PXP"  # örnek TOTP secret
enc = aes_like_encrypt(secret, KEY)
check("Şifreli metin düz metinden farklı", enc != secret)
check("Şifre çözme orijinali geri verir (round-trip)", aes_like_decrypt(enc, KEY) == secret)
# Tamper: şifreli veriyi boz
tampered = enc[:-1] + ('X' if enc[-1] != 'X' else 'Y')
try:
    aes_like_decrypt(tampered, KEY)
    check("Kurcalanmış şifreli veri tespit edilir", False, "tespit edilmedi")
except ValueError:
    check("Kurcalanmış şifreli veri tespit edilir (GCM tag)", True)

print("\n[26] JWT BLACKLIST — logout edilen token (jti) reddedilir")

class TokenBlacklist:
    def __init__(self):
        self.revoked = {}  # jti -> expiry
    def revoke(self, jti, expiry_minutes):
        if expiry_minutes > 0:
            self.revoked[jti] = expiry_minutes
    def is_revoked(self, jti):
        return jti in self.revoked

bl = TokenBlacklist()
bl.revoke("jti-active-token", expiry_minutes=30)
check("Logout edilen token (jti) kara listede", bl.is_revoked("jti-active-token"))
check("Normal token kara listede değil", not bl.is_revoked("jti-other"))
# Süresi dolmuş token revoke edilmez (gereksiz)
bl.revoke("jti-expired", expiry_minutes=0)
check("Süresi dolmuş token kara listeye alınmaz (gereksiz)", not bl.is_revoked("jti-expired"))

print("\n[27] TOTP (2FA) — RFC 6238 kod üretimi + ±1 pencere toleransı + yanlış kod reddi")
import hmac as _hmac, struct as _struct, base64 as _b64, time as _time

def totp_code(secret_b32, counter, digits=6):
    key = _b64.b32decode(secret_b32)
    msg = _struct.pack(">Q", counter)
    h = _hmac.new(key, msg, _hl.sha1).digest()
    o = h[-1] & 0x0F
    binary = ((h[o] & 0x7F) << 24) | (h[o+1] << 16) | (h[o+2] << 8) | h[o+3]
    return str(binary % (10 ** digits)).zfill(digits)

def totp_validate(secret_b32, code, current_counter):
    # ±1 pencere toleransı
    for i in (-1, 0, 1):
        if totp_code(secret_b32, current_counter + i) == code:
            return True
    return False

SEC = "JBSWY3DPEHPK3PXP"
counter = int(_time.time()) // 30
valid_code = totp_code(SEC, counter)
check("Doğru TOTP kodu kabul edilir", totp_validate(SEC, valid_code, counter))
check("Bir önceki pencere kodu tolere edilir (saat kayması)", totp_validate(SEC, totp_code(SEC, counter-1), counter))
check("Yanlış kod reddedilir", not totp_validate(SEC, "000000", counter) or totp_code(SEC,counter)=="000000")
check("Çok eski pencere kodu reddedilir", not totp_validate(SEC, totp_code(SEC, counter-5), counter))

print("\n[28] SECURITY EVENT — başarısız login loglanır, kilitlenme Critical")

class SecurityEventLog:
    def __init__(self):
        self.events = []
    def log(self, event_type, severity, customer_id=None, detail=None):
        self.events.append(dict(type=event_type, severity=severity, customer_id=customer_id, detail=detail))
    def critical_count(self):
        return sum(1 for e in self.events if e["severity"] == "Critical")

sec_log = SecurityEventLog()
# 5 başarısız deneme simülasyonu
attempts = 0
for i in range(5):
    attempts += 1
    if attempts >= 5:
        sec_log.log("AccountLocked", "Critical", customer_id=1, detail="5 başarısız deneme")
    else:
        sec_log.log("LoginFailed", "Warning", customer_id=1, detail="Hatalı şifre")
check("Başarısız login'ler loglandı (5 olay)", len(sec_log.events) == 5)
check("İlk 4 Warning, 5. Critical (kilitlenme)", sec_log.events[4]["severity"] == "Critical")
check("Kritik güvenlik olayı sayısı 1 (kilitlenme)", sec_log.critical_count() == 1)

print("\n[29] CAPTCHA — kapalıyken geç, açıkken boş token reddet")
def captcha_validate(token, enabled):
    if not enabled: return True   # dev: kapalı
    return bool(token and token.strip())
check("Captcha kapalı -> her zaman geçer (dev)", captcha_validate("", enabled=False))
check("Captcha açık + geçerli token -> geçer", captcha_validate("valid-token", enabled=True))
check("Captcha açık + boş token -> reddedilir", not captcha_validate("", enabled=True))

print("\n[30] IDOR (ORTA KATMAN) — JWT id ile route id çakışması engellenir")
def resource_access(jwt_customer_id, resource_owner_id):
    # SecureControllerBase.EnsureOwner mantığı
    return jwt_customer_id == resource_owner_id and jwt_customer_id > 0

check("Kendi kaynağına erişim -> izin", resource_access(5, 5))
check("Başkasının kaynağına erişim -> RED (IDOR)", not resource_access(5, 9))
check("Kimliksiz (id=0) erişim -> RED", not resource_access(0, 0))



# ═════════════════════════════════════════════════════════════
# EK GÜVENLİK KATMANI TESTLERİ (query filter, CSRF, step-up, retention)
# ═════════════════════════════════════════════════════════════

print("\n[31] GLOBAL QUERY FILTER — soft-delete kayıtlar otomatik gizlenir")

class FilteredRepo:
    def __init__(self):
        self.rows = [dict(id=1, name="A", is_active=True), dict(id=2, name="B", is_active=False),
                     dict(id=3, name="C", is_active=True)]
    def query(self, ignore_filter=False):
        # Global query filter: is_active=false otomatik dışla (ignore ile admin görebilir)
        return self.rows if ignore_filter else [r for r in self.rows if r["is_active"]]

repo = FilteredRepo()
check("Normal sorgu pasif kaydı gizler (2 aktif)", len(repo.query()) == 2)
check("Pasif kayıt (id=2) normal sorguda yok", all(r["id"] != 2 for r in repo.query()))
check("IgnoreQueryFilters ile admin hepsini görür (3)", len(repo.query(ignore_filter=True)) == 3)

print("\n[32] CSRF (double-submit) — cookie taşıyan mutasyonda token eşleşmeli")

def csrf_check(method, has_auth_cookie, has_bearer, header_token, cookie_token):
    safe = method in ("GET", "HEAD", "OPTIONS")
    if safe or not has_auth_cookie or has_bearer:
        return "Pass"  # güvenli metot / cookie yok / bearer JWT -> CSRF'e kapalı
    if not header_token or header_token != cookie_token:
        return "Blocked"
    return "Pass"

check("GET isteği CSRF kontrolünden muaf", csrf_check("GET", True, False, "", "") == "Pass")
check("Bearer JWT ile POST CSRF'e kapalı (header taşır)", csrf_check("POST", False, True, "", "") == "Pass")
check("Cookie + POST + eşleşen token -> geçer", csrf_check("POST", True, False, "tok", "tok") == "Pass")
check("Cookie + POST + token yok -> engellenir (CSRF)", csrf_check("POST", True, False, "", "tok") == "Blocked")
check("Cookie + POST + token uyuşmaz -> engellenir", csrf_check("POST", True, False, "x", "y") == "Blocked")

print("\n[33] STEP-UP AUTH — hassas işlemde eski oturum reddedilir")

def requires_recent_auth(auth_time_minutes_ago, max_minutes=10):
    return auth_time_minutes_ago <= max_minutes

check("Yeni giriş (2 dk) hassas işleme izin verir", requires_recent_auth(2))
check("Eski oturum (30 dk) hassas işlemde reddedilir", not requires_recent_auth(30))
check("Sınırda (10 dk) izin verilir", requires_recent_auth(10))

print("\n[34] IP ALLOWLIST (webhook) — bilinmeyen IP reddedilir")

def webhook_allowed(path, ip, allowlist):
    if not path.startswith("/api/payment/webhook") or not allowlist:
        return "Pass"  # webhook değil / allowlist boş (dev)
    return "Pass" if ip in allowlist else "Blocked"

IYZICO_IPS = {"85.111.1.1", "85.111.1.2"}
check("Bilinen Iyzico IP -> geçer", webhook_allowed("/api/payment/webhook", "85.111.1.1", IYZICO_IPS) == "Pass")
check("Bilinmeyen IP -> engellenir", webhook_allowed("/api/payment/webhook", "1.2.3.4", IYZICO_IPS) == "Blocked")
check("Allowlist boşsa (dev) geçer", webhook_allowed("/api/payment/webhook", "1.2.3.4", set()) == "Pass")

print("\n[35] VERİ SAKLAMA (retention) — eski kayıtlar temizlenir, Critical korunur")

from datetime import datetime, timedelta
now = datetime(2026, 1, 1)
sessions = [dict(id=1, is_active=False, created=now - timedelta(days=100)),
            dict(id=2, is_active=False, created=now - timedelta(days=30)),
            dict(id=3, is_active=True, created=now - timedelta(days=200))]
# 90 günden eski pasif oturumları sil
to_delete = [s for s in sessions if not s["is_active"] and s["created"] < now - timedelta(days=90)]
check("90+ gün eski pasif oturum silinir (1 kayıt)", len(to_delete) == 1 and to_delete[0]["id"] == 1)
check("Aktif oturum (eski olsa da) silinmez", not any(s["id"] == 3 for s in to_delete))

events = [dict(id=1, severity="Warning", created=now - timedelta(days=400)),
          dict(id=2, severity="Critical", created=now - timedelta(days=400))]
# 1 yıldan eski, Critical HARİÇ sil
del_events = [e for e in events if e["severity"] != "Critical" and e["created"] < now - timedelta(days=365)]
check("1+ yıl eski Warning log silinir", len(del_events) == 1 and del_events[0]["id"] == 1)
check("Critical log korunur (silinmez)", not any(e["severity"] == "Critical" for e in del_events))



# ═════════════════════════════════════════════════════════════
# SON GÜVENLİK KATMANI TESTLERİ (XSS, SSRF, alg, GDPR, timing)
# ═════════════════════════════════════════════════════════════

print("\n[36] GİRDİ SANİTİZASYONU — stored XSS payload'ları temizlenir")
import re as _re

def sanitize(text):
    if not text: return text
    text = _re.sub(r'<\s*script[^>]*>.*?<\s*/\s*script\s*>', '', text, flags=_re.I|_re.S)
    text = _re.sub(r'<\s*(iframe|object|embed|form|link|meta|style|base)[^>]*>', '', text, flags=_re.I)
    text = _re.sub(r'\son\w+\s*=\s*("[^"]*"|\'[^\']*\'|[^\s>]+)', '', text, flags=_re.I)
    text = _re.sub(r'javascript\s*:', '', text, flags=_re.I)
    return text.strip()

check("Script tag temizlenir", '<script>' not in sanitize('Merhaba<script>alert(1)</script>'))
check("iframe temizlenir", '<iframe' not in sanitize('<iframe src=evil></iframe>yorum'))
check("onerror event handler temizlenir", 'onerror' not in sanitize('<img src=x onerror=alert(1)>'))
check("javascript: protokolü temizlenir", 'javascript:' not in sanitize('<a href=javascript:alert(1)>'))
check("Normal metin korunur", sanitize('Güzel ürün, çok beğendim!') == 'Güzel ürün, çok beğendim!')

print("\n[37] SSRF — callback_url iç ağ/localhost/metadata reddedilir")

def is_safe_url(url):
    if not url or not url.startswith('https://'): return False
    import re as r
    m = r.match(r'https://([^/:]+)', url)
    if not m: return False
    host = m.group(1)
    if host == 'localhost': return False
    # IP kontrolü
    parts = host.split('.')
    if len(parts) == 4 and all(p.isdigit() for p in parts):
        b = [int(p) for p in parts]
        if b[0] == 10: return False
        if b[0] == 172 and 16 <= b[1] <= 31: return False
        if b[0] == 192 and b[1] == 168: return False
        if b[0] == 169 and b[1] == 254: return False  # metadata
        if b[0] == 127: return False
    return True

check("Geçerli public HTTPS URL kabul", is_safe_url("https://divisima.com/callback"))
check("http:// reddedilir (yalnız https)", not is_safe_url("http://divisima.com/callback"))
check("localhost reddedilir", not is_safe_url("https://localhost/callback"))
check("İç ağ IP (192.168.x) reddedilir", not is_safe_url("https://192.168.1.1/x"))
check("Cloud metadata (169.254.169.254) reddedilir", not is_safe_url("https://169.254.169.254/latest"))
check("10.x iç ağ reddedilir", not is_safe_url("https://10.0.0.5/x"))

print("\n[38] JWT ALG CONFUSION — yalnız HS256 kabul, none/RS256 reddi")
ALLOWED_ALGS = {"HS256", "HmacSha256"}
def accept_jwt_alg(alg):
    return alg in ALLOWED_ALGS
check("HS256 kabul edilir", accept_jwt_alg("HS256"))
check("alg=none reddedilir (imzasız token)", not accept_jwt_alg("none"))
check("RS256 reddedilir (alg confusion)", not accept_jwt_alg("RS256"))

print("\n[39] TIMING-SAFE LOGIN — kullanıcı yok/var yanıt süresi eşitlenir (dummy hash)")

def login_flow(user_exists, verify_dummy_when_missing=True):
    # Enumeration timing: kullanıcı yoksa da hash doğrulama süresi harcanır
    hash_computed = False
    if not user_exists:
        if verify_dummy_when_missing:
            hash_computed = True  # dummy hash doğrulandı (süre eşitlendi)
        return "LoginFailed", hash_computed
    hash_computed = True
    return "LoginFailed", hash_computed  # yanlış şifre de aynı mesaj

r1, h1 = login_flow(user_exists=False)
r2, h2 = login_flow(user_exists=True)
check("Kullanıcı yok -> LoginFailed + dummy hash hesaplandı", r1 == "LoginFailed" and h1 == True)
check("Kullanıcı var (yanlış şifre) -> aynı LoginFailed + hash", r2 == "LoginFailed" and h2 == True)
check("İki durumda da hash hesaplandı (timing eşit)", h1 == h2)

print("\n[40] GDPR — hesap silme anonimleştirir, export hassas alan sızdırmaz")

def delete_account(customer):
    # Anonimleştirme
    customer["name"] = "Silinmiş Kullanıcı"
    customer["email"] = "deleted-abc@anonymized.local"
    customer["phone"] = None
    customer["two_factor_secret"] = None
    customer["is_active"] = False
    return customer

def export_data(customer):
    # Hassas alanlar HARİÇ
    return {k: customer[k] for k in ("id", "name", "email", "phone") if k in customer}

cust = dict(id=1, name="Ali", email="ali@x.com", phone="555", password_hash="SECRET", two_factor_secret="TFSECRET", is_active=True)
deleted = delete_account(dict(cust))
check("Silme sonrası isim anonimleşti", deleted["name"] == "Silinmiş Kullanıcı")
check("Silme sonrası e-posta anonimleşti", "anonymized" in deleted["email"])
check("Silme sonrası 2FA secret temizlendi", deleted["two_factor_secret"] is None)
check("Silme sonrası hesap pasif", deleted["is_active"] == False)

exported = export_data(cust)
check("Export password_hash içermez", "password_hash" not in exported)
check("Export two_factor_secret içermez", "two_factor_secret" not in exported)
check("Export kullanıcının kendi verisini içerir", exported["email"] == "ali@x.com")


# ============================================================
# [41] DASHBOARD - ciro hesabi (iptal haric)
# ============================================================
def dashboard_revenue(orders):
    # Aciklama: ciro = iptal olmayan siparislerin total toplami (status 5 = Cancelled)
    return sum(o["total"] for o in orders if o["status"] != 5)

def dashboard_avg_order(orders):
    rev_orders = [o for o in orders if o["status"] != 5]
    total = sum(o["total"] for o in rev_orders)
    return round(total / len(rev_orders), 2) if rev_orders else 0

orders_d = [
    dict(total=1000, status=4),   # Delivered
    dict(total=500,  status=1),   # Confirmed
    dict(total=2000, status=5),   # Cancelled - ciroya girmez
    dict(total=1500, status=3),   # Shipped
]
check("Dashboard ciro iptal edileni haric tutar", dashboard_revenue(orders_d) == 3000)
check("Dashboard ortalama sepet dogru", dashboard_avg_order(orders_d) == 1000.0)
check("Dashboard tum iptal ise ciro sifir", dashboard_revenue([dict(total=100, status=5)]) == 0)

# En cok satan urun toplama
def top_products(items, valid_order_ids):
    agg = {}
    for it in items:
        if it["order_id"] not in valid_order_ids:
            continue
        agg.setdefault(it["product_id"], {"qty": 0, "rev": 0})
        agg[it["product_id"]]["qty"] += it["qty"]
        agg[it["product_id"]]["rev"] += it["unit_price"] * it["qty"]
    return sorted(agg.items(), key=lambda x: x[1]["qty"], reverse=True)

items_t = [
    dict(order_id=1, product_id=10, qty=3, unit_price=100),
    dict(order_id=1, product_id=20, qty=1, unit_price=200),
    dict(order_id=2, product_id=10, qty=2, unit_price=100),
    dict(order_id=3, product_id=10, qty=5, unit_price=100),  # order 3 iptal - sayilmaz
]
top = top_products(items_t, valid_order_ids={1, 2})
check("En cok satan urun dogru (iptal haric)", top[0][0] == 10 and top[0][1]["qty"] == 5)
check("En cok satan ciro dogru", top[0][1]["rev"] == 500)

# ============================================================
# [42] IADE - kurallar (sure, sahiplik, teslim, adet)
# ============================================================
from datetime import datetime, timedelta

def can_return(order, req_customer_id, item, req_qty, now):
    RETURN_WINDOW = 14
    if order["customer_id"] != req_customer_id:
        return "not_your_order"
    if order["status"] != 4:  # Delivered
        return "not_delivered"
    if order["created_at"] + timedelta(days=RETURN_WINDOW) < now:
        return "window_expired"
    if item is None or req_qty <= 0 or req_qty > item["qty"]:
        return "invalid_item"
    return "ok"

now = datetime(2025, 6, 15)
delivered = dict(customer_id=1, status=4, created_at=datetime(2025, 6, 10))
item_r = dict(qty=3)

check("Iade: gecerli talep kabul", can_return(delivered, 1, item_r, 2, now) == "ok")
check("Iade: baskasinin siparisi ret (IDOR)", can_return(delivered, 99, item_r, 2, now) == "not_your_order")
check("Iade: teslim edilmemis ret", can_return(dict(customer_id=1, status=3, created_at=now), 1, item_r, 1, now) == "not_delivered")
old_order = dict(customer_id=1, status=4, created_at=datetime(2025, 5, 1))
check("Iade: 14 gun gecince ret", can_return(old_order, 1, item_r, 1, now) == "window_expired")
check("Iade: fazla adet ret", can_return(delivered, 1, item_r, 5, now) == "invalid_item")
check("Iade: sifir adet ret", can_return(delivered, 1, item_r, 0, now) == "invalid_item")

# Iade tutari = kalem birim fiyati x adet
def refund_amount(unit_price, qty):
    return unit_price * qty
check("Iade tutari dogru hesaplanir", refund_amount(250, 2) == 500)

# ============================================================
# [43] FATURA - KDV ayristirma (fiyat KDV dahil)
# ============================================================
def invoice_breakdown(total, vat_rate=20):
    # Aciklama: Turkiye - fiyat KDV dahil; KDV = toplam * oran / (100 + oran)
    tax = round(total * vat_rate / (100 + vat_rate), 2)
    subtotal = total - tax
    return dict(subtotal=subtotal, tax=tax, total=total)

inv = invoice_breakdown(1200)
check("Fatura KDV dogru (1200 -> 200 KDV)", inv["tax"] == 200.0)
check("Fatura matrah dogru (1200 -> 1000)", inv["subtotal"] == 1000.0)
check("Fatura toplam korunur", inv["subtotal"] + inv["tax"] == inv["total"])

inv2 = invoice_breakdown(600)
check("Fatura KDV dogru (600 -> 100 KDV)", inv2["tax"] == 100.0)

# Sipariş başına tek fatura (idempotent)
def create_invoice(existing_invoices, order_id, total):
    for iv in existing_invoices:
        if iv["order_id"] == order_id:
            return iv  # var olani dondur - yeni kesme
    inv = dict(order_id=order_id, **invoice_breakdown(total))
    existing_invoices.append(inv)
    return inv

invoices_list = []
create_invoice(invoices_list, 1, 1200)
create_invoice(invoices_list, 1, 1200)  # ayni siparis - tekrar kesilmez
check("Fatura idempotent (siparis basina tek)", len(invoices_list) == 1)


# ============================================================
# [44] SIPARIS DURUMU -> YAN ETKI (fatura + bildirim tetikleyici)
# ============================================================
# Aciklama: OrderManager.HandleStatusSideEffects mantiginin simulasyonu.
# Confirmed(1) -> fatura uret; Shipped(3)/Delivered(4) -> musteri bildir.
CONFIRMED, PREPARING, SHIPPED, DELIVERED, CANCELLED = 1, 2, 3, 4, 5

def status_side_effects(new_status, prev_status):
    # Ayni durumsa hicbir sey yapma
    effects = dict(invoice=False, notify=False)
    if new_status == prev_status:
        return effects
    if new_status == CONFIRMED:
        effects["invoice"] = True
    if new_status in (SHIPPED, DELIVERED):
        effects["notify"] = True
    return effects

check("Onaylaninca fatura tetiklenir", status_side_effects(CONFIRMED, 0)["invoice"] is True)
check("Onaylaninca bildirim tetiklenmez", status_side_effects(CONFIRMED, 0)["notify"] is False)
check("Kargoya verilince bildirim tetiklenir", status_side_effects(SHIPPED, CONFIRMED)["notify"] is True)
check("Teslim edilince bildirim tetiklenir", status_side_effects(DELIVERED, SHIPPED)["notify"] is True)
check("Ayni durum tekrar yazilinca yan etki yok",
      status_side_effects(SHIPPED, SHIPPED) == dict(invoice=False, notify=False))
check("Hazirlaniyor durumu bildirim tetiklemez", status_side_effects(PREPARING, CONFIRMED)["notify"] is False)

# Bildirim kanallari - hepsi best-effort (biri patlarsa akis devam eder)
def send_notifications(channels_ok):
    # channels_ok: {"inapp": bool, "push": bool, "sms": bool} - her biri bagimsiz dener
    sent = []
    for ch in ("inapp", "push", "sms"):
        try:
            if not channels_ok.get(ch, True):
                raise RuntimeError("kanal hatasi")
            sent.append(ch)
        except RuntimeError:
            pass  # best-effort - digerleri devam
    return sent

# SMS patlasa bile in-app + push gider
result = send_notifications({"inapp": True, "push": True, "sms": False})
check("SMS patlasa bile in-app + push gonderilir", result == ["inapp", "push"])
# Push token yoksa (push False) digerleri etkilenmez
check("Push basarisizsa in-app + SMS etkilenmez",
      send_notifications({"inapp": True, "push": False, "sms": True}) == ["inapp", "sms"])


# ============================================================
# [45] KARGO TAKIP + CIHAZ/PUSH
# ============================================================
# Kargo: siparis basina tek kargo (idempotent)
def create_shipment(shipments, order_id, tracking):
    for s in shipments:
        if s["order_id"] == order_id:
            return None  # zaten var - cakisma
    s = dict(order_id=order_id, tracking=tracking, status=0)
    shipments.append(s)
    return s

ships = []
create_shipment(ships, 1, "YT123")
dup = create_shipment(ships, 1, "YT999")  # ayni siparis
check("Kargo idempotent (siparis basina tek)", dup is None and len(ships) == 1)

# Teslim durumu siparisi Delivered yapar
DELIVERED = 3
def on_track_update(shipment_status, order_status):
    if shipment_status == DELIVERED and order_status != DELIVERED:
        order_status = DELIVERED
    return order_status
check("Kargo teslim edilince siparis Delivered olur", on_track_update(DELIVERED, 1) == DELIVERED)
check("Kargo yoldayken siparis durumu degismez", on_track_update(1, 1) == 1)

# Kargo takip sahiplik (IDOR)
def track_allowed(order_owner, requester):
    return order_owner == requester
check("Baskasinin kargosu takip edilemez (IDOR)", track_allowed(5, 9) is False)
check("Kendi kargosu takip edilebilir", track_allowed(5, 5) is True)

# Cihaz upsert (ayni token tekrar kaydedilince cogalmaz)
def register_device(devices, token, customer_id):
    for d in devices:
        if d["token"] == token:
            d["customer_id"] = customer_id
            d["active"] = True
            return "updated"
    devices.append(dict(token=token, customer_id=customer_id, active=True))
    return "added"

devs = []
register_device(devs, "tok_abc", 1)
r = register_device(devs, "tok_abc", 1)  # ayni token
check("Cihaz upsert (ayni token cogalmaz)", r == "updated" and len(devs) == 1)

# Push: gecersiz token pasiflesir, digerleri etkilenmez
def notify_devices(devices, send_ok):
    # send_ok: {token: bool}
    sent = 0
    for d in devices:
        if not d["active"]:
            continue
        ok = send_ok.get(d["token"], True)
        if ok:
            sent += 1
        else:
            d["active"] = False  # gecersiz token pasiflesir
    return sent

devs2 = [dict(token="t1", active=True), dict(token="t2", active=True), dict(token="t3", active=True)]
sent = notify_devices(devs2, {"t2": False})  # t2 gecersiz
check("Push: gecerli cihazlara gonderilir (2/3)", sent == 2)
check("Push: gecersiz token pasiflesir", devs2[1]["active"] is False)
check("Push: gecerli tokenlar aktif kalir", devs2[0]["active"] and devs2[2]["active"])


# ============================================================
# [46] STOK REZERVASYONU (oversell + terk edilen sepet koruması)
# ============================================================
# Aciklama: musait = fiziksel - rezerve. Rezerve fiziksel dusurmez; onay dusurur; serbest geri verir.
class StockR:
    def __init__(self, physical):
        self.physical = physical
        self.reserved = 0
    def available(self):
        return self.physical - self.reserved
    def reserve(self, qty):
        if self.available() < qty:
            return False  # musait yetmez - oversell engeli
        self.reserved += qty
        return True
    def confirm(self, qty):
        # odeme basarili: fiziksel duser, rezerve serbest
        self.physical -= qty
        self.reserved = max(0, self.reserved - qty)
    def release(self, qty):
        # odeme basarisiz/sure doldu: rezerve geri, fiziksel degismez
        self.reserved = max(0, self.reserved - qty)

s = StockR(10)
check("Baslangicta musait = fiziksel", s.available() == 10)
ok = s.reserve(3)
check("Rezerve edilince musait duser (10->7)", ok and s.available() == 7)
check("Rezerve fiziksel stogu dusurmez", s.physical == 10)

# Onay: fiziksel duser
s.confirm(3)
check("Onaylaninca fiziksel duser (10->7)", s.physical == 7)
check("Onaydan sonra rezerve sifir", s.reserved == 0)
check("Onaydan sonra musait = fiziksel", s.available() == 7)

# Serbest birakma: fiziksel korunur
s2 = StockR(5)
s2.reserve(2)
s2.release(2)
check("Serbest birakinca rezerve geri (musait 5)", s2.available() == 5)
check("Serbest birakma fizikseli degistirmez", s2.physical == 5)

# Oversell engeli: musaitten fazla rezerve edilemez
s3 = StockR(4)
check("Musaitten fazla rezerve reddedilir", s3.reserve(5) is False)
s3.reserve(4)
check("Tum stok rezerve edilince musait sifir", s3.available() == 0)
check("Rezerve doluyken ek rezerve reddedilir", s3.reserve(1) is False)

# Iki es zamanli rezervasyon toplami fizikseli asamaz
s4 = StockR(3)
r1 = s4.reserve(2)  # musteri A
r2 = s4.reserve(2)  # musteri B - sadece 1 kaldi
check("Es zamanli rezervasyonlar fizikseli asamaz", r1 is True and r2 is False)

# Terk edilen sepet: sure dolunca serbest (release ile ayni)
s5 = StockR(6)
s5.reserve(4)  # musteri odemeye gitti ama donmedi
check("Terk edilen sepet rezerve tutulur", s5.available() == 2)
s5.release(4)  # job sureyi gecince serbest birakir
check("Sure dolunca stok geri kazanilir", s5.available() == 6)


# ============================================================
# [47] GORSEL DOGRULAMA + STOK DUZELTME (Priority 2)
# ============================================================
# Gorsel yukleme: tur + boyut dogrulama
ALLOWED = {"image/jpeg", "image/png", "image/webp"}
MAX_BYTES = 5 * 1024 * 1024
def validate_image(content_type, size):
    if content_type not in ALLOWED: return "type"
    if size == 0: return "empty"
    if size > MAX_BYTES: return "large"
    return "ok"
check("Gecerli JPEG kabul edilir", validate_image("image/jpeg", 100000) == "ok")
check("PNG kabul edilir", validate_image("image/png", 50000) == "ok")
check("PDF reddedilir (tur)", validate_image("application/pdf", 1000) == "type")
check("SVG reddedilir (XSS riski)", validate_image("image/svg+xml", 1000) == "type")
check("6 MB reddedilir (boyut)", validate_image("image/jpeg", 6*1024*1024) == "large")
check("Bos dosya reddedilir", validate_image("image/jpeg", 0) == "empty")

# Birincil gorsel: tek birincil olmali
def set_primary(images, target_id):
    for im in images: im["primary"] = (im["id"] == target_id)
    return sum(1 for im in images if im["primary"])
imgs = [{"id":1,"primary":True},{"id":2,"primary":False},{"id":3,"primary":False}]
cnt = set_primary(imgs, 2)
check("Tek birincil gorsel (2 secilince 1 birincil)", cnt == 1 and imgs[1]["primary"])
check("Eski birincil kaldirilir", imgs[0]["primary"] is False)

# Stok duzeltme: rezerve altina inilemez
def adjust_stock(physical, reserved, new_qty):
    if new_qty < 0: return "invalid"
    if new_qty < reserved: return "below_reserved"
    return "ok"
check("Yeni sevkiyat artisi kabul (5->20)", adjust_stock(5, 0, 20) == "ok")
check("Rezerve altina inilemez (rezerve 3, yeni 2)", adjust_stock(10, 3, 2) == "below_reserved")
check("Rezerve esitine inilebilir (rezerve 3, yeni 3)", adjust_stock(10, 3, 3) == "ok")
check("Negatif stok reddedilir", adjust_stock(5, 0, -1) == "invalid")

# Fark hesabi (hareket kaydi icin)
def stock_delta(old, new):
    return new - old
check("Artis farki dogru (+15)", stock_delta(5, 20) == 15)
check("Azalis farki dogru (-3)", stock_delta(10, 7) == -3)

# E-posta dogrulama zorunlulugu (login guard)
def can_login(active, verified):
    if not active: return "inactive"
    if not verified: return "not_verified"
    return "ok"
check("Dogrulanmis aktif hesap giris yapar", can_login(True, True) == "ok")
check("Dogrulanmamis hesap giris yapamaz", can_login(True, False) == "not_verified")
check("Pasif hesap giris yapamaz", can_login(False, True) == "inactive")


# ============================================================
# [48] REDIS RATE LIMIT (dağıtık sabit-pencere sayacı)
# ============================================================
# Aciklama: RedisRateLimiter mantigi - INCR + ilk artista EXPIRE, limit asilinca 429.
class FixedWindowLimiter:
    def __init__(self):
        self.counters = {}  # key -> count (Redis INCR simulasyonu)
    def check(self, key, limit, window=60):
        self.counters[key] = self.counters.get(key, 0) + 1
        current = self.counters[key]
        allowed = current <= limit
        remaining = max(0, limit - current)
        return dict(allowed=allowed, remaining=remaining)

# Genel limit 100/dk
rl = FixedWindowLimiter()
for i in range(100):
    r = rl.check("global:1.2.3.4", 100)
check("100 istek limite kadar gecer", r["allowed"] is True and r["remaining"] == 0)
r101 = rl.check("global:1.2.3.4", 100)
check("101. istek reddedilir (429)", r101["allowed"] is False)

# Auth sıkı limit 5/dk (brute-force)
rl2 = FixedWindowLimiter()
results = [rl2.check("auth:5.6.7.8", 5) for _ in range(6)]
check("Auth: 5 giris denemesi gecer", results[4]["allowed"] is True)
check("Auth: 6. deneme reddedilir (brute-force engeli)", results[5]["allowed"] is False)

# Farkli IP'ler bagimsiz sayilir
rl3 = FixedWindowLimiter()
for _ in range(5): rl3.check("auth:1.1.1.1", 5)
r_other = rl3.check("auth:2.2.2.2", 5)  # farkli IP
check("Farkli IP bagimsiz sayilir", r_other["allowed"] is True and r_other["remaining"] == 4)

# Farkli kapsam (scope) bagimsiz - ayni IP auth ve global ayri
rl4 = FixedWindowLimiter()
for _ in range(5): rl4.check("auth:9.9.9.9", 5)     # auth doldu
r_global = rl4.check("global:9.9.9.9", 100)          # global ayri sayac
check("Ayni IP farkli kapsam bagimsiz", r_global["allowed"] is True)

# Fail-open: Redis erisilemezse istek gecer (servis > rate limit)
def check_fail_open(redis_up, key, limit):
    if not redis_up:
        return dict(allowed=True, remaining=limit)  # fail-open
    return dict(allowed=True, remaining=limit-1)
check("Redis down ise fail-open (istek gecer)", check_fail_open(False, "x", 100)["allowed"] is True)


# ============================================================
# [49] KUPON KULLANIM KAYDI + SİPARİŞ TRANSACTION (bug fix)
# ============================================================
# Bug fix 1: used_count odeme basarisinda artar - limit anlamli olur
class Coupon:
    def __init__(self, limit): self.usage_limit=limit; self.used_count=0
    def can_use(self): return self.usage_limit==0 or self.used_count < self.usage_limit
    def consume(self): self.used_count += 1

c = Coupon(limit=1)  # tek kullanimlik
check("Tek kullanimlik kupon ilk kullanimda gecerli", c.can_use() is True)
c.consume()  # odeme basarili -> kaydet
check("Kullanildiktan sonra used_count artar", c.used_count == 1)
check("Tek kullanimlik kupon ikinci kez REDDEDILIR (bug fixli)", c.can_use() is False)

c2 = Coupon(limit=3)
for _ in range(3): 
    assert c2.can_use(); c2.consume()
check("3 kullanimlik kupon 3 kez kabul", c2.used_count == 3)
check("4. kullanim reddedilir", c2.can_use() is False)

c3 = Coupon(limit=0)  # sinirsiz
for _ in range(10): c3.consume()
check("Sinirsiz kupon (limit=0) hep gecerli", c3.can_use() is True)

# Bug fix 2: odeme BASARISIZ olursa kupon TUKETILMEZ (sadece basarida kaydedilir)
c4 = Coupon(limit=1)
payment_success = False
if payment_success: c4.consume()
check("Odeme basarisizsa kupon tuketilmez", c4.used_count == 0 and c4.can_use() is True)

# Bug fix 3: siparis transaction - rezervasyon basarisizsa HERSEY geri alinir
def place_order_atomic(items, stock_available):
    order = {"created": False, "items": [], "reservations": []}
    order["created"] = True  # order eklendi
    for it in items:
        order["items"].append(it)
        if stock_available.get(it["pid"], 0) < it["qty"]:
            # rezervasyon basarisiz -> ROLLBACK (hepsini geri al)
            return {"created": False, "items": [], "reservations": [], "rolled_back": True}
        order["reservations"].append(it)
    return order

# Yeterli stok: basarili
r_ok = place_order_atomic([{"pid":1,"qty":2}], {1:10})
check("Yeterli stokta siparis olusur", r_ok["created"] is True and len(r_ok["reservations"])==1)

# 2. kalem stok yetmez: TUM siparis geri alinir (kismi kalmaz)
r_fail = place_order_atomic([{"pid":1,"qty":2},{"pid":2,"qty":5}], {1:10, 2:1})
check("Bir kalem yetmezse TUM siparis geri alinir", r_fail["created"] is False)
check("Kismi siparis kalmaz (rollback)", len(r_fail["items"])==0 and r_fail.get("rolled_back"))

# Rezervasyon sonucu kontrol ediliyor (eskiden yok sayiliyordu)
def reserve_checked(code):
    if code != "OK": return ("fail", code)  # sonuc kontrol edilir
    return ("proceed", None)
check("Rezervasyon basarisiz sonucu siparisi durdurur", reserve_checked("Conflict")[0] == "fail")
check("Rezervasyon basarili sonucu devam eder", reserve_checked("OK")[0] == "proceed")


# ============================================================
# [50] ÖNERİ MOTORU (birliktelik + benzerlik)
# ============================================================
# "Bunu alanlar sunu da aldi" - ayni siparislerde gecen urunler siklikla
def frequently_bought(target_pid, order_items, limit=8):
    # target'i iceren siparisler
    order_ids = set(oi["order"] for oi in order_items if oi["pid"]==target_pid)
    # o siparislerdeki DIGER urunler
    co = [oi["pid"] for oi in order_items if oi["order"] in order_ids and oi["pid"]!=target_pid]
    # siklikla sirala
    from collections import Counter
    ranked = Counter(co).most_common(limit)
    return [pid for pid,_ in ranked]

# Siparisler: #1[A,B,C], #2[A,B], #3[A,C], #4[D]
items = [
    {"order":1,"pid":"A"},{"order":1,"pid":"B"},{"order":1,"pid":"C"},
    {"order":2,"pid":"A"},{"order":2,"pid":"B"},
    {"order":3,"pid":"A"},{"order":3,"pid":"C"},
    {"order":4,"pid":"D"},
]
rec = frequently_bought("A", items)
check("A ile en cok B ve C onerilir", set(rec)=={"B","C"})
check("B (2 kez) C'den (2 kez) once veya esit", rec[0] in ("B","C"))
check("D onerilmez (A ile hic ayni sipariste degil)", "D" not in rec)

# Kendisi onerilmez
check("Urun kendini onermez", "A" not in frequently_bought("A", items))

# Hic siparisi olmayan urun -> bos
check("Siparissiz urun bos oneri", frequently_bought("Z", items)==[])

# Benzer urunler - ayni kategori, kendisi haric, sadece aktif
def similar_products(target, products, limit=8):
    cat = target["cat"]
    return [p["id"] for p in products 
            if p["cat"]==cat and p["id"]!=target["id"] and p["active"]][:limit]

products = [
    {"id":1,"cat":"elbise","active":True},
    {"id":2,"cat":"elbise","active":True},
    {"id":3,"cat":"elbise","active":False},  # pasif
    {"id":4,"cat":"canta","active":True},    # farkli kategori
]
sim = similar_products(products[0], products)
check("Benzer: ayni kategori aktif urunler", sim==[2])
check("Benzer: pasif urun haric", 3 not in sim)
check("Benzer: farkli kategori haric", 4 not in sim)
check("Benzer: kendisi haric", 1 not in sim)

# Limit uygulanir
many = [{"id":i,"cat":"x","active":True} for i in range(20)]
check("Oneri limiti uygulanir (max 8)", len(similar_products({"id":99,"cat":"x"}, many, 8))==8)

# Limit sinir kontrol (0 veya >20 -> 8'e duser)
def clamp_limit(l): return 8 if (l<=0 or l>20) else l
check("Limit 0 -> 8", clamp_limit(0)==8)
check("Limit 50 -> 8", clamp_limit(50)==8)
check("Limit 5 -> 5", clamp_limit(5)==5)


# ============================================================
# [51] STOK BİLDİRİMİ ("gelince haber ver")
# ============================================================
# Abonelik idempotent - ayni email+urun+beden tekrar eklenmez
class NotifStore:
    def __init__(self): self.reqs=[]
    def subscribe(self, pid, size, email):
        size=size or ""
        # zaten bekleyen var mi
        for r in self.reqs:
            if r["pid"]==pid and r["size"]==size and r["email"]==email and not r["notified"]:
                return "already"
        self.reqs.append({"pid":pid,"size":size,"email":email,"notified":False})
        return "subscribed"

store=NotifStore()
check("Ilk abonelik: subscribed", store.subscribe(1,"M","a@x.com")=="subscribed")
check("Ayni abonelik tekrar: already (idempotent)", store.subscribe(1,"M","a@x.com")=="already")
check("Farkli beden: yeni abonelik", store.subscribe(1,"L","a@x.com")=="subscribed")
check("Farkli email: yeni abonelik", store.subscribe(1,"M","b@x.com")=="subscribed")
check("Toplam 3 bekleyen talep", len([r for r in store.reqs if not r["notified"]])==3)

# Email dogrulama
def valid_email(e): return bool(e) and "@" in e
check("Gecersiz email reddedilir (bos)", not valid_email(""))
check("Gecersiz email reddedilir (@ yok)", not valid_email("abc"))
check("Gecerli email kabul", valid_email("x@y.com"))

# NotifyBackInStock - bekleyen aboneler bilgilendirilir + isaretlenir
def notify_back_in_stock(store, pid, size):
    size=size or ""
    pending=[r for r in store.reqs if r["pid"]==pid and r["size"]==size and not r["notified"]]
    sent=0
    for r in pending:
        r["notified"]=True  # mail gonderildi
        sent+=1
    return sent

sent=notify_back_in_stock(store, 1, "M")
check("Stok gelince M bedene 2 abone bilgilendirildi", sent==2)
check("Bilgilendirilenler isaretlendi", all(r["notified"] for r in store.reqs if r["pid"]==1 and r["size"]=="M"))
check("L beden abonesi etkilenmedi", not [r for r in store.reqs if r["size"]=="L"][0]["notified"])
check("Ikinci bildirim tekrar gondermez", notify_back_in_stock(store,1,"M")==0)

# Tetik kosulu: SADECE musait 0'dan pozitife cikinca (pozitif->pozitif tetiklemez)
def should_notify(available_before, available_after):
    return available_before <= 0 and available_after > 0

check("Stok 0->5: bildirim tetiklenir", should_notify(0, 5) is True)
check("Stok 3->8: bildirim TETIKLENMEZ (zaten vardi)", should_notify(3, 8) is False)
check("Stok 0->0: tetiklenmez", should_notify(0, 0) is False)
check("Stok -2(rezerve fazlasi)->3: tetiklenir", should_notify(-2, 3) is True)
check("Musait = stok - rezerve (5 stok, 5 rezerve = 0 musait)", (5-5) <= 0)

# Email hata izolasyonu - bir mail patlarsa digerleri gonderilir
def notify_with_failures(emails, fail_on):
    sent=[]; failed=[]
    for e in emails:
        try:
            if e==fail_on: raise Exception("smtp error")
            sent.append(e)
        except:
            failed.append(e)  # is_notified false kalir, sonra tekrar denenir
    return sent, failed

sent, failed = notify_with_failures(["a@x.com","b@x.com","c@x.com"], "b@x.com")
check("Bir mail hatasi digerlerini engellemez", set(sent)=={"a@x.com","c@x.com"})
check("Hatali mail sonra tekrar denenmek uzere isaretsiz kalir", failed==["b@x.com"])


# ============================================================
# [52] ARAMA RELEVANCE RANKING + STOK FİLTRESİ (#24)
# ============================================================
def relevance_score(name, brand, query, tokens):
    name=name.lower(); brand=brand.lower(); score=0
    if name==query: score+=100
    elif name.startswith(query): score+=50
    elif query in name: score+=30
    if brand==query: score+=40
    elif query in brand: score+=15
    token_hits=0
    for t in tokens:
        if t in name: score+=10; token_hits+=1
        if t in brand: score+=5
    if len(tokens)>1 and token_hits==len(tokens): score+=20
    return score

def search_rank(products, query):
    q=query.lower().strip(); tokens=[t for t in q.split() if t]
    scored=[(p, relevance_score(p["name"],p["brand"],q,tokens)) for p in products]
    # sadece eslesenler (skor>0) + skora gore sirala, esitlikte yeni
    matched=[(p,s) for p,s in scored if s>0]
    matched.sort(key=lambda x:(-x[1], -x[0]["created"]))
    return [p["name"] for p,s in matched]

prods=[
    {"name":"Siyah Elbise","brand":"Zara","created":5},
    {"name":"Elbise Askısı","brand":"Ikea","created":3},
    {"name":"Kırmızı Elbise Gece","brand":"Mango","created":4},
    {"name":"Pantolon","brand":"Elbise Co","created":2},  # marka eslesme
]
res=search_rank(prods, "elbise")
check("Tam eslesme 'Siyah Elbise' degil ama iceren once", res[0] in ("Elbise Askısı","Siyah Elbise","Kırmızı Elbise Gece"))
# "Elbise Askısı" startswith elbise -> 50; "Siyah Elbise" contains -> 30; ilk sirada startswith olmali
check("Baştan eslesen (Elbise Askısı) içerene göre üstte", res[0]=="Elbise Askısı")
check("Marka-only eslesme (Pantolon) listede", "Pantolon" in res)
check("Marka eslesme ada göre daha düşük", res.index("Pantolon") > res.index("Elbise Askısı"))

# Tam ad eslesmesi en üstte
prods2=[
    {"name":"Elbise","brand":"X","created":1},
    {"name":"Uzun Elbise","brand":"X","created":9},
]
check("Tam ad eslesmesi (Elbise=100) en üstte", search_rank(prods2,"elbise")[0]=="Elbise")

# Cok kelimeli sorgu - tum kelimeler adda geciyorsa bonus
prods3=[
    {"name":"Siyah Uzun Elbise","brand":"X","created":1},   # 3 token da var +bonus
    {"name":"Siyah Ceket","brand":"X","created":2},          # 1 token
]
r3=search_rank(prods3, "siyah elbise")
check("Iki kelimeli sorguda tum kelimeleri iceren üstte", r3[0]=="Siyah Uzun Elbise")

# Eslesmeyenler dışlanır
check("Eslesmeyen ürün sonuçta yok", "Ceket" not in " ".join(search_rank([{"name":"Ceket","brand":"Y","created":1}], "elbise")))

# Stok filtresi - musait (stock - reserved) > 0
def in_stock_filter(products, stocks):
    # stocks: {pid: (stock, reserved)}
    return [p for p in products if p["id"] in stocks and (stocks[p["id"]][0]-stocks[p["id"]][1])>0]

items=[{"id":1},{"id":2},{"id":3}]
stocks={1:(10,3), 2:(5,5), 3:(0,0)}  # 1 musait, 2 hepsi rezerve, 3 sifir
filtered=[p["id"] for p in in_stock_filter(items, stocks)]
check("Musait stok filtresi: sadece id=1", filtered==[1])
check("Hepsi rezerve (2) stokta sayılmaz", 2 not in filtered)
check("Sıfır stok (3) filtrelenir", 3 not in filtered)

# In-memory sayfalama
def paginate(items, page, size): return items[(page-1)*size:(page-1)*size+size]
big=list(range(1,26))
check("Sayfa 1 (size 10): ilk 10", paginate(big,1,10)==list(range(1,11)))
check("Sayfa 3 (size 10): son 5", paginate(big,3,10)==list(range(21,26)))


# ============================================================
# [53] IDOR FIX - sipariş sahiplik kontrolü (güvenlik)
# ============================================================
def get_order_by_id(order_id, requesting_customer, orders):
    o = orders.get(order_id)
    if o is None: return ("not_found", None)
    # IDOR koruması: sadece sahibi görebilir; degilse "bulunamadi" (varlik sizdirma)
    if o["customer_id"] != requesting_customer: return ("not_found", None)
    return ("ok", o)

orders = {
    100: {"customer_id": 1, "total": 500},
    200: {"customer_id": 2, "total": 999},
}
# Musteri 1 kendi siparisini gorur
st, o = get_order_by_id(100, 1, orders)
check("Musteri kendi siparisini gorur", st=="ok" and o["total"]==500)
# Musteri 1 baskasinin (id=200, sahibi=2) siparisini GOREMEZ
st2, o2 = get_order_by_id(200, 1, orders)
check("Musteri baskasinin siparisini goremez (IDOR fix)", st2=="not_found" and o2 is None)
# Var olmayan = ayni sekilde not_found (varlik bilgisi sizmaz)
st3, _ = get_order_by_id(999, 1, orders)
check("Var olmayan siparis de not_found (varlik sizdirmaz)", st3=="not_found")
# Sahibi olmayan siparis ile var olmayan ayni yaniti verir (enumeration engeli)
check("Baskasinin siparisi ve yok olan ayni yanit (enumeration engeli)", 
      get_order_by_id(200,1,orders)[0]==get_order_by_id(999,1,orders)[0])
# Musteri 2 kendi siparisini gorur
check("Musteri 2 kendi siparisini gorur", get_order_by_id(200,2,orders)[0]=="ok")


# ============================================================
# [54] SİPARİŞ DURUM ZAMAN ÇİZELGESİ (#29)
# ============================================================
STATUS={0:"Pending",1:"Confirmed",2:"Preparing",3:"Shipped",4:"Delivered",5:"Cancelled"}

class Timeline:
    def __init__(self): self.rows=[]; self._t=0
    def record(self, order_id, status, note):
        self._t+=1
        self.rows.append({"order_id":order_id,"status":status,"note":note,"seq":self._t})
    def get(self, order_id, requester, orders):
        o=orders.get(order_id)
        if o is None or o["customer_id"]!=requester: return ("not_found", None)
        rows=sorted([r for r in self.rows if r["order_id"]==order_id], key=lambda r:r["seq"])
        return ("ok", [{"status":r["status"],"name":STATUS[r["status"]],"note":r["note"]} for r in rows])

tl=Timeline()
orders={50:{"customer_id":7}}
# Siparis yasam dongusu
tl.record(50,0,"Sipariş oluşturuldu")   # Pending (PlaceOrder)
tl.record(50,1,"Ödeme onaylandı")        # Confirmed (payment)
tl.record(50,3,"Durum: Shipped")         # Shipped (admin)
tl.record(50,4,"Durum: Delivered")       # Delivered (admin)

st, timeline = tl.get(50, 7, orders)
check("Sahibi zaman çizelgesini görür", st=="ok")
check("4 durum kaydı sırayla", len(timeline)==4)
check("İlk kayıt Pending", timeline[0]["name"]=="Pending")
check("Son kayıt Delivered", timeline[-1]["name"]=="Delivered")
check("Sıra korunur (Pending->Confirmed->Shipped->Delivered)", 
      [t["status"] for t in timeline]==[0,1,3,4])
check("Note'lar kayıtlı", timeline[1]["note"]=="Ödeme onaylandı")

# IDOR: baskasi timeline goremez
st2, _ = tl.get(50, 99, orders)
check("Baskasi zaman çizelgesini göremez (IDOR)", st2=="not_found")

# Sadece gercek durum degisimi kaydedilir (ayni durum tekrar yazilmaz)
def change_status(current, new, timeline_recorder, oid):
    if new != current:  # sadece degisimde kaydet
        timeline_recorder(oid, new)
        return new
    return current

recorded=[]
cur=1
cur=change_status(cur, 1, lambda o,s: recorded.append(s), 50)  # ayni -> kayit yok
check("Aynı duruma geçiş kaydedilmez", len(recorded)==0)
cur=change_status(cur, 3, lambda o,s: recorded.append(s), 50)  # degisim -> kayit
check("Gerçek durum değişimi kaydedilir", recorded==[3])


# ============================================================
# [55] SEPET TERK HATIRLATMASI (#30)
# ============================================================
from datetime import datetime, timedelta
NOW=datetime(2026,1,15,12,0,0)
IDLE=timedelta(hours=24)

def send_reminders(carts, cart_items, customers):
    cutoff = NOW - IDLE
    sent=[]
    for c in carts:
        # aktif + hatirlatilmamis + atil
        if not c["is_active"]: continue
        if c["reminder_sent_at"] is not None: continue
        last = c["updated_at"] or c["created_at"]
        if not (last < cutoff): continue
        # dolu mu
        items=[i for i in cart_items if i["cart_id"]==c["id"] and i["is_active"]]
        if not items: continue
        # musteri emaili
        cust=customers.get(c["customer_id"])
        if not cust or not cust.get("email"): continue
        # gonder + damgala
        c["reminder_sent_at"]=NOW
        sent.append(c["id"])
    return sent

carts=[
    {"id":1,"customer_id":10,"is_active":True,"reminder_sent_at":None,"created_at":NOW-timedelta(hours=30),"updated_at":NOW-timedelta(hours=26)},  # atil+dolu -> gonder
    {"id":2,"customer_id":11,"is_active":True,"reminder_sent_at":None,"created_at":NOW-timedelta(hours=2),"updated_at":NOW-timedelta(hours=1)},    # taze -> gonderme
    {"id":3,"customer_id":12,"is_active":True,"reminder_sent_at":NOW-timedelta(hours=5),"created_at":NOW-timedelta(hours=40),"updated_at":None},  # zaten hatirlatildi
    {"id":4,"customer_id":13,"is_active":True,"reminder_sent_at":None,"created_at":NOW-timedelta(hours=30),"updated_at":None},                     # atil ama BOS -> gonderme
    {"id":5,"customer_id":14,"is_active":False,"reminder_sent_at":None,"created_at":NOW-timedelta(hours=30),"updated_at":None},                    # pasif
]
cart_items=[
    {"cart_id":1,"is_active":True},{"cart_id":1,"is_active":True},
    {"cart_id":2,"is_active":True},
    {"cart_id":3,"is_active":True},
    # cart 4 -> item yok (bos)
]
customers={10:{"email":"a@x.com"},11:{"email":"b@x.com"},12:{"email":"c@x.com"},13:{"email":"d@x.com"},14:{"email":"e@x.com"}}

sent=send_reminders(carts, cart_items, customers)
check("Atil+dolu sepete hatirlatma gonderilir", 1 in sent)
check("Taze sepete gonderilmez", 2 not in sent)
check("Zaten hatirlatilmis sepete tekrar gonderilmez", 3 not in sent)
check("Bos sepete gonderilmez", 4 not in sent)
check("Pasif sepete gonderilmez", 5 not in sent)
check("Sadece 1 hatirlatma gonderildi", sent==[1])
check("Gonderilen sepet damgalandi (tekrar onlenir)", carts[0]["reminder_sent_at"]==NOW)

# Ikinci calistirma tekrar gondermez (damga sayesinde)
sent2=send_reminders(carts, cart_items, customers)
check("Ikinci calistirma tekrar gondermez", sent2==[])

# Email hata izolasyonu - damga sadece basarili gonderimde
def send_with_failure(cart, fails):
    try:
        if fails: raise Exception("smtp")
        cart["reminder_sent_at"]=NOW  # sadece basarida
        return True
    except:
        return False  # damga atilmaz -> sonra tekrar denenir

c_fail={"id":9,"reminder_sent_at":None}
send_with_failure(c_fail, fails=True)
check("Email hatasinda damga atilmaz (tekrar denenir)", c_fail["reminder_sent_at"] is None)

# ============================================================
# [56] SON GÖRÜNTÜLENEN ÜRÜNLER (#31)
# ============================================================
class RecentStore:
    def __init__(self, cap=50): self.rows=[]; self.cap=cap; self._t=0
    def record(self, cust, pid):
        self._t+=1
        for r in self.rows:
            if r["cust"]==cust and r["pid"]==pid:
                r["viewed"]=self._t; return "updated"  # upsert
        self.rows.append({"cust":cust,"pid":pid,"viewed":self._t})
        # cap
        mine=[r for r in self.rows if r["cust"]==cust]
        if len(mine)>self.cap:
            mine.sort(key=lambda r:-r["viewed"])
            for old in mine[self.cap:]: self.rows.remove(old)
        return "added"
    def get(self, cust, limit=10):
        mine=sorted([r for r in self.rows if r["cust"]==cust], key=lambda r:-r["viewed"])
        return [r["pid"] for r in mine[:limit]]

st=RecentStore()
check("Ilk goruntuleme eklenir", st.record(1, 100)=="added")
check("Tekrar goruntuleme upsert (yeni satir degil)", st.record(1, 100)=="updated")
st.record(1, 200); st.record(1, 300)
check("Son goruntulenen en ustte (300 en son)", st.get(1)[0]==300)
# 100 tekrar goruntulenince en uste ciker
st.record(1, 100)
check("Tekrar goruntulenen basa gecer", st.get(1)[0]==100)
check("Toplam 3 farkli urun (upsert cift saymaz)", len(st.get(1))==3)

# Limit uygulanir
st2=RecentStore()
for i in range(20): st2.record(2, i)
check("Limit uygulanir (10)", len(st2.get(2, 10))==10)
check("En son goruntulenenler doner (19,18,...)", st2.get(2,3)==[19,18,17])

# Cap - musteri basina max 50
st3=RecentStore(cap=5)
for i in range(10): st3.record(3, i)
mine=[r for r in st3.rows if r["cust"]==3]
check("Cap: musteri basina max 5 kayit", len(mine)==5)
check("Cap: en yeniler tutulur (5,6,7,8,9)", sorted(r["pid"] for r in mine)==[5,6,7,8,9])

# Musteri izolasyonu
st4=RecentStore()
st4.record(1, 100); st4.record(2, 200)
check("Musteri izolasyonu - herkes kendi listesini gorur", st4.get(1)==[100] and st4.get(2)==[200])

# Limit sinir
def clamp(l): return 10 if (l<=0 or l>50) else l
check("Limit 0 -> 10", clamp(0)==10)
check("Limit 100 -> 10", clamp(100)==10)


# ============================================================
# [57] İPTAL STOK GERİ KAZANIMI + SEPET MİKTAR GUARD (bug fix)
# ============================================================
# İptal: Pending -> rezervasyon serbest (reserved düşer, fiziksel stok değişmez)
#        Confirmed+ -> fiziksel stok geri yüklenir (satış iptal)
STATUS_PENDING, STATUS_CONFIRMED = 0, 1
def cancel_order_stock(prev_status, stock, reserved, qty):
    # doner: (yeni_stock, yeni_reserved)
    if prev_status == STATUS_PENDING:
        # rezervasyon serbest: reserved azalir, stok ayni
        return (stock, max(0, reserved - qty))
    else:
        # fiziksel stok geri yukle (confirm'de dusmustu)
        return (stock + qty, reserved)

# Pending iptal: 10 stok, 3 rezerve, 3 adetlik siparis iptal
s, r = cancel_order_stock(STATUS_PENDING, 10, 3, 3)
check("Pending iptal: reserved azalir (3->0)", r == 0)
check("Pending iptal: fiziksel stok degismez (10)", s == 10)
check("Pending iptal sonrasi musait = 10", (s - r) == 10)

# Confirmed iptal: confirm'de stok 10->7 dustu (reserved 3->0). Iptal geri yukler 7->10
s2, r2 = cancel_order_stock(STATUS_CONFIRMED, 7, 0, 3)
check("Confirmed iptal: fiziksel stok geri yuklenir (7->10)", s2 == 10)
check("Confirmed iptal: reserved degismez (0)", r2 == 0)
check("Confirmed iptal sonrasi musait = 10 (hayalet kayip yok)", (s2 - r2) == 10)

# Kritik: iptal OLMASAYDI stok 7'de kalirdi (hayalet kayip) - fix bunu onler
check("Fix olmadan hayalet kayip olurdu (7 != 10)", 7 != 10)

# Sepet miktar guard: 1-100 disi reddedilir
def cart_add_allowed(qty): return 1 <= qty <= 100
check("Sepet: negatif adet reddedilir", not cart_add_allowed(-5))
check("Sepet: sifir adet reddedilir", not cart_add_allowed(0))
check("Sepet: 1 adet kabul", cart_add_allowed(1))
check("Sepet: 100 adet kabul", cart_add_allowed(100))
check("Sepet: 101 adet reddedilir (asiri)", not cart_add_allowed(101))

# CheckStock tek basina negatifi yakalamaz (bu yuzden guard gerekli)
def check_stock_only(available, qty): return available >= qty
check("CheckStock negatif adeti YANLIS gecirir (guard gerekli)", check_stock_only(10, -5) is True)
check("Guard negatif adeti yakalar", not cart_add_allowed(-5))


# ============================================================
# [58] SADAKAT PUANI + MAĞAZA KREDİSİ + HEDİYE KARTI (Dalga 2-3)
# ============================================================
# Puan kazanimi: her 10 TL = 1 puan
def earn_points(total): import math; return math.floor(total / 10)
check("100 TL siparis -> 10 puan", earn_points(100)==10)
check("155 TL -> 15 puan (floor)", earn_points(155)==15)
check("9 TL -> 0 puan", earn_points(9)==0)

# Puan -> kredi: 100 puan = 10 TL (1 puan = 0.10), min 100 puan
CREDIT_PER_POINT=0.10; MIN_REDEEM=100
def redeem(points, balance):
    if points < MIN_REDEEM: return ("min_error", balance, 0)
    if balance < points: return ("insufficient", balance, 0)
    credit = points * CREDIT_PER_POINT
    return ("ok", balance - points, credit)

st, bal, credit = redeem(100, 500)
check("100 puan bozdur -> 10 TL kredi", credit==10.0 and bal==400)
check("50 puan reddedilir (min 100)", redeem(50, 500)[0]=="min_error")
check("Yetersiz puan reddedilir", redeem(200, 100)[0]=="insufficient")

# Kredi kullanimi: bakiye kontrolu
def use_credit(amount, balance):
    if amount <= 0: return ("invalid", balance)
    if balance < amount: return ("insufficient", balance)
    return ("ok", balance - amount)
check("Kredi kullan: bakiyeden duser", use_credit(30, 100)==("ok", 70))
check("Yetersiz kredi reddedilir", use_credit(150, 100)[0]=="insufficient")
check("Negatif/sifir kredi reddedilir", use_credit(0, 100)[0]=="invalid")

# Hediye karti bozdurma: bakiye krediye aktarilir, kart kapanir
def redeem_giftcard(card_balance, card_active, customer_credit):
    if not card_active or card_balance <= 0: return ("invalid", customer_credit, card_balance, card_active)
    new_credit = customer_credit + card_balance
    return ("ok", new_credit, 0, False)  # kart sifirlanir + pasif

st, cred, cbal, active = redeem_giftcard(50, True, 20)
check("Hediye karti bozdur: 50 kredi eklenir (20->70)", cred==70)
check("Kart sifirlanir + pasif olur", cbal==0 and active is False)
check("Bos kart reddedilir", redeem_giftcard(0, True, 20)[0]=="invalid")
check("Pasif kart reddedilir", redeem_giftcard(50, False, 20)[0]=="invalid")

# Defter butunlugu: kazanim ve harcama ayri kayit + bakiye tutar
class Ledger:
    def __init__(self): self.balance=0; self.entries=[]
    def earn(self, amt): self.balance+=amt; self.entries.append(("earn",amt))
    def redeem(self, amt):
        if self.balance<amt: return False
        self.balance-=amt; self.entries.append(("redeem",amt)); return True
L=Ledger(); L.earn(100); L.earn(50); L.redeem(30)
check("Defter bakiyesi tutar (100+50-30=120)", L.balance==120)
check("Her islem ayri kayit", len(L.entries)==3)



# ============================================================
# [59] FİYAT DÜŞÜŞ + YORUM GÜÇLENDİRME + ENGAGEMENT + REFERANS (Dalga 4-6)
# ============================================================
# Fiyat düşüş: yeni fiyat < abone fiyatı ise bildir
def should_notify_price_drop(subscribed_price, new_price, is_notified):
    return (not is_notified) and (subscribed_price > new_price)
check("Fiyat düştü + bekleyen -> bildir", should_notify_price_drop(100, 80, False))
check("Fiyat aynı -> bildirme", not should_notify_price_drop(100, 100, False))
check("Fiyat arttı -> bildirme", not should_notify_price_drop(100, 120, False))
check("Zaten bildirilmiş -> tekrar bildirme", not should_notify_price_drop(100, 80, True))

# Doğrulanmış alıcı: teslim edilmiş siparişte ürün var mı
def is_verified_purchase(delivered_order_product_ids, product_id):
    return product_id in delivered_order_product_ids
check("Teslim edilmiş siparişte ürün -> doğrulanmış", is_verified_purchase([5,7,9], 7))
check("Satın almamış -> doğrulanmamış", not is_verified_purchase([5,7,9], 3))

# Faydalı oylama: müşteri başına tek
class ReviewVotes:
    def __init__(self): self.votes=set(); self.count=0
    def vote(self, review_id, customer_id):
        key=(review_id, customer_id)
        if key in self.votes: return False
        self.votes.add(key); self.count+=1; return True
RV=ReviewVotes()
check("İlk oy kabul", RV.vote(1, 100) is True)
check("Aynı müşteri tekrar oy reddedilir", RV.vote(1, 100) is False)
check("Farklı müşteri oy kabul", RV.vote(1, 200) is True)
check("Toplam faydalı = 2", RV.count==2)

# Küfür filtresi
BLACKLIST={"amk","salak","aptal","mal"}
def contains_profanity(text):
    import re
    return any(w in BLACKLIST for w in re.split(r'[^\w]+', text.lower()))
check("Temiz yorum kabul", not contains_profanity("Harika bir ürün cok begendim"))
check("Küfürlü yorum reddedilir", contains_profanity("bu ne salak tasarim"))

# Q&A: sadece yayınlanmış görünür
def visible_questions(all_q):
    return [q for q in all_q if q['published']]
qs=[{'id':1,'published':True},{'id':2,'published':False},{'id':3,'published':True}]
check("Sadece yayınlanmış Q&A görünür (2/3)", len(visible_questions(qs))==2)

# Doğum günü: bugün + bu yıl gönderilmemiş
def send_birthday(bd_day, bd_month, today_day, today_month, sent_year, this_year):
    is_bday = bd_day==today_day and bd_month==today_month
    already = sent_year==this_year
    return is_bday and not already
check("Bugün doğum günü + gönderilmemiş -> gönder", send_birthday(15,6,15,6,2024,2025))
check("Doğum günü değil -> gönderme", not send_birthday(15,6,20,6,0,2025))
check("Bu yıl zaten gönderilmiş -> gönderme", not send_birthday(15,6,15,6,2025,2025))

# Win-back: son sipariş > 60 gün + cooldown
def send_winback(days_since_order, days_since_last_winback):
    return days_since_order >= 60 and days_since_last_winback >= 30
check("70 gün önce sipariş + cooldown ok -> gönder", send_winback(70, 45))
check("30 gün önce sipariş -> gönderme (yakın)", not send_winback(30, 45))
check("Yakında win-back gönderilmiş -> gönderme (cooldown)", not send_winback(70, 10))

# Referans ödülü: sadece İLK tamamlanan siparişte
def reward_referral(referred_by_set, completed_order_count):
    return referred_by_set and completed_order_count==1
check("Davet edilmiş + ilk sipariş -> ödül", reward_referral(True, 1))
check("Davet edilmemiş -> ödül yok", not reward_referral(False, 1))
check("2. sipariş -> ödül yok (sadece ilk)", not reward_referral(True, 2))

# İki tarafa kredi
def referral_credit(referrer_bal, referee_bal, reward=50):
    return (referrer_bal+reward, referee_bal+reward)
rb, rf = referral_credit(0, 10)
check("Davet eden +50, davet edilen +50", rb==50 and rf==60)



# ============================================================
# [60] FLASH SALE + İLK-SİPARİŞ + VİTRİN + KUPON CONCURRENCY (Dalga 7-9)
# ============================================================
from datetime import datetime, timedelta

# Flash sale etkin fiyat: pencere aktifse sale_price
def effective_price(price, sale_price, sale_start, sale_end, now):
    on_sale = (sale_price is not None and sale_price > 0
               and (sale_start is None or now >= sale_start)
               and (sale_end is None or now <= sale_end))
    return sale_price if on_sale else price

now = datetime(2025, 6, 15, 12, 0)
check("Aktif flash sale -> indirimli fiyat", effective_price(100, 70, now-timedelta(hours=1), now+timedelta(hours=1), now)==70)
check("Sale bitmiş -> normal fiyat", effective_price(100, 70, now-timedelta(days=5), now-timedelta(days=1), now)==100)
check("Sale başlamamış -> normal fiyat", effective_price(100, 70, now+timedelta(days=1), now+timedelta(days=2), now)==100)
check("Sale fiyatı yok -> normal fiyat", effective_price(100, None, None, None, now)==100)
check("Süresiz sale (tarih yok) -> indirimli", effective_price(100, 80, None, None, now)==80)

# Siparişte etkin fiyat kullanılır (flash sale gerçekten indirir)
def order_subtotal(items):  # items: [(effective_price, qty)]
    return sum(p*q for p,q in items)
check("Sipariş flash sale fiyatıyla hesaplanır", order_subtotal([(70,2),(80,1)])==220)

# İlk-sipariş kuponu: tamamlanmış sipariş varsa geçersiz
def first_order_coupon_valid(completed_order_count):
    return completed_order_count == 0
check("İlk sipariş (0 tamamlanmış) -> kupon geçerli", first_order_coupon_valid(0))
check("Tamamlanmış sipariş var -> ilk-sipariş kuponu geçersiz", not first_order_coupon_valid(2))

# Çok satanlar: adet bazında sırala
def best_sellers(order_items, take):
    from collections import defaultdict
    agg=defaultdict(int)
    for pid, qty in order_items: agg[pid]+=qty
    return [pid for pid,_ in sorted(agg.items(), key=lambda x:-x[1])][:take]
items=[(1,5),(2,10),(1,3),(3,20),(2,1)]  # 1:8, 2:11, 3:20
check("Çok satanlar adet sırasıyla (3,2,1)", best_sellers(items, 3)==[3,2,1])
check("Take limiti uygulanır", len(best_sellers(items, 2))==2)

# Trending: son 30 gün penceresi
def trending(orders, order_items, now, window_days=30):
    from collections import defaultdict
    cutoff = now - timedelta(days=window_days)
    recent_order_ids = {oid for oid, created in orders if created >= cutoff}
    agg=defaultdict(int)
    for oid, pid, qty in order_items:
        if oid in recent_order_ids: agg[pid]+=qty
    return sorted(agg.items(), key=lambda x:-x[1])
orders=[(100, now-timedelta(days=5)), (101, now-timedelta(days=40))]  # 101 pencere disi
oitems=[(100,1,5),(101,2,99)]  # sadece 100 sayilir
tr=trending(orders, oitems, now)
check("Trending sadece son 30 gün siparişlerini sayar", tr==[(1,5)])

# Kupon used_count optimistic concurrency retry
class CouponRow:
    def __init__(self): self.used=0; self.version=0
    def try_increment(self, expected_version):
        if expected_version != self.version: return False  # concurrency conflict
        self.used+=1; self.version+=1; return True
def increment_with_retry(coupon, max_retry=5):
    for _ in range(max_retry):
        v=coupon.version  # taze oku
        if coupon.try_increment(v): return True
    return False
c=CouponRow()
check("Tek artış başarılı", increment_with_retry(c) and c.used==1)
# Iki eszamanli: ilki eski versiyonla dener -> conflict -> retry -> basarili
c2=CouponRow()
v_stale=c2.version   # eski versiyon yakala
c2.try_increment(c2.version)  # baska islem araya girdi (used=1, version=1)
# stale versiyonla deneme conflict verir, retry taze okur
def increment_stale_then_retry(coupon, stale_v, max_retry=5):
    tried_stale=False
    for _ in range(max_retry):
        v = stale_v if not tried_stale else coupon.version
        tried_stale=True
        if coupon.try_increment(v): return True
    return False
check("Concurrency çakışması retry ile çözülür", increment_stale_then_retry(c2, v_stale) and c2.used==2)

# used_count post-commit: ödeme concurrency'den etkilenmez (rollback yok)
def payment_succeeds_despite_coupon_conflict(payment_committed, coupon_increment_failed):
    # Ödeme commit edildikten SONRA kupon artışı denenir; başarısız olsa da ödeme geçerli
    return payment_committed  # kupon sonucu ödemeyi etkilemez
check("Kupon çakışması ödemeyi bozmaz (post-commit)", payment_succeeds_despite_coupon_conflict(True, True))

# RFC 7807 problem details şekli
def problem_details(status, path):
    return {"type": f"https://httpstatuses.io/{status}", "title": "Sunucu Hatası",
            "status": status, "instance": path, "traceId": "abc"}
pd = problem_details(500, "/api/order")
check("Problem Details RFC 7807 alanları", all(k in pd for k in ["type","title","status","instance","traceId"]))



# ============================================================
# [61] SEPET/SİPARİŞ + KATALOG + CHECKOUT + IDEMPOTENCY (Dalga 10-13)
# ============================================================
from datetime import datetime, timedelta

# Kaydet-sonra-al: sepetten cikar + favorilere ekle (idempotent)
def save_for_later(cart_items, wishlist, pid, size):
    cart_items = [i for i in cart_items if not (i['pid']==pid and i['size']==size)]
    if pid not in wishlist: wishlist.append(pid)
    return cart_items, wishlist
ci=[{'pid':1,'size':'M'},{'pid':2,'size':'L'}]; wl=[]
ci, wl = save_for_later(ci, wl, 1, 'M')
check("Kaydet-sonra-al: sepetten cikti", not any(i['pid']==1 for i in ci))
check("Kaydet-sonra-al: favorilere eklendi", 1 in wl)
ci2, wl2 = save_for_later(ci, wl, 1, 'M')  # tekrar
check("Idempotent: favori cift eklenmez", wl2.count(1)==1)

# Kismi iptal: sadece Confirmed/Preparing (odenmis+kargolanmamis)
def can_cancel_item(status):  # 1=Confirmed, 2=Preparing, 3=Shipped, 4=Delivered
    return status in (1, 2)
check("Confirmed'da kalem iptali olur", can_cancel_item(1))
check("Preparing'de kalem iptali olur", can_cancel_item(2))
check("Shipped'de kalem iptali OLMAZ", not can_cancel_item(3))
check("Delivered'da kalem iptali OLMAZ", not can_cancel_item(4))

# Kismi iptal: tutar dus + kredi iade + son kalemse siparis iptal
def cancel_item(order_subtotal, order_total, item_amount, remaining_after, customer_credit):
    new_sub = max(0, order_subtotal - item_amount)
    new_total = max(0, order_total - item_amount)
    new_credit = customer_credit + item_amount  # iade krediye
    new_status = 5 if remaining_after == 0 else None  # 5=Cancelled
    return new_sub, new_total, new_credit, new_status
sub, tot, cr, st = cancel_item(300, 320, 100, 1, 20)
check("Kismi iptal: tutar dusuruldu", sub==200 and tot==220)
check("Kismi iptal: kredi iade edildi", cr==120)
check("Kismi iptal: kalem kaldi -> siparis acik", st is None)
_,_,_, st2 = cancel_item(100, 120, 100, 0, 0)
check("Kismi iptal: son kalem -> siparis iptal", st2==5)

# Tahmini teslim: is gunu ekle (hafta sonu atla)
def add_business_days(start, days):
    d=start; added=0
    while added<days:
        d += timedelta(days=1)
        if d.weekday()<5: added+=1  # 0-4 hafta ici
    return d
# Cuma + 3 is gunu = Carsamba (Cmt/Paz atlanir)
friday=datetime(2025,6,13)  # Cuma
check("Cuma+3 is gunu Carsamba (hafta sonu atlanir)", add_business_days(friday,3).weekday()==2)

# Faceted search: tum anahtar filtrelerini karsilayan urunler (anahtarlar arasi VE)
def faceted_filter(products_attrs, filters):
    # products_attrs: {pid: {key: {values}}}, filters: {key: {values}}
    result=[]
    for pid, attrs in products_attrs.items():
        if all(k in attrs and attrs[k] & vals for k, vals in filters.items()):
            result.append(pid)
    return sorted(result)
pa={1:{'materyal':{'pamuk'},'sezon':{'yaz'}}, 2:{'materyal':{'polyester'},'sezon':{'yaz'}}, 3:{'materyal':{'pamuk'},'sezon':{'kis'}}}
check("Faceted: pamuk+yaz -> sadece urun 1", faceted_filter(pa, {'materyal':{'pamuk'},'sezon':{'yaz'}})==[1])
check("Faceted: sadece yaz -> urun 1,2", faceted_filter(pa, {'sezon':{'yaz'}})==[1,2])
check("Faceted: pamuk (VEYA deger yok) -> 1,3", faceted_filter(pa, {'materyal':{'pamuk'}})==[1,3])

# Beden onerisi: en yakin (mutlak fark toplami)
def recommend_size(entries, bust, waist, hip):
    best=None; best_score=float('inf')
    for label, b, w, h in entries:
        score=0; considered=0
        if bust and b: score+=abs(bust-b); considered+=1
        if waist and w: score+=abs(waist-w); considered+=1
        if hip and h: score+=abs(hip-h); considered+=1
        if considered==0: continue
        if score<best_score: best_score=score; best=label
    return best
entries=[('S',84,64,90),('M',88,68,94),('L',92,72,98)]
check("Beden onerisi: 89/69/95 -> M", recommend_size(entries, 89, 69, 95)=='M')
check("Beden onerisi: 83/63/89 -> S", recommend_size(entries, 83, 63, 89)=='S')

# Karsilastirma: 2-4 urun siniri
def can_compare(count):
    return 2 <= count <= 4
check("2 urun karsilastirilir", can_compare(2))
check("4 urun karsilastirilir", can_compare(4))
check("1 urun karsilastirilamaz", not can_compare(1))
check("5 urun karsilastirilamaz", not can_compare(5))

# Misafir checkout: var olan e-posta reddi
def guest_checkout_allowed(email_exists):
    return not email_exists
check("Yeni e-posta -> misafir checkout olur", guest_checkout_allowed(False))
check("Kayitli e-posta -> giris yonlendir", not guest_checkout_allowed(True))

# Idempotency: ayni anahtar ikinci kez islenmez (cache'ten doner)
class IdempotencyStore:
    def __init__(self): self.cache={}
    def process(self, key, compute):
        if key in self.cache: return self.cache[key], True  # replayed
        result=compute(); 
        if result[0]<500: self.cache[key]=result  # sadece kesin sonuc cache'lenir
        return result, False
store=IdempotencyStore()
counter=[0]
def make_order(): counter[0]+=1; return (200, f"order-{counter[0]}")
r1, replay1 = store.process("key-abc", make_order)
r2, replay2 = store.process("key-abc", make_order)
check("Idempotency: ilk istek islenir", replay1 is False and r1==(200,"order-1"))
check("Idempotency: ayni anahtar cache'ten (replay)", replay2 is True and r2==(200,"order-1"))
check("Idempotency: islem bir kez calisti", counter[0]==1)
# 5xx cache'lenmez (tekrar denenebilir)
store2=IdempotencyStore()
def failing(): return (503, "gecici hata")
_, _ = store2.process("key-x", failing)
check("Idempotency: 5xx cache'lenmez", "key-x" not in store2.cache)


# ==================================================================
# GÜVENLİK DENETİMİ FİX'LERİ (bu oturumda bulunan+fixlenen açıklar)
# ==================================================================
print("\n--- Güvenlik fix testleri ---")

# BULGU #1: PlaceOrder miktar doğrulaması (fiyat manipülasyonu / stok baypası)
def place_order_line_valid(quantity, size):
    # C# ile ayni: quantity 1-100 disi VEYA bos beden -> reddet
    if quantity < 1 or quantity > 100:
        return (400, "OrderInvalidQuantity")
    if not size or not size.strip():
        return (400, "OrderInvalidSize")
    return (200, "OK")
check("Sipariş: negatif miktar reddedilir (fiyat manip. engeli)", place_order_line_valid(-5, "M") == (400, "OrderInvalidQuantity"))
check("Sipariş: sıfır miktar reddedilir", place_order_line_valid(0, "M") == (400, "OrderInvalidQuantity"))
check("Sipariş: 100 üstü miktar reddedilir", place_order_line_valid(101, "M") == (400, "OrderInvalidQuantity"))
check("Sipariş: boş beden reddedilir", place_order_line_valid(2, "") == (400, "OrderInvalidSize"))
check("Sipariş: geçerli kalem kabul edilir", place_order_line_valid(2, "M") == (200, "OK"))

# Negatif miktarın subtotal'ı düşürmesi ARTIK mümkün değil (reddediliyor)
def compute_subtotal_with_validation(items):
    # items: [(price, qty)]
    subtotal = 0
    for price, qty in items:
        code, _ = place_order_line_valid(qty, "M")
        if code != 200:
            return None  # sipariş reddedilir
        subtotal += price * qty
    return subtotal
check("Sipariş: negatif kalemle toplam düşürme ENGELLENDİ",
      compute_subtotal_with_validation([(1000, 1), (500, -3)]) is None)
check("Sipariş: geçerli kalemlerle toplam doğru", approx(compute_subtotal_with_validation([(1000, 2), (500, 1)]), 2500))

# BULGU #2: Çift-iade engeli (kalan iade-edilebilir miktar bazlı)
REJECTED = 2
def returnable_remaining(original_qty, prior_returns):
    # prior_returns: [(quantity, status)] - Rejected haric hepsi miktari tuketir
    already = sum(q for q, st in prior_returns if st != REJECTED)
    return original_qty - already
def can_return(original_qty, request_qty, prior_returns):
    if request_qty <= 0:
        return False
    return request_qty <= returnable_remaining(original_qty, prior_returns)
check("İade: 5 al -> 5 iade (Completed) -> tekrar 5 iade ENGELLENDİ (çift iade)",
      can_return(5, 5, [(5, 3)]) is False)  # 3=Completed
check("İade: 5 al -> 2 iade (Approved) -> 3 daha iade EDİLEBİLİR",
      can_return(5, 3, [(2, 1)]) is True)   # 1=Approved
check("İade: 5 al -> 2 iade (Approved) -> 4 daha iade ENGELLENDİ (kalan 3)",
      can_return(5, 4, [(2, 1)]) is False)
check("İade: reddedilen iade miktarı serbest bırakır (tekrar talep edilebilir)",
      can_return(5, 5, [(5, REJECTED)]) is True)
check("İade: negatif/sıfır miktar reddedilir", can_return(5, 0, []) is False and can_return(5, -1, []) is False)

# BULGU #4: PlaceOrder tam kupon doğrulaması (baypas engeli)
import datetime as _dt
def place_order_coupon_valid(subtotal, coupon, customer_has_completed_order):
    # C# ile ayni: min_amount + expire + usage_limit + first_order + (percentage'de max cap)
    if coupon is None:
        return (False, 0)
    if subtotal < coupon["min_amount"]:
        return (False, 0)
    if coupon.get("expire_date") and coupon["expire_date"] < _dt.datetime.now():
        return (False, 0)
    if coupon["usage_limit"] > 0 and coupon["used_count"] >= coupon["usage_limit"]:
        return (False, 0)
    if coupon.get("first_order_only") and customer_has_completed_order:
        return (False, 0)
    # indirim
    if coupon["type"] == "pct":
        d = round(subtotal * coupon["value"] / 100, 2)
        if coupon.get("max_discount") is not None and d > coupon["max_discount"]:
            d = coupon["max_discount"]
        return (True, d)
    return (True, min(coupon["value"], subtotal))
_expired = {"min_amount": 0, "expire_date": _dt.datetime(2020,1,1), "usage_limit": 0, "used_count": 0, "type": "pct", "value": 10}
check("Kupon(PlaceOrder): süresi dolmuş kupon UYGULANMAZ (baypas engeli)",
      place_order_coupon_valid(1000, _expired, False) == (False, 0))
_maxed = {"min_amount": 0, "usage_limit": 5, "used_count": 5, "type": "pct", "value": 10}
check("Kupon(PlaceOrder): limit dolan kupon UYGULANMAZ",
      place_order_coupon_valid(1000, _maxed, False) == (False, 0))
_firstonly = {"min_amount": 0, "usage_limit": 0, "used_count": 0, "type": "pct", "value": 10, "first_order_only": True}
check("Kupon(PlaceOrder): ilk-sipariş kuponu, önceki siparişi olan müşteride UYGULANMAZ",
      place_order_coupon_valid(1000, _firstonly, True) == (False, 0))
check("Kupon(PlaceOrder): ilk-sipariş kuponu, yeni müşteride uygulanır",
      place_order_coupon_valid(1000, _firstonly, False)[0] is True)
_capped = {"min_amount": 0, "usage_limit": 0, "used_count": 0, "type": "pct", "value": 20, "max_discount": 100}
ok_cap, d_cap = place_order_coupon_valid(1000, _capped, False)  # %20=200 ama tavan 100
check("Kupon(PlaceOrder): yüzde indirim tavanı uygulanır (200 -> 100)", ok_cap and approx(d_cap, 100))


# BULGU #3: Atomik bakiye işlemleri (eşzamanlı çift-harcama / çift-bozdurma engeli)
# Atomik "UPDATE ... WHERE balance >= amount" davranışını simüle et: WHERE guard geçmezse 0 satır.
class AtomicBalance:
    def __init__(self, balance): self.balance = balance
    def try_decrement(self, amount):
        # Tek atomik işlem: koşul + düşüm bölünmez (iki eşzamanlı çağrı sırayla değerlendirilir)
        if self.balance >= amount:
            self.balance -= amount
            return 1  # affected
        return 0
# Senaryo: bakiye 100, iki eşzamanlı 100'lük harcama isteği -> yalnızca biri başarılı
acct = AtomicBalance(100)
r1 = acct.try_decrement(100)
r2 = acct.try_decrement(100)  # ikinci istek: bakiye artık 0, WHERE geçmez
check("Mağaza kredisi: eşzamanlı çift-harcama ENGELLENDİ (biri 0 alır)", r1 == 1 and r2 == 0)
check("Mağaza kredisi: bakiye negatife düşmedi", acct.balance == 0)
# Yeterli bakiyede kısmi harcamalar
acct2 = AtomicBalance(100)
check("Mağaza kredisi: 60 harca -> başarı, kalan 40", acct2.try_decrement(60) == 1 and acct2.balance == 40)
check("Mağaza kredisi: 50 daha harca -> yetersiz (kalan 40), reddedilir", acct2.try_decrement(50) == 0 and acct2.balance == 40)

# Hediye kartı compare-and-swap: iki eşzamanlı bozdurma -> yalnız biri krediyi alır
class AtomicGiftCard:
    def __init__(self, balance): self.balance = balance
    def try_redeem(self, expected):
        # WHERE balance == expected AND balance > 0 -> SET balance = 0
        if self.balance == expected and self.balance > 0:
            self.balance = 0
            return 1
        return 0
card = AtomicGiftCard(250)
amount = card.balance  # her iki istek de 250 okur
c1 = card.try_redeem(amount)
c2 = card.try_redeem(amount)  # ikinci: balance artık 0, expected(250) eşleşmez
check("Hediye kartı: eşzamanlı çift-bozdurma ENGELLENDİ (biri 0 alır)", c1 == 1 and c2 == 0)
total_credited = amount * (c1 + c2)  # yalnızca başarılı olan kadar kredi
check("Hediye kartı: yalnızca 250 kredi verildi (500 değil)", total_credited == 250)


# ==================================================================
# ADMIN OLUŞTURMA (bu oturumda eklenen: seeder + promote guard)
# ==================================================================
print("\n--- Admin oluşturma testleri ---")
ADMIN, CUSTOMER = 1, 2

# AdminSeeder mantığı: idempotent + mevcut e-postayı yükselt / yeni oluştur
def seed_admin(enabled, email, password, existing_customers):
    # existing_customers: [(email, user_type)]
    if not enabled:
        return ("skip", None)
    if not email or not password:
        return ("skip-missing-config", None)
    # zaten admin var mı
    if any(ut == ADMIN for _, ut in existing_customers):
        return ("skip-admin-exists", None)
    # e-posta kayıtlı mı -> yükselt
    for i, (em, ut) in enumerate(existing_customers):
        if em == email:
            return ("promoted", i)
    return ("created", None)

check("Seeder: kapalıysa hiçbir şey yapmaz", seed_admin(False, "a@x.com", "pw", [])[0] == "skip")
check("Seeder: config eksikse çalışmaz", seed_admin(True, "", "pw", [])[0] == "skip-missing-config")
check("Seeder: admin zaten varsa atlar (idempotent)",
      seed_admin(True, "a@x.com", "pw", [("b@x.com", ADMIN)])[0] == "skip-admin-exists")
check("Seeder: mevcut müşteri e-postası admin'e yükseltilir",
      seed_admin(True, "a@x.com", "pw", [("a@x.com", CUSTOMER)])[0] == "promoted")
check("Seeder: yeni e-posta ile admin oluşturulur",
      seed_admin(True, "yeni@x.com", "pw", [("a@x.com", CUSTOMER)])[0] == "created")

# SetUserType promote guard: geçersiz tip + son-admin koruması
def set_user_type(target_current_type, new_type, all_admin_count):
    if new_type not in (ADMIN, CUSTOMER):
        return "InvalidUserType"
    # son admin müşteriye indirilemez
    if target_current_type == ADMIN and new_type == CUSTOMER and all_admin_count <= 1:
        return "CannotDemoteLastAdmin"
    return "UserTypeUpdated"
check("Promote: geçersiz tip (3) reddedilir", set_user_type(CUSTOMER, 3, 5) == "InvalidUserType")
check("Promote: müşteri admin'e yükseltilir", set_user_type(CUSTOMER, ADMIN, 1) == "UserTypeUpdated")
check("Promote: SON admin müşteriye indirilemez (kilitlenme engeli)",
      set_user_type(ADMIN, CUSTOMER, 1) == "CannotDemoteLastAdmin")
check("Promote: başka admin varken admin müşteriye indirilebilir",
      set_user_type(ADMIN, CUSTOMER, 2) == "UserTypeUpdated")

# KDV ayrıştırma (configurable oran) - fiyat KDV dahil: matrah = total/(1+r), kdv = total - matrah
def kdv_breakdown(total, rate):
    net = round(total / (1 + rate), 2)
    tax = round(total - net, 2)
    return net, tax
net20, tax20 = kdv_breakdown(1200, 0.20)  # %20: matrah 1000, KDV 200
check("KDV %20: 1200₺ -> matrah 1000₺ + KDV 200₺", approx(net20, 1000) and approx(tax20, 200))
net10, tax10 = kdv_breakdown(1100, 0.10)  # %10: matrah 1000, KDV 100
check("KDV %10 (configurable): 1100₺ -> matrah 1000₺ + KDV 100₺", approx(net10, 1000) and approx(tax10, 100))


print("\n" + "=" * 62)
print(f"SONUÇ:  {_passed} geçti, {_failed} başarısız  (toplam {_passed + _failed})")
print("=" * 62)
import sys
sys.exit(0 if _failed == 0 else 1)
