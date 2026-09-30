FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json Directory.Build.props ./
COPY src/KeycapStore.Domain/KeycapStore.Domain.csproj                 src/KeycapStore.Domain/
COPY src/KeycapStore.Application/KeycapStore.Application.csproj       src/KeycapStore.Application/
COPY src/KeycapStore.Infrastructure/KeycapStore.Infrastructure.csproj src/KeycapStore.Infrastructure/
COPY src/KeycapStore.Web/KeycapStore.Web.csproj                       src/KeycapStore.Web/
RUN dotnet restore src/KeycapStore.Web/KeycapStore.Web.csproj

COPY src/ src/
RUN dotnet publish src/KeycapStore.Web/KeycapStore.Web.csproj \
    --configuration Release --no-restore --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

USER $APP_UID

EXPOSE 8080

ENTRYPOINT ["dotnet", "KeycapStore.Web.dll"]