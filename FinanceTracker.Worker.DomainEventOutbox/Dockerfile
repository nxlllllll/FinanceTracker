FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 5109

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["FinanceTracker.Worker.DomainEventOutbox/FinanceTracker.Worker.DomainEventOutbox.csproj", "FinanceTracker.Worker.DomainEventOutbox/"]
COPY ["FinanceTracker.Contracts/FinanceTracker.Contracts.csproj", "FinanceTracker.Contracts/"]
COPY ["FinanceTracker.Core/FinanceTracker.Core.csproj", "FinanceTracker.Core/"]
COPY ["FinanceTracker.Infrastructure/FinanceTracker.Infrastructure.csproj", "FinanceTracker.Infrastructure/"]
COPY ["FinanceTracker.Worker.Shared/FinanceTracker.Worker.Shared.csproj", "FinanceTracker.Worker.Shared/"]
RUN dotnet restore "FinanceTracker.Worker.DomainEventOutbox/FinanceTracker.Worker.DomainEventOutbox.csproj"
COPY . .
RUN dotnet publish "FinanceTracker.Worker.DomainEventOutbox/FinanceTracker.Worker.DomainEventOutbox.csproj" -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "FinanceTracker.Worker.DomainEventOutbox.dll"]