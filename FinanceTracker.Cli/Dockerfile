FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
WORKDIR /app
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["Directory.Packages.props", "Directory.Build.props", "BannedSymbols.txt", "./"]
COPY ["FinanceTracker.Cli/FinanceTracker.Cli.csproj", "FinanceTracker.Cli/"]
COPY ["FinanceTracker.Application/FinanceTracker.Application.csproj", "FinanceTracker.Application/"]
COPY ["FinanceTracker.Contracts/FinanceTracker.Contracts.csproj", "FinanceTracker.Contracts/"]
COPY ["FinanceTracker.Core/FinanceTracker.Core.csproj", "FinanceTracker.Core/"]
COPY ["FinanceTracker.Infrastructure/FinanceTracker.Infrastructure.csproj", "FinanceTracker.Infrastructure/"]
RUN dotnet restore "FinanceTracker.Cli/FinanceTracker.Cli.csproj"
COPY . .
RUN dotnet publish "FinanceTracker.Cli/FinanceTracker.Cli.csproj" -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
USER app
ENTRYPOINT ["dotnet", "FinanceTracker.Cli.dll"]
