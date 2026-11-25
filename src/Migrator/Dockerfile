# The migrator image, the init container of the api app. The same base images
# and user as src/Api/Dockerfile; it listens on no port. The build context is
# the repository root. The packages come with the BuildKit secret `packages`, as in src/Api/Dockerfile.

FROM mcr.microsoft.com/dotnet/sdk:11.0.100-resolute@sha256:2794448a07118a6eaf00f29ab5d2bf44928517ce5ff86505a63a69230374f599 AS build
WORKDIR /repo
COPY global.json nuget.config ./
COPY src/Directory.Build.props src/Directory.Packages.props src/
COPY src/Api/Api.csproj src/Api/
COPY src/Migrator/Migrator.csproj src/Migrator/
RUN --mount=type=secret,id=packages,env=PACKAGES_TOKEN dotnet restore src/Migrator/Migrator.csproj
COPY src/Api/ src/Api/
COPY src/Migrator/ src/Migrator/
RUN dotnet publish src/Migrator/Migrator.csproj --configuration Release --no-restore --output /app

FROM mcr.microsoft.com/dotnet/aspnet:11.0.0-resolute-chiseled@sha256:045ba65412b8edcc2f70a779163bf4353d450f72535a0dddf6188d6d945bfaef
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
ENTRYPOINT ["dotnet", "Migrator.dll"]
