/*
 * Divisima API İstemcisi (api-client.js)
 * ---------------------------------------
 * Çerçeveden bağımsız (vanilla JS). Tüm backend uçlarını sarar.
 * Özellikler: JWT saklama, 401'de otomatik token yenileme, CSRF (double-submit),
 *             tutarlı hata yönetimi.
 *
 * NOT (SPRINT 8 MADDE 6): refresh token ARTIK httpOnly cookie ile taşınıyor. Backend onu
 * login/refresh/2FA yanıtında cookie'ye yazıp GÖVDEDEN SİLİYOR; /api/auth/refresh gövde
 * almıyor, cookie'den okuyor. İstemci refresh token'ı ne görüyor ne saklıyor.
 * (E1'de ölçülen eski durum: SetRefreshTokenCookie tanımlı ama hiç çağrılmıyordu, token
 * gövdede dönüyordu ve localStorage'da duruyordu - XSS'te çalınabilirdi.)
 * CSRF: double-submit; "csrf_token" cookie'si okunup "X-CSRF-Token" başlığıyla geri gönderilir.
 *
 * Kullanım:
 *   const api = new DivisimaAPI("https://api.divisima.com");
 *   await api.auth.login(email, password);
 *   const products = await api.products.list();
 */
(function (global) {
  "use strict";

  class DivisimaAPI {
    constructor(baseUrl) {
      // Açıklama: Sondaki / temizlenir
      this.baseUrl = (baseUrl || "").replace(/\/+$/, "");
      this._accessToken = null;
      this._refreshToken = null;
      this._refreshing = null; // eszamanli 401lerde tek yenileme

      // Açıklama: Access token sekmeler arası paylaşılsın diye localStorage.
      // SPRINT 8 MADDE 6: refresh token ARTIK OKUNMUYOR - httpOnly cookie'de duruyor.
      // Eski sürümden kalan kalıntı burada, ilk kurulumda TEMİZLENİYOR; aksi halde kullanıcının
      // localStorage'ında hâlâ geçerli bir refresh token kalır ve XSS yüzeyi kapanmamış olur.
      try {
        this._accessToken = localStorage.getItem("divisima_access_token");
        localStorage.removeItem("divisima_refresh_token");
      } catch (_) {}

      // Alt modüller
      this.auth = this._buildAuth();
      this.products = this._buildProducts();
      this.categories = this._buildCategories();
      this.collections = this._buildCollections();
      this.cart = this._buildCart();
      this.wishlist = this._buildWishlist();
      this.orders = this._buildOrders();
      this.payment = this._buildPayment();
      this.coupons = this._buildCoupons();
      this.returns = this._buildReturns();
      this.invoices = this._buildInvoices();
      this.shipment = this._buildShipment();
      this.reviews = this._buildReviews();
      this.address = this._buildAddress();
      this.search = this._buildSearch();
      this.content = this._buildContent();
      this.device = this._buildDevice();
      this.stockNotification = this._buildStockNotification();
      this.priceDrop = this._buildPriceDrop();
      this.account = this._buildAccount();
      this.recentlyViewed = this._buildRecentlyViewed();
      this.stock = this._buildStock();
      this.productImage = this._buildProductImage();
      this.admin = this._buildAdmin();
    }

    // ─────────────────────── Token yönetimi ───────────────────────
    setAccessToken(token) {
      this._accessToken = token;
      try {
        if (token) localStorage.setItem("divisima_access_token", token);
        else localStorage.removeItem("divisima_access_token");
      } catch (_) {}
    }
    getAccessToken() { return this._accessToken; }

    // REFRESH TOKEN ARTIK ISTEMCIDE SAKLANMIYOR (SPRINT 8 MADDE 6).
    // Backend refresh token'i httpOnly cookie'ye yaziyor ve YANIT GOVDESINDEN siliyor; JS onu
    // ne gorebiliyor ne saklayabiliyor - XSS'te calinamaz. E1'de zorunlu olan localStorage
    // saklamasi bu yuzden KALKTI.
    //
    // Metot KALDIRILMADI, cunku eski surumlerden gelen kullanicilarin localStorage'inda hala
    // bir token duruyor olabilir; cagrildiginda o kalintiyi TEMIZLIYOR. Boylece yeni surume
    // gecen kullanicida eski token JS'in erisebildigi yerde UNUTULMUYOR.
    setRefreshToken(_token) {
      this._refreshToken = null;
      try { localStorage.removeItem("divisima_refresh_token"); } catch (_) {}
    }
    getRefreshToken() { return null; }

    // Göreli medya URL'ini API tabanına çöz. Backend Storage:PublicBaseUrl bosken
    // "/uploads/products/x.png" gibi GÖRELİ URL döndürüyor; storefront ayrı origin'de
    // çalıştığında bu kendi origin'ine çözülür ve 404 verir (E4a'da ölçüldü).
    resolveUrl(u) {
      u = String(u || "");
      if (!u) return "";
      if (/^(https?:)?\/\//i.test(u) || /^data:/i.test(u)) return u;
      return this.baseUrl.replace(/\/+$/, "") + (u.startsWith("/") ? u : "/" + u);
    }
    isLoggedIn() { return !!this._accessToken; }

    // Açıklama: CSRF token'ı çerezden okur (backend antiforgery double-submit kullanır)
    // CSRF ADLARI (SPRINT 8 MADDE 6'da OLCULDU ve DUZELTILDI):
    // Istemci "XSRF-TOKEN" cookie'sini okuyup "X-XSRF-TOKEN" basligi gonderiyordu; backend'deki
    // AntiforgeryMiddleware ise "csrf_token" cookie'sini ve "X-CSRF-Token" basligini okuyor.
    // UC YONLU AD UYUSMAZLIGI - iki taraf da kendi adiyla calisiyordu, hicbir zaman eslesmedi.
    // Bugune kadar gorunmemesinin sebebi middleware'in HIC calismamasiydi: yalniz refresh_token
    // cookie'si VARSA devreye giriyor, o cookie de hic yazilmiyordu. Cookie yazilmaya baslayinca
    // bu uyusmazlik /api/auth/refresh'i 403'e cevirirdi. Adlar backend'inkine hizalandi.
    _getCsrfToken() {
      const m = document.cookie.match(/(?:^|;\s*)csrf_token=([^;]+)/);
      return m ? decodeURIComponent(m[1]) : null;
    }

    // ─────────────────────── Çekirdek istek ───────────────────────
    async _request(method, path, body, opts) {
      opts = opts || {};
      const url = this.baseUrl + path;
      const headers = { "Accept": "application/json" };
      if (body !== undefined && body !== null && !(body instanceof FormData)) {
        headers["Content-Type"] = "application/json";
      }
      if (this._accessToken) headers["Authorization"] = "Bearer " + this._accessToken;

      // Açıklama: Durum değiştiren isteklerde CSRF başlığı
      if (["POST", "PUT", "DELETE", "PATCH"].includes(method)) {
        const csrf = this._getCsrfToken();
        if (csrf) headers["X-CSRF-Token"] = csrf;
      }

      const fetchOpts = {
        method,
        headers,
        credentials: "include", // httpOnly refresh cookie + antiforgery cookie gönderilsin
      };
      if (body !== undefined && body !== null) {
        fetchOpts.body = body instanceof FormData ? body : JSON.stringify(body);
      }

      let res = await fetch(url, fetchOpts);

      // Açıklama: 401 ise bir kez token yenilemeyi dene, sonra isteği tekrarla
      if (res.status === 401 && !opts._retried && path !== "/api/auth/refresh") {
        const ok = await this._tryRefresh();
        if (ok) {
          return this._request(method, path, body, Object.assign({}, opts, { _retried: true }));
        }
      }

      return this._parse(res);
    }

    async _parse(res) {
      let data = null;
      const text = await res.text();
      if (text) {
        try { data = JSON.parse(text); } catch (_) { data = text; }
      }
      if (!res.ok) {
        // Açıklama: Backend Result deseni {success, message} döner; hatayı fırlat
        const message = (data && data.message) ? data.message : ("İstek başarısız (" + res.status + ")");
        const err = new Error(message);
        err.status = res.status;
        err.data = data;
        throw err;
      }
      return data;
    }

    // Açıklama: Eşzamanlı 401'lerde tek bir yenileme çalışır (diğerleri onu bekler).
    //
    // SÖZLEŞME (SPRINT 8 MADDE 6 ile DEĞİŞTİ):
    // POST /api/auth/refresh artık GÖVDE ALMIYOR - token httpOnly "refresh_token" cookie'sinden
    // okunuyor. Bu yüzden istek gövdesiz gidiyor ve "credentials: include" ZORUNLU (cookie
    // olmadan uç 401 döner). Ayrıca istek Bearer TAŞIMADIĞI ve cookie TAŞIDIĞI için
    // AntiforgeryMiddleware devreye girer: "X-CSRF-Token" başlığı "csrf_token" cookie'siyle
    // eşleşmezse 403 gelir. Başlık bu yüzden burada AÇIKÇA ekleniyor - ortak _request yolundan
    // geçmediğimiz için otomatik eklenmiyor.
    async _tryRefresh() {
      if (this._refreshing) return this._refreshing;
      this._refreshing = (async () => {
        try {
          const headers = { "Accept": "application/json" };
          const csrf = this._getCsrfToken();
          if (csrf) headers["X-CSRF-Token"] = csrf;
          const res = await fetch(this.baseUrl + "/api/auth/refresh", {
            method: "POST",
            headers,
            credentials: "include",
          });
          if (!res.ok) { this.setAccessToken(null); this.setRefreshToken(null); return false; }
          const data = await res.json();
          const payload = (data && data.data) ? data.data : data;
          const token = payload && (payload.token || payload.access_token);
          if (token) {
            this.setAccessToken(token);
            // Rotasyon artik SUNUCU tarafinda: yeni refresh token cookie'ye yaziliyor,
            // govdede gelmiyor. Istemcinin saklayacagi bir sey YOK.
            return true;
          }
          return false;
        } catch (_) {
          this.setAccessToken(null);
          return false;
        } finally {
          this._refreshing = null;
        }
      })();
      return this._refreshing;
    }

    _get(p, o) { return this._request("GET", p, null, o); }
    _post(p, b, o) { return this._request("POST", p, b, o); }
    _put(p, b, o) { return this._request("PUT", p, b, o); }
    _del(p, b, o) { return this._request("DELETE", p, b, o); }
    _patch(p, b, o) { return this._request("PATCH", p, b, o); }

    // Açıklama: Query string yardımcı
    _qs(params) {
      if (!params) return "";
      const q = Object.entries(params)
        .filter(([, v]) => v !== undefined && v !== null && v !== "")
        .map(([k, v]) => encodeURIComponent(k) + "=" + encodeURIComponent(v))
        .join("&");
      return q ? "?" + q : "";
    }

    // ─────────────────────── Auth ───────────────────────
    _buildAuth() {
      const api = this;
      return {
        async register(payload) { return api._post("/api/auth/register", payload); },
        // Açıklama: Login yanıtı CustomerLoginResponseDto -> alan adı "token" (access_token DEĞİL)
        // ve "refresh_token". Önceki kod data.data.access_token okuyordu; alan yok, undefined
        // dönüyor, token HİÇ SAKLANMIYORDU: login 200 dönmesine rağmen sonraki her çağrı 401
        // alıyordu (ölçüldü - panele girilemiyordu). access_token okuması ileri uyumluluk için
        // yedek olarak bırakıldı.
        async login(email, password) {
          const data = await api._post("/api/auth/login", { email, password });
          const payload = (data && data.data) ? data.data : data;
          const token = payload && (payload.token || payload.access_token);
          if (token) api.setAccessToken(token);
          // SPRINT 8 MADDE 6: refresh token artik GOVDEDE GELMIYOR (sunucu httpOnly cookie'ye
          // yazip alani govdeden siliyor). setRefreshToken cagrisi kalintiyi temizlemek icin
          // duruyor - eski surumden gelen kullanicinin localStorage'inda unutulmasin.
          api.setRefreshToken(null);
          return data;
        },
        async refresh() { return api._tryRefresh(); },
        async logout() {
          try { await api._post("/api/auth/logout", {}); }
          finally { api.setAccessToken(null); api.setRefreshToken(null); }
        },
        async verifyEmail(token) { return api._get("/api/auth/verify-email" + api._qs({ token })); },
        // MFIX-3 / F-M3g: uc `ResendVerification([FromQuery] string email)` - GOVDE DEGIL
        // SORGU DIZESI bagliyor (AuthController.cs). Govdeyle cagrildiginda CANLI OLCULDU:
        // HTTP 400 "The email field is required."; sorgu dizesiyle 200. Misafir checkout'un
        // hesap SAHIPLENME zincirinin ILK halkasi bu uctu ve KIRIKTI.
        // Kalip verifyEmail ile AYNI (o da _qs kullaniyor).
        async resendVerification(email) { return api._post("/api/auth/resend-verification" + api._qs({ email }), {}); },
        async forgotPassword(email) { return api._post("/api/auth/forgot-password", { email }); },
        async resetPassword(payload) { return api._post("/api/auth/reset-password", payload); },
        async deleteAccount() { return api._del("/api/auth/account"); },   // GDPR anonimleştirme
        async exportMyData() { return api._get("/api/auth/my-data"); },     // GDPR veri dışa aktarma
      };
    }

    // ─────────────────────── Ürün / katalog ───────────────────────
    _buildProducts() {
      const api = this;
      return {
        // DİKKAT: getlist ADMIN yetkisi ister ([RequireUserType(Admin)]). Storefront bunu
        // ÇAĞIRAMAZ - anonim ziyaretçi 403 alır. Katalog için filter() kullanılır.
        // DALGA-3-FIX (P3): uc ARTIK SAYFALI bir zarf donuyor
        // (items + total_count + page + size + total_pages) - storefront yolundaki desenin aynisi.
        // GERIYE DONUK UYUM: admin paneli (admin.html 189/345/440) bu yardimciyi cagirip DIZI
        // bekliyor. Panel DEGISTIRILMEDI; zarfi BURADA aciyoruz ve dizi donuyoruz.
        // KIRPILMA SESSIZ DEGIL: donen sayfa toplamdan azsa konsola UYARI yazilir - operatorun
        // "62 urunum vardi, 100 gorunuyor" gibi bir sessiz kayipla karsilasmasi engellenir.
        // Tam zarfa ihtiyaci olan (ileride sayfalama arayuzu) listPaged() kullanir.
        async list(opts) {
          const res = await api._get("/api/product/getlist" + api._qs(opts || {}));
          const d = res && (res.data !== undefined ? res.data : res);
          if (d && Array.isArray(d.items)) {
            if (typeof d.total_count === "number" && d.total_count > d.items.length) {
              console.warn("Divisima: ürün listesi SAYFALANDI - gösterilen " + d.items.length +
                           " / toplam " + d.total_count + ". Tamamı için sayfa parametresi gerekir.");
            }
            return { data: d.items, success: true, message: res && res.message };
          }
          return res;   // beklenmedik bicimde ham yaniti gec - sessizce bos dizi UYDURMA
        },
        listPaged(opts) { return api._get("/api/product/getlist" + api._qs(opts || {})); },
        get(id) { return api._get("/api/product/get/" + id); },
        // Anonim katalog yolu. Alan adları ProductFilterRequestDto ile birebir:
        // {category_id, sub_category_id, sizes[], colors[], min_price, max_price,
        //  on_sale, in_stock, sort: "price-asc|price-desc|new|old", page, size}
        filter(payload) { return api._post("/api/product/filter", payload || { page: 1, size: 24 }); },
        // Açıklama: Öneriler - ürün detay/sepet sayfasında kişiselleştirme
        frequentlyBought(productId, limit) { return api._get("/api/recommendation/frequently-bought/" + productId + api._qs({ limit })); },
        similar(productId, limit) { return api._get("/api/recommendation/similar/" + productId + api._qs({ limit })); },
      };
    }
    _buildCategories() {
      const api = this;
      return {
        list() { return api._get("/api/category/getlist"); },
        get(id) { return api._get("/api/category/get/" + id); },
      };
    }
    _buildCollections() {
      const api = this;
      return {
        list() { return api._get("/api/collection/getlist"); },
        get(slug) { return api._get("/api/collection/get/" + encodeURIComponent(slug)); },
      };
    }
    _buildSearch() {
      const api = this;
      // Uç [FromQuery] ProductSearchRequestDto bağlıyor: alan adı "query" (q DEĞİL).
      // Önceden "q" gönderiliyordu; parametre HİÇ bağlanmıyor, arama metni yok sayılıp
      // filtresiz sonuç dönüyordu (ölçüldü).
      return {
        products(q, opts) {
          return api._get("/api/search/products" + api._qs(Object.assign({ query: q }, opts || {})));
        },
      };
    }
    _buildContent() {
      const api = this;
      return {
        get(slug) { return api._get("/api/content/get/" + encodeURIComponent(slug)); },
        list() { return api._get("/api/content/getlist"); },
      };
    }

    // ─────────────────────── Sepet / favori ───────────────────────
    _buildCart() {
      const api = this;
      return {
        get() { return api._get("/api/cart"); },
        // DIKKAT: add UPSERT'tir ve adeti ARTIRMAZ, SET eder (CartManager: existing.quantity = dto.quantity).
        // "Adet guncelle" icin de bu cagrilir; yeni MUTLAK adet gonderilir.
        add(productId, size, quantity) { return api._post("/api/cart/add", { product_id: productId, size, quantity }); },
        setQuantity(productId, size, quantity) { return api._post("/api/cart/add", { product_id: productId, size, quantity }); },
        // Uc [HttpDelete("remove")] Remove(int productId, string size) - govde DEGIL SORGU DIZESI bagliyor.
        // Onceden govde gonderiliyordu; parametreler hic baglanmiyor, silme yanlis satiri hedefliyordu.
        remove(productId, size) {
          return api._del("/api/cart/remove" + api._qs({ productId: productId, size: size }));
        },
        clear() { return api._del("/api/cart/clear"); },
      };
    }
    _buildWishlist() {
      const api = this;
      return {
        get() { return api._get("/api/wishlist"); },
        toggle(productId) { return api._post("/api/wishlist/toggle", { product_id: productId }); },
      };
    }

    // ─────────────────────── Sipariş / ödeme ───────────────────────
    _buildOrders() {
      const api = this;
      return {
        // Açıklama: Sipariş durum zaman çizelgesi (takip)
        timeline(orderId) { return api._get("/api/order/timeline/" + orderId); },
        // OrderCreateRequestDto: {customer_id, request_id, address_id, coupon_code, use_store_credit,
        // payment_method (0=Online, 1=Kapida), items:[{product_id,size,quantity}]}
        // TUZAKLAR (olculdu): customer_id > 0 ZORUNLU - FluentValidation controller token'dan
        // degeri set etmeden ONCE kosuyor (token yine de ezer, govdedeki deger yok sayilir);
        // coupon_code non-nullable oldugu icin "" gonderilmeli. Alan adi payment_METHOD.
        place(payload) { return api._post("/api/order/place", payload); },
        // A3 HIBRIT: misafir (hesapsiz) siparis. Uc ANONIM ve YALNIZ kapida odeme kabul eder;
        // payment_method != 1 gelirse SUNUCU REDDEDER (sessizce COD'a dusurme YOK).
        placeAsGuest(payload) { return api._post("/api/guest-checkout/place", payload); },
        get(id) { return api._get("/api/order/get/" + id); },
        my() { return api._get("/api/order/my-orders"); },
        // E3 - FATURA HTML. Uc "text/html" doner, JSON DEGIL; _parse JSON cozemezse ham metni
        // dondurdugu icin burada HTML dizesi elde edilir. ROTA SIRASI: {orderId}/invoice-html
        // (timeline'in tersine - timeline "timeline/{orderId}", bu ise "{orderId}/invoice-html").
        invoiceHtml(orderId) { return api._get("/api/order/" + orderId + "/invoice-html"); },
      };
    }
    _buildPayment() {
      const api = this;
      return {
        // Açıklama: Ödeme başlat -> Iyzico Checkout Form içeriği döner (iframe/HTML gömülür)
        // Yanit: {conversation_id, checkout_form_content}. checkout_form_content Iyzico'nun
        // <script> iceren HTML parcasidir; DOM'a innerHTML ile konulursa script CALISMAZ -
        // script dugumleri yeniden olusturulmalidir (bkz. api-bridge.js embedCheckoutForm).
        initialize(orderId) { return api._post("/api/payment/initialize", { order_id: orderId }); },
        // callback/webhook backend-Iyzico arası; frontend genelde çağırmaz.
        // Callback ARTIK 302 ile storefront'un #/odeme/sonuc sayfasina donuyor (E2).
      };
    }

    // ─────────────────────── Kupon (storefront) ───────────────────────
    _buildCoupons() {
      const api = this;
      return {
        // customer_id sunucuda token'dan set edilir; govdede gondermek gereksiz.
        validate(code, cartTotal) {
          return api._post("/api/coupon/validate", { code: code, cart_total: cartTotal });
        },
      };
    }
    _buildReturns() {
      const api = this;
      return {
        create(payload) { return api._post("/api/return/create", payload); }, // {order_id,product_id,size,quantity,reason,return_type,description}
        my() { return api._get("/api/return/my"); },
      };
    }
    _buildInvoices() {
      const api = this;
      return {
        my() { return api._get("/api/invoice/my"); },
        byOrder(orderId) { return api._get("/api/invoice/order/" + orderId); },
      };
    }
    _buildShipment() {
      const api = this;
      return {
        track(orderId) { return api._get("/api/shipment/track/" + orderId); }, // firma API'sinden güncel durum
      };
    }
    _buildReviews() {
      const api = this;
      return {
        add(payload) { return api._post("/api/productreview/add", payload); }, // {product_id, rating, comment}
        forProduct(productId) { return api._get("/api/productreview/product/" + productId); },
      };
    }
    _buildAddress() {
      const api = this;
      return {
        list() { return api._get("/api/address"); },
        upsert(payload) { return api._post("/api/address/upsert", payload); },
        remove(id) { return api._del("/api/address/delete/" + id); },
      };
    }
    _buildDevice() {
      const api = this;
      return {
        // Açıklama: Push token kaydı (FCM). platform: 0=Web,1=Android,2=iOS
        register(deviceToken, platform) { return api._post("/api/device/register", { device_token: deviceToken, platform: platform || 0 }); },
        unregister(deviceToken) { return api._post("/api/device/unregister", { device_token: deviceToken }); },
      };
    }

    _buildStockNotification() {
      const api = this;
      return {
        // Açıklama: Stoksuz ürün+beden için "gelince haber ver" aboneliği
        subscribe(productId, size, email) { return api._post("/api/stocknotification/subscribe", { product_id: productId, size: size, email: email }); },
        // SPRINT 8 MADDE 10: abonelik yonetimi. Onceden backend'de YALNIZ subscribe vardi -
        // kullanici kurdugu bildirimi ne gorebiliyor ne kapatabiliyordu.
        mine() { return api._get("/api/stocknotification/my"); },
        remove(id) { return api._del("/api/stocknotification/" + id); },
      };
    }

    // E3 - FIYAT DUSUSU ABONELIGI.
    // ROTA TUZAGI (olculdu): PriceDropController "[Route(\"api/price-drop\")]" ile ACIKCA
    // TIRELI tanimlanmis; StockNotificationController ise "[Route(\"api/[controller]\")]"
    // yani TIRESIZ. Yan yana iki farkli kural - E4a'daki "api/product-image" tuzaginin tekrari.
    _buildPriceDrop() {
      const api = this;
      return {
        subscribe(productId, email) { return api._post("/api/price-drop/subscribe", { product_id: productId, email: email }); },
        // SPRINT 8 MADDE 10 (stok bildirimiyle ayni gerekce).
        mine() { return api._get("/api/price-drop/my"); },
        remove(id) { return api._del("/api/price-drop/" + id); },
      };
    }

    // E3 - HESAP. AccountController sinif duzeyinde [RequireUserType(Customer)].
    _buildAccount() {
      const api = this;
      return {
        summary() { return api._get("/api/account/summary"); },
        updateProfile(payload) { return api._put("/api/account/profile", payload); },
        changePassword(payload) { return api._post("/api/account/change-password", payload); },
        notificationPreferences(payload) { return api._put("/api/account/notification-preferences", payload); },
      };
    }

    _buildRecentlyViewed() {
      const api = this;
      return {
        // Açıklama: Son görüntülenen ürünler - kişiselleştirme
        record(productId) { return api._post("/api/recentlyviewed/record/" + productId, {}); },
        list(limit) { return api._get("/api/recentlyviewed" + api._qs({ limit })); },
      };
    }

    // ─────────────────────── Admin ───────────────────────
    // ─────────────────────── Stok yönetimi (admin) ───────────────────────
    // Açıklama: Uçların TAMAMI admin yetkisi ister (StockController sınıf düzeyinde
    // [RequireUserType(Admin)]). Müşteri token'ı ile çağrılırsa 403 döner.
    _buildStock() {
      const api = this;
      return {
        // Beden bazında fiziksel stok + rezerve + satılabilir (E4a admin ucu)
        byProduct(productId) { return api._get("/api/Stock/" + productId); },
        // DİKKAT: new_quantity MUTLAK yeni değerdir, fark (delta) DEĞİL.
        // Backend farkı kendisi hesaplayıp StockMovement'a Adjustment olarak yazar.
        // Panelde operatör delta giriyor; mutlak değere ekran çeviriyor (bkz. admin.html).
        adjust(productId, size, newQuantity, note) {
          return api._post("/api/Stock/adjust", {
            product_id: productId, size: size, new_quantity: newQuantity, note: note || ""
          });
        },
      };
    }

    // ─────────────────────── Ürün görselleri ───────────────────────
    // Açıklama: Route "api/product-image" (TİRELİ) - "api/productimage" 404 döner.
    _buildProductImage() {
      const api = this;
      return {
        // Görselleri herkes görebilir (ürün detayı)
        byProduct(productId) { return api._get("/api/product-image/product/" + productId); },
        // Yükleme admin. multipart/form-data: Content-Type'ı ELLE KOYMA - tarayıcı
        // boundary ile birlikte kendisi yazar. _request FormData'yı zaten olduğu gibi
        // gövdeye koyuyor ve Authorization/CSRF başlıklarını ekliyor.
        upload(productId, file, isPrimary) {
          const fd = new FormData();
          fd.append("productId", String(productId));
          fd.append("file", file);
          fd.append("isPrimary", isPrimary ? "true" : "false");
          return api._post("/api/product-image/upload", fd);
        },
        setPrimary(imageId) { return api._post("/api/product-image/" + imageId + "/primary"); },
        remove(imageId) { return api._del("/api/product-image/" + imageId); },
      };
    }

    _buildAdmin() {
      const api = this;
      return {
        // Dashboard
        summary() { return api._get("/api/dashboard/summary"); },
        dailySales(start, end) { return api._get("/api/dashboard/daily-sales" + api._qs({ start, end })); },
        topProducts(top) { return api._get("/api/dashboard/top-products" + api._qs({ top })); },
        orderStatus() { return api._get("/api/dashboard/order-status"); },
        lowStock(threshold) { return api._get("/api/dashboard/low-stock" + api._qs({ threshold })); },
        // DALGA C / C4: basarisiz arka plan isleri. Hangfire panosu tarayicidan ERISILEMEZ
        // (uygulamada tek kimlik semasi JwtBearer; tarayici gezintisi Authorization basligi
        // gondermez), bu yuzden operatorun arka plan hatalarini gorebilecegi TEK yuzey budur.
        failedJobs(take) { return api._get("/api/dashboard/failed-jobs" + api._qs({ take })); },
        // Ürün yönetimi
        addProduct(p) { return api._post("/api/product/add", p); },
        updateProduct(p) { return api._put("/api/product/update", p); },
        deleteProduct(id) { return api._del("/api/product/delete/" + id); },
        // Kupon
        addCoupon(c) { return api._post("/api/coupon/add", c); },
        updateCoupon(c) { return api._put("/api/coupon/update", c); },
        deleteCoupon(id) { return api._del("/api/coupon/delete/" + id); },
        listCoupons() { return api._get("/api/coupon/getlist"); },
        // İade
        pendingReturns() { return api._get("/api/return/pending"); },
        processReturn(returnId, approve, adminNote) { return api._post("/api/return/process", { return_id: returnId, approve, admin_note: adminNote }); },
        // Fatura
        generateInvoice(orderId) { return api._post("/api/invoice/generate/" + orderId); },
        // Kargo
        createShipment(p) { return api._post("/api/shipment/create", p); }, // {order_id,carrier,tracking_number,estimated_delivery}
        shipmentByOrder(orderId) { return api._get("/api/shipment/order/" + orderId); },
        // Tüm siparişler (admin, filtre+sayfalama)
        allOrders(filter) { return api._post("/api/order/admin/list", filter || { page: 1, page_size: 20 }); },
        // Sipariş durumu (PATCH /api/order/status - fatura+bildirim tetikler)
        changeOrderStatus(orderId, orderStatus) { return api._patch("/api/order/status", { id: orderId, order_status: orderStatus }); },
        // Denetim
        auditLogs() { return api._get("/api/auditlog/list"); },
      };
    }
  }

  global.DivisimaAPI = DivisimaAPI;
  if (typeof module !== "undefined" && module.exports) module.exports = DivisimaAPI;
})(typeof window !== "undefined" ? window : this);
