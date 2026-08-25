using System;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Divisima.Core.Security
{
    // ═══ FIX-1A / F3 - DENETIM KAYDI REDAKSIYONU ═══════════════════════════════════════════
    //
    // KVKK unutulma hakki kullanildiginda, o musteriye ait denetim satirlarindaki KISISEL
    // DEGERLER isaretle degistirilir. SATIR SILINMEZ: id / action / entity_id / created_at /
    // user_id ve ALAN ADLARI korunur - yani "su tarihte su alan degisti" izi ayakta kalir,
    // yalnizca "neydi / ne oldu" gider.
    //
    // FAZ 1'de OLCULEN ZARAR: silinen hesabin e-postasi 2, adi 3, telefonu 9, acik adres metni
    // 1 satirda audit_logs'ta KALIYORDU ve `AccountManager.DeleteAccount` icinde audit_logs'a
    // dokunan TEK SATIR YOKTU.
    public static class DenetimRedaksiyonu
    {
        // Ayristirilamayan / beklenmedik bicimli bir payload GECIRILMEZ: tamami isarete
        // cevrilir. Gerekce: KVKK yolunda "anlayamadim, oldugu gibi biraktim" KABUL EDILEMEZ;
        // ama tek bozuk satir yuzunden silmeyi KALICI OLARAK bloke etmek de dogru degil -
        // fail-safe yon PII'nin GITMESIDIR.
        public static string Redakte(string changes)
        {
            if (string.IsNullOrWhiteSpace(changes)) return changes;

            JsonNode kok;
            try { kok = JsonNode.Parse(changes); }
            catch (JsonException) { return JsonSerializer.Serialize(DenetimGizlilik.Isaret); }

            if (kok is not JsonObject nesne) return JsonSerializer.Serialize(DenetimGizlilik.Isaret);

            var sonuc = new JsonObject();
            foreach (var alan in nesne)
            {
                if (!DenetimGizlilik.RedakteEdilmeli(alan.Key))
                {
                    sonuc[alan.Key] = alan.Value?.DeepClone();
                    continue;
                }

                // Alan ADI korunur, DEGERLER isaretle degistirilir. Beklenen bicim {old,new};
                // baska bir bicim gelirse tumu tek isarete duser (yine deger sizmaz).
                if (alan.Value is JsonObject ciftler)
                {
                    var yeni = new JsonObject();
                    foreach (var c in ciftler) yeni[c.Key] = JsonValue.Create(DenetimGizlilik.Isaret);
                    sonuc[alan.Key] = yeni;
                }
                else
                {
                    sonuc[alan.Key] = JsonValue.Create(DenetimGizlilik.Isaret);
                }
            }
            return sonuc.ToJsonString();
        }

        // Bir payload'da redakte edilmesi GEREKEN ama edilmemis deger kalip kalmadigini soyler.
        // Pinler ve calisma ani kontrolu icin TEK olcut - iki ayri el yazmasi olmasin.
        public static bool RedakteEdilmemisDegerVarMi(string changes)
        {
            if (string.IsNullOrWhiteSpace(changes)) return false;
            JsonNode kok;
            try { kok = JsonNode.Parse(changes); } catch (JsonException) { return true; }
            if (kok is not JsonObject nesne) return true;

            foreach (var alan in nesne)
            {
                if (!DenetimGizlilik.RedakteEdilmeli(alan.Key)) continue;
                if (alan.Value is JsonObject ciftler)
                {
                    foreach (var c in ciftler)
                    {
                        var v = c.Value;
                        if (v == null) continue;                                   // null deger PII tasimaz
                        if (v is JsonValue jv && jv.TryGetValue<string>(out var s) && s == DenetimGizlilik.Isaret) continue;
                        return true;
                    }
                }
                else if (alan.Value != null)
                {
                    if (alan.Value is JsonValue jv2 && jv2.TryGetValue<string>(out var s2) && s2 == DenetimGizlilik.Isaret) continue;
                    return true;
                }
            }
            return false;
        }
    }
}
