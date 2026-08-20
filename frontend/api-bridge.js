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

  // ── Kupon (E2 kapsamı - dokunulmadı) ───────────────────────────────────────
  function wireCoupon() {
    window.divisimaValidateCoupon = async function (code, subtotal) {
      try { return unwrap(await api._post("/api/coupon/validate", { code: code, cart_total: subtotal })); }
      catch (e) { return null; }
    };
  }

  // ── Checkout (E2 kapsamı - dokunulmadı) ────────────────────────────────────
  function wireCheckout() {
    window.divisimaCheckout = async function (opts) {
      opts = opts || {};
      try {
        if (window.cart && window.cart.values) {
          for (var entry of window.cart.values()) {
            await api.cart.add(entry.id, entry.size || "", entry.qty);
          }
        }
      } catch (e) { console.warn("Sepet senkronu kısmi", e); }

      var order = await api.orders.place({
        address_id: opts.addressId || null,
        coupon_code: opts.couponCode || null,
        payment_type: opts.paymentType != null ? opts.paymentType : 0,
        request_id: (window.crypto && crypto.randomUUID) ? crypto.randomUUID() : String(Date.now())
      });
      var orderData = unwrap(order);
      var pay = await api.payment.initialize(orderData.id);
      return { order: orderData, payment: unwrap(pay) };
    };

    window.divisimaCheckoutSafe = async function (opts) {
      try { notify("Sipariş oluşturuluyor…"); return await window.divisimaCheckout(opts); }
      catch (e) { notify(e.message || "Sipariş oluşturulamadı, tekrar deneyin."); throw e; }
    };
  }

  // ── Başlat ──
  async function init() {
    wireCoupon();
    wireAuth();
    wireCheckout();
    wireSearch();
    wireProductDetail();
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
