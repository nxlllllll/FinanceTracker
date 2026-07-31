FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 5005
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["Directory.Packages.props", "Directory.Build.props", "BannedSymbols.txt", "./"]
COPY ["FinanceTracker.Worker.TransferProjection/FinanceTracker.Worker.TransferProjection.csproj", "FinanceTracker.Worker.TransferProjection/"]
COPY ["FinanceTracker.Contracts/FinanceTracker.Contracts.csproj", "FinanceTracker.Contracts/"]
COPY ["FinanceTracker.Core/FinanceTracker.Core.csproj", "FinanceTracker.Core/"]
COPY ["FinanceTracker.Infrastructure/FinanceTracker.Infrastructure.csproj", "FinanceTracker.Infrastructure/"]
COPY ["FinanceTracker.Worker.Shared/FinanceTracker.Worker.Shared.csproj", "FinanceTracker.Worker.Shared/"]
RUN dotnet restore "FinanceTracker.Worker.TransferProjection/FinanceTracker.Worker.TransferProjection.csproj"
COPY . .
RUN dotnet publish "FinanceTracker.Worker.TransferProjection/FinanceTracker.Worker.TransferProjection.csproj" -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
USER app
ENTRYPOINT ["dotnet", "FinanceTracker.Worker.TransferProjection.dll"]