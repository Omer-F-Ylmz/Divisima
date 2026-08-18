# Açıklayıcı yorum: Çok aşamalı güvenli build. Non-root kullanıcı, minimal runtime image, secret gömülmez.
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
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
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
# Açıklayıcı yorum: HEALTHCHECK curl kullanıyor - aspnet imajında curl YOK, kur (root iken, minimal + cache temizle)
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
# Non-root kullanıcı oluştur
RUN groupadd -r divisima && useradd -r -g divisima divisima
COPY --from=build /app/publish .
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
