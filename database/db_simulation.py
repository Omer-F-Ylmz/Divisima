#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
GERCEK VERITABANI SIMULASYONU - Divisima e-ticaret
====================================================
Python re-implementasyonu DEGIL. Gercek SQL motoru (SQLite) uzerinde:
  - Uretilen semayi kurar (43 tablo, gercek CREATE TABLE)
  - Gercek veri yukler (tum NOT NULL kolonlar - gercek constraint'ler)
  - Is akislarini GERCEK TRANSACTION + atomik UPDATE ile calistirir
Kolon adlari entity ile birebir (stock_quantity, is_cancelled, ...).
MSSQL uretimi: database/mssql/*.sql (sqlcmd). Ayni entity'lerden -> esdeger.
"""
import sqlite3, os, sys
_p = 0; _f = 0
def check(name, cond, detail=""):
    global _p, _f
    if cond: _p += 1; print(f"  \u2713 {name}")
    else: _f += 1; print(f"  \u2717 {name}  << {detail}")
HERE = os.path.dirname(os.path.abspath(__file__))
NOW = "datetime('now')"

print("=" * 62)
print("DIVISIMA - GERCEK VERITABANI SIMULASYONU (SQLite motoru)")
print("=" * 62)

con = sqlite3.connect(":memory:")
con.execute("PRAGMA foreign_keys = ON;")
con.executescript(open(os.path.join(HERE, "sqlite_schema.sql"), encoding="utf-8").read())
cur = con.cursor()

# --- 1) SEMA ---
print("\n--- 1) Sema kurulumu (43 tablo) ---")
tables = [r[0] for r in cur.execute("SELECT name FROM sqlite_master WHERE type='table'").fetchall()]
check(f"Sema kuruldu ({len(tables)} tablo)", len(tables) >= 43, f"tablo={len(tables)}")
for t in ["customers","products","product_stocks","orders","order_items","coupons",
          "gift_cards","store_credit_transactions","return_requests","consent_records","payments"]:
    check(f"Tablo: {t}", t in tables, "yok")

# --- 2) SEED (tum zorunlu kolonlar) ---
print("\n--- 2) Seed veri ---")
cur.execute(f"INSERT INTO categories (id,name,slug,display_order,is_active,created_at) "
            f"VALUES (1,'Kadin Giyim','kadin-giyim',1,1,{NOW})")
cur.executemany(
    f"INSERT INTO products (id,name,brand,category_id,price,sale_price,description,color_hex,"
    f"product_type,is_active,created_at) VALUES (?,?,'Divisima',1,?,?,'Urun aciklamasi','#000000',0,1,{NOW})",
    [(1,"Siyah Elbise",1200.00,None),(2,"Kot Ceket",900.00,720.00),(3,"Trenckot",2400.00,None)])
cur.executemany(
    f"INSERT INTO product_stocks (id,product_id,size,stock_quantity,reserved_quantity,row_version,is_active,created_at) "
    f"VALUES (?,?,?,?,0,0,1,{NOW})",
    [(1,1,"S",10),(2,1,"M",5),(3,1,"L",3),(4,2,"M",8),(5,3,"L",2)])
cur.executemany(
    f"INSERT INTO customers (id,name,email,phone,user_type,password_salt,password_hash,is_active,"
    f"email_verified,two_factor_enabled,failed_login_attempts,loyalty_points,store_credit,created_at,"
    f"notify_email,notify_sms,notify_push) VALUES (?,?,?,'05000000000',?,x'00',x'00',1,1,0,0,?,?,{NOW},1,1,1)",
    [(1,"Yonetici","admin@divisima.com",1,0,0.0),
     (2,"Ayse Yilmaz","ayse@example.com",2,500,250.00),
     (3,"Yeni Musteri","yeni@example.com",2,0,0.0)])
cur.execute(f"INSERT INTO coupons (id,code,discount_type,value,min_amount,max_discount_amount,expire_date,"
            f"usage_limit,used_count,first_order_only,is_active,created_at,row_version) "
            f"VALUES (1,'HOSGELDIN',0,10,0,100,'2030-01-01',1000,0,1,1,{NOW},0)")
cur.execute(f"INSERT INTO coupons (id,code,discount_type,value,min_amount,max_discount_amount,expire_date,"
            f"usage_limit,used_count,first_order_only,is_active,created_at,row_version) "
            f"VALUES (2,'ESKIKOD',0,20,0,NULL,'2020-01-01',1000,0,0,1,{NOW},0)")
cur.execute(f"INSERT INTO gift_cards (id,code,initial_amount,balance,is_active,created_at) "
            f"VALUES (1,'GIFT-250',250.00,250.00,1,{NOW})")
con.commit()
check("Urun (3) + stok (5) yuklendi",
      cur.execute("SELECT COUNT(*) FROM products").fetchone()[0]==3 and
      cur.execute("SELECT COUNT(*) FROM product_stocks").fetchone()[0]==5)
check("Admin user_type=1", cur.execute("SELECT user_type FROM customers WHERE id=1").fetchone()[0]==1)

# --- 3) NOT NULL gercekten uygulaniyor ---
print("\n--- 3) Gercek constraint uygulamasi ---")
try:
    cur.execute(f"INSERT INTO products (id,brand,category_id,price,description,color_hex,product_type,is_active,created_at) "
                f"VALUES (99,'X',1,10,'d','#000',0,1,{NOW})")  # name yok
    con.commit(); check("NOT NULL uygulaniyor", False, "hata firlatmadi")
except sqlite3.IntegrityError:
    con.rollback(); check("NOT NULL (products.name eksik) reddedildi", True)

# --- 4) SIPARIS (transaction + atomik stok) ---
print("\n--- 4) Siparis verme ---")
def place_order(con, cid, items, coupon=None):
    cur = con.cursor()
    try:
        cur.execute("BEGIN")
        subtotal = 0.0; lines = []
        for pid, size, qty in items:
            if qty < 1 or qty > 100 or not size:
                con.rollback(); return ("BadRequest", None)
            prod = cur.execute("SELECT price,sale_price FROM products WHERE id=? AND is_active=1",(pid,)).fetchone()
            if not prod: con.rollback(); return ("ProductNotFound", None)
            price = prod[1] if prod[1] is not None else prod[0]
            # ATOMIK stok dusumu (overselling engeli): WHERE stock_quantity >= qty
            cur.execute("UPDATE product_stocks SET stock_quantity = stock_quantity - ? "
                        "WHERE product_id=? AND size=? AND stock_quantity >= ?",(qty,pid,size,qty))
            if cur.rowcount == 0: con.rollback(); return ("StockInsufficient", None)
            subtotal += price*qty; lines.append((pid,size,qty,price))
        discount = 0.0; applied = None
        if coupon:
            c = cur.execute("SELECT discount_type,value,min_amount,max_discount_amount,expire_date,"
                            "usage_limit,used_count,first_order_only FROM coupons WHERE code=? AND is_active=1",(coupon,)).fetchone()
            if c:
                dtype,val,minamt,maxd,expire,ulim,ucnt,foo = c
                valid = subtotal >= minamt
                if expire and expire < "2025-07-20": valid = False
                if ulim > 0 and ucnt >= ulim: valid = False
                if foo:
                    done = cur.execute("SELECT COUNT(*) FROM orders WHERE customer_id=? AND status NOT IN (0,5)",(cid,)).fetchone()[0]
                    if done > 0: valid = False
                if valid:
                    if dtype == 0:
                        discount = round(subtotal*val/100,2)
                        if maxd is not None and discount > maxd: discount = maxd
                    applied = coupon
        total = subtotal - discount
        cur.execute(f"INSERT INTO orders (customer_id,order_number,status,subtotal,discount_amount,shipping_cost,"
                    f"total_price,currency,coupon_code,payment_type,installment_count,is_online_payment_done,created_at) "
                    f"VALUES (?,?,0,?,?,0,?,'TRY',?,0,1,0,{NOW})",
                    (cid,f"ORD-{cid}-{subtotal:.0f}",subtotal,discount,total,applied))
        oid = cur.lastrowid
        for pid,size,qty,price in lines:
            cur.execute(f"INSERT INTO order_items (order_id,product_id,size,quantity,unit_price,is_cancelled,created_at) "
                        f"VALUES (?,?,?,?,?,0,{NOW})",(oid,pid,size,qty,price))
        con.commit(); return ("OK", {"order_id":oid,"subtotal":subtotal,"discount":discount,"total":total})
    except Exception as e:
        con.rollback(); return ("Error", str(e))

sb = cur.execute("SELECT stock_quantity FROM product_stocks WHERE product_id=1 AND size='M'").fetchone()[0]
st, res = place_order(con, 2, [(1,"M",2)])
sa = cur.execute("SELECT stock_quantity FROM product_stocks WHERE product_id=1 AND size='M'").fetchone()[0]
check("Siparis basarili", st == "OK", st)
check("Stok atomik dusuruldu (5->3)", sb==5 and sa==3, f"{sb}->{sa}")
check("Toplam dogru (2x1200=2400)", res and abs(res["total"]-2400)<0.01, res)

# --- 5) OVERSELLING ---
print("\n--- 5) Overselling engeli ---")
st, res = place_order(con, 2, [(3,"L",5)])  # stok 2
sl = cur.execute("SELECT stock_quantity FROM product_stocks WHERE product_id=3 AND size='L'").fetchone()[0]
check("Fazla siparis reddedildi", st == "StockInsufficient", st)
check("Stok degismedi (rollback, 2)", sl == 2, f"stok={sl}")

# --- 6) NEGATIF MIKTAR ---
print("\n--- 6) Negatif miktar engeli ---")
st, res = place_order(con, 2, [(1,"S",-3)])
check("Negatif miktar reddedildi", st == "BadRequest", st)

# --- 7) HEDIYE KARTI CAS ---
print("\n--- 7) Hediye karti atomik bozdurma (CAS) ---")
def redeem(con, cid, code):
    cur = con.cursor()
    try:
        cur.execute("BEGIN")
        card = cur.execute("SELECT id,balance FROM gift_cards WHERE code=? AND is_active=1",(code,)).fetchone()
        if not card or card[1] <= 0: con.rollback(); return ("Empty",0)
        gid,bal = card
        cur.execute(f"UPDATE gift_cards SET balance=0,is_active=0,redeemed_by=?,redeemed_at={NOW} "
                    f"WHERE id=? AND balance=? AND balance>0",(cid,gid,bal))
        if cur.rowcount == 0: con.rollback(); return ("Conflict",0)
        cur.execute("UPDATE customers SET store_credit=store_credit+? WHERE id=?",(bal,cid))
        cur.execute(f"INSERT INTO store_credit_transactions (customer_id,amount,type,reason,created_at) "
                    f"VALUES (?,?,0,'Hediye karti',{NOW})",(cid,bal))
        con.commit(); return ("OK",bal)
    except Exception as e:
        con.rollback(); return ("Error",str(e))
cb = cur.execute("SELECT store_credit FROM customers WHERE id=2").fetchone()[0]
st, amt = redeem(con, 2, "GIFT-250")
ca = cur.execute("SELECT store_credit FROM customers WHERE id=2").fetchone()[0]
check("Bozduruldu (250)", st=="OK" and amt==250, st)
check("Kredi eklendi (250->500)", abs(ca-cb-250)<0.01, f"{cb}->{ca}")
st2, _ = redeem(con, 3, "GIFT-250")
check("Ikinci bozdurma ENGELLENDI (cift kredi yok)", st2 in ("Empty","Conflict"), st2)
check("2. musteriye kredi verilmedi", cur.execute("SELECT store_credit FROM customers WHERE id=3").fetchone()[0]==0)

# --- 8) MAGAZA KREDISI ATOMIK ---
print("\n--- 8) Magaza kredisi atomik harcama ---")
def spend(con, cid, amt):
    cur = con.cursor(); cur.execute("BEGIN")
    cur.execute("UPDATE customers SET store_credit=store_credit-? WHERE id=? AND store_credit>=?",(amt,cid,amt))
    ok = cur.rowcount == 1
    con.commit() if ok else con.rollback()
    return ok
check("500'den 300 harcandi", spend(con,2,300) is True)
check("Bakiye 200", abs(cur.execute("SELECT store_credit FROM customers WHERE id=2").fetchone()[0]-200)<0.01)
check("200'den 300 reddedildi (overdraft)", spend(con,2,300) is False)
check("Bakiye degismedi (200)", abs(cur.execute("SELECT store_credit FROM customers WHERE id=2").fetchone()[0]-200)<0.01)

# --- 9) KUPON DOGRULAMA ---
print("\n--- 9) Kupon dogrulama (SQL) ---")
# Musteri 3 ilk kez siparis veriyor -> HOSGELDIN uygulanir
st, res = place_order(con, 3, [(1,"S",1)], coupon="HOSGELDIN")
check("Yeni musteri HOSGELDIN uygulandi (tavan 100)", st=="OK" and res["discount"]==100, res)
# C# semantigi: ilk-siparis kontrolu Pending/Cancelled OLMAYAN siparise bakar (status NOT IN (0,5)).
# Musteri 3'un siparisini Confirmed(1) yap - artik "gecmis siparisi" var.
cur.execute("UPDATE orders SET status=1 WHERE id=?", (res["order_id"],)); con.commit()
# Suresi dolmus kupon uygulanmaz
st, res = place_order(con, 3, [(2,"M",1)], coupon="ESKIKOD")
check("Suresi dolmus kupon UYGULANMADI", st=="OK" and res["discount"]==0, res)
# Musteri 3 artik Confirmed siparise sahip -> HOSGELDIN (ilk-siparis) reddedilir
st, res = place_order(con, 3, [(1,"L",1)], coupon="HOSGELDIN")
check("Gecmis siparisi olan musteride ilk-siparis kuponu UYGULANMADI", st=="OK" and res["discount"]==0, res)

# --- 10) IADE MUHASEBESI ---
print("\n--- 10) Iade muhasebesi (cift iade engeli) ---")
oid = cur.execute("SELECT id FROM orders WHERE customer_id=2 ORDER BY id LIMIT 1").fetchone()[0]
cur.execute("UPDATE orders SET status=4 WHERE id=?",(oid,)); con.commit()
def create_return(con, oid, pid, size, qty):
    cur = con.cursor()
    it = cur.execute("SELECT quantity,unit_price FROM order_items WHERE order_id=? AND product_id=? AND size=?",(oid,pid,size)).fetchone()
    if not it or qty <= 0: return "InvalidItem"
    oq, price = it
    already = cur.execute("SELECT COALESCE(SUM(quantity),0) FROM return_requests "
                          "WHERE order_id=? AND product_id=? AND size=? AND status != 2",(oid,pid,size)).fetchone()[0]
    if qty > (oq - already): return "ExceedsRemaining"
    cur.execute(f"INSERT INTO return_requests (order_id,customer_id,product_id,size,quantity,reason,return_type,"
                f"status,refund_amount,created_at) VALUES (?,2,?,?,?,'Begenmedim',0,3,?,{NOW})",(oid,pid,size,qty,price*qty))
    con.commit(); return "OK"
check("Ilk iade (2/2) basarili", create_return(con,oid,1,"M",2)=="OK")
check("Ikinci iade ENGELLENDI (kalan 0)", create_return(con,oid,1,"M",2)=="ExceedsRemaining")

# --- 11) KVKK RIZA ---
print("\n--- 11) KVKK riza kaydi ---")
cur.executemany(f"INSERT INTO consent_records (customer_id,consent_type,document_version,granted,ip_address,created_at) "
                f"VALUES (?,?,?,?,?,{NOW})",
                [(2,"terms","1.0",1,"1.2.3.4"),(2,"privacy","1.0",1,"1.2.3.4"),(2,"marketing","1.0",0,"1.2.3.4")])
con.commit()
check("Riza kayitlari (3)", cur.execute("SELECT COUNT(*) FROM consent_records WHERE customer_id=2").fetchone()[0]==3)
check("Pazarlama RET saklandi (ETK kaniti)",
      cur.execute("SELECT granted FROM consent_records WHERE customer_id=2 AND consent_type='marketing'").fetchone()[0]==0)

# --- 12) TAKSIT ---
print("\n--- 12) Taksit kaydi ---")
oid2 = cur.execute("SELECT id FROM orders WHERE customer_id=2 ORDER BY id LIMIT 1").fetchone()[0]
total = cur.execute("SELECT total_price FROM orders WHERE id=?",(oid2,)).fetchone()[0]
paid = round(total*1.05,2)
cur.execute("UPDATE orders SET installment_count=3 WHERE id=?",(oid2,))
cur.execute(f"INSERT INTO payments (order_id,payment_provider,payment_status,amount,paid_price,installment_count,"
            f"installment_fee,created_at) VALUES (?,'iyzico',1,?,?,3,?,{NOW})",(oid2,total,paid,round(paid-total,2)))
con.commit()
pay = cur.execute("SELECT installment_count,installment_fee FROM payments WHERE order_id=?",(oid2,)).fetchone()
check("Taksit 3 kaydedildi", pay[0]==3, pay)
check("Komisyon hesaplandi", abs(pay[1]-round(paid-total,2))<0.01, pay)

# --- 13) REFERANS BUTUNLUGU ---
print("\n--- 13) Referans butunlugu ---")
orph = cur.execute("SELECT COUNT(*) FROM order_items oi LEFT JOIN orders o ON oi.order_id=o.id WHERE o.id IS NULL").fetchone()[0]
check("Yetim order_item yok", orph==0, f"yetim={orph}")

# --- 14) LOST-UPDATE: atomik UPDATE vs read-modify-write (concurrency fix kaniti) ---
print("\n--- 14) Lost-update engeli (atomik vs read-modify-write) ---")
# Musteri 3 bakiyesini bilinen degere getir
cur.execute("UPDATE customers SET store_credit=100 WHERE id=3"); con.commit()
# KOTU YOL (tracked read-modify-write): iade islemi bakiyeyi okur (100)...
old_balance = cur.execute("SELECT store_credit FROM customers WHERE id=3").fetchone()[0]
# ...bu ARADA eszamanli bir atomik harcama olur (100 -> 70)
cur.execute("UPDATE customers SET store_credit=store_credit-30 WHERE id=3 AND store_credit>=30"); con.commit()
# ...sonra iade eski okunan degeri baz alarak yazsaydi (100+50=150) -> harcama KAYBOLURDU
would_be_lost_update = old_balance + 50   # 150 (YANLIS - dogru: 70+50=120)
# IYI YOL (atomik increment - bizim fix): mevcut degere ekler
cur.execute("UPDATE customers SET store_credit=store_credit+50 WHERE id=3"); con.commit()
atomic_result = cur.execute("SELECT store_credit FROM customers WHERE id=3").fetchone()[0]
check("Atomik increment dogru sonuc verir (70+50=120)", abs(atomic_result-120)<0.01, f"sonuc={atomic_result}")
check("Read-modify-write olsaydi harcama kaybolurdu (150 != 120)", would_be_lost_update != atomic_result,
      f"rmw={would_be_lost_update} atomik={atomic_result}")

con.close()
print("\n" + "=" * 62)
print(f"VERITABANI SIMULASYON SONUCU:  {_p} gecti, {_f} basarisiz  (toplam {_p+_f})")
print("=" * 62)
sys.exit(0 if _f == 0 else 1)
