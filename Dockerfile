FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json Marketplace.slnx ./
COPY src/Marketplace.Web/Marketplace.Web.csproj src/Marketplace.Web/
RUN dotnet restore src/Marketplace.Web/Marketplace.Web.csproj
COPY src/Marketplace.Web/ src/Marketplace.Web/
RUN dotnet publish src/Marketplace.Web/Marketplace.Web.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /app/App_Data /app/wwwroot/uploads /home/app/.aspnet/DataProtection-Keys \
    && chown -R app:app /app/App_Data /app/wwwroot/uploads /home/app/.aspnet
COPY --chown=app:app --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Development \
    DOTNET_EnableDiagnostics=0
EXPOSE 8080
USER app
HEALTHCHECK --interval=10s --timeout=3s --start-period=10s --retries=5 CMD curl --fail --silent http://127.0.0.1:8080/health/ready || exit 1
ENTRYPOINT ["dotnet", "Marketplace.Web.dll"]
