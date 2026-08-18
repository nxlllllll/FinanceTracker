namespace FinanceTracker.Api.Endpoints.Budgets.Contracts;

public sealed record ChangeBudgetPeriodRequest(DateOnly From, DateOnly To);
