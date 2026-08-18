#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Divisima entity siniflarindan MSSQL + SQLite semasi uretir (43 tablo)."""
import re, glob, os

ENTITY_DIR = 'Divisima.Entity/Entities'

# --- C# tip -> (MSSQL, SQLite) ---
def map_type(csharp_type, name, nullable):
    t = csharp_type.replace('?','').strip()
    long_field = any(k in name.lower() for k in ['description','body','content','token','url','answer','comment','question','message','html','json','notes','payload'])
    if t == 'int':      return ('INT', 'INTEGER')
    if t == 'long':     return ('BIGINT', 'INTEGER')
    if t == 'byte':     return ('TINYINT', 'INTEGER')
    if t == 'short':    return ('SMALLINT','INTEGER')
    if t == 'decimal':  return ('DECIMAL(18,2)', 'NUMERIC')
    if t in ('double','float'): return ('FLOAT','REAL')
    if t == 'bool':     return ('BIT', 'INTEGER')
    if t == 'DateTime': return ('DATETIME2', 'TEXT')
    if t == 'Guid':     return ('UNIQUEIDENTIFIER','TEXT')
    if t == 'byte[]':
        if name == 'row_version': return ('ROWVERSION', 'INTEGER')  # MSSQL otomatik; SQLite manuel
        return ('VARBINARY(MAX)', 'BLOB')
    if t == 'string':
        if long_field: return ('NVARCHAR(MAX)', 'TEXT')
        return ('NVARCHAR(256)', 'TEXT')
    return ('NVARCHAR(256)', 'TEXT')  # fallback (enum vs.)

def snake_plural(cls):
    s = re.sub(r'(?<!^)(?=[A-Z])','_',cls).lower()
    # basit pluralize
    if s.endswith('y') and s[-2] not in 'aeiou': return s[:-1]+'ies'
    if s.endswith(('s','x','z','ch','sh')): return s+'es'
    return s+'s'

DEFAULTS = {}  # (mssql_default, sqlite_default)
def parse_default(line):
    m = re.search(r'=\s*(.+?);', line)
    if not m: return None
    v = m.group(1).strip()
    if v in ('true','false'): return (('1' if v=='true' else '0'), ('1' if v=='true' else '0'))
    v_num = v.rstrip('mMfFdD')   # decimal/float suffix strip (0m, 0.0m, 1f)
    if re.match(r'^-?\d+(\.\d+)?$', v_num): return (v_num, v_num)
    if re.match(r'^"[^"]*"$', v):
        inner = v.strip('"')
        return (f"N'{inner}'", f"'{inner}'")
    return None

entities = {}
for f in sorted(glob.glob(f'{ENTITY_DIR}/*.cs')):
    src = open(f, encoding='utf-8').read()
    cm = re.search(r'public class (\w+)', src)
    if not cm: continue
    cls = cm.group(1)
    fields = []
    for line in src.splitlines():
        pm = re.match(r'\s*public\s+([\w<>\[\]?]+(?:<[^>]+>)?[?]?)\s+(\w+)\s*\{\s*get;\s*set;\s*\}(.*)$', line)
        if not pm: continue
        ctype, fname, rest = pm.group(1), pm.group(2), pm.group(3)
        nullable = ctype.endswith('?')  # C# non-nullable -> NOT NULL (sadakat)
        # enum tipleri (CustomerGenderEnum vb.) string map edilir -> TINYINT gibi davranmaz; entity'de zaten byte kullaniliyor
        default = parse_default(line)
        fields.append((fname, ctype, nullable, default))
    entities[cls] = (snake_plural(cls), fields)

# FK cikarimi: <name>_id -> <name>s tablosu (id) - guvenli, sadece bilinen tablolara
tables_by_singular = {}
for cls,(tbl,_) in entities.items():
    sing = re.sub(r'(?<!^)(?=[A-Z])','_',cls).lower()
    tables_by_singular[sing] = tbl

def resolve_fk(fname):
    if not fname.endswith('_id') or fname=='id': return None
    base = fname[:-3]
    # customer_id -> customers, order_id -> orders, product_id -> products
    if base in tables_by_singular: return tables_by_singular[base]
    # ozel eslemeler
    special = {'redeemed_by':'customers','order':'orders'}
    return None

def emit(dialect):  # 'mssql' | 'sqlite'
    idx = 0 if dialect=='mssql' else 1
    out = []
    out.append(f"-- Divisima e-ticaret veritabani ({dialect.upper()}) - {len(entities)} tablo")
    out.append(f"-- Entity siniflarindan otomatik uretildi. Kolon adlari entity ile birebir (snake_case).\n")
    if dialect=='mssql':
        out.append("-- CREATE DATABASE Divisima;\n-- GO\n-- USE Divisima;\n-- GO\n")
    # FK sirasi icin: bagimsiz tablolar once (basit topolojik degil; MSSQL FK'lari sonda ALTER ile ekle)
    for cls,(tbl,fields) in entities.items():
        out.append(f"CREATE TABLE {tbl} (")
        cols = []
        for fname, ctype, nullable, default in fields:
            mssql_t, sqlite_t = map_type(ctype, fname, nullable)
            coltype = mssql_t if dialect=='mssql' else sqlite_t
            if fname == 'id':
                if dialect=='mssql':
                    cols.append(f"    id INT IDENTITY(1,1) NOT NULL PRIMARY KEY")
                else:
                    cols.append(f"    id INTEGER PRIMARY KEY AUTOINCREMENT")
                continue
            if fname=='row_version' and dialect=='mssql':
                cols.append(f"    row_version ROWVERSION")  # otomatik
                continue
            null_sql = "NULL" if nullable else "NOT NULL"
            defclause = ""
            if default:
                defclause = f" DEFAULT {default[idx]}"
            cols.append(f"    {fname} {coltype} {null_sql}{defclause}")
        out.append(",\n".join(cols))
        out.append(");\n")
    # FK'lar (MSSQL: ALTER; SQLite: FK inline destekler ama basitlik icin atlanir/PRAGMA)
    if dialect=='mssql':
        out.append("-- === Foreign Key kisitlari (yetim kayit onleme) ===")
        for cls,(tbl,fields) in entities.items():
            for fname,ctype,nullable,default in fields:
                fk = resolve_fk(fname)
                if fk and fk!=tbl:
                    out.append(f"ALTER TABLE {tbl} ADD CONSTRAINT FK_{tbl}_{fname} "
                               f"FOREIGN KEY ({fname}) REFERENCES {fk}(id);")
        out.append("")
        out.append("-- === Sik sorgulanan kolonlarda index ===")
        for cls,(tbl,fields) in entities.items():
            for fname,_,_,_ in fields:
                if fname.endswith('_id') and fname!='id':
                    out.append(f"CREATE INDEX IX_{tbl}_{fname} ON {tbl}({fname});")
    return "\n".join(out)

os.makedirs('database/mssql', exist_ok=True)
open('database/mssql/01_schema.sql','w',encoding='utf-8').write(emit('mssql'))
open('database/sqlite_schema.sql','w',encoding='utf-8').write(emit('sqlite'))
print(f"✅ {len(entities)} tablo uretildi")
print(f"   MSSQL:  database/mssql/01_schema.sql")
print(f"   SQLite: database/sqlite_schema.sql (simulasyon icin)")
# ozet
fk_count = sum(1 for cls,(tbl,fields) in entities.items() for fn,_,_,_ in fields if resolve_fk(fn) and resolve_fk(fn)!=tbl)
print(f"   Tablolar: {', '.join(sorted(t for t,_ in entities.values()))[:200]}...")
print(f"   FK kisiti: {fk_count}")
