using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;

namespace FinanceTracker.Core.Domains.Abstractions.Rate;

public interface IRateSettleable
{
	Guid Id { get; }
	decimal ExchangeRate { get; }
	RateStatus RateStatus { get; }

	Result<Unit, DomainException> ResolveRate(decimal newRate, DateTimeOffset changedAt);
	Result<Unit, DomainException> ApproximateRate(DateTimeOffset changedAt);
	Result<Unit, DomainException> MarkRateUnresolvable(DateTimeOffset changedAt);
}
