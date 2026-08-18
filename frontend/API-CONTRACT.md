# Divisima - Frontend API Kontratı

Backend endpoint'leri özet (frontend entegrasyonu için). Auth: **Anonim** (herkes), **Müşteri/Token** (giriş gerekli, JWT), **Admin**.

Base URL: `/` · Kimlik: `Authorization: Bearer <token>` · Yanıt: `{ success, message, data }`

## Account

| Metod | Endpoint | Yetki |
|-------|----------|-------|
| GET | `/api/[controller]/summary` | Müşteri/Token |
| PUT | `/api/[controller]/profile` | Müşteri/Token |
| POST | `/api/[controller]/change-password` | Müşteri/Token |
| PUT | `/api/[controller]/notification-preferences` | Müşteri/Token |
| DELETE | `/api/[controller]/delete` | Müşteri/Token |

## Address

| Metod | Endpoint | Yetki |
|-------|----------|-------|
| POST | `/api/[controller]/upsert` | Müşteri/Token |
| DELETE | `/api/[controller]/delete/{id:int:min(1)}` | Müşteri/Token |
| GET | `/api/[controller]` | Müşteri/Token |

## AdminCustomer

| Metod | Endpoint | Yetki |
|-------|----------|-------|
| POST | `/api/admin/customer/list` | Müşteri/Token |
| POST | `/api/admin/customer/status` | Müşteri/Token |
| POST | `/api/admin/customer/set-type` | Müşteri/Token |

## AuditLog

| Metod | Endpoint | Yetki |
|-------|----------|-------|
| GET | `/api/[controller]/list` | Admin |

## Auth

| Metod | Endpoint | Yetki |
|-------|----------|-------|
| POST | `/api/[controller]/register` | Anonim |
| POST | `/api/[controller]/login` | Anonim |
| POST | `/api/[controller]/refresh` | Anonim |
| GET | `/api/[controller]/verify-email` | Anonim |
| POST | `/api/[controller]/resend-verification` | Anonim |
| DELETE | `/api/[controller]/account` | Müşteri/Token |
| GET | `/api/[controller]/my-data` | Müşteri/Token |
| POST | `/api/[controller]/forgot-password` | Anonim |
| POST | `/api/[controller]/reset-password` | Anonim |
| POST | `/api/[controller]/logout` | Müşteri/Token |

## Cart

| Metod | Endpoint | Yetki |
|-------|----------|-------|
| POST | `/api/[controller]/add` | Müşteri/Token |
| DELETE | `/api/[controller]/remove` | Müşteri/Token |
| GET | `/api/[controller]` | Müşteri/Token |
| DELETE | `/api/[controller]/clear` | Müşteri/Token |
| POST | `/api/[controller]/save-for-later` | Müşteri/Token |
| POST | `/api/[controller]/move-to-cart` | Müşteri/Token |

## Category

| Metod | Endpoint | Yetki |
|-------|----------|-------|
| POST | `/api/[controller]/add` | Admin |
| PUT | `/api/[controller]/update` | Admin |
| DELETE | `/api/[controller]/delete/{id:int:min(1)}` | Admin |
| GET | `/api/[controller]/get/{id:int:min(1)}` | Anonim |
| GET | `/api/[controller]/getlist` | Anonim |

## Collection

| Metod | Endpoint | Yetki |
|-------|----------|-------|
| POST | `/api/[controller]/add` | Admin |
| PUT | `/api/[controller]/update` | Admin |
| DELETE | `/api/[controller]/delete/{id:int:min(1)}` | Admin |
| GET | `/api/[controller]/getlist` | Anonim |
| GET | `/api/[controller]/get/{slug}` | Anonim |

## Comparison

| Metod | Endpoint | Yetki |
|-------|----------|-------|
| POST | `/api/[controller]/compare` | Müşteri/Token |

## Content

| Metod | Endpoint | Yetki |
|-------|----------|-------|
| GET | `/api/[controller]/get/{slug}` | Anonim |
| GET | `/api/[controller]/getlist` | Anonim |
| PUT | `/api/[controller]/update` | Admin |

## Coupon

| Metod | Endpoint | Yetki |
|-------|----------|-------|
| POST | `/api/[controller]/add` | Admin |
| PUT | `/api/[controller]/update` | Admin |
| DELETE | `/api/[controller]/delete/{id:int:min(1)}` | Admin |
| GET | `/api/[controller]/getlist` | Admin |
| POST | `/api/[controller]/validate` | Anonim |

## Dashboard

| Metod | Endpoint | Yetki |
|-------|----------|-------|
| GET | `/api/[controller]/summary` | Müşteri/Token |
| GET | `/api/[controller]/daily-sales` | Müşteri/Token |
| GET | `/api/[controller]/top-products` | Müşteri/Token |
| GET | `/api/[controller]/order-status` | Müşteri/Token |
| GET | `/api/[controller]/low-stock` | Müşteri/Token |
| GET | `/api/[controller]/sales-by-category` | Müşteri/Token |

## Device

| Metod | Endpoint | Yetki |
|-------|----------|-------|
| POST | `/api/[controller]/register` | Müşteri/Token |
| POST | `/api/[controller]/unregister` | Müşteri/Token |

## GiftCard

| Metod | Endpoint | Yetki |
|-------|----------|-------|
| POST | `/api/gift-card/create` | Admin |
| GET | `/api/gift-card/balance/{code}` | Müşteri/Token |
| POST | `/api/gift-card/redeem/{code}` | Müşteri/Token |

## GuestCheckout

| Metod | Endpoint | Yetki |
|-------|----------|-------|
| POST | `/api/guest-checkout/place` | Müşteri/Token |

## Invoice

| Metod | Endpoint | Yetki |
|-------|----------|-------|
| POST | `/api/[controller]/generate/{orderId}` | Admin |
| GET | `/api/[controller]/my` | Müşteri/Token |
| GET | `/api/[controller]/order/{orderId}` | Müşteri/Token |

## Loyalty

| Metod | Endpoint | Yetki |
|-------|----------|-------|
| GET | `/api/[controller]/balance` | Müşteri/Token |
| GET | `/api/[controller]/history` | Müşteri/Token |
| POST | `/api/[controller]/redeem/{points:int:min(1)}` | Müşteri/Token |
| GET | `/api/[controller]/tier` | Müşteri/Token |

## Merchandising

| Metod | Endpoint | Yetki |
|-------|----------|-------|
| GET | `/api/[controller]/best-sellers` | Müşteri/Token |
| GET | `/api/[controller]/trending` | Müşteri/Token |
| GET | `/api/[controller]/new-arrivals` | Müşteri/Token |

## Order

| Metod | Endpoint | Yetki |
|-------|----------|-------|
| POST | `/api/[controller]/place` | Müşteri/Token |
| GET | `/api/[controller]/get/{id:int:min(1)}` | Müşteri/Token |
| GET | `/api/[controller]/my-orders` | Müşteri/Token |
| GET | `/api/[controller]/timeline/{orderId:int:min(1)}` | Müşteri/Token |
| POST | `/api/[controller]/admin/list` | Admin |
| POST | `/api/[controller]/{orderId:int:min(1)}/cancel-item/{orderItemId:int:min(1)}` | Müşteri/Token |
| GET | `/api/[controller]/{orderId:int:min(1)}/estimated-delivery` | Müşteri/Token |
| POST | `/api/[controller]/confirm-manual-payment/{orderId}` | Admin |
| GET | `/api/[controller]/{orderId}/invoice-html` | Müşteri/Token |

## Payment

| Metod | Endpoint | Yetki |
|-------|----------|-------|
| POST | `/api/[controller]/initialize` | Müşteri/Token |
| POST | `/api/[controller]/callback` | Anonim |
| POST | `/api/[controller]/webhook` | Anonim |

## PriceDrop

| Metod | Endpoint | Yetki |
|-------|----------|-------|
| POST | `/api/price-drop/subscribe` | Anonim |

## ProductAttribute

| Metod | Endpoint | Yetki |
|-------|----------|-------|
| POST | `/api/product-attribute/set` | Admin |
| GET | `/api/product-attribute/product/{productId:int:min(1)}` | Anonim |
| GET | `/api/product-attribute/facets` | Anonim |
| POST | `/api/product-attribute/filter` | Anonim |

## Product

| Metod | Endpoint | Yetki |
|-------|----------|-------|
| POST | `/api/[controller]/add` | Admin |
| POST | `/api/[controller]/import` | Admin |
| PUT | `/api/[controller]/update` | Admin |
| DELETE | `/api/[controller]/delete/{id:int:min(1)}` | Admin |
| GET | `/api/[controller]/get/{id:int:min(1)}` | Anonim |
| GET | `/api/[controller]/getlist` | Admin |
| POST | `/api/[controller]/filter` | Anonim |
| GET | `/api/[controller]/on-sale` | Anonim |
| GET | `/api/[controller]/{productId:int:min(1)}/variants` | Anonim |

## ProductImage

| Metod | Endpoint | Yetki |
|-------|----------|-------|
| GET | `/api/product-image/product/{productId:int:min(1)}` | Müşteri/Token |
| POST | `/api/product-image/upload` | Admin |
| DELETE | `/api/product-image/{imageId:int:min(1)}` | Admin |
| POST | `/api/product-image/{imageId:int:min(1)}/primary` | Admin |

## ProductQuestion

| Metod | Endpoint | Yetki |
|-------|----------|-------|
| GET | `/api/product-question/product/{productId}` | Anonim |
| POST | `/api/product-question/ask` | Müşteri/Token |
| GET | `/api/product-question/pending` | Admin |
| POST | `/api/product-question/answer` | Admin |

## ProductReview

| Metod | Endpoint | Yetki |
|-------|----------|-------|
| POST | `/api/[controller]/add` | Müşteri/Token |
| POST | `/api/[controller]/vote-helpful/{reviewId:int:min(1)}` | Müşteri/Token |
| GET | `/api/[controller]/product/{productId:int:min(1)}` | Anonim |

## RecentlyViewed

| Metod | Endpoint | Yetki |
|-------|----------|-------|
| POST | `/api/[controller]/record/{productId:int:min(1)}` | Müşteri/Token |
| GET | `/api/[controller]` | Müşteri/Token |

## Recommendation

| Metod | Endpoint | Yetki |
|-------|----------|-------|
| GET | `/api/[controller]/frequently-bought/{productId}` | Müşteri/Token |
| GET | `/api/[controller]/similar/{productId}` | Müşteri/Token |

## Referral

| Metod | Endpoint | Yetki |
|-------|----------|-------|
| GET | `/api/[controller]/my-code` | Müşteri/Token |

## Return

| Metod | Endpoint | Yetki |
|-------|----------|-------|
| POST | `/api/[controller]/create` | Müşteri/Token |
| GET | `/api/[controller]/my` | Müşteri/Token |
| GET | `/api/[controller]/pending` | Admin |
| POST | `/api/[controller]/process` | Admin |

## Search

| Metod | Endpoint | Yetki |
|-------|----------|-------|
| GET | `/api/[controller]/products` | Anonim |

## Seo

| Metod | Endpoint | Yetki |
|-------|----------|-------|
| GET | `/api/[controller]/sitemap` | Müşteri/Token |

## Shipment

| Metod | Endpoint | Yetki |
|-------|----------|-------|
| POST | `/api/[controller]/create` | Admin |
| GET | `/api/[controller]/track/{orderId}` | Müşteri/Token |
| GET | `/api/[controller]/order/{orderId}` | Admin |

## SizeGuide

| Metod | Endpoint | Yetki |
|-------|----------|-------|
| POST | `/api/size-guide/upsert` | Admin |
| GET | `/api/size-guide/category/{categoryId:int:min(1)}` | Anonim |
| GET | `/api/size-guide/recommend` | Anonim |

## Stock

| Metod | Endpoint | Yetki |
|-------|----------|-------|
| POST | `/api/[controller]/adjust` | Müşteri/Token |

## StockNotification

| Metod | Endpoint | Yetki |
|-------|----------|-------|
| POST | `/api/[controller]/subscribe` | Anonim |

## StoreCredit

| Metod | Endpoint | Yetki |
|-------|----------|-------|
| GET | `/api/store-credit/balance` | Müşteri/Token |
| GET | `/api/store-credit/history` | Müşteri/Token |

## Wishlist

| Metod | Endpoint | Yetki |
|-------|----------|-------|
| POST | `/api/[controller]/toggle` | Müşteri/Token |
| GET | `/api/[controller]` | Müşteri/Token |
| POST | `/api/[controller]/move-to-cart` | Müşteri/Token |

## Yeni eklenen özellikler (bu sürüm)
- **Havale/EFT ödeme**: sipariş `payment_method=2` ile oluşturulur (Pending kalır), admin `POST /api/order/confirm-manual-payment/{id}` ile onaylar.
- **Kapıda ödeme (COD)**: `payment_method=1` (5000 TL limit).
- **Mağaza kredisi checkout**: sipariş DTO'suna `use_store_credit` (kalan online tahsil edilir).
- **Sadakat seviyesi**: `GET /api/loyalty/tier` (rozet + ilerleme).
- **İstek listesi → sepet**: `POST /api/wishlist/move-to-cart`.
- **Ürün soru-cevap**: `GET /api/product-question/product/{id}` (yanıtlılar), `POST /api/product-question/ask`.
- **Ürün puanı**: ürün listesi/detayında `average_rating` + `review_count`.
- **Fatura**: `GET /api/order/{id}/invoice-html` (yazdırılabilir HTML).