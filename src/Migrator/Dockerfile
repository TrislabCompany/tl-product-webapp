# The migrator image, the init container of the api app. The same base images
# and user as src/Api/Dockerfile; it listens on no port. The build context is
# the repository root.

FROM mcr.microsoft.com/dotnet/sdk:10.0.404-noble@sha256:fe0fd4e93bd2fbb47c883b7a6369e5c8d3db8bf01cdc5981f6ee35fbdf3ff4fb AS build
WORKDIR /repo
COPY global.json ./
COPY src/Directory.Build.props src/Directory.Packages.props src/
COPY src/Api/Api.csproj src/Api/
COPY src/Migrator/Migrator.csproj src/Migrator/
RUN dotnet restore src/Migrator/Migrator.csproj
COPY src/Api/ src/Api/
COPY src/Migrator/ src/Migrator/
RUN dotnet publish src/Migrator/Migrator.csproj --configuration Release --no-restore --output /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0.15-noble-chiseled@sha256:7c625e44e5d588b4d7b2a913e94ca4bd5098ce47442da404829e4c7936a764e1
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
ENTRYPOINT ["dotnet", "Migrator.dll"]
