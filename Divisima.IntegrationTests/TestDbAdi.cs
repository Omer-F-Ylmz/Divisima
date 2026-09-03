namespace Divisima.IntegrationTests
{
    // ══ GF-3 / F2 - TEST VERITABANI ADININ TEK URETIM NOKTASI ══════════════════════════════
    //
    // OLCULEN KUSUR (MK-4b, IKI DENETCI BAGIMSIZ BULDU): denetcilere ayri worktree ve ayri
    // `DIVISIMA_TEST_SQL` verildi, ama izolasyon FIILEN CALISMADI. Sebep: her test sinifi
    // kendi SABIT `DbName` sabitini tasiyor ve `SqlConnectionStringBuilder`in
    // `InitialCatalog` alanini onunla EZIYOR - yani baglanti dizesindeki `Database=` parcasi
    // YOK SAYILIYOR. Iki kosucu ayni anda calisinca AYNI veritabanlarina girdiler:
    // `Database 'DivisimaAuthIdorTest' already exists` ile 157-335 SAHTE kirmizi olculdu.
    //
    // NEDEN "TEK VERITABANI"NA GECILMEDI: CLAUDE.md bolum 4 "SINIF BASINA AYRI VERITABANI"
    // diyor ve gerekcesi olculmus - xUnit test SINIFLARINI PARALEL kosar, ortak DB kullanilsa
    // bir sinifin `EnsureDeleted` cagrisi digerinin verisini SILERDI. `DIVISIMA_TEST_SQL`in
    // `Database=` parcasini TUM siniflara vermek o kurali kirardi.
    //
    // COZUM: sinif basina ayrilik KORUNUR, uzerine KOSUCU AD ALANI eklenir. `DIVISIMA_TEST_DB`
    // verildiginde her sinifin adina son ek olarak eklenir:
    //     DivisimaAuthIdorTest        (ana akis - degisken YOK)
    //     DivisimaAuthIdorTest_L3     (DIVISIMA_TEST_DB=L3)
    // Degisken verilmezse davranis BIREBIR ESKISI GIBI kalir - yani CI ve mevcut yerel akis
    // ETKILENMEZ (geriye donuk uyumlu).
    //
    // AD SINIRI: SQL Server veritabani adi 128 karaktere kadar; en uzun sinif adi + son ek
    // bu sinirin cok altinda. Son ek KIMLIK dizgesidir - kulturlu casing YASAK (CLAUDE.md 6c),
    // bu yuzden dokunulmadan (ham) eklenir ve karsilastirma yapilmaz.
    internal static class TestDbAdi
    {
        private static readonly string? Sonek =
            Environment.GetEnvironmentVariable("DIVISIMA_TEST_DB");

        /// <summary>
        /// Sinifin sabit veritabani adina, varsa kosucu ad alani son ekini ekler.
        /// `DIVISIMA_TEST_DB` yoksa ad AYNEN doner.
        /// </summary>
        public static string Cozumle(string sinifDbAdi) =>
            string.IsNullOrWhiteSpace(Sonek) ? sinifDbAdi : sinifDbAdi + "_" + Sonek.Trim();
    }
}
