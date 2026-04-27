# Multi-stage build for the CareerForge API.
# Stage 1 restores and publishes the application; stage 2 produces the runtime image.

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Restore is performed in its own layer, keyed only on project manifests, so the
# package cache survives source-only changes.
COPY CareerForge.sln ./
COPY src/CareerForge.Api/CareerForge.Api.csproj            src/CareerForge.Api/
COPY src/CareerForge.Application/CareerForge.Application.csproj src/CareerForge.Application/
COPY src/CareerForge.Domain/CareerForge.Domain.csproj           src/CareerForge.Domain/
COPY src/CareerForge.Infrastructure/CareerForge.Infrastructure.csproj src/CareerForge.Infrastructure/
COPY tests/CareerForge.Api.Tests/CareerForge.Api.Tests.csproj             tests/CareerForge.Api.Tests/
COPY tests/CareerForge.Application.Tests/CareerForge.Application.Tests.csproj tests/CareerForge.Application.Tests/

RUN dotnet restore src/CareerForge.Api/CareerForge.Api.csproj

COPY src/ src/
RUN dotnet publish src/CareerForge.Api/CareerForge.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# ---

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

USER app

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "CareerForge.Api.dll"]
