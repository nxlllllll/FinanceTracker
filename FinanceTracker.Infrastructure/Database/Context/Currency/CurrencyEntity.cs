namespace FinanceTracker.Infrastructure.Database.Context.Currency;

public sealed class CurrencyEntity
{
	public Core.ValueObjects.Currency Code { get; init; }
	public string Name { get; init; } = String.Empty;
	public string Symbol { get; init; } = String.Empty;
	public bool IsActive { get; init; }
}
