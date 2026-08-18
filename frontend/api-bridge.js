/*
 * Divisima API Köprüsü (api-bridge.js)
 * -------------------------------------
 * Mevcut index.html'i (mock veriyle çalışan) gerçek backend'e bağlar.
 * index.html'e <script src="/api-client.js"></script> ve <script src="/api-bridge.js"></script>
 * olarak </body> öncesine EKLENIR (api-client'tan SONRA).
 *
 * Ne yapar:
 *  1) Ürünleri API'den çeker, frontend'in beklediği şekle map eder, PRODUCTS'ı doldurur, grid'i yeniden çizer.
 *  2) Kupon doğrulamayı gerçek API'ye bağlar.
 *  3) Sepet/checkout'u gerçek sipariş + ödeme akışına bağlar.
 *  4) Giriş/kayıt/çıkışı gerçek auth'a bağlar.
 * API erişilemezse mevcut mock veriyle çalışmaya devam eder (geliştirme dostu).
 */
(function () {
  "use strict";

  // ── Yapılandırma: kendi backend adresin ──
  var API_BASE = window.DIVISIMA_API_BASE || "http://localhost:5000";
  var api = new DivisimaAPI(API_BASE);
  window.divisimaApi = api; // konsoldan erişim

  // ── Kullanıcıya geri bildirim (storefront toast'ı varsa onu kullan) ──
  function notify(msg) { try { if (typeof window.toast === "function") window.toast(msg); else console.log("Divisima:", msg); } catch (e) {} }

  // ── Türkçe slug (kategori adı → frontend slug) ──
  function slugify(s) {
    if (!s) return "";
    return s.toString().toLowerCase()
      .replace(/ç/g, "c").replace(/ğ/g, "g").replace(/ı/g, "i")
      .replace(/ö/g, "o").replace(/ş/g, "s").replace(/ü/g, "u")
      .replace(/[^a-z0-9]+/g, "-").replace(/^-+|-+$/g, "");
  }

  // ── Backend ürün DTO → frontend ürün şekli ──
  function mapProduct(p) {
    // Frontend bekliyor: {id,name,brand,cat,sub,price,old,stock,sizes,col,img}
    return {
      id: p.id,
      name: p.name,
      brand: p.brand || "Divisima",
      cat: slugify(p.category_name) || "tumu",
      sub: "",
      price: Number(p.price) || 0,
      old: p.old_price ? Number(p.old_price) : 0,
      cart: p.price ? Number(p.price) : 0,
      stock: Number(p.total_stock) || 0,
      // Açıklama: bedenler string; frontend hem sayı hem string kabul eder. Beden→stok haritası için _sizeStock
      sizes: (p.sizes && p.sizes.length) ? p.sizes.map(function (s) { return isNaN(+s) ? s : +s; }) : [],
      col: p.color_hex || "#cccccc",
      img: p.image_url || "" // boşsa frontend placeholder üretir (thumbC)
    };
  }

  // ── PRODUCTS'ı yerinde değiştir (tüm closure'lar aynı diziyi görsün) ──
  function replaceProducts(mapped) {
    if (typeof window.PRODUCTS === "undefined") { window.PRODUCTS = []; }
    window.PRODUCTS.length = 0;
    Array.prototype.push.apply(window.PRODUCTS, mapped);
  }

  // ── Grid'leri yeniden çiz (frontend render fonksiyonlarını tekrar çağır) ──
  function rerender() {
    try {
      var g = document.getElementById("grid");
      if (g && typeof window.cardHTML === "function") {
        g.innerHTML = window.PRODUCTS.slice(0, 8).map(window.cardHTML).join("");
      }
      if (typeof window.renderCatGrid === "function") window.renderCatGrid();
      if (typeof window.renderRecent === "function") window.renderRecent();
      if (typeof window.renderIgFeed === "function") window.renderIgFeed();
    } catch (e) { console.warn("Divisima: grid yeniden çizilemedi", e); }
  }

  // ── Ürünleri yükle ──
  async function loadProducts() {
    try {
      var res = await api.products.list();
      var list = (res && res.data !== undefined) ? res.data : res;
      if (!Array.isArray(list) || !list.length) {
        console.warn("Divisima: API ürün döndürmedi, mock veri korunuyor");
        return; // sessiz - boş katalog kullanıcıya gösterilmez, mevcut içerik kalır
      }
      var mapped = list.map(mapProduct);
      replaceProducts(mapped);
      rerender();
      console.log("Divisima: " + mapped.length + " ürün API'den yüklendi");
    } catch (e) {
      console.warn("Divisima: ürünler API'den alınamadı, mock veriyle devam", e);
      // Kullanıcıya gösterme (mock veri zaten görünüyor) - sadece geliştirici konsolu
    }
  }

  // ── Kupon doğrulamayı gerçek API'ye bağla ──
  function wireCoupon() {
    // Frontend validateCoupon() client-side; gerçek doğrulama için sunucuya sor
    window.divisimaValidateCoupon = async function (code, subtotal) {
      try {
        var res = await api._post("/api/coupon/validate", { code: code, cart_total: subtotal });
        return (res && res.data !== undefined) ? res.data : res;
      } catch (e) { return null; }
    };
  }

  // ── Auth (giriş/kayıt/çıkış) ──
  function wireAuth() {
    window.divisimaAuth = {
      async login(email, pass) {
        var r = await api.auth.login(email, pass);
        window.loggedIn = true;
        return r;
      },
      async register(payload) { return api.auth.register(payload); },
      async logout() { await api.auth.logout(); window.loggedIn = false; },
      isLoggedIn: function () { return api.isLoggedIn(); }
    };
    // Giriş durumunu frontend'e yansıt
    if (api.isLoggedIn()) window.loggedIn = true;
  }

  // ── Checkout (sepet → sipariş + ödeme) ──
  function wireCheckout() {
    // Frontend'in checkout butonuna bağlanır. Sepeti API sipariş akışına çevirir.
    window.divisimaCheckout = async function (opts) {
      // opts: { addressId, couponCode, paymentType }
      opts = opts || {};
      // 1) Sepeti sunucuya senkronla (client cart → API cart)
      try {
        if (window.cart && window.cart.forEach) {
          for (var entry of window.cart.values()) {
            await api.cart.add(entry.id, entry.size || "", entry.qty);
          }
        }
      } catch (e) { console.warn("Sepet senkronu kısmi", e); }

      // 2) Sipariş oluştur
      var order = await api.orders.place({
        address_id: opts.addressId || null,
        coupon_code: opts.couponCode || null,
        payment_type: opts.paymentType != null ? opts.paymentType : 0,
        request_id: (window.crypto && crypto.randomUUID) ? crypto.randomUUID() : String(Date.now())
      });
      var orderData = (order && order.data !== undefined) ? order.data : order;

      // 3) Ödeme başlat (Iyzico Checkout Form)
      var pay = await api.payment.initialize(orderData.id);
      var payData = (pay && pay.data !== undefined) ? pay.data : pay;
      return { order: orderData, payment: payData }; // payData.checkout_form_content → iframe göm
    };

    // Açıklama: Hata yakalamalı checkout sarmalayıcı - kullanıcıya toast gösterir
    window.divisimaCheckoutSafe = async function (opts) {
      try {
        notify("Sipariş oluşturuluyor…");
        return await window.divisimaCheckout(opts);
      } catch (e) {
        notify(e.message || "Sipariş oluşturulamadı, tekrar deneyin.");
        throw e;
      }
    };
  }

  // ── Başlat ──
  function init() {
    wireCoupon();
    wireAuth();
    wireCheckout();
    loadProducts(); // async - grid dolunca yeniden çizer
  }

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", init);
  } else {
    init();
  }
})();
