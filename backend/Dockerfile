# syntax=docker/dockerfile:1

# ---------- Build ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first for better layer caching.
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/ECommerce.Core/ECommerce.Core.csproj src/ECommerce.Core/
COPY src/ECommerce.Infrastructure/ECommerce.Infrastructure.csproj src/ECommerce.Infrastructure/
COPY src/ECommerce.Api/ECommerce.Api.csproj src/ECommerce.Api/
RUN dotnet restore src/ECommerce.Api/ECommerce.Api.csproj

COPY src/ src/
RUN dotnet publish src/ECommerce.Api/ECommerce.Api.csproj -c Release -o /app --no-restore /p:UseAppHost=false

# ---------- Runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    ASPNETCORE_ENVIRONMENT=Production
COPY --from=build /app .

# Built-in non-root user provided by the official .NET images.
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "ECommerce.Api.dll"]
