using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using AutoMapper;
using Divisima.Bussiness.Abstract;
using Divisima.Core.Utilities.Constants;
using Divisima.Core.Utilities.Enums;
using Divisima.Core.Utilities.Moderation;
using Divisima.Core.Utilities.Results;
using Divisima.Core.Utilities.Sanitization;
using Divisima.DataAccess.Abstract;
using Divisima.Entity.Dtos.ProductReview;
using Divisima.Entity.Entities;
using Microsoft.EntityFrameworkCore;

namespace Divisima.Bussiness.Concrete
{
    // Açıklayıcı yorum: Ürün yorumu iş kuralları. Yeni yorum Pending gelir, admin onaylar. Cafixo ProductReviewManager kalıbı.
    public class ProductReviewManager : IProductReviewService
    {
        private readonly IProductReviewDal _productReviewDal;
        private readonly IProductDal _productDal;
        private readonly IMapper _mapper;

        private readonly IOrderItemDal _orderItemDal;
        private readonly IOrderDal _orderDal;
        private readonly IReviewHelpfulVoteDal _helpfulVoteDal;

        public ProductReviewManager(IProductReviewDal productReviewDal, IProductDal productDal, IOrderItemDal orderItemDal,
            IOrderDal orderDal, IReviewHelpfulVoteDal helpfulVoteDal, IMapper mapper)
        {
            _productReviewDal = productReviewDal;
            _productDal = productDal;
            _orderItemDal = orderItemDal;
            _orderDal = orderDal;
            _helpfulVoteDal = helpfulVoteDal;
            _mapper = mapper;
        }

        // Açıklayıcı yorum: Yorum ekle. Puan 1-5 aralığında; onay bekler durumunda kaydedilir.
        public async Task<(HttpStatusCode, Result)> Add(ProductReviewAddRequestDto dto)
        {
            if (dto.rating < 1 || dto.rating > 5)
                return (HttpStatusCode.BadRequest, new ErrorResult(Messages.ReviewInvalidRating));

            // Açıklayıcı yorum: Küfür/uygunsuz içerik reddi
            if (ProfanityFilter.ContainsProfanity(dto.comment))
                return (HttpStatusCode.BadRequest, new ErrorResult(Messages.ReviewProfanity));

            // Açıklayıcı yorum: ÇİFT YORUM ENGELİ - aynı müşteri aynı ürüne birden fazla yorum yapamaz (spam/puan manipülasyonu).
            var existing = await _productReviewDal.GetAsync(r => r.customer_id == dto.customer_id
                && r.product_id == dto.product_id && r.is_active);
            if (existing != null)
                return (HttpStatusCode.Conflict, new ErrorResult(Messages.ReviewAlreadyExists));

            // Açıklayıcı yorum: Doğrulanmış alıcı kontrolü - müşteri bu ürünü teslim edilmiş bir siparişte almış mı
            bool verified = await HasPurchasedAsync(dto.customer_id, dto.product_id);

            // DTO -> entity esleme AutoMapper ile YAPILMAZ. Iki sebep:
            //  1) ProductReviewProfile yalnizca ProductReview -> ListResponseDto yonunu tanimliyordu;
            //     ters yon hicbir profilde yoktu ve HER yorum ekleme AutoMapperMappingException ile
            //     500 donuyordu (yorum ozelligi uctan uca oluydu).
            //  2) Elle kurmak alan sizintisini kalici olarak kapatir: DTO'ya ileride bir alan eklense
            //     bile entity'ye gecemez. review_status / is_active / is_verified_purchase gibi
            //     SUNUCU alanlari istemciden ASLA gelmez.
            var review = new ProductReview
            {
                product_id = dto.product_id,
                customer_id = dto.customer_id,
                rating = dto.rating,
                comment = InputSanitizer.Sanitize(dto.comment ?? ""),
                is_verified_purchase = verified,
                helpful_count = 0,
                review_status = (byte)ReviewStatusEnum.Pending,
                is_active = true,
                created_at = DateTime.Now
            };

            try
            {
                await _productReviewDal.AddAsync(review);
            }
            catch (DbUpdateException)
            {
                // YARIS: yukaridaki "zaten yorumu var mi" kontrolu ile bu insert arasinda baska bir
                // istek ayni cifti yazmis olabilir (check-then-act, arada kilit yok). Tek gercek
                // koruma filtreli tekil indeks: (customer_id, product_id) WHERE is_active = 1.
                // Kaybeden cagri 500 degil, check-then-act yolunun verdigi AYNI yaniti almali.
                //
                // Hatayi KORU KORUNE yutmuyoruz: gercekten o yaris mi oldu diye DB'ye soruyoruz.
                // Aktif satir olustuysa yaris kaybedildi (409); olusmadiysa sebep baska bir DB
                // hatasidir ve yukari birakilir.
                var raced = await _productReviewDal.GetAsync(r => r.customer_id == dto.customer_id
                    && r.product_id == dto.product_id && r.is_active);
                if (raced != null)
                    return (HttpStatusCode.Conflict, new ErrorResult(Messages.ReviewAlreadyExists));
                throw;
            }

            return (HttpStatusCode.Created, new SuccessResult(Messages.ReviewAdded));
        }

        // Açıklayıcı yorum: ÜRÜN PUAN AGREGASYONU - onaylı yorumlardan ortalama + sayı yeniden hesaplanır.
        // Frontend her seferinde AVG hesaplamak yerine ürün üstünde hazır puanı okur (liste performansı).
        private async Task RecalculateProductRatingAsync(int productId)
        {
            var approved = await _productReviewDal.GetApprovedByProductAsync(productId);
            var product = await _productDal.GetAsync(p => p.id == productId);
            if (product == null) return;
            product.review_count = approved.Count;
            product.average_rating = approved.Count > 0
                ? Math.Round((decimal)approved.Average(r => r.rating), 2, MidpointRounding.AwayFromZero) : 0m;
            await _productDal.UpdateAsync(product);
        }

        // Açıklayıcı yorum: Müşteri bu ürünü teslim edilmiş bir siparişte satın almış mı (doğrulanmış alıcı)
        private async Task<bool> HasPurchasedAsync(int customerId, int productId)
        {
            var deliveredOrders = await _orderDal.GetListNoTrackingAsync(o =>
                o.customer_id == customerId && o.status == (byte)OrderStatusEnum.Delivered);
            if (deliveredOrders.Count == 0) return false;
            var orderIds = deliveredOrders.Select(o => o.id).ToList();
            // SAHTE "DOĞRULANMIŞ ALICI" FIX (H48): İPTAL EDİLMİŞ kalem satın alma SAYILMAZ.
            // Önceden yalnız sipariş durumu (Delivered) bakılıyordu, kalem bayrağı DEĞİL -> sömürü:
            // tek siparişe 10 ürün koy -> 9'unu CancelItem ile iptal edip PARASINI GERİ AL -> kalan 1 ucuz
            // ürün teslim edilince sipariş Delivered olur -> 10 ürünün HEPSİNE "doğrulanmış alıcı" rozetli
            // yorum yazılabilirdi (rakibe 1 yıldız / kendine 5 yıldız, maliyeti bir ucuz ürün).
            // Kural referansı: PaidOrderSpec.IsSoldItem - sipariş durumu VE kalem bayrağı BİRLİKTE değerlendirilir.
            // PERFORMANS (H51): EXISTS - "boyle bir kalem var mi" sorusu icin satirlari cekmeye gerek yok.
            return await _orderItemDal.AnyAsync(i =>
                orderIds.Contains(i.order_id) && i.product_id == productId && !i.is_cancelled);
        }

        // Açıklayıcı yorum: "Bu yorum faydalı" oyu - müşteri başına tek (çift oy engeli)
        public async Task<(HttpStatusCode, Result)> VoteHelpful(int reviewId, int customerId)
        {
            var review = await _productReviewDal.GetAsync(r => r.id == reviewId && r.is_active);
            if (review == null) return (HttpStatusCode.NotFound, new ErrorResult(Messages.ReviewNotFound));

            var existing = await _helpfulVoteDal.GetAsync(v => v.review_id == reviewId && v.customer_id == customerId);
            if (existing != null) return (HttpStatusCode.OK, new SuccessResult(Messages.ReviewAlreadyVoted));

            try
            {
                await _helpfulVoteDal.AddAsync(new ReviewHelpfulVote { review_id = reviewId, customer_id = customerId, created_at = DateTime.Now });
            }
            catch
            {
                // Concurrency: ayni musteri ESZAMANLI oy verdi -> (review_id, customer_id) unique index ihlali.
                // Graceful: zaten oylanmis say (500 yerine), sayaci ARTIRMA (kazanan zaten artirdi -> cift-artis yok).
                return (HttpStatusCode.OK, new SuccessResult(Messages.ReviewAlreadyVoted));
            }
            // Insert BASARILI oldugunda sayaci atomik artir (LOST-UPDATE + cift-sayim engeli - insert throw ederse buraya gelinmez).
            await _productReviewDal.IncrementHelpfulCountAsync(reviewId);
            return (HttpStatusCode.OK, new SuccessResult(Messages.ReviewVoted));
        }

        // Açıklayıcı yorum: Yorumu onayla (admin) - storefront'ta görünür olur.
        public async Task<(HttpStatusCode, Result)> Approve(int id)
        {
            var review = await _productReviewDal.GetAsync(r => r.id == id);
            if (review == null)
                return (HttpStatusCode.NotFound, new ErrorResult(Messages.ReviewNotFound));

            review.review_status = (byte)ReviewStatusEnum.Approved;
            review.updated_at = DateTime.Now;
            await _productReviewDal.UpdateAsync(review);
            await RecalculateProductRatingAsync(review.product_id);   // ürün ortalama puanını güncelle
            return (HttpStatusCode.OK, new SuccessResult(Messages.ReviewApproved));
        }

        // Açıklayıcı yorum: Yorumu reddet (admin).
        public async Task<(HttpStatusCode, Result)> Reject(int id)
        {
            var review = await _productReviewDal.GetAsync(r => r.id == id);
            if (review == null)
                return (HttpStatusCode.NotFound, new ErrorResult(Messages.ReviewNotFound));

            review.review_status = (byte)ReviewStatusEnum.Rejected;
            review.updated_at = DateTime.Now;
            await _productReviewDal.UpdateAsync(review);
            await RecalculateProductRatingAsync(review.product_id);   // agregat güncelle (onaylıydıysa düşer)
            return (HttpStatusCode.OK, new SuccessResult(Messages.ReviewRejected));
        }

        // Açıklayıcı yorum: Ürünün onaylı yorumları (storefront).
        public async Task<(HttpStatusCode, Result)> GetByProduct(int productId)
        {
            var reviews = await _productReviewDal.GetApprovedByProductAsync(productId);
            var data = _mapper.Map<List<ProductReviewResponseDto>>(reviews);
            return (HttpStatusCode.OK, new SuccessDataResult<List<ProductReviewResponseDto>>(data, Messages.ReviewListed));
        }
    }
}
