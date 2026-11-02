FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

COPY ["BebesNadadores.Domain/BebesNadadores.Domain.csproj", "BebesNadadores.Domain/"]
COPY ["BebesNadadores.Application/BebesNadadores.Application.csproj", "BebesNadadores.Application/"]
COPY ["BebesNadadores.Infrastructure/BebesNadadores.Infrastructure.csproj", "BebesNadadores.Infrastructure/"]
COPY ["BebesNadadores.Web/BebesNadadores.Web.csproj", "BebesNadadores.Web/"]

RUN dotnet restore "BebesNadadores.Web/BebesNadadores.Web.csproj"

COPY . .

RUN dotnet publish \
    "BebesNadadores.Web/BebesNadadores.Web.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false


FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
       fontconfig \
       fonts-dejavu-core \
    && rm -rf /var/lib/apt/lists/*

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

COPY --from=build /app/publish .

RUN mkdir -p /app/logs

ENTRYPOINT ["dotnet", "BebesNadadores.Web.dll"]