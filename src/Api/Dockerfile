# The api image. Stage 1 publishes the API with the .NET SDK. Stage 2 is the
# chiseled ASP.NET runtime: no shell, no package manager, a non-root user, and
# the API on port 8080. Both base images are pinned by digest; Renovate bumps
# them from roadmap phase 12 on. The build context is the repository root.
# The Trislab packages come from GitHub Packages, which asks for a token: the build gets it as the BuildKit
# secret `packages` (docker build --secret id=packages,env=PACKAGES_TOKEN), and no layer keeps it.

FROM mcr.microsoft.com/dotnet/sdk:11.0.100-resolute@sha256:2794448a07118a6eaf00f29ab5d2bf44928517ce5ff86505a63a69230374f599 AS build
WORKDIR /repo
# Restores first, from the project files only, so the layer is reused while
# the dependencies stay the same.
COPY global.json nuget.config ./
COPY src/Directory.Build.props src/Directory.Packages.props src/
COPY src/Api/Api.csproj src/Api/
RUN --mount=type=secret,id=packages,env=PACKAGES_TOKEN dotnet restore src/Api/Api.csproj
COPY src/Api/ src/Api/
RUN dotnet publish src/Api/Api.csproj --configuration Release --no-restore --output /app

FROM mcr.microsoft.com/dotnet/aspnet:11.0.0-resolute-chiseled@sha256:045ba65412b8edcc2f70a779163bf4353d450f72535a0dddf6188d6d945bfaef
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Api.dll"]
