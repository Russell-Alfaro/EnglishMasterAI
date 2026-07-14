# ─── Etapa 1: Compilación ────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY EnglishMasterAI.slnx .
COPY src/EnglishMasterAI.API/EnglishMasterAI.API.csproj src/EnglishMasterAI.API/
COPY src/EnglishMasterAI.Application/EnglishMasterAI.Application.csproj src/EnglishMasterAI.Application/
COPY src/EnglishMasterAI.Domain/EnglishMasterAI.Domain.csproj src/EnglishMasterAI.Domain/
COPY src/EnglishMasterAI.Infrastructure/EnglishMasterAI.Infrastructure.csproj src/EnglishMasterAI.Infrastructure/

RUN dotnet restore src/EnglishMasterAI.API/EnglishMasterAI.API.csproj

COPY src/EnglishMasterAI.API/ src/EnglishMasterAI.API/
COPY src/EnglishMasterAI.Application/ src/EnglishMasterAI.Application/
COPY src/EnglishMasterAI.Domain/ src/EnglishMasterAI.Domain/
COPY src/EnglishMasterAI.Infrastructure/ src/EnglishMasterAI.Infrastructure/

RUN dotnet publish src/EnglishMasterAI.API/EnglishMasterAI.API.csproj -c Release -o /app/publish

# ─── Etapa 2: Runtime ────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENTRYPOINT ASPNETCORE_URLS=http://+:$PORT dotnet EnglishMasterAI.API.dll