namespace FinanceTracker.Infrastructure.Database.Entities;

public sealed class CurrencyRateEntity
{
	public string BaseCode { get; init; } = String.Empty;
	public string TargetCode { get; init; } = String.Empty;
	public decimal Rate { get; init; }
	public DateOnly ActualAt { get; init; }
	public DateTime CreatedAt { get; init; }
}