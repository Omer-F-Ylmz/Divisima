#!/usr/bin/env python3
# Divisima statik analiz - dotnet build YOKken (derleyici olmadan) yakalanabilen hatalar.
# NOT: derleyici degildir; tip/imza cogunu yakalar ama hepsini degil. dotnet build hala sart.
import re, glob, collections, sys, os

txt={f:open(f,encoding='utf-8',errors='replace').read() for f in glob.glob('**/*.cs',recursive=True)}
issues=[]

# 1) Cift tip tanimi
d=collections.defaultdict(list)
for f in txt:
    for m in re.finditer(r'\b(?:public|internal)\s+(?:sealed |abstract |static |partial )?(class|interface|enum|record)\s+(\w+)',txt[f]): d[m.group(2)].append(f)
for n,v in d.items():
    if len(v)>1 and n!='Program': issues.append(f"Cift tip: {n} -> {v}")

# 2) DI kaydi (Autofac + Program)
reg=txt.get('Divisima.Bussiness/DependencyResolvers/AutofacBusinessModule.cs','')+txt.get('Divisima.API/Program.cs','')
for f in txt:
    for x in re.findall(r'public interface (I\w+(?:Service|Dal))\b',txt[f]):
        if x not in reg: issues.append(f"Kayitsiz DI: {x}")

# 3) Tanimsiz Messages
mf=[f for f in txt if f.endswith('Messages.cs')]
if mf:
    defined=set(re.findall(r'public static string (\w+)', txt[mf[0]]))
    used=set(re.findall(r'Messages\.(\w+)', chr(10).join(txt.values())))-{'X','cs'}
    for u in used-defined: issues.append(f"Tanimsiz Messages: {u}")

# 4) Gecersiz Divisima.* using
ns={m for f in txt for m in re.findall(r'namespace ([\w.]+)',txt[f])}
for f in txt:
    for m in re.finditer(r'using (Divisima\.[\w.]+);',txt[f]):
        if not (m.group(1) in ns or any(x.startswith(m.group(1)+'.') for x in ns)): issues.append(f"{f.split('/')[-1]}: gecersiz using {m.group(1)}")

# 5) Brace dengesi
for f in txt:
    if txt[f].count('{')!=txt[f].count('}'): issues.append(f"brace dengesiz: {f.split('/')[-1]}")

# 6) YENI (HUNT16): DbContext config-entity DRIFT - config'in map ettigi property entity'de var mi (CS1061 yakalar)
entity_props={}
for f in glob.glob('Divisima.Entity/Entities/*.cs')+glob.glob('Divisima.Core/Entities/**/*.cs',recursive=True):
    s=open(f,encoding='utf-8',errors='replace').read(); m=re.search(r'class (\w+)', s)
    if m: entity_props[m.group(1)]=set(re.findall(r'public [^\n]+? (\w+)\s*\{\s*get', s))
ctxf=[f for f in txt if f.endswith('DivisimaDbContext.cs')]
if ctxf:
    ctx=txt[ctxf[0]]
    for bm in re.finditer(r'modelBuilder\.Entity<(\w+)>\(b =>\s*\{(.*?)\n            \}\);', ctx, re.DOTALL):
        ent,body=bm.group(1),bm.group(2)
        if ent not in entity_props: continue
        for r in set(re.findall(r'b\.(?:Property|HasIndex)\(\w+ => \w+\.(\w+)\)', body)):
            if r not in entity_props[ent]: issues.append(f"CONFIG-ENTITY DRIFT (CS1061): {ent}.{r} config'te map ama entity'de YOK")
    # cift config blogu
    blk=collections.Counter(re.findall(r'modelBuilder\.Entity<(\w+)>\(b =>', ctx))
    for k,v in blk.items():
        if v>1: issues.append(f"CIFT CONFIG BLOGU: {k} ({v} kez)")

# 7) YENI (HUNT43): MISSING CONSTRUCTOR (CS7036) - EfEntityRepositoryBase yalniz (TContext) ctor'a sahip;
#    turetilen Ef{X}Dal kendi ctor'unu tanimlamali yoksa DERLENMEZ. H42'de 9 DAL boyle eksikti (static-check kacirmisti).
for f in glob.glob('Divisima.Dal/Concrete/Ef*.cs'):
    s=open(f,encoding='utf-8',errors='replace').read()
    cm=re.search(r'class (Ef\w+)\s*:\s*EfEntityRepositoryBase<', s)
    if cm and not re.search(rf'public {cm.group(1)}\s*\(', s):
        issues.append(f"MISSING CTOR (CS7036): {cm.group(1)} EfEntityRepositoryBase'den turuyor ama ctor YOK")

# 8) YENI (HUNT43): MISSING DbSet - DAL'li entity DbContext modelinde (DbSet veya Entity<>) olmali (yoksa runtime hata)
if ctxf:
    in_model=set(re.findall(r'DbSet<(\w+)>', ctx)) | set(re.findall(r'Entity<(\w+)>', ctx))
    for f in glob.glob('Divisima.Dal/Concrete/Ef*.cs'):
        m=re.search(r'EfEntityRepositoryBase<(\w+),', open(f,encoding='utf-8',errors='replace').read())
        if m and m.group(1) not in in_model:
            issues.append(f"MISSING DbSet: {m.group(1)} DAL var ama DbContext modelinde YOK")

# 9) YENI (HUNT43): UNDEFINED ENUM MEMBER (CS0117) - EnumType.Member kodda var ama enum'da yok
enums={}
for f in glob.glob('**/Enums/*.cs',recursive=True):
    for em in re.finditer(r'enum\s+(\w+)[^{]*\{([^}]*)\}', open(f,encoding='utf-8',errors='replace').read()):
        enums[em.group(1)]={x for x in re.findall(r'(\w+)\s*(?:=\s*\d+)?\s*,?', em.group(2)) if x and not x.isdigit()}
_em={'ToString','HasFlag','GetType','Equals','GetHashCode','Parse','TryParse','GetValues','GetNames','IsDefined'}
if enums:
    _epat=re.compile(r'\b(' + '|'.join(re.escape(e) for e in enums) + r')\.(\w+)\b')
    _seen=set()
    for f in txt:
        for m in _epat.finditer(txt[f]):
            en,mb=m.group(1),m.group(2)
            if mb in _em or (en,mb) in _seen: continue
            if mb not in enums[en]: _seen.add((en,mb)); issues.append(f"UNDEFINED ENUM (CS0117): {en}.{mb} enum'da YOK")

# 10) YENI (HUNT44): ENTITY FIELD REF (CS1061 in Business layer) - manager kodunda <entity-degiskeni>.<alan> referansi
#     entity'de YOK ise DERLENMEZ. H44'te iki gercek CS1061 boyle bulundu (Order.updated_at yok - entity'de sadece
#     created_at/delivered_at var). static_check'in eski config-drift kontrolu YALNIZ DbContext'e bakiyordu, manager koduna DEGIL.
#     Heuristik (dusuk yanlis-pozitif): degiskeni SADECE "var X = await _<e>Dal.GetAsync(" ve
#     "foreach (var X in <GetListAsync'ten gelen koleksiyon>)" kaliplarindan turet.
_ent_props={}
for f in glob.glob('Divisima.Entity/Entities/*.cs'):
    s=open(f,encoding='utf-8',errors='replace').read(); m=re.search(r'class (\w+)', s)
    if m: _ent_props[m.group(1)]=set(re.findall(r'public [^\n]+? (\w+)\s*\{\s*get', s))
_dal2ent={}   # _orderDal -> Order
for f in glob.glob('Divisima.Dal/Concrete/Ef*.cs'):
    s=open(f,encoding='utf-8',errors='replace').read()
    m=re.search(r'class Ef(\w+)Dal\s*:\s*EfEntityRepositoryBase<(\w+),', s)
    if m: _dal2ent['_'+m.group(1)[0].lower()+m.group(1)[1:]+'Dal']=m.group(2)
for f in glob.glob('Divisima.Bussiness/Concrete/*.cs'):
    s=open(f,encoding='utf-8',errors='replace').read()
    # KONUM-DUYARLI baglama: ayni degisken adi farkli metotlarda farkli tipe baglanabilir (ör. CartManager'da
    # "existing" once CartItem sonra WishlistItem) -> her kullanim icin EN YAKIN ONCEKI baglamayi kullan.
    binds=[]   # (pos, var, entity)
    for m in re.finditer(r'var (\w+)\s*=\s*await (_\w+Dal)\.GetAsync\(', s):
        if m.group(2) in _dal2ent: binds.append((m.start(), m.group(1), _dal2ent[m.group(2)]))
    for m in re.finditer(r'var (\w+)\s*=\s*await (_\w+Dal)\.GetList(?:NoTracking)?Async\(', s):
        if m.group(2) in _dal2ent:
            for fm in re.finditer(r'foreach\s*\(\s*var (\w+) in ' + re.escape(m.group(1)) + r'\b', s):
                binds.append((fm.start(), fm.group(1), _dal2ent[m.group(2)]))
    if not binds: continue
    names={b[1] for b in binds}
    for pm in re.finditer(r'\b(' + '|'.join(re.escape(n) for n in names) + r')\.([a-z_][a-z0-9_]*)\b', s):
        var, prop = pm.group(1), pm.group(2)
        prior=[b for b in binds if b[1]==var and b[0] < pm.start()]
        if not prior: continue
        ent=max(prior, key=lambda b: b[0])[2]
        if ent not in _ent_props or prop in _ent_props[ent]: continue
        issues.append(f"ENTITY FIELD REF (CS1061): {os.path.basename(f)} -> {var}.{prop} ({ent} entity'sinde YOK)")


# 11) YENI (HUNT44): DAL LAMBDA PARAM + OBJECT INITIALIZER alan kontrolu (CS1061/CS0117).
#     H44'te WishlistItem.is_active hatalarinin 3'u bu kaliplardaydi: "_wishlistItemDal.GetAsync(w => ... w.is_active)"
#     ve "new WishlistItem { is_active = true }". Entity'de olmayan alan -> DERLENMEZ.
def _match_paren(src, i):
    d=0
    for j in range(i, len(src)):
        if src[j]=='(': d+=1
        elif src[j]==')':
            d-=1
            if d==0: return j
    return len(src)-1
for f in glob.glob('Divisima.Bussiness/Concrete/*.cs')+glob.glob('Divisima.Dal/Concrete/*.cs'):
    s=open(f,encoding='utf-8',errors='replace').read()
    # (a) DAL lambda parametreleri
    for m in re.finditer(r'(_\w+Dal)\.(?:GetAsync|GetListAsync|GetListNoTrackingAsync|DeleteWhereAsync|GetPagedAsync)\s*\(', s):
        dal=m.group(1)
        if dal not in _dal2ent: continue
        ent=_dal2ent[dal]
        if ent not in _ent_props: continue
        end=_match_paren(s, m.end()-1)
        body=s[m.end():end]
        lm=re.match(r'\s*(\w+)\s*=>', body)
        if not lm: continue
        prm=lm.group(1)
        for pm in re.finditer(r'\b'+re.escape(prm)+r'\.([a-z_][a-z0-9_]*)\b', body):
            if pm.group(1) not in _ent_props[ent]:
                issues.append(f"DAL LAMBDA FIELD (CS1061): {os.path.basename(f)} -> {prm}.{pm.group(1)} ({ent} entity'sinde YOK)")
    # (b) nesne baslatici
    for m in re.finditer(r'new\s+(?:[\w.]+\.)?(\w+)\s*\{([^{}]*)\}', s):
        ent=m.group(1)
        if ent not in _ent_props: continue
        for fm in re.finditer(r'(\w+)\s*=', m.group(2)):
            if fm.group(1) not in _ent_props[ent]:
                issues.append(f"OBJECT INIT FIELD (CS0117): {os.path.basename(f)} -> {ent} {{ {fm.group(1)} = ... }} entity'de YOK")


# 12) YENI (HUNT45b): MISSING ENUM USING (CS0103) - dosya *Enum tipini kullaniyor ama
#     "Divisima.Core.Utilities.Enums" IMPORT EDILMEMIS -> "name does not exist" derleme hatasi.
#     H45b'de tam bu tuzaga dusuluyordu (MerchandisingManager'a OrderStatusEnum eklendi, using yoktu).
_enum_ns = "Divisima.Core.Utilities.Enums"
for f in glob.glob('Divisima.Bussiness/**/*.cs', recursive=True)+glob.glob('Divisima.API/**/*.cs', recursive=True):
    if '/obj/' in f or '/bin/' in f: continue
    s2 = open(f, encoding='utf-8', errors='replace').read()
    used = set(re.findall(r'\b(\w+Enum)\b', s2)) & set(enums.keys() if 'enums' in dir() else [])
    if not used: continue
    if _enum_ns in s2: continue                      # using var (veya tam nitelikli kullanim)
    if re.search(r'Divisima\.Core\.Utilities\.Enums\.', s2): continue
    issues.append(f"MISSING ENUM USING (CS0103): {os.path.basename(f)} -> {sorted(used)[0]} kullaniyor ama '{_enum_ns}' import YOK")


# 13) YENI (HUNT46): AUTHZ GAP - her controller action'i [RequireUserType]/[AllowAnonymous]/[Authorize]
#     ile isaretli OLMALI (veya controller seviyesinde). Isaretsiz action = BROKEN ACCESS CONTROL (en tehlikeli sinif).
#     YORUM-TOLERANSLI: attribute'lar arasina yorum/bos satir girse bile zincir kopmaz (naif regex bunu kaciriyordu).
for f in glob.glob('Divisima.API/Controllers/*.cs'):
    src = open(f, encoding='utf-8', errors='replace').read()
    lines = src.split("\n")
    cls_idx = next((i for i, l in enumerate(lines) if re.search(r'public\s+(?:abstract\s+)?class \w+Controller', l)), None)
    if cls_idx is None: continue
    header = "\n".join(lines[:cls_idx])
    cls_level = ('RequireUserType' in header) or ('AllowAnonymous' in header) or ('Authorize' in header)
    for i, l in enumerate(lines):
        if not re.search(r'public\s+(?:async\s+)?Task<IActionResult>\s+\w+\s*\(', l): continue
        if cls_level: continue
        found = False
        j = i - 1
        while j >= 0:
            t = lines[j].strip()
            if t == '' or t.startswith('//') or t.startswith('///') or t.startswith('*') or t.startswith('/*'):
                j -= 1; continue
            if t.startswith('['):
                if ('RequireUserType' in t) or ('AllowAnonymous' in t) or ('Authorize' in t):
                    found = True; break
                j -= 1; continue
            break
        if not found:
            mname = re.search(r'Task<IActionResult>\s+(\w+)', l)
            issues.append(f"AUTHZ GAP: {os.path.basename(f)} -> {mname.group(1) if mname else '?'} action'inda yetkilendirme attribute'u YOK")


# 14) YENI (HUNT51): PAGED RESULT FIELD (CS1061) - GetPagedAsync donen degiskenin ozellikleri
#     PagedResult<T>'de VAR MI. H51'de tam bu tuzaga dusuldu: pagedOrders.total_count / .items yazildi
#     ama gercek isimler PascalCase (TotalCount/Items) -> derleme hatasi. Entity kontrolu bunu kapsamiyordu.
_paged_props = set()
for f in glob.glob('Divisima.Core/Utilities/Dtos/PagedResult.cs'):
    _paged_props |= set(re.findall(r'public [^\n]+? (\w+)\s*(?:\{\s*get|=>)', open(f, encoding='utf-8', errors='replace').read()))
if _paged_props:
    for f in glob.glob('Divisima.Bussiness/**/*.cs', recursive=True) + glob.glob('Divisima.API/**/*.cs', recursive=True):
        if '/obj/' in f or '/bin/' in f: continue
        s_ = open(f, encoding='utf-8', errors='replace').read()
        for m in re.finditer(r'var (\w+)\s*=\s*await\s+_\w+Dal\.GetPagedAsync\(', s_):
            var = m.group(1)
            for pm in re.finditer(rf'\b{var}\.(\w+)', s_[m.end():]):
                prop = pm.group(1)
                if prop in _paged_props or prop in ('ToString', 'GetType', 'Equals', 'GetHashCode'): continue
                issues.append(f"PAGED RESULT FIELD (CS1061): {os.path.basename(f)} -> {var}.{prop} (PagedResult'ta YOK; dogrusu {sorted(_paged_props)})")
                break


# 15) YENI (HUNT54): VOID RETURN VALUE (CS1997) - "async Task" (generic OLMAYAN) bir metot ICINDE
#     deger donduren "return (...)" varsa DERLENMEZ. H54'te kendi otomatik duzenlemem tam bu hatayi
#     uretti (ReferralManager.RewardOnFirstOrder void iken oraya tuple return eklendi).
for f in glob.glob('Divisima.Bussiness/**/*.cs', recursive=True) + glob.glob('Divisima.API/**/*.cs', recursive=True):
    if '/obj/' in f or '/bin/' in f: continue
    s_ = open(f, encoding='utf-8', errors='replace').read()
    for mm in re.finditer(r'public\s+async\s+Task\s+(\w+)\s*\([^)]*\)\s*\{', s_):
        st = mm.end(); d = 1; k = st
        while k < len(s_) and d > 0:
            if s_[k] == '{': d += 1
            elif s_[k] == '}': d -= 1
            k += 1
        body = s_[st:k-1]
        # ic-metot/lambda govdelerini kabaca disla: sadece "return <deger>;" ara
        bad = re.search(r'\breturn\s+(?!;)[^;\n]{2,};', body)
        if bad:
            issues.append(f"VOID RETURN VALUE (CS1997): {os.path.basename(f)} -> {mm.group(1)}() 'async Task' (void) ama deger donduruyor: {bad.group(0)[:48]}")

print(f"{len(txt)} .cs tarandi.")
if issues:
    print(f"❌ {len(issues)} SORUN:")
    for i in issues: print(f"  - {i}")
    sys.exit(1)
else:
    print("✅ TEMIZ (tum statik kontroller gecti)")
    sys.exit(0)
