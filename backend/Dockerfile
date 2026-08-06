FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/ShopHub.Api/ShopHub.Api.csproj src/ShopHub.Api/
RUN dotnet restore src/ShopHub.Api/ShopHub.Api.csproj

COPY src/ShopHub.Api/ src/ShopHub.Api/
RUN dotnet publish src/ShopHub.Api/ShopHub.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
ENTRYPOINT ["dotnet", "ShopHub.Api.dll"]
