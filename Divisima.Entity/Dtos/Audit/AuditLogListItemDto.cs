using System;
using Divisima.Core.Utilities.Dtos;

namespace Divisima.Entity.Dtos.Audit
{
    // ═══ FAZ 0 / K6 - DENETIM KAYDI LISTE KALEMI ═══════════════════════════════════════
    //
    // OLCULEN ONCE-DURUM: AuditLogController HAM ENTITY (AuditLog) donuyordu. Entity'yi
    // dogrudan HTTP'ye koymak, alan eklendiginde/adi degistiginde sozlesmeyi SESSIZCE
    // degistirir; ayrica hangi alanin disari acildigi kararini entity tasarimina birakir.
    //
    // KAPSAM KARARI: entity'nin YEDI alaninin YEDISI de burada - denetim kaydinin AMACI
    // zaten "ne oldu, kim yapti" sorusunu yanitlamak ve uc [RequireUserType(Admin)] ile
    // korumali. Yani bu bir DARALTMA degil, SOZLESMEYI SABITLEME islemidir: entity yarin
    // degisirse DTO degismez ve degistirmek BILINCLI bir karar olur.
    public class AuditLogListItemDto : IDto
    {
        public int id { get; set; }
        public string table_name { get; set; } = "";
        public string entity_id { get; set; } = "";
        public string action { get; set; } = "";      // Added / Modified / Deleted
        public string? changes { get; set; }           // JSON: degisen alanlar (eski->yeni)
        public string? user_id { get; set; }           // islemi yapan (JWT claim)
        public DateTime created_at { get; set; }
    }
}
