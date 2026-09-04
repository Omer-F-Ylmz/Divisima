using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Divisima.Entity.Dtos.Coupon;
using Divisima.Entity.Dtos.Order;
using Divisima.Entity.Dtos.Product;
using Divisima.Entity.Dtos.Return;
using Divisima.Entity.Dtos.Shipping;
using FluentAssertions;
using Xunit;

namespace Divisima.IntegrationTests
{
    // ══ DALGA B / B1 - ADMIN PANELI <-> DTO ALAN ADI SOZLESMESI ═══════════════════════════
    //
    // OLCULEN ZARAR (canli, panelden - hepsi bu dalgada birebir uretildi):
    //
    //  KUPON (uc parca):
    //   (a) Panel "discount_value" gonderiyordu, CouponAddRequestDto alani "value". Operator
    //       %30 girdi -> DB'ye value=.00 yazildi, uc HTTP 201 dondu, panel "Kupon eklendi"
    //       dedi ve musteri 1000 TL sepette "Kupon gecerli." + discount_amount 0.00 gordu.
    //       HER KATMAN BASARILI DIYORDU; indirim yoktu. Sessizligin sebebi: bilinmeyen JSON
    //       alani model binding tarafindan sessizce ATILIR.
    //   (b) Liste "c.discount_value" okuyordu -> undefined -> her satirda "-".
    //   (c) Liste "discount_type"i SAYIYLA karsilastiriyordu; uc ENUM ADINI (metin) doner.
    //       "Percentage"==0 ve "Percentage"==1 ikisi de false -> UCUNCU dala dusuyor ve
    //       HER kupon "Kargo" gorunuyordu. Bu eksik bilgi degil YANLIS bilgidir.
    //
    //  URUN: panel "stocks" ve "color_hex" gondermiyordu (formda O ALANLAR YOKTU) - ikisi de
    //       zorunlu. Operatore ham cerceve mesaji dusuyordu: "The stocks field is required."
    //       Yani panelden urun eklemek/duzenlemek MUMKUN DEGILDI.
    //
    //  SIPARIS: uc { items, totalCount, ... } donerken panel "Items"/"TotalCount" okuyordu ->
    //       veritabaninda 52 siparis varken ekran "Siparisler (0) / Siparis yok" gosteriyordu.
    //       AYNI oturumda Panel sekmesi "SIPARIS 52" diyordu - panel kendi kendisiyle celisti.
    //
    // BU DOSYANIN ISI: ayni SINIF hatanin sessizce geri gelmemesi. 3. pin TEK TEK alan degil,
    // panelin TUM admin yazma ekranlarini tarar - yeni bir ekran DTO'da olmayan bir alan
    // gonderirse KIRILIR.
    //
    // SINIR (durust kayit): depoda JS/DOM kosucusu YOK, dolayisiyla bu pinler KAYNAK
    // SOZLESMESINI tutar, tarayici davranisini degil. Davranis kaniti dalga raporundaki canli
    // olcumlerdedir; uc duzeyindeki karsiligi ise DalgaBOperasyonTests'te (HTTP + SQL).
    public class AdminPanelSozlesmeTests
    {
        private static readonly Lazy<string> KokDizin = new(() =>
        {
            var d = new DirectoryInfo(AppContext.BaseDirectory);
            while (d != null && !File.Exists(Path.Combine(d.FullName, "frontend", "admin.html")))
                d = d.Parent;
            if (d == null)
                throw new InvalidOperationException(
                    "Depo koku bulunamadi: frontend/admin.html iceren ust dizin yok. " +
                    "Sessiz skip YOK - bu pinler kaynagi okuyamadan yesil kalamaz.");
            return d.FullName;
        });

        private static string Oku(string goreliYol)
        {
            var tam = Path.Combine(KokDizin.Value, goreliYol.Replace('/', Path.DirectorySeparatorChar));
            File.Exists(tam).Should().BeTrue($"pinlenen kaynak dosya bulunmali: {goreliYol}");
            return File.ReadAllText(tam);
        }

        // ══ GF-2b / K5 - PANEL KAYNAGI ARTIK IKI DOSYA ═════════════════════════════════
        // Panelin JS'i `admin.html` icinde SATIR ICI bir <script> blogundaydi ve bu, admin
        // CSP'sinde `script-src 'unsafe-inline'`i ZORUNLU kiliyordu. GF-2b/K5'te blok
        // `frontend/admin.js`e tasindi ve `'unsafe-inline'` KALDIRILDI.
        //
        // Bu pinlerin OLCTUGU SEY DEGISMEDI - yalnizca kaynagin yeri degisti. Panel bir
        // BUTUN olarak okunur; boylece markup pinleri (script sirasi, CSP) ve JS pinleri
        // (fonksiyon govdeleri, payload anahtarlari) AYNI kapsamda kalir ve tasima
        // hicbir pinde kapsam KAYBI yaratmaz.
        // SIRA ONEMLI: html ONCE gelir - `IndexOf` ile sira olcen pinler (purify'in
        // api-client'tan once yuklenmesi gibi) bozulmasin.
        private static string PanelKaynagi() =>
            Oku("frontend/admin.html") + "\n" + Oku("frontend/admin.js");

        // Bir JS fonksiyonunun govdesini kaba ama YETERLI bicimde cikarir: adindan baslar,
        // suslu parantezleri sayarak kapanisa kadar gider. Regex ile "govdeyi tahmin etmek"
        // yerine sayma kullaniliyor - ic ice objeler yuzunden regex sessizce yanlis kesebilir.
        private static string FonksiyonGovdesi(string kaynak, string fonksiyonAdi)
        {
            var i = kaynak.IndexOf("function " + fonksiyonAdi, StringComparison.Ordinal);
            i.Should().BeGreaterThan(-1, $"'{fonksiyonAdi}' fonksiyonu admin.html'de bulunmali");
            var acilis = kaynak.IndexOf('{', i);
            acilis.Should().BeGreaterThan(-1);
            var derinlik = 0;
            for (var j = acilis; j < kaynak.Length; j++)
            {
                if (kaynak[j] == '{') derinlik++;
                else if (kaynak[j] == '}')
                {
                    derinlik--;
                    if (derinlik == 0) return kaynak.Substring(acilis, j - acilis + 1);
                }
            }
            throw new InvalidOperationException($"'{fonksiyonAdi}' govdesinin kapanisi bulunamadi.");
        }

        // Nesne literalinde ANAHTAR gibi gorunen ama anahtar OLMAYAN tek kalip: ucluk operatorun
        // ikinci yarisi ("... ? null : parseFloat(x)"). JS anahtar sozcukleri anahtar olamaz,
        // bu yuzden onlari acikca eliyoruz. Baska bir suzgec YOK - aksi halde tarama gercekten
        // gonderilen bir alani gozden kacirabilirdi ve pin SESSIZCE zayiflardi.
        private static readonly HashSet<string> JsAnahtarSozcukleri =
            new(StringComparer.Ordinal) { "null", "true", "false", "undefined" };

        // Govdedeki "payload={...}" / "const payload={...}" nesnesinin ANAHTARLARINI cikarir.
        private static HashSet<string> PayloadAnahtarlari(string govde)
        {
            var p = govde.IndexOf("payload", StringComparison.Ordinal);
            p.Should().BeGreaterThan(-1, "yazma ekraninin govdesi 'payload' adli bir nesne kurmalı");
            var acilis = govde.IndexOf('{', p);
            acilis.Should().BeGreaterThan(-1);
            var derinlik = 0; var son = -1;
            for (var j = acilis; j < govde.Length; j++)
            {
                if (govde[j] == '{') derinlik++;
                else if (govde[j] == '}') { derinlik--; if (derinlik == 0) { son = j; break; } }
            }
            son.Should().BeGreaterThan(acilis);
            var literal = govde.Substring(acilis, son - acilis + 1);

            return new HashSet<string>(
                Regex.Matches(literal, @"(?<![\w.$])([A-Za-z_][A-Za-z0-9_]*)\s*:")
                     .Select(m => m.Groups[1].Value)
                     .Where(k => !JsAnahtarSozcukleri.Contains(k)),
                StringComparer.Ordinal);
        }

        private static HashSet<string> DtoAlanlari(Type t) =>
            new(t.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => p.Name), StringComparer.Ordinal);

        // ── 1) KUPON EKLEME ────────────────────────────────────────────────────────────────
        [Fact]
        public void KuponEklemeGovdesi_DTO_ALANLARIYLA_ORTUSUR_discount_value_ARTIK_YOK()
        {
            var govde = FonksiyonGovdesi(PanelKaynagi(), "saveCoupon");
            var anahtarlar = PayloadAnahtarlari(govde);

            // VAKUM KIRICI: taramanin gercekten bir sey bulmus olmasi. Bos bir kume her
            // "icermez" assertini bedavaya gecerdi.
            anahtarlar.Should().NotBeEmpty("kupon gövdesinden en az bir alan çıkarılmalı");

            anahtarlar.Should().Contain("value", "DTO alani 'value' - indirim degeri BUNUNLA tasinir");
            anahtarlar.Should().NotContain("discount_value",
                "olculen kusur buydu: DTO'da olmayan bu ad sessizce atiliyor ve kupon 0 indirimle kaydediliyordu");

            var dto = DtoAlanlari(typeof(CouponAddRequestDto));
            anahtarlar.Should().BeSubsetOf(dto,
                "panelin gonderdigi HER alan DTO'da bulunmali; bulunmayan alan model binding tarafindan SESSIZCE atilir");
        }

        // ── 2) KUPON LISTESI ───────────────────────────────────────────────────────────────
        [Fact]
        public void KuponListesi_DTO_ALANINI_OKUR_ve_TIPI_METIN_OLARAK_COZER()
        {
            var kaynak = PanelKaynagi();
            var govde = FonksiyonGovdesi(kaynak, "renderCoupons");

            govde.Should().NotContain("c.discount_value",
                "liste ucu 'value' doner; 'discount_value' okumak her satirda '-' gosteriyordu");
            govde.Should().Contain("kuponDegerMetni(c)", "deger tek bir merkezden bicimlenmeli");
            govde.Should().Contain("kuponTipEtiket(c.discount_type)", "tip tek bir merkezden cozulmeli");

            // CIFT-ANLAM KIRICI: "tip etiketini merkeze tasidim" demek yetmez - merkezin METIN
            // gosterimini GERCEKTEN tanidigi da dogrulanmali. Uc, enum ADINI doner.
            var merkez = kaynak.Substring(kaynak.IndexOf("const KUPON_TIPI", StringComparison.Ordinal), 400);
            merkez.Should().Contain("Percentage").And.Contain("Fixed").And.Contain("FreeShipping",
                "liste ucu ((DiscountTypeEnum)x).ToString() doner; merkez bu adlari tanimazsa her kupon yine yanlis etiket alir");

            // Eski, sayisal karsilastirmaya dayanan kalip GERI GELMEMELI.
            govde.Should().NotContain("discount_type==0")
                 .And.NotContain("discount_type == 0",
                    "metin donen bir alani sayiyla karsilastirmak HER kuponu 'Kargo' gosteriyordu");
        }

        // ── 3) SINIF DUZEYI TARAMA ─────────────────────────────────────────────────────────
        // B1'in ASIL istegi: "alan adi uyusmazligi bir daha SESSIZ kalmasin". Tek tek alan
        // pinlemek yeni eklenen bir ekrani kapsamaz; bu yuzden tarama EKRAN BAZLI.
        // YENI BIR ADMIN YAZMA EKRANI EKLENIRSE buraya bir satir eklenmeli - eklenmezse
        // asagidaki KAPSAM PINI kirilir (yani liste sessizce eskiyemez).
        public static IEnumerable<object[]> YazmaEkranlari() => new List<object[]>
        {
            new object[] { "saveCoupon",     typeof(CouponAddRequestDto)  },
            new object[] { "saveProduct",    typeof(ProductUpdateRequestDto) },  // Add'in ustkumesi (yalnizca id fazla)
            new object[] { "createShipment", typeof(ShipmentCreateDto)    },
        };

        [Theory]
        [MemberData(nameof(YazmaEkranlari))]
        public void HicbirAdminYazmaEkrani_DTO_DA_OLMAYAN_ALAN_GONDERMEZ(string fonksiyon, Type dtoTipi)
        {
            var anahtarlar = PayloadAnahtarlari(FonksiyonGovdesi(PanelKaynagi(), fonksiyon));

            // VAKUM KIRICI: cikarim gercekten calismis olmali.
            anahtarlar.Should().NotBeEmpty($"'{fonksiyon}' govdesinden alan cikarilamadi - tarama VAKUMA dusmus olurdu");

            var dto = DtoAlanlari(dtoTipi);
            var fazlalik = anahtarlar.Except(dto, StringComparer.Ordinal).ToList();
            fazlalik.Should().BeEmpty(
                $"'{fonksiyon}' su alanlari gonderiyor ama {dtoTipi.Name} bunlari TANIMIYOR: {string.Join(", ", fazlalik)}. " +
                "Bilinmeyen alan model binding tarafindan SESSIZCE atilir - kupon ekraninda tam bu yuzden 0 indirimli kupon uretiliyordu.");
        }

        // ── 3b) KAPSAM PINI ────────────────────────────────────────────────────────────────
        // Yukaridaki tarama ancak LISTESI GUNCEL kaldigi surece korur. Bu pin, panelde GOVDE
        // gonderen her admin yazma cagrisinin listede karsiligi oldugunu dogrular; yeni bir
        // ekran eklenip listeye yazilmazsa KIRILIR. Yoksa tarama sessizce eskirdi.
        [Fact]
        public void ADMIN_YAZMA_CAGRILARININ_TAMAMI_TARAMA_KAPSAMINDA()
        {
            var kaynak = PanelKaynagi();

            // Govde GONDEREN cagrilar: api.admin.<x>(payload). Kimlikle cagrilanlar (deleteCoupon(id),
            // changeOrderStatus(id,status), generateInvoice(id)) govde kurmaz - alan adi uyusmazligi
            // uretemezler, kapsam disi olmalari DOGRUDUR.
            var govdeliCagrilar = Regex.Matches(kaynak, @"api\.admin\.(\w+)\(payload\)")
                                       .Select(m => m.Groups[1].Value)
                                       .Distinct(StringComparer.Ordinal)
                                       .ToList();

            // VAKUM KIRICI: tarama gercekten cagri bulmus olmali.
            govdeliCagrilar.Should().NotBeEmpty("admin.html'de govde gonderen en az bir admin cagrisi olmali");

            var kapsanan = YazmaEkranlari().Select(x => (string)x[0]).ToList();
            var kapsamDisi = govdeliCagrilar
                .Where(c => !kapsanan.Any(f => FonksiyonGovdesi(kaynak, f).Contains("api.admin." + c + "(payload)", StringComparison.Ordinal)))
                .ToList();

            kapsamDisi.Should().BeEmpty(
                $"su admin yazma cagrilari alan-adi taramasinin DISINDA: {string.Join(", ", kapsamDisi)}. " +
                "YazmaEkranlari listesine eklenmeleri gerekir - aksi halde B1'deki sessiz uyusmazlik sinifi geri gelir.");
        }

        // ── 4) URUN FORMU: ZORUNLU ALANLAR ─────────────────────────────────────────────────
        [Fact]
        public void UrunFormu_ZORUNLU_ALANLARI_GONDERIR_color_hex_ve_stocks()
        {
            var anahtarlar = PayloadAnahtarlari(FonksiyonGovdesi(PanelKaynagi(), "saveProduct"));

            anahtarlar.Should().Contain("color_hex", "zorunlu (schema NOT NULL); yokken uc 'The color_hex field is required.' donuyordu");
            anahtarlar.Should().Contain("stocks", "zorunlu; yokken uc 'The stocks field is required.' donuyordu");

            // GUNCELLEMEDE SESSIZ SILINME KAPISI: ProductManager.Update TAM-VARLIK map yapar,
            // yani DTO'da BULUNAN ama gonderilmeyen alan MEVCUT DEGERI EZER. Bu uc alan da
            // DTO'da vardir; formun onlari tasimasi "duzenlerken indirim/renk/tip kayboldu"
            // sinifini kapatir.
            anahtarlar.Should().Contain("sale_price");
            anahtarlar.Should().Contain("old_price");
            anahtarlar.Should().Contain("product_type");
        }

        // ── 5) SIPARIS ZARFI: TEK KONVANSIYON ──────────────────────────────────────────────
        [Fact]
        public void SiparisListesiZarfi_TEK_KONVANSIYON_Sunucu_ve_Panel_AYNI_ADLARI_Kullanir()
        {
            // Sunucu tarafi: repo tipi (PagedResult<T>) ARTIK HTTP yanitina konmuyor.
            var dto = DtoAlanlari(typeof(AdminOrderPagingListResponseDto));
            dto.Should().Contain("items").And.Contain("total_count").And.Contain("total_pages");
            dto.Should().NotContain("TotalCount", "zarf snake_case olmali - deponun diger sayfali uclariyla ayni");

            // Deponun DIGER sayfali zarfiyla BIREBIR ayni alan seti olmali; iki konvansiyon
            // tam olarak bu yuzden olusmustu.
            dto.Should().BeEquivalentTo(DtoAlanlari(typeof(ProductPagingListResponseDto)),
                "sayfalama zarfi TEK konvansiyona sahip olmali");

            // Panel tarafi: eski PascalCase okumasi geri gelmemeli.
            var govde = FonksiyonGovdesi(PanelKaynagi(), "renderOrders");
            govde.Should().Contain("res.items").And.Contain("res.total_count");
            govde.Should().NotContain("res.Items").And.NotContain("res.TotalCount",
                "olculen kusur buydu: 52 siparis varken ekran 'Siparis yok' gosteriyordu");

            // CIFT-ANLAM KIRICI: sessiz catch geri gelmemeli. Zarf adlari duzelse bile
            // "allOrders(...).catch(()=>({items:[]}))" 401/500'u yutup yine BOS TABLO gosterirdi
            // ve "siparis yok" ile "uc patladi" ayirt edilemezdi.
            // ARAMA CAGRI YERINE BAKAR, duz metne DEGIL: ilk yazimda duz "\.catch\(\(\)=>" araniyordu
            // ve pin, kaldirilmis kalibi ALINTILAYAN kendi aciklama yorumuna takildi (yanlis kirmizi).
            Regex.IsMatch(govde, @"allOrders\s*\([^)]*\)\s*\.catch").Should().BeFalse(
                "hata yutulmamali - render()'in ortak hata dali GORUNUR mesaj yaziyor");
        }
    }
}
