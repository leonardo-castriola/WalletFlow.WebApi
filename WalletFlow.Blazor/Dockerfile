FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["WalletFlow.Blazor/WalletFlow.Blazor/WalletFlow.Blazor.csproj", "WalletFlow.Blazor/WalletFlow.Blazor/"]
COPY ["WalletFlow.Blazor/WalletFlow.Blazor.Client/WalletFlow.Blazor.Client.csproj", "WalletFlow.Blazor/WalletFlow.Blazor.Client/"]
COPY ["WalletFlow.Application/WalletFlow.Application.csproj", "WalletFlow.Application/"]
COPY ["WalletFlow.Domain/WalletFlow.Domain.csproj", "WalletFlow.Domain/"]
COPY ["WalletFlow.Infrastructure/WalletFlow.Infrastructure.csproj", "WalletFlow.Infrastructure/"]
RUN dotnet restore "WalletFlow.Blazor/WalletFlow.Blazor/WalletFlow.Blazor.csproj"

COPY ["WalletFlow.Blazor/", "WalletFlow.Blazor/"]
COPY ["WalletFlow.Application/", "WalletFlow.Application/"]
COPY ["WalletFlow.Domain/", "WalletFlow.Domain/"]
COPY ["WalletFlow.Infrastructure/", "WalletFlow.Infrastructure/"]

WORKDIR "/src/WalletFlow.Blazor/WalletFlow.Blazor"
RUN dotnet publish "WalletFlow.Blazor.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# libfontconfig1 e fonts-dejavu-core garantem suporte de fontes para geração de PDF pelo QuestPDF em Linux
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl tzdata libfontconfig1 fonts-dejavu-core \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /data/dpkeys \
    && chown -R $APP_UID:$APP_UID /data /app

USER $APP_UID
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish .

HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
    CMD curl -fsS http://127.0.0.1:8080/health || exit 1

ENTRYPOINT ["dotnet", "WalletFlow.Blazor.dll"]
