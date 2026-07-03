FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["Directory.Packages.props", "Directory.Packages.props"]
COPY ["FinanceTracker.Migrator/FinanceTracker.Migrator.csproj", "FinanceTracker.Migrator/"]
RUN dotnet restore "FinanceTracker.Migrator/FinanceTracker.Migrator.csproj"
COPY ["FinanceTracker.Migrator/", "FinanceTracker.Migrator/"]
RUN dotnet publish "FinanceTracker.Migrator/FinanceTracker.Migrator.csproj" -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "FinanceTracker.Migrator.dll"]