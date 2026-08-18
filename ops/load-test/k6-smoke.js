// Divisima - k6 yuk/duman testi.  Calistirma:  k6 run ops/load-test/k6-smoke.js
// Kac es zamanli kullanici kaldirdigini olcer; p95 gecikme + hata orani esiklerini dogrular.
import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate } from 'k6/metrics';

const errorRate = new Rate('errors');
const BASE = __ENV.BASE_URL || 'http://localhost:5000';

export const options = {
  stages: [
    { duration: '30s', target: 20 },   // isinma: 20 kullaniciya ramp
    { duration: '1m',  target: 50 },   // sabit yuk: 50 kullanici
    { duration: '30s', target: 100 },  // pik: 100 kullanici
    { duration: '30s', target: 0 },    // soguma
  ],
  thresholds: {
    http_req_duration: ['p(95)<500'],  // istekelerin %95'i < 500ms
    errors: ['rate<0.01'],             // hata orani < %1
    http_req_failed: ['rate<0.01'],
  },
};

export default function () {
  // 1) Saglik
  let res = http.get(`${BASE}/health/live`);
  check(res, { 'health 200': (r) => r.status === 200 }) || errorRate.add(1);

  // 2) Urun listesi (en sik cagrilan uc)
  res = http.get(`${BASE}/api/product/list`);
  check(res, { 'urun listesi 200': (r) => r.status === 200 }) || errorRate.add(1);
  sleep(1);

  // 3) Indirimli urunler
  res = http.get(`${BASE}/api/product/on-sale`);
  check(res, { 'indirimliler 200': (r) => r.status === 200 || r.status === 404 }) || errorRate.add(1);
  sleep(1);
}
