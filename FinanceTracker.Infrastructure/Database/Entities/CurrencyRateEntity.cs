using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Infrastructure.Database.Entities;

public sealed class CurrencyRateEntity
{
	public Currency BaseCode { get; init; }
	public Currency TargetCode { get; init; }
	public decimal Rate { get; init; }
	public DateOnly ActualAt { get; init; }
	public DateTime CreatedAt { get; init; }
}