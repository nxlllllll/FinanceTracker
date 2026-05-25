using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Infrastructure.Database.Entities;

public sealed class CurrencyEntity
{
	public Currency Code { get; init; }
	public string Name { get; init; } = String.Empty;
	public string Symbol { get; init; } = String.Empty;
	public bool IsActive { get; init; }
}
