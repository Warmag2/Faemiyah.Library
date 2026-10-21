FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS base
WORKDIR /app

FROM base AS build
WORKDIR /build
COPY ["Directory.Build.props", "."]
COPY ["README_Common.md", "README_Repository.md", "README_Ui.md", "./"]
COPY ["src/", "src/"]
COPY ["infra/sdk/", "."]
RUN mkdir -p /build/customnugets
WORKDIR "/build/src/Faemiyah.Common"
RUN dotnet build "Faemiyah.Common.csproj" -c Release
RUN dotnet pack "Faemiyah.Common.csproj" -c Release -o /build/customnugets
WORKDIR "/build/src/Faemiyah.Common.Hosting"
RUN dotnet build "Faemiyah.Common.Hosting.csproj" -c Release
RUN dotnet pack "Faemiyah.Common.Hosting.csproj" -c Release -o /build/customnugets
WORKDIR "/build/src/Faemiyah.Repository"
RUN dotnet build "Faemiyah.Repository.csproj" -c Release
RUN dotnet pack "Faemiyah.Repository.csproj" -c Release -o /build/customnugets
WORKDIR "/build/src/Faemiyah.Ui"
RUN dotnet build "Faemiyah.Ui.csproj" -c Release
RUN dotnet pack "Faemiyah.Ui.csproj" -c Release -o /build/customnugets

FROM base AS final
WORKDIR /build
COPY ["infra/sdk/", "."]
COPY --from=build ["/build/customnugets/*.nupkg", "customnugets/"]
