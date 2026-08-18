# WAF Yapılandırması (Cloudflare / AWS WAF / ModSecurity)

## Katman 1: Cloudflare (önerilen - DDoS + WAF birlikte)
- **Managed Ruleset:** OWASP Core Rule Set (CRS) aktif → SQLi, XSS, RCE, LFI otomatik engellenir.
- **Rate Limiting Rules:**
  - `/api/auth/*` → 5 istek/dk/IP (brute-force)
  - `/api/payment/*` → 10 istek/dk/IP
  - Genel → 100 istek/dk/IP
- **Bot Fight Mode:** açık (bilinen bot/scraper engeli).
- **DDoS Protection:** L3/L4/L7 otomatik (Cloudflare varsayılan).
- **Managed Challenge:** şüpheli trafik için (Turnstile ile entegre).
- **IP Reputation:** kötü şöhretli IP'ler engellenir.
- **Geo-blocking:** gerekiyorsa yalnız hizmet verilen ülkeler.

## Katman 2: ModSecurity (self-hosted nginx ile)
```nginx
# nginx.conf içinde
modsecurity on;
modsecurity_rules_file /etc/nginx/modsec/main.conf;
```
`main.conf` → OWASP CRS v4 include. Paranoia level 1-2 (false positive dengeli).

## AWS alternatifi
- **AWS WAF** + Managed Rules (AWSManagedRulesCommonRuleSet, SQLiRuleSet, KnownBadInputs)
- **AWS Shield** (DDoS)
- **CloudFront** (CDN + edge TLS)

## Uygulama ile ilişki
Kod tarafındaki korumalar (rate limit, input sanitization, SSRF, CSP) WAF'ın **arkasındaki** son kalkan.
WAF kaba filtreleme yapar; uygulama iş mantığı doğrulaması. İkisi birlikte defense-in-depth.
