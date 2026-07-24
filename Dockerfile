FROM node:22-alpine AS frontend
WORKDIR /src/web
COPY web/package.json web/package-lock.json ./
RUN npm ci
COPY web/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY backend/src/Api/Api.csproj backend/src/Api/
RUN dotnet restore backend/src/Api/Api.csproj
COPY backend/src/Api/ backend/src/Api/
RUN dotnet publish backend/src/Api/Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    -p:SkipSpaBuild=true

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish .
COPY --from=frontend /src/web/dist ./wwwroot
USER $APP_UID
ENTRYPOINT ["dotnet", "Api.dll"]
