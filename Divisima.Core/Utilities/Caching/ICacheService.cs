namespace Divisima.Core.Utilities.Caching
{
    // Açıklayıcı yorum: Cache soyutlaması (IMemoryCache -> ileride Redis'e geçiş kolay olsun diye).
    // Cache-aside deseni: GetOrSetAsync ile "önce cache, yoksa üret+yaz".
    public interface ICacheService
    {
        Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? ttl = null);

        // ══ GF-1 / K2 - SALT-OKUMA VARLIK SORGUSU (merkez onayi) ═══════════════════════════
        //
        // NEDEN YENI UYE GEREKTI (olculdu): bu arayuzdeki OKUMA yollarinin HEPSI YAZIYORDU.
        // `GetOrSetAsync` cache-aside'dir: anahtar yoksa factory'yi kosar ve SONUCU CACHE'E
        // YAZAR. Kara liste bunu "varsa true, yoksa false" diye kullaniyordu, yani HER
        // KONTROL anahtari `false` ile DOLDURUYORDU; ardindan gelen `RevokeAsync` ayni
        // `GetOrSetAsync`i cagirdigi icin DOLU anahtari EZEMIYOR ve iptal SESSIZ NO-OP
        // oluyordu. `TryAddAsync` de coz(e)mez: zehirli `false` anahtari VARKEN o da
        // `false` doner ve degeri DEGISTIRMEZ.
        //
        // Bu uye HICBIR SEY YAZMAZ - "var mi" sorusunu yan etkisiz yanitlar. Kara listenin
        // okuma yolu buna gecti; boylece okuma artik yazma uretmiyor ve `TryAddAsync`
        // (atomik set-if-not-exists) iptal icin DOGRU primitif haline geldi.
        Task<bool> ExistsAsync(string key);

        // ══ GF-1b / K1 - DEGER YAZ + SALT OKU (merkez onayi) ═══════════════════════════════
        //
        // NEDEN IKI YENI UYE GEREKTI (olculdu): GF-1'de eklenen `ExistsAsync` yalnizca
        // VARLIK sorar - `revoked_before` ise bir ZAMAN DAMGASIDIR ve DEGERI okunmalidir.
        // Mevcut uyelerin hicbiri bunu KALDIRMIYOR:
        //   `GetOrSetAsync` OKURKEN YAZAR  -> GF-1'de olculen ZEHIRLENME sinifi geri gelirdi
        //   `ExistsAsync`   yalniz VARLIK  -> esigi dondurmez
        //   `TryAddAsync`   DEGER ALMAZ    -> zaman damgasi yazilamaz
        // Alternatif `customers` uzerinde KOLON olurdu; o da IKINCI MIGRATION demekti ve
        // GF-1'de reddedilmisti.
        //
        // `SetAsync` her cagrida degeri EZER (set-if-not-exists DEGIL): iptal esigi ileri
        // tasinabilmelidir. `GetAsync` HICBIR SEY YAZMAZ; anahtar yoksa `default` doner.
        Task SetAsync<T>(string key, T value, TimeSpan ttl);
        Task<T?> GetAsync<T>(string key);
        // Aciklayici yorum: ATOMIK set-if-not-exists (Redis SETNX / in-memory lock). true = BU cagri anahtari ekledi
        // (yoktu); false = zaten vardi. "Yalniz ilk kazanir" senaryolari (idempotency, kilit) icin - check-then-act race YOK.
        Task<bool> TryAddAsync(string key, TimeSpan ttl);
        void Remove(string key);
        void RemoveByPrefix(string prefix);
    }
}
