#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Divisima KAPSAMLI DENETIM - her dosyayi bilinen tum bug siniflari + yeni risk desenleri icin tarar.
Derleyici degildir; statik/desenle yakalanabilenler. dotnet build hala sart. Amac: en genis kapsamli sinyal.
"""
import re, glob, collections, sys

files = {f: open(f, encoding='utf-8', errors='replace').read()
         for f in glob.glob('**/*.cs', recursive=True)
         if '/bin/' not in f and '/obj/' not in f}

findings = collections.defaultdict(list)  # kategori -> [detay]

def add(cat, detail):
    findings[cat].append(detail)

# ============ 1) SYNC-OVER-ASYNC (deadlock riski): .Result / .Wait() ============
for f, s in files.items():
    for i, ln in enumerate(s.split('\n'), 1):
        # .Result veya .Wait() (await olmadan bloklamali cagri) - ASP.NET'te deadlock riski
        if re.search(r'\.Result\b(?!\s*[;,)])', ln) and 'await' not in ln and '//' not in ln.split('.Result')[0]:
            if re.search(r'\w+\.Result\b', ln) and 'httpContext' not in ln.lower():
                add('sync_over_async', f"{f.split('/')[-1]}:{i}  .Result: {ln.strip()[:70]}")
        if re.search(r'\.Wait\(\)', ln) and 'await' not in ln:
            add('sync_over_async', f"{f.split('/')[-1]}:{i}  .Wait(): {ln.strip()[:70]}")

# ============ 2) ASYNC VOID (fire-and-forget - istisna yakalanamaz) ============
for f, s in files.items():
    for m in re.finditer(r'(public|private|protected|internal)\s+async\s+void\s+(\w+)', s):
        if m.group(2) not in ('Main',):  # event handler olabilir ama Bussiness'ta olmamali
            add('async_void', f"{f.split('/')[-1]}  async void {m.group(2)}")

# ============ 3) BOS CATCH (hata sessizce yutuluyor - loglanmali) ============
for f, s in files.items():
    # catch { } veya catch (X) { } tamamen bos (yorum bile yok) - GERCEKTEN bos olanlar
    for m in re.finditer(r'catch\s*(\([^)]*\))?\s*\{\s*\}', s):
        # satir numarasi
        ln = s[:m.start()].count('\n') + 1
        add('empty_catch', f"{f.split('/')[-1]}:{ln}  tamamen bos catch (loglanmiyor)")

# ============ 4) SQL INJECTION (raw SQL / string concat) ============
for f, s in files.items():
    for i, ln in enumerate(s.split('\n'), 1):
        if re.search(r'FromSqlRaw|ExecuteSqlRaw', ln):
            add('sql_raw', f"{f.split('/')[-1]}:{i}  raw SQL: {ln.strip()[:70]}")
        # string interpolation ile SQL benzeri
        if re.search(r'"\s*(SELECT|INSERT|UPDATE|DELETE)\s.*\{', ln, re.I) and 'FromSql' not in ln:
            add('sql_concat', f"{f.split('/')[-1]}:{i}  SQL string-concat?: {ln.strip()[:60]}")

# ============ 5) HARDCODED SECRET (sifre/key kod icinde) ============
for f, s in files.items():
    for i, ln in enumerate(s.split('\n'), 1):
        if re.search(r'(password|secret|apikey|api_key|token)\s*=\s*"[A-Za-z0-9+/]{12,}"', ln, re.I):
            if 'Messages.' not in ln and 'HasColumnName' not in ln and 'nameof' not in ln:
                add('hardcoded_secret', f"{f.split('/')[-1]}:{i}  {ln.strip()[:60]}")

# ============ 6) CONTROLLER AUTHZ (attribute var mi) ============
for f, s in files.items():
    if not f.endswith('Controller.cs'):
        continue
    cls = re.search(r'class (\w+Controller)', s)
    if not cls:
        continue
    has_authz = bool(re.search(r'\[(Authorize|RequireUserType|AllowAnonymous)', s))
    if not has_authz:
        add('controller_no_authz', f"{f.split('/')[-1]}  ({cls.group(1)}) HIC authz attribute yok")

# ============ 7) IGNORED ATOMIC RESULT (0/false donerse basarisizlik) ============
critical = ['TryDecrementStoreCreditAsync', 'TryDecrementLoyaltyPointsAsync', 'TryTransitionAsync',
            'TryRedeemAsync', 'TryDirectDeductAsync', 'TryClaimAsync', 'TryConsumeAsync', 'TryAddAsync']
for f, s in files.items():
    for i, ln in enumerate(s.split('\n'), 1):
        for m in critical:
            if re.match(r'\s*await\s+_?\w+\.' + m + r'\(', ln) and '=' not in ln.split(m)[0]:
                add('ignored_atomic', f"{f.split('/')[-1]}:{i}  {m} sonuc yok sayiliyor")

# ============ 8) EF QUERY FILTER BYPASS (IgnoreQueryFilters) ============
for f, s in files.items():
    for i, ln in enumerate(s.split('\n'), 1):
        if 'IgnoreQueryFilters' in ln:
            add('query_filter_bypass', f"{f.split('/')[-1]}:{i}  IgnoreQueryFilters (soft-delete/is_active bypass?)")

# ============ 9) DateTime.Now vs UtcNow tutarlılığı (karisik kullanim) ============
now_count = sum(len(re.findall(r'DateTime\.Now\b', s)) for s in files.values())
utcnow_count = sum(len(re.findall(r'DateTime\.UtcNow\b', s)) for s in files.values())
# sadece bilgi - karisik kullanim timezone bug'i olabilir

# ============ 10) TODO/FIXME/HACK (yarim is) ============
for f, s in files.items():
    for i, ln in enumerate(s.split('\n'), 1):
        if re.search(r'//\s*(TODO|FIXME|HACK|XXX|BUG)\b', ln, re.I):
            add('todo_fixme', f"{f.split('/')[-1]}:{i}  {ln.strip()[:70]}")

# ============ 11) DI KAYIT: her Manager kayitli mi ============
reg = files.get('Divisima.Bussiness/DependencyResolvers/AutofacBusinessModule.cs', '') + files.get('Divisima.API/Program.cs', '')
for f, s in files.items():
    if f.startswith('Divisima.Bussiness/Concrete/') and f.endswith('Manager.cs'):
        mgr = f.split('/')[-1].replace('.cs', '')
        if mgr not in reg:
            add('manager_not_registered', f"{mgr} Autofac/Program'da kayitli DEGIL")

# ============ 12) NULL-FORGIVING (!) asiri kullanim / .Value guard'siz ============
# (bilgi amacli - cok fazla ! null-safety bypass olabilir)

# ============ 13) N+1 QUERY hot-path (request manager'da foreach-icinde-DAL) ============
for f, s in files.items():
    if 'Divisima.Bussiness/Concrete/' not in f or not f.endswith('Manager.cs'):
        continue
    lines = s.split('\n')
    stack = []
    for i, ln in enumerate(lines):
        if re.search(r'\b(foreach|for)\s*\(', ln):
            stack.append(i)
        if stack and re.search(r'await\s+_\w*[Dd]al\.(Get|GetList)Async', ln) and i - stack[-1] < 20:
            method = ''
            for j in range(i, -1, -1):
                mm = re.search(r'public async Task.*?\s(\w+)\(', lines[j])
                if mm: method = mm.group(1); break
            if not re.search(r'(Import|Seed|Cleanup|Retention|Bulk|Send|Notify|Reward|Migrate|Process|Campaign|Reminder|Offer|Invite)', method + f):
                add('n_plus_1_hot', f"{f.split('/')[-1]}:{i+1} [{method}] foreach-icinde-DAL-GET (N+1?)")
        if ln.strip() == '}' and stack: stack.pop()

# ============ 14) MONEY as double (decimal olmali) ============
for f, s in files.items():
    for i, ln in enumerate(s.split('\n'), 1):
        if re.search(r'\bdouble\s+\w*(price|amount|total|cost|credit|refund|fee|balance)', ln, re.I):
            add('money_double', f"{f.split('/')[-1]}:{i} money=double")

# ============ 15) MASS ASSIGNMENT ([FromBody] Entity) ============
for f, s in files.items():
    if not f.endswith('Controller.cs'): continue
    for i, ln in enumerate(s.split('\n'), 1):
        if re.search(r'\[FromBody\]\s+(Product|Customer|Order|Coupon|Category)\b', ln):
            add('mass_assignment', f"{f.split('/')[-1]}:{i} entity binding (DTO kullan)")

# ============ RAPOR ============
print("=" * 70)
print("DIVISIMA KAPSAMLI DENETIM RAPORU")
print("=" * 70)
print(f"Taranan: {len(files)} .cs dosyasi, {sum(s.count(chr(10)) for s in files.values())} satir\n")

# Kritik vs bilgi kategorileri
critical_cats = {
    'sync_over_async': 'SYNC-OVER-ASYNC (deadlock riski)',
    'async_void': 'ASYNC VOID (istisna kaybi)',
    'empty_catch': 'BOS CATCH (sessiz hata yutma)',
    'sql_raw': 'RAW SQL',
    'sql_concat': 'SQL STRING-CONCAT (injection?)',
    'hardcoded_secret': 'HARDCODED SECRET',
    'controller_no_authz': 'CONTROLLER AUTHZ YOK',
    'ignored_atomic': 'IGNORED ATOMIC RESULT',
    'query_filter_bypass': 'QUERY FILTER BYPASS',
    'manager_not_registered': 'MANAGER DI KAYITSIZ',
    'n_plus_1_hot': 'N+1 QUERY (hot-path)',
    'money_double': 'MONEY as DOUBLE',
    'mass_assignment': 'MASS ASSIGNMENT',
}
info_cats = {'todo_fixme': 'TODO/FIXME/HACK'}

total_critical = 0
print("── KRITIK KATEGORILER ──")
for cat, label in critical_cats.items():
    items = findings.get(cat, [])
    total_critical += len(items)
    status = "✅" if not items else "⚠"
    print(f"{status} {label}: {len(items)}")
    for it in items[:8]:
        print(f"      {it}")
    if len(items) > 8:
        print(f"      ... +{len(items)-8} daha")

print("\n── BILGI (mutlaka bug degil) ──")
for cat, label in info_cats.items():
    items = findings.get(cat, [])
    print(f"  {label}: {len(items)}")
    for it in items[:5]:
        print(f"      {it}")

print(f"\n  DateTime.Now: {now_count}, DateTime.UtcNow: {utcnow_count} (karisik kullanim timezone riski olabilir)")
print("\n" + "=" * 70)
print(f"KRITIK BULGU TOPLAMI: {total_critical}")
print("=" * 70)
sys.exit(0)
