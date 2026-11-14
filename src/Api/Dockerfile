# The api image. Stage 1 publishes the API with the .NET SDK. Stage 2 is the
# chiseled ASP.NET runtime: no shell, no package manager, a non-root user, and
# the API on port 8080. Both base images are pinned by digest; Renovate bumps
# them from roadmap phase 12 on. The build context is the repository root.

FROM mcr.microsoft.com/dotnet/sdk:10.0.404-noble@sha256:fe0fd4e93bd2fbb47c883b7a6369e5c8d3db8bf01cdc5981f6ee35fbdf3ff4fb AS build
WORKDIR /repo
# Restores first, from the project files only, so the layer is reused while
# the dependencies stay the same.
COPY global.json ./
COPY src/Directory.Build.props src/Directory.Packages.props src/
COPY src/Api/Api.csproj src/Api/
RUN dotnet restore src/Api/Api.csproj
COPY src/Api/ src/Api/
RUN dotnet publish src/Api/Api.csproj --configuration Release --no-restore --output /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0.15-noble-chiseled@sha256:7c625e44e5d588b4d7b2a913e94ca4bd5098ce47442da404829e4c7936a764e1
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Api.dll"]
