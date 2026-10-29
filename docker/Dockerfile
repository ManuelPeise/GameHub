# syntax=docker/dockerfile:1
# Image gamehub-app: API with the built frontend (wwwroot) and the database tool (dbtools) for migrations.
# Build from the repository root:  docker build -f docker/Dockerfile -t gamehub-app .

ARG DOTNET_VERSION=10.0

# 1) Frontend
FROM node:22-alpine AS web
WORKDIR /src/Web.App
COPY sources/Web.App/package.json sources/Web.App/package-lock.json ./
RUN npm ci
COPY sources/Web.App/ ./
RUN npm run build

# 2) API and database tool
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
WORKDIR /src
COPY sources/ ./
RUN dotnet publish Services.Api/Services.Api.csproj --configuration Release --output /out/api /p:UseAppHost=false \
 && dotnet publish Services.DbTools/Services.DbTools.csproj --configuration Release --output /out/dbtools /p:UseAppHost=false

# EF Core migration bundle (US-030): applies migrations without SDK and without starting the app.
RUN dotnet tool restore \
 && dotnet ef migrations bundle --project Data.Database --startup-project Data.Database \
      --configuration Release --output /out/efbundle --force

# 3) Runtime with PostgreSQL client tools (pg_dump/pg_restore for backups, US-027)
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS final
RUN apt-get update \
 && apt-get install --yes --no-install-recommends postgresql-client \
 && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /out/api ./
COPY --from=build /out/dbtools ./dbtools/
COPY --from=web /src/Web.App/dist ./wwwroot/
COPY --from=build /out/efbundle ./efbundle
COPY --chmod=755 docker/migrate.sh ./migrate.sh

# Volumes for data protection keys (sign-ins survive updates) and backups.
RUN mkdir -p /keys /backups && chown "$APP_UID" /keys /backups

ENV ASPNETCORE_HTTP_PORTS=8080 \
    App__Security__DataProtectionKeysPath=/keys \
    App__Backup__Directory=/backups

USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Services.Api.dll"]
