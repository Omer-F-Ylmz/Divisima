# Açıklayıcı yorum: Çok aşamalı güvenli build. Non-root kullanıcı, minimal runtime image, secret gömülmez.
# GF-4/K5 (Y6): taban imajlar TAG + DIGEST ile pinli. "8.0" yuruyen bir etikettir;
# digest olmadan ayni Dockerfile zaman icinde BASKA bir taban imaji cozer (reproducible
# build ve tedarik zinciri butunlugu icin). Dependabot "docker" ekosistemi bu dosyayi
# izliyor (.github/dependabot.yml), yani digest yamalari PR olarak gelir.
FROM mcr.microsoft.com/dotnet/sdk:8.0@sha256:bb32ba3ba3ea36e38572d9d8db76fa15f7cbf722f3f886e06bca6d528bd4fba8 AS build
WORKDIR /src
COPY ["Divisima-Backend.sln", "./"]
COPY ["Divisima.Core/Divisima.Core.csproj", "Divisima.Core/"]
COPY ["Divisima.Entity/Divisima.Entity.csproj", "Divisima.Entity/"]
COPY ["Divisima.Dal/Divisima.Dal.csproj", "Divisima.Dal/"]
COPY ["Divisima.Bussiness/Divisima.Bussiness.csproj", "Divisima.Bussiness/"]
COPY ["Divisima.API/Divisima.API.csproj", "Divisima.API/"]
RUN dotnet restore "Divisima.API/Divisima.API.csproj"
COPY . .
RUN dotnet publish "Divisima.API/Divisima.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Açıklayıcı yorum: Runtime - küçük image, non-root kullanıcı (ele geçirilse bile sınırlı yetki)
FROM mcr.microsoft.com/dotnet/aspnet:8.0@sha256:787c228ea85457bec43c8b084e6ac360b26ea43b5c2fcbe861f721f2e8670dd3 AS final
WORKDIR /app
# Açıklayıcı yorum: HEALTHCHECK curl kullanıyor - aspnet imajında curl YOK, kur (root iken, minimal + cache temizle)
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
# Non-root kullanıcı oluştur
RUN groupadd -r divisima && useradd -r -g divisima divisima
COPY --from=build /app/publish .
# ══ DALGA C / C2 - YUKLEME DIZINI ACIKCA OLUSTURULUR (SAHIPLIK ICIN ZORUNLU) ═══════════════
# Bu satir olmadan zincir SESSIZCE kirilir:
#   .dockerignore, dev makinesindeki yuklenmis gorselleri build context'inden DISLIYOR
#   -> `dotnet publish` bos bir dizini ciktiya KOPYALAMAZ (bos dizinler korunmaz)
#   -> imajda /app/wwwroot/uploads YOK
#   -> compose'daki adlandirilmis volume OLMAYAN bir yola mount edilir ve Docker onu
#      root:root olarak olusturur
#   -> `USER divisima` oraya YAZAMAZ; gorsel yukleme uretimde basarisiz olur.
# Dizin BURADA, chown'dan ONCE olusturuluyor; boylece volume ilk mount'ta dogru sahipligi
# devralir. (Volume sahipliginin imajdaki dizinden devralindigi Dalga C'de olculdu.)
RUN mkdir -p /app/wwwroot/uploads/products
# Açıklayıcı yorum: Dosya sahipliği non-root'a, sadece okuma
RUN chown -R divisima:divisima /app
USER divisima
# Açıklayıcı yorum: Sağlık kontrolü (orchestrator readiness)
HEALTHCHECK --interval=30s --timeout=5s --retries=3 \
    CMD curl -f http://localhost:5000/health/live || exit 1
EXPOSE 5000
ENV ASPNETCORE_URLS=http://+:5000
# Secret ASLA image'a gömülmez - runtime'da env/Key Vault'tan gelir
ENTRYPOINT ["dotnet", "Divisima.API.dll"]
