// ── Yapılandırma ── API adresini kendi ortamına göre değiştir
// DALGA-4-FIX-2 / M1: taban TEK KAYNAKTAN (bastaki meta) turer; operator override'i
// (localStorage) ONCELIKLI kalir. Ikinci bir "localhost" literali YOK.
const API_ORIGIN_META = (document.querySelector('meta[name="divisima-api-origin"]') || {}).content || "";
const API_BASE = (localStorage.getItem("divisima_api_base") || API_ORIGIN_META).trim().replace(/\/+$/, "");
(function () {
  function bagir(msg) {
    if (window.console && console.error) console.error("[DIVISIMA YAPILANDIRMA] " + msg);
    var b = document.createElement("div");
    b.setAttribute("role", "alert");
    b.style.cssText = "position:fixed;left:0;right:0;top:0;z-index:2147483647;background:#8c2f2f;color:#fff;font:600 13px/1.45 system-ui,sans-serif;padding:10px 14px";
    b.textContent = "Yapilandirma hatasi: " + msg;
    var koy = function () { (document.body || document.documentElement).appendChild(b); };
    if (document.body) koy(); else document.addEventListener("DOMContentLoaded", koy);
  }
  if (!API_BASE) { bagir('API tabani belirlenemedi - meta[name="divisima-api-origin"] eksik.'); return; }
  var cm = document.querySelector('meta[http-equiv="Content-Security-Policy"]');
  var csp = cm ? String(cm.getAttribute("content") || "") : "";
  if (!csp) { bagir("CSP meta etiketi okunamadi - origin tutarliligi DOGRULANAMADI."); return; }
  var ayniOrigin = (API_BASE === location.origin);
  // REGEX YOK - storefront'takiyle ayni gerekce: ters bolu kacisi bir kez sessizce
  // kayboldu ve guard YANLIS ALARM verdi. Duz split + trim'in kacis semantigi yoktur.
  function direktifDegeri(d) {
    var ps = csp.split(";");
    for (var i = 0; i < ps.length; i++) {
      var p = ps[i].trim();
      if (p === d) return "";
      if (p.indexOf(d + " ") === 0) return p.slice(d.length + 1);
    }
    return null;
  }
  var eksik = ["img-src", "connect-src"].filter(function (d) {
    var v = direktifDegeri(d);
    if (v === null) return true;
    if (v.indexOf(API_BASE) >= 0) return false;
    return !(ayniOrigin && v.indexOf("'self'") >= 0);
  });
  if (eksik.length) bagir('API origin "' + API_BASE + '" su CSP direktiflerinde YOK: ' + eksik.join(", ") + " - dagitimda ops/set-api-origin.sh kullanilmali.");
})();
const api = new DivisimaAPI(API_BASE);
let chartRefs = {};

// ── Yardımcılar ──
function toast(msg, isErr){const t=document.getElementById("toast");t.textContent=msg;t.className="toast show"+(isErr?" err":"");setTimeout(()=>t.className="toast",2600);}
function money(n){return "₺"+Number(n||0).toLocaleString("tr-TR",{minimumFractionDigits:2,maximumFractionDigits:2});}
function unwrap(r){return r && r.data !== undefined ? r.data : r;}
const ORDER_STATUS={0:["Bekliyor","warn"],1:["Onaylandı","ok"],2:["Hazırlanıyor","muted"],3:["Kargoda","warn"],4:["Teslim","ok"],5:["İptal","danger"]};
const RETURN_STATUS={0:["Bekliyor","warn"],1:["Onaylandı","ok"],2:["Reddedildi","danger"],3:["Tamamlandı","ok"]};

// ── Auth ──
async function doLogin(){
  const email=document.getElementById("loginEmail").value.trim();
  const pass=document.getElementById("loginPass").value;
  if(!email||!pass) return toast("E-posta ve şifre girin",true);
  try{
    await api.auth.login(email,pass);
    showApp();
  }catch(e){toast(e.message||"Giriş başarısız",true);}
}
async function doLogout(){await api.auth.logout();location.reload();}
function showApp(){
  document.getElementById("loginScreen").classList.add("hidden");
  document.getElementById("app").classList.remove("hidden");
  render("dashboard");
}

// ── Navigasyon ──
document.getElementById("nav").addEventListener("click",e=>{
  const a=e.target.closest("a[data-view]");if(!a)return;
  document.querySelectorAll("#nav a").forEach(x=>x.classList.remove("active"));
  a.classList.add("active");
  render(a.dataset.view);
});

// ── Görünüm yönlendirici ──
async function render(view){
  const el=document.getElementById("views");
  el.innerHTML='<p class="muted">Yükleniyor…</p>';
  try{
    if(view==="dashboard") await renderDashboard(el);
    else if(view==="products") await renderProducts(el);
    else if(view==="orders") await renderOrders(el);
    else if(view==="returns") await renderReturns(el);
    else if(view==="shipments") await renderShipments(el);
    else if(view==="coupons") await renderCoupons(el);
    else if(view==="stock") await renderStock(el);
    else if(view==="images") await renderImages(el);
  }catch(e){
    el.innerHTML='<div class="panel"><p class="muted">Veri alınamadı: '+esc(e.message||"hata")+'</p></div>';
    // DALGA B / B2: 403 DALI EKLENDI. Once yalniz 401 tanınıyordu; bayat ya da baska bir
    // hesaba ait bir token localStorage'da kaldiginda uclar 403 doner ve panel "Veri
    // alinamadi" yazip KILITLI kalirdi - kullanicinin cikis yapmasi gerektigini soyleyen
    // hicbir sey yoktu (bu oturumda birebir yasandi). Iki durum da ayni sey demek:
    // elindeki token bu panel icin GECERSIZ.
    if(e.status===401||e.status===403){toast("Oturum geçersiz - yeniden giriş gerekiyor",true);api.setAccessToken(null);setTimeout(()=>location.reload(),1800);}
  }
}

// ── Dashboard ──
async function renderDashboard(el){
  const [sum,daily,top,status,low,hatali]=await Promise.all([
    api.admin.summary().then(unwrap),
    api.admin.dailySales().then(unwrap),
    api.admin.topProducts(5).then(unwrap),
    api.admin.orderStatus().then(unwrap),
    api.admin.lowStock(5).then(unwrap),
    // DALGA C / C4: basarisiz arka plan isleri. Uretimde YEDI recurring is kosuyor ve biri
    // dustugunde operatorun gorebilecegi HICBIR yuzey yoktu (Hangfire panosu tarayicidan
    // ERISILEMEZ - tek kimlik semasi JwtBearer). Panel sekmesine konuldu cunku operator
    // her giriste BURAYA bakiyor; ayri bir sekmede gozden kacardi.
    api.admin.failedJobs(20).then(unwrap),
  ]);
  el.innerHTML=`
    <div class="topbar"><h2>Panel</h2><button class="btn ghost sm" data-act="git" data-gorunum="dashboard">↻ Yenile</button></div>
    <div class="cards">
      <div class="card"><div class="label">Toplam Ciro</div><div class="value">${money(sum.total_revenue)}</div></div>
      <div class="card"><div class="label">Sipariş</div><div class="value">${sum.total_orders}</div></div>
      <div class="card"><div class="label">Bekleyen</div><div class="value">${sum.pending_orders}</div></div>
      <div class="card"><div class="label">Ort. Sepet</div><div class="value">${money(sum.average_order_value)}</div></div>
      <div class="card"><div class="label">Müşteri</div><div class="value">${sum.total_customers}</div></div>
      <div class="card"><div class="label">Stok Uyarısı</div><div class="value" style="color:${sum.low_stock_count>0?'var(--warn)':'inherit'}">${sum.low_stock_count}</div></div>
    </div>
    <div class="grid2">
      <div class="panel"><h3>Günlük Ciro (30 gün)</h3><canvas id="chDaily" height="140"></canvas></div>
      <div class="panel"><h3>Sipariş Durumu</h3><canvas id="chStatus" height="140"></canvas></div>
    </div>
    <div class="grid2">
      <div class="panel"><h3>En Çok Satan Ürünler</h3><table><thead><tr><th>Ürün</th><th>Adet</th><th>Ciro</th></tr></thead><tbody>
        ${(top||[]).map(p=>`<tr><td>${esc(p.product_name)}</td><td>${p.total_quantity}</td><td>${money(p.total_revenue)}</td></tr>`).join("")||'<tr><td colspan=3 class=muted>Veri yok</td></tr>'}
      </tbody></table></div>
      <div class="panel"><h3>Stok Uyarıları</h3><table><thead><tr><th>Ürün</th><th>Beden</th><th>Stok</th></tr></thead><tbody>
        ${(low||[]).map(s=>`<tr><td>${esc(s.product_name)}</td><td>${esc(s.size)}</td><td><span class="badge warn">${s.quantity}</span></td></tr>`).join("")||'<tr><td colspan=3 class=muted>Kritik stok yok</td></tr>'}
      </tbody></table></div>
    </div>
    <div class="panel">
      <h3>Başarısız Arka Plan İşleri ${(hatali||[]).length?`<span class="badge danger">${hatali.length}</span>`:""}</h3>
      <p class="muted" style="margin-bottom:12px">Yeniden deneme hakkı tükenmiş işler (e-posta, ödeme yan etkileri, sipariş bildirimleri). Boş olması beklenen durumdur.</p>
      <table><thead><tr><th>ID</th><th>İş</th><th>Deneme</th><th>Hata</th><th>Zaman</th></tr></thead><tbody>
        ${(hatali||[]).map(j=>`<tr><td>${j.id}</td><td><b>${esc(j.event_type)}</b></td><td><span class="badge danger">${j.retry_count}</span></td><td class=muted style="max-width:420px;word-break:break-word">${esc(j.error||"-")}</td><td class=muted>${new Date(j.created_at).toLocaleString("tr-TR")}</td></tr>`).join("")||'<tr><td colspan=5 class=muted>Başarısız iş yok</td></tr>'}
      </tbody></table>
    </div>`;
  // Grafikler
  Object.values(chartRefs).forEach(c=>c&&c.destroy());chartRefs={};
  chartRefs.daily=new Chart(document.getElementById("chDaily"),{type:"line",data:{labels:(daily||[]).map(d=>new Date(d.date).toLocaleDateString("tr-TR",{day:"2-digit",month:"2-digit"})),datasets:[{label:"Ciro",data:(daily||[]).map(d=>d.revenue),borderColor:"#111",backgroundColor:"rgba(17,17,17,.06)",fill:true,tension:.3}]},options:{plugins:{legend:{display:false}},scales:{y:{beginAtZero:true}}}});
  chartRefs.status=new Chart(document.getElementById("chStatus"),{type:"doughnut",data:{labels:(status||[]).map(s=>ORDER_STATUS[s.status]?ORDER_STATUS[s.status][0]:s.status_name),datasets:[{data:(status||[]).map(s=>s.count),backgroundColor:["#f2c94c","#0f6e56","#b4b2a9","#ba7517","#1d9e75","#a32d2d"]}]},options:{plugins:{legend:{position:"right"}}}});
}

// ── Ürünler ──
async function renderProducts(el){
  const products=unwrap(await api.products.list())||[];
  el.innerHTML=`
    <div class="topbar"><h2>Ürünler (${products.length})</h2><button class="btn" data-act="urunFormuYeni">+ Yeni Ürün</button></div>
    <div class="panel"><table><thead><tr><th>ID</th><th>Ad</th><th>Marka</th><th>Fiyat</th><th></th></tr></thead><tbody>
      ${products.map(p=>`<tr><td>${p.id}</td><td>${esc(p.name)}</td><td class=muted>${esc(p.brand||"-")}</td><td>${money(p.price)}</td>
        <td class=row-actions><button class="btn ghost sm" data-act="urunFormu" data-id="${p.id}">Düzenle</button>
        <button class="btn danger sm" data-act="urunSil" data-id="${p.id}">Sil</button></td></tr>`).join("")}
    </tbody></table></div>`;
}
// DALGA B / B2 - URUN FORMU: EKLEME VE GUNCELLEME IKISI DE OLUYDU.
//
// OLCULEN ONCE-DURUM (canli, panelden): "Kaydet" -> operatore ham cerceve mesaji
// "The stocks field is required." dusuyordu ve formda O ALAN YOKTU. Ikinci deneme
// (stocks:[] ile) "The color_hex field is required." dedi. Yani panelden urun eklemek
// ya da duzenlemek MUMKUN DEGILDI - zorunlu iki alan (stocks, color_hex) forma hic
// konmamisti. B1 ile ayni sinif: panelin gonderdigi govde DTO ile ortusmuyor.
//
// GUNCELLEMEDE SESSIZ VERI KAYBI - AYRI VE DAHA AGIR BIR TUZAK:
// ProductManager.Update TAM-VARLIK map yapar (_mapper.Map(dto, product)). Yani
// ProductUpdateRequestDto'da BULUNAN ama gonderilmeyen her alan varsayilanina (null/0)
// duser ve MEVCUT DEGERI EZER. Eski form yalniz 5 alan gonderiyordu; calissaydi bile
// old_price / sale_price / sub_category_id SILINIR, product_type sessizce Clothing'e
// donerdi. Ustelik Update "if (dto.stocks != null)" ile TUM beden satirlarini pasifleyip
// gelenleri yeniden yazar - bos bir liste gondermek URUNUN TUM STOGUNU SIFIRLARDI.
// Bu yuzden DUZENLEME formu artik urunun GERCEK guncel halini detay ucundan yukler ve
// hepsini geri gonderir; hicbir alan "gonderilmedigi icin" kaybolmaz.
//
// URUN TIPI de KUPON TIPIYLE AYNI SINIF: detay ucu ENUM ADI (metin) doner
// (ProductProfile: ((ProductTypeEnum)src.product_type).ToString()), ekleme/guncelleme ucu
// SAYI bekler. Iki gosterimi de taniyan tek merkez:
const URUN_TIPI_KOD={"0":0,"Clothing":0,"1":1,"Accessory":1};
function urunTipiKodu(t){const k=URUN_TIPI_KOD[String(t)];return k===undefined?0:k;}

let urunStokSatirlari=[];   // [{size, stock_quantity}] - forma ozel gecici durum

function stokSatirlariniCiz(){
  const kutu=document.getElementById("p_stocks");
  if(!kutu)return;
  kutu.innerHTML=urunStokSatirlari.map((s,i)=>`<div class="grid2" style="margin-bottom:8px">
      <label class="f"><span>Beden</span><input value="${esc(s.size)}" data-act="stokBeden" data-i="${i}"></label>
      <label class="f"><span>Adet</span><input type="number" step="1" min="0" value="${Number(s.stock_quantity)||0}" data-act="stokAdet" data-i="${i}"></label>
    </div>`).join("")
    +`<button class="btn ghost sm" type="button" data-act="bedenEkle">+ Beden ekle</button>`
    +(urunStokSatirlari.length>1?` <button class="btn ghost sm" type="button" data-act="bedenKaldir">− Son bedeni kaldır</button>`:"");
}

async function productForm(pListe){
  const el=document.getElementById("views");
  el.innerHTML='<p class="muted">Yükleniyor…</p>';
  const duzenle=!!(pListe&&pListe.id);
  let p={};
  try{
    // DUZENLEMEDE GERCEK GUNCEL HALI YUKLE. Liste ogesi yetmez: description, sale_price,
    // sub_category_id, product_type ve beden/stok LISTE DTO'sunda YOK - eksik gonderilen
    // her alan Update'te MEVCUT DEGERI EZER (yukaridaki tuzak).
    p = duzenle ? (unwrap(await api.products.get(pListe.id))||{}) : {};
  }catch(e){ el.innerHTML='<div class="panel"><p class="muted">Ürün yüklenemedi: '+esc(e.message||"hata")+'</p></div>'; return; }

  let kategoriler=[];
  try{ kategoriler=unwrap(await api.categories.list())||[]; }catch(_){ kategoriler=[]; }

  // MFIX-B / K1 (ZORUNLU): STOK SATIRLARI ARTIK ADMIN UCUNDAN OKUNUR.
  // Anonim detay ucu (api.products.get) K1'den sonra SATILABILIR adedi donuyor. O degeri forma
  // koyup "Kaydet" demek fiziksel stogu SESSIZCE satilabilire dusururdu: ProductManager.Update
  // `mevcut.stock_quantity = s.stock_quantity` ile FIZIKSEL kolona yazar ve reserved_quantity'ye
  // DOKUNMAZ; rezerve>0 olan her bedende available = stock - reserved kimligi kalici bozulurdu
  // (or. 937/M fiziksel 10, rezerve 6 -> kaydet -> fiziksel 4, available 0). Dalga B'nin
  // "tam-varlik map -> sessiz veri kaybi" sinifinin birebir tekrari olurdu.
  // /api/Stock/{id} admin korumalidir ve FIZIKSEL stogu doner.
  let adminStok=[];
  if(duzenle){
    try{ adminStok=unwrap(await api.stock.byProduct(pListe.id))||[]; }
    catch(e){
      // FAIL-CLOSED: stok okunamadiysa forma ANONIM degerler DUSMEZ - form hic acilmaz.
      el.innerHTML='<div class="panel"><p class="muted">Stok bilgisi alınamadı: '+esc(e.message||"hata")
        +'. Yanlış stok kaydetmemek için düzenleme formu açılmadı.</p></div>';
      return;
    }
  }

  urunStokSatirlari = (adminStok&&adminStok.length)
    ? adminStok.map(s=>({size:s.size,stock_quantity:s.stock_quantity}))
    : [{size:"S",stock_quantity:0},{size:"M",stock_quantity:0},{size:"L",stock_quantity:0}];

  const tipKod = duzenle ? urunTipiKodu(p.product_type) : 0;
  el.innerHTML=`<div class="topbar"><h2>${duzenle?"Ürün Düzenle":"Yeni Ürün"}</h2><button class="btn ghost sm" data-act="git" data-gorunum="products">← Geri</button></div>
    <div class="panel" style="max-width:640px">
      <label class="f"><span>Ürün Adı</span><input id="p_name" value="${esc(p.name||"")}"></label>
      <label class="f"><span>Marka</span><input id="p_brand" value="${esc(p.brand||"")}"></label>
      <div class="grid2">
        <label class="f"><span>Fiyat (₺)</span><input id="p_price" type="number" step="0.01" value="${p.price||""}"></label>
        <label class="f"><span>Kategori</span><select id="p_cat">
          ${kategoriler.length
            ? kategoriler.map(k=>`<option value="${k.id}"${Number(p.category_id)===Number(k.id)?" selected":""}>#${k.id} · ${esc(k.name)}</option>`).join("")
            : `<option value="${p.category_id||""}">Kategori listesi alınamadı — mevcut: ${p.category_id||"yok"}</option>`}
        </select></label>
      </div>
      <div class="grid2">
        <label class="f"><span>İndirimli fiyat (₺, boş = indirim yok)</span><input id="p_sale" type="number" step="0.01" value="${p.sale_price!=null?p.sale_price:""}"></label>
        <label class="f"><span>Üstü çizili fiyat (₺, boş = yok)</span><input id="p_old" type="number" step="0.01" value="${p.old_price!=null?p.old_price:""}"></label>
      </div>
      <div class="grid2">
        <label class="f"><span>Renk (hex)</span><input id="p_color" placeholder="#2244aa" value="${esc(p.color_hex||"#000000")}"></label>
        <label class="f"><span>Ürün Tipi</span><select id="p_type">
          <option value="0"${tipKod===0?" selected":""}>Giysi (bedenli)</option>
          <option value="1"${tipKod===1?" selected":""}>Aksesuar</option>
        </select></label>
      </div>
      <label class="f"><span>Açıklama</span><textarea id="p_desc" rows="3">${esc(p.description||"")}</textarea></label>
      <h3 style="margin:16px 0 10px">Beden / Stok</h3>
      <div id="p_stocks"></div>
      ${duzenle?'<p class="muted" style="margin-top:10px">Kaydedince beden listesi <b>tamamen bu tabloyla değiştirilir</b> — burada olmayan bir beden pasife alınır.</p>':""}
      <button class="btn" style="margin-top:14px" data-act="urunKaydet" data-id="${duzenle?p.id:""}">${duzenle?"Güncelle":"Kaydet"}</button>
    </div>`;
  stokSatirlariniCiz();
}

async function saveProduct(id){
  const renk=document.getElementById("p_color").value.trim();
  const fiyat=parseFloat(document.getElementById("p_price").value);
  const indirim=document.getElementById("p_sale").value.trim();
  const ustu=document.getElementById("p_old").value.trim();
  const bedenler=urunStokSatirlari.filter(s=>String(s.size||"").trim()!=="")
                                  .map(s=>({size:String(s.size).trim(),stock_quantity:parseInt(s.stock_quantity)||0}));

  // GIRIS KAPILARI - sunucu bu uc durumu da reddediyor; hatayi kullaniciya TURKCE ve
  // duzeltilebilir sekilde burada soyluyoruz (once ham "The X field is required." dusuyordu).
  if(!document.getElementById("p_name").value.trim()) return toast("Ürün adı girin",true);
  if(!(fiyat>0)) return toast("Fiyat 0'dan büyük olmalı",true);
  if(!/^#([0-9a-fA-F]{6}|[0-9a-fA-F]{8})$/.test(renk)) return toast("Renk #RRGGBB biçiminde olmalı",true);
  // BOS BEDEN LISTESI GONDERILMEZ: Update tum satirlari pasifleyip gelenleri yazar, yani
  // bos liste URUNUN STOGUNU SIFIRLAR. Ekleme tarafinda da bedensiz urun satin ALINAMAZ.
  if(!bedenler.length) return toast("En az bir beden/stok satırı gerekli",true);

  const payload={
    name:document.getElementById("p_name").value.trim(),
    brand:document.getElementById("p_brand").value.trim(),
    category_id:parseInt(document.getElementById("p_cat").value)||0,
    price:fiyat,
    sale_price:indirim===""?null:parseFloat(indirim),
    old_price:ustu===""?null:parseFloat(ustu),
    description:document.getElementById("p_desc").value.trim(),
    color_hex:renk,
    product_type:parseInt(document.getElementById("p_type").value)||0,
    stocks:bedenler
  };
  try{
    if(id){payload.id=id;await api.admin.updateProduct(payload);toast("Ürün güncellendi");}
    else{await api.admin.addProduct(payload);toast("Ürün eklendi");}
    render("products");
  }catch(e){toast(e.message,true);}
}
async function delProduct(id){if(!confirm("Ürün silinsin mi?"))return;try{await api.admin.deleteProduct(id);toast("Silindi");render("products");}catch(e){toast(e.message,true);}}

// ── Siparişler ──
async function renderOrders(el){
  // DALGA B / B2 - IKI KUSUR BIRDEN DUZELTILDI:
  //  (1) ZARF ADLARI: panel "Items"/"TotalCount" okuyordu; uc { items, total_count, ... }
  //      donuyor. Sonuc CANLI olculdu - veritabaninda 52 siparis varken bu ekran
  //      "Siparisler (0) / Siparis yok" gosteriyordu, ustelik AYNI oturumda Panel sekmesi
  //      "SIPARIS 52" diyordu. Operatorun gelen siparisi gorebildigi TEK liste bu.
  //  (2) SESSIZ CATCH: ".catch(()=>({Items:[]}))" 401/403/500 dahil HER hatayi yutup BOS
  //      TABLO ciziyordu - yani "hic siparis yok" ile "uc patladi" ayirt EDILEMIYORDU.
  //      Catch kaldirildi; hata artik render()'in ortak hata daline dusuyor ve GORUNUYOR.
  const res=unwrap(await api.admin.allOrders({page:1,page_size:50}));
  const orders=(res&&res.items)||[];
  const toplam=(res&&res.total_count!=null)?res.total_count:orders.length;
  el.innerHTML=`<div class="topbar"><h2>Siparişler (${toplam})</h2><button class="btn ghost sm" data-act="git" data-gorunum="orders">↻</button></div>
    <div class="panel"><p class="muted" style="margin-bottom:14px">Sipariş no ile işlem: durum değiştir, fatura oluştur, kargo ekle.</p>
      <div class="grid2">
        <label class="f"><span>Sipariş ID</span><input id="o_id" type="number" placeholder="123"></label>
        <label class="f"><span>Yeni Durum</span><select id="o_status">
          <option value="1">Onaylandı</option><option value="2">Hazırlanıyor</option><option value="3">Kargoda</option><option value="4">Teslim</option><option value="5">İptal</option>
        </select></label>
      </div>
      <div class="row-actions">
        <button class="btn" data-act="siparisDurum">Durumu Güncelle</button>
        <button class="btn ghost" data-act="fatura">Fatura Oluştur</button>
      </div>
    </div>
    <div class="panel"><h3>Tüm Siparişler</h3><table><thead><tr><th>No</th><th>Müşteri</th><th>Tutar</th><th>Durum</th><th>Tarih</th></tr></thead><tbody>
      ${orders.map(o=>{const st=ORDER_STATUS[o.status]||["?","muted"];return `<tr><td>${esc(o.order_number||String(o.id))}</td><td class=muted>#${o.customer_id}</td><td>${money(o.total_price)}</td><td><span class="badge ${st[1]}">${st[0]}</span></td><td class=muted>${new Date(o.created_at).toLocaleDateString("tr-TR")}</td></tr>`;}).join("")||'<tr><td colspan=5 class=muted>Sipariş yok</td></tr>'}
    </tbody></table></div>`;
}
async function changeOrderStatus(){
  const id=parseInt(document.getElementById("o_id").value);const status=parseInt(document.getElementById("o_status").value);
  if(!id)return toast("Sipariş ID girin",true);
  try{await api.admin.changeOrderStatus(id,status);toast("Durum güncellendi");}
  catch(e){toast(e.message,true);}
}
async function genInvoice(){const id=parseInt(document.getElementById("o_id").value);if(!id)return toast("Sipariş ID girin",true);try{await api.admin.generateInvoice(id);toast("Fatura oluşturuldu");}catch(e){toast(e.message,true);}}

// ── İadeler ──
async function renderReturns(el){
  const pending=unwrap(await api.admin.pendingReturns())||[];
  el.innerHTML=`<div class="topbar"><h2>Bekleyen İadeler (${pending.length})</h2><button class="btn ghost sm" data-act="git" data-gorunum="returns">↻</button></div>
    <div class="panel"><table><thead><tr><th>ID</th><th>Sipariş</th><th>Ürün</th><th>Adet</th><th>Tutar</th><th></th></tr></thead><tbody>
      ${pending.map(r=>`<tr><td>${r.id}</td><td>${r.order_id}</td><td>${r.product_id} (${esc(r.size)})</td><td>${r.quantity}</td><td>${money(r.refund_amount)}</td>
        <td class=row-actions><button class="btn ok sm" data-act="iade" data-id="${r.id}" data-onay="1">Onayla</button><button class="btn danger sm" data-act="iade" data-id="${r.id}" data-onay="0">Reddet</button></td></tr>`).join("")||'<tr><td colspan=6 class=muted>Bekleyen iade yok</td></tr>'}
    </tbody></table></div>`;
}
async function procReturn(id,approve){
  const note=approve?"":prompt("Ret nedeni (opsiyonel):")||"";
  try{await api.admin.processReturn(id,approve,note);toast(approve?"İade onaylandı (refund + stok)":"İade reddedildi");render("returns");}
  catch(e){toast(e.message,true);}
}

// ── Kargo ──
// ══ DALGA C / C6b - "KARGOLANMAYI BEKLEYENLER" LISTESI ═══════════════════════════════════
// OLCULEN ONCE-DURUM: Kargo ekrani KOR BIR FORMDU - operatorden siparis ID'si ELLE isteniyor,
// hangi siparisin kargolanmayi bekledigini gosteren HICBIR liste yok. Operator ID'yi baska
// ekrandan bulup kopyalamak zorundaydi.
//
// HANGI DURUM "BEKLIYOR" - UYDURULMADI, DURUM MAKINESINDEN TURETILDI:
// OrderStatusMachine'e gore Shipped'e YALNIZ Preparing(2) gecebilir ve CreateShipment bu
// gecisi zaten dogruluyor. Yani liste = status 2. Baska bir durumu gostermek operatore
// ucun REDDEDECEGI siparisleri sunmak olurdu.
//
// BACKEND DEGISIKLIGI SIFIR: AdminOrderFilterDto'da `status` filtresi ZATEN var.
async function kargoBekleyenler(){
  try{
    const res=unwrap(await api.admin.allOrders({status:2,page:1,page_size:50}));
    const list=(res&&res.items)||[];
    document.getElementById("s_pending").innerHTML=list.length
      ? `<table><thead><tr><th>No</th><th>Müşteri</th><th>Tutar</th><th>Tarih</th><th></th></tr></thead><tbody>${
          list.map(o=>`<tr><td>${esc(o.order_number||String(o.id))}</td><td class=muted>#${o.customer_id}</td><td>${money(o.total_price)}</td><td class=muted>${new Date(o.created_at).toLocaleDateString("tr-TR")}</td>
            <td><button class="btn ghost sm" data-act="kargoAl" data-id="${o.id}">Kargo gir</button></td></tr>`).join("")
        }</tbody></table>`
      : '<p class="muted">Kargolanmayı bekleyen sipariş yok.</p>';
  }catch(e){
    // SESSIZ DEGIL: liste alinamadiysa operatore SOYLENIR (Dalga B'de kaldirilan sessiz
    // catch'in ayni tuzagina dusmemek icin) - ama ekranin kalani calismaya devam eder.
    document.getElementById("s_pending").innerHTML='<p class="muted">Bekleyen sipariş listesi alınamadı: '+esc(e.message||"hata")+'</p>';
  }
}
function kargoFormunaAl(id){
  document.getElementById("s_order").value=id;
  document.getElementById("s_track").focus();
  toast("Sipariş #"+id+" forma alındı - takip no gir");
}

async function renderShipments(el){
  el.innerHTML=`<div class="topbar"><h2>Kargo</h2><button class="btn ghost sm" data-act="git" data-gorunum="shipments">↻ Yenile</button></div>
    <div class="panel"><h3>Kargolanmayı Bekleyenler</h3>
      <p class="muted" style="margin-bottom:12px">Durumu <b>Hazırlanıyor</b> olan siparişler — kargoya verilebilecek tek durum budur.</p>
      <div id="s_pending"><p class="muted">Yükleniyor…</p></div>
    </div>
    <div class="panel" style="max-width:560px"><h3>Kargo Oluştur</h3>
      <label class="f"><span>Sipariş ID</span><input id="s_order" type="number"></label>
      <div class="grid2">
        <label class="f"><span>Kargo Firması</span><select id="s_carrier"><option value="0">Yurtiçi</option><option value="1">Aras</option><option value="2">MNG</option><option value="3">PTT</option><option value="4">Sürat</option></select></label>
        <label class="f"><span>Takip No</span><input id="s_track" placeholder="YT1234567890"></label>
      </div>
      <button class="btn" data-act="kargoOlustur">Kargo Oluştur (sipariş → Kargoda)</button>
    </div>
    <div class="panel" style="max-width:560px"><h3>Kargo Sorgula</h3>
      <label class="f"><span>Sipariş ID</span><input id="s_q" type="number"></label>
      <button class="btn ghost" data-act="kargoSorgu">Sorgula</button>
      <div id="s_result" style="margin-top:14px"></div>
    </div>`;
  await kargoBekleyenler();
}
async function createShipment(){
  const payload={order_id:parseInt(document.getElementById("s_order").value),carrier:parseInt(document.getElementById("s_carrier").value),tracking_number:document.getElementById("s_track").value.trim()};
  if(!payload.order_id||!payload.tracking_number)return toast("Sipariş ID + takip no girin",true);
  // DALGA C / C6b: basarili kargodan sonra bekleyenler listesi TAZELENIR - siparis artik
  // Kargoda durumuna gecti, listede kalmasi operatoru ayni siparisi ikinci kez girmeye
  // yoneltirdi (uc Conflict doner, ama once bosuna is yapilmis olur).
  try{
    await api.admin.createShipment(payload);
    toast("Kargo oluşturuldu");
    document.getElementById("s_track").value="";
    document.getElementById("s_order").value="";
    await kargoBekleyenler();
  }catch(e){toast(e.message,true);}
}
async function queryShipment(){
  const id=parseInt(document.getElementById("s_q").value);if(!id)return;
  // DALGA B / B4: takip no ve firma/durum adlari DB'den geliyor - innerHTML'e girmeden once notrle.
  // Durum adi artik TURKCE geliyor (once ham enum adi "Preparing" yaziyordu).
  try{const s=unwrap(await api.admin.shipmentByOrder(id));document.getElementById("s_result").innerHTML=`<div class="card"><b>${esc(s.carrier_name)}</b> — ${esc(s.tracking_number)}<br><span class="badge warn">${esc(s.status_name)}</span> ${esc(s.last_status_text||"")}</div>`;}
  catch(e){document.getElementById("s_result").innerHTML='<p class=muted>'+esc(e.message)+'</p>';}
}

// ── Kuponlar ──
// DALGA B / B1 - ALAN ADI UYUSMAZLIGI (uc parca, hepsi CANLI olculdu):
//   (a) EKLEME: panel "discount_value" gonderiyordu, CouponAddRequestDto alani "value".
//       Sonuc: %30 girilen kupon veritabanina value=0 olarak yaziliyor, uc 201 doneyor,
//       panel "Kupon eklendi" diyor ve musteri sepette "Kupon gecerli" + indirim 0.00
//       goruyordu. Her katman BASARILI diyordu; indirim yoktu.
//   (b) LISTE DEGERI: panel "c.discount_value" okuyordu -> undefined -> her satirda "-".
//   (c) LISTE TIPI: liste ucu discount_type'i ENUM ADI (metin) olarak doner
//       (CouponProfile: ((DiscountTypeEnum)src.discount_type).ToString()), panel ise
//       SAYIYLA karsilastiriyordu. "Percentage"==0 -> false, "Percentage"==1 -> false,
//       dolayisiyla UCUNCU dala dusup HER kupon "Kargo" gorunuyordu - eksik bilgi degil,
//       YANLIS bilgi: yuzde kuponu ucretsiz kargo kuponu gibi okunuyordu.
// EKLEME ucu SAYI bekler, LISTE ucu METIN doner - ikisi ayni alan adini farkli tipte
// tasidigi icin panel her iki gosterimi de tanir; tek merkez asagisi.
const KUPON_TIPI={"0":"Yüzde","Percentage":"Yüzde","1":"Sabit","Fixed":"Sabit","2":"Ücretsiz Kargo","FreeShipping":"Ücretsiz Kargo"};
function kuponTipEtiket(t){return KUPON_TIPI[String(t)]||esc(t==null?"—":t);}
function kuponDegerMetni(c){
  const t=String(c.discount_type);
  if(t==="2"||t==="FreeShipping") return "—";            // kargo kuponunda deger anlamsiz
  const v=Number(c.value);
  if(!isFinite(v)) return "—";
  // ONEMLI: v===0 icin "-" YAZILMAZ. Eski panel bunu gizliyordu; sifir degerli bir kupon
  // BOZUK bir kupondur ve operatorun gormesi gereken sey tam olarak budur.
  return (t==="0"||t==="Percentage") ? ("%"+v.toLocaleString("tr-TR",{maximumFractionDigits:2})) : money(v);
}
async function renderCoupons(el){
  const coupons=unwrap(await api.admin.listCoupons())||[];
  el.innerHTML=`<div class="topbar"><h2>Kuponlar (${coupons.length})</h2><button class="btn" data-act="kuponFormu">+ Yeni Kupon</button></div>
    <div class="panel"><table><thead><tr><th>Kod</th><th>Tip</th><th>Değer</th><th>Min. Sepet</th><th>Kullanım</th><th></th></tr></thead><tbody>
      ${coupons.map(c=>`<tr><td><b>${esc(c.code)}</b></td><td class=muted>${kuponTipEtiket(c.discount_type)}</td><td>${kuponDegerMetni(c)}</td><td>${money(c.min_amount||0)}</td><td class=muted>${(c.used_count||0)}${c.usage_limit>0?" / "+c.usage_limit:" / ∞"}</td>
        <td><button class="btn danger sm" data-act="kuponSil" data-id="${c.id}">Sil</button></td></tr>`).join("")||'<tr><td colspan=6 class=muted>Kupon yok</td></tr>'}
    </tbody></table></div>`;
}
function couponForm(){
  document.getElementById("views").innerHTML=`<div class="topbar"><h2>Yeni Kupon</h2><button class="btn ghost sm" data-act="git" data-gorunum="coupons">← Geri</button></div>
    <div class="panel" style="max-width:480px">
      <label class="f"><span>Kod</span><input id="c_code" placeholder="HOSGELDIN"></label>
      <div class="grid2">
        <label class="f"><span>İndirim Tipi</span><select id="c_type"><option value="0">Yüzde (%)</option><option value="1">Sabit (₺)</option><option value="2">Ücretsiz Kargo</option></select></label>
        <label class="f"><span>Değer</span><input id="c_val" type="number" step="0.01"></label>
      </div>
      <label class="f"><span>Min. Sepet (₺)</span><input id="c_min" type="number" step="0.01" value="0"></label>
      <button class="btn" data-act="kuponKaydet">Kaydet</button>
    </div>`;
}
async function saveCoupon(){
  const tip=parseInt(document.getElementById("c_type").value);
  const deger=parseFloat(document.getElementById("c_val").value)||0;
  // ALAN ADI: "value" (CouponAddRequestDto). Eski "discount_value" DTO'da YOK, sessizce
  // yok sayiliyordu. "is_active" de DTO'da yok - sunucu ekleme aninda zaten true yaziyor;
  // gonderilmesi hicbir sey yapmiyordu, kaldirildi.
  const payload={code:document.getElementById("c_code").value.trim().toUpperCase(),discount_type:tip,value:deger,min_amount:parseFloat(document.getElementById("c_min").value)||0};
  if(!payload.code)return toast("Kod girin",true);
  // GIRIS KAPISI: yuzde/sabit kuponda 0 deger ANLAMSIZDIR - musteriye "kupon gecerli"
  // denip 0 indirim uygulanmasi tam olarak B1'in zarariydi. Sunucu 0'i reddetmiyor
  // (yalniz negatif ve %100 ustu reddediliyor); bu yuzden kapi burada.
  if(tip!==2 && deger<=0) return toast("Yüzde/sabit kuponda değer 0'dan büyük olmalı",true);
  if(tip===0 && deger>100) return toast("Yüzde indirim 100'den büyük olamaz",true);
  try{await api.admin.addCoupon(payload);toast("Kupon eklendi");render("coupons");}catch(e){toast(e.message,true);}
}
async function delCoupon(id){if(!confirm("Kupon silinsin mi?"))return;try{await api.admin.deleteCoupon(id);toast("Silindi");render("coupons");}catch(e){toast(e.message,true);}}


// ── Stok yönetimi (E4a) ──────────────────────────────────────────────────────
// Operatör bugüne kadar stok düzeltmesini panelden YAPAMIYORDU (uç vardı, ekran yoktu).
// KRİTİK ayrım: fiziksel stok ≠ satılabilir. Rezerve edilmiş adet fiziksel stokta DURUR
// ama satılamaz; bu yüzden üç sütun da gösteriliyor (admin ucu GET /api/Stock/{id}).
let stockState = { productId: null, size: null, rows: [] };

// HTML kaçışı - ürün adı/beden veritabanından geliyor, innerHTML'e girmeden önce nötrle.
function esc(s){return String(s==null?"":s).replace(/[&<>"']/g,c=>({"&":"&amp;","<":"&lt;",">":"&gt;",'"':"&quot;","'":"&#39;"}[c]));}

// ══ GF-2a / K1 (D-1) - FAIL-CLOSED SANITIZE SARMALAYICI ════════════════════════════
//
// KAPSAM VE DURUST SINIR: bugun panelde SUNUCUDAN GELEN HTML CIZEN bir yuzey YOK -
// 32 `innerHTML` yaziminin tamami sablon literali + `esc()`. Yani bu sarmalayici BUGUN
// bir kusuru KAPATMIYOR; panele HTML cizen bir yuzey EKLENDIGINDE hazir olmasi ve
// storefront'takiyle AYNI sozlesmeyi tasimasi icin konuyor. Raporda bu acikca yazildi -
// "sarmalayici eklendi, cagiran yok" durumu GIZLENMIYOR.
//
// FAIL-CLOSED: purify yuklenmediyse `null` doner ve cagiran HAM HTML BASMAZ. Bu,
// `api-bridge.js:2613 guvenliHTML` ile AYNI sozlesmedir - ikinci bir POLITIKA degil,
// ayni politikanin panele tasinmis hali (panel `api-bridge.js`i yuklemiyor).
function guvenliHTML(ham){
  if (typeof window.DOMPurify === "undefined" || !window.DOMPurify.sanitize) {
    console.error("DOMPurify yuklenmedi - HTML icerik CIZILMEDI (fail-closed).");
    return null;
  }
  return window.DOMPurify.sanitize(ham || "", {
    ALLOWED_TAGS: ["h1","h2","h3","h4","p","br","hr","strong","b","em","i","u",
                   "ul","ol","li","a","span","div","table","thead","tbody","tr","th","td","small"],
    ALLOWED_ATTR: ["href","title","colspan","rowspan","class"],
    ALLOWED_URI_REGEXP: /^(?:https?:|mailto:|tel:|#|\/)/i
  });
}
// Cizim yardimcisi: null gelirse (purify yok) icerik YAZILMAZ, uyari metni yazilir.
//
// ══ BRIDGE SURUMUNDEN UC NOKTADA AYRISIR - BILINCLI, DENETCI OLCTU ══════════════════
// `api-bridge.js`teki `guvenliYaz(el, ham, hataMetni)` ile karsilastirildiginda:
//   (1) ARITE 2 vs 3 - panelde cagiranin ozel hata metni gecirecegi bir yol YOK.
//   (2) FAIL-CLOSED DALI `textContent` vs `innerHTML` - bridge surumu `hataMetni`yi
//       KACISSIZ `innerHTML`e koyuyor; burasi BILEREK daha dar (metin dugumu).
//   (3) METIN KAYNAGI sabit literal vs `ceviri(...)` - `ceviri()` panelde TANIMLI DEGIL.
// POLITIKA (`guvenliHTML`) IKISINDE DE OZDES - onemli olan odur. Ayrisan sey CIZIM
// yardimcisidir ve bu surum STRICT OLARAK DAHA GUVENLIDIR. Sessiz kalmasin diye yazildi.
function guvenliYaz(el, ham){
  const temiz = guvenliHTML(ham);
  if (temiz === null) { el.textContent = "Icerik guvenlik dogrulamasindan gecemedi."; return false; }
  el.innerHTML = temiz;
  return true;
}

// Görsel URL'i API tabanına göre çöz. Backend, Storage:PublicBaseUrl boşken GÖRELİ URL
// döndürüyor ("/uploads/products/x.png"). Panel API'den AYRI origin'de çalıştığında bu
// göreli adres panelin kendi origin'ine çözülür ve 404 verir (ölçüldü: 5173 → 404).
// Mutlak URL geldiğinde (CDN/PublicBaseUrl ayarlı) olduğu gibi kullanılır.
function imgUrl(u){
  u=String(u||"");
  return /^https?:\/\//i.test(u) ? u : API_BASE.replace(/\/+$/,"")+(u.startsWith("/")?u:"/"+u);
}

async function renderStock(el){
  const products=unwrap(await api.products.list())||[];
  el.innerHTML=`
    <div class="topbar"><h2>Stok Yönetimi</h2><button class="btn ghost sm" data-act="git" data-gorunum="stock">↻ Yenile</button></div>
    <div class="panel" style="max-width:760px">
      <label class="f"><span>Ürün</span>
        <select id="st_product" data-act="stokYukle">
          <option value="">— ürün seçin —</option>
          ${products.map(p=>`<option value="${p.id}">#${p.id} · ${esc(p.name)}</option>`).join("")}
        </select>
      </label>
      <div id="st_rows"><p class="muted">Ürün seçildiğinde beden bazında stok listelenir.</p></div>
    </div>
    <div class="panel hidden" id="st_form" style="max-width:760px">
      <h3>Düzeltme — <span id="st_label" class="muted"></span></h3>
      <div class="grid2">
        <label class="f"><span>Değişim (+ giriş / − çıkış)</span>
          <input id="st_delta" type="number" step="1" value="0" data-act="stokOnizle"></label>
        <label class="f"><span>Sonuç (yeni fiziksel stok)</span>
          <input id="st_result" disabled value="—"></label>
      </div>
      <label class="f"><span>Sebep (denetim izine yazılır)</span>
        <input id="st_note" placeholder="Yeni sevkiyat / sayım düzeltmesi / hasarlı iade"></label>
      <p class="muted" id="st_warn"></p>
      <button class="btn" data-act="stokUygula">Uygula</button>
    </div>`;
}

async function loadStock(){
  const id=parseInt(document.getElementById("st_product").value)||0;
  const box=document.getElementById("st_rows");
  document.getElementById("st_form").classList.add("hidden");
  stockState={productId:id,size:null,rows:[]};
  if(!id){box.innerHTML='<p class="muted">Ürün seçildiğinde beden bazında stok listelenir.</p>';return;}
  try{
    const rows=unwrap(await api.stock.byProduct(id))||[];
    stockState.rows=rows;
    if(!rows.length){box.innerHTML='<p class="muted">Bu ürünün aktif beden/stok satırı yok.</p>';return;}
    box.innerHTML=`<table><thead><tr><th>Beden</th><th>Fiziksel</th><th>Rezerve</th><th>Satılabilir</th><th></th></tr></thead><tbody>
      ${rows.map(r=>`<tr><td><b>${esc(r.size)}</b></td><td>${r.stock_quantity}</td>
        <td class="${r.reserved_quantity>0?"warn":"muted"}">${r.reserved_quantity}</td>
        <td class="${r.available<=0?"danger":"ok"}"><b>${r.available}</b></td>
        <td class=row-actions><button class="btn ghost sm" data-pick-size="${esc(r.size)}">Düzelt</button></td></tr>`).join("")}
    </tbody></table>`;
    /* ══ GF-2a / SUPHE-2 - HTML KACISI JS BAGLAMINDA COZULUYORDU ═══════════════════════
       ONCEKI HAL: onclick="pickSize('${esc(r.size)}')". `esc()` ' -> &#39; ceviriyordu ama
       tarayici oznitelik degerini ONCE HTML-cozuyor, SONRA JS olarak calistiriyor: &#39;
       yeniden ' oluyor ve tirnaktan CIKILIYOR. Kacis VARDI, BAGLAM YANLISTI.
       COZUM: deger `data-*` niteliginde DURUYOR (orada HTML kacisi DOGRU baglamdir) ve
       JS'e `dataset` uzerinden geciyor - hic ayristirilmiyor. Satir ici isleyici de
       kalkiyor, bu GF-2b'nin CSP sokumune de hizmet eder. */
    box.querySelectorAll("[data-pick-size]").forEach(function (b) {
      b.addEventListener("click", function () { pickSize(b.dataset.pickSize); });
    });
  }catch(e){
    // 403 = admin değil; ayırt edilebilir mesaj (401 "oturum yok" ile karıştırılmasın)
    box.innerHTML='<p class="muted">Stok okunamadı: '+esc(e.message||"hata")+'</p>';
  }
}

function pickSize(size){
  stockState.size=size;
  const row=stockState.rows.find(r=>r.size===size);
  if(!row)return;
  document.getElementById("st_form").classList.remove("hidden");
  document.getElementById("st_label").textContent=
    `beden ${size} · fiziksel ${row.stock_quantity} · rezerve ${row.reserved_quantity} · satılabilir ${row.available}`;
  document.getElementById("st_delta").value=0;
  document.getElementById("st_note").value="";
  previewStock();
}

// Panelde operatör FARK giriyor; uç MUTLAK yeni değer istiyor (StockAdjustDto.new_quantity).
// Çeviri burada yapılır ve sonuç gönderilmeden ÖNCE ekranda gösterilir - operatör ne
// yazılacağını görmeden onaylamasın.
function previewStock(){
  const row=stockState.rows.find(r=>r.size===stockState.size);
  if(!row)return;
  const delta=parseInt(document.getElementById("st_delta").value)||0;
  const next=row.stock_quantity+delta;
  document.getElementById("st_result").value=next;
  const w=document.getElementById("st_warn");
  if(next<0) w.innerHTML='<span class="danger">Negatif stok olamaz — uç 400 döner.</span>';
  else if(next<row.reserved_quantity) w.innerHTML=`<span class="danger">Rezerve (${row.reserved_quantity}) altına inilemez — uç 400 döner.</span>`;
  else w.textContent="";
}

async function applyStock(){
  const row=stockState.rows.find(r=>r.size===stockState.size);
  if(!row)return toast("Beden seçin",true);
  const delta=parseInt(document.getElementById("st_delta").value)||0;
  if(delta===0)return toast("Değişim 0 - yapacak bir şey yok",true);
  const note=document.getElementById("st_note").value.trim();
  if(!note)return toast("Sebep zorunlu (denetim izi)",true);
  try{
    await api.stock.adjust(stockState.productId,row.size,row.stock_quantity+delta,note);
    toast("Stok güncellendi");
    await loadStock();
  }catch(e){toast(e.message||"Düzeltme başarısız",true);}
}

// ── Ürün görselleri (E4a) ────────────────────────────────────────────────────
// Yükleme ucu: POST /api/product-image/upload (TİRELİ route), multipart + Bearer.
// Backend savunmaları: MIME beyaz listesi + magic-byte imzası + 5 MB sınır; dosya adı
// istemciden ALINMAZ (Guid + doğrulanmış content-type'tan uzantı).
async function renderImages(el){
  const products=unwrap(await api.products.list())||[];
  el.innerHTML=`
    <div class="topbar"><h2>Ürün Görselleri</h2><button class="btn ghost sm" data-act="git" data-gorunum="images">↻ Yenile</button></div>
    <div class="panel" style="max-width:760px">
      <label class="f"><span>Ürün</span>
        <select id="im_product" data-act="gorselListe">
          <option value="">— ürün seçin —</option>
          ${products.map(p=>`<option value="${p.id}">#${p.id} · ${esc(p.name)}</option>`).join("")}
        </select>
      </label>
      <label class="f"><span>Dosya (birden fazla seçilebilir · jpg/png/webp · en fazla 5 MB)</span>
        <input id="im_file" type="file" accept="image/jpeg,image/png,image/webp" multiple></label>
      <label class="f" style="flex-direction:row;align-items:center;gap:8px">
        <input id="im_primary" type="checkbox"><span>İlk yüklenen görsel birincil olsun</span></label>
      <button class="btn" data-act="gorselYukle">Yükle</button>
      <p class="muted" id="im_status"></p>
    </div>
    <div class="panel" style="max-width:760px"><h3>Mevcut görseller</h3>
      <div id="im_list"><p class="muted">Ürün seçin.</p></div></div>`;
}

async function loadImages(){
  const id=parseInt(document.getElementById("im_product").value)||0;
  const box=document.getElementById("im_list");
  if(!id){box.innerHTML='<p class="muted">Ürün seçin.</p>';return;}
  try{
    const imgs=unwrap(await api.productImage.byProduct(id))||[];
    if(!imgs.length){box.innerHTML='<p class="muted">Bu ürünün görseli yok.</p>';return;}
    box.innerHTML=`<div style="display:flex;flex-wrap:wrap;gap:12px">
      ${imgs.map(i=>`<div style="width:150px">
        <img src="${esc(imgUrl(i.image_url))}" alt="" style="width:150px;height:150px;object-fit:cover;border-radius:8px;background:#222">
        <div class="muted" style="font-size:12px;margin-top:4px">
          #${i.id} ${i.is_primary?'<span class="ok">· birincil</span>':""}</div>
        <div class=row-actions style="margin-top:4px">
          ${i.is_primary?"":`<button class="btn ghost sm" data-act="gorselBirincil" data-id="${i.id}">Birincil yap</button>`}
          <button class="btn danger sm" data-act="gorselSil" data-id="${i.id}">Sil</button></div>
      </div>`).join("")}</div>`;
  }catch(e){box.innerHTML='<p class="muted">Görseller okunamadı: '+esc(e.message||"hata")+'</p>';}
}

async function uploadImages(){
  const id=parseInt(document.getElementById("im_product").value)||0;
  if(!id)return toast("Ürün seçin",true);
  const input=document.getElementById("im_file");
  const files=Array.from(input.files||[]);
  if(!files.length)return toast("Dosya seçin",true);
  const wantPrimary=document.getElementById("im_primary").checked;
  const status=document.getElementById("im_status");
  let ok=0,fail=0;
  // SIRAYLA yükle: paralel gönderim "birincil" yarışına girer (her istek diğerlerinin
  // birincilini sıfırlayabilir) ve hata mesajı hangi dosyaya ait belirsizleşir.
  for(let k=0;k<files.length;k++){
    status.textContent=`Yükleniyor ${k+1}/${files.length}: ${files[k].name}`;
    try{
      await api.productImage.upload(id,files[k],wantPrimary&&k===0);
      ok++;
    }catch(e){
      fail++;
      toast(files[k].name+": "+(e.message||"yüklenemedi"),true);
    }
  }
  status.textContent=`Bitti — ${ok} başarılı, ${fail} başarısız.`;
  input.value="";
  await loadImages();
}

async function makePrimary(imageId){
  try{await api.productImage.setPrimary(imageId);toast("Birincil güncellendi");await loadImages();}
  catch(e){toast(e.message,true);}
}
async function delImage(imageId){
  if(!confirm("Görsel silinsin mi?"))return;
  try{await api.productImage.remove(imageId);toast("Silindi");await loadImages();}
  catch(e){toast(e.message,true);}
}

// ══ GF-2b / K5 - SATIR ICI HANDLER YOK: TEK DELEGE DINLEYICI ═══════════════════════
//
// NEDEN: panelin butonlari `onclick="..."` tasiyordu ve bunlarin calisabilmesi icin
// admin CSP'sinde `script-src 'unsafe-inline'` ACIK kalmak zorundaydi - yani bir XSS
// bulunsaydi enjekte edilen her satir ici script de calisirdi. Handler'lar `data-act`
// ozniteligine tasindi, dinleme TEK NOKTADA yapiliyor ve `'unsafe-inline'` KALKTI.
//
// EYLEM TABLOSU BIR BEYAZ LISTEDIR - BILINCLI. `data-act` degeri fonksiyon adina
// DOGRUDAN cevrilmiyor: `window[el.dataset.act]()` yazmak, DOM'a oznitelik
// yazabilen bir saldirgana KEYFI global fonksiyon cagirma yetkisi verirdi ve
// `'unsafe-inline'`i kaldirmakla kazanilan sey geri verilirdi. Yalniz burada ADI
// GECEN eylemler calisir; tanimsiz bir `data-act` SESSIZCE yok sayilir.
//
// DELEGASYON `document` UZERINDE: panel govdesi `innerHTML` ile SIK SIK yeniden
// ciziliyor (her `render()` cagrisinda). Dinleyici tek tek dugumlere baglansaydi her
// cizimden sonra YENIDEN baglanmasi gerekirdi ve bir cizim yolu unutulunca o ekran
// SESSIZCE olu dugmelerle acilirdi.
const PANEL_EYLEM = {
  login: () => doLogin(),
  logout: () => doLogout(),
  git: (el) => render(el.dataset.gorunum),
  urunFormuYeni: () => productForm(),
  urunFormu: (el) => productForm({ id: Number(el.dataset.id) }),
  urunSil: (el) => delProduct(Number(el.dataset.id)),
  // `saveProduct(null)` = YENI urun. Duzenlemede id dolu gelir; bos dize null'a duser.
  urunKaydet: (el) => saveProduct(el.dataset.id ? Number(el.dataset.id) : null),
  bedenEkle: () => { urunStokSatirlari.push({ size: "", stock_quantity: 0 }); stokSatirlariniCiz(); },
  bedenKaldir: () => { urunStokSatirlari.pop(); stokSatirlariniCiz(); },
  siparisDurum: () => changeOrderStatus(),
  fatura: () => genInvoice(),
  iade: (el) => procReturn(Number(el.dataset.id), el.dataset.onay === "1"),
  kargoAl: (el) => kargoFormunaAl(Number(el.dataset.id)),
  kargoOlustur: () => createShipment(),
  kargoSorgu: () => queryShipment(),
  kuponFormu: () => couponForm(),
  kuponSil: (el) => delCoupon(Number(el.dataset.id)),
  kuponKaydet: () => saveCoupon(),
  stokUygula: () => applyStock(),
  gorselYukle: () => uploadImages(),
  gorselBirincil: (el) => makePrimary(Number(el.dataset.id)),
  gorselSil: (el) => delImage(Number(el.dataset.id)),
};

// Girdi olaylari AYRI tabloda: `click` tablosuyla birlestirilseydi bir `data-act`
// yanlis olay turunden de tetiklenebilirdi (orn. bir input'a tiklamak "Sil" calistirir).
const PANEL_GIRDI = {
  stokBeden: (el) => { urunStokSatirlari[Number(el.dataset.i)].size = el.value; },
  stokAdet: (el) => { urunStokSatirlari[Number(el.dataset.i)].stock_quantity = parseInt(el.value, 10) || 0; },
  stokOnizle: () => previewStock(),
};
const PANEL_DEGISIM = {
  stokYukle: () => loadStock(),
  gorselListe: () => loadImages(),
};

function panelOlayBagla(tur, tablo) {
  document.addEventListener(tur, (ev) => {
    // `closest`: tiklama dugmenin ICINDEKI <span>/<b> uzerine dusebilir.
    const el = ev.target && ev.target.closest ? ev.target.closest("[data-act]") : null;
    if (!el) return;
    const islev = Object.prototype.hasOwnProperty.call(tablo, el.dataset.act)
      ? tablo[el.dataset.act] : null;
    if (!islev) return;   // bu olay turunde tanimsiz -> SESSIZCE yok say
    islev(el);
  });
}
panelOlayBagla("click", PANEL_EYLEM);
panelOlayBagla("input", PANEL_GIRDI);
panelOlayBagla("change", PANEL_DEGISIM);

// ── Başlangıç: token varsa doğrudan panele ──
if(api.isLoggedIn()) showApp();
