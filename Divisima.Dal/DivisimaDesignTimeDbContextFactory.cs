using System;
using System.IO;
using Divisima.DataAccess.Concrete.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Divisima.Dal
{
    // === TASARIM ZAMANI DbContext FABRIKASI (D-SEMA-FIX EKI) =============================
    //
    // NEDEN VAR - OLCULDU, VARSAYILMADI:
    // Bir fabrika YOKKEN `dotnet ef ...` komutlari DbContext'i elde etmek icin BASLANGIC
    // PROJESININ HOST'UNU calistirir - yani `Program.cs`'in fail-fast blogunu da. O blok
    // `ConnectionStrings:DivisimaDb`, `TokenOptions:SecurityKey`, `Encryption:Key` ve
    // `MailSettings:Host` yoksa ISTISNA FIRLATIR. Sonuc: sema ile HICBIR ILGISI OLMAYAN
    // eksik bir JWT anahtari yuzunden migration komutlari duser.
    //
    // BU IKI YERDE BIREBIR YASANDI:
    //   1) CI: yeni "Model ile migration'lar SENKRON mu" adimi `format-check` job'inda
    //      kirildi (exit 1). O job'da secret YOK ve ASPNETCORE_ENVIRONMENT verilmiyor.
    //      Komut YERELDE gecmisti - cunku yerelde user-secrets VARDI. Yani kirilan sey
    //      komut degil, OLCUM ORTAMIYDI.
    //   2) DAHA ONEMLISI - FELAKET KURTARMA: `ops/backup-dr-runbook.md` operatore
    //      `dotnet ef database update` diyor. Fabrika olmadan bu komut, bir SEMA islemi icin
    //      uygulamanin TAM URETIM CONFIG'INI (JWT anahtari dahil) sart kosar. Ayricalikli bir
    //      bastion'da sema kurtarmaya calisan operator, "FATAL: Config - TokenOptions:
    //      SecurityKey eksik" ile karsilasirdi.
    //
    // Fabrika varsa EF host'u HIC calistirmaz; migration araclari uygulama config'inden
    // BAGIMSIZ hale gelir.
    //
    // BAGLANTI DIZGESI COZUM SIRASI (ilk bulunan kazanir):
    //   1) ConnectionStrings__DivisimaDb        - .NET'in standart ortam degiskeni override'i
    //   2) Divisima.API/appsettings.Development.json  (yerel gelistirme)
    //   3) Divisima.API/appsettings.json
    //   4) YER TUTUCU - yalnizca BAGLANMAYAN komutlar icin (migrations add / script /
    //      has-pending-model-changes). `database update` bunu kullanirsa GURULTULU duser;
    //      sessizce yanlis bir veritabanina yazmaz.
    public class DivisimaDesignTimeDbContextFactory : IDesignTimeDbContextFactory<DivisimaDbContext>
    {
        // Bilerek BAGLANILAMAZ: `database update` yanlislikla buraya duserse SQL Server
        // "sunucu bulunamadi" ile patlar. Sessiz basari YOK.
        private const string BaglanmayanYerTutucu =
            "Server=divisima-design-time-yer-tutucu;Database=DivisimaDesignTime;Trusted_Connection=True;TrustServerCertificate=True;";

        public DivisimaDbContext CreateDbContext(string[] args)
        {
            var options = new DbContextOptionsBuilder<DivisimaDbContext>()
                .UseSqlServer(BaglantiCoz())
                .Options;
            return new DivisimaDbContext(options);
        }

        private static string BaglantiCoz()
        {
            var ortam = Environment.GetEnvironmentVariable("ConnectionStrings__DivisimaDb");
            if (!string.IsNullOrWhiteSpace(ortam)) return ortam;

            var apiDizini = ApiDiziniBul();
            if (apiDizini != null)
            {
                var cfg = new ConfigurationBuilder()
                    .SetBasePath(apiDizini)
                    .AddJsonFile("appsettings.json", optional: true)
                    .AddJsonFile("appsettings.Development.json", optional: true)
                    .Build();

                var dosyadan = cfg.GetConnectionString("DivisimaDb");
                // appsettings.json'daki "CHANGE_ME" yer tutucusu GECERLI SAYILMAZ - onu
                // kabul etmek, olmayan bir sunucuya baglanmayi "yapilandirilmis" gostermek olurdu.
                if (!string.IsNullOrWhiteSpace(dosyadan) && !dosyadan.Contains("CHANGE_ME", StringComparison.Ordinal))
                    return dosyadan;
            }

            return BaglanmayanYerTutucu;
        }

        // Cikti dizininden yukari yurunerek Divisima.API bulunur. EF araclarinin calisma
        // dizinine GUVENILMEZ (baslangic projesine gore degisir).
        private static string? ApiDiziniBul()
        {
            var d = new DirectoryInfo(AppContext.BaseDirectory);
            while (d != null)
            {
                var aday = Path.Combine(d.FullName, "Divisima.API");
                if (File.Exists(Path.Combine(aday, "appsettings.json"))) return aday;
                d = d.Parent;
            }
            return null;
        }
    }
}
