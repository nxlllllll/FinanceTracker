using FinanceTracker.Core.Domains.Abstractions.Rate;

namespace FinanceTracker.Core.Exceptions.DomainExceptions;

public sealed class InvalidRateStatusTransitionException(
	string message,
	RateStatus from,
	RateStatus to
) : DomainException(message: message)
{
	/// <summary>The state the operation was actually in when the transition was attempted.</summary>
	public RateStatus From { get; } = from;

	/// <summary>The state the caller tried to move it to.</summary>
	public RateStatus To { get; } = to;
}
