// Divisima - k6 yuk testi (sicak okuma + siparis yolu)
// Calistirma:  k6 run -e BASE_URL=https://api.divisima.com -e TOKEN=<jwt> load-tests/k6-hot-paths.js
// Kurulum:     https://k6.io/docs/get-started/installation/
//
// Amac: urun listesi (en cok cagrilan GET) ve siparis verme (en kritik POST) altinda
// p95 gecikme + hata oranini olcmek. Esikler (thresholds) asilirsa exit code != 0 (CI'da kullanilabilir).

import http from 'k6/http';
import { check, sleep, group } from 'k6';
import { Rate, Trend } from 'k6/metrics';

const errorRate = new Rate('errors');
const productListDuration = new Trend('product_list_duration', true);
const placeOrderDuration = new Trend('place_order_duration', true);

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';
const TOKEN = __ENV.TOKEN || ''; // siparis testi icin gerekli (auth-korumali)

export const options = {
  scenarios: {
    // Senaryo 1: Urun bogesme - kademeli artan okuma yuku (musterilerin cogu goz atiyor)
    browsing: {
      executor: 'ramping-vus',
      exec: 'browse',
      startVUs: 0,
      stages: [
        { duration: '30s', target: 50 },   // isinma
        { duration: '1m', target: 200 },    // pik trafik
        { duration: '30s', target: 0 },     // sogutma
      ],
    },
    // Senaryo 2: Siparis - sabit dusuk oran (okumaya gore az ama kritik)
    ordering: {
      executor: 'constant-vus',
      exec: 'placeOrder',
      vus: 10,
      duration: '2m',
      startTime: '30s',
    },
  },
  thresholds: {
    // Aciklayici yorum: Uretim SLO hedefleri - asilirsa test basarisiz (CI gate)
    'http_req_duration{scenario:browsing}': ['p(95)<500'],   // urun listesi p95 < 500ms
    'place_order_duration': ['p(95)<1500'],                  // siparis p95 < 1.5s (transaction + stok)
    'errors': ['rate<0.01'],                                 // hata orani < %1
    'http_req_failed': ['rate<0.02'],
  },
};

// Aciklayici yorum: Urun listesi + detay + faceted filtre (tipik goz atma akisi)
export function browse() {
  group('product browsing', () => {
    const listRes = http.get(`${BASE_URL}/api/product`);
    productListDuration.add(listRes.timings.duration);
    check(listRes, { 'product list 200': (r) => r.status === 200 }) || errorRate.add(1);

    // Faceted filtre (kategori + fiyat araligi)
    const filterRes = http.get(`${BASE_URL}/api/product/search?minPrice=100&maxPrice=1000`);
    check(filterRes, { 'filter ok': (r) => r.status === 200 || r.status === 404 }) || errorRate.add(1);

    sleep(Math.random() * 2 + 1); // musteri okuma suresi (1-3s)
  });
}

// Aciklayici yorum: Siparis verme (auth gerekli) - stok dususu + transaction yolu
export function placeOrder() {
  if (!TOKEN) return; // token yoksa siparis senaryosu atlanir

  const payload = JSON.stringify({
    customer_id: 1,
    items: [{ product_id: 1, size: 'M', quantity: 1 }],
  });
  const params = {
    headers: {
      'Content-Type': 'application/json',
      'Authorization': `Bearer ${TOKEN}`,
      // Aciklayici yorum: Idempotency - ayni siparis cift islenmez (yuk altinda kritik)
      'Idempotency-Key': `k6-${__VU}-${__ITER}-${Date.now()}`,
    },
  };
  const res = http.post(`${BASE_URL}/api/order/place`, payload, params);
  placeOrderDuration.add(res.timings.duration);
  check(res, {
    'order created or expected fail': (r) => r.status === 201 || r.status === 400,
  }) || errorRate.add(1);

  sleep(1);
}
