# syntax=docker/dockerfile:1

# ---------- Build ----------
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copia só os .csproj primeiro: a camada de restore fica em cache enquanto as dependências não mudam.
COPY src/SistemaGestaoInfra.Domain/SistemaGestaoInfra.Domain.csproj           src/SistemaGestaoInfra.Domain/
COPY src/SistemaGestaoInfra.Application/SistemaGestaoInfra.Application.csproj src/SistemaGestaoInfra.Application/
COPY src/SistemaGestaoInfra.Infra/SistemaGestaoInfra.Infra.csproj             src/SistemaGestaoInfra.Infra/
COPY src/SistemaGestaoInfra.Web/SistemaGestaoInfra.Web.csproj                 src/SistemaGestaoInfra.Web/
RUN dotnet restore src/SistemaGestaoInfra.Web/SistemaGestaoInfra.Web.csproj

COPY src/ src/
RUN dotnet publish src/SistemaGestaoInfra.Web/SistemaGestaoInfra.Web.csproj \
    -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---------- Runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Dados persistentes (uploads e chaves de cookie). Monte um volume aqui.
# A pasta é criada e entregue ao usuário não-root ANTES de declarar o volume,
# para que volumes novos herdem a permissão correta.
RUN mkdir -p /app/App_Data && chown -R $APP_UID:$APP_UID /app/App_Data
VOLUME /app/App_Data

COPY --from=build --chown=$APP_UID:$APP_UID /app/publish .

ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_EnableDiagnostics=0

# Não roda como root.
USER $APP_UID
EXPOSE 8080

ENTRYPOINT ["dotnet", "SistemaGestaoInfra.Web.dll"]
