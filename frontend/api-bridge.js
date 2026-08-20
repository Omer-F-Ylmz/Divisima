/*
 * Divisima API Köprüsü (api-bridge.js)
 * -------------------------------------
 * index.html'i (mock veriyle çalışan storefront) gerçek backend'e bağlar.
 * </body> öncesine api-client.js'ten SONRA eklenir.
 *
 * E1 KAPSAMI: AUTH + KATALOG gerçek uçlara bağlandı, MOCK/STATİK VERİ YOLLARI KAPATILDI.
 * Sepet/checkout/kupon kablolaması E2'ye ait; aşağıda olduğu gibi duruyor.
 *
 * E1'DE ÖLÇÜLEN VE DÜZELTİLEN ÜÇ SÖZLEŞME HATASI (tahmin değil, koda bakılıp canlı doğrulandı):
 *  1) Katalog GET /api/product/getlist ile çekiliyordu; o uç [RequireUserType(Admin)].
 *     Anonim ziyaretçi 403 alıyor, köprü her seferinde sessizce MOCK veriye düşüyordu -
 *     yani "API'ye bağlı" görünen storefront hiçbir zaman gerçek ürün göstermedi.
 *     Anonim katalog yolu POST /api/product/filter.
 *  2) Görsel URL'leri göreli geliyor ("/uploads/..."); storefront ayrı origin'de olduğunda
 *     kendi origin'ine çözülüp 404 veriyor. api.resolveUrl ile API tabanına çözülüyor.
 *  3) Arama parametresi "q" gönderiliyordu; uç "query" bekliyor (api-client.js'te düzeltildi).
 *
 * BOŞ KATALOG SÖZLEŞMESİ: API 0 ürün dönerse mock'a DÜŞÜLMEZ - kullanıcıya açık bir
 * "katalog boş" durumu gösterilir. API hata verirse "bağlanılamadı + tekrar dene" gösterilir.
 * Yalan veri göstermek, boş vitrin göstermekten kötüdür.
 */
(function () {
  "use strict";

  // ── Yapılandırma ──
  var API_BASE = window.DIVISIMA_API_BASE || "http://localhost:5000";
  var api = new DivisimaAPI(API_BASE);
  window.divisimaApi = api; // konsoldan erişim

  // Sayfa boyutu bilinçli olarak KÜÇÜK: liste yolu stok/beden döndürmediği için her ürünün
  // detayı ayrıca çekiliyor (aşağıdaki enrichAll). 24 ürün = 24 detay çağrısı, kabul edilebilir.
  // Backend liste yolunu doldurduğu gün bu telafi kaldırılır ve sayfa boyutu serbest kalır.
  var CATALOG_PAGE_SIZE = 24;
  var ENRICH_CONCURRENCY = 6;

  // ── Yardımcılar ──
  function notify(msg) {
    try { if (typeof window.toast === "function") window.toast(msg); else console.log("Divisima:", msg); } catch (e) {}
  }
  function unwrap(r) { return (r && r.data !== undefined) ? r.data : r; }

  // HTML kaçışı. index.html kendi esc()'sini tanımlıyor; yüklenme sırası değişirse diye
  // burada da bir tane var - sunucudan gelen metin innerHTML'e kaçışsız GİRMEZ.
  function esc(s) {
    return String(s == null ? "" : s).replace(/[&<>"']/g, function (c) {
      return { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c];
    });
  }

  // Zarf toleransı: /product/filter "items/total_count" (küçük harf) döner,
  // /search/products ise PagedResult<T> -> camelCase "items/totalCount". İkisi de kabul.
  function pageItems(res) {
    var d = unwrap(res);
    if (!d) return [];
    if (Array.isArray(d)) return d;
    return d.items || d.Items || [];
  }

  function slugify(s) {
    if (!s) return "";
    return s.toString().toLowerCase()
      .replace(/ç/g, "c").replace(/ğ/g, "g").replace(/ı/g, "i")
      .replace(/ö/g, "o").replace(/ş/g, "s").replace(/ü/g, "u")
      .replace(/[^a-z0-9]+/g, "-").replace(/^-+|-+$/g, "");
  }

  // ── Backend ürün DTO → frontend ürün şekli ──
  // Frontend bekliyor: {id,name,brand,cat,sub,price,old,stock,sizes,col,img}
  //
  // ÖLÇÜLEN BACKEND BOŞLUĞU: storefront liste yolu (POST /api/product/filter)
  // category_name / total_stock / sizes alanlarını DOLDURMUYOR (ProductProfile bu üçünü
  // Ignore ediyor, admin GetList sizes'ı sonradan dolduruyor ama filter yolu doldurmuyor).
  // Yani ham liste verisiyle her ürün "kategorisiz + 0 stok + bedensiz" görünür - vitrin
  // baştan sona "Tükendi" olur. İstemci tarafı telafi:
  //   - kategori: category_id + /api/category/getlist ile çözülür (tam çözüm)
  //   - stok/beden: detay ucundan (aşağıda enrichVisible) - kısmi, N+1 sınırlı
  // Kalıcı düzeltme backend'de; raporda ŞÜPHELİ olarak duruyor.
  function categorySlugOf(p) {
    if (p.category_name) return slugify(p.category_name);
    var c = CATEGORY_BY_ID[p.category_id];
    if (c) return c.slug || slugify(c.name);
    return "tumu";
  }

  function mapProduct(p) {
    return {
      id: p.id,
      name: p.name,
      brand: p.brand || "Divisima",
      cat: categorySlugOf(p),
      sub: "",
      price: Number(p.price) || 0,
      old: p.old_price ? Number(p.old_price) : 0,
      cart: Number(p.price) || 0,
      stock: Number(p.total_stock) || 0,   // liste yolu 0 döner; enrichAll detaydan düzeltir
      sizes: (p.sizes && p.sizes.length) ? p.sizes.map(function (s) { return isNaN(+s) ? s : +s; }) : [],
      col: p.color_hex || "#cccccc",
      img: api.resolveUrl(p.image_url) // göreli URL'i API tabanına çöz (yoksa "" - frontend placeholder üretir)
    };
  }

  // ── PRODUCTS'ı YERİNDE değiştir (tüm closure'lar aynı diziyi görsün) ──
  function replaceProducts(mapped) {
    if (typeof window.PRODUCTS === "undefined") { window.PRODUCTS = []; }
    window.PRODUCTS.length = 0;
    Array.prototype.push.apply(window.PRODUCTS, mapped);
  }

  function rerender() {
    try {
      var g = document.getElementById("grid");
      if (g && typeof window.cardHTML === "function") {
        g.innerHTML = window.PRODUCTS.slice(0, 8).map(window.cardHTML).join("");
      }
      // grid2 sabit "BEST" id listesiyle çiziliyor; API kataloğunda o id'ler olmayabilir -
      // byId null döner ve cardHTML boş string verir. Bu yüzden mevcut ürünlerden dolduruyoruz.
      var g2 = document.getElementById("grid2");
      if (g2 && typeof window.cardHTML === "function") {
        g2.innerHTML = window.PRODUCTS.slice(8, 16).map(window.cardHTML).join("");
      }
      // Her çizici AYRI sarılır: index.html'deki bir çizicinin DOM'u eksik olduğunda
      // (ör. o bölüm sayfada yoksa) diğerleri ATLANMAMALI. Tek try kullanılınca ilk
      // hata kalan çizimleri sessizce iptal ediyordu (ölçüldü).
      ["renderCatGrid", "renderRecent", "renderIgFeed"].forEach(function (fn) {
        try { if (typeof window[fn] === "function") window[fn](); }
        catch (e) { console.warn("Divisima: " + fn + " çizilemedi", e && e.message); }
      });
      if (location.hash.indexOf("#/kategori") === 0 && typeof window.router === "function") window.router();
    } catch (e) { console.warn("Divisima: grid yeniden çizilemedi", e); }
  }

  // ── Katalog durum ekranı (boş / hata) ──────────────────────────────────────
  // Mock'a düşmek YOK: kullanıcı gerçeği görür.
  function showCatalogState(title, detail, withRetry) {
    var html =
      '<div style="grid-column:1/-1;padding:48px 16px;text-align:center;color:#6b6b6b">' +
      '<div style="font-size:32px;margin-bottom:10px">◍</div>' +
      '<div style="font-weight:600;color:#1a1a1a;margin-bottom:6px"></div>' +
      '<div style="font-size:13px"></div>' +
      (withRetry ? '<button id="dvsRetry" style="margin-top:14px;padding:9px 16px;border:1px solid #e8e4de;border-radius:8px;background:#fff;cursor:pointer">Tekrar dene</button>' : "") +
      "</div>";
    ["grid", "grid2", "catGrid"].forEach(function (id) {
      var el = document.getElementById(id);
      if (!el) return;
      if (id === "grid") {
        el.innerHTML = html;
        // Metinleri textContent ile yaz - innerHTML'e kullanıcı/sunucu metni gömmüyoruz.
        var t1 = el.querySelector("div > div:nth-child(2)");
        var t2 = el.querySelector("div > div:nth-child(3)");
        if (t1) t1.textContent = title;
        if (t2) t2.textContent = detail;
        var rb = document.getElementById("dvsRetry");
        if (rb) rb.onclick = function () { loadCatalog(); };
      } else {
        el.innerHTML = "";
      }
    });
  }

  // ── Kategoriler (gerçek API) ───────────────────────────────────────────────
  var CATEGORIES = [];
  var CATEGORY_BY_ID = {};

  // index.html'in çeviri tablosu T["cat_<slug>"] = [tr, en] bekliyor; karşılığı yoksa
  // t() ANAHTARIN KENDİSİNİ basıyor ("cat_e4a-kategori" gibi - ölçüldü). Statik tabloda
  // yalnız mock kategoriler var, gerçek kategoriler yok. API'den gelenleri tabloya ekliyoruz.
  function registerCategoryLabels() {
    try {
      if (typeof window.T !== "object" || !window.T) return;
      CATEGORIES.forEach(function (c) {
        var key = "cat_" + (c.slug || slugify(c.name));
        if (!window.T[key]) window.T[key] = [c.name, c.name];
        if (typeof window.AR === "object" && window.AR && window.AR[key] === undefined) window.AR[key] = c.name;
      });
    } catch (e) { console.warn("Divisima: kategori etiketleri eklenemedi", e); }
  }
  async function loadCategories() {
    try {
      var res = await api.categories.list();
      CATEGORIES = unwrap(res) || [];
      CATEGORY_BY_ID = {};
      var bySlug = {};
      CATEGORIES.forEach(function (c) {
        CATEGORY_BY_ID[c.id] = c;
        bySlug[c.slug || slugify(c.name)] = c.id;
      });
      window.divisimaCategories = CATEGORIES;
      window.divisimaCategoryIdBySlug = bySlug;   // kategori sayfası gerçek id ile sorgulayabilsin
      registerCategoryLabels();
    } catch (e) {
      CATEGORIES = [];
      CATEGORY_BY_ID = {};
      console.warn("Divisima: kategoriler alınamadı", e);
    }
  }

  // ── Ürünler (gerçek API - ANONİM yol) ──────────────────────────────────────
  // Liste yolunun döndürmediği stok/beden/açıklamayı detay ucundan tamamla.
  // Sınırlı eşzamanlılık: tarayıcı bağlantı havuzunu ve sunucuyu boğmadan.
  async function enrichAll(products) {
    var queue = products.slice();
    async function worker() {
      while (queue.length) {
        var p = queue.shift();
        await enrichProduct(p.id);
      }
    }
    var workers = [];
    for (var i = 0; i < Math.min(ENRICH_CONCURRENCY, products.length); i++) workers.push(worker());
    await Promise.all(workers);
  }

  async function loadCatalog(filter) {
    try {
      // sort/sizes/colors ZORUNLU: DTO'da non-nullable oldukları için eksik gönderilirse
      // uç 400 "The sort field is required." döner (ölçüldü - katalog hiç yüklenmiyordu).
      var payload = Object.assign(
        { page: 1, size: CATALOG_PAGE_SIZE, sort: "new", sizes: [], colors: [] },
        filter || {}
      );
      var res = await api.products.filter(payload);
      var list = pageItems(res);
      if (!list.length) {
        replaceProducts([]);           // mock KALMAZ
        showCatalogState("Katalog şu an boş", "Henüz yayınlanmış ürün yok. Yönetim panelinden ürün ekleyince burada görünür.", false);
        console.log("Divisima: API 0 ürün döndü - boş katalog durumu gösteriliyor");
        return [];
      }
      var mapped = list.map(mapProduct);
      replaceProducts(mapped);
      rerender();                       // ilk çizim (fiyat/ad/görsel hazır)
      await enrichAll(mapped);          // stok/beden/açıklama detaydan
      rerender();                       // gerçek stokla yeniden çiz
      console.log("Divisima: " + mapped.length + " ürün API'den yüklendi (detayla zenginleştirildi)");
      return mapped;
    } catch (e) {
      replaceProducts([]);             // mock KALMAZ - yalan vitrin gösterilmez
      showCatalogState("Ürünlere ulaşılamadı", (e && e.message) ? e.message : "Sunucuya bağlanılamadı.", true);
      console.warn("Divisima: katalog alınamadı", e);
      return [];
    }
  }
  window.divisimaReloadCatalog = loadCatalog;

  // ── Ürün detayı (gerçek API) ───────────────────────────────────────────────
  // Liste DTO'sunda açıklama yok; detay ucu description + beden/stok taşır.
  // openDetail senkron olduğu için: detayı çek, ürünü zenginleştir, sonra aç.
  var detailCache = {};
  async function enrichProduct(id) {
    if (detailCache[id]) return detailCache[id];
    try {
      var d = unwrap(await api.products.get(id));
      if (!d) return null;
      var p = (typeof window.byId === "function") ? window.byId(id) : null;
      if (p) {
        if (d.description) p.desc = d.description;
        if (d.image_url) p.img = api.resolveUrl(d.image_url);
        if (!p.cat || p.cat === "tumu") p.cat = categorySlugOf(d);
        if (d.stocks && d.stocks.length) {
          p.sizes = d.stocks.map(function (s) { return isNaN(+s.size) ? s.size : +s.size; });
          // storefront beden-stok'u p._ss'te tutuyor ve sizeStockOf() ONU ÖNBELLEĞE ALIYOR.
          // İlk çizim stok=0 iken yapıldığı için _ss "tüm bedenler 0" olarak donuyor ve
          // ürün sonsuza kadar "Stokta Yok" görünüyordu (ölçüldü). Gerçek haritayı yazıyoruz.
          var map = {}, total = 0;
          d.stocks.forEach(function (s) {
            var q = Number(s.stock_quantity) || 0;
            map[s.size] = q;
            total += q;
          });
          p._ss = map;
          p.stock = total;   // liste yolunun 0 döndürdüğü gerçek toplam stok
        }
      }
      detailCache[id] = d;
      return d;
    } catch (e) {
      console.warn("Divisima: ürün detayı alınamadı #" + id, e);
      return null;
    }
  }
  function wireProductDetail() {
    if (typeof window.openDetail !== "function") return;
    var orig = window.openDetail;
    window.openDetail = function (id) {
      if (detailCache[id]) return orig.call(window, id);
      // Önce mevcut (liste) veriyle aç - kullanıcı beklemesin; detay gelince yeniden aç.
      orig.call(window, id);
      enrichProduct(id).then(function (d) { if (d) orig.call(window, id); });
    };
  }

  // ── Arama (gerçek API) ─────────────────────────────────────────────────────
  // renderSearch/searchProducts SENKRON. Bu yüzden: sonuçları önbelleğe çek, sonra
  // renderSearch'i yeniden çalıştır. searchProducts önbelleği okur.
  var searchCache = { q: null, items: [] };
  async function fetchSearch(q) {
    try {
      var res = await api.search.products(q, { page: 1, size: 24 });
      var items = pageItems(res).map(mapProduct);
      // byId(id) çalışsın diye sonuçları PRODUCTS'a ekle (var olanları ezme)
      items.forEach(function (it) {
        if (typeof window.byId === "function" && !window.byId(it.id)) window.PRODUCTS.push(it);
      });
      searchCache = { q: q, items: items };
      return items;
    } catch (e) {
      console.warn("Divisima: arama başarısız", e);
      searchCache = { q: q, items: [] };
      return [];
    }
  }
  function wireSearch() {
    window.searchProducts = function (q) {
      var n = (q || "").trim();
      if (!n) return [];
      return (searchCache.q === n) ? searchCache.items : [];
    };
    if (typeof window.renderSearch === "function") {
      var orig = window.renderSearch;
      window.renderSearch = function (q) {
        var n = (q || "").trim();
        if (!n || searchCache.q === n) return orig.call(window, q);
        orig.call(window, q);                       // ara durum (boş sonuç ekranı)
        fetchSearch(n).then(function () {
          // Kullanıcı yazmaya devam ettiyse eski sorgunun sonucunu ÇİZME
          var cur = document.getElementById("searchInput");
          if (!cur || (cur.value || "").trim() === n) orig.call(window, q);
        });
      };
    }
  }

  // ── Auth (gerçek API) ──────────────────────────────────────────────────────
  function wireAuth() {
    window.divisimaAuth = {
      async login(email, pass) {
        var r = await api.auth.login(email, pass);
        var d = unwrap(r) || {};
        window.loggedIn = true;
        if (typeof window.login === "function") window.login(d.name || String(email).split("@")[0]);
        return r;
      },
      async register(payload) { return api.auth.register(payload); },
      async verifyEmail(token) { return api.auth.verifyEmail(token); },
      async resend(email) { return api.auth.resendVerification(email); },
      async logout() {
        try { await api.auth.logout(); } finally {
          window.loggedIn = false;
          if (typeof window.logout === "function") window.logout();
        }
      },
      isLoggedIn: function () { return api.isLoggedIn(); },
      // Otomatik yenilemeyi elle sürmek için (doğrulama/teşhis): access token'ı bilerek
      // bozup bir çağrı yapmak yerine doğrudan yenileme yolunu çalıştırır.
      forceRefresh: function () { return api.auth.refresh(); }
    };

    if (api.isLoggedIn()) window.loggedIn = true;

    // index.html'in MOCK giriş/kayıt düğmeleri gerçek uçlara bağlanır.
    // Eski davranış: loginSubmit yalnız login(email.split("@")[0]) çağırıyordu -
    // hiçbir istek atılmıyordu, "giriş" tamamen sahteydi.
    var lb = document.getElementById("loginSubmit");
    if (lb) {
      lb.onclick = async function () {
        var em = document.getElementById("lgEmail"), pw = document.getElementById("lgPass"),
            er = document.getElementById("lgErr");
        if (er) er.textContent = "";
        try {
          await window.divisimaAuth.login((em.value || "").trim(), pw.value || "");
        } catch (e) {
          if (er) er.textContent = e.message || "Giriş başarısız";
          else notify(e.message || "Giriş başarısız");
        }
      };
    }

    var rb = document.getElementById("regSubmit");
    if (rb) {
      rb.onclick = async function () {
        var nm = document.getElementById("rgName"), em = document.getElementById("rgEmail"),
            pw = document.getElementById("rgPass"), er = document.getElementById("rgErr");
        if (er) er.textContent = "";
        var email = (em.value || "").trim();
        try {
          await window.divisimaAuth.register({
            name: (nm.value || "").trim(),
            email: email,
            phone: "5550000000",
            password: pw.value || "",
            accepted_terms: true,
            accepted_privacy: true,
            accepted_marketing: false
          });
          showVerifyPrompt(email);
        } catch (e) {
          if (er) er.textContent = e.message || "Kayıt başarısız";
          else notify(e.message || "Kayıt başarısız");
        }
      };
    }

    // Çıkış: index.html'in logout()'u yalnız yerel durumu temizliyordu; sunucudaki
    // oturum açık kalıyordu. Gerçek uca bağlanır.
    if (typeof window.logout === "function" && !window.logout.__divisimaWrapped) {
      var origLogout = window.logout;
      window.logout = function () {
        api.auth.logout().catch(function () {}).then(function () { origLogout.call(window); });
      };
      window.logout.__divisimaWrapped = true;
    }
  }

  // E-POSTA DOĞRULAMA: backend doğrulama e-postasında düz TOKEN yolluyor (link değil),
  // bu yüzden kullanıcıdan token isteniyor. Uç: GET /api/auth/verify-email?token=...
  function showVerifyPrompt(email) {
    var er = document.getElementById("rgErr");
    if (er) er.textContent = "";
    var host = document.getElementById("paneReg") || document.body;
    var box = document.getElementById("dvsVerifyBox");
    if (!box) {
      box = document.createElement("div");
      box.id = "dvsVerifyBox";
      box.style.cssText = "margin-top:14px;padding:14px;border:1px solid #e8e4de;border-radius:10px;background:#faf8f5";
      host.appendChild(box);
    }
    box.innerHTML =
      '<div style="font-weight:600;margin-bottom:6px">E-postanı doğrula</div>' +
      '<div id="dvsVerifyMsg" style="font-size:13px;color:#6b6b6b;margin-bottom:10px"></div>' +
      '<input id="dvsVerifyToken" placeholder="Doğrulama kodu" style="width:100%;padding:9px 11px;border:1px solid #e8e4de;border-radius:8px">' +
      '<div style="display:flex;gap:8px;margin-top:10px">' +
      '<button id="dvsVerifyGo" style="padding:9px 16px;border:none;border-radius:8px;background:#111;color:#fff;cursor:pointer">Doğrula</button>' +
      '<button id="dvsVerifyResend" style="padding:9px 16px;border:1px solid #e8e4de;border-radius:8px;background:#fff;cursor:pointer">Tekrar gönder</button>' +
      "</div>" +
      '<div id="dvsVerifyErr" style="color:#a32d2d;font-size:12px;margin-top:8px"></div>';
    document.getElementById("dvsVerifyMsg").textContent =
      email + " adresine doğrulama kodu gönderildi. Kodu girip hesabını etkinleştir.";

    document.getElementById("dvsVerifyGo").onclick = async function () {
      var errEl = document.getElementById("dvsVerifyErr");
      errEl.textContent = "";
      var tok = (document.getElementById("dvsVerifyToken").value || "").trim();
      if (!tok) { errEl.textContent = "Kodu gir."; return; }
      try {
        await api.auth.verifyEmail(tok);
        box.remove();
        notify("E-posta doğrulandı, giriş yapabilirsin.");
      } catch (e) { errEl.textContent = e.message || "Doğrulama başarısız"; }
    };
    document.getElementById("dvsVerifyResend").onclick = async function () {
      var errEl = document.getElementById("dvsVerifyErr");
      errEl.textContent = "";
      try { await api.auth.resendVerification(email); notify("Kod tekrar gönderildi."); }
      catch (e) { errEl.textContent = e.message || "Gönderilemedi"; }
    };
  }
  window.divisimaShowVerify = showVerifyPrompt;


  // ══ E2 - SEPET + CHECKOUT + ODEME ══════════════════════════════════════════
  //
  // Sepet: storefront'un yerel `cart` Map'i EKRAN icin kaynak olmaya devam ediyor
  // (rozet, mini adim, oneriler hepsi ona bagli); her mutasyon API'ye AYNALANIYOR.
  // Boylece sozlesme degismeden sunucu sepeti gercek kalir.
  //
  // Checkout: index.html'in checkout ekrani MOCK'tur - yerel adres listesi (ADDR),
  // yerel kart formu (CARDS) ve yerel kupon tablosu (COUPONS) kullanir. KART BILGISI
  // BIZE HIC GELMEMELI (Iyzico Checkout Form alir), bu yuzden o ekran gercek bir panelle
  // DEGISTIRILIYOR: adresler API'den, kupon API'den, magaza kredisi API'den.

  var checkoutState = { addresses: [], addrId: null, coupon: null, useCredit: 0, credit: 0, method: "card" };

  function money(n) {
    try { return Number(n || 0).toLocaleString("tr-TR", { minimumFractionDigits: 2, maximumFractionDigits: 2 }) + " TL"; }
    catch (e) { return (Number(n || 0)).toFixed(2) + " TL"; }
  }

  // Sepetteki kalemleri API siparis kalemi bicimine cevir.
  function cartItemsPayload() {
    var out = [];
    if (!window.cart || !window.cart.forEach) return out;
    window.cart.forEach(function (it) {
      var q = Math.floor(it.qty); if (!isFinite(q) || q < 1) q = 1;
      out.push({ product_id: it.id, size: it.size || "", quantity: q });
    });
    return out;
  }

  function cartSubtotal() {
    var s = 0;
    if (!window.cart || !window.cart.forEach) return 0;
    window.cart.forEach(function (it) {
      var p = window.byId(it.id); if (!p) return;
      var q = Math.floor(it.qty); if (!isFinite(q) || q < 1) q = 1;
      s += (Number(p.price) || 0) * q;
    });
    return s;
  }

  // ── Sepet aynalama ─────────────────────────────────────────────────────────
  // API cagrisi UI'yi BLOKLAMAZ: sepet yerelde aninda guncellenir, sunucu arkadan
  // yakalar. Hata olursa kullaniciya bildirilir ama sepet geri alinmaz (yeniden
  // deneme checkout'ta zaten sunucuya tam liste gonderilerek yapiliyor).
  // Aynalama hatasi SESSIZ KALMAZ: yerel sepet dolu gorunurken sunucu sepeti bos kalirsa
  // musteri bunu ancak checkout'ta anlar. Ornek (olculdu): bedeni secilmemis bir giyim
  // kalemi sunucuda "stok yetersiz" ile reddediliyor - beden satiri "" ile eslesmiyor.
  var lastMirrorWarn = 0;
  function mirror(promise, adim) {
    return promise.catch(function (e) {
      console.warn("Divisima: sepet aynalama basarisiz (" + adim + ")", e && e.message);
      var now = Date.now();
      if (now - lastMirrorWarn > 4000) {   // ust uste toast yagmuru olmasin
        lastMirrorWarn = now;
        notify("Sepet sunucuya yazılamadı: " + (e && e.message ? e.message : "bilinmeyen hata"));
      }
    });
  }

  function wireCart() {
    // addToCart(id, size, qty, color) - storefront yerel Map'e yaziyor; sonrasinda aynala.
    if (typeof window.addToCart === "function" && !window.addToCart.__divisimaWrapped) {
      var origAdd = window.addToCart;
      window.addToCart = function (id, size, qty, color) {
        origAdd.apply(window, arguments);
        if (!api.isLoggedIn()) return;   // anonim sepet yerel kalir (uc Customer ister)
        // Yerel Map'teki GUNCEL adet gonderilir: uc UPSERT (SET) semantigi kullaniyor,
        // artirma degil. Yerelde 2'ye ciktiysa sunucuya da 2 yazilmali.
        var key = null, entry = null;
        window.cart.forEach(function (v, k) { if (v.id === id && (v.size || "") === (size || "")) { key = k; entry = v; } });
        var q = entry ? Math.floor(entry.qty) : (qty || 1);
        mirror(api.cart.setQuantity(id, size || "", q), "ekle");
      };
      window.addToCart.__divisimaWrapped = true;
    }

    // Adet/silme storefront icinde dogrudan cart.set/delete ile yapiliyor ve tek ortak
    // fonksiyon yok. renderCart her degisiklikten sonra cagrildigi icin ORAYA baglanip
    // sunucu sepetini yerel sepete esitliyoruz (tam senkron - kaymayi imkansiz kilar).
    if (typeof window.renderCart === "function" && !window.renderCart.__divisimaWrapped) {
      var origRender = window.renderCart;
      var syncTimer = null;
      window.renderCart = function () {
        origRender.apply(window, arguments);
        if (!api.isLoggedIn()) return;
        clearTimeout(syncTimer);
        syncTimer = setTimeout(syncCartToServer, 250);   // hizli tiklamalarda tek istek
      };
      window.renderCart.__divisimaWrapped = true;
    }
  }

  // Sunucu sepetini yerel sepete esitle: yereldeki her kalem SET edilir, sunucuda olup
  // yerelde olmayan kalem SILINIR. (Sepet kucuk oldugu icin tam esitleme guvenli ve basit.)
  var syncing = false;
  async function syncCartToServer() {
    if (syncing || !api.isLoggedIn()) return;
    syncing = true;
    try {
      var local = cartItemsPayload();
      var localKey = {};
      local.forEach(function (it) { localKey[it.product_id + "|" + it.size] = it; });

      var server = [];
      try { server = (unwrap(await api.cart.get()) || {}).items || unwrap(await api.cart.get()) || []; }
      catch (e) { server = []; }
      if (!Array.isArray(server)) server = [];

      for (var i = 0; i < local.length; i++) {
        await mirror(api.cart.setQuantity(local[i].product_id, local[i].size, local[i].quantity), "esitle");
      }
      for (var j = 0; j < server.length; j++) {
        var s = server[j];
        var k = s.product_id + "|" + (s.size || "");
        if (!localKey[k]) await mirror(api.cart.remove(s.product_id, s.size || ""), "sil");
      }
    } finally { syncing = false; }
  }
  window.divisimaSyncCart = syncCartToServer;

  // ── Checkout paneli (MOCK ekranin yerine) ──────────────────────────────────
  async function renderRealCheckout() {
    var view = document.getElementById("checkoutView");
    if (!view) return;

    if (!api.isLoggedIn()) {
      view.innerHTML = '<div class="wrap" style="padding:40px 0"><h2>Ödeme</h2>' +
        '<p class="muted" style="margin:10px 0 16px">Siparişi tamamlamak için giriş yapmalısın.</p>' +
        '<a class="btn" href="#/giris">Giriş yap</a></div>';
      return;
    }
    if (!window.cart || window.cart.size === 0) {
      view.innerHTML = '<div class="wrap" style="padding:40px 0"><h2>Ödeme</h2>' +
        '<p class="muted" style="margin:10px 0 16px">Sepetin boş.</p>' +
        '<a class="btn" href="#/kategori/tumu">Alışverişe başla</a></div>';
      return;
    }

    view.innerHTML = '<div class="wrap" style="padding:28px 0"><p class="muted">Ödeme hazırlanıyor…</p></div>';

    // Adresler + magaza kredisi paralel
    var addrs = [], credit = 0;
    try { addrs = unwrap(await api.address.list()) || []; } catch (e) { addrs = []; }
    try { credit = Number((unwrap(await api._get("/api/Account/summary")) || {}).store_credit) || 0; } catch (e) { credit = 0; }
    checkoutState.addresses = addrs;
    checkoutState.credit = credit;
    if (!checkoutState.addrId) {
      var def = addrs.filter(function (a) { return a.is_default; })[0] || addrs[0];
      checkoutState.addrId = def ? def.id : null;
    }
    drawCheckout();
  }

  function drawCheckout() {
    var view = document.getElementById("checkoutView");
    if (!view) return;
    var sub = cartSubtotal();
    var disc = checkoutState.coupon ? Number(checkoutState.coupon.discount_amount) || 0 : 0;
    var freeShip = !!(checkoutState.coupon && checkoutState.coupon.free_shipping);
    // Kargo kurali backend ile AYNI: >= 2000 bedava, degilse 49.90 (OrderManager sabitleri).
    // Kesin tutar siparis yanitindan gelir; burada gosterilen TAHMINDIR.
    var ship = (freeShip || sub >= 2000) ? 0 : 49.9;
    var credUse = Math.min(checkoutState.useCredit, checkoutState.credit, Math.max(0, sub - disc + ship));
    var total = Math.max(0, sub - disc + ship - credUse);

    var items = [];
    window.cart.forEach(function (it) {
      var p = window.byId(it.id); if (!p) return;
      var q = Math.floor(it.qty) || 1;
      items.push('<div style="display:flex;justify-content:space-between;padding:6px 0;font-size:13px">' +
        '<span>' + esc(p.name) + (it.size ? " · " + esc(String(it.size)) : "") + " × " + q + "</span>" +
        "<span>" + money((Number(p.price) || 0) * q) + "</span></div>");
    });

    var addrOpts = checkoutState.addresses.map(function (a) {
      return '<option value="' + a.id + '"' + (a.id === checkoutState.addrId ? " selected" : "") + ">" +
        esc(a.title || a.full_name || ("Adres #" + a.id)) + " · " + esc(a.city || "") + "</option>";
    }).join("");

    view.innerHTML =
      '<div class="wrap" style="padding:28px 0;max-width:720px">' +
      "<h2>Ödeme</h2>" +

      '<div class="panel" style="margin-top:16px"><h3>Teslimat adresi</h3>' +
      (checkoutState.addresses.length
        ? '<select id="coAddr" style="width:100%;padding:9px 11px;border:1px solid #e8e4de;border-radius:8px">' + addrOpts + "</select>"
        : '<p class="muted" style="font-size:13px">Kayıtlı adresin yok, aşağıdan ekle.</p>') +
      '<button class="btn ghost sm" id="coNewAddr" style="margin-top:10px">+ Yeni adres</button>' +
      '<div id="coAddrForm" style="display:none;margin-top:12px"></div>' +
      "</div>" +

      '<div class="panel"><h3>Sipariş özeti</h3>' + items.join("") +
      '<div style="border-top:1px solid #e8e4de;margin-top:10px;padding-top:10px;font-size:13px">' +
      '<div style="display:flex;justify-content:space-between"><span>Ara toplam</span><span>' + money(sub) + "</span></div>" +
      (disc > 0 ? '<div style="display:flex;justify-content:space-between;color:#0f6e56"><span>Kupon indirimi</span><span>-' + money(disc) + "</span></div>" : "") +
      '<div style="display:flex;justify-content:space-between"><span>Kargo' + (ship === 0 ? " (ücretsiz)" : "") + "</span><span>" + money(ship) + "</span></div>" +
      (credUse > 0 ? '<div style="display:flex;justify-content:space-between;color:#0f6e56"><span>Mağaza kredisi</span><span>-' + money(credUse) + "</span></div>" : "") +
      '<div style="display:flex;justify-content:space-between;font-weight:600;font-size:15px;margin-top:8px"><span>Toplam</span><span id="coTotal">' + money(total) + "</span></div>" +
      "</div></div>" +

      '<div class="panel"><h3>Kupon</h3>' +
      '<div style="display:flex;gap:8px"><input id="coCoupon" placeholder="Kupon kodu" style="flex:1;padding:9px 11px;border:1px solid #e8e4de;border-radius:8px" value="' +
      (checkoutState.coupon ? esc(checkoutState.coupon.code) : "") + '">' +
      '<button class="btn ghost" id="coCouponGo">Uygula</button></div>' +
      '<div id="coCouponMsg" style="font-size:12px;margin-top:6px"></div></div>' +

      (checkoutState.credit > 0
        ? '<div class="panel"><h3>Mağaza kredisi</h3>' +
          '<p class="muted" style="font-size:13px">Bakiyen: ' + money(checkoutState.credit) + "</p>" +
          '<label style="display:flex;align-items:center;gap:8px;margin-top:8px">' +
          '<input type="checkbox" id="coUseCredit"' + (checkoutState.useCredit > 0 ? " checked" : "") + "> Bakiyeyi kullan</label></div>"
        : "") +

      '<div class="panel"><h3>Ödeme yöntemi</h3>' +
      '<label style="display:flex;align-items:center;gap:8px"><input type="radio" name="coPay" value="card"' +
      (checkoutState.method === "card" ? " checked" : "") + "> Kredi/banka kartı (güvenli ödeme sayfası)</label>" +
      '<label style="display:flex;align-items:center;gap:8px;margin-top:6px"><input type="radio" name="coPay" value="cod"' +
      (checkoutState.method === "cod" ? " checked" : "") + "> Kapıda ödeme</label>" +
      '<p class="muted" style="font-size:12px;margin-top:8px">Kart bilgilerin bize hiç gelmez; ödeme sağlayıcının kendi sayfasında alınır.</p>' +
      "</div>" +

      '<button class="btn" id="coSubmit" style="width:100%;padding:13px">Siparişi tamamla</button>' +
      '<div id="coErr" style="color:#a32d2d;font-size:13px;margin-top:10px"></div>' +
      '<div id="coPayHost" style="margin-top:16px"></div>' +
      "</div>";

    var sel = document.getElementById("coAddr");
    if (sel) sel.onchange = function () { checkoutState.addrId = parseInt(sel.value) || null; };
    document.getElementById("coNewAddr").onclick = toggleAddrForm;
    document.getElementById("coCouponGo").onclick = applyCouponReal;
    var cc = document.getElementById("coUseCredit");
    if (cc) cc.onchange = function () { checkoutState.useCredit = cc.checked ? checkoutState.credit : 0; drawCheckout(); };
    Array.prototype.forEach.call(document.getElementsByName("coPay"), function (r) {
      r.onchange = function () { checkoutState.method = r.value; };
    });
    document.getElementById("coSubmit").onclick = submitOrder;
  }

  function toggleAddrForm() {
    var box = document.getElementById("coAddrForm");
    if (!box) return;
    if (box.style.display !== "none") { box.style.display = "none"; return; }
    box.style.display = "";
    box.innerHTML =
      '<input id="adTitle" placeholder="Adres başlığı (Ev/İş)" style="width:100%;padding:9px 11px;border:1px solid #e8e4de;border-radius:8px;margin-bottom:8px">' +
      '<input id="adName" placeholder="Ad Soyad" style="width:100%;padding:9px 11px;border:1px solid #e8e4de;border-radius:8px;margin-bottom:8px">' +
      '<input id="adPhone" placeholder="Telefon" style="width:100%;padding:9px 11px;border:1px solid #e8e4de;border-radius:8px;margin-bottom:8px">' +
      '<input id="adCity" placeholder="İl" style="width:100%;padding:9px 11px;border:1px solid #e8e4de;border-radius:8px;margin-bottom:8px">' +
      '<input id="adDistrict" placeholder="İlçe" style="width:100%;padding:9px 11px;border:1px solid #e8e4de;border-radius:8px;margin-bottom:8px">' +
      '<textarea id="adFull" rows="2" placeholder="Açık adres" style="width:100%;padding:9px 11px;border:1px solid #e8e4de;border-radius:8px;margin-bottom:8px"></textarea>' +
      '<button class="btn sm" id="adSave">Adresi kaydet</button>' +
      '<div id="adErr" style="color:#a32d2d;font-size:12px;margin-top:6px"></div>';
    document.getElementById("adSave").onclick = async function () {
      var err = document.getElementById("adErr"); err.textContent = "";
      try {
        await api.address.upsert({
          customer_id: 1,   // sunucu token'dan ezer; validator > 0 istiyor olabilir
          title: (document.getElementById("adTitle").value || "").trim(),
          full_name: (document.getElementById("adName").value || "").trim(),
          phone: (document.getElementById("adPhone").value || "").trim(),
          city: (document.getElementById("adCity").value || "").trim(),
          district: (document.getElementById("adDistrict").value || "").trim(),
          full_address: (document.getElementById("adFull").value || "").trim(),
          zip_code: "",
          is_default: checkoutState.addresses.length === 0
        });
        checkoutState.addrId = null;
        await renderRealCheckout();
      } catch (e) { err.textContent = e.message || "Adres kaydedilemedi"; }
    };
  }

  async function applyCouponReal() {
    var msg = document.getElementById("coCouponMsg");
    var code = (document.getElementById("coCoupon").value || "").trim();
    msg.textContent = ""; msg.style.color = "#a32d2d";
    if (!code) { checkoutState.coupon = null; drawCheckout(); return; }
    try {
      var d = unwrap(await api.coupons.validate(code, cartSubtotal()));
      checkoutState.coupon = d ? Object.assign({ code: code }, d) : null;
      drawCheckout();
      var m2 = document.getElementById("coCouponMsg");
      if (m2) { m2.style.color = "#0f6e56"; m2.textContent = "Kupon uygulandı."; }
    } catch (e) {
      checkoutState.coupon = null;
      drawCheckout();
      var m3 = document.getElementById("coCouponMsg");
      if (m3) { m3.style.color = "#a32d2d"; m3.textContent = e.message || "Kupon geçersiz"; }
    }
  }

  async function submitOrder() {
    var err = document.getElementById("coErr");
    var btn = document.getElementById("coSubmit");
    err.textContent = "";
    var items = cartItemsPayload();
    if (!items.length) { err.textContent = "Sepet boş."; return; }
    if (!checkoutState.addrId && checkoutState.addresses.length) { err.textContent = "Adres seç."; return; }

    // BEDENSIZ GIYIM KALEMI: sunucu stok satirini beden ile bulur, "" hicbir satirla
    // eslesmez ve siparis "stok yetersiz" ile duser. Kullaniciyi checkout'un ortasinda
    // anlamsiz bir hataya birakmak yerine burada ADIYLA soyluyoruz.
    var bedensiz = items.filter(function (it) {
      return !it.size && typeof window.isClothing === "function" && window.isClothing(it.product_id);
    });
    if (bedensiz.length) {
      var adlar = bedensiz.map(function (it) {
        var p = window.byId(it.product_id); return p ? p.name : ("#" + it.product_id);
      }).join(", ");
      err.textContent = "Beden seçilmemiş ürün var: " + adlar + ". Sepetten beden seçip tekrar dene.";
      return;
    }

    btn.disabled = true; btn.textContent = "Gönderiliyor…";
    try {
      // Sunucu sepetini de esitle (siparis kalemleri govdeden gidiyor ama sepet tutarli kalsin)
      await syncCartToServer();

      var order = unwrap(await api.orders.place({
        customer_id: 1,                       // sunucu token'dan EZER; validator > 0 istiyor
        request_id: (window.crypto && crypto.randomUUID) ? crypto.randomUUID() : String(Date.now()),
        address_id: checkoutState.addrId || null,
        coupon_code: checkoutState.coupon ? checkoutState.coupon.code : "",   // non-nullable
        use_store_credit: checkoutState.useCredit > 0 ? checkoutState.credit : 0,
        payment_method: checkoutState.method === "cod" ? 1 : 0,
        items: items
      }));

      var orderId = (order && order.id) ? order.id : order;   // uc siparis id'sini dogrudan donuyor
      try { sessionStorage.setItem("divisima_last_order", String(orderId)); } catch (e) {}

      if (checkoutState.method === "cod") {
        // Kapida odeme: odeme baslatilmaz, siparis dogrudan olusur.
        if (window.cart) window.cart.clear();
        try { await api.cart.clear(); } catch (e) {}
        location.hash = "#/odeme/sonuc?order=" + orderId + "&status=cod";
        return;
      }

      var pay = unwrap(await api.payment.initialize(orderId));
      if (!pay || !pay.checkout_form_content) throw new Error("Ödeme formu alınamadı.");
      embedCheckoutForm(pay.checkout_form_content);
    } catch (e) {
      err.textContent = e.message || "Sipariş oluşturulamadı";
    } finally {
      btn.disabled = false; btn.textContent = "Siparişi tamamla";
    }
  }

  // Iyzico Checkout Form HTML'i <script> icerir. innerHTML ile eklenen script
  // TARAYICI TARAFINDAN CALISTIRILMAZ (HTML5 kurali) - dugumler yeniden olusturulmali.
  function embedCheckoutForm(html) {
    var host = document.getElementById("coPayHost");
    if (!host) return;
    host.innerHTML = "";
    var holder = document.createElement("div");
    holder.id = "iyzipay-checkout-form";
    holder.className = "responsive";
    host.appendChild(holder);

    var tmp = document.createElement("div");
    tmp.innerHTML = html;
    Array.prototype.forEach.call(tmp.childNodes, function (node) {
      if (node.tagName === "SCRIPT") {
        var s = document.createElement("script");
        if (node.src) s.src = node.src; else s.text = node.textContent;
        document.body.appendChild(s);
      } else {
        host.appendChild(node.cloneNode(true));
      }
    });
    host.scrollIntoView({ behavior: "smooth", block: "start" });
  }
  window.divisimaEmbedCheckoutForm = embedCheckoutForm;

  // ── Odeme sonuc sayfasi (#/odeme/sonuc?order=..&status=..) ─────────────────
  async function renderPaymentResult(params) {
    var view = document.getElementById("checkoutView");
    if (!view) return;
    var orderId = parseInt(params.order) || 0;
    var status = params.status || "";
    var ok = status === "success" || status === "cod";

    view.innerHTML = '<div class="wrap" style="padding:40px 0;max-width:640px"><p class="muted">Yükleniyor…</p></div>';

    var order = null;
    if (orderId) { try { order = unwrap(await api.orders.get(orderId)); } catch (e) { order = null; } }

    var baslik = status === "cod" ? "Siparişin alındı" : (ok ? "Ödemen alındı" : "Ödeme tamamlanamadı");
    var alt = status === "cod"
      ? "Kapıda ödeme ile siparişin oluşturuldu."
      : (ok ? "Siparişin onaylandı ve hazırlanmaya başlıyor." : "Tutar tahsil edilmedi. Kartında bir kesinti olduysa iade edilir.");

    var ozet = "";
    if (order) {
      // ALAN ADLARI (olculdu): siparis detayi "total" ve "order_status" (METIN, "Confirmed")
      // doner - "total_price"/"status" DEGIL. Yanlis alan okununca ekranda "0,00 TL" ve
      // "undefined" cikiyordu. Yine de her iki bicim de kabul ediliyor.
      var toplam = (order.total !== undefined) ? order.total : order.total_price;
      var durum = (order.order_status !== undefined && order.order_status !== null)
        ? order.order_status : orderStatusLabel(order.status);
      var kalemler = (order.items || []).map(function (it) {
        return '<div style="display:flex;justify-content:space-between;font-size:13px;padding:3px 0;color:#6b6b6b">' +
          "<span>" + esc(it.product_name || ("#" + it.product_id)) +
          (it.size ? " · " + esc(String(it.size)) : "") + " × " + (it.quantity || 1) + "</span>" +
          "<span>" + money(it.line_total) + "</span></div>";
      }).join("");

      ozet = '<div class="panel" style="text-align:left"><h3>Sipariş özeti</h3>' +
        '<div style="display:flex;justify-content:space-between;font-size:13px;padding:4px 0"><span>Sipariş no</span><span>' +
        esc(String(order.order_number || order.id || orderId)) + "</span></div>" +
        kalemler +
        '<div style="display:flex;justify-content:space-between;font-size:13px;padding:4px 0"><span>Kargo</span><span>' +
        money(order.shipping_cost) + "</span></div>" +
        '<div style="display:flex;justify-content:space-between;font-size:14px;font-weight:600;padding:6px 0;border-top:1px solid #e8e4de;margin-top:6px"><span>Toplam</span><span>' +
        money(toplam) + "</span></div>" +
        '<div style="display:flex;justify-content:space-between;font-size:13px;padding:4px 0"><span>Durum</span><span>' +
        esc(String(durum)) + "</span></div></div>";
    } else if (orderId) {
      ozet = '<p class="muted" style="font-size:13px">Sipariş #' + orderId + " detayına şu an ulaşılamadı.</p>";
    }

    view.innerHTML =
      '<div class="wrap" style="padding:40px 0;max-width:640px;text-align:center">' +
      '<div style="font-size:44px;margin-bottom:8px">' + (ok ? "✓" : "✕") + "</div>" +
      "<h2>" + baslik + "</h2>" +
      '<p class="muted" style="margin:8px 0 18px">' + alt + "</p>" +
      ozet +
      '<div style="display:flex;gap:10px;justify-content:center;margin-top:18px">' +
      '<a class="btn" href="#/hesabim/siparislerim">Siparişlerime git</a>' +
      (ok ? '<a class="btn ghost" href="#/kategori/tumu">Alışverişe devam</a>'
          : '<a class="btn ghost" href="#/odeme">Tekrar dene</a>') +
      "</div></div>";

    if (ok && window.cart && window.cart.size) {
      window.cart.clear();
      try { if (typeof window.renderCart === "function") window.renderCart(); } catch (e) {}
    }
  }

  function orderStatusLabel(s) {
    var m = { 0: "Beklemede", 1: "Onaylandı", 2: "Hazırlanıyor", 3: "Kargoda", 4: "Teslim edildi", 5: "İptal" };
    return m[s] !== undefined ? m[s] : String(s);
  }

  // Yonlendirici: #/odeme -> gercek checkout, #/odeme/sonuc -> sonuc sayfasi.
  function wireCheckoutRouting() {
    function handle() {
      var raw = location.hash.replace(/^#\/?/, "");
      var qi = raw.indexOf("?");
      var path = qi >= 0 ? raw.slice(0, qi) : raw;
      var query = qi >= 0 ? raw.slice(qi + 1) : "";
      var seg = path.split("/");
      if (seg[0] !== "odeme") return;
      var params = {};
      query.split("&").forEach(function (kv) {
        if (!kv) return;
        var i = kv.indexOf("=");
        params[decodeURIComponent(kv.slice(0, i))] = decodeURIComponent(kv.slice(i + 1));
      });
      if (typeof window.setView === "function") window.setView("checkout");
      if (seg[1] === "sonuc") renderPaymentResult(params);
      else renderRealCheckout();
    }
    // ROUTER'I SARMALA: yalniz hashchange dinlemek YETMIYOR. index.html'in router'i
    // "#/odeme" gorunce showCheckout() -> renderCheckout() ile MOCK ekrani ciziyor ve
    // sayfa yeniden yuklendiginde (odeme callback'i 302 ile geri donduğunde) bizim
    // ciziminizin USTUNE yaziyordu (olculdu: sonuc sayfasi yerine checkout goruldu).
    // Router'in ardindan calisarak son sozu biz soyluyoruz.
    if (typeof window.router === "function" && !window.router.__divisimaWrapped) {
      var origRouter = window.router;
      window.router = function () {
        origRouter.apply(window, arguments);
        handle();
      };
      window.router.__divisimaWrapped = true;
    }
    window.addEventListener("hashchange", function () { setTimeout(handle, 0); });
    setTimeout(handle, 0);   // ilk yuklemede zaten #/odeme'deysek
  }
  // ── Kupon (gerçek API) ─────────────────────────────────────────────────────
  function wireCoupon() {
    window.divisimaValidateCoupon = async function (code, subtotal) {
      try { return unwrap(await api.coupons.validate(code, subtotal)); }
      catch (e) { return null; }
    };
  }

  // ── Eski koprü checkout'u KALDIRILDI (E2) ──────────────────────────────────
  // Onceki divisimaCheckout uc alanı YANLIS gonderiyordu: "payment_type" (dogru ad
  // payment_method), coupon_code null (non-nullable -> 400), customer_id yok
  // (validator > 0 istiyor) ve items HIC gonderilmiyordu. Yani hicbir zaman calisan
  // bir siparis yolu degildi. Yerine gercek checkout paneli geldi (submitOrder).
  // Disari acilan tek yuzey, elle surus/teshis icin:
  function wireCheckout() {
    window.divisimaCheckout = function () {
      return Promise.reject(new Error("Kaldirildi - #/odeme panelini kullan (submitOrder)."));
    };
  }

  // ── Başlat ──
  async function init() {
    wireCoupon();
    wireAuth();
    wireCheckout();
    wireSearch();
    wireProductDetail();
    wireCart();               // E2: sepet mutasyonlarini sunucuya aynala
    wireCheckoutRouting();    // E2: #/odeme ve #/odeme/sonuc
    // Kategoriler ÖNCE: ürün kategorisi category_id üzerinden çözülüyor (liste yolu
    // category_name döndürmüyor), yükleme sırası ters olursa tüm ürünler "tumu" olur.
    await loadCategories();
    await loadCatalog();
  }

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", init);
  } else {
    init();
  }
})();
