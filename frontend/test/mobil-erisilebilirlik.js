/* ══ DALGA 4 - MOBIL ERISILEBILIRLIK OLCUM BETIGI (M10 + M11 + M3) ═══════════════════════
 *
 * NEDEN VAR: bu depoda JS/DOM test kosucusu YOK (Divisima.IntegrationTests'te AngleSharp /
 * Jint / Playwright bulunmuyor - olculdu). Bu yuzden .NET tarafindaki pinler
 * (FrontendDokunmaHedefiTests) yalniz KAYNAK SOZLESMESINI tutabiliyor: "handler hedefi
 * closest ile cozer", "cerez bari kendi alanindadir". TARAYICI SEMANTIGI - hit-test, CSS
 * ozgullugu, elementFromPoint - orada dogrulanamaz.
 *
 * Bu dosya o boslugu KAPATMAZ, ama olcumu TEKRARLANABILIR kilar: kaniti yeniden uretmek
 * icin ekran goruntusune ya da hatirlamaya degil, kosulabilir bir betige bakilir.
 *
 * KULLANIM (tarayici konsolu):
 *   1. Storefront'u ac, CEREZ BARI ACIK olsun (onay verilmisse: localStorage'daki
 *      'dvs_cookie' anahtarini silip sayfayi yenile).
 *   2. Sepette EN AZ BIR urun bulunsun (aksi halde #checkoutBtn cizilmez).
 *   3. Bu dosyanin icerigini konsola yapistir, sonra:  await divisimaMobilKontrol()
 *   4. Olcumu 360x640, 384x638 ve 412x730'da TEKRARLA (cihaz emulasyonu ya da gercek cihaz).
 *
 * BEKLENEN (duzeltme sonrasi, UC VIEWPORT'ta da):
 *   a_GirisYap        GECTI     b_AltNav 4/4 GECTI     c_SepetiOnayla GECTI
 *   d_AltElemanHedefi GECTI     (ripple ink hedefiyle tiklama da odeme rotasina gecer)
 *
 * OLCULEN ONCE-DURUM (duzeltmeden once, kayit icin):
 *   360x640  bar 199-640 h=441  "Giris yap" ORTULU <- div.ck-text        alt nav 0/4
 *   384x638  bar 217-638 h=421  "Giris yap" ORTULU <- span               alt nav 0/4
 *   412x730  bar 326-730 h=404  "Giris yap" ulasilir                     alt nav 0/4
 *   ve gercek cihazda: click hedefi span.ripple-ink -> hash DEGISMIYOR.
 */
(function () {
  function ad(n) {
    if (!n) return 'null';
    var c = (typeof n.className === 'string' && n.className.trim()) ? '.' + n.className.trim().split(/\s+/)[0] : '';
    return n.tagName.toLowerCase() + (n.id ? '#' + n.id : '') + c;
  }

  // Bir ogenin MERKEZINDE gercekten kendisi mi var? Gorunur olmasi YETMEZ - ustunde
  // seffaf/opak bir katman varsa kullanici ona dokunur, bu ogeye DEGIL.
  function ulasilirMi(el) {
    if (!el) return { yok: true };
    var r = el.getBoundingClientRect();
    if (r.width < 1 || r.height < 1) return { gorunmez: true };
    var hit = document.elementFromPoint(Math.round(r.left + r.width / 2), Math.round(r.top + r.height / 2));
    var ok = !!(hit && (hit === el || el.contains(hit)));
    return { top: Math.round(r.top), bottom: Math.round(r.bottom), ulasilir: ok, ustundeki: ok ? null : ad(hit) };
  }

  function bekle(ms) { return new Promise(function (r) { setTimeout(r, ms); }); }

  // CSS gecisleri sekme ARKA PLANDAYKEN DONAR (document.hidden). Olcumden once biten
  // animasyonlari zorlamazsak cekmece "acik konumda takili" gorunur ve yanlis bulgu uretir.
  function gecisleriBitir(el) {
    try { if (el && el.getAnimations) el.getAnimations().forEach(function (a) { try { a.finish(); } catch (e) { } }); } catch (e) { }
  }

  function yaz(o) { return o.yok ? 'OGE YOK' : (o.gorunmez ? 'GORUNMEZ' : (o.ulasilir ? 'GECTI (' + o.top + '-' + o.bottom + ')' : 'KALDI - ustunde ' + o.ustundeki)); }

  window.divisimaMobilKontrol = async function () {
    var sonuc = { viewport: window.innerWidth + 'x' + window.innerHeight };

    var ck = document.getElementById('cookieBar');
    var ckGorunur = ck && getComputedStyle(ck).display !== 'none';
    sonuc.cerezBariAcik = !!ckGorunur;
    if (!ckGorunur) sonuc.UYARI = 'Cerez bari KAPALI - bu olcumun anlami yok. dvs_cookie anahtarini silip yenile.';
    if (ck) { var cr = ck.getBoundingClientRect(); sonuc.cerezBari = Math.round(cr.top) + '-' + Math.round(cr.bottom) + ' h=' + Math.round(cr.height); }
    sonuc.mnavH = getComputedStyle(document.documentElement).getPropertyValue('--mnav-h').trim() || '(yazilmamis)';

    // (a) ODEME sayfasindaki BIRINCIL EYLEM - cikisli kullanicinin TEK cikis yolu.
    location.hash = '#/odeme';
    await bekle(450);
    var giris = [].slice.call(document.querySelectorAll('#checkoutView a.btn,#checkoutView button.btn'))
      .filter(function (b) { return /giri/i.test(b.textContent || ''); })[0];
    sonuc.a_GirisYap = giris ? yaz(ulasilirMi(giris)) : 'GIRISLI KULLANICI (buton yok) - bu kontrol CIKIŞLI kosulmali';

    // (b) ALT NAVIGASYONUN DORT OGESI
    var nav = [];
    [].slice.call(document.querySelectorAll('.mob-nav .mnav-item')).forEach(function (i) {
      if (getComputedStyle(i).display === 'none') return;
      var rr = ulasilirMi(i);
      nav.push(i.getAttribute('data-mnav') + ': ' + (rr.ulasilir ? 'GECTI' : 'KALDI - ustunde ' + rr.ustundeki));
    });
    sonuc.b_AltNav = nav;

    // (c) SEPET "Sepeti Onayla"
    try { openCart(); } catch (e) { sonuc.sepetHatasi = String(e); }
    await bekle(420);
    gecisleriBitir(document.getElementById('cart'));
    await bekle(80);
    var cb = document.getElementById('checkoutBtn');
    sonuc.c_SepetiOnayla = cb ? yaz(ulasilirMi(cb)) : 'BUTON YOK - sepet bos, once bir urun ekle';

    // (d) M10: tiklama hedefi butonun ALT ELEMANI oldugunda da eylem calismali.
    // Gercek dokunusta ripple ink tam olarak bunu yapiyor; sentetik .click() yapmaz -
    // bu yuzden ink'i ELLE uretip hedefi ONA veriyoruz.
    if (cb) {
      location.hash = '#/';
      await bekle(180);
      var ink = document.createElement('span');
      ink.className = 'ripple-ink';
      cb.appendChild(ink);
      sonuc.inkPointerEvents = getComputedStyle(ink).pointerEvents; // 'none' olmali
      ink.dispatchEvent(new MouseEvent('click', { bubbles: true, cancelable: true }));
      sonuc.d_AltElemanHedefi = (location.hash === '#/odeme')
        ? 'GECTI (hash #/odeme)'
        : 'KALDI - hash DEGISMEDI (' + location.hash + ')';
      if (ink.parentNode) ink.remove();
    }

    try { closeCart(); } catch (e) { }
    location.hash = '#/';
    return sonuc;
  };

  if (window.console && console.info) console.info('divisimaMobilKontrol() hazir - "await divisimaMobilKontrol()" ile kosur.');
})();
