# Imagem única: frontend React (estático) + API .NET 10, servidos pelo mesmo processo.
# Build a partir da raiz do repositório: docker build -f deploy/Dockerfile -t easyos .

FROM node:24-alpine AS frontend
WORKDIR /src/frontend
COPY src/frontend/package.json src/frontend/package-lock.json ./
RUN npm ci
COPY src/frontend/ ./
# O vite grava em ../backend/EasyOS.Api/wwwroot
RUN npx tsc -b && npx vite build --outDir /out/wwwroot --emptyOutDir

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend
WORKDIR /src
COPY src/backend/ ./
RUN dotnet publish EasyOS.Api/EasyOS.Api.csproj -c Release -o /out/app /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=backend /out/app ./
COPY --from=frontend /out/wwwroot ./wwwroot
# /app/data: catálogo (SQLite), chaves do Data Protection e storage local. Precisa de volume e backup.
RUN mkdir -p /app/data && chown -R app:app /app/data
USER app
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    ConnectionStrings__Catalog="Data Source=/app/data/catalog.db" \
    DataProtection__KeysPath=/app/data/keys
EXPOSE 8080
ENTRYPOINT ["dotnet", "EasyOS.Api.dll"]
