FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["src/PartnerIntegrationBFF.Api/PartnerIntegrationBFF.Api.csproj", "src/PartnerIntegrationBFF.Api/"]
RUN dotnet restore "src/PartnerIntegrationBFF.Api/PartnerIntegrationBFF.Api.csproj"

COPY ["src/PartnerIntegrationBFF.Api/", "src/PartnerIntegrationBFF.Api/"]
COPY ["Directory.Build.props", "./"]
WORKDIR "/src/src/PartnerIntegrationBFF.Api"
RUN dotnet build "PartnerIntegrationBFF.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build --no-restore

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "PartnerIntegrationBFF.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false --no-restore

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "PartnerIntegrationBFF.Api.dll"]
