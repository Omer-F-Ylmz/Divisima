#!/usr/bin/env bash
# Açıklayıcı yorum: Secrets rotasyon script'i (Azure Key Vault). JWT signing key + Encryption key üretir,
# kasaya yeni sürüm olarak yazar. Eski sürüm kısa süre geçerli kalır (kademeli geçiş - grace period).
# CI/cron ile 90 günde bir çalıştırılır. Kullanım: ./rotate-secrets.sh <vault-name>
set -euo pipefail

VAULT="${1:?Kullanım: rotate-secrets.sh <vault-name>}"

echo "== Divisima secret rotasyonu: $VAULT =="

# JWT signing key (256-bit rastgele, base64)
NEW_JWT_KEY=$(openssl rand -base64 48)
az keyvault secret set --vault-name "$VAULT" --name "TokenOptions--SecurityKey" --value "$NEW_JWT_KEY" >/dev/null
echo "✓ JWT SecurityKey rotasyonu yapıldı"

# Field encryption key (256-bit = 32 byte, base64)
NEW_ENC_KEY=$(openssl rand -base64 32)
az keyvault secret set --vault-name "$VAULT" --name "Encryption--Key" --value "$NEW_ENC_KEY" >/dev/null
echo "✓ Encryption Key rotasyonu yapıldı"

# NOT: Encryption key rotasyonunda mevcut şifreli veriler için re-encryption gerekir.
# Uygulama eski+yeni anahtarı desteklemeli (key versioning) - AesEncryptionProvider'a keyId eklenmeli.
echo "⚠ Encryption key rotasyonu sonrası: mevcut şifreli alanlar için re-encryption job'ı çalıştırın."

echo "== Rotasyon tamamlandı. Uygulamayı yeniden başlatın (yeni secret'lar yüklensin). =="
