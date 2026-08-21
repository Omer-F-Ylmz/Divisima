using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Divisima.Bussiness.Abstract;
using Divisima.Core.Utilities.Constants;
using Divisima.Core.Utilities.Results;
using Divisima.DataAccess.Abstract;
using Divisima.Entity.Dtos.SizeGuide;
using Divisima.Entity.Entities;

namespace Divisima.Bussiness.Concrete
{
    // Açıklayıcı yorum: Beden rehberi iş kuralları. Öneri: kullanıcı ölçülerine en yakın bedeni bul (mutlak fark toplamı).
    public class SizeGuideManager : ISizeGuideService
    {
        private readonly ISizeGuideEntryDal _dal;

        public SizeGuideManager(ISizeGuideEntryDal dal) { _dal = dal; }

        public async Task<(HttpStatusCode, Result)> Upsert(SizeGuideEntryDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.size_label))
                return (HttpStatusCode.BadRequest, new ErrorResult(Messages.SizeGuideInvalid));

            // Açıklayıcı yorum: Aynı kategori+beden varsa güncelle, yoksa ekle (idempotent)
            var existing = await _dal.GetAsync(e => e.category_id == dto.category_id && e.size_label == dto.size_label && e.is_active);
            if (existing != null)
            {
                existing.bust_cm = dto.bust_cm; existing.waist_cm = dto.waist_cm;
                existing.hip_cm = dto.hip_cm; existing.length_cm = dto.length_cm;
                existing.sort_order = dto.sort_order;
                await _dal.UpdateAsync(existing);
            }
            else
            {
                await _dal.AddAsync(new SizeGuideEntry
                {
                    category_id = dto.category_id,
                    size_label = dto.size_label,
                    bust_cm = dto.bust_cm,
                    waist_cm = dto.waist_cm,
                    hip_cm = dto.hip_cm,
                    length_cm = dto.length_cm,
                    sort_order = dto.sort_order,
                    is_active = true,
                    created_at = DateTime.Now
                });
            }
            return (HttpStatusCode.OK, new SuccessResult(Messages.SizeGuideUpdated));
        }

        public async Task<(HttpStatusCode, Result)> GetByCategory(int categoryId)
        {
            var list = await _dal.GetListNoTrackingAsync(e => e.category_id == categoryId && e.is_active);
            return (HttpStatusCode.OK, new SuccessDataResult<List<SizeGuideEntry>>(list.OrderBy(e => e.sort_order).ToList()));
        }

        public async Task<(HttpStatusCode, Result)> RecommendSize(int categoryId, decimal? bust, decimal? waist, decimal? hip)
        {
            var entries = await _dal.GetListNoTrackingAsync(e => e.category_id == categoryId && e.is_active);
            if (entries.Count == 0) return (HttpStatusCode.NotFound, new ErrorResult(Messages.SizeGuideNotFound));

            // Açıklayıcı yorum: En yakın beden - verilen ölçülerle satırın ölçüleri arasındaki mutlak fark toplamı en küçük olan
            SizeGuideEntry best = null;
            decimal bestScore = decimal.MaxValue;
            foreach (var e in entries)
            {
                decimal score = 0m; int considered = 0;
                if (bust.HasValue && e.bust_cm.HasValue) { score += Math.Abs(bust.Value - e.bust_cm.Value); considered++; }
                if (waist.HasValue && e.waist_cm.HasValue) { score += Math.Abs(waist.Value - e.waist_cm.Value); considered++; }
                if (hip.HasValue && e.hip_cm.HasValue) { score += Math.Abs(hip.Value - e.hip_cm.Value); considered++; }
                if (considered == 0) continue; // kıyaslanacak ölçü yok
                // MANTIK FIX (H52): skor TOPLAM fark idi ve "considered" hesaplanip HIC KULLANILMIYORDU ->
                // az olcusu dolu satirlar sistematik olarak kazaniyordu (1 olcude 1 cm sapan satir, 3 olcude
                // ortalama 1 cm sapan satiri yeniyordu; cunku 1 < 3). Dogru olcut ORTALAMA sapma.
                // Ornek: musteri 90/70/95 -> "S"(sadece bust=91) skor 1 ile kazanirdi; "M"(90/72/96) skor 3 ile kaybederdi.
                var avgScore = score / considered;
                if (avgScore < bestScore) { bestScore = avgScore; best = e; }
            }

            if (best == null) return (HttpStatusCode.BadRequest, new ErrorResult(Messages.SizeGuideNoMeasurements));
            return (HttpStatusCode.OK, new SuccessDataResult<SizeGuideEntry>(best, Messages.SizeGuideRecommended));
        }
    }
}
