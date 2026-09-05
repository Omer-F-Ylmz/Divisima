using Divisima.DataAccess.Concrete.Context;
using Divisima.Entity.Entities;
using Microsoft.EntityFrameworkCore;

namespace Divisima.IntegrationTests
{
    // ══ GF-6 / K2 (D2) - TESTLER ICIN TEK ADRES YARDIMCISI ═════════════════════════════════
    //
    // NEDEN VAR: `address_id` bu dalgada ZORUNLU hale geldi (AV-3 / T1-B2 - adressiz siparis
    // LAUNCH BLOKER'di). Zeminde siparis kuran testlerin cogu adres GONDERMIYORDU, cunku
    // uretim kodu onu istemiyordu; kapi kapaninca hepsi 400 aldi.
    //
    // TEK YARDIMCI - UC KOPYA DEGIL: depoda ZATEN IKI ayri `AdresHazirlaAsync` kopyasi vardi
    // (`LaunchFixMailZinciriTests` ve `MisafirCheckoutTests`, HTTP `address/upsert` uzerinden)
    // ve bu dalga UCUNCUSUNU eklemek uzereydi. "Ayni kuralin ikinci kopyasi" ailesi bu depoda
    // yedi kez bedeli odenmis bir hatadir; bu yuzden yardimci TEK YERE konuldu.
    //
    // NEDEN HTTP DEGIL DOGRUDAN DB: bu yardimciyi cagiran testlerin bir kismi `IOrderService`i
    // DOGRUDAN cozuyor (HTTP istemcisi YOK). Mevcut iki HTTP kopyasi DEGISTIRILMEDI - onlar
    // `address/upsert` ucunun kendi sozlesmesini de olcuyor; buradaki yardimci yalnizca
    // ON KOSUL uretir, uc davranisi OLCMEZ.
    public static class TestAdresHelper
    {
        public static async Task<int> AdresOlusturAsync(DivisimaDbContext ctx, int musteriId,
            string sehir = "Istanbul", string ilce = "Kadikoy")
        {
            var adres = new Address
            {
                customer_id = musteriId,
                title = "Teslimat",
                full_name = "Test Alici",
                phone = "5550000000",
                city = sehir,
                district = ilce,
                full_address = "Test Mah. Test Sok. No 1",
                zip_code = "34710",
                is_default = true,
                is_active = true,
                created_at = DateTime.Now
            };
            ctx.Set<Address>().Add(adres);
            await ctx.SaveChangesAsync();
            return adres.id;
        }

        // Baglanti dizgesinden KENDI context'ini acan surum - cagiran elinde context
        // tutmuyorsa (cogu test sinifi `NewContext()`i kendi kullanir).
        public static async Task<int> AdresOlusturAsync(string connectionString, int musteriId)
        {
            await using var ctx = new DivisimaDbContext(
                new DbContextOptionsBuilder<DivisimaDbContext>().UseSqlServer(connectionString).Options);
            return await AdresOlusturAsync(ctx, musteriId);
        }
    }
}
