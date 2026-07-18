# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY LicenseAuthority.csproj ./
RUN dotnet restore LicenseAuthority.csproj

COPY . ./
RUN dotnet publish LicenseAuthority.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_FORWARDEDHEADERS_ENABLED=true \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DataProtection__KeysPath=/tmp/hostelpro-authority/DataProtectionKeys

COPY --from=build /app/publish .
COPY docker-entrypoint.sh /usr/local/bin/hostelpro-authority-entrypoint.sh
RUN chmod +x /usr/local/bin/hostelpro-authority-entrypoint.sh

EXPOSE 8080
ENTRYPOINT ["/usr/local/bin/hostelpro-authority-entrypoint.sh", "LicenseAuthority.dll"]
