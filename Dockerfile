
# ==========================================
# BEBES NADADORES - BUILD
# ==========================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

COPY ["BebesNadadores.Domain/BebesNadadores.Domain.csproj", "BebesNadadores.Domain/"]
COPY ["BebesNadadores.Application/BebesNadadores.Application.csproj", "BebesNadadores.Application/"]
COPY ["BebesNadadores.Infrastructure/BebesNadadores.Infrastructure.csproj", "BebesNadadores.Infrastructure/"]
COPY ["BebesNadadores.Web/BebesNadadores.Web.csproj", "BebesNadadores.Web/"]
COPY ["BebesNadadores.Migrador/BebesNadadores.Migrador.csproj", "BebesNadadores.Migrador/"]

# Configuracion de NuGet
COPY ["NuGet.Config", "./"]

# Restaurar dependencias
RUN dotnet restore "BebesNadadores.Web/BebesNadadores.Web.csproj" \
    && dotnet restore "BebesNadadores.Migrador/BebesNadadores.Migrador.csproj"

COPY . .

# Publicar Web y Migrador
RUN dotnet publish "BebesNadadores.Web/BebesNadadores.Web.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false \
    && dotnet publish "BebesNadadores.Migrador/BebesNadadores.Migrador.csproj" \
    -c Release \
    -o /app/migrador \
    --no-restore \
    /p:UseAppHost=false


# ==========================================
# BEBES NADADORES - RUNTIME
# ==========================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

WORKDIR /app

# Dependencias del sistema, cliente MySQL
# y cliente PostgreSQL 17
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
       fontconfig \
       fonts-dejavu-core \
       postgresql-common \
       mariadb-client \
       curl \
       ca-certificates \
    && install -d /usr/share/postgresql-common/pgdg \
    && curl -fsSL https://www.postgresql.org/media/keys/ACCC4CF8.asc \
       -o /usr/share/postgresql-common/pgdg/apt.postgresql.org.asc \
    && . /etc/os-release \
    && echo "deb [signed-by=/usr/share/postgresql-common/pgdg/apt.postgresql.org.asc] https://apt.postgresql.org/pub/repos/apt ${VERSION_CODENAME}-pgdg main" \
       > /etc/apt/sources.list.d/pgdg.list \
    && apt-get update \
    && apt-get install -y --no-install-recommends \
       postgresql-client-17 \
    && rm -rf /var/lib/apt/lists/*

# Configuracion ASP.NET Core
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Cliente pg_dump correcto
ENV Migracion__PgDumpCli=/usr/lib/postgresql/17/bin/pg_dump

EXPOSE 8080

# Copiar Web y Migrador
COPY --from=build /app/publish .
COPY --from=build /app/migrador /app/migrador

# Directorios internos
RUN mkdir -p \
    /app/logs \
    /app/App_Data/Migraciones \
    /backups/bebes-nadadores

ENTRYPOINT ["dotnet", "BebesNadadores.Web.dll"]
