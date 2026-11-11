FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["BebesNadadores.Domain/BebesNadadores.Domain.csproj", "BebesNadadores.Domain/"]
COPY ["BebesNadadores.Application/BebesNadadores.Application.csproj", "BebesNadadores.Application/"]
COPY ["BebesNadadores.Infrastructure/BebesNadadores.Infrastructure.csproj", "BebesNadadores.Infrastructure/"]
COPY ["BebesNadadores.Web/BebesNadadores.Web.csproj", "BebesNadadores.Web/"]
COPY ["BebesNadadores.Migrador/BebesNadadores.Migrador.csproj", "BebesNadadores.Migrador/"]

# Si el repositorio usa NuGet.Config, copiarlo para las restauraciones.
COPY ["NuGet.Config", "./"]
RUN dotnet restore "BebesNadadores.Web/BebesNadadores.Web.csproj" \
    && dotnet restore "BebesNadadores.Migrador/BebesNadadores.Migrador.csproj"

COPY . .
RUN dotnet publish "BebesNadadores.Web/BebesNadadores.Web.csproj" \
    -c Release -o /app/publish --no-restore /p:UseAppHost=false \
    && dotnet publish "BebesNadadores.Migrador/BebesNadadores.Migrador.csproj" \
    -c Release -o /app/migrador --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
       fontconfig fonts-dejavu-core postgresql-client mariadb-client \
    && rm -rf /var/lib/apt/lists/*

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
COPY --from=build /app/publish .
COPY --from=build /app/migrador /app/migrador
RUN mkdir -p /app/logs /app/App_Data/Migraciones /backups/bebes-nadadores
ENTRYPOINT ["dotnet", "BebesNadadores.Web.dll"]
