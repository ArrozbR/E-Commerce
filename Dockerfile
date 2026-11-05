FROM node:24-alpine AS css
WORKDIR /web

COPY src/KeycapStore.Web/package.json src/KeycapStore.Web/package-lock.json ./
RUN npm ci

COPY src/KeycapStore.Web/Styles Styles/
COPY src/KeycapStore.Web/Views Views/
COPY src/KeycapStore.Web/wwwroot/js wwwroot/js/
COPY src/KeycapStore.Web/scripts scripts/
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json Directory.Build.props ./
COPY src/KeycapStore.Domain/KeycapStore.Domain.csproj                 src/KeycapStore.Domain/
COPY src/KeycapStore.Application/KeycapStore.Application.csproj       src/KeycapStore.Application/
COPY src/KeycapStore.Infrastructure/KeycapStore.Infrastructure.csproj src/KeycapStore.Infrastructure/
COPY src/KeycapStore.Web/KeycapStore.Web.csproj                       src/KeycapStore.Web/
RUN dotnet restore src/KeycapStore.Web/KeycapStore.Web.csproj

COPY src/ src/
COPY --from=css /web/wwwroot/css/app.css src/KeycapStore.Web/wwwroot/css/app.css
COPY --from=css /web/wwwroot/lib/gsap src/KeycapStore.Web/wwwroot/lib/gsap/
RUN dotnet publish src/KeycapStore.Web/KeycapStore.Web.csproj \
    --configuration Release --no-restore --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

USER $APP_UID

EXPOSE 8080

ENTRYPOINT ["dotnet", "KeycapStore.Web.dll"]