# Build from the repo root:  docker build -f backend/src/SalesDashboard.Api/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

# Restore first so this layer caches unless a project file changes.
COPY backend/Directory.Build.props backend/nuget.config backend/SalesDashboard.slnx backend/
COPY backend/src/SalesDashboard.Contracts/*.csproj                 backend/src/SalesDashboard.Contracts/
COPY backend/src/SalesDashboard.Infrastructure/*.csproj            backend/src/SalesDashboard.Infrastructure/
COPY backend/src/SalesDashboard.Api/*.csproj                       backend/src/SalesDashboard.Api/
COPY backend/src/Modules/SalesDashboard.Modules.Sales/*.csproj     backend/src/Modules/SalesDashboard.Modules.Sales/
COPY backend/src/Modules/SalesDashboard.Modules.Catalog/*.csproj   backend/src/Modules/SalesDashboard.Modules.Catalog/
COPY backend/src/Modules/SalesDashboard.Modules.People/*.csproj    backend/src/Modules/SalesDashboard.Modules.People/
COPY backend/src/Modules/SalesDashboard.Modules.Analytics/*.csproj backend/src/Modules/SalesDashboard.Modules.Analytics/
RUN dotnet restore backend/src/SalesDashboard.Api/SalesDashboard.Api.csproj

COPY backend/ backend/
RUN dotnet publish backend/src/SalesDashboard.Api/SalesDashboard.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app

# Refresh the vulnerable OpenSSL runtime libraries from Alpine's signed repositories. The minimum
# versions keep a stale mirror or cached package index from silently restoring the known-vulnerable
# 3.5.7-r0 build while still allowing later security revisions.
RUN apk add --no-cache --upgrade \
      "libcrypto3>=3.5.8-r0" \
      "libssl3>=3.5.8-r0"

# The app runs in globalization-invariant mode (see Directory.Build.props), so Alpine's missing ICU
# is fine and no icu packages are needed. The .NET runtime image already ships a non-root "app" user,
# so we reuse it rather than creating one.
COPY --from=build --chown=app:app /app .
USER app

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true
EXPOSE 8080

# Readiness, not liveness: /api/health/ready returns 200 only after migrations + seed complete. The
# start period covers first-boot migrate+seed, which run inside this process before it serves.
HEALTHCHECK --interval=10s --timeout=5s --start-period=120s --retries=6 \
  CMD wget -qO- http://127.0.0.1:8080/api/health/ready || exit 1

ENTRYPOINT ["dotnet", "SalesDashboard.Api.dll"]
