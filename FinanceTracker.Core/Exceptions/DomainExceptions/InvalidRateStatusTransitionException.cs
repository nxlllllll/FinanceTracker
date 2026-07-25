using FinanceTracker.Core.Domains.Abstractions.Rate;

namespace FinanceTracker.Core.Exceptions.DomainExceptions;

[ErrorCode(code: "transaction.invalid_rate_status_transition")]
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
