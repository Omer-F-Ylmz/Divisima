namespace Divisima.Core.Utilities.Stock
{
    // Açıklayıcı yorum: SATILABİLİR STOK - tek doğruluk kaynağı.
    //
    // NEDEN VAR: aynı formül (stock_quantity - reserved_quantity) ProductManager'ın İKİ AYRI
    // yolunda birbirinden bağımsız yazılmıştı ve AYRIŞMIŞTI: liste yolu satılabilir değeri
    // döndürürken (Sprint 8 madde 5), detay yolu FİZİKSEL stoğu döndürüyordu. Ölçüldü (ürün 937):
    // detay S=12/M=10/L=11 (toplam 33), liste total_stock=26 - fark tam olarak 7 rezerve adet.
    // Yani "aynı sınıfta iki stok tanımı" vardı. Depoda bu sınıf hata (aynı kuralın ikinci kopyası)
    // defalarca bedelini ödetti; bu yüzden formül TEK bir yere alındı.
    //
    // NEDEN PRIMITIVE İMZA: Divisima.Core'un HİÇBİR ProjectReference'ı yok, yani ProductStock
    // entity'sini GÖREMEZ. PricingHelper/MoneyHelper ile aynı idiyom: saf static + primitive.
    //
    // SINIR (dürüst kayıt): Divisima.Dal/Concrete/EfProductStockDal içindeki İKİ kullanım bir
    // EF expression-tree'sidir ve SQL'e çevrilir - ortak bir C# metoduna ÇEKİLEMEZ. StockManager
    // ve SearchManager'daki bellek-içi kullanımlar bu dalgada kapsam dışıdır (bkz. rapor/ÖNERİ).
    public static class StokHesabi
    {
        // Açıklayıcı yorum: Rezerve düşülmüş satılabilir adet. NEGATİFE İZİN YOK - rezervasyon
        // fiziksel stoğu aşmış olsa bile (veri sapması) "eksi stok" satılabilir gibi okunamaz.
        public static int Satilabilir(int stockQuantity, int reservedQuantity)
        {
            var kalan = stockQuantity - reservedQuantity;
            return kalan > 0 ? kalan : 0;
        }
    }
}
