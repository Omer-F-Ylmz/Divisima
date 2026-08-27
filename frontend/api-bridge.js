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
  // DALGA-4-FIX-2 / M1: IKINCI LITERAL YOK. Taban TEK KAYNAKTAN gelir
  // (index.html'deki meta[name="divisima-api-origin"]); eksikligini oradaki guard
  // GURULTULU bildirir. Burada sessiz bir "localhost" yedegi birakmak, yanlis
  // yapilandirilmis bir dagitimda istekleri SESSIZCE kullanicinin KENDI makinesine
  // yollamak demekti - M1'in ta kendisi (LAN'dan acilinca ERR_BLOCKED_BY_CLIENT,
  // katalog BOS). Bos taban gorunur sekilde bozuktur; sessiz yanlis taban degildir.
  var API_BASE = window.DIVISIMA_API_BASE || "";
  if (!API_BASE && window.console && console.error)
    console.error('[DIVISIMA YAPILANDIRMA] API tabani belirlenemedi - meta[name="divisima-api-origin"] eksik.');
  var api = new DivisimaAPI(API_BASE);
  window.divisimaApi = api; // konsoldan erişim

  // SPRINT 8 MADDE 5: sayfa boyutu ARTIK TELAFIYE BAGLI DEGIL.
  // Onceki not: "liste yolu stok/beden dondurmedigi icin her urunun detayi ayrica cekiliyor;
  // 24 urun = 24 detay cagrisi". Backend liste yolu doldurulunca o telafi kaldirildi.
  // 24 degeri korunuyor - bu artik bir SINIR degil, makul bir sayfa boyutu.
  var CATALOG_PAGE_SIZE = 24;

  // ILK YUKLEME YARISI ICIN: kullanicinin GERCEKTEN ISTEDIGI kategori slug'i.
  //
  // OLCULDU: index.html'in ozgun `showCategory`'si taninmayan slug'i 'tumu'ya cevirip
  // ADRESI DE yeniden yaziyor. Bu betik `defer` ile yuklendigi icin (Dalga 3'un render
  // engelini kaldiran duzeltmesi) index.html'in satir ici router'i DAHA ONCE kosuyor -
  // yani bu satir calistiginda `location.hash` ARTIK `#/kategori/tumu` ve "taninmayan
  // rota" bilgisi KAYBOLMUS oluyor. Olculdu:
  //     navigation.name -> ".../index.html?v=...#/kategori/olmayan"   (ORIJINAL)
  //     location.href   -> ".../index.html?v=...#/kategori/tumu"      (YENIDEN YAZILMIS)
  // Bu yuzden kaynak `location.hash` DEGIL, gezinme kaydinin ADRESIDIR; o, belge hangi
  // adresle getirildiyse ONU tasir ve sonradan yapilan hash yeniden yazimlarindan ETKILENMEZ.
  // `defer`i kaldirmak da bir cozum olurdu ama Dalga 3'te OLCUMLE kazanilan
  // "render-bloklayan kaynak 5 -> 0" iyilesmesini geri alirdi.
  var ILK_KATEGORI_SLUG = (function () {
    var kaynak = "";
    try {
      var nav = performance.getEntriesByType("navigation")[0];
      kaynak = (nav && nav.name) || "";
    } catch (e) { kaynak = ""; }
    if (!kaynak) kaynak = location.href || "";
    var m = kaynak.match(/#\/kategori\/([^\/?&]+)/);
    return m ? m[1] : "";
  })();

  // ── Yardımcılar ──
  // MFIX-3b/(3): notify AYNI IMZAYI tasir - notify(mesaj, tip). Tip index.html'in
  // toast(msg, tip) sozlesmesine AYNEN gecer; verilmezse orada "info" varsayilir
  // ("ok" DEGIL - tipsiz cagrinin onay isareti basmasi sinif olarak olu bir kusurdur,
  // T1 ekraninda canli olculdu).
  function notify(msg, tip) {
    try { if (typeof window.toast === "function") window.toast(msg, tip); else console.log("Divisima:", msg); } catch (e) {}
  }
  function unwrap(r) { return (r && r.data !== undefined) ? r.data : r; }

  // HTML kaçışı. index.html kendi esc()'sini tanımlıyor; yüklenme sırası değişirse diye
  // burada da bir tane var - sunucudan gelen metin innerHTML'e kaçışsız GİRMEZ.
  function esc(s) {
    return String(s == null ? "" : s).replace(/[&<>"']/g, function (c) {
      return { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c];
    });
  }

  // ── MFIX-3 / F-M2: i18n KOPRUSU ───────────────────────────────────────────
  // api-bridge'in KULLANICI-GORUNUR dizgeleri index.html'in MEVCUT sozluk mekanizmasina
  // baglanir. YENI MEKANIZMA ICAT EDILMEDI: sozluk (T / AR) index.html'de, cozucu
  // window.t(anahtar); applyI18n ve setLang o mekanizmayi zaten suruyor ve setLang
  // renderAccount/renderCheckout/renderCart/renderFavs'i YENIDEN CIZIYOR - yani
  // buradaki dizgeler de dil degisiminde tazelenir.
  // YEDEK metin bilincli: depoda ZATEN kullanilan kalip
  // (`typeof window.t === "function" ? window.t("load_more") : "Daha Fazla Yukle"`).
  // Sozluk yuklenmemisse ya da anahtar yoksa ekran BOS/HAM ANAHTAR gostermez.
  function ceviri(anahtar, yedek) {
    try {
      if (typeof window.t === "function") {
        var v = window.t(anahtar);
        if (v && v !== anahtar) return v;
      }
    } catch (e) {}
    return yedek !== undefined ? yedek : anahtar;
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
  // D3 (gercek olcek provasi) DUZELTMESI - SLUG UZAYLARI AYRISIYORDU.
  // OLCULDU (403 urunluk katalogla): urunun `cat` degeri `slugify(category_name)` ile
  // uretiliyordu, oysa rota ve etiket tarafi VERITABANI SLUG'ini kullaniyor:
  //     kategori adi "D3OLCEK Kategori 1" -> slugify(ad) = "d3olcek-kategori-1"
  //     veritabani slug'i                                = "d3olcek-1"
  // Ikisi ESLESMIYORDU. Sonuclari: (a) kategori rotasi urunleri suzemiyor,
  // (b) `registerCategoryLabels` etiketi `cat_<db-slug>` altina yaziyor ama urun
  // `cat_<slugify-ad>` ile ariyor -> E1'de bir kez duzeltilen "ham anahtar basimi"
  // (`cat_e4a-kategori`) adi slug'indan FARKLI olan HER kategori icin geri geliyordu.
  // Basit adlarda (Elbise -> elbise) ikisi tesadufen ortustugu icin bugune kadar gorunmedi.
  // ARTIK VERITABANI SLUG'I ONCELIKLI - tek dogruluk kaynagi kategori satirinin kendisi.
  function categorySlugOf(p) {
    var c = CATEGORY_BY_ID[p.category_id];
    if (c && c.slug) return c.slug;
    if (c && c.name) return slugify(c.name);
    if (p.category_name) return slugify(p.category_name);
    return "tumu";
  }

  function mapProduct(p) {
    return {
      id: p.id,
      name: p.name,
      brand: p.brand || "Divisima",
      cat: categorySlugOf(p),
      // MFIX-2 / F-M9: beden tablosu GERCEK size-guide'dan cekilecegi icin urun
      // KATEGORI KIMLIGINI de tasimali (GET /api/size-guide/category/{categoryId}).
      catId: Number(p.category_id) || 0,
      sub: "",
      price: Number(p.price) || 0,
      old: p.old_price ? Number(p.old_price) : 0,
      // VITRIN-FIX-2 / F-D1: yildiz ve yorum sayisi SUNUCUDAN gelir. index.html eskiden
      // bunlari urunun id'sinden tohumlanan bir PRNG ile UYDURUYORDU. ProductListResponseDto
      // ve ProductDetailResponseDto ikisi de bu iki alani tasiyor (canli olculdu).
      rating: Number(p.average_rating) || 0,
      rvcount: Math.max(0, Math.floor(Number(p.review_count) || 0)),
      cart: Number(p.price) || 0,
      stock: Number(p.total_stock) || 0,   // SPRINT 8 madde 5: liste yolu ARTIK gercek degeri donduruyor
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

  // ── D3: GERCEK SAYFALAMA ALTYAPISI ─────────────────────────────────────────
  // OLCULEN ZARAR (403 urunluk katalog, tarayicida): `loadCatalog` HER ZAMAN
  // { page:1, size:24 } cekiyor ve `replaceProducts` bellegi bu 24 urunle DEGISTIRIYORDU.
  // Ikinci sayfa HIC istenmiyordu; kategori rotalari ve "Daha Fazla Yukle" tamamen bu 24
  // urun uzerinde ISTEMCI TARAFINDA calisiyordu. Sonuc: musteri katalogun ilk 24 urununu
  // gezebiliyor, kalan %94'e GEZINEREK ULASAMIYORDU (tek kacis arama).
  // 3 urunluk gelistirme verisinde GORUNMEZ bir kusurdu.
  //
  // Backend ZATEN sayfali (Dalga 3: items + total_count + page + size + total_pages);
  // eksik olan yalnizca istemciydi.

  // SAYFALAR BIRIKIR - EZMEZ. Kimlige gore tekillestirilir ki ayni urun iki kez girmesin
  // ve kullanici bir kategoriye gidip GERI DONDUGUNDE liste SIFIRLANMASIN.
  function appendProducts(mapped) {
    if (typeof window.PRODUCTS === "undefined") { window.PRODUCTS = []; }
    var varOlan = {};
    window.PRODUCTS.forEach(function (p) { varOlan[p.id] = true; });
    var yeni = mapped.filter(function (p) { return !varOlan[p.id]; });
    Array.prototype.push.apply(window.PRODUCTS, yeni);
    return yeni.length;
  }

  function pageMeta(res) {
    var d = unwrap(res) || {};
    return {
      sayfa: Number(d.page || d.Page || 1) || 1,
      toplamSayfa: Number(d.total_pages || d.TotalPages || 1) || 1,
      toplamKayit: Number(d.total_count || d.TotalCount || 0) || 0
    };
  }

  // Sayfalama durumu FILTRE BASINA tutulur: "tum katalogun 3. sayfasi" ile
  // "5 numarali kategorinin 3. sayfasi" AYRI seylerdir.
  var katalogSayfaDurumu = {};
  function filtreImzasi(f) { return "kategori:" + (((f && f.category_id) || 0)); }

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
      (withRetry ? '<button id="dvsRetry" style="margin-top:14px;padding:9px 16px;border:1px solid #e8e4de;border-radius:8px;background:#fff;cursor:pointer">' + ceviri("b_tekrar_dene_btn") + '</button>' : "") +
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

  // ── TAKSONOMI: GEZINME MENUSU VERITABANINDAN URETILIR ──────────────────────
  //
  // OLCULEN ZARAR (D3'te 403 urunluk katalogla): index.html'in kategori menusu SABIT bir
  // dizidir (`NAV` = yeni/elbise/ust/alt/dis/aksesuar/indirim) ve veritabaniyla yalnizca
  // "elbise" uzerinden kesisiyordu. Iki sonucu vardi:
  //   (a) Veritabaninda VAR OLAN ama navda olmayan bir kategoriye ROTA YOKTU. Olculdu:
  //       `#/kategori/d3olcek-3` router tarafindan `#/kategori/tumu`ya SESSIZCE YENIDEN
  //       YAZILIYORDU (index.html: `if(!CAT_INFO[cat]&&!navBySlug[cat])cat='tumu';`).
  //   (b) Navda VAR ama veritabaninda OLMAYAN bir kategori (ust/alt/aksesuar...) "gecerli"
  //       sayilip BOS bir kategori sayfasi ciziyordu.
  // Gercek katalog aktarildiginda (a) sikintisi HER kategori icin gecerli olacakti.
  //
  // EK ISTEK MALIYETI YOK: `/api/category/getlist` ZATEN ilk yuklemede cagriliyor
  // (`loadCategories`); menu o yanittan uretiliyor, yeni bir istek EKLENMIYOR.
  //
  // ALT KATEGORILER - OLCULDU, UYDURULMADI: `CategoryResponseDto` ZATEN `sub_categories`
  // tasiyor ve `CategoryManager.GetList` onu dolduruyor; uc bugun her kategori icin `[]`
  // donuyor (`sub_categories` tablosu BOS ve onlar icin ayri bir uc YOK). Yani sozlesme
  // MEVCUT: dolu geldigi gun alt menu KENDILIGINDEN cizilir, bos oldugu surece cizilmez.
  // Uydurma bir alt-kategori kaynagi EKLENMEDI.
  //
  // YEDEK DAVRANIS - MENU BOS GORUNMEZ: "tumu", "yeni" ve "indirim" VERITABANI KATEGORISI
  // DEGILDIR; bellekteki urunler uzerinden turetilen ISTEMCI TARAFI GORUNUMLERDIR (tamami /
  // en yeniler / indirimdekiler). Bu yuzden kategori tablosu BOS olsa ya da uc DUSSE bile
  // menude bu ucu kalir ve vitrin gezilebilir olur. Yani "kategori yoksa menu bos gorunmez"
  // sarti, uydurma bir yedek listeyle DEGIL, zaten DB'ye bagli olmayan gorunumlerle saglanir.
  var SENTETIK_ROTALAR = ["tumu", "yeni", "indirim"];

  // Taninmayan bir kategori rotasinda 404'e dustuk mu (baslik duzeltmesi icin).
  var sonKategoriBulunamadi = false;

  function kategoriEtiketiKaydet(anahtar, ad) {
    if (typeof window.T !== "object" || !window.T) return;
    // MEVCUT CEVIRI KORUNUR: index.html'de `cat_elbise` gibi anahtarlarin [tr, en] cifti
    // olabilir; veritabaninda ceviri alani YOK. Slug'in TEK KAYNAGI veritabanidir, ama
    // ETIKETIN cevirisi varsa onu silmek Ingilizce vitrini bozardi.
    if (!window.T[anahtar]) window.T[anahtar] = [ad, ad];
    if (typeof window.AR === "object" && window.AR && window.AR[anahtar] === undefined) window.AR[anahtar] = ad;
  }

  function menuyuVeritabanindanKur() {
    if (typeof window.NAV === "undefined") return;   // index.html henuz kurulmadi

    var dbKategoriler = (CATEGORIES || [])
      .filter(function (c) { return c && c.is_active !== false && (c.slug || c.name); })
      .slice()
      .sort(function (a, b) { return (a.display_order || 0) - (b.display_order || 0); })
      .map(function (c) {
        var slug = c.slug || slugify(c.name);
        var item = { slug: slug, label: c.name };
        var alt = (c.sub_categories || []).filter(function (s) { return s && (s.slug || s.name); });
        if (alt.length) {
          item.subs = alt.map(function (s) {
            var sslug = s.slug || slugify(s.name);
            kategoriEtiketiKaydet("sub_" + sslug, s.name);
            return { slug: sslug, label: s.name };
          });
        }
        kategoriEtiketiKaydet("cat_" + slug, c.name);
        return item;
      });

    // "yeni" basta, "indirim" sonda - index.html'in ozgun sirasi korunur.
    var yeniNav = [{ slug: "yeni", label: "Yeni Gelenler" }]
      .concat(dbKategoriler)
      .concat([{ slug: "indirim", label: "İndirim", sale: true }]);

    window.NAV = yeniNav;
    window.navBySlug = {};
    yeniNav.forEach(function (n) { window.navBySlug[n.slug] = n; });

    // CAT_INFO artik YALNIZ GECERLI slug'lari tasir. index.html'deki sabit girdiler
    // (elbise/ust/alt/dis/aksesuar) veritabaninda karsiligi olmasa bile rotayi "gecerli"
    // yapiyordu - bu yuzden yeniden kuruluyor, uzerine eklenmiyor.
    // NOT: CAT_INFO'nun `t`/`d` alanlari hicbir yerde CIZILMIYOR (tarandi: tek kullanim
    // `showCategory`in uyelik kontrolu), bu yuzden veritabaninda olmayan bir "aciklama"
    // UYDURULMUYOR.
    var eskiInfo = window.CAT_INFO || {};
    var yeniInfo = {};
    SENTETIK_ROTALAR.forEach(function (s) { yeniInfo[s] = eskiInfo[s] || { t: s, d: "" }; });
    dbKategoriler.forEach(function (c) { yeniInfo[c.slug] = { t: c.label, d: "" }; });
    window.CAT_INFO = yeniInfo;

    // Filtre kenar cubugu ve ana sayfa pill'leri de ayni kaynaktan.
    window.MAINS = [["tumu", "Tümü"]].concat(dbKategoriler.map(function (c) { return [c.slug, c.label]; }));

    ["renderNav", "renderMob", "renderPills"].forEach(function (fn) {
      try { if (typeof window[fn] === "function") window[fn](); } catch (e) { console.warn("Divisima: " + fn + " cizilemedi", e); }
    });

    console.log("Divisima: menu " + dbKategoriler.length + " veritabani kategorisinden uretildi");
  }

  // TANINMAYAN ROTA ARTIK SESSIZCE YENIDEN YAZILMIYOR.
  // Onceden `showCategory` bilinmeyen slug'i 'tumu'ya cevirip TUM KATALOGU gosteriyordu -
  // kullanici yanlis sayfada oldugunu ANLAYAMIYORDU ve arama motoru ayni icerigi birden
  // cok adreste goruyordu. Artik uygulamanin KENDI 404'une dusuyor (show404 zaten var).
  function taksonomiRotasiniBagla() {
    // GOZ-FIX / F-G2: BAYRAK HER ROTA GIRISINDE SIFIRLANIR.
    // Eskiden `sonKategoriBulunamadi` YALNIZCA showCategory sarmalayicisinin icinde
    // sifirlaniyordu; kategori olmayan hicbir rota (#/giris, #/odeme, #/hesabim, #/sozlesme,
    // #/) onu temizlemiyordu ve setDocTitle sarmalayicisi DOGRU basligi 404 ile eziyordu.
    // OLCULDU: taze yuklemede #/giris -> ceviri("b_sayfa_bulunamadi"); gecerli bir kategori
    // ziyaret edilince duzeliyor; TEK bir bozuk kategoriden sonra TUM rotalar yeniden
    // ceviri("b_sayfa_bulunamadi") oluyordu. Router her rota degerlendirmesinin BASINDA calisir,
    // yani bayrak yalnizca o turdaki kategori-404 yolunda TRUE kalabilir.
    if (typeof window.router === "function" && !window.router.__taksonomi) {
      var _router = window.router;
      var sarmalR = function () {
        sonKategoriBulunamadi = false;
        return _router.apply(this, arguments);
      };
      sarmalR.__taksonomi = true;
      window.router = sarmalR;
    }

    if (typeof window.showCategory === "function" && !window.showCategory.__taksonomi) {
      var _showCategory = window.showCategory;
      var sarmal = function (cat, sub) {
        var gecerli = SENTETIK_ROTALAR.indexOf(cat) >= 0 || !!(window.navBySlug || {})[cat];
        if (!gecerli && typeof window.show404 === "function") {
          sonKategoriBulunamadi = true;
          window.show404();
          return;
        }
        sonKategoriBulunamadi = false;
        return _showCategory.apply(this, arguments);
      };
      sarmal.__taksonomi = true;
      window.showCategory = sarmal;
    }

    // Baslik: router `setDocTitle`i showCategory'DEN SONRA cagirir ve "kategori" dalina
    // duserek kategori basligini yazar. 404'e dustugumuzde bu YANLIS olur.
    if (typeof window.setDocTitle === "function" && !window.setDocTitle.__taksonomi) {
      var _setDocTitle = window.setDocTitle;
      var sarmalT = function () {
        _setDocTitle.apply(this, arguments);
        if (sonKategoriBulunamadi) {
          var en = (typeof window.lang !== "undefined" && window.lang === "en");
          document.title = (en ? "Page Not Found" : ceviri("b_sayfa_bulunamadi")) + " · Divisima";
        }
      };
      sarmalT.__taksonomi = true;
      window.setDocTitle = sarmalT;
    }

    // 404 sayfasinin "populer kategoriler" satiri da SABIT slug'lar tasiyordu
    // (elbise/ust/alt/dis/aksesuar). Menu veritabanindan gelince o baglantilar OLU kalirdi.
    if (typeof window.show404 === "function" && !window.show404.__taksonomi) {
      var _show404 = window.show404;
      var sarmal4 = function () {
        _show404.apply(this, arguments);
        try {
          var kutu = document.querySelector(".nf-cats");
          if (!kutu) return;
          var gercek = (window.NAV || []).filter(function (n) { return SENTETIK_ROTALAR.indexOf(n.slug) < 0; });
          // KATEGORI YOKSA SABIT SATIR BIRAKILAMAZ - OLCULDU: o satirdaki bes baglanti
          // (elbise/ust/alt/dis/aksesuar) artik 404'e dusuyor, yani 404 sayfasi kullaniciyi
          // BASKA BIR 404'e gonderiyordu. Yedek olarak HER ZAMAN gecerli olan sentetik
          // gorunumler konur (bunlar veritabanina bagli DEGILDIR).
          var satir = gercek.length
            ? gercek.slice(0, 5)
            : [{ slug: "tumu" }, { slug: "yeni" }, { slug: "indirim" }];
          var etiket = kutu.querySelector("span");
          kutu.innerHTML = (etiket ? etiket.outerHTML : "") + satir.map(function (n) {
            return '<a href="#/kategori/' + n.slug + '">' +
              (typeof window.t === "function" ? window.t("cat_" + n.slug) : n.label) + "</a>";
          }).join("");
        } catch (e) { console.warn("Divisima: 404 kategori satiri tazelenemedi", e); }
      };
      sarmal4.__taksonomi = true;
      window.show404 = sarmal4;
    }
  }

  // ILK YUKLEME YARISI - OLCULDU ve KAPATILDI.
  // Acilistaki `router()` cagrisi, bu sarmalayicilar BAGLANMADAN once kosuyor; yani sayfa
  // DOGRUDAN `#/kategori/olmayan` ile acildiginda index.html'in ozgun mantigi devreye girip
  // rotayi SESSIZCE 'tumu'ya cevirmeye devam ediyordu (olculdu: 404 hic gorunmedi).
  // Bu, depoda daha once E3/M12 ve D3-FIX'te yasanan yarisin AYNISI. Sarmalayicilar
  // kurulduktan sonra rota BIR KEZ DAHA degerlendirilir.
  function kategoriRotasiniTazele() {
    var m = (location.hash || "").match(/^#\/kategori\/([^\/?]+)/);
    if (!m) return;
    var suanki = m[1];
    var istenen = ILK_KATEGORI_SLUG || suanki;
    try {
      if (istenen !== suanki) {
        // Adres yeniden yazilmis. ISTENEN slug'i geri koy - `hashchange` router'i tetikler
        // ve bu kez sarmalanmis `showCategory` calisir (taninmiyorsa 404).
        location.hash = "#/kategori/" + istenen;
        return;
      }
      if (typeof window.router === "function") window.router();
    } catch (e) { console.warn("Divisima: kategori rotasi tazelenemedi", e); }
  }

  // ── Ürünler (gerçek API - ANONİM yol) ──────────────────────────────────────
  // SPRINT 8 MADDE 5: "enrichAll" (6 eszamanli, urun basina detay cagrisi) KALDIRILDI.
  // Backend liste yolu artik category_name + total_stock + sizes donduruyor; telafiye
  // gerek kalmadi. Detay zenginlestirmesi tembel hale geldi (bkz. wireProductDetail).

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
      // D3: sayfalama durumu KAYDEDILIR - "daha var mi" sorusu ancak boyle yanitlanir.
      var m = pageMeta(res);
      katalogSayfaDurumu[filtreImzasi(payload)] =
        { sayfa: m.sayfa, toplamSayfa: m.toplamSayfa, toplamKayit: m.toplamKayit };
      if (!list.length) {
        replaceProducts([]);           // mock KALMAZ
        showCatalogState(ceviri("b_katalog_bos"), ceviri("b_katalog_bos_alt"), false);
        console.log("Divisima: API 0 ürün döndü - boş katalog durumu gösteriliyor");
        return [];
      }
      var mapped = list.map(mapProduct);
      replaceProducts(mapped);
      // SPRINT 8 MADDE 5: EAGER ZENGINLESTIRME KALDIRILDI.
      // Liste yolu artik category_name + total_stock + sizes DOLDURUYOR (backend), dolayisiyla
      // grid icin urun basina AYRI detay cagrisi GEREKMIYOR. Onceden bir vitrin sayfasi
      // 1 + 24 = 25 istek demekti; artik TEK istek.
      // Detay (aciklama + beden BAZINDA stok) hala gerekli ama TEMBEL: wireProductDetail,
      // kullanici urunu ACTIGINDA enrichProduct'i cagiriyor. Bu bir N+1 degil, kullanici
      // eylemine bagli tek cagri.
      rerender();
      console.log("Divisima: " + mapped.length + " ürün API'den yüklendi (tek istek)");
      return mapped;
    } catch (e) {
      replaceProducts([]);             // mock KALMAZ - yalan vitrin gösterilmez
      // GOZ-FIX / F-Ö3: SAGLAYICI/TARAYICI METNI KULLANICIYA SIZMAZ. `e.message` burada
      // tarayicinin kendi dizgesi olabiliyor ("Failed to fetch", "NetworkError...") ve
      // musteriye Ingilizce teknik metin gosteriyordu. Ayrinti konsola, kullaniciya Turkce
      // ve EYLEM ICEREN metin.
      console.warn("Divisima: katalog alınamadı (ayrıntı)", e && e.message);
      showCatalogState(ceviri("b_urunler_yuklenemedi"), ceviri("b_tekrar_dene"), true);
      console.warn("Divisima: katalog alınamadı", e);
      return [];
    }
  }
  window.divisimaReloadCatalog = loadCatalog;

  // ── D3: SONRAKI SAYFAYI GERCEKTEN API'DEN CEK ──────────────────────────────
  // Donen deger EKLENEN urun sayisidir (0 = alinacak sayfa kalmadi).
  async function sonrakiSayfayiCek(kategoriId) {
    var filtre = kategoriId ? { category_id: kategoriId } : {};
    var imza = filtreImzasi(filtre);
    var d = katalogSayfaDurumu[imza];
    if (d && d.sayfa >= d.toplamSayfa) return 0;         // sayfa bitti
    var istenen = d ? d.sayfa + 1 : 1;
    var payload = Object.assign(
      { page: istenen, size: CATALOG_PAGE_SIZE, sort: "new", sizes: [], colors: [] },
      filtre
    );
    try {
      var res = await api.products.filter(payload);
      var m = pageMeta(res);
      katalogSayfaDurumu[imza] = { sayfa: m.sayfa, toplamSayfa: m.toplamSayfa, toplamKayit: m.toplamKayit };
      var eklenen = appendProducts(pageItems(res).map(mapProduct));
      if (eklenen) rerender();
      return eklenen;
    } catch (e) {
      // SESSIZ DEGIL: kullanici "daha fazla" dedi ve bir sey olmadiysa bunu OGRENMELI.
      notify(ceviri("err_more"),'err');
      console.warn("Divisima: sonraki katalog sayfasi alinamadi", e);
      return 0;
    }
  }
  window.divisimaSonrakiSayfa = sonrakiSayfayiCek;

  // Aktif kategori rotasinin GERCEK veritabani id'si (yoksa 0 = tum katalog).
  // index.html'in gezinme taksonomisi SABITTIR ve veritabaniyla birebir ORTUSMEZ
  // (olculdu: nav sluglari yeni/elbise/ust/alt/dis..., DB sluglari elbise + d3olcek-*).
  // Karsiligi OLMAYAN bir rota icin sunucuya gonderilecek bir kategori de yoktur -
  // o durumda tum katalog sayfalanir. Uydurma id gonderilmez.
  function aktifKategoriId() {
    try {
      var st = window.catState;
      if (!st || !st.cat || st.cat === "tumu" || st.cat === "yeni") return 0;
      var harita = window.divisimaCategoryIdBySlug || {};
      return harita[st.cat] || 0;
    } catch (e) { return 0; }
  }

  // Kategori rotasina ILK girildiginde o kategorinin sayfasi SUNUCUDAN cekilir.
  // Onceden hicbir istek atilmiyordu: kategori sayfasi, ana sayfanin 24 urunu icinden
  // tesadufen o kategoriye dusenleri gosteriyordu (olculdu: kategori basina 3 urun).
  var kategoriIlkYuklemesi = {};
  function kategoriSayfasiniHazirla() {
    var kid = aktifKategoriId();
    if (!kid || kategoriIlkYuklemesi[kid]) return;
    kategoriIlkYuklemesi[kid] = true;
    sonrakiSayfayiCek(kid);
  }

  // "Daha Fazla Yukle" dugmesi: index.html'in kendi dugmesi YALNIZ bellekteki listeyi
  // ilerletir ve bellek bitince KAYBOLUR. Bellekteki kalan bittiginde ama sunucuda sayfa
  // VARSA, dugmeyi biz yeniden koyuyoruz ve o dugme GERCEK bir API sayfasi cekiyor.
  function sayfalamaDugmesiniTazele() {
    try {
      var sarmal = document.getElementById("loadMoreWrap");
      if (!sarmal) return;
      if (sarmal.querySelector("button")) return;        // yerel kalan var - index.html'in dugmesi duruyor
      var kid = aktifKategoriId();
      var d = katalogSayfaDurumu[filtreImzasi(kid ? { category_id: kid } : {})];
      if (!d || d.sayfa >= d.toplamSayfa) return;        // sunucuda da sayfa kalmadi
      var kalan = Math.max(0, d.toplamKayit - (window.PRODUCTS || []).length);
      var etiket = (typeof window.t === "function" ? window.t("load_more") : "Daha Fazla Yükle");
      sarmal.innerHTML = '<button class="load-more" id="loadMoreApiBtn">' + etiket +
        (kalan ? " (" + kalan + ")" : "") + "</button>";
      var b = document.getElementById("loadMoreApiBtn");
      if (b) b.onclick = async function () {
        b.disabled = true;
        var eklenen = await sonrakiSayfayiCek(kid);
        if (eklenen && window.catState) window.catState.shown += eklenen;
        if (typeof window.renderCatGrid === "function") window.renderCatGrid();
        b.disabled = false;
      };
    } catch (e) { console.warn("Divisima: sayfalama dugmesi tazelenemedi", e); }
  }

  // renderCatGrid SARMALANIR (index.html'e dokunulmaz - deponun yerlesik idiyomu).
  function sayfalamayiBagla() {
    if (typeof window.renderCatGrid !== "function" || window.renderCatGrid.__d3sayfalama) return;
    var orij = window.renderCatGrid;
    var sarmal = function () {
      var r = orij.apply(this, arguments);
      kategoriSayfasiniHazirla();
      sayfalamaDugmesiniTazele();
      return r;
    };
    sarmal.__d3sayfalama = true;
    window.renderCatGrid = sarmal;
  }

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
        // F-D1: detay ucu de average_rating/review_count tasiyor; liste yolundan gelen
        // degeri TAZELER. Uydurma yok - alan yoksa mevcut deger korunur.
        if (d.average_rating !== undefined) p.rating = Number(d.average_rating) || 0;
        if (d.review_count !== undefined) p.rvcount = Math.max(0, Math.floor(Number(d.review_count) || 0));
        /* MFIX-3b/(6): OLU DAL SOKULDU. `if (d.image_url)` hicbir zaman girmiyordu -
           anonim detay ucu (ProductDetailResponseDto) image_url ALANI DONDURMUYOR
           (canli alan listesi MFIX-3'te olculdu). Guard'li oldugu icin zarar yoktu ama
           okuyucuya "detaydan gorsel gelebilir" diye YANLIS bir sozlesme vaat ediyordu.
           Gorsel gerekirse dogru yol AYRI bir uctur (product-image/product/{id}) ve o
           AYRI BIR KARARDIR - burada sessizce ikinci bir istek ACILMAZ. */
        if (!p.cat || p.cat === "tumu") p.cat = categorySlugOf(d);
        if (d.stocks && d.stocks.length) {
          // MFIX-2 / F-M1-H3: ONCE (olculdu, urun 937) detay acilinca liste 29 -> 35 EZILIYORDU.
          // Sebep: ProductStockDto YALNIZ stock_quantity (FIZIKSEL) tasiyor; liste yolu ise
          // Sprint 8 madde 5'ten beri total_stock/sizes degerlerini available uzerinden
          // (stock_quantity - reserved_quantity) DOLDURUYOR. Yani detayin toplami YANLIS,
          // listenin toplami DOGRU. Detayin toplami stok alanina YAZILMIYOR artik (satir
          // KALDIRILDI - kalibi buraya YAZMIYORUM, kayitli ders: yorum taramayi kirletir) ve eski
          // yorum ("liste yolunun 0 dondurdugu") o tarihten beri BAYATTI.
          // Beden listesi de LISTENIN sozune uyar: liste tamamen rezerve bedeni ZATEN
          // disliyor (urun 932: total_stock 0, sizes []), detay ise onu hala gosterirdi.
          var listeBedenleri = (p.sizes && p.sizes.length) ? p.sizes.map(String) : null;
          var map = {};
          d.stocks.forEach(function (s) {
            if (listeBedenleri && listeBedenleri.indexOf(String(s.size)) < 0) return;
            map[s.size] = Number(s.stock_quantity) || 0;
          });
          // Liste beden bildirmediyse (eski/dar yanit) detayinkine duseriz - bos kalmasin.
          if (!listeBedenleri) {
            p.sizes = d.stocks.map(function (s) { return isNaN(+s.size) ? s.size : +s.size; });
            d.stocks.forEach(function (s) { map[s.size] = Number(s.stock_quantity) || 0; });
          }
          p._ss = map;
          // MFIX-B / K1: BU SINIR KAPANDI. Yukaridaki "beden BASINA ust sinir HALA FIZIKSEL"
          // notu artik GECERSIZ - anonim detay ucu stock_quantity alaninda SATILABILIR adedi
          // donuyor (ProductManager.GetById -> StokHesabi.Satilabilir), dolayisiyla _ss ve ona
          // bagli beden-basi ust sinir da satilabilirle sinirli. Canli olculdu (urun 937):
          // _ss {S:11, M:4, L:11} ve addToCart(937,"M",5) -> sepette 4.
          // DAVRANIS DEGISIKLIGI (durust kayit): kirpma esigi artik BASKALARININ rezervasyonuna
          // duyarli; sepetteki adet baskasi rezerve ettiginde sessizce dusebilir. Yon DOGRU
          // (sunucunun CheckStock'u zaten satilabiliri kullaniyor, yani checkout ile TUTARLI).
        }
      }
      detailCache[id] = d;
      return d;
    } catch (e) {
      console.warn("Divisima: ürün detayı alınamadı #" + id, e);
      return null;
    }
  }
  // VITRIN-FIX-2 / F-D1: GERCEK yorumlar TEMBEL cekilir. index.html'deki uydurma uretim
  // kaldirildigi icin yorum METINLERI artik YALNIZCA buradan gelebilir.
  // Uc ANONIM ve yalniz ONAYLI yorumlari doner: GET /api/productreview/product/{id}.
  // Onbellek urun basinadir - ayni urunu ikinci kez acmak yeni istek atmaz.
  var yorumOnbellek = {};
  async function yorumlariCiz(id) {
    var p = (typeof window.byId === "function") ? window.byId(id) : null;
    if (!p) return;
    if (!yorumOnbellek[id]) {
      try { yorumOnbellek[id] = unwrap(await api.reviews.forProduct(id)) || []; }
      catch (e) { console.warn("Divisima: yorumlar alinamadi #" + id, e); yorumOnbellek[id] = []; }
    }
    if (!Array.isArray(yorumOnbellek[id])) yorumOnbellek[id] = [];
    p._rvList = yorumOnbellek[id];
    // Kullanici bu arada detayi kapatmis ya da baska urune gecmis olabilir - o zaman
    // baskasinin panelini EZMEYIZ.
    if (window.detailOpenId !== id) return;
    var el = document.getElementById("pdReviews");
    if (!el || typeof window.reviewsSection !== "function") return;
    var kutu = document.createElement("div");
    kutu.innerHTML = window.reviewsSection(p);
    if (kutu.firstChild) el.parentNode.replaceChild(kutu.firstChild, el);
  }


  // ── MFIX-2 / F-M9: GERCEK VERI CEKICILERI ──────────────────────────────────
  // Kural (0b): bir ikna satiri icin GERCEK alan VARSA ondan cizilir, YOKSA satir
  // TAMAMEN kaldirilir. Asagidaki ucu de GERCEK uclara baglanir; uc bos donerse
  // ilgili blok CIZILMEZ - uydurma yerine BOSLUK.

  // (a) URUN OZELLIKLERI: GET /api/product-attribute/product/{id} (ANONIM).
  //     attribute_key / attribute_value dondurur. Tablo bugun BOS; admin doldurunca
  //     blok kendiliginden dolar. Urun basina onbellekli - ikinci acilis istek atmaz.
  var attrCache = {};
  async function ozellikleriCiz(id) {
    var p = (typeof window.byId === "function") ? window.byId(id) : null;
    if (!p) return;
    if (attrCache[id] === undefined) {
      try {
        var res = await api._get("/api/product-attribute/product/" + id);
        var liste = unwrap(res) || [];
        attrCache[id] = liste.map(function (a) {
          return { k: a.attribute_key || a.key || "", v: a.attribute_value || a.value || "" };
        }).filter(function (a) { return a.k && a.v; });
      } catch (e) { attrCache[id] = []; }
    }
    p._attrs = attrCache[id];
    if (!p._attrs.length) return;                      // BOSSA blok DEGISMEZ (durust bos durum kalir)
    var govde = document.getElementById("piBody");
    if (!govde) return;
    govde.innerHTML = p._attrs.map(function (a) {
      return '<div class="pi-row"><span>' + esc(a.k) + "</span><b>" + esc(a.v) + "</b></div>";
    }).join("");
  }

  // (b) BEDEN TABLOSU: GET /api/size-guide/category/{categoryId} (ANONIM).
  //     index.html'in openSizeChart'i SENKRON oldugu icin onbellek onceden doldurulur;
  //     window.divisimaSizeGuide(catId) yalnizca onbellegi OKUR (uydurma uretmez).
  var sizeGuideCache = {};
  window.divisimaSizeGuide = function (catId) {
    var k = Number(catId) || 0;
    var v = sizeGuideCache[k];
    return (v && v.length) ? v : null;
  };
  async function bedenRehberiniCek(catId) {
    var k = Number(catId) || 0;
    if (!k || sizeGuideCache[k] !== undefined) return;
    try {
      var res = await api._get("/api/size-guide/category/" + k);
      sizeGuideCache[k] = unwrap(res) || [];
    } catch (e) { sizeGuideCache[k] = []; }
  }

  // MFIX-2 / F-M1-H3: siparis sonrasi vitrin tazeleme. detailCache ve urunlerin _ss
  // haritasi siparis ONCESI stoga ait; ikisi de bosaltilir ve katalog yeniden cekilir.
  function katalogTazele() {
    try {
      Object.keys(detailCache).forEach(function (k) { delete detailCache[k]; });
      if (window.PRODUCTS && window.PRODUCTS.length) {
        window.PRODUCTS.forEach(function (p) { if (p && p._ss) delete p._ss; });
      }
      loadCatalog();
    } catch (e) { console.warn("Divisima: katalog tazelenemedi", e); }
  }

  // ── MFIX-3 / F-M4: SEPETTEKI URUNLERI KATALOGA TAMAMLA ────────────────────
  // index.html'in geri yuklemesi artik kalem DUSURMUYOR ve renderCart SILMIYOR; ama
  // urunu bulunamayan kalem CIZILEMEZ. Ilk sayfa yalniz 24 urun getirdigi icin sepette
  // baska sayfadan bir urun olabilir. Eksik olanlar TEK TEK cekilip PRODUCTS'a eklenir.
  // SEPET BOSSA YA DA EKSIK YOKSA HIC ISTEK ATILMAZ (ilk yukleme maliyeti degismez).
  //
  // DURUST SINIR: detay ucu `image_url` DONDURMUYOR (canli olculdu: alan listesinde YOK),
  // bu yuzden boyle tamamlanan bir urun gorselsiz gelir ve frontend kendi yer tutucusunu
  // cizer - bugun katalogdaki TUM urunler zaten oyle (D1 temizliginden sonra
  // product_images BOS). Ikinci bir gorsel istegi ATILMADI: kazanci bugun SIFIR.
  function detaydanUrun(d) {
    var p = mapProduct(d);
    // MFIX-B / K1 (denetimde bulundu): bu toplama MFIX-2/F-M1-H3'un enrichProduct icin
    // KALDIRDIGI kalibin IKINCI kopyasiydi ve FIZIKSEL topluyordu (or. urun 937: 12+10+11=33,
    // dogrusu 11+4+11=26). Detay ucu artik SATILABILIR dondugu icin bu ikinci site de
    // KENDILIGINDEN dogrulandi - ayri bir duzeltme GEREKMEDI. Kayit: "ayni kuralin ikinci
    // kopyasi" sinifinin bu depodaki bir ornegi daha.
    if (d.stocks && d.stocks.length) {
      var toplam = 0, bedenler = [];
      d.stocks.forEach(function (s) {
        var q = Number(s.stock_quantity) || 0;
        toplam += q;
        if (q > 0 && bedenler.indexOf(String(s.size)) < 0) bedenler.push(String(s.size));
      });
      p.stock = toplam;
      p.sizes = bedenler.map(function (s) { return isNaN(+s) ? s : +s; });
    }
    if (d.description) p.desc = d.description;
    return p;
  }

  async function sepetUrunleriniTamamla() {
    try {
      if (typeof window.cart === "undefined" || !window.cart || !window.cart.size) return;
      var varOlan = {};
      (window.PRODUCTS || []).forEach(function (p) { varOlan[p.id] = true; });
      var eksik = [];
      window.cart.forEach(function (it) {
        if (it && it.id && !varOlan[it.id] && eksik.indexOf(it.id) < 0) eksik.push(it.id);
      });
      if (!eksik.length) return;
      var sonuc = await Promise.all(eksik.map(function (id) {
        return api.products.get(id).then(function (r) { return unwrap(r); }).catch(function () { return null; });
      }));
      var mapped = sonuc.filter(Boolean).map(detaydanUrun);
      if (mapped.length) appendProducts(mapped);
    } catch (e) {
      console.warn("Divisima: sepet ürünleri tamamlanamadı", e && e.message);
    }
    try { if (typeof window.renderCart === "function") window.renderCart(); } catch (e) {}
    try { if (typeof window.cartBump === "function") window.cartBump(); } catch (e) {}
  }

  // (c) TESLIMAT SEHRI: GET /api/address (Customer). YALNIZ girisli kullanicida ve
  //     YALNIZ VARSAYILAN adresten. Cikista TEMIZLENIR - eski oturumun sehri sizmasin.
  window.divisimaDelivCity = null;
  async function teslimatSehriniTazele() {
    if (!api.isLoggedIn()) { window.divisimaDelivCity = null; return; }
    try {
      var liste = unwrap(await api.address.list()) || [];
      var v = liste.filter(function (a) { return a.is_default; })[0] || liste[0] || null;
      window.divisimaDelivCity = (v && v.city) ? String(v.city) : null;
    } catch (e) { window.divisimaDelivCity = null; }
  }
  // ── MFIX-3 / F-M5: FAVORILER HESABA OZGU ──────────────────────────────────
  // OLCULEN ONCE-DURUM: kalp MISAFIRKEN de calisiyor ve favoriyi CIHAZ-GENELI
  // localStorage anahtarina yaziyordu (canli: misafir kalbi -> yerel anahtar dolu,
  // wishlist_items TOPLAM=0); ayni cihazda giris yapan BASKA kullanici o favorileri
  // DEVRALIYORDU ve cikista temizlenmiyordu. Sunucu tarafi (WishlistController) TAM ve
  // CALISIYOR ama vitrin HIC cagirmiyordu (api-bridge'de "wishlist" gecisi 0).
  //
  // SOZLESME KAYNAKTAN OKUNDU (WishlistController.cs):
  //   POST /api/wishlist/toggle?productId=N   -> Toggle(int productId), [FromBody] YOK
  //   GET  /api/wishlist                      -> List<ProductListResponseDto>
  // api-client.wishlist.toggle GOVDE gonderiyor ve CANLI OLCULDU: HTTP 500 (productId 0'a
  // baglaniyor). Bu dalgada api-client'a YALNIZ resendVerification icin dokunma izni var;
  // dogru sozlesme BURADAN api._post + api._qs ile cagriliyor, api-client kusuru
  // KAPSAM DISI BULGU olarak raporlandi.
  function favSeti() { return (typeof window.favs !== "undefined" && window.favs) ? window.favs : null; }

  // Kalp ikonlari + rozet + cekmece. index.html'in KENDI cizicileri kullanilir; burada
  // yeni bir favori arayuzu ICAT EDILMEZ.
  function favEkranlariniTazele() {
    var s = favSeti();
    var DOLU = String.fromCharCode(0x2665), BOS = String.fromCharCode(0x2661);
    try {
      document.querySelectorAll(".card-fav[data-fav]").forEach(function (b) {
        var acik = !!(s && s.has(+b.getAttribute("data-fav")));
        b.classList.toggle("on", acik);
        b.textContent = acik ? DOLU : BOS;
      });
    } catch (e) {}
    // Urun detayindaki kalp: index.html'in kendi onclick'i toggleFav'dan HEMEN SONRA
    // `favs.has(id)` okur. Sunucu cagrisi ASENKRON oldugu icin o okuma BAYAT kalir
    // (olculdu: kart ve rozet guncellendi, #pdLike '@' isaretinde kaldi). Bu yuzden
    // detay kalbini de BURADAN, sunucu yaniti geldikten sonra tazeliyoruz.
    try {
      var lk = document.getElementById("pdLike");
      var aid = window.detailOpenId;
      if (lk && aid) {
        var acikD = !!(s && s.has(+aid));
        lk.classList.toggle("on", acikD);
        lk.textContent = acikD ? DOLU : BOS;
      }
    } catch (e) {}
    try { if (typeof window.favBump === "function") window.favBump(); } catch (e) {}
    try { if (typeof window.renderFavs === "function") window.renderFavs(); } catch (e) {}
    // Hesabim > Favorilerim sekmesi acikken listeyi de tazele.
    try {
      if (location.hash.indexOf("#/hesabim/favorilerim") === 0 && typeof window.renderAccount === "function") {
        window.renderAccount("favorilerim");
      }
    } catch (e) {}
  }

  // Sunucu favorileri -> yerel `favs` seti. Donen kayitlar KATALOGLA AYNI sekilde
  // oldugu icin dogrudan mapProduct'tan gecirilip PRODUCTS'a EKLENIR; boylece
  // index.html'in byId'ye dayanan cizicileri (kart, cekmece, Favorilerim) calisir.
  async function favorileriSunucudanCek() {
    var s = favSeti();
    if (!s) return;
    if (!api.isLoggedIn()) { s.clear(); favEkranlariniTazele(); return; }
    try {
      var liste = unwrap(await api.wishlist.get()) || [];
      var mapped = liste.map(mapProduct);
      appendProducts(mapped);
      s.clear();
      mapped.forEach(function (p) { s.add(p.id); });
    } catch (e) {
      console.warn("Divisima: favoriler alınamadı", e && e.message);
    }
    favEkranlariniTazele();
  }

  // Cikista YEREL GORUNUM temizlenir (sunucudaki kayit DURUR - o hesabin verisidir).
  function favorileriTemizle() {
    var s = favSeti();
    if (s) s.clear();
    favEkranlariniTazele();
  }

  function wireFavoriler() {
    if (typeof window.toggleFav !== "function" || window.toggleFav.__divisimaWrapped) return;
    var orig = window.toggleFav;
    window.toggleFav = function (id) {
      if (!api.isLoggedIn()) {
        // MISAFIRDE YEREL YAZMA YOK: gorunur Turkce yonlendirme + MEVCUT giris akisi.
        notify(ceviri("fav_login"),'info');
        location.hash = "#/giris";
        return;
      }
      // Once SUNUCU, sonra yerel: boylece ekrandaki durum sunucudan AYRISAMAZ.
      // MFIX-3b/(4): TEK SOZLESME. Onceki dalgada api-client'a dokunma izni YOKTU ve
      // dogru sozlesme burada ELLE kuruluyordu (_post + _qs). api-client duzeltildigi
      // icin o gecici kopya KALDIRILDI - artik TEK yazici api-client'in kendi uyesi.
      // En dar dokunus bu yon: iki yerde iki ayri el yazmasi birakmak, sozlesmenin
      // yarin yine ayrismasi demekti (bu depoda "ayni kuralin ikinci kopyasi" sinifinin
      // bedeli alti kez odendi).
      api.wishlist.toggle(id)
        .then(function () { orig.call(window, id); favEkranlariniTazele(); })
        .catch(function (e) {
          notify(ceviri("fav_err"),'err');
          console.warn("Divisima: favori güncellenemedi", e && e.message);
        });
    };
    window.toggleFav.__divisimaWrapped = true;
  }

  function wireProductDetail() {
    if (typeof window.openDetail !== "function") return;
    var orig = window.openDetail;
    window.openDetail = function (id) {
      // MFIX-2 / F-M9: beden rehberi index.html'de SENKRON okundugu icin onbellek
      // detay ACILIRKEN doldurulur; gelmezse tablo GERCEK BEDENLERE duser (uydurma YOK).
      var _p0 = (typeof window.byId === "function") ? window.byId(id) : null;
      if (_p0 && _p0.catId) bedenRehberiniCek(_p0.catId);
      if (detailCache[id]) { orig.call(window, id); yorumlariCiz(id); ozellikleriCiz(id); return; }
      // Önce mevcut (liste) veriyle aç - kullanıcı beklemesin; detay gelince yeniden aç.
      orig.call(window, id);
      yorumlariCiz(id);
      ozellikleriCiz(id);
      enrichProduct(id).then(function (d) {
        if (d) {
          var _p = (typeof window.byId === "function") ? window.byId(id) : null;
          if (_p && _p.catId) bedenRehberiniCek(_p.catId);
          orig.call(window, id); yorumlariCiz(id); ozellikleriCiz(id);
        }
      });
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
        sepetBirlestirmesiniSilahlandir();   // F-A1: bu oturumun ilk senkronu BIRLESTIRME olsun
        teslimatSehriniTazele();             // MFIX-2/F-M9: teslimat sehri GERCEK varsayilan adresten
        favorileriSunucudanCek();            // MFIX-3/F-M5: favoriler HESABA OZGU - sunucudan gelir
        if (typeof window.login === "function") window.login(d.name || String(email).split("@")[0]);
        return r;
      },
      async register(payload) { return api.auth.register(payload); },
      async verifyEmail(token) { return api.auth.verifyEmail(token); },
      async resend(email) { return api.auth.resendVerification(email); },
      async logout() {
        try { await api.auth.logout(); } finally {
          window.loggedIn = false;
          sepetBirlestirmesiniSilahlandir();   // F-A1: sonraki giris de birlestirme ile baslar
          window.divisimaDelivCity = null;     // MFIX-2/F-M9: eski oturumun sehri SIZMAZ
          favorileriTemizle();                 // MFIX-3/F-M5: yerel favori GORUNUMU temizlenir (sunucudaki kayit DURUR)
          if (typeof window.logout === "function") window.logout();
        }
      },
      isLoggedIn: function () { return api.isLoggedIn(); },
      // Otomatik yenilemeyi elle sürmek için (doğrulama/teşhis): access token'ı bilerek
      // bozup bir çağrı yapmak yerine doğrudan yenileme yolunu çalıştırır.
      forceRefresh: function () { return api.auth.refresh(); }
    };

    if (api.isLoggedIn()) window.loggedIn = true;
    teslimatSehriniTazele();   // MFIX-2/F-M9: sayfa gecerli jetonla acildiginda da sehir gerekir

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
          if (er) er.textContent = e.message || ceviri("b_giris_basarisiz");
          else notify(e.message || ceviri("b_giris_basarisiz"),'err');
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
          if (er) er.textContent = e.message || ceviri("b_kayit_basarisiz");
          else notify(e.message || ceviri("b_kayit_basarisiz"),'err');
        }
      };
    }

    // Çıkış: index.html'in logout()'u yalnız yerel durumu temizliyordu; sunucudaki
    // oturum açık kalıyordu. Gerçek uca bağlanır.
    if (typeof window.logout === "function" && !window.logout.__divisimaWrapped) {
      var origLogout = window.logout;
      window.logout = function () {
        api.auth.logout().catch(function () {}).then(function () {
          origLogout.call(window);
          // MFIX-3/F-M5: hesap menusundeki "Cikis Yap" bu yoldan gecer - yerel favori
          // GORUNUMU burada da temizlenmeli (sunucudaki kayit DURUR, o hesabin verisidir).
          // MFIX-3/F-M4: SEPETE DOKUNULMAZ - cikista sepet KORUNUR (kapsam karari).
          favorileriTemizle();
          window.divisimaDelivCity = null;
        });
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
      '<div style="font-weight:600;margin-bottom:6px">' + ceviri("b_epostani_dogrula") + '</div>' +
      '<div id="dvsVerifyMsg" style="font-size:13px;color:#6b6b6b;margin-bottom:10px"></div>' +
      '<input id="dvsVerifyToken" placeholder=ceviri("b_dogrulama_kodu") style="width:100%;padding:9px 11px;border:1px solid #e8e4de;border-radius:8px">' +
      '<div style="display:flex;gap:8px;margin-top:10px">' +
      '<button id="dvsVerifyGo" style="padding:9px 16px;border:none;border-radius:8px;background:#111;color:#fff;cursor:pointer">' + ceviri("b_dogrula_btn") + '</button>' +
      '<button id="dvsVerifyResend" style="padding:9px 16px;border:1px solid #e8e4de;border-radius:8px;background:#fff;cursor:pointer">' + ceviri("b_tekrar_gonder_btn") + '</button>' +
      "</div>" +
      '<div id="dvsVerifyErr" style="color:#a32d2d;font-size:12px;margin-top:8px"></div>';
    // GÜVENLİK-FIX (G2): kayıt ucu artık "bu adres kayıtlı mı" sorusunu YANITLAMIYOR - var olan
    // adres de yeni adres de AYNI 201'i alıyor. Bu yüzden buradaki metin de bir şey VARSAYAMAZ:
    // eskiden "doğrulama kodu gönderildi" diyordu ve zaten hesabı olan kullanıcıya YALAN olurdu.
    // Ne olduğunu kullanıcı e-postadan öğrenir (yeni hesap -> kod; var olan hesap -> "giriş yap").
    document.getElementById("dvsVerifyMsg").textContent =
      email + ceviri("b_verify_sent_1") +
      ceviri("b_verify_sent_2");

    document.getElementById("dvsVerifyGo").onclick = async function () {
      var errEl = document.getElementById("dvsVerifyErr");
      errEl.textContent = "";
      var tok = (document.getElementById("dvsVerifyToken").value || "").trim();
      if (!tok) { errEl.textContent = ceviri("b_kodu_gir"); return; }
      try {
        await api.auth.verifyEmail(tok);
        box.remove();
        notify(ceviri("b_eposta_dogrulandi_giris"),'ok');
      } catch (e) { errEl.textContent = e.message || ceviri("b_dogrulama_basarisiz"); }
    };
    document.getElementById("dvsVerifyResend").onclick = async function () {
      var errEl = document.getElementById("dvsVerifyErr");
      errEl.textContent = "";
      // GÜVENLİK-FIX (G2b): uç artık üç ayrı yanıt değil TEK yanıt dönüyor (varlık sızdırmıyor),
      // bu yüzden istemci de "gönderildi" diye kesin konuşamaz - adres kayıtlı olmayabilir.
      try { await api.auth.resendVerification(email); notify(ceviri("b_kod_tekrar_gonderildi"),'info'); }
      catch (e) { errEl.textContent = e.message || ceviri("b_gonderilemedi"); }
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

  // LAUNCH-FIX A4: fiyat bicimi TEK KAYNAKTAN. index.html'in tl() fonksiyonuna delege edilir;
  // o yoksa (bu dosya tek basina yuklendiyse) eski TRY bicimi yedek kalir. Onceden bu dosya
  // tl()'i HIC kullanmiyordu (olculdu: 0 cagri) ve vitrin ile odeme paneli ayrisabiliyordu.
  function money(n) {
    if (typeof window.tl === "function") { try { return window.tl(Number(n || 0)); } catch (_) { } }
    try { return Number(n || 0).toLocaleString((typeof window.dvsLocale === "function") ? window.dvsLocale() : "tr-TR", { minimumFractionDigits: 2, maximumFractionDigits: 2 }) + " TL"; }
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
  // GOZ-FIX / F-Ö1: artik BASARIYI DONDURUR (true/false). Cagiran, sunucuya yazilamamis bir
  // kalemi yerelde "eklendi" gibi BIRAKMAMAK icin bu degeri kullanir.
  function mirror(promise, adim) {
    return promise.then(function () { return true; }).catch(function (e) {
      console.warn("Divisima: sepet aynalama basarisiz (" + adim + ")", e && e.message);
      var now = Date.now();
      if (now - lastMirrorWarn > 4000) {   // ust uste toast yagmuru olmasin
        lastMirrorWarn = now;
        // 401 = oturum gercekten bitti (api-client BIR KEZ refresh denedi, o da dustu).
        // Eski metin "Sepet sunucuya yazılamadı: İstek başarısız (401)" idi ve toast onu
        // BASINA "✓" koyarak gosteriyordu - basarisizlik BASARI gibi gorunuyordu (olculdu).
        // GOZ-FIX / F-Ö1: iyimser "✓ ... sepete eklendi" mesaji toast SIRASINDA (_toastQ)
        // bekliyor olabilir; duzeltmeyi sona eklemek kullaniciya ONCE "eklendi" dedirtir
        // (olculdu: 4 saniye boyunca ekranda yalniz basari mesaji vardi). Bekleyen iyimser
        // mesajlar DUSURULUR ve duzeltme mesaji hemen sonraki adimda gosterilir.
        try { if (Array.isArray(window._toastQ)) window._toastQ.length = 0; } catch (_t) {}
        // MFIX-3b / T1 (2): HATA METNI ARTIK GERCEK SEBEBI SOYLER.
        // OLCULDU (kabul turu + kurgu tekrari): sunucu 400 ile "Yetersiz stok. İstenen
        // adet mevcut değil." derken kullaniciya "Internet baglantini kontrol et"
        // gosteriliyordu - YANLIS TESHIS, yani bir DURUSTLUK kusuru. Gercek sebep zaten
        // elimizde: api-client _parse, sunucunun {message} alanini tasiyan bir Error
        // firlatiyor (err.message) ve HTTP durumunu ekliyor (err.status).
        // AYRIM: e.status VARSA sunucu yanit VERMISTIR -> onun sebebi yazilir.
        // e.status YOKSA istek aga hic cikamamistir -> ancak O ZAMAN baglanti metni.
        if (e && e.status === 401) notify(ceviri("err_session"), 'err');
        else if (e && e.status) notify(ceviri("err_cart_sync_reason").replace("{sebep}", String(e.message || e.status)), 'err');
        else notify(ceviri("err_cart_offline"), 'err');
      }
      return false;
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
        // GOZ-FIX / F-Ö1: yazma BASARISIZSA yerel ekleme GERI ALINIR.
        // OLCULDU (oturum olu): cart/add 401 -> auth/refresh 401 iken rozet 2 -> 3 oluyor,
        // toast "✓ ... yazılamadı" diyordu; kullanici urunun sepette oldugunu saniyordu ama
        // sunucuda YOKTU. Onceki adet saklanir; yazma dusserse o hale donulur.
        var oncekiAdet = entry ? entry.qty - (qty || 1) : 0;
        mirror(api.cart.setQuantity(id, size || "", q), "ekle").then(function (ok) {
          if (ok) return;
          try {
            if (oncekiAdet > 0) { var it = window.cart.get(key); if (it) it.qty = oncekiAdet; }
            else if (key) window.cart.delete(key);
            if (typeof window.cartBump === "function") window.cartBump();
            if (typeof window.renderCart === "function") window.renderCart();
          } catch (e) { console.warn("Divisima: yerel sepet geri alinamadi", e); }
        });
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
        // GOZ-FIX / F-Ö4: ODEME OZETI BAYAT KALMAZ. Rozet, cekmece ve ozet AYNI kaynaktan
        // (yerel `cart` Map) besleniyor; ama ozet yalnizca rota girisinde ciziliyordu.
        // Kullanici #/odeme'deyken sepeti degistirirse (or. cekmeceden kalem silerse)
        // ozet eski tutari gostermeye devam ediyordu. Sepet her degistiginde ve odeme
        // sayfasi ACIKKEN ozet yeniden hesaplanir.
        odemeOzetiniTazele();
        if (!api.isLoggedIn()) return;
        // MFIX-3b / T1 (1): SALT YENIDEN CIZIM SUNUCUYA YAZMAZ.
        // OLCULDU: setLang -> renderCart() cagiriyor; bu sarmalayici sepet HIC DEGISMEDIGI
        // halde her seferinde GET /api/cart + her kalem icin POST /api/cart/add gonderiyordu
        // (dil degisimi basina 5 istek). Stok o arada dustuyse ayni yazma 400 aliyor ve
        // kullanici sebepsiz bir hata mesaji goruyor - kabul turunda birebir bu yasandi.
        // Cozum SINIF DUZEYINDE: yalnizca SETLANG degil, HER salt-cizim yolu (para birimi,
        // sekme gorunurlugu, tema) artik yazma tetiklemez. Olcut sepetin IMZASI:
        // imza degismediyse sunucuda degisecek bir sey de YOKTUR.
        // ILK SENKRON (birlestirme) BU KAPIDAN MUAFTIR - o, sunucudan OKUMAK icin gerekli.
        if (ilkSenkronYapildi && sepetImzasi() === sonSenkronImzasi) return;
        clearTimeout(syncTimer);
        syncTimer = setTimeout(syncCartToServer, 250);   // hizli tiklamalarda tek istek
      };
      window.renderCart.__divisimaWrapped = true;
    }

    // GOZ-FIX / F-Ö4: sekme yeniden gorunur oldugunda da ozet tazelenir - kullanici baska
    // sekmede sepetini degistirmis olabilir ya da sayfa arka planda bayatlamis olabilir.
    if (!window.__divisimaOzetGorunurlukBagli) {
      window.__divisimaOzetGorunurlukBagli = true;
      document.addEventListener("visibilitychange", function () {
        if (document.visibilityState !== "visible") return;
        odemeOzetiniTazele();
      });
    }
  }

  // VITRIN-FIX-2 / F-A1: GIRISTEN SONRAKI ILK SENKRON ASLA SILMEZ - BIRLESTIRIR.
  //
  // OLCULEN ZARAR: eski akista her senkron "yereldeki her kalem SET, sunucuda olup yerelde
  // OLMAYAN kalem SIL" idi. Kullanici bos bir tarayicida giris yapip sepet cekmecesini
  // actiginda (renderCart -> senkron) SET dongusu hic donmuyor, SIL dongusu ise sunucudaki
  // KALICI sepeti tumuyle temizliyordu. Yani baska cihazda/oturumda birakilan sepet, yalnizca
  // giris yapmakla YOK oluyordu.
  //
  // BUGUN: sayfa oturumunun ILK senkronu bir BIRLESTIRMEDIR -
  //   * once sunucu sepeti okunur (TEK istek),
  //   * sunucuda olup yerelde olmayan kalem YERELE INER (yerel bossa sunucudan tohumlanir),
  //   * ayni urun+beden iki tarafta da varsa YEREL ADET KAZANIR (kullanicinin son eylemi),
  //   * hicbir sey SILINMEZ,
  //   * rozet/cekmece/ozet guncellenir.
  // Ayna duzeni (SET + yerelde olmayani sil) ancak BIRLESTIRMEDEN SONRAKI senkronlarda baslar.
  //
  // Bayrak neden "giris olayi" degil "ilk senkron"? Cunku ikinci bir giris yolu daha var:
  // sayfa ACILIRKEN gecerli jetonla gelen kullanici (wireAuth icindeki
  // `if (api.isLoggedIn()) window.loggedIn = true;`). O yolda hicbir login olayi ATESLENMEZ
  // ama ilk renderCart yine senkronu tetikler - eski kodda kalici sepeti silen yol da buydu.
  //
  // KORUNANLAR: sunucudaki bir kalemin urunu o an KATALOGDA yoksa (PRODUCTS sayfali gelir,
  // 24'luk sayfalar halinde) yerele indiremeyiz - index.html'deki renderCart, byId(it.id)
  // bulamadigi kalemi SESSIZCE siler (olculdu: `var p=byId(it.id);if(!p){cart.delete(k);return;}`).
  // Boyle bir kalemi ayna dongusunun silmesi de veri kaybi olurdu; bu yuzden anahtari
  // KORUNANLAR kumesine yazilir ve SIL dongusu onu ATLAR.
  var syncing = false;
  var ilkSenkronYapildi = false;
  var korunanSunucuAnahtarlari = {};
  // MFIX-3b / T1 (1): son BASARIYLA senkronlanan sepetin imzasi. Yalniz sepet ICERIGINDEN
  // turer (urun + beden + adet), siralamadan bagimsizdir. Yazma BASARISIZ olduysa imza
  // GUNCELLENMEZ - boylece bir sonraki gercek degisiklikte yeniden denenir.
  var sonSenkronImzasi = null;
  function sepetImzasi() {
    if (!window.cart || typeof window.cart.forEach !== "function") return "";
    var p = [];
    window.cart.forEach(function (it) { p.push(it.id + "|" + (it.size || "") + "|" + Math.floor(it.qty)); });
    return p.sort().join(";");
  }

  function sunucuSepetiniOku(yanit) {
    var d = unwrap(yanit);
    if (d && Array.isArray(d.items)) return d.items;
    if (Array.isArray(d)) return d;
    return [];
  }

  // Sunucudan gelen kalemleri yerel sepete BIRLESTIR. Donen deger: yerel sepet degisti mi.
  function sunucuKalemleriniBirlestir(server) {
    var degisti = false;
    if (!window.cart || typeof window.cart.set !== "function") return false;
    var ck = (typeof window.ckey === "function") ? window.ckey : function (id, size) { return id + "|" + (size || "") + "|"; };
    var yerel = {};
    window.cart.forEach(function (it) { yerel[it.id + "|" + (it.size || "")] = true; });
    for (var i = 0; i < server.length; i++) {
      var s = server[i];
      var k = s.product_id + "|" + (s.size || "");
      if (yerel[k]) continue;                       // CAKISMA: yerel adet kazanir, dokunma
      var p = (typeof window.byId === "function") ? window.byId(s.product_id) : null;
      if (!p) { korunanSunucuAnahtarlari[k] = true; continue; }   // katalogda yok -> SILINMESIN
      var q = Math.floor(Number(s.quantity) || 0);
      if (!isFinite(q) || q < 1) q = 1;
      if (q > 99) q = 99;
      window.cart.set(ck(s.product_id, s.size || "", ""), { id: s.product_id, size: s.size || "", color: null, qty: q });
      degisti = true;
    }
    return degisti;
  }

  async function syncCartToServer() {
    if (syncing || !api.isLoggedIn()) return;
    syncing = true;
    try {
      // TEK istek. (Eski kod `.items` bos dustugunde ayni ucu IKINCI KEZ cagiriyordu.)
      var server = [];
      try { server = sunucuSepetiniOku(await api.cart.get()); } catch (e) { server = []; }

      if (!ilkSenkronYapildi) {
        ilkSenkronYapildi = true;                   // once isaretle: yeniden giris tek birlestirme
        var degisti = sunucuKalemleriniBirlestir(server);
        // Yerel kalemleri sunucuya yaz (SET - silme YOK). Cakismada yerel adet kazanir.
        var yerelKalemler = cartItemsPayload();
        for (var a = 0; a < yerelKalemler.length; a++) {
          await mirror(api.cart.setQuantity(yerelKalemler[a].product_id, yerelKalemler[a].size, yerelKalemler[a].quantity), "esitle");
        }
        if (degisti) {
          // Rozet + kalici depolama + cekmece + odeme ozeti. renderCart sarmalanmis oldugu
          // icin bir sonraki (ayna) senkronu da kendisi zamanlar - istenen davranis budur.
          if (typeof window.cartBump === "function") window.cartBump();
          if (typeof window.renderCart === "function") window.renderCart();
        }
        return;                                     // BU GECISTE HICBIR SEY SILINMEZ
      }

      var local = cartItemsPayload();
      var localKey = {};
      local.forEach(function (it) { localKey[it.product_id + "|" + it.size] = it; });

      // MFIX-3b / T1 (1): SEPET DURUMU BASINA TEK DENEME.
      // Ilk yazimda imzayi YALNIZ tum yazmalar basariliyken kaydediyordum; R-T1 OLCUMU
      // BUNU CURUTTU: sepette KALICI olarak reddedilen bir kalem varsa (adet > satilabilir)
      // imza HIC kaydedilmiyor ve her salt-cizim yeniden deniyordu - yani dil degisimi
      // gene 5 istek atip AYNI hatayi tekrar tekrar gosteriyordu (olculdu: 3 dil degisimi
      // = 15 istek, 3 kez ayni toast). Ayni istek ayni sonucu verecegi icin bu YENIDEN
      // DENEME DEGIL, GURULTU. Imza artik DENEME sonrasi HER DURUMDA kaydedilir:
      //   * sepet GERCEKTEN degisirse imza degisir ve yeni durum icin yeniden denenir,
      //   * salt-cizim (dil/para/sekme) hicbir sey tetiklemez,
      //   * kalici hatanin gercek telafisi checkout'tur - orada TAM liste sunucuya
      //     yeniden gonderilir (bu dosyanin kendi notu, mirror blogunun basinda).
      var turImzasi = sepetImzasi();

      for (var i = 0; i < local.length; i++) {
        await mirror(api.cart.setQuantity(local[i].product_id, local[i].size, local[i].quantity), "esitle");
      }
      for (var j = 0; j < server.length; j++) {
        var s = server[j];
        var k = s.product_id + "|" + (s.size || "");
        if (localKey[k] || korunanSunucuAnahtarlari[k]) continue;
        await mirror(api.cart.remove(s.product_id, s.size || ""), "sil");
      }
      sonSenkronImzasi = turImzasi;
    } finally { syncing = false; }
  }
  // Yeni bir oturum (giris ya da cikis) sonrasi birlestirme YENIDEN silahlanir: baska bir
  // kullanici girerse onun kalici sepeti de silinmek yerine birlestirilmelidir.
  function sepetBirlestirmesiniSilahlandir() { ilkSenkronYapildi = false; korunanSunucuAnahtarlari = {}; }
  window.divisimaSyncCart = syncCartToServer;

  // GOZ-FIX / F-Ö4: odeme ozetini YERINDE tazele. `window.divisimaCheckout` bir SAP degil,
  // E2'den kalan reddeden bir stub'dir (bilerek) - onu cagirmak yakalanmamis bir promise
  // reddi uretirdi. Dogru yuzey: uye yolunda drawCheckout(), misafir yolunda
  // misafirCheckoutCiz(). Ikisi de YEREL sepetten hesaplar, EK ISTEK ATMAZ.
  function odemeOzetiniTazele() {
    try {
      var h = String(location.hash || "");
      if (h.indexOf("#/odeme") !== 0 || h.indexOf("sonuc") >= 0) return;
      var view = document.getElementById("checkoutView");
      if (!view || view.offsetParent === null) return;
      if (api.isLoggedIn()) {
        if (document.getElementById("coSubmit")) drawCheckout();
      } else if (document.getElementById("mgGonder")) {
        misafirDegerleriOku();     // kullanicinin yazdiklari KAYBOLMASIN
        misafirCheckoutCiz(view);
      }
    } catch (e) { console.warn("Divisima: ödeme özeti tazelenemedi", e); }
  }

  // GOZ-FIX / F-Ö5: "Sepeti Bosalt" - sunucu tarafi. YENI BACKEND UCU ACILMADI; mevcut
  // DELETE /api/cart/clear kullaniliyor. Anonim kullanicida sunucu sepeti zaten yoktur,
  // yerel temizlik index.html tarafinda yapilmis olur.
  window.divisimaClearServerCart = async function () {
    if (!api.isLoggedIn()) return true;
    try { await api.cart.clear(); return true; }
    catch (e) {
      // Uc dusserse SESSIZ KALINMAZ: yerel bosaldi ama sunucu bosalmadiysa kullanici bilmeli.
      if (e && e.status === 401) notify(ceviri("err_session"),'err');
      else notify(ceviri("err_cart_clear"),'err');
      return false;
    }
  };

  // ── Checkout paneli (MOCK ekranin yerine) ──────────────────────────────────
  // ══ A3 HIBRIT - MISAFIR CHECKOUT (YALNIZ KAPIDA ODEME) ══════════════════════════════════
  //
  // OLCULEN ONCE-DURUM (kapsama denetimi):
  //   - POST /api/guest-checkout/place VARDI ama storefront'ta cagrisi SIFIRDI
  //     (index.html 0, api-bridge 0, api-client 0).
  //   - index.html'in ".co-guest" blogu DOM'DA YOKTU: E2'nin gercek odeme paneli, o blogu
  //     cizen mock checkout'un (coStep1) USTUNE yaziyor - yani UI vaadi ZATEN OLUYDU.
  //   - YASAYAN TEK VAAT SSS'deydi ve YANLISTI.
  //   - Ayrica misafir siparisi ASLA ODENEMIYORDU: DTO'da payment_method yoktu ->
  //     payment_type=0 (online) -> /api/payment/initialize [RequireUserType(Customer)] ve
  //     misafirin token'i YOK. Siparis sonsuza kadar Pending kaliyordu.
  //
  // KULLANICI KARARI (secenek iii): misafire YALNIZ KAPIDA ODEME. Misafire OTURUM VERILMEZ,
  // yetki modeline DOKUNULMAZ. Kart secenegi misafire KAPALI ve NEDENI GORUNUR.
  //
  // OLU i18n ANAHTARLARI YENIDEN KULLANILIYOR: co_guest_t / co_guest_s / co_guest_login
  // index.html'de tanimli ve UC DILDE cevirisi var (tr/en/ar) ama cizildikleri blok oluydu.
  // Silmek yerine YENI FORMA baglandilar - ceviriler kazanildi, olu anahtar kalmadi.
  var misafirState = { ad: "", eposta: "", telefon: "", il: "", ilce: "", adres: "", posta: "" };

  function bosSepetEkrani() {
    return '<div class="wrap" style="padding:40px 0"><h2>' + ceviri("b_h_odeme") + '</h2>' +
      '<p class="muted" style="margin:10px 0 16px">' + ceviri("b_sepetin_bos") + '</p>' +
      '<a class="btn" href="#/kategori/tumu">' + esc(ceviri("shop_start")) + "</a></div>";
  }

  function misafirAlan(id, etiket, deger, tip) {
    return '<label class="f" style="display:block;margin-top:10px">' +
      '<span style="display:block;font-size:12.5px;color:var(--muted);margin-bottom:4px">' +
      esc(etiket) + "</span>" +
      '<input id="' + id + '" type="' + (tip || "text") + '" value="' + esc(deger || "") +
      '" style="width:100%;padding:9px 11px;border:1px solid #e8e4de;border-radius:8px"></label>';
  }

  function misafirCheckoutCiz(view) {
    // OLCULDU (tarayici): sepet kalemi {id,size,qty,color} tutuyor - FIYAT TASIMIYOR. Ilk
    // yazimda `it.price` okunmustu ve ozet "Ara toplam 0 TL" gosterdi. Fiyat katalogdan
    // cozulur; bu is icin bu dosyada ZATEN cartSubtotal() var ve uye yolu da onu kullaniyor.
    // Kendi hesabimi yazmak, ayni sayinin iki yerde ayrisması demekti.
    var toplam = cartSubtotal();
    var kargo = toplam >= 2000 ? 0 : 49.9;

    view.innerHTML =
      '<div class="wrap" style="padding:28px 0"><h2>' + ceviri("b_h_odeme") + '</h2>' +
      // co_guest_t / co_guest_s: index.html'de ZATEN CEVIRILI, blogu olu kalmisti.
      '<div class="co-guest" style="margin:12px 0 18px"><div><b>' +
        esc(typeof t === "function" ? t("co_guest_t") : ceviri("b_misafir_devam")) + "</b><span>" +
        esc(typeof t === "function" ? t("co_guest_s") : "Sipariş bilgilerin e-postana gönderilecek.") +
        '</span></div><a href="#/giris" class="co-guest-link">' +
        esc(typeof t === "function" ? t("co_guest_login") : "Üye girişi yap") + "</a></div>" +

      '<div class="co-block"><h3>' + ceviri("b_h_iletisim_teslimat") + '</h3>' +
      misafirAlan("mgAd", ceviri("b_ad_soyad"), misafirState.ad) +
      misafirAlan("mgMail", "E-posta", misafirState.eposta, "email") +
      misafirAlan("mgTel", ceviri("b_telefon"), misafirState.telefon, "tel") +
      misafirAlan("mgIl", ceviri("b_il"), misafirState.il) +
      misafirAlan("mgIlce", ceviri("b_ilce"), misafirState.ilce) +
      misafirAlan("mgAdres", ceviri("b_acik_adres"), misafirState.adres) +
      misafirAlan("mgPosta", ceviri("b_posta_kodu"), misafirState.posta) +
      "</div>" +

      // GOZ-FIX / F-G4: ONCEDEN IKI SECENEK DE `disabled` IDI - "Kapıda ödeme" bile SOLUK
      // gorunuyordu ve ekran "hiçbir şey seçemiyorum" hissi veriyordu (olculdu: iki radyo da
      // disabled=true, yalnizca gonder dugmesi etkindi). Simdi: kapida odeme ETKIN + SECILI;
      // kart secenegi TIKLANABILIR ama secilince nedeni soylenip girise yonlendiriliyor.
      '<div class="co-block" style="margin-top:18px"><h3>' + ceviri("b_h_odeme_yontemi") + '</h3>' +
      '<label class="saved-item" style="display:block"><input type="radio" name="mgOdeme" id="mgOdemeKapida" value="cod" checked> ' +
      "<b>" + ceviri("b_kapida_odeme") + "</b></label>" +
      '<label class="saved-item" style="display:block"><input type="radio" name="mgOdeme" id="mgOdemeKart" value="card"> ' +
      ceviri("b_kart") + "</label>" +
      '<p class="muted" id="mgOdemeNot" style="font-size:12.5px;margin:8px 0 0">' + ceviri("b_kartla_odeme_icin") +
      '<a href="#/giris">' + ceviri("b_uye_girisi_link") + '</a>' + ceviri("b_misafir_kapida_not") + '</p>' +
      "</div>" +

      '<div class="co-block" style="margin-top:18px"><h3>' + ceviri("b_h_siparis_ozeti") + '</h3>' +
      '<div class="od-sum"><span>' + ceviri("b_ara_toplam") + '</span><b>' + money(toplam) + "</b></div>" +
      '<div class="od-sum"><span>' + ceviri("b_kargo") + (kargo === 0 ? ceviri("b_ucretsiz") : "") + "</span><b>" + money(kargo) + "</b></div>" +
      '<div class="od-sum"><span>' + ceviri("b_toplam") + '</span><b>' + money(toplam + kargo) + "</b></div>" +
      '<p class="muted" style="font-size:12px;margin:8px 0 0">' + ceviri("b_kargo_tahmini") + '</p>' +
      "</div>" +

      '<div class="co-nav" style="margin-top:18px">' +
      '<button class="btn" id="mgGonder">' + esc(ceviri("mg_submit")) + "</button></div>" +
      '<div id="mgErr" style="color:#a32d2d;font-size:13px;margin-top:10px"></div></div>';

    var btn = document.getElementById("mgGonder");
    if (btn) btn.onclick = misafirSiparisGonder;

    // GOZ-FIX / F-G4: kart secenegi TIKLANABILIR; secilince sebep soylenir, secim kapida
    // odemeye geri doner ve kullanici girise yonlendirilir. "Sessizce disabled" yerine
    // "secilebilir ama neden olmadigini soyleyen" davranis.
    var kart = document.getElementById("mgOdemeKart");
    var kapida = document.getElementById("mgOdemeKapida");
    var not = document.getElementById("mgOdemeNot");
    if (kart) {
      kart.onchange = function () {
        if (!kart.checked) return;
        if (kapida) kapida.checked = true;
        if (not) { not.style.color = "#a32d2d"; not.style.fontWeight = "600"; }
        notify(ceviri("mg_card_login"),'info');
        setTimeout(function () { location.hash = "#/giris"; }, 1400);
      };
    }
  }

  function misafirDegerleriOku() {
    function v(id) { var e = document.getElementById(id); return e ? (e.value || "").trim() : ""; }
    misafirState = {
      ad: v("mgAd"), eposta: v("mgMail"), telefon: v("mgTel"),
      il: v("mgIl"), ilce: v("mgIlce"), adres: v("mgAdres"), posta: v("mgPosta")
    };
    return misafirState;
  }

  async function misafirSiparisGonder() {
    var er = document.getElementById("mgErr");
    if (er) er.textContent = "";
    var d = misafirDegerleriOku();
    if (!d.ad) { er.textContent = ceviri("b_ad_soyad_gir"); return; }
    if (!d.eposta || d.eposta.indexOf("@") < 0) { er.textContent = ceviri("b_gecerli_eposta"); return; }
    if (!d.adres) { er.textContent = ceviri("b_acik_adres_gir"); return; }

    var kalemler = cartItemsPayload();
    if (!kalemler.length) { er.textContent = ceviri("b_sepetin_bos"); return; }

    var btn = document.getElementById("mgGonder");
    if (btn) { btn.disabled = true; btn.textContent = ceviri("sending"); }
    try {
      var r = await api.orders.placeAsGuest({
        guest_name: d.ad, guest_email: d.eposta, guest_phone: d.telefon,
        city: d.il, district: d.ilce, full_address: d.adres, zip_code: d.posta,
        coupon_code: "",              // non-nullable string - eksikse 400 (E2 dersi)
        payment_method: 1,            // A3: misafirde YALNIZ kapida odeme
        request_id: checkoutIstekIdAl(),   // MFIX-1/F-M3f: OTURUM basina (her tikta YENI degil)
        items: kalemler
      });
      // MFIX-B / K3: uc artik { id, order_number } donuyor. ESKI HALI "unwrap(r)"yi DUZ SAYI
      // varsayiyordu; nesne gelince URL'e "[object Object]" yazardi. Iki bicim de kabul edilir
      // (uye yolundaki 1850 ile ayni kalip) - eski bir yanit sekli gelse bile kirilmaz.
      var _y = unwrap(r);
      var siparisId = (_y && _y.id) ? _y.id : _y;
      // MISAFIR ICIN KRITIK: /api/order/get anonime KAPALI, yani order_number BASKA hicbir
      // yerden alinamaz. Yanittan geldiyse URL ile sonuc sayfasina TASINIR; gelmezse
      // UYDURULMAZ ve sayfa eskisi gibi referans kimligini gosterir.
      var siparisNo = (_y && _y.order_number) ? String(_y.order_number) : "";
      if (window.cart && window.cart.clear) { window.cart.clear(); if (typeof renderCart === "function") renderCart(); }
      location.hash = "#/odeme/sonuc?order=" + encodeURIComponent(siparisId)
        + (siparisNo ? "&no=" + encodeURIComponent(siparisNo) : "")
        + "&status=success&guest=1";
    } catch (e) {
      // Uc "e-posta kayitli" (409) ya da "yalniz kapida odeme" (400) donebilir - ikisi de
      // KULLANICIYA GOSTERILIR; sessizce baska bir yola sapmak yanlis olurdu.
      er.textContent = e.message || ceviri("b_siparis_olusturulamadi");
      if (btn) { btn.disabled = false; btn.textContent = ceviri("mg_submit"); }
    }
  }

  async function renderRealCheckout() {
    var view = document.getElementById("checkoutView");
    if (!view) return;

    if (!api.isLoggedIn()) {
      // A3 HIBRIT: cikisli kullaniciya artik DUZ BIR DUVAR degil, MISAFIR FORMU gosteriliyor.
      // Onceki hal "Siparisi tamamlamak icin giris yapmalisin" + tek buton idi; SSS ise
      // "misafir olarak devam edebilirsin" diyordu - vaat ile davranis CELISIYORDU.
      if (!window.cart || window.cart.size === 0) {
        view.innerHTML = bosSepetEkrani();
        return;
      }
      misafirCheckoutCiz(view);
      return;
    }
    if (!window.cart || window.cart.size === 0) {
      // A3: ayni metin iki dalda tekrarlaniyordu; tek yardimciya baglandi.
      view.innerHTML = bosSepetEkrani();
      return;
    }

    view.innerHTML = '<div class="wrap" style="padding:28px 0"><p class="muted">' + ceviri("b_odeme_hazirlaniyor") + '</p></div>';

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
        esc(a.title || a.full_name || (ceviri("b_adres_diyez") + a.id)) + " · " + esc(a.city || "") + "</option>";
    }).join("");

    view.innerHTML =
      '<div class="wrap" style="padding:28px 0;max-width:720px">' +
      "<h2>" + ceviri("b_h_odeme") + "</h2>" +

      '<div class="panel" style="margin-top:16px"><h3>Teslimat adresi</h3>' +
      (checkoutState.addresses.length
        ? '<select id="coAddr" style="width:100%;padding:9px 11px;border:1px solid #e8e4de;border-radius:8px">' + addrOpts + "</select>"
        : '<p class="muted" style="font-size:13px">' + ceviri("b_adres_yok_ekle") + '</p>') +
      '<button class="btn ghost sm" id="coNewAddr" style="margin-top:10px">' + ceviri("b_yeni_adres") + '</button>' +
      '<div id="coAddrForm" style="display:none;margin-top:12px"></div>' +
      "</div>" +

      '<div class="panel"><h3>' + ceviri("b_h_siparis_ozeti") + '</h3>' + items.join("") +
      '<div style="border-top:1px solid #e8e4de;margin-top:10px;padding-top:10px;font-size:13px">' +
      '<div style="display:flex;justify-content:space-between"><span>' + ceviri("b_ara_toplam") + '</span><span>' + money(sub) + "</span></div>" +
      (disc > 0 ? '<div style="display:flex;justify-content:space-between;color:#0f6e56"><span>Kupon indirimi</span><span>-' + money(disc) + "</span></div>" : "") +
      '<div style="display:flex;justify-content:space-between"><span>' + ceviri("b_kargo") + (ship === 0 ? ceviri("b_ucretsiz") : "") + "</span><span>" + money(ship) + "</span></div>" +
      (credUse > 0 ? '<div style="display:flex;justify-content:space-between;color:#0f6e56"><span>' + ceviri("b_magaza_kredisi") + '</span><span>-' + money(credUse) + "</span></div>" : "") +
      '<div style="display:flex;justify-content:space-between;font-weight:600;font-size:15px;margin-top:8px"><span>' + ceviri("b_toplam") + '</span><span id="coTotal">' + money(total) + "</span></div>" +
      "</div></div>" +

      '<div class="panel"><h3>' + ceviri("b_kupon") + '</h3>' +
      '<div style="display:flex;gap:8px"><input id="coCoupon" placeholder=ceviri("b_kupon_kodu") style="flex:1;padding:9px 11px;border:1px solid #e8e4de;border-radius:8px" value="' +
      (checkoutState.coupon ? esc(checkoutState.coupon.code) : "") + '">' +
      '<button class="btn ghost" id="coCouponGo">Uygula</button></div>' +
      '<div id="coCouponMsg" style="font-size:12px;margin-top:6px"></div></div>' +

      (checkoutState.credit > 0
        ? '<div class="panel"><h3>' + ceviri("b_magaza_kredisi") + '</h3>' +
          '<p class="muted" style="font-size:13px">Bakiyen: ' + money(checkoutState.credit) + "</p>" +
          '<label style="display:flex;align-items:center;gap:8px;margin-top:8px">' +
          '<input type="checkbox" id="coUseCredit"' + (checkoutState.useCredit > 0 ? " checked" : "") + "> Bakiyeyi kullan</label></div>"
        : "") +

      '<div class="panel"><h3>' + ceviri("b_h_odeme_yontemi") + '</h3>' +
      '<label style="display:flex;align-items:center;gap:8px"><input type="radio" name="coPay" value="card"' +
      (checkoutState.method === "card" ? " checked" : "") + "> " + ceviri("b_kart_guvenli") + "</label>" +
      '<label style="display:flex;align-items:center;gap:8px;margin-top:6px"><input type="radio" name="coPay" value="cod"' +
      (checkoutState.method === "cod" ? " checked" : "") + "> " + ceviri("b_kapida_odeme") + "</label>" +
      '<p class="muted" style="font-size:12px;margin-top:8px">' + ceviri("b_kart_bize_gelmez") + '</p>' +
      "</div>" +

      '<button class="btn" id="coSubmit" style="width:100%;padding:13px">' + esc(ceviri("place_order_btn")) + "</button>" +
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
    // GOZ-FIX / F-Ö2 + F-Ö4 BIRLIKTE CALISSIN: ozet tazeleme (F-Ö4) checkout HTML'ini
    // yeniden kuruyor ve submitOrder'in yazdigi GORUNUR hatayi SILIYORDU (olculdu: mesaj
    // yazildi, sepet aynalamasi renderCart'i tetikledi, drawCheckout yeniden cizdi, coErr
    // BOSALDI). Hata metni state'te tutulur ve her cizimden sonra geri konur.
    var _e = document.getElementById("coErr");
    if (_e && sonCheckoutHatasi) _e.textContent = sonCheckoutHatasi;
  }

  function toggleAddrForm() {
    var box = document.getElementById("coAddrForm");
    if (!box) return;
    if (box.style.display !== "none") { box.style.display = "none"; return; }
    box.style.display = "";
    box.innerHTML =
      '<input id="adTitle" placeholder=ceviri("b_adres_basligi") style="width:100%;padding:9px 11px;border:1px solid #e8e4de;border-radius:8px;margin-bottom:8px">' +
      '<input id="adName" placeholder=ceviri("b_ad_soyad") style="width:100%;padding:9px 11px;border:1px solid #e8e4de;border-radius:8px;margin-bottom:8px">' +
      '<input id="adPhone" placeholder=ceviri("b_telefon") style="width:100%;padding:9px 11px;border:1px solid #e8e4de;border-radius:8px;margin-bottom:8px">' +
      '<input id="adCity" placeholder=ceviri("b_il") style="width:100%;padding:9px 11px;border:1px solid #e8e4de;border-radius:8px;margin-bottom:8px">' +
      '<input id="adDistrict" placeholder=ceviri("b_ilce") style="width:100%;padding:9px 11px;border:1px solid #e8e4de;border-radius:8px;margin-bottom:8px">' +
      '<textarea id="adFull" rows="2" placeholder=ceviri("b_acik_adres") style="width:100%;padding:9px 11px;border:1px solid #e8e4de;border-radius:8px;margin-bottom:8px"></textarea>' +
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
      if (m2) { m2.style.color = "#0f6e56"; m2.textContent = ceviri("b_kupon_uygulandi"); }
    } catch (e) {
      checkoutState.coupon = null;
      drawCheckout();
      var m3 = document.getElementById("coCouponMsg");
      if (m3) { m3.style.color = "#a32d2d"; m3.textContent = e.message || ceviri("b_kupon_gecersiz"); }
    }
  }

  // GOZ-FIX / F-Ö2: gosterilen son checkout hatasi. drawCheckout her yeniden cizimde bunu
  // geri koyar; aksi halde F-Ö4'un ozet tazelemesi mesaji siliyordu.
  var sonCheckoutHatasi = "";
  function checkoutHatasiYaz(mesaj) {
    sonCheckoutHatasi = mesaj || "";
    var e = document.getElementById("coErr");
    if (e) e.textContent = sonCheckoutHatasi;
  }

  // ══ MFIX-1 / F-M3f: REQUEST_ID CHECKOUT OTURUMU BASINA ═══════════════════════════
  // OLCULDU: sunucu idempotency'si CALISIYOR (ayni request_id ikinci kez gonderilince
  // 200 + "Bu siparis zaten olusturulmus" + AYNI id) ama istemci HER TIKTA
  // crypto.randomUUID() ile YENI anahtar uretiyordu -> koruma YAPISAL OLARAK ULASILAMAZ.
  // Zinciri tamamlayan sey: "odeme formu donmedi" dali return ediyor, finally dugmeyi
  // geri aciyor ve mesaj "tekrar deneyebilirsin" diyor - ama SIPARIS ZATEN OLUSTU.
  // Omer'in turunda TEK denemeden ALTI Pending siparis cikti (dort saniyede uc tanesi).
  // Artik anahtar OTURUM basina: sepet icerigi degisince ve BASARILI siparisten sonra
  // yenilenir; arada kac kez tiklanirsa tiklansin AYNI anahtar gider.
  var _checkoutIstekId = null;
  var _checkoutSepetImzasi = null;
  function sepetImzasi() {
    try {
      return (cartItemsPayload() || []).map(function (i) {
        return i.product_id + "|" + (i.size || "") + "|" + i.quantity;
      }).sort().join(",");
    } catch (e) { return ""; }
  }
  function checkoutIstekIdYenile() { _checkoutIstekId = null; }
  function checkoutIstekIdSepeteGoreTazele() {
    var imza = sepetImzasi();
    if (imza !== _checkoutSepetImzasi) { _checkoutSepetImzasi = imza; checkoutIstekIdYenile(); }
  }
  function checkoutIstekIdAl() {
    if (!_checkoutIstekId) {
      _checkoutIstekId = (window.crypto && crypto.randomUUID)
        ? crypto.randomUUID()
        : ("co-" + Date.now() + "-" + Math.random().toString(36).slice(2, 10));
    }
    return _checkoutIstekId;
  }

  // MFIX-1 / F-M8: siparis numarasi UYDURULMAZ. order_number varsa O basilir; yoksa ne
  // oldugu DURUSTCE yazilir ve id yalnizca referans olarak gecer (bugun siparis olusturma
  // uclari YALNIZ sayisal id donuyor - MFIX-B'de order_number eklenecek).
  function siparisNoMetni(order, orderId) {
    var no = (order && order.order_number) ? String(order.order_number).trim() : "";
    if (no) return no;
    var ref = orderId || (order && order.id) || "-";
    return ceviri("b_eposta_paylasilacak") + ref + ")";
  }

  async function submitOrder() {
    var err = document.getElementById("coErr");
    var btn = document.getElementById("coSubmit");
    checkoutHatasiYaz("");
    checkoutIstekIdSepeteGoreTazele();   // MFIX-1/F-M3f: sepet degistiyse YENI anahtar
    var _zatenVarMesaji = "";            // MFIX-1/F-M3f: catch dalinda da gorulsun
    var items = cartItemsPayload();
    if (!items.length) { checkoutHatasiYaz(ceviri("b_sepet_bos_nokta")); return; }
    if (!checkoutState.addrId && checkoutState.addresses.length) { checkoutHatasiYaz(ceviri("b_adres_sec")); return; }

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
      checkoutHatasiYaz(ceviri("b_bedensiz_urun") + adlar + ceviri("b_sepetten_beden_sec"));
      return;
    }

    btn.disabled = true; btn.textContent = ceviri("sending");
    try {
      // Sunucu sepetini de esitle (siparis kalemleri govdeden gidiyor ama sepet tutarli kalsin)
      await syncCartToServer();

      var _zarf = await api.orders.place({
        customer_id: 1,                       // sunucu token'dan EZER; validator > 0 istiyor
        request_id: checkoutIstekIdAl(),   // MFIX-1/F-M3f: OTURUM basina (her tikta YENI degil)
        address_id: checkoutState.addrId || null,
        coupon_code: checkoutState.coupon ? checkoutState.coupon.code : "",   // non-nullable
        use_store_credit: checkoutState.useCredit > 0 ? checkoutState.credit : 0,
        payment_method: checkoutState.method === "cod" ? 1 : 0,
        items: items
      });
      var order = unwrap(_zarf);

      // MFIX-1 / F-M3f: sunucu AYNI request_id icin 200 + "zaten olusturulmus" doner.
      // Kullaniciya YENI siparis olusmadigi ACIKCA soylenir; akis normal devam eder
      // (kart yolunda odeme baslatma tekrar denenir - kullanicinin istedigi sey budur).
      var zatenVar = !!(_zarf && typeof _zarf.message === "string" && /zaten olu/i.test(_zarf.message));

      var orderId = (order && order.id) ? order.id : order;   // MFIX-B/K3 oncesi uc DUZ SAYI donuyordu - iki bicim de kabul
      // MFIX-B / K3: siparis numarasi ARTIK YANITTAN gelir. Onceden YALNIZ order_number icin
      // ikinci bir /api/order/get cagrisi yapiliyordu (burada ve odeme formu donmedigi dalda).
      var orderNo = (order && order.order_number) ? String(order.order_number) : "";
      if (zatenVar) {
        var _no = orderNo || String(orderId);
        _zatenVarMesaji = ceviri("b_siparis_zaten") + _no + ceviri("b_yeni_siparis_yok");
        checkoutHatasiYaz(_zatenVarMesaji);
      }
      try { sessionStorage.setItem("divisima_last_order", String(orderId)); } catch (e) {}

      if (checkoutState.method === "cod") {
        // Kapida odeme: odeme baslatilmaz, siparis dogrudan olusur.
        if (window.cart) window.cart.clear();
        try { await api.cart.clear(); } catch (e) {}
        location.hash = "#/odeme/sonuc?order=" + orderId + "&status=cod";
        return;
      }

      var pay = unwrap(await api.payment.initialize(orderId));
      if (!pay || !pay.checkout_form_content) throw new Error(ceviri("b_odeme_formu_alinamadi"));

      // GOZ-FIX / F-Ö2: "GORUNUR BIR SEY GELDI MI" KONTROLU.
      // OLCULDU: Iyzico mock modunda (Iyzico:UseRealSdk=false) uc HTTP 200 doner ama govde
      // yalnizca "<!-- Iyzico CF (UseRealSdk=false) -->" - bir HTML YORUMU. Eski kod bunu
      // truthy gorup gomuyor, host 0 px kaliyor ve scrollIntoView sayfayi asagi atiyordu:
      // kullanici icin "dugmeye bastim, sayfa zipladi, HICBIR SEY olmadi" (siparis ise
      // Pending olarak asili kaliyor - bu sabah 7 Pending siparisin sebebi budur).
      if (!odemeFormuGorunurMu(pay.checkout_form_content)) {
        // MFIX-1 / F-M8: burada SAYISAL ID basiliyordu ("Siparisin 207 numarasiyla...") -
        // o bir siparis NUMARASI degil veritabani kimligidir. Gercek order_number cekilir;
        // gelmezse UYDURULMAZ, durustce referans olarak yazilir.
        var _n2 = orderNo || orderId;
        checkoutHatasiYaz(
          ceviri("b_saglayici_form_yok") +
          ceviri("b_siparisin") + _n2 + ceviri("b_odenmemis_duruyor") +
          ceviri("b_kapida_devam"));
        notify(ceviri("err_pay_form"),'err');
        return;
      }
      embedCheckoutForm(pay.checkout_form_content);
    } catch (e) {
      // GOZ-FIX / F-Ö2: 401 = oturum gercekten bitti (api-client zaten BIR KEZ refresh
      // dener; buraya dusuyorsa o da basarisiz olmustur - olculdu: cart/add 401 ->
      // auth/refresh 401). Teknik mesaj yerine ne yapmasi gerektigi soylenir.
      if (e && e.status === 401) {
        checkoutHatasiYaz(ceviri("err_session"));
        notify(ceviri("err_session"),'err');
        setTimeout(function () { location.hash = "#/giris"; }, 1200);
      } else if (_zatenVarMesaji) {
        // MFIX-1 / F-M3f: siparis ZATEN vardi ve odeme baslatma da dustu. Kullaniciya
        // ONCE "yeni siparis olusmadi" bilgisi verilir - saglayicinin teknik metni
        // ("zaten bekleyen bir odeme var") tek basina birakilirsa kullanici yine
        // tekrar dener ve neden bir sey olmadigini ANLAMAZ.
        checkoutHatasiYaz(_zatenVarMesaji + ceviri("b_odeme_baslatilamiyor"));
      } else {
        checkoutHatasiYaz(e && e.message ? e.message : ceviri("b_siparis_olusturulamadi_b"));
      }
    } finally {
      // Dugme HER durumda eski haline doner - asili kalmaz.
      btn.disabled = false; btn.textContent = ceviri("place_order_btn");
    }
  }

  // GOZ-FIX / F-Ö2: gelen govde GERCEKTEN cizilebilir bir sey tasiyor mu?
  // Iyzico'nun gercek Checkout Form'u <script> tasir; mock yalnizca bir HTML YORUMU dondurur.
  // Yorumlar ve bosluk ayiklandiktan sonra geriye bir sey kalmiyorsa "form gelmedi" demektir.
  function odemeFormuGorunurMu(html) {
    if (!html) return false;
    var s = String(html).replace(/<!--[\s\S]*?-->/g, "").trim();
    return s.length > 0;
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
    // GOZ-FIX / F-Ö2: BOS bir host'a kaydirma YAPILMAZ. Eskiden kosulsuzdu ve mock modda
    // 0 px yuksekligindeki host'a kaydiriyordu - olculdu: scrollY 0 -> 648, ekranda hicbir
    // sey yok. Kaydirma yalnizca gercekten cizilmis bir form varsa anlamlidir.
    if (host.getBoundingClientRect().height > 0) {
      host.scrollIntoView({ behavior: "smooth", block: "start" });
    }
  }
  window.divisimaEmbedCheckoutForm = embedCheckoutForm;

  // ── Odeme sonuc sayfasi (#/odeme/sonuc?order=..&status=..) ─────────────────
  // MFIX-3 / DEVIR-3: BASARI AILESI TEK KAYNAKTAN. Iki kod yolu "basarili"yi FARKLI
  // tanimliyordu: renderPaymentResult "success VEYA cod" derken sekme basligini yazan
  // setDocTitle sarmalayicisi YALNIZ "success" ariyordu. OLCULEN ZARAR (MFIX-2 ve MFIX-3
  // R-BASLIK ONCE): basarili bir KAPIDA ODEME sipariste ekran "Siparisin alindi" derken
  // SEKME BASLIGI "Odeme Tamamlanamadi" diyordu - iki yuzey birbiriyle celisiyordu.
  // Ayrisma bir kez daha olusmasin diye olcut TEK FONKSIYONDA.
  function odemeBasariliMi(status) { return status === "success" || status === "cod"; }
  // Baslik anahtari da TEK KAYNAKTAN: ekran basligi ile sekme basligi AYNI metni gostersin.
  // (Ilk olcumde ekran "Siparisin alindi" derken sekme "Odemen alindi" diyordu - ayni aile
  // ama FARKLI metin; kapida odeme bir ODEME degildir.)
  function odemeSonucBaslikAnahtari(status) {
    return status === "cod" ? "pay_cod_title" : (odemeBasariliMi(status) ? "pay_ok_title" : "pay_fail_title");
  }

  async function renderPaymentResult(params) {
    var view = document.getElementById("checkoutView");
    if (!view) return;
    var orderId = parseInt(params.order) || 0;
    var status = params.status || "";
    var ok = odemeBasariliMi(status);

    // MFIX-1 / F-M3f: siparis TAMAMLANDI (COD ve misafir yollari da buraya duser) ->
    // bir sonraki checkout icin YENI anahtar. Kart yolunda embed sirasinda YENILENMEZ:
    // kullanici geri donup tekrar basarsa sunucu "zaten olusturulmus" demeli, YENI
    // siparis DEGIL.
    if (ok) checkoutIstekIdYenile();
    // MFIX-2 / F-M1-H3: siparis BASARILI olduysa stok DUSMUSTUR. Vitrindeki sayilar
    // siparis oncesine ait; kullanici geri dondugunde YENILEMEDEN dogru gormeli.
    // EN DAR COZUM: (1) kendi detay onbellegimizi bosalt, (2) katalogu yeniden cek.
    // Tarayici onbellegi ICIN BIR SEY YAPILMASI GEREKMEDI - olculdu: katalog ucu
    // POST /api/product/filter'dir ve POST yanitlari onbelleklenmez; ETag'in
    // "private, max-age=60" basligi yalnizca GET detay ucunu etkiler, o da bosaltilan
    // detailCache yuzunden zaten yeniden istenir.
    if (ok) katalogTazele();

    view.innerHTML = '<div class="wrap" style="padding:40px 0;max-width:640px"><p class="muted">' + ceviri("b_yukleniyor") + '</p></div>';

    var order = null;
    if (orderId) { try { order = unwrap(await api.orders.get(orderId)); } catch (e) { order = null; } }

    var baslik = ceviri(odemeSonucBaslikAnahtari(status));
    var alt = status === "cod"
      ? ceviri("pay_cod_sub")
      : ceviri(ok ? "pay_ok_sub" : "pay_fail_sub");

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

      ozet = '<div class="panel" style="text-align:left"><h3>' + ceviri("b_h_siparis_ozeti") + '</h3>' +
        '<div style="display:flex;justify-content:space-between;font-size:13px;padding:4px 0"><span>' + ceviri("b_siparis_no_etiket") + '</span><span>' +
        esc(siparisNoMetni(order, orderId)) + "</span></div>" +
        kalemler +
        '<div style="display:flex;justify-content:space-between;font-size:13px;padding:4px 0"><span>' + ceviri("b_kargo") + '</span><span>' +
        money(order.shipping_cost) + "</span></div>" +
        '<div style="display:flex;justify-content:space-between;font-size:14px;font-weight:600;padding:6px 0;border-top:1px solid #e8e4de;margin-top:6px"><span>' + ceviri("b_toplam") + '</span><span>' +
        money(toplam) + "</span></div>" +
        '<div style="display:flex;justify-content:space-between;font-size:13px;padding:4px 0"><span>Durum</span><span>' +
        esc(String(durum)) + "</span></div></div>";
    } else if (orderId) {
      ozet = '<p class="muted" style="font-size:13px">' + ceviri("b_siparis_diyez") + orderId + ceviri("b_detay_ulasilamadi") + "</p>";
    }

    // ══ A3 HIBRIT - MISAFIR SONUC EKRANI ═══════════════════════════════════════════════════
    //
    // OLCULDU (tarayici): misafir siparisinden sonra bu sayfa "Siparis #91 detayina su an
    // ulasilamadi" gosteriyor ve tek eylem butonu "Siparislerime git" oluyordu. Ikisi de
    // misafir icin DOGRU DEGIL: siparis detayi ucu [RequireUserType(Customer)] ve misafirin
    // OTURUMU YOK (A3 karari geregi verilmiyor da). Yani kullaniciya ULASAMAYACAGI bir yol
    // gosteriliyordu - M11'de ogrenilen ders: hedefteki eylem gercekten kullanilabilir olmali.
    //
    // Misafir oldugunu URL soyluyor (guest=1); tahmin edilmiyor.
    var misafirMi = String(params.guest || "") === "1";
    if (misafirMi) {
      // MFIX-1 / F-M8: "#<id>" bir SIPARIS NUMARASI DEGIL, veritabani kimligi. Misafir
      // order/get'i cagiramadigi icin (uc [RequireUserType(Customer)]; anonim GET 401 -
      // olculdu) gercek order_number ELDE YOKTU ve durustce "Referans: <id>" yaziliyordu.
      // MFIX-B / K3: numara ARTIK siparis yanitindan geliyor ve URL ile buraya tasiniyor.
      // Gelmezse eski durust hal korunur - UYDURULMAZ.
      var misafirNo = String(params.no || "").trim();
      /* MFIX-3b/(5a) ONARIM: bu cumlede "Şifremi unuttum" TIRNAK ICINDE bir ALINTIYDI ve
         duz-metin degistirmesi onu yanlislikla kod gibi ele almisti. Cumle artik TEK
         anahtarla ceviriliyor; alinti isaretleri cevirinin ICINDE. */
      ozet = '<div class="panel" style="text-align:left"><h3>' + ceviri("b_h_siparis_kaydi") + '</h3>'
        + (misafirNo
            ? '<p class="muted" style="font-size:13px;margin:6px 0 4px">' + ceviri("b_siparis_numaran") + '</p><p style="font-weight:600;margin:0 0 12px">' + esc(misafirNo) + '</p>'
            : '<p class="muted" style="font-size:13px;margin:6px 0 10px">' + ceviri("b_no_epostayla") + '</p><p class="muted" style="font-size:12px;margin:0 0 12px">' + ceviri("b_referans") + orderId + '</p>')
        + '<p class="muted" style="font-size:13px;margin:0">' + ceviri("b_misafir_sifre_belirle") + '</p></div>';
    }

    view.innerHTML =
      '<div class="wrap" style="padding:40px 0;max-width:640px;text-align:center">' +
      '<div style="font-size:44px;margin-bottom:8px">' + (ok ? "✓" : "✕") + "</div>" +
      "<h2>" + baslik + "</h2>" +
      '<p class="muted" style="margin:8px 0 18px">' + alt + "</p>" +
      ozet +
      '<div style="display:flex;gap:10px;justify-content:center;margin-top:18px">' +
      // A3: misafire "Siparislerime git" GOSTERILMEZ - oturumu yok, o sayfa ona bos/401 verir.
      // Yerine hesabini sahiplenmeye goturen GERCEKTEN calisan yol.
      (misafirMi ? '<a class="btn" href="#/giris">' + esc(ceviri("set_pass")) + "</a>"
                 : '<a class="btn" href="#/hesabim/siparislerim">' + esc(ceviri("go_orders")) + "</a>") +
      (ok ? '<a class="btn ghost" href="#/kategori/tumu">' + esc(ceviri("shop_continue")) + "</a>"
          : '<a class="btn ghost" href="#/odeme">' + ceviri("b_tekrar_dene_btn") + '</a>') +
      "</div></div>";

    if (ok && window.cart && window.cart.size) {
      window.cart.clear();
      try { if (typeof window.renderCart === "function") window.renderCart(); } catch (e) {}
    }
  }

  // MFIX-3 / F-M2: sayisal siparis durumu ARTIK sozluk anahtarina esleniyor.
  // Eskiden burada TR literalleri vardi ve EN/AR modunda da Turkce goruntuleniyordu.
  var SIPARIS_DURUM_ANAHTARI = {
    0: "st_pending", 1: "od_confirmed", 2: "od_prep", 3: "od_shipped", 4: "od_delivered", 5: "st_cancel"
  };
  function orderStatusLabel(s) {
    var k = SIPARIS_DURUM_ANAHTARI[s];
    return k ? ceviri(k) : String(s);
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

    // ══ MFIX-1 / F-M3a: MOCK ADIMLI CHECKOUT ARTIK HICBIR YOLDAN CIZILEMEZ ══════════
    // OLCULDU: index.html'in kendi mock checkout'u (renderCheckout) ile bizim gercek
    // cizicimiz AYNI kaba (#checkoutView) yaziyor ve tercih ROTAYA degil CIZIM SIRASINA
    // bagliydi. Mock'u DORT DIS yol geri getiriyordu - kupon uygula, kupon kaldir, para
    // birimi, DIL - ve biz onu GERI ALAMIYORDUK (odemeOzetiniTazele yalniz #coSubmit /
    // #mgGonder arar, mock ikisini de icermez). Mock CANLI KART FORMU tasiyor ve coFinish()
    // sunucuya HICBIR istek atmadan "Siparisin alindi" deyip sepeti bosaltiyordu.
    // Depo idiyomu: api-bridge index.html'in fonksiyonunu SARMALAYIP EZER (router, addToCart,
    // renderCart, logout ayni kalipta). Boylece DORT yol da TEK noktadan kapanir ve
    // ADDR/CARDS/couponUI gibi index.html'in BASKA yuzeylerde kullandigi parcalara
    // DOKUNULMAZ (on olcum haritasi: onlarin mock DISINDA tuketicileri VAR).
    if (typeof window.renderCheckout === "function" && !window.renderCheckout.__divisimaGercek) {
      var gercekCizim = function () {
        var yol = location.hash.replace(/^#\/?/, "").split("?")[0].split("/")[0];
        if (yol !== "odeme") return;      // checkout disinda cizim YOK
        renderRealCheckout();
      };
      gercekCizim.__divisimaGercek = true;
      window.renderCheckout = gercekCizim;
    }
    // showCheckout YALNIZ gorunumu acar; cizimi router'in ardindan kosan handle() yapar
    // (aksi halde her gezinmede IKI kez cizilirdi).
    if (typeof window.showCheckout === "function" && !window.showCheckout.__divisimaGercek) {
      var gercekGoster = function () { if (typeof window.setView === "function") window.setView("checkout"); };
      gercekGoster.__divisimaGercek = true;
      window.showCheckout = gercekGoster;
    }

    // MFIX-1 / F-M3a: cekmecede SUNUCU ile dogrulanan kupon checkout'a TASINIR.
    window.divisimaSetCheckoutCoupon = function (code, d) {
      if (!code || !d) { checkoutState.coupon = null; return; }
      checkoutState.coupon = Object.assign({ code: code }, d);
    };
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

  // ══════════════════════════════════════════════════════════════════════════
  // E3 - HESABIM EKRANLARI (gercek API) + CMS + BILDIRIM ABONELIGI
  // ══════════════════════════════════════════════════════════════════════════

  // ── OKUMA KATMANI SANITIZASYONU (iki katmanin ikincisi) ────────────────────
  // Yazma katmani ContentManager'da (InputSanitizer). Bu ikinci kalkan, depodaki
  // icerik BIR SEKILDE kirli kalsa bile (eski kayit, dogrudan SQL, baska bir yazma
  // yolu) tarayicida CALISMAMASINI saglar. innerHTML'e giden HER dis kaynakli
  // HTML buradan gecer.
  //
  // FAIL-CLOSED: DOMPurify yuklenmemisse HTML ENJEKTE EDILMEZ. Sessizce ham HTML
  // basmak, iki katmanli savunmayi tek katmana indirmek olurdu.
  function guvenliHTML(ham) {
    if (typeof window.DOMPurify === "undefined" || !window.DOMPurify.sanitize) {
      console.error("DOMPurify yuklenmedi - HTML icerik CIZILMEDI (fail-closed).");
      return null;
    }
    return window.DOMPurify.sanitize(ham || "", {
      // Legal sayfa + fatura icerigi: metin, basliklar, listeler, tablolar, baglantilar.
      ALLOWED_TAGS: ["h1", "h2", "h3", "h4", "p", "br", "hr", "strong", "b", "em", "i", "u",
        "ul", "ol", "li", "a", "span", "div", "table", "thead", "tbody", "tr", "th", "td", "small"],
      ALLOWED_ATTR: ["href", "title", "colspan", "rowspan", "class"],
      // javascript:/data: protokolleri disarida
      ALLOWED_URI_REGEXP: /^(?:https?:|mailto:|tel:|#|\/)/i,
    });
  }

  function guvenliYaz(el, ham, hataMetni) {
    var temiz = guvenliHTML(ham);
    if (temiz === null) {
      el.innerHTML = '<p class="muted">' + (hataMetni || ceviri("b_icerik_gosterilemedi")) + "</p>";
      return false;
    }
    el.innerHTML = temiz;
    return true;
  }

  // ── Ortak yardimcilar ──────────────────────────────────────────────────────
  // MFIX-3 / F-M2: metin siparis durumu ARTIK sozluk anahtarina esleniyor.
  var DURUM_ANAHTAR = {
    "Pending": "st_await", "Confirmed": "od_confirmed", "Preparing": "od_prep",
    "Shipped": "od_shipped", "Delivered": "od_delivered", "Cancelled": "st_cancelled"
  };
  function durumEtiket(s) { var k = DURUM_ANAHTAR[s]; return k ? ceviri(k) : (s || "—"); }
  // Iade durumlari AYRI enum (ReturnStatusEnum): Pending/Approved/Rejected/Completed.
  // MFIX-3 / F-M2: iade durumu da sozluk anahtarina eslenir.
  var IADE_ANAHTAR = { "Pending": "st_pending", "Approved": "st_approved", "Rejected": "st_rejected", "Completed": "st_completed" };
  function iadeDurumEtiket(s) { var k = IADE_ANAHTAR[s]; return k ? ceviri(k) : (s || "—"); }
  // SPRINT 8 MADDE 5: iade satiri ARTIK "product_name" tasiyor (backend doldurdu).
  // Onceden yalniz product_id geliyordu ve ad KATALOGDAN cozuluyordu; bu yalniz fazladan is
  // degil, YANLIS da olabiliyordu - pasiflenmis ya da katalogdan cikmis bir urunun iadesi
  // "Urun #12" gorunuyordu. Iade kaydi GECMISE ait bir belgedir; adi kaydin kendisi tasimali.
  // Once sunucunun verdigi ad, yoksa katalog, o da yoksa kimlik. Hicbir asamada UYDURMA ad yok.
  function urunAdi(pid, sunucuAdi) {
    if (sunucuAdi) return sunucuAdi;
    try { var p = (typeof byId === "function") ? byId(pid) : null; if (p) return (typeof nameOf === "function") ? nameOf(p) : (p.name || (ceviri("b_urun_no") + pid)); } catch (_) { }
    return ceviri("b_urun_no") + pid;
  }
  // MFIX-3b/(5b): TARIH BICIMI DILE BAGLI. Ad "trTarih" idi ve SABIT tr-TR kullaniyordu -
  // kabul turunda EN/AR modda "28 Ağustos 2026" olarak kalmasinin sebebi buydu.
  // Locale TEK KAYNAKTAN gelir (index.html dvsLocale); o yoksa tr-TR yedegi kalir.
  function tarihBicimi(iso) {
    if (!iso) return "—";
    var loc = (typeof window.dvsLocale === "function") ? window.dvsLocale() : "tr-TR";
    try { return new Date(iso).toLocaleDateString(loc, { day: "2-digit", month: "long", year: "numeric" }); }
    catch (_) { return String(iso).slice(0, 10); }
  }
  // Eski ad KORUNUYOR: bu dosyada 20+ cagri yeri var ve hepsini yeniden adlandirmak
  // dalganin kapsamini gereksiz buyuturdu. Davranis TEK yerden degisti.
  var trTarih = tarihBicimi;
  // LAUNCH-FIX A4: money() ile AYNI gerekce - bicim tek kaynaktan (index.html tl()).
  function paraTL(n) {
    if (typeof window.tl === "function") { try { return window.tl(Number(n) || 0); } catch (_) { } }
    return (Number(n) || 0).toLocaleString((typeof window.dvsLocale === "function") ? window.dvsLocale() : "tr-TR", { minimumFractionDigits: 2, maximumFractionDigits: 2 }) + " TL";
  }
  function bosDurum(metin, ctaMetin, ctaHref) {
    return '<div class="wrap" style="padding:24px 0"><p class="muted" style="margin:0 0 14px">' + esc(metin) + "</p>" +
      (ctaMetin ? '<a class="btn" href="' + ctaHref + '">' + esc(ctaMetin) + "</a>" : "") + "</div>";
  }
  function yukleniyor(metin) {
    return '<div class="wrap" style="padding:24px 0"><p class="muted">' + esc(metin || ceviri("b_yukleniyor")) + "</p></div>";
  }

  // IADE UYGUNLUGU - backend kurali BIREBIR yansitilir (ReturnManager.cs:22,59,64-66):
  // siparis DELIVERED olmali VE teslim tarihinden (yoksa siparis tarihinden) 14 gun gecmemis olmali.
  // Uygun degilse dugme CIZILMEZ ve SEBEBI yazilir - kullanici 400 yiyip sasirmasin.
  var IADE_PENCERESI_GUN = 14;
  function iadeUygunlugu(siparis) {
    if (siparis.order_status !== "Delivered")
      return { uygun: false, sebep: ceviri("b_iade_yalniz_teslim") };
    var taban = siparis.delivered_at || siparis.created_at;
    var son = new Date(taban);
    son.setDate(son.getDate() + IADE_PENCERESI_GUN);
    if (son < new Date())
      return { uygun: false, sebep: ceviri("b_iade_suresi_doldu") + IADE_PENCERESI_GUN + ceviri("b_gun_kapanis") };
    return { uygun: true, sonTarih: son };
  }

  // ── Sekme icerikleri ───────────────────────────────────────────────────────
  async function sekmeOzet(el) {
    try {
      var s = unwrap(await api.account.summary());
      el.innerHTML =
        '<div class="acc-tiles">' +
        '<div class="acc-tile"><div class="at-head"><b>' + ceviri("b_sadakat_puani") + '</b></div><p style="font-size:22px;margin:6px 0">' + (s.loyalty_points || 0) + "</p></div>" +
        '<div class="acc-tile"><div class="at-head"><b>' + ceviri("b_magaza_kredisi") + '</b></div><p style="font-size:22px;margin:6px 0">' + paraTL(s.store_credit) + "</p></div>" +
        '<div class="acc-tile"><div class="at-head"><b>Referans kodun</b></div><p style="font-size:18px;margin:6px 0">' + esc(s.referral_code || "—") + "</p></div>" +
        "</div>" +
        '<div class="acc-tile" style="margin-top:14px"><div class="at-head"><b>' + ceviri("b_hesap") + '</b></div>' +
        "<p>" + esc(s.name || "") + " · " + esc(s.email || "") + (s.phone ? " · " + esc(s.phone) : "") + "</p>" +
        '<p class="muted">' + ceviri("b_eposta_dogrulamasi") + (s.email_verified ? ceviri("b_yapildi") : "bekliyor") +
        ceviri("b_iki_adimli") + (s.two_factor_enabled ? ceviri("b_acik") : ceviri("b_kapali")) + "</p></div>";
    } catch (e) {
      el.innerHTML = bosDurum(ceviri("b_ozet_alinamadi") + (e && e.message ? e.message : "bilinmeyen hata"));
    }
  }

  async function sekmeSiparisler(el) {
    try {
      var liste = unwrap(await api.orders.my()) || [];
      if (!liste.length) { el.innerHTML = bosDurum(ceviri("orders_empty"), ceviri("shop_start"), "#/kategori/tumu"); return; }
      // NOT: my-orders DTO'su KALEM ICERMIYOR (OrderListResponseDto: id, order_number,
      // order_status, total, created_at). Kalemler ve zaman cizelgesi ancak ACILINCA,
      // siparis basina ayri cagriyla getirilir - gereksiz N+1 istegi onlemek icin tembel.
      el.innerHTML = liste.map(function (o) {
        return '<div class="acc-order" data-oid="' + o.id + '">' +
          '<div class="ao-head"><div class="ao-hl"><span class="ao-no">' + esc(o.order_number) + "</span>" +
          '<span class="ao-date">' + trTarih(o.created_at) + "</span></div>" +
          '<span class="ao-badge">' + esc(durumEtiket(o.order_status)) + "</span></div>" +
          '<div class="ao-body"><div class="ao-meta"><b>' + paraTL(o.total) + "</b></div></div>" +
          '<div class="od-detail" hidden></div>' +
          '<div class="ao-actions"><button class="ao-btn" data-siparis-ac="' + o.id + '">' + esc(ceviri("ord_track")) + "</button></div>" +
          "</div>";
      }).join("");
    } catch (e) {
      el.innerHTML = bosDurum(ceviri("b_siparisler_alinamadi") + (e && e.message ? e.message : "bilinmeyen hata"));
    }
  }

  // Siparis detayi + zaman cizelgesi (tembel yuklenir)
  async function siparisDetayAc(kart, orderId) {
    var kutu = kart.querySelector(".od-detail");
    if (!kutu) return;
    if (!kutu.hidden) { kutu.hidden = true; return; }
    kutu.hidden = false;
    kutu.innerHTML = yukleniyor();
    try {
      var d = unwrap(await api.orders.get(orderId)) || {};
      var kalemler = d.items || d.order_items || [];
      var satirlar = kalemler.map(function (k) {
        var ad = k.product_name || k.name || (ceviri("b_urun_no") + (k.product_id || "?"));
        return '<div class="od-row"><div class="od-info"><b>' + esc(ad) + "</b>" +
          "<span>" + esc(k.size || "") + (k.quantity ? " · " + k.quantity + " adet" : "") + "</span></div>" +
          '<div class="od-price">' + paraTL(k.unit_price != null ? k.unit_price : k.price) + "</div></div>";
      }).join("");

      var cizelge = "";
      try {
        var tl2 = unwrap(await api.orders.timeline(orderId)) || [];
        cizelge = '<div class="od-track">' + tl2.map(function (a) {
          var not = a.note || a.description || "";
          return '<div class="od-step done"><b>' + esc(durumEtiket(a.status_name)) + "</b>" +
            '<span class="muted"> · ' + trTarih(a.created_at) + (not ? " · " + esc(not) : "") + "</span></div>";
        }).join("") + "</div>";
      } catch (_) {
        cizelge = '<p class="muted">' + ceviri("b_takip_alinamadi") + '</p>';
      }

      // ══ DALGA B / B4 - KARGO TAKIP NUMARASI MUSTERIYE GOSTERILIR ═════════════════════
      // OLCULEN ONCE-DURUM: admin panele kargo firmasi + takip numarasi giriyor, siparis
      // "Kargoda" oluyor - ama musteri o numarayi HICBIR YERDE goremiyordu. Siparis detay
      // DTO'sunda kargo alani YOK ve storefront `shipment.track` ucunu HIC cagirmiyordu
      // (index.html 0, api-bridge 0 referans - tarandi). Kargo firmasi entegrasyonu yok ve
      // olmayacak (is karari), yani elle girilen bu numara musterinin paketini takip
      // edebilmesinin TEK yolu.
      //
      // AYRI CAGRI, AYRI try: kargo kaydi YOKSA uc 404 doner - bu NORMAL bir durumdur
      // (henuz kargolanmamis siparis) ve detay ekranini bozmamalidir. Blok o zaman hic cizilmez.
      var kargoBlok = "";
      try {
        var kg = unwrap(await api.shipment.track(orderId));
        if (kg && kg.tracking_number) {
          kargoBlok = '<div class="od-row"><div class="od-info"><b>' + ceviri("b_kargo") + '</b><span>' +
            esc(kg.carrier_name || "") + " · Takip no: " + esc(kg.tracking_number) +
            (kg.status_name ? " · " + esc(kg.status_name) : "") + "</span></div></div>";
        }
      } catch (_) { kargoBlok = ""; }

      var uygun = iadeUygunlugu({ order_status: d.order_status, delivered_at: d.delivered_at, created_at: d.created_at });
      var iadeBlok = uygun.uygun
        ? '<button class="ao-btn primary" data-iade-ac="' + orderId + '">' + esc(ceviri("ret_create")) + "</button>"
        : '<p class="muted" style="margin:8px 0 0">' + esc(uygun.sebep) + "</p>";

      kutu.innerHTML = cizelge + kargoBlok + satirlar +
        '<div class="od-sum"><span>' + ceviri("b_toplam") + '</span><b>' + paraTL(d.total) + "</b></div>" +
        '<div class="ao-actions" style="margin-top:10px">' +
        '<button class="ao-btn" data-fatura="' + orderId + '">' + esc(ceviri("inv_view")) + "</button>" + iadeBlok + "</div>";
    } catch (e) {
      kutu.innerHTML = '<p class="muted">' + ceviri("b_detay_alinamadi") + esc(e && e.message ? e.message : "hata") + "</p>";
    }
  }

  async function sekmeIadeler(el) {
    try {
      var liste = unwrap(await api.returns.my()) || [];
      if (!liste.length) {
        el.innerHTML = bosDurum(ceviri("b_iade_yok"),
          ceviri("go_orders"), "#/hesabim/siparislerim");
        return;
      }
      el.innerHTML = '<div class="acc-tiles">' + liste.map(function (r) {
        return '<div class="acc-tile"><div class="at-head"><b>' + ceviri("b_at_siparis") + esc(String(r.order_number || ("#" + (r.order_id || "")))) + "</b>" +
          '<span class="at-def">' + esc(iadeDurumEtiket(r.status_name)) + "</span></div>" +
          "<p>" + esc(urunAdi(r.product_id, r.product_name)) + " · " + esc(r.size || "") +
          " · " + (r.quantity || 1) + " adet</p>" +
          '<p class="muted">' + trTarih(r.created_at) + (r.refund_amount != null ? " · " + paraTL(r.refund_amount) : "") + "</p></div>";
      }).join("") + "</div>";
    } catch (e) {
      el.innerHTML = bosDurum(ceviri("b_iadeler_alinamadi") + (e && e.message ? e.message : "bilinmeyen hata"));
    }
  }

  async function sekmeFaturalar(el) {
    try {
      var liste = unwrap(await api.invoices.my()) || [];
      if (!liste.length) { el.innerHTML = bosDurum(ceviri("b_fatura_yok")); return; }
      el.innerHTML = '<div class="acc-tiles">' + liste.map(function (f) {
        return '<div class="acc-tile"><div class="at-head"><b>' + esc(f.invoice_number || ("#" + f.id)) + "</b></div>" +
          "<p>" + paraTL(f.total) + " · " + trTarih(f.created_at || f.issued_at) + "</p>" +
          '<div class="ao-actions"><button class="ao-btn" data-fatura="' + (f.order_id || "") + '">' + esc(ceviri("view_btn")) + "</button></div></div>";
      }).join("") + "</div>";
    } catch (e) {
      el.innerHTML = bosDurum(ceviri("b_faturalar_alinamadi") + (e && e.message ? e.message : "bilinmeyen hata"));
    }
  }

  async function sekmeAdresler(el) {
    try {
      var liste = unwrap(await api.address.list()) || [];
      var form = '<div class="ao-actions" style="margin-bottom:12px"><button class="ao-btn primary" data-adres-yeni>' + ceviri("b_yeni_adres") + '</button></div>';
      if (!liste.length) { el.innerHTML = form + bosDurum(ceviri("b_kayitli_adres_yok")); return; }
      el.innerHTML = form + '<div class="acc-tiles">' + liste.map(function (a) {
        return '<div class="acc-tile"><div class="at-head"><b>' + esc(a.title) + "</b>" +
          (a.is_default ? '<span class="at-def">' + ceviri("b_varsayilan") + '</span>' : "") + "</div>" +
          "<p>" + esc(a.full_name) + (a.phone ? " · " + esc(a.phone) : "") + "</p>" +
          "<p>" + esc(a.city) + " / " + esc(a.district) + "</p>" +
          '<p class="muted">' + esc(a.full_address) + "</p>" +
          '<div class="ao-actions"><button class="ao-btn" data-adres-sil="' + a.id + '">Sil</button></div></div>';
      }).join("") + "</div>";
    } catch (e) {
      el.innerHTML = bosDurum(ceviri("b_adresler_alinamadi") + (e && e.message ? e.message : "bilinmeyen hata"));
    }
  }

  // Kartlarim: MOCK bir yerel kart deposuydu. E2'de kart bilgisinin bize HIC gelmedigi
  // (Iyzico kendi sayfasinda topluyor) tespit edilmisti; sahte "kayitli kart" listesi
  // gostermek dogrudan yalan olur. Notr ve DOGRU bilgi cizilir.
  function sekmeKartlar(el) {
    el.innerHTML = '<div class="wrap" style="padding:24px 0"><p class="muted" style="margin:0 0 8px">' +
      "Kart bilgilerin Divisima'da saklanmaz." + "</p>" +
      '<p class="muted">' + ceviri("b_kart_saglayici_sayfasi") +
      ceviri("b_kart_ulasmaz") + "</p></div>";
  }

  // ── Fatura goruntuleme (sunucu HTML'i -> DOMPurify -> modal) ───────────────
  // Fatura HTML'i BIZIM sunucudan geliyor ama yine de okuma katmanindan gecer:
  // "kendi sunucum guvenli" varsayimi, iki katmanli savunmanin amacini bosa cikarir.
  function faturaModalAc(orderId) {
    var eski = document.getElementById("e3FaturaModal");
    if (eski) eski.remove();
    var m = document.createElement("div");
    m.id = "e3FaturaModal";
    m.setAttribute("role", "dialog");
    m.setAttribute("aria-modal", "true");
    m.setAttribute("aria-label", "Fatura");
    m.style.cssText = "position:fixed;inset:0;z-index:9999;background:rgba(0,0,0,.55);display:flex;align-items:center;justify-content:center;padding:20px";
    m.innerHTML = '<div style="background:#fff;color:#111;max-width:860px;width:100%;max-height:88vh;overflow:auto;border-radius:10px;padding:18px">' +
      '<div style="display:flex;justify-content:space-between;align-items:center;gap:12px;margin-bottom:10px">' +
      "<b>Fatura</b><button id=\"e3FaturaKapat\" class=\"btn ghost\">Kapat</button></div>" +
      '<div id="e3FaturaGovde">' + ceviri("b_yukleniyor") + '</div></div>';
    document.body.appendChild(m);
    m.querySelector("#e3FaturaKapat").onclick = function () { m.remove(); };
    m.addEventListener("click", function (e) { if (e.target === m) m.remove(); });

    api.orders.invoiceHtml(orderId).then(function (html) {
      var kutu = document.getElementById("e3FaturaGovde");
      if (!kutu) return;
      // SAVUNMA SATIRI: uc bos govde donerse SESSIZ bos modal yerine DURUM soylenir.
      // E3 elle dogrulamasinda bu dal GERCEKTEN tetiklendi - kok sebep SuccessDataResult<string>
      // asiri yukleme belirsizligiydi (T=string iken "(T data)" ile "(string message)" ayni
      // imzaya duser, C# non-generic olani secer -> HTML Message a gider, Data NULL kalir ve
      // controller Content(ok.Data) yazdigi icin uc 200 + Content-Length: 0 dondu).
      // E3 KAPSAMINDA DUZELTILDI (OrderManager artik "data:" adlandirilmis argumani kullanir);
      // bu dal yine de duruyor - belirsizlik dilde kaldigi icin (Sprint 8 madde 11).
      if (!html || !String(html).trim()) {
        kutu.innerHTML = '<p class="muted">' + ceviri("b_fatura_icerik_yok") +
          ceviri("b_fatura_musteri_hizmet") + "</p>";
        return;
      }
      guvenliYaz(kutu, html, ceviri("b_fatura_gosterilemedi"));
    }).catch(function (e) {
      document.getElementById("e3FaturaGovde").textContent = ceviri("b_fatura_alinamadi") + (e && e.message ? e.message : "hata");
    });
  }

  // ── Iade talebi formu (siparis detayinin icinde) ───────────────────────────
  var IADE_SEBEPLERI = [
    [0, ceviri("b_beden_uymadi")], [1, ceviri("b_ret_beklendigi_gibi")], [2, ceviri("b_ret_hatali_urun")],
    [3, ceviri("b_ret_yanlis_urun")], [4, ceviri("b_diger")]
  ];
  async function iadeFormuAc(kart, orderId) {
    var kutu = kart.querySelector(".od-detail");
    if (!kutu || kutu.querySelector(".e3-iade-form")) return;
    var d;
    try { d = unwrap(await api.orders.get(orderId)) || {}; }
    catch (e) { toast(ceviri("err_order_info"),'err'); return; }
    var kalemler = (d.items || d.order_items || []).filter(function (k) { return !k.is_cancelled; });
    if (!kalemler.length) { toast(ceviri("err_ret_none"),'err'); return; }

    var f = document.createElement("div");
    f.className = "e3-iade-form";
    f.style.cssText = "border-top:1px solid rgba(0,0,0,.12);margin-top:12px;padding-top:12px";
    f.innerHTML =
      "<h4 style=\"margin:0 0 8px\">" + ceviri("b_h_iade_talebi") + "</h4>" +
      '<label style="display:block;margin-bottom:8px">' + ceviri("b_urun") + '<select id="e3IadeKalem" style="width:100%">' +
      kalemler.map(function (k, i) {
        var ad = k.product_name || k.name || (ceviri("b_urun_no") + k.product_id);
        return '<option value="' + i + '">' + esc(ad) + " · " + esc(k.size || "") + " · " + (k.quantity || 1) + " adet</option>";
      }).join("") + "</select></label>" +
      '<label style="display:block;margin-bottom:8px">Adet<input id="e3IadeAdet" type="number" min="1" value="1" style="width:100%"></label>' +
      '<label style="display:block;margin-bottom:8px">Sebep<select id="e3IadeSebep" style="width:100%">' +
      IADE_SEBEPLERI.map(function (s) { return '<option value="' + s[0] + '">' + esc(s[1]) + "</option>"; }).join("") + "</select></label>" +
      '<label style="display:block;margin-bottom:8px">' + ceviri("b_tur") + '<select id="e3IadeTur" style="width:100%">' +
      '<option value="0">' + ceviri("b_iade") + '</option><option value="1">' + ceviri("b_degisim") + '</option></select></label>' +
      '<label style="display:block;margin-bottom:10px">' + ceviri("b_aciklama_istege") + '<textarea id="e3IadeAcik" rows="2" style="width:100%"></textarea></label>' +
      '<button class="ao-btn primary" id="e3IadeGonder">' + esc(ceviri("ret_send")) + "</button>";
    kutu.appendChild(f);

    f.querySelector("#e3IadeGonder").onclick = async function () {
      var i = +f.querySelector("#e3IadeKalem").value;
      var k = kalemler[i];
      var adet = Math.max(1, Math.min(+f.querySelector("#e3IadeAdet").value || 1, k.quantity || 1));
      var btn = f.querySelector("#e3IadeGonder");
      btn.disabled = true;
      try {
        // customer_id GONDERILMEZ - JWT'den override ediliyor (ReturnCreateRequestDto yorumu).
        await api.returns.create({
          order_id: orderId, product_id: k.product_id, size: k.size || "",
          quantity: adet, reason: +f.querySelector("#e3IadeSebep").value,
          return_type: +f.querySelector("#e3IadeTur").value,
          description: f.querySelector("#e3IadeAcik").value.trim()
        });
        toast(ceviri("ok_ret_sent"),'ok');
        f.innerHTML = '<p class="muted">' + ceviri("b_iade_olusturuldu") + '</p>';
      } catch (e) {
        btn.disabled = false;
        toast(ceviri("b_iade_olusturulamadi") + (e && e.message ? e.message : "hata"),'err');
      }
    };
  }

  // ── Adres formu ────────────────────────────────────────────────────────────
  function adresFormuAc(el) {
    if (el.querySelector(".e3-adres-form")) return;
    var f = document.createElement("div");
    f.className = "e3-adres-form acc-tile";
    f.style.cssText = "margin-bottom:12px";
    f.innerHTML =
      '<input id="e3AdBaslik" placeholder=ceviri("b_adres_basligi") style="width:100%;margin-bottom:6px">' +
      '<input id="e3AdAd" placeholder=ceviri("b_ad_soyad") style="width:100%;margin-bottom:6px">' +
      '<input id="e3AdTel" placeholder=ceviri("b_telefon") style="width:100%;margin-bottom:6px">' +
      '<input id="e3AdIl" placeholder=ceviri("b_il") style="width:100%;margin-bottom:6px">' +
      '<input id="e3AdIlce" placeholder=ceviri("b_ilce") style="width:100%;margin-bottom:6px">' +
      '<textarea id="e3AdTam" rows="2" placeholder=ceviri("b_acik_adres") style="width:100%;margin-bottom:6px"></textarea>' +
      '<label style="display:block;margin-bottom:8px"><input type="checkbox" id="e3AdVars">' + ceviri("b_varsayilan_adres") + '</label>' +
      '<button class="ao-btn primary" id="e3AdKaydet">Adresi kaydet</button>';
    el.insertBefore(f, el.firstChild);
    f.querySelector("#e3AdKaydet").onclick = async function () {
      var p = {
        // id GONDERILMEZ -> yeni kayit. customer_id sunucuda token'dan set edilir.
        title: f.querySelector("#e3AdBaslik").value.trim(),
        full_name: f.querySelector("#e3AdAd").value.trim(),
        phone: f.querySelector("#e3AdTel").value.trim(),
        city: f.querySelector("#e3AdIl").value.trim(),
        district: f.querySelector("#e3AdIlce").value.trim(),
        full_address: f.querySelector("#e3AdTam").value.trim(),
        is_default: f.querySelector("#e3AdVars").checked
      };
      if (!p.title || !p.city || !p.full_address) { toast(ceviri("err_addr_req"),'err'); return; }
      try { await api.address.upsert(p); toast(ceviri("b_adres_kaydedildi"),'ok'); sekmeAdresler(el); }
      catch (e) { toast(ceviri("b_adres_kaydedilemedi_i") + (e && e.message ? e.message : "hata"),'err'); }
    };
  }

  // ── renderAccount OVERRIDE ─────────────────────────────────────────────────

  // ── (SPRINT 8 MADDE 10) BILDIRIMLERIM ──────────────────────────────────────
  //
  // OLCULEN BOSLUK: backend'de YALNIZ "subscribe" vardi - kullanici kurdugu stok/fiyat
  // bildirimini ne gorebiliyor ne kapatabiliyordu. Uclar eklendi, bu ekran onlari kullaniyor.
  // Iki farkli tablo (stok / fiyat dususu) kullanici icin TEK liste; her satir turunu soyluyor.
  async function sekmeBildirimler(el) {
    el.innerHTML = yukleniyor();
    var stok = [], fiyat = [], hata = null;
    try {
      // Iki uc BAGIMSIZ: biri patlarsa digeri yine gosterilir (hepsini birden kaybetmeyelim).
      var sonuclar = await Promise.allSettled([api.stockNotification.mine(), api.priceDrop.mine()]);
      if (sonuclar[0].status === "fulfilled") stok = unwrap(sonuclar[0].value) || [];
      if (sonuclar[1].status === "fulfilled") fiyat = unwrap(sonuclar[1].value) || [];
      if (sonuclar[0].status === "rejected" && sonuclar[1].status === "rejected") {
        hata = (sonuclar[0].reason && sonuclar[0].reason.message) || "hata";
      }
    } catch (e) { hata = (e && e.message) ? e.message : "hata"; }

    if (hata) {
      el.innerHTML = '<p class="muted">' + ceviri("b_bildirim_alinamiyor") + esc(hata) + ").</p>";
      return;
    }

    var hepsi = stok.concat(fiyat);
    if (!hepsi.length) {
      el.innerHTML = '<p class="muted">' + ceviri("b_bildirim_yok") +
        ceviri("b_bildirim_nasil") + "</p>";
      return;
    }

    // En yeni ustte. created_at sunucudan ISO geliyor; Date ile karsilastirilir.
    hepsi.sort(function (a, b) { return new Date(b.created_at) - new Date(a.created_at); });

    el.innerHTML = hepsi.map(function (s) {
      var stokMu = s.type === "stock";
      var ad = s.product_name || (ceviri("b_urun_no") + s.product_id);
      var ayrinti = stokMu
        ? (s.size ? "Beden " + esc(s.size) : ceviri("b_tum_bedenler"))
        : (ceviri("b_takip_fiyati") + paraTL(s.subscribed_price));
      // is_notified: bildirim GONDERILMIS demek. Satiri gizlemiyoruz - kullanici "bana haber
      // verilmis mi" sorusunun yanitini gorebilmeli; ama durumu ACIKCA yaziyoruz.
      var durum = s.is_notified
        ? '<span class="muted">Haber verildi</span>'
        : '<span class="muted">Bekliyor</span>';
      return '<div class="acc-tile" style="display:flex;justify-content:space-between;gap:12px;align-items:center;margin-bottom:10px">' +
        "<div><b>" + esc(ad) + "</b><br>" +
        '<span class="muted">' + (stokMu ? ceviri("b_stok_bildirimi") : ceviri("b_fiyat_uyarisi")) + " · " + ayrinti + "</span><br>" +
        durum + "</div>" +
        '<button class="btn ghost" data-bildirim-sil="' + s.id + '" data-bildirim-tur="' +
        (stokMu ? "stock" : "price_drop") + '">' + esc(ceviri("remove")) + "</button></div>";
    }).join("");
  }
  // MFIX-3 / F-M2: hesap menusu etiketleri ARTIK sozluk anahtari (ikinci alan).
  // OLCULEN ONCE-DURUM: EN modunda 10/10 sekme TURKCE kaliyordu.
  // SPRINT 8 MADDE 10: bildirim abonelikleri GORULEBILIR ve KAPATILABILIR (bildirimlerim).
  var E3_SEKMELER = [
    ["ozet", "acc_summary"], ["siparislerim", "acc_orders"], ["iadelerim", "acc_returns"],
    ["faturalarim", "acc_invoices"], ["adreslerim", "acc_addr"],
    ["bildirimlerim", "acc_notifs"],
    ["favorilerim", "acc_favs"], ["kartlarim", "acc_cards"], ["bilgilerim", "acc_profile"]
  ];

  function wireAccount() {
    window.renderAccount = function (tab) {
      tab = tab || "ozet";
      var side = '<div class="acc-side"><div class="acc-user"><div class="acc-ava">' +
        esc((window.userName || "U").charAt(0).toUpperCase()) + '</div><div class="acc-uname"><b>' +
        esc(window.userName || "—") + "</b><small>" + esc(ceviri("acc_member")) + "</small></div></div><nav class=\"acc-nav\">" +
        E3_SEKMELER.map(function (x) {
          return '<a href="#/hesabim/' + x[0] + '" class="acc-link' + (x[0] === tab ? " on" : "") + '">' + esc(ceviri(x[1])) + "</a>";
        }).join("") + '<a href="#/" class="acc-link acc-logout" id="accLogout">' + esc(ceviri("acc_logout")) + "</a></nav></div>";

      accountView.innerHTML = '<div class="cat-banner"><div class="wrap"><div class="breadcrumb"><a href="#/">' +
        esc(ceviri("home")) + "</a> &nbsp;/&nbsp; " + esc(ceviri("acc_title")) + "</div>" +
        '<h1 class="serif">' + esc(ceviri("acc_greet")) + ", " + esc((window.userName || "").split(" ")[0] || window.userName || "—") + "</h1></div></div>" +
        '<section class="wrap acc-grid">' + side + '<div class="acc-content">' + yukleniyor() + "</div></section>";

      var lo = document.getElementById("accLogout");
      if (lo) lo.onclick = function (e) { e.preventDefault(); logout(); };

      var el = accountView.querySelector(".acc-content");
      if (!el) return;

      if (tab === "ozet") sekmeOzet(el);
      else if (tab === "siparislerim") sekmeSiparisler(el);
      else if (tab === "iadelerim") sekmeIadeler(el);
      else if (tab === "faturalarim") sekmeFaturalar(el);
      else if (tab === "adreslerim") sekmeAdresler(el);
      else if (tab === "bildirimlerim") sekmeBildirimler(el);
      else if (tab === "kartlarim") sekmeKartlar(el);
      else if (tab === "favorilerim") el.innerHTML = (typeof accFavs === "function" ? accFavs() : "");
      else if (tab === "bilgilerim") el.innerHTML = (typeof accProfile === "function" ? accProfile() : "");

      el.addEventListener("click", function (e) {
        var ac = e.target.closest("[data-siparis-ac]");
        if (ac) { siparisDetayAc(ac.closest(".acc-order"), +ac.getAttribute("data-siparis-ac")); return; }
        var fa = e.target.closest("[data-fatura]");
        if (fa) { var oid = +fa.getAttribute("data-fatura"); if (oid) faturaModalAc(oid); return; }
        var ia = e.target.closest("[data-iade-ac]");
        if (ia) { iadeFormuAc(ia.closest(".acc-order"), +ia.getAttribute("data-iade-ac")); return; }
        if (e.target.closest("[data-adres-yeni]")) { adresFormuAc(el); return; }
        // SPRINT 8 MADDE 10: abonelik kaldirma. Tur satirda tasiniyor cunku iki AYRI uc var
        // (stok / fiyat dususu) ve id'ler bagimsiz - tur olmadan hangi uca gidilecegi bilinemez.
        var bs = e.target.closest("[data-bildirim-sil]");
        if (bs) {
          var bid = +bs.getAttribute("data-bildirim-sil");
          var tur = bs.getAttribute("data-bildirim-tur");
          var cagri = (tur === "stock") ? api.stockNotification.remove(bid) : api.priceDrop.remove(bid);
          cagri.then(function () { toast(ceviri("ok_notif_rm"),'ok'); sekmeBildirimler(el); })
            .catch(function (er) { toast(ceviri("b_kaldirilamadi") + (er && er.message ? er.message : "hata"),'err'); });
          return;
        }
        var as = e.target.closest("[data-adres-sil]");
        if (as) {
          var aid = +as.getAttribute("data-adres-sil");
          api.address.remove(aid).then(function () { toast(ceviri("b_adres_silindi"),'ok'); sekmeAdresler(el); })
            .catch(function (er) { toast(ceviri("b_adres_silinemedi") + (er && er.message ? er.message : "hata"),'err'); });
          return;
        }
        var fav = e.target.closest("[data-fadd]");
        if (fav && typeof addToCart === "function") { addToCart(+fav.getAttribute("data-fadd"), "", 1, null); return; }
      });

      var ps = document.getElementById("pfSave");
      if (ps && typeof saveProfileForm === "function") ps.onclick = saveProfileForm;
    };

    // ILK YUKLEME TUZAGI (wireLegal ile ayni - E2'de sepette, E3'te sozlesmede yasandi):
    // index.html'in kendi script'i router()'i BIZ ezmeden ONCE calistiriyor. Sayfa dogrudan
    // #/hesabim ile acildiysa ekranda ESKI accOrders() cizilir ve MOCK_ORDERS gorunur
    // (olculdu: DVS-20260012 gibi sahte siparis numaralari). Ezdikten sonra yeniden cizeriz.
    if (location.hash.indexOf("#/hesabim") === 0 && typeof window.loggedIn !== "undefined" && window.loggedIn) {
      var _tab = location.hash.replace(/^#\/?/, "").split("?")[0].split("/")[1] || "ozet";
      window.renderAccount(_tab);
    }
  }

  // ── (b) CMS: #/sozlesme sayfalari API'den ──────────────────────────────────
  // Gomulu LEGAL nesnesi ARTIK KULLANILMIYOR. Icerik content/get/{slug}'dan gelir ve
  // okuma katmani (DOMPurify) uzerinden cizilir.
  function wireLegal() {
    window.showLegal = function (slug) {
      slug = slug || "mesafeli-satis";
      setView("legal");
      var L = (window.lang === "en") ? "en" : "tr";
      legalView.innerHTML = '<div class="cat-banner"><div class="wrap"><div class="breadcrumb"><a href="#/">' + ceviri("b_anasayfa") + '</a> &nbsp;/&nbsp; ' + ceviri("b_sozlesmeler") + '</div>' +
        '<h1 class="serif">…</h1></div></div><section class="wrap legal-wrap"><div class="legal-doc" id="e3LegalGovde">' +
        yukleniyor() + "</div></section>";

      api.content.get(slug).then(function (r) {
        var c = unwrap(r) || {};
        var baslik = (L === "en" && c.title_en) ? c.title_en : c.title_tr;
        var govde = (L === "en" && c.body_en) ? c.body_en : c.body_tr;
        var h1 = legalView.querySelector("h1");
        if (h1) h1.textContent = baslik || ceviri("b_sozlesme");
        var kutu = document.getElementById("e3LegalGovde");
        if (kutu) guvenliYaz(kutu, govde, ceviri("b_sozlesme_gosterilemedi"));
        document.title = (baslik || ceviri("b_sozlesme")) + " · Divisima";
      }).catch(function (e) {
        var kutu = document.getElementById("e3LegalGovde");
        if (kutu) kutu.innerHTML = '<p class="muted">' + ceviri("b_sayfa_goruntulenemiyor") +
          (e && e.status === 404 ? ceviri("b_icerik_yok") : "") + ".</p>";
      });
      window.scrollTo(0, 0);
    };

    // ILK YUKLEME TUZAGI (E2'de sepette birebir yasandi): index.html'in kendi script'i
    // router()'i BIZ ezmeden ONCE bir kez calistiriyor. Sayfa dogrudan #/sozlesme/... ile
    // acildiysa ekranda ESKI (gomulu LEGAL) surum kalir ve API yolu hic denenmez.
    // O yuzden ezdikten sonra, zaten o sayfadaysak YENIDEN cizeriz.
    if (location.hash.indexOf("#/sozlesme") === 0) {
      var _slug = location.hash.replace(/^#\/?/, "").split("?")[0].split("/")[1] || "mesafeli-satis";
      window.showLegal(_slug);
    }
  }

  // ── (d) BILDIRIM ABONELIKLERI: mock akislar gercek uclara baglanir ─────────
  // OLCULEN SOZLESME BOSLUGU: backend'de YALNIZ "subscribe" var; unsubscribe ve
  // "aboneliklerim" ucu YOK (tum controller'larda arandi). Bu yuzden:
  //  - Stok bildirimi: gercek POST yapilir.
  //  - Fiyat uyarisi: TEK YONLU "abone ol" olur; eskiden yereldeki listeyi acip kapatan
  //    bir anahtardi ve sunucuda karsiligi YOKTU - "kapattim" demek YALAN olurdu.
  function wireNotify() {
    // openNotify(p, size) kutuyu ciziyor ama urun/beden bilgisini DOM'a birakmiyor.
    // Sarmalayip global olarak saklariz - abonelik POST'u bunlari gonderecek.
    if (typeof window.openNotify === "function") {
      var _openNotify = window.openNotify;
      window.openNotify = function (p, size) {
        window.__e3NotifyPid = p ? p.id : null;
        window.__e3NotifySize = size || "";
        return _openNotify.apply(this, arguments);
      };
    }

    // Stok bildirimi (urun detayindaki "gelince haber ver" kutusu)
    document.addEventListener("click", function (e) {
      var b = e.target.closest("#notifyBtn");
      if (!b) return;
      var box = b.closest(".notify-box") || b.parentNode.parentNode;
      var inp = document.getElementById("notifyEmail");
      var em = inp ? inp.value.trim() : "";
      if (!em || em.indexOf("@") < 1) { if (inp) { inp.style.borderColor = "#b85c5c"; inp.focus(); } return; }
      var pid = window.__e3NotifyPid, size = window.__e3NotifySize;
      if (!pid) { toast(ceviri("err_prod_info"),'err'); return; }
      e.preventDefault(); e.stopPropagation();
      b.disabled = true;
      api.stockNotification.subscribe(pid, size || "", em).then(function () {
        if (box) box.innerHTML = '<div class="notify-done"><span class="nd-ic">✓</span>' + ceviri("b_stoga_girince") + esc(em) + ceviri("b_haber_verecegiz") + "</div>";
        toast(ceviri("ok_notify_saved"),'ok');
      }).catch(function (er) {
        b.disabled = false;
        toast(ceviri("b_kayit_yapilamadi") + (er && er.message ? er.message : "hata"),'err');
      });
    }, true);

    // OLCULEN E3 HATASI: burada once "window.userEmail" okunuyordu. index.html o degiskeni
    // kendi yerel profil deposundan (dvs_profile) dolduruyor ve GERCEK giris o alani
    // DOLDURMUYOR (olculdu: giris yapilmis kullanicida dvs_profile = {name:"E3 Fix", email:""}),
    // yani giris yapmis kullaniciya da "giris yapmalisin" deniyordu. Dogru kaynak sunucudur:
    // /api/Account/summary. Bir kez cekilip onbellege alinir.
    var _epostaOnbellek = null;
    async function kullaniciEpostasi() {
      var yerel = (window.userEmail || "").trim();
      if (yerel) return yerel;
      if (_epostaOnbellek) return _epostaOnbellek;
      try {
        var s = unwrap(await api.account.summary()) || {};
        _epostaOnbellek = (s.email || "").trim();
        if (_epostaOnbellek) window.userEmail = _epostaOnbellek;   // index.html tarafi da faydalansin
        return _epostaOnbellek;
      } catch (e) { return ""; }
    }

    // Fiyat uyarisi (favorilerdeki zil dugmesi) - TEK YONLU abonelik
    document.addEventListener("click", async function (e) {
      var pa = e.target.closest("[data-palert]");
      if (!pa) return;
      e.preventDefault(); e.stopPropagation();
      var pid = +pa.getAttribute("data-palert");
      var em = await kullaniciEpostasi();
      if (!em) { toast(ceviri("err_pa_login"),'info'); return; }
      if (pa.classList.contains("on")) { toast(ceviri("err_pa_dup"),'info'); return; }
      api.priceDrop.subscribe(pid, em).then(function () {
        pa.classList.add("on");
        toast(ceviri("ok_pa_set"),'ok');
      }).catch(function (er) {
        toast(ceviri("b_fiyat_uyarisi_kurulamadi") + (er && er.message ? er.message : "hata"),'err');
      });
    }, true);
  }


  // ── (SPRINT 8 MADDE 12) PAYLASIM BAGLANTILARI: #/urun/<id> ─────────────────
  //
  // OLCUM DUZELTMESI (ONEMLI): E3 raporunda "router #/urun yolunu TANIMIYOR" yazilmisti -
  // BU YANLISTI. index.html:2077'deki router'da yol VAR:
  //     else if(top==='urun'){ showHome(); var _pid=+h[1]; if(byId(_pid)) openDetail(_pid); }
  // Yeniden olculdu: #/urun/1 ile acilan sayfada gorunen view "home", `detailOpenId` 1 -
  // yani urun detayi GERCEKTEN aciliyor. Gozlenen "Sayfa Bulunamadi" bir 404 SAYFASI degil,
  // SAYFA BASLIGIYDI.
  //
  // GERCEK KUSUR IKI TANE:
  //  (1) BASLIK. `setDocTitle()` icinde 'urun' dali YOK; bilinmeyen yol dalina duser ve
  //      "Sayfa Bulunamadi · Divisima" yazar. Ustelik router bu fonksiyonu openDetail'DEN
  //      SONRA cagiriyor, yani openDetail'in setProductSchema ile koydugu dogru baslik
  //      hemen EZILIYOR. Paylasilan her urun baglantisi tarayici sekmesinde ve sosyal
  //      onizlemede "Sayfa Bulunamadi" gorunuyor.
  //  (2) KATALOG YARISI. Acilista router, PRODUCTS'in O ANDAKI icerigiyle calisiyor; gercek
  //      katalog ASENKRON geliyor ve `loadCatalog` sonrasi yeniden yonlendirme YALNIZ
  //      "#/kategori" icin yapiliyordu. Bu, Hesabim > Favorilerim'de bu oturumda OLCULEN
  //      yarisin aynisi (mock urun cizilmisti).
  function urunRotasiniTazele() {
    if (location.hash.indexOf("#/urun") !== 0) return;
    var m = location.hash.match(/^#\/urun\/(\d+)/);
    if (!m) return;
    var id = +m[1];
    if (typeof byId !== "function" || !byId(id)) return;   // urun katalogda yoksa dokunma
    if (typeof openDetail === "function") openDetail(id);
    urunBasligiDuzelt(id);
  }

  function urunBasligiDuzelt(id) {
    if (typeof byId !== "function") return;
    var p = byId(id);
    if (!p) return;
    var ad = (typeof nameOf === "function") ? nameOf(p) : p.name;
    if (ad) document.title = ad + " · Divisima";
  }

  function wireUrunRotasi() {
    // setDocTitle'i SARMALA: router onu openDetail'den SONRA cagirdigi icin dogru basligi
    // eziyordu. Sarmalayici once orijinali calistirir (diger yollar aynen kalsin), sonra
    // yalniz #/urun yolunda basligi duzeltir.
    if (typeof window.setDocTitle === "function") {
      var _setDocTitle = window.setDocTitle;
      window.setDocTitle = function () {
        _setDocTitle.apply(this, arguments);
        var m = location.hash.match(/^#\/urun\/(\d+)/);
        if (m) urunBasligiDuzelt(+m[1]);
        // KALITE SUPURMESI B9: odeme SONUC sayfasinin kendi basligi yoktu - "Ödeme · Divisima"
        // kaliyordu. Kullanici geri donup sekmelerine baktiginda odeme formu ile sonuc sayfasi
        // AYNI gorunuyor; ustelik sonuc sayfasi paylasilan/yer imlerine eklenen bir adres.
        // Basarili/basarisiz ayrimi da baslikta gorunsun (status parametresi zaten adreste).
        if (location.hash.indexOf("#/odeme/sonuc") === 0) {
          // MFIX-3 / DEVIR-3: olcut TEK KAYNAKTAN (odemeBasariliMi) - "cod" da BASARI.
          // Eskiden burada `indexOf("status=success")` vardi ve kapida odeme BASARISIZ sayiliyordu.
          var sm = location.hash.match(/[?&]status=([^&]*)/);
          var durum = sm ? decodeURIComponent(sm[1]) : "";
          document.title = ceviri(odemeSonucBaslikAnahtari(durum)) + " · Divisima";
        }
      };
      // MFIX-3 / DEVIR-3: DOGRUDAN ACILIS YARISI. Sayfa dogrudan #/odeme/sonuc ile
      // acildiginda (paylasilan baglanti, yer imi, saglayici 302 donusu) index.html'in
      // router'i api-bridge YUKLENMEDEN once kosar ve baslik "Odeme · Divisima" kalir -
      // B9'un asil gerekcesi tam bu senaryoydu. Sarmalayici kuruldugu an bir kez calistir.
      // (MFIX-1'de belgelenen `defer` yarisinin ayni sinifi; urun rotasinda da ayni
      // telafi var - `urunRotasiniTazele`.)
      if (location.hash.indexOf("#/odeme/sonuc") === 0) {
        try { window.setDocTitle(); } catch (e) {}
      }
    }
    // Ilk yukleme + katalog sonrasi tazeleme init() icinde yapiliyor.
  }
  // ══ LAUNCH-FIX A2 - SIFREMI UNUTTUM + A1(c) DOGRULAMA ROTASI ═══════════════════════════════
  //
  // OLCULEN ONCE-DURUM (kapsama denetimi):
  //   index.html'de  <a href="#" data-i18n="forgot">Sifremi unuttum</a>  -> href="#" OLU LINK.
  //   api-client.js'te forgotPassword/resetPassword TANIMLI ama CAGIRAN YOK (api-bridge'de 0
  //   eslesme). Yani sifresini unutan musterinin siteden geri donus yolu HIC YOKTU.
  //   Backend tarafi hazirdi: token 30 dk, TEK KULLANIMLIK (kullanildiginda null'lanir) ve
  //   sifre degisince TUM oturumlar kapatiliyor.
  //
  // ROTALAR ROUTER SARMALANARAK EKLENIYOR (index.html'in router'ina DOKUNULMADI):
  //   #/dogrula/<token>        -> jetonu otomatik dogrula
  //   #/sifre-sifirla/<token>  -> yeni sifre ekrani
  // Sarmalama kalibi depoda zaten var (setDocTitle, logout, addToCart). Bilinmeyen rota
  // show404'e dustugu icin bu iki yol ONCE yakalanmak zorunda.
  //
  // EKRANLAR AUTH GORUNUMUNU YENIDEN KULLANIR: yeni bir view acilmadi; kutular showVerifyPrompt
  // kalibiyla #paneLogin'e enjekte ediliyor.
  function authKutusu(baslik) {
    if (typeof window.showAuth === "function") window.showAuth();
    var host = document.getElementById("paneLogin") || document.body;
    var box = document.getElementById("dvsAuthAksiyon");
    if (!box) {
      box = document.createElement("div");
      box.id = "dvsAuthAksiyon";
      box.style.cssText = "margin-top:14px;padding:14px;border:1px solid #e8e4de;border-radius:10px;background:#faf8f5";
      host.appendChild(box);
    }
    box.innerHTML = '<div style="font-weight:600;margin-bottom:8px">' + esc(baslik) + "</div>" +
      '<div id="dvsAuthGovde" style="font-size:13px;color:#6b6b6b"></div>' +
      '<div id="dvsAuthErr" style="color:#a32d2d;font-size:12px;margin-top:8px"></div>';
    return box;
  }
  function authInput(id, ph, tip) {
    return '<input id="' + id + '" type="' + (tip || "text") + '" placeholder="' + esc(ph) +
      '" style="width:100%;padding:9px 11px;border:1px solid #e8e4de;border-radius:8px;margin-top:8px">';
  }
  function authBtn(id, metin) {
    return '<button id="' + id + '" style="margin-top:10px;padding:9px 16px;border:none;border-radius:8px;' +
      'background:#111;color:#fff;cursor:pointer">' + esc(metin) + "</button>";
  }

  function sifremiUnuttumEkrani(onDolu) {
    authKutusu(ceviri("b_sifremi_unuttum"));
    document.getElementById("dvsAuthGovde").innerHTML =
      ceviri("b_sifirlama_aciklama") +
      authInput("dvsFpMail", "E-posta", "email") + authBtn("dvsFpGo", ceviri("b_baglanti_gonder"));
    if (onDolu) document.getElementById("dvsFpMail").value = onDolu;
    document.getElementById("dvsFpGo").onclick = async function () {
      var er = document.getElementById("dvsAuthErr"); er.textContent = "";
      var mail = (document.getElementById("dvsFpMail").value || "").trim();
      if (!mail) { er.textContent = ceviri("b_eposta_gir"); return; }
      try {
        await api.auth.forgotPassword(mail);
        // GUVENLIK-FIX (G2) KALIBI: uc, adresin kayitli olup olmadigini SIZDIRMIYOR (her durumda
        // ayni yanit). Istemci de bu yuzden "gonderildi" diye KESIN konusamaz.
        document.getElementById("dvsAuthGovde").innerHTML =
          ceviri("b_sifirlama_gonderildi");
      } catch (e) { er.textContent = e.message || ceviri("b_gonderilemedi"); }
    };
  }

  function sifreSifirlaEkrani(token) {
    authKutusu(ceviri("b_yeni_sifre_belirle"));
    document.getElementById("dvsAuthGovde").innerHTML =
      ceviri("b_sifre_kural_aciklama") +
      (token ? "" : authInput("dvsRpToken", ceviri("b_epostadaki_kod"), "text")) +
      authInput("dvsRpPass", ceviri("b_yeni_sifre"), "password") +
      authInput("dvsRpPass2", ceviri("b_yeni_sifre_tekrar"), "password") +
      authBtn("dvsRpGo", ceviri("b_sifreyi_guncelle"));
    document.getElementById("dvsRpGo").onclick = async function () {
      var er = document.getElementById("dvsAuthErr"); er.textContent = "";
      var tkEl = document.getElementById("dvsRpToken");
      var tk = token || (tkEl ? (tkEl.value || "").trim() : "");
      var p1 = document.getElementById("dvsRpPass").value || "";
      var p2 = document.getElementById("dvsRpPass2").value || "";
      if (!tk) { er.textContent = ceviri("b_epostadaki_kod_gir"); return; }
      if (p1 !== p2) { er.textContent = ceviri("b_iki_sifre_ayni_degil"); return; }
      // ISTEMCI TARAFI POLITIKA - SUNUCUNUN YERINE GECMEZ, ONUNLA AYNI OLMAYA CALISIR.
      // A2-FIX (SUPHELI #21) SONRASI DURUM: sunucu tarafinda kural ARTIK VAR ve DORT ucta da
      // AYNI (Divisima.Core.Security.SifrePolitikasi - register / seller register /
      // change-password / reset-password). Buradaki kontrol kullaniciyi bir gidis-donusten
      // kurtarir; GUVENCE SUNUCUDADIR.
      // NOT: sunucu Unicode buyuk/kucuk harf sayar ("Ş"/"ş" de gecerlidir), buradaki regex ise
      // ASCII. Yani istemci sunucudan BIR TIK KATI - yanlis pozitif uretmez, yalniz Turkce
      // harfli bir sifreyi burada reddedip sunucuda kabul ettirebilir. Ters yonde bir bosluk
      // YOK; kritik olan da bu.
      if (p1.length < 8 || !/[A-Z]/.test(p1) || !/[a-z]/.test(p1) || !/[0-9]/.test(p1)) {
        er.textContent = ceviri("b_sifre_kurali");
        return;
      }
      try {
        await api.auth.resetPassword({ token: tk, new_password: p1 });
        document.getElementById("dvsAuthGovde").innerHTML =
          ceviri("b_sifre_guncellendi");
        // Sunucu sifre degisince TUM oturumlari kapatiyor (InvalidateAllForCustomerAsync);
        // istemcideki bayat access token da atilmali - aksi halde kullanici 15 dakika boyunca
        // "girisli" gorunup her korumali cagrida 401 yerdi.
        try { api.setAccessToken(null); api.setRefreshToken(null); } catch (_) { }
        window.loggedIn = false;
      } catch (e) { er.textContent = e.message || ceviri("b_sifre_guncellenemedi"); }
    };
  }

  async function dogrulaEkrani(token) {
    authKutusu(ceviri("b_eposta_dogrulama"));
    var govde = document.getElementById("dvsAuthGovde");
    if (!token) { govde.textContent = ceviri("b_dogrulama_kodu_yok"); return; }
    govde.textContent = ceviri("b_dogrulaniyor");
    try {
      await api.auth.verifyEmail(token);
      govde.innerHTML = ceviri("b_epostan_dogrulandi");
    } catch (e) {
      document.getElementById("dvsAuthErr").textContent = e.message || ceviri("b_dogrulama_basarisiz");
      govde.innerHTML = ceviri("b_kod_gecersiz") +
        "\"Tekrar gönder\" ile yeni kod isteyebilirsin.";
    }
  }

  function ozelAuthRotasi() {
    var h = location.hash.replace(/^#\/?/, "").split("?")[0].split("/");
    // BASLIK BURADA SET EDILIYOR - SPRINT 8 MADDE 12'NIN AYNI TUZAGI, BU DALGADA OLCULDU:
    // ekran DOGRU cizildigi halde sekme basligi "Sayfa Bulunamadi · Divisima" kaliyordu, cunku
    // index.html'in setDocTitle() fonksiyonunun bu yollar icin dali YOK ve sarmalayici orijinal
    // router'a devretmediginde setDocTitle hic cagrilmiyor. Paylasilan/yer imine eklenen bir
    // sifirlama baglantisinin "Sayfa Bulunamadi" gorunmesi, kullaniciya linkin BOZUK oldugunu
    // soyler - oysa sayfa calisiyor.
    if (h[0] === "dogrula") {
      document.title = ceviri("b_eposta_dogrulama_baslik");
      dogrulaEkrani(decodeURIComponent(h[1] || ""));
      return true;
    }
    if (h[0] === "sifre-sifirla") {
      document.title = ceviri("b_yeni_sifre_baslik");
      sifreSifirlaEkrani(decodeURIComponent(h[1] || ""));
      return true;
    }
    return false;
  }

  function wireSifreVeDogrulama() {
    if (typeof window.router === "function" && !window.router.__dvsAuthWrapped) {
      var origRouter = window.router;
      var sarmal = function () {
        // Ozel rotalar ONCE: aksi halde bilinmeyen yol show404'e duser.
        if (ozelAuthRotasi()) {
          if (typeof window.setNavActive === "function") window.setNavActive();
          return;
        }
        return origRouter.apply(this, arguments);
      };
      sarmal.__dvsAuthWrapped = true;
      window.router = sarmal;
    }

    // "Sifremi unuttum" - index.html'deki href="#" olu link. DELEGE dinleyici kullaniliyor:
    // baglanti auth ekrani her cizildiginde YENIDEN olusuyor, dogrudan onclick baglamak
    // ikinci cizimde kaybolurdu. DALGA 4 / M10 dersi: hedef ALT ELEMAN olabilir -> closest.
    if (!document.__dvsForgotWired) {
      document.__dvsForgotWired = true;
      document.addEventListener("click", function (e) {
        var a = e.target && e.target.closest ? e.target.closest('[data-i18n="forgot"]') : null;
        if (!a) return;
        e.preventDefault();
        var em = document.getElementById("lgEmail");
        sifremiUnuttumEkrani(em ? (em.value || "").trim() : "");
      });
    }

    // ILK YUKLEME YARISI: index.html'in router'i sayfa ayristirilirken (DOMContentLoaded'dan
    // ONCE) bir kez kosuyor, yani sarmalama devreye girmeden show404 cizilmis olabilir.
    // E3/M12'de olculen ayni yaris; cozum de ayni - bir kez daha ciz.
    ozelAuthRotasi();
  }

  // ══ LAUNCH-FIX A4 - TEK PARA BIRIMI (TRY) ══════════════════════════════════════════════════
  //
  // KULLANICI KARARI: launch'ta tek para birimi TRY. Secici KALDIRILMADI, GIZLENDI (ileride
  // gercek bir kur servisiyle geri gelecek).
  //
  // OLCULEN ONCE-DURUM: index.html'de  var CUR={TRY:{rate:1},EUR:{rate:53.2},USD:{rate:46.6}}
  // - kurlar KAYNAGA GOMULU sabitlerdi. tl(n) non-TRY'de  sym + (n/rate)  donduruyordu.
  // Buna karsilik api-bridge.js'in cizdigi ekranlar (odeme paneli, siparis listesi, faturalar)
  // tl() fonksiyonunu HIC KULLANMIYORDU (olculdu: 0 cagri) ve ham TRY basiyordu. Backend ise
  // her kosulda TRY tahsil ediyor (order.currency ?? "TRY", Iyzico para birimi dogrulamasi).
  // Yani USD secili bir kullanici vitrinde "$X", odeme panelinde TRY tutar goruyordu.
  //
  // BU DALGADA: kur tablosu index.html'de TRY'ye indirildi (o dosyadaki degisiklik) ve burada
  // asagidaki iki sey yapiliyor:
  //   1) secici gizlenir (markup DURUYOR),
  //   2) bu dosyanin IKI para bicimleyicisi (money, paraTL) index.html'in tl() fonksiyonuna
  //      DELEGE eder -> "fiyat bicimi tek kaynaktan" sarti saglanir.
  function wireParaBirimi() {
    // 1) Secici gizlenir. Kaldirmiyoruz: markup ileride gercek kur servisiyle geri acilacak.
    ["curbox", "curSelect"].forEach(function (id) {
      var el = document.getElementById(id);
      if (!el) return;
      el.hidden = true;
      el.style.display = "none";
      el.setAttribute("aria-hidden", "true");
      // Klavye ile odaklanip degistirilebilmesin (display:none zaten engeller; select icin
      // disabled ayrica form davranisini de kapatir).
      if (el.tagName === "SELECT") el.disabled = true;
    });
    // Secicinin sarmalayici etiketi de gizlenmeli - aksi halde "Para birimi" basligi bos kutuyla
    // kalirdi (olculdu: #curSelect bir <label> icinde).
    var sel = document.getElementById("curSelect");
    if (sel && sel.closest) {
      var lbl = sel.closest("label");
      if (lbl) { lbl.hidden = true; lbl.style.display = "none"; }
    }
    // 2) Eski oturumlardan kalan secim temizlenir: kullanicinin localStorage'inda "USD" varsa
    //    index.html'in okuma guard'i (CUR[_sc]) zaten reddediyor, ama kaydi birakmak ileride
    //    secici geri acildiginda sessizce USD'ye donmek demekti.
    try { localStorage.removeItem("dvs_cur"); } catch (_) { }
  }

  async function init() {
    wireCoupon();
    wireAuth();
    wireCheckout();
    wireSearch();
    wireProductDetail();
    wireCart();               // E2: sepet mutasyonlarini sunucuya aynala
    wireCheckoutRouting();    // E2: #/odeme ve #/odeme/sonuc
    // E3: Hesabim ekranlari gercek API'ye baglandi - E2b'deki gecici wireAccountOrders
    // (yalniz cokmeyi kaldiran notr durum) ARTIK GEREKSIZ, yerini tam ekran aldi.
    wireAccount();            // E3 (a): ozet + siparisler/zaman cizelgesi + iade + fatura + adres
    wireLegal();              // E3 (b): #/sozlesme icerigi content/get/{slug}'dan + DOMPurify
    wireNotify();             // E3 (d): stok / fiyat dususu abonelikleri gercek uclara
    wireUrunRotasi();         // Sprint 8 madde 12: paylasim baglantilarinin basligi
    wireSifreVeDogrulama();   // LAUNCH-FIX A2 + A1(c): sifremi unuttum / sifre-sifirla / dogrula
    wireParaBirimi();         // LAUNCH-FIX A4: tek para birimi TRY
    wireFavoriler();          // MFIX-3 / F-M5: kalp -> sunucu (misafirde giris yonlendirmesi)
    // Kategoriler ÖNCE: ürün kategorisi category_id üzerinden çözülüyor (liste yolu
    // category_name döndürmüyor), yükleme sırası ters olursa tüm ürünler "tumu" olur.
    await loadCategories();

    // TAKSONOMI: menu veritabanindan uretilir + taninmayan rota artik 404'e duser.
    // `loadCategories`ten HEMEN SONRA - ek istek YOK, ayni yanittan uretiliyor.
    // Rota sarmalayicisi ONCE baglanir ki acilistaki router cagrisi da dogru davransin.
    taksonomiRotasiniBagla();
    menuyuVeritabanindanKur();
    kategoriRotasiniTazele();   // ilk yukleme yarisi (gerekce fonksiyonun basinda)

    await loadCatalog();

    // D3: gercek sayfalama. `renderCatGrid` KATALOG YUKLENDIKTEN SONRA sarmalanir -
    // index.html o fonksiyonu kendi kurulumunda tanimliyor ve daha once sarmalamak
    // tanimlanmamis bir fonksiyonu sarmalamak olurdu (sessizce etkisiz kalirdi).
    sayfalamayiBagla();
    sayfalamaDugmesiniTazele();

    // MFIX-3 / F-M4: sepet geri yuklemesi KATALOG GELDIKTEN SONRA gercek PRODUCTS'a
    // karsi bir kez daha kosar (idempotent - ayni anahtarlari yeniden yazar) ve
    // katalogda olmayan sepet urunleri TEK TEK cekilip eklenir. Sepet bossa istek YOK.
    try { if (typeof window.sepetiGeriYukle === "function") window.sepetiGeriYukle(); } catch (e) {}
    await sepetUrunleriniTamamla();

    // MFIX-3 / F-M5: favoriler SUNUCUDAN. Katalogdan SONRA cagrilir - donen kayitlar
    // PRODUCTS'a eklenir ve index.html'in byId'ye dayanan cizicileri calisir.
    await favorileriSunucudanCek();

    // KATALOG SONRASI YENIDEN CIZIM (E3 elle dogrulamasinda OLCULDU):
    // Hesabim ekranindaki "Favorilerim" ve "Kayitli Kartlar" sekmeleri index.html in
    // KENDI cizicilerini (cardHTML -> byId) kullanir, yani KATALOGA baglidir. Katalog
    // asenkron yuklendigi icin sayfa dogrudan #/hesabim/favorilerim ile acildiginda
    // ekran MOCK urunle ciziliyordu (olculdu: favori id 2 icin "Yumusak Triko Kazak /
    // 649 TL" gorundu; gercek katalogda id 2 = "E4a Test Urun / 499,90 TL").
    // wireAccount icindeki ilk yukleme yamasi BU YARISI KAPATMIYOR - o, katalog
    // gelmeden ONCE kosuyor. Katalog geldikten SONRA bir kez daha cizeriz.
    // Sprint 8 madde 12: katalog geldikten SONRA urun rotasini tazele (yaris kapatilir) ve
    // basligi duzelt. Acilistaki router cagrisi PRODUCTS henuz gercek degilken kosuyor.
    urunRotasiniTazele();

    if (location.hash.indexOf("#/hesabim") === 0 && typeof window.renderAccount === "function") {
      var _t = location.hash.replace(/^#\/?/, "").split("?")[0].split("/")[1] || "ozet";
      window.renderAccount(_t);
    }
  }

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", init);
  } else {
    init();
  }
})();
