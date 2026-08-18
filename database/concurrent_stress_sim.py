#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Divisima GERCEK-ESZAMANLILIK stres simulasyonu (v1).

Onceki advanced_simulation.py eszamanliligi SIRALI (adversarial siralama) simule eder - tek thread.
Bu simulasyon GERCEK OS thread'leri + GERCEK SQLite motoru (WAL modu) + GERCEK kilit cekismesi kullanir.
Amac: C#'taki atomik desenlerin (ExecuteUpdateAsync CAS, filtered-unique index, WHERE-guard'li UPDATE)
GERCEK paralellikte tutup tutmadigini kanitlamak. SQLite'in transaction izolasyonu + BEGIN IMMEDIATE
+ busy_timeout, MSSQL'in RowVersion/atomik-UPDATE davranisinin gecerli bir yaklasimidir.

Her thread kendi baglantisini kullanir; yazma cakismasi "database is locked" -> retry ile cozulur.
Sonda GLOBAL invariant'lar dogrulanir (overselling yok, negatif bakiye yok, defter==sayac, cift-yok).
"""
import sqlite3, threading, random, time, os, sys

DB = "/tmp/divisima_concurrent.db"
if os.path.exists(DB):
    os.remove(DB)

# ---- Semayi kur ----
setup = sqlite3.connect(DB)
setup.executescript("""
PRAGMA journal_mode=WAL;
CREATE TABLE product_stocks (
    id INTEGER PRIMARY KEY, product_id INT, size TEXT,
    stock_quantity INT, reserved_quantity INT DEFAULT 0
);
CREATE TABLE customers (
    id INTEGER PRIMARY KEY, store_credit REAL DEFAULT 0, loyalty_points INT DEFAULT 0
);
CREATE TABLE orders (
    id INTEGER PRIMARY KEY AUTOINCREMENT, customer_id INT, request_id TEXT
);
CREATE UNIQUE INDEX ix_orders_req ON orders(request_id) WHERE request_id IS NOT NULL;
CREATE TABLE reservations (
    id INTEGER PRIMARY KEY AUTOINCREMENT, product_id INT, size TEXT, qty INT, status TEXT
);
CREATE TABLE reviews (id INTEGER PRIMARY KEY, helpful_count INT DEFAULT 0);
CREATE TABLE votes (id INTEGER PRIMARY KEY AUTOINCREMENT, review_id INT, customer_id INT);
CREATE UNIQUE INDEX ix_votes ON votes(review_id, customer_id);
CREATE TABLE cart_items (
    id INTEGER PRIMARY KEY AUTOINCREMENT, cart_id INT, product_id INT, size TEXT, quantity INT, is_active INT DEFAULT 1
);
CREATE UNIQUE INDEX ix_cart ON cart_items(cart_id, product_id, size) WHERE is_active=1;
CREATE TABLE credit_ledger (id INTEGER PRIMARY KEY AUTOINCREMENT, customer_id INT, amount REAL, kind TEXT);
CREATE TABLE outbox (id INTEGER PRIMARY KEY, status INT DEFAULT 0, processed_at TEXT);
CREATE TABLE deliveries (id INTEGER PRIMARY KEY AUTOINCREMENT, outbox_id INT);
CREATE TABLE payment_attempts (customer_id INTEGER PRIMARY KEY, attempts INT DEFAULT 0);
CREATE TABLE idem_keys (key TEXT PRIMARY KEY);
CREATE TABLE processed_requests (id INTEGER PRIMARY KEY AUTOINCREMENT, idem_key TEXT);
""")
# Seed: 20 urun x 3 beden, her biri 100 stok; 10 musteri 1000 kredi + 5000 puan; 5 yorum
SIZES = ["S", "M", "L"]
N_PRODUCTS = 20
N_CUSTOMERS = 10
INIT_STOCK = 100
INIT_CREDIT = 1000.0
INIT_POINTS = 5000
for p in range(1, N_PRODUCTS + 1):
    for sz in SIZES:
        setup.execute("INSERT INTO product_stocks (product_id,size,stock_quantity,reserved_quantity) VALUES (?,?,?,0)", (p, sz, INIT_STOCK))
for c in range(1, N_CUSTOMERS + 1):
    setup.execute("INSERT INTO customers (id,store_credit,loyalty_points) VALUES (?,?,?)", (c, INIT_CREDIT, INIT_POINTS))
    setup.execute("INSERT INTO credit_ledger (customer_id,amount,kind) VALUES (?,?,'seed')", (c, INIT_CREDIT))
for r in range(1, 6):
    setup.execute("INSERT INTO reviews (id,helpful_count) VALUES (?,0)", (r,))
# Outbox mesajlari (100 pending) - cok thread claim etmeye calisir, cift teslim OLMAMALI
N_OUTBOX = 100
for i in range(1, N_OUTBOX + 1):
    setup.execute("INSERT INTO outbox (id,status) VALUES (?,0)", (i,))
# SICAK KAYNAKLAR (asiri cekisme - overselling/double-spend'i zorla):
# Sicak urun 999: TEK beden, SADECE 5 stok -> onlarca thread ayni anda rezerve etmeye calisir
HOT_PRODUCT = 999
HOT_STOCK = 5
setup.execute("INSERT INTO product_stocks (product_id,size,stock_quantity,reserved_quantity) VALUES (?,?,?,0)", (HOT_PRODUCT, "M", HOT_STOCK))
# Sicak musteri 99: SADECE 50 kredi + 200 puan -> onlarca thread ayni anda harcamaya calisir
HOT_CUSTOMER = 99
HOT_CREDIT = 50.0
HOT_POINTS = 200
setup.execute("INSERT INTO customers (id,store_credit,loyalty_points) VALUES (?,?,?)", (HOT_CUSTOMER, HOT_CREDIT, HOT_POINTS))
setup.execute("INSERT INTO credit_ledger (customer_id,amount,kind) VALUES (?,?,'seed')", (HOT_CUSTOMER, HOT_CREDIT))
setup.commit()
setup.close()

# ---- Thread-guvenli baglanti + retry ----
def connect():
    con = sqlite3.connect(DB, timeout=30)
    con.execute("PRAGMA busy_timeout=30000")
    con.execute("PRAGMA journal_mode=WAL")
    return con

def retry(fn, tries=200):
    for _ in range(tries):
        try:
            return fn()
        except sqlite3.OperationalError as e:
            if "locked" in str(e) or "busy" in str(e):
                time.sleep(random.uniform(0.001, 0.005)); continue
            raise
    raise RuntimeError("retry tukendi (deadlock?)")

# ---- Atomik islemler (C# DAL desenlerinin GERCEK-DB karsiligi) ----
def op_reserve(con, pid, sz, qty):
    def f():
        con.execute("BEGIN IMMEDIATE")
        cur = con.execute(
            "UPDATE product_stocks SET reserved_quantity=reserved_quantity+? "
            "WHERE product_id=? AND size=? AND stock_quantity-reserved_quantity>=?",
            (qty, pid, sz, qty))
        if cur.rowcount == 1:
            con.execute("INSERT INTO reservations (product_id,size,qty,status) VALUES (?,?,?, 'Active')", (pid, sz, qty))
            con.commit(); return True
        con.commit(); return False
    return retry(f)

def op_confirm(con, resv_id, pid, sz, qty):
    def f():
        con.execute("BEGIN IMMEDIATE")
        # rezervasyonu Active->Confirmed (atomik gecis)
        cur = con.execute("UPDATE reservations SET status='Confirmed' WHERE id=? AND status='Active'", (resv_id,))
        if cur.rowcount == 1:
            con.execute("UPDATE product_stocks SET stock_quantity=stock_quantity-?, reserved_quantity=reserved_quantity-? "
                        "WHERE product_id=? AND size=? AND reserved_quantity>=?", (qty, qty, pid, sz, qty))
            con.commit(); return True
        con.commit(); return False
    return retry(f)

def op_release(con, resv_id, pid, sz, qty):
    def f():
        con.execute("BEGIN IMMEDIATE")
        cur = con.execute("UPDATE reservations SET status='Released' WHERE id=? AND status='Active'", (resv_id,))
        if cur.rowcount == 1:
            con.execute("UPDATE product_stocks SET reserved_quantity=reserved_quantity-? "
                        "WHERE product_id=? AND size=? AND reserved_quantity>=?", (qty, pid, sz, qty))
            con.commit(); return True
        con.commit(); return False
    return retry(f)

def op_redeem_credit(con, cid, amt):
    def f():
        con.execute("BEGIN IMMEDIATE")
        cur = con.execute("UPDATE customers SET store_credit=store_credit-? WHERE id=? AND store_credit>=?", (amt, cid, amt))
        if cur.rowcount == 1:
            con.execute("INSERT INTO credit_ledger (customer_id,amount,kind) VALUES (?,?,'redeem')", (cid, -amt))
            con.commit(); return True
        con.commit(); return False
    return retry(f)

def op_add_credit(con, cid, amt):
    def f():
        con.execute("BEGIN IMMEDIATE")
        con.execute("UPDATE customers SET store_credit=store_credit+? WHERE id=?", (amt, cid))
        con.execute("INSERT INTO credit_ledger (customer_id,amount,kind) VALUES (?,?,'add')", (cid, amt))
        con.commit(); return True
    return retry(f)

def op_redeem_loyalty(con, cid, pts):
    def f():
        con.execute("BEGIN IMMEDIATE")
        cur = con.execute("UPDATE customers SET loyalty_points=loyalty_points-? WHERE id=? AND loyalty_points>=?", (pts, cid, pts))
        con.commit(); return cur.rowcount == 1
    return retry(f)

def op_place_order(con, cid, req):
    def f():
        try:
            con.execute("BEGIN IMMEDIATE")
            con.execute("INSERT INTO orders (customer_id,request_id) VALUES (?,?)", (cid, req))
            con.commit(); return "placed"
        except sqlite3.IntegrityError:
            con.rollback(); return "duplicate"  # idempotent - kazanan zaten var
    return retry(f)

def op_vote(con, rid, cid):
    def f():
        try:
            con.execute("BEGIN IMMEDIATE")
            con.execute("INSERT INTO votes (review_id,customer_id) VALUES (?,?)", (rid, cid))
            con.execute("UPDATE reviews SET helpful_count=helpful_count+1 WHERE id=?", (rid,))
            con.commit(); return "voted"
        except sqlite3.IntegrityError:
            con.rollback(); return "already"  # sayac ARTMAZ (insert basarisiz)
    return retry(f)

def op_cart_add(con, cart, pid, sz):
    def f():
        try:
            con.execute("BEGIN IMMEDIATE")
            con.execute("INSERT INTO cart_items (cart_id,product_id,size,quantity,is_active) VALUES (?,?,?,1,1)", (cart, pid, sz))
            con.commit(); return "added"
        except sqlite3.IntegrityError:
            con.rollback()
            con.execute("UPDATE cart_items SET quantity=quantity+1 WHERE cart_id=? AND product_id=? AND size=? AND is_active=1", (cart, pid, sz))
            con.commit(); return "merged"
    return retry(f)

def op_outbox_claim(con):
    # OutboxProcessor.TryClaimAsync GERCEK-DB karsiligi: Pending(0)->Processing(3) atomik claim.
    # rowcount=1 ise BU thread teslim eder (delivery kaydi). Iki thread ayni mesaji claim EDEMEZ -> cift teslim yok.
    def f():
        con.execute("BEGIN IMMEDIATE")
        row = con.execute("SELECT id FROM outbox WHERE status=0 ORDER BY RANDOM() LIMIT 1").fetchone()
        if not row:
            con.commit(); return False
        mid = row[0]
        cur = con.execute("UPDATE outbox SET status=3, processed_at='claimed' WHERE id=? AND status=0", (mid,))
        if cur.rowcount == 1:
            con.execute("INSERT INTO deliveries (outbox_id) VALUES (?)", (mid,))  # teslim et
            con.execute("UPDATE outbox SET status=1 WHERE id=?", (mid,))          # Processed
            con.commit(); return True
        con.commit(); return False
    return retry(f)

def op_idem_process(con, key):
    # IdempotencyMiddleware.TryAddAsync (SETNX) GERCEK-DB karsiligi: INSERT OR IGNORE (atomik set-if-not-exists).
    # rowcount=1 ise BU istek isler; degilse zaten islenmis -> 409. Iki eszamanli ayni-key CIFT ISLEYEMEZ.
    def f():
        con.execute("BEGIN IMMEDIATE")
        cur = con.execute("INSERT OR IGNORE INTO idem_keys (key) VALUES (?)", (key,))
        if cur.rowcount == 1:
            con.execute("INSERT INTO processed_requests (idem_key) VALUES (?)", (key,))  # islendi
            con.commit(); return True
        con.commit(); return False  # zaten islenmis (409)
    return retry(f)

def op_payment_attempt(con, cid):
    # FraudCheck ATOMIK velocity sayaci (rate limiter karsiligi): sayaci atomik artir.
    # Eski cache-based oku-sil-yaz LOST-UPDATE'liydi; atomik UPDATE ile eszamanli denemeler DOGRU sayilir.
    def f():
        con.execute("BEGIN IMMEDIATE")
        con.execute("INSERT INTO payment_attempts (customer_id, attempts) VALUES (?, 1) "
                    "ON CONFLICT(customer_id) DO UPDATE SET attempts = attempts + 1", (cid,))
        con.commit(); return True
    return retry(f)

def op_expire_random(con):
    # Expiry job'un GERCEK-DB karsiligi: rastgele bir Active rezervasyonu Expired yap + reserved serbest birak.
    # confirm/release ile ATOMIK yarisir (TryTransition Active->X yalniz biri kazanir). Bug: expire kazanip
    # sonra odeme confirm denerse stok yeniden guvenceye alinmali (I2 tutarliligini test eder).
    def f():
        con.execute("BEGIN IMMEDIATE")
        row = con.execute("SELECT id,product_id,size,qty FROM reservations WHERE status='Active' ORDER BY RANDOM() LIMIT 1").fetchone()
        if not row:
            con.commit(); return False
        rid, pid, sz, qty = row
        cur = con.execute("UPDATE reservations SET status='Expired' WHERE id=? AND status='Active'", (rid,))
        if cur.rowcount == 1:
            con.execute("UPDATE product_stocks SET reserved_quantity=reserved_quantity-? WHERE product_id=? AND size=? AND reserved_quantity>=?", (qty, pid, sz, qty))
            con.commit(); return True
        con.commit(); return False
    return retry(f)

# ---- Worker: her thread rastgele islemler yapar ----
stats = {"reserve_ok": 0, "reserve_fail": 0, "confirm": 0, "release": 0, "expired": 0,
         "credit_redeem_ok": 0, "credit_redeem_fail": 0, "loyalty_ok": 0, "loyalty_fail": 0,
         "order_placed": 0, "order_dup": 0, "vote": 0, "vote_dup": 0, "cart_add": 0, "cart_merge": 0, "outbox_claimed": 0, "payment_attempt": 0, "idem_processed": 0}
stats_lock = threading.Lock()
my_reservations = []  # (resv_id, pid, sz, qty) - thread-local aktif rezervasyonlar
resv_lock = threading.Lock()

OPS_PER_THREAD = 400
N_THREADS = 8

def worker(tid):
    con = connect()
    rng = random.Random(tid * 7919)
    local = {k: 0 for k in stats}
    for _ in range(OPS_PER_THREAD):
        act = rng.random()
        try:
            if act < 0.30:
                # rezerve et - %45 SICAK urun (5 stok, tek beden) -> asiri overselling baskisi
                if rng.random() < 0.45:
                    pid = 999; sz = "M"; qty = rng.randint(1, 2)
                else:
                    pid = rng.randint(1, N_PRODUCTS); sz = rng.choice(SIZES); qty = rng.randint(1, 3)
                if op_reserve(con, pid, sz, qty):
                    local["reserve_ok"] += 1
                    rid = con.execute("SELECT last_insert_rowid()").fetchone()[0]
                    with resv_lock: my_reservations.append((rid, pid, sz, qty))
                else:
                    local["reserve_fail"] += 1
            elif act < 0.45:
                # confirm (varsa)
                with resv_lock:
                    r = my_reservations.pop() if my_reservations else None
                if r:
                    if op_confirm(con, *r): local["confirm"] += 1
            elif act < 0.52:
                # release (varsa)
                with resv_lock:
                    r = my_reservations.pop(0) if my_reservations else None
                if r:
                    if op_release(con, *r): local["release"] += 1
            elif act < 0.58:
                # EXPIRE (expiry job simulasyonu) - rastgele Active rezervasyonu expire et; confirm ile YARISIR
                if op_expire_random(con): local["expired"] += 1
            elif act < 0.68:
                # kredi harca - %45 SICAK musteri (50 kredi) -> asiri double-spend baskisi
                cid = 99 if rng.random() < 0.45 else rng.randint(1, N_CUSTOMERS)
                amt = round(rng.uniform(10, 150), 2)
                if op_redeem_credit(con, cid, amt): local["credit_redeem_ok"] += 1
                else: local["credit_redeem_fail"] += 1
            elif act < 0.73:
                cid = rng.randint(1, N_CUSTOMERS); amt = round(rng.uniform(10, 100), 2)
                op_add_credit(con, cid, amt)
            elif act < 0.83:
                # puan harca - %45 SICAK musteri (200 puan) -> asiri double-spend baskisi
                cid = 99 if rng.random() < 0.45 else rng.randint(1, N_CUSTOMERS)
                pts = rng.randint(100, 800)
                if op_redeem_loyalty(con, cid, pts): local["loyalty_ok"] += 1
                else: local["loyalty_fail"] += 1
            elif act < 0.90:
                # cift-siparis idempotency: dar bir request_id havuzu (cakisma zorla)
                cid = rng.randint(1, N_CUSTOMERS); req = f"REQ-{rng.randint(1, 50)}"
                res = op_place_order(con, cid, req)
                local["order_placed" if res == "placed" else "order_dup"] += 1
            elif act < 0.94:
                # cift-oy: dar musteri havuzu (ayni review+customer cakismasi zorla)
                rid = rng.randint(1, 5); cid = rng.randint(1, N_CUSTOMERS)
                res = op_vote(con, rid, cid)
                local["vote" if res == "voted" else "vote_dup"] += 1
            elif act < 0.96:
                # OUTBOX CLAIM - cok thread ayni pending mesajlari claim etmeye calisir (cift-teslim testi)
                if op_outbox_claim(con): local["outbox_claimed"] += 1
            elif act < 0.975:
                # FRAUD velocity sayaci - SICAK musteri 99'a cok thread ayni anda deneme (lost-update testi)
                if op_payment_attempt(con, 99): local["payment_attempt"] += 1
            elif act < 0.99:
                # IDEMPOTENCY - dar key havuzu (10 key) -> cok thread ayni key'i islemeye calisir (cift-islem testi)
                key = f"IDEM-{rng.randint(1, 10)}"
                if op_idem_process(con, key): local["idem_processed"] += 1
            else:
                # sepet cift-kalem
                cart = rng.randint(1, N_CUSTOMERS); pid = rng.randint(1, N_PRODUCTS); sz = rng.choice(SIZES)
                res = op_cart_add(con, cart, pid, sz)
                local["cart_add" if res == "added" else "cart_merge"] += 1
        except Exception as e:
            print(f"  [thread {tid}] HATA: {e}")
    con.close()
    with stats_lock:
        for k, v in local.items(): stats[k] += v

# ---- Thread'leri baslat ----
print("=" * 64)
print(f"GERCEK-ESZAMANLILIK STRES SIMULASYONU")
print(f"  {N_THREADS} paralel OS thread x {OPS_PER_THREAD} islem = {N_THREADS*OPS_PER_THREAD} eszamanli DB islemi")
print(f"  GERCEK SQLite (WAL) + BEGIN IMMEDIATE + busy_timeout + kilit cekismesi")
print("=" * 64)
t0 = time.time()
threads = [threading.Thread(target=worker, args=(i,)) for i in range(N_THREADS)]
for t in threads: t.start()
for t in threads: t.join()
elapsed = time.time() - t0
print(f"\n{N_THREADS*OPS_PER_THREAD} islem {elapsed:.2f}s'de tamamlandi ({int(N_THREADS*OPS_PER_THREAD/elapsed)} islem/s)")
print("\nIslem istatistikleri:")
for k, v in stats.items(): print(f"  {k}: {v}")

# ---- GLOBAL INVARIANT DOGRULAMA ----
print("\n" + "=" * 64)
print("INVARIANT DOGRULAMA (gercek paralellik sonrasi tutarlilik)")
print("=" * 64)
con = connect()
passed = failed = 0
def check(name, cond, detail=""):
    global passed, failed
    if cond: passed += 1; print(f"  \u2713 {name}")
    else: failed += 1; print(f"  \u2717 {name}  [{detail}]")

# I1: hicbir urun-beden OVERSOLD degil (reserved <= stock, ikisi de >=0)
rows = con.execute("SELECT product_id,size,stock_quantity,reserved_quantity FROM product_stocks").fetchall()
oversold = [r for r in rows if r[3] > r[2] or r[2] < 0 or r[3] < 0]
check("I1: hicbir urun-beden OVERSOLD/negatif degil (reserved<=stock, ikisi>=0)", not oversold, str(oversold[:3]))

# I2: reserved_quantity == acik (Active) rezervasyon toplami (defter==sayac)
mismatch = []
for pid, sz, stock, reserved in rows:
    active_sum = con.execute("SELECT COALESCE(SUM(qty),0) FROM reservations WHERE product_id=? AND size=? AND status='Active'", (pid, sz)).fetchone()[0]
    if active_sum != reserved:
        mismatch.append((pid, sz, reserved, active_sum))
check("I2: reserved_quantity == acik rezervasyon defteri toplami (her urun-beden)", not mismatch, str(mismatch[:3]))

# I3: hicbir musteri NEGATIF store_credit / loyalty degil
neg = con.execute("SELECT id,store_credit,loyalty_points FROM customers WHERE store_credit<0 OR loyalty_points<0").fetchall()
check("I3: hicbir musteri negatif store_credit/loyalty degil (CAS tuttu)", not neg, str(neg[:3]))

# I4: store_credit == ledger toplami (para korunumu - double-spend/lost-update yok) - SICAK musteri (99) DAHIL
ledger_mismatch = []
all_customer_ids = list(range(1, N_CUSTOMERS + 1)) + [99]
for cid in all_customer_ids:
    bal = con.execute("SELECT store_credit FROM customers WHERE id=?", (cid,)).fetchone()[0]
    led = con.execute("SELECT COALESCE(SUM(amount),0) FROM credit_ledger WHERE customer_id=?", (cid,)).fetchone()[0]
    if abs(bal - led) > 0.001:
        ledger_mismatch.append((cid, round(bal, 2), round(led, 2)))
check("I4: store_credit == kredi defteri toplami (para korunumu, lost-update yok) [sicak musteri dahil]", not ledger_mismatch, str(ledger_mismatch[:3]))

# I5: her request_id icin EN FAZLA 1 siparis (idempotency race tuttu)
dup_orders = con.execute("SELECT request_id,COUNT(*) c FROM orders WHERE request_id IS NOT NULL GROUP BY request_id HAVING c>1").fetchall()
check("I5: her request_id icin en fazla 1 siparis (cift-siparis race yok)", not dup_orders, str(dup_orders[:3]))

# I6: her review helpful_count == gercek oy kaydi sayisi (drift yok)
vote_mismatch = []
for rid in range(1, 6):
    hc = con.execute("SELECT helpful_count FROM reviews WHERE id=?", (rid,)).fetchone()[0]
    vc = con.execute("SELECT COUNT(*) FROM votes WHERE review_id=?", (rid,)).fetchone()[0]
    if hc != vc:
        vote_mismatch.append((rid, hc, vc))
check("I6: helpful_count == gercek oy sayisi (sayac drift yok, cift-artis yok)", not vote_mismatch, str(vote_mismatch[:3]))

# I7: hicbir (review,customer) icin >1 oy (unique index tuttu)
dup_votes = con.execute("SELECT review_id,customer_id,COUNT(*) c FROM votes GROUP BY review_id,customer_id HAVING c>1").fetchall()
check("I7: hicbir (review,customer) icin cift oy yok (unique index)", not dup_votes, str(dup_votes[:3]))

# I8: her (cart,product,size) icin EN FAZLA 1 aktif kalem (cift-sepet-kalemi yok)
dup_cart = con.execute("SELECT cart_id,product_id,size,COUNT(*) c FROM cart_items WHERE is_active=1 GROUP BY cart_id,product_id,size HAVING c>1").fetchall()
check("I8: her (cart,product,size) icin en fazla 1 aktif kalem (cift-kalem race yok)", not dup_cart, str(dup_cart[:3]))

# I9: confirm edilen rezervasyonlar stok_quantity'yi dogru dusurdu (baslangic - kalan == confirmed toplam)
# NOT: normal urunler INIT_STOCK=100, sicak urun 999 ise 5 -> her urun-beden icin ayri hesapla
confirmed_by_ps = {}
for r in con.execute("SELECT product_id,size,COALESCE(SUM(qty),0) FROM reservations WHERE status='Confirmed' GROUP BY product_id,size").fetchall():
    confirmed_by_ps[(r[0], r[1])] = r[2]
i9_bad = []
for pid, sz, stock, reserved in rows:
    init = 5 if pid == 999 else INIT_STOCK
    consumed = init - stock
    conf = confirmed_by_ps.get((pid, sz), 0)
    if consumed != conf:
        i9_bad.append((pid, sz, consumed, conf))
check("I9: her urun-beden: tuketilen stok == confirmed rezervasyon (confirm dogru dustu)", not i9_bad, str(i9_bad[:3]))

# I10: SICAK URUN (999, 5 stok) hicbir zaman OVERSOLD olmadi - confirmed + aktif-reserved <= 5 (asiri cekisme altinda)
hot = con.execute("SELECT stock_quantity,reserved_quantity FROM product_stocks WHERE product_id=999 AND size='M'").fetchone()
hot_confirmed = confirmed_by_ps.get((999, "M"), 0)
hot_committed = hot_confirmed + hot[1]  # dusulen + hala rezerve
check("I10: SICAK urun (5 stok) OVERSOLD degil - confirmed+reserved <= 5 (asiri cekisme)", hot_committed <= 5 and hot[0] >= 0 and hot[1] >= 0, f"confirmed={hot_confirmed} reserved={hot[1]} stock={hot[0]}")

# I11: SICAK MUSTERI (99, 50 kredi) NEGATIF olmadi + para korundu (asiri double-spend baskisi altinda)
hot_c = con.execute("SELECT store_credit,loyalty_points FROM customers WHERE id=99").fetchone()
check("I11: SICAK musteri (50 kredi/200 puan) negatif degil (double-spend baskisi altinda CAS tuttu)", hot_c[0] >= 0 and hot_c[1] >= 0, f"credit={hot_c[0]} points={hot_c[1]}")

# I12: her outbox mesaji EN FAZLA 1 kez teslim edildi (atomik claim -> cift teslim yok, gercek paralellik)
dup_deliv = con.execute("SELECT outbox_id,COUNT(*) c FROM deliveries GROUP BY outbox_id HAVING c>1").fetchall()
total_deliv = con.execute("SELECT COUNT(*) FROM deliveries").fetchone()[0]
distinct_deliv = con.execute("SELECT COUNT(DISTINCT outbox_id) FROM deliveries").fetchone()[0]
check("I12: her outbox mesaji <=1 kez teslim (atomik claim - cift teslim YOK, gercek paralellik)", not dup_deliv and total_deliv==distinct_deliv, f"dup={dup_deliv[:3]} total={total_deliv} distinct={distinct_deliv}")

# I13: fraud velocity sayaci == GERCEK deneme sayisi (atomik artis -> lost-update YOK, velocity limiti bypass edilemez)
attempt_counter = con.execute("SELECT attempts FROM payment_attempts WHERE customer_id=99").fetchone()
counter_val = attempt_counter[0] if attempt_counter else 0
actual_attempts = stats["payment_attempt"]
check("I13: fraud sayaci == gercek deneme sayisi (lost-update YOK, velocity bypass edilemez)", counter_val==actual_attempts, f"counter={counter_val} actual={actual_attempts}")

# I14: her idempotency-key EN FAZLA 1 kez islendi (atomik SETNX -> eszamanli ayni-key cift-islem YOK)
dup_idem = con.execute("SELECT idem_key,COUNT(*) c FROM processed_requests GROUP BY idem_key HAVING c>1").fetchall()
total_proc = con.execute("SELECT COUNT(*) FROM processed_requests").fetchone()[0]
distinct_keys = con.execute("SELECT COUNT(DISTINCT idem_key) FROM processed_requests").fetchone()[0]
check("I14: her idempotency-key <=1 kez islendi (atomik SETNX - cift-islem YOK, gercek paralellik)", not dup_idem and total_proc==distinct_keys, f"dup={dup_idem[:3]} total={total_proc} distinct={distinct_keys}")

con.close()
print("\n" + "=" * 64)
print(f"STRES SIMULASYON SONUCU:  {passed} gecti, {failed} basarisiz  (toplam {passed+failed})")
print("=" * 64)
sys.exit(1 if failed else 0)
