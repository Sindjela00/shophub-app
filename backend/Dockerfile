FROM node:22-alpine AS frontend-build
WORKDIR /frontend
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci
COPY frontend/ .
# Already defaults to a same-origin-relative '/api' when unset (see lib/shop-sites-api.ts) —
# set explicitly anyway so that default staying correct isn't just an accident nobody documented.
ENV VITE_API_URL=/api
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY backend/src/ShopHub.Api/ShopHub.Api.csproj backend/src/ShopHub.Api/
RUN dotnet restore backend/src/ShopHub.Api/ShopHub.Api.csproj

COPY backend/src/ShopHub.Api/ backend/src/ShopHub.Api/
RUN dotnet publish backend/src/ShopHub.Api/ShopHub.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
COPY --from=frontend-build /frontend/dist ./wwwroot
USER $APP_UID
ENTRYPOINT ["dotnet", "ShopHub.Api.dll"]
