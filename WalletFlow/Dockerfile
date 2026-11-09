FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["WalletFlow/WalletFlow.csproj", "WalletFlow/"]
COPY ["WalletFlow.Application/WalletFlow.Application.csproj", "WalletFlow.Application/"]
COPY ["WalletFlow.Domain/WalletFlow.Domain.csproj", "WalletFlow.Domain/"]
COPY ["WalletFlow.Infrastructure/WalletFlow.Infrastructure.csproj", "WalletFlow.Infrastructure/"]
RUN dotnet restore "WalletFlow/WalletFlow.csproj"

COPY ["WalletFlow/", "WalletFlow/"]
COPY ["WalletFlow.Application/", "WalletFlow.Application/"]
COPY ["WalletFlow.Domain/", "WalletFlow.Domain/"]
COPY ["WalletFlow.Infrastructure/", "WalletFlow.Infrastructure/"]

WORKDIR "/src/WalletFlow"
RUN dotnet publish "WalletFlow.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl tzdata \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /data \
    && chown -R $APP_UID:$APP_UID /data /app

USER $APP_UID
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish .

HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
    CMD curl -fsS http://127.0.0.1:8080/health || exit 1

ENTRYPOINT ["dotnet", "WalletFlow.dll"]
