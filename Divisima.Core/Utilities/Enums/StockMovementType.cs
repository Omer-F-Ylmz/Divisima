namespace Divisima.Core.Utilities.Enums
{
    // Açıklayıcı yorum: Stok hareket yönü (Cafixo StockMovement kalıbı). StockMovement.movement_type (byte).
    public enum StockMovementType
    {
        In = 1,   // Giriş (iade/iptal)
        Out = 2,  // Çıkış (sipariş)
        Adjustment = 3 // Admin düzeltme (sevkiyat/sayım)
    }
}
