namespace FinanceTracker.Infrastructure.Database.Context.Currency;

public sealed class CurrencyRateEntity
{
	public Core.ValueObjects.Currency BaseCode { get; init; }
	public Core.ValueObjects.Currency TargetCode { get; init; }
	public decimal Rate { get; init; }
	public DateOnly ActualAt { get; init; }
	public DateTimeOffset CreatedAt { get; init; }
}
