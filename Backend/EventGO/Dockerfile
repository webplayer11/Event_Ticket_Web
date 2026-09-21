FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY EventGO.Domain/EventGO.Domain.csproj EventGO.Domain/
COPY EventGO.Application/EventGO.Application.csproj EventGO.Application/
COPY EventGO.Infrastructure/EventGO.Infrastructure.csproj EventGO.Infrastructure/
COPY EventGO.API/EventGO.API.csproj EventGO.API/

RUN dotnet restore EventGO.API/EventGO.API.csproj

COPY . .

RUN dotnet publish EventGO.API/EventGO.API.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build /app/publish .

USER $APP_UID

ENTRYPOINT ["dotnet", "EventGO.API.dll"]