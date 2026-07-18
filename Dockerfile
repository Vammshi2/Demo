# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY HostelPro.csproj ./
RUN dotnet restore HostelPro.csproj

COPY . ./
RUN dotnet publish HostelPro.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_FORWARDEDHEADERS_ENABLED=true \
    DOTNET_RUNNING_IN_CONTAINER=true \
    Storage__DataPath=/tmp/hostelpro-data \
    DataProtection__KeysPath=/tmp/hostelpro-data/DataProtectionKeys

COPY --from=build /app/publish .
COPY docker-entrypoint.sh /usr/local/bin/hostelpro-entrypoint.sh
RUN chmod +x /usr/local/bin/hostelpro-entrypoint.sh

EXPOSE 8080
ENTRYPOINT ["/usr/local/bin/hostelpro-entrypoint.sh", "HostelPro.dll"]
