using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;

namespace FinanceTracker.Core.Domains.Abstractions.Rate;

/// <summary>
/// The single source of truth for which rate lifecycle transitions are legal.
/// </summary>
public static class RateStatusTransitions
{
	public static bool IsOpen(this RateStatus status)
		=> status == RateStatus.Pending;

	/// <summary>
	/// Validates a move from <paramref name="from"/> to <paramref name="to"/>.
	/// Returns the target state on success so callers can assign directly:
	/// <c>RateStatus = transition.Value!;</c>
	/// </summary>
	public static Result<RateStatus, DomainException> To(RateStatus from, RateStatus to)
	{
		if (!IsLegal(from: from, to: to))
		{
			return Result<RateStatus, DomainException>.Failure(error: new InvalidRateStatusTransitionException(
				message: $"Rate status cannot move from {from} to {to}. Only {RateStatus.Pending} may transition.",
				from: from,
				to: to
			));
		}

		return Result<RateStatus, DomainException>.Success(value: to);
	}

	private static bool IsLegal(RateStatus from, RateStatus to)
	{
		if (from != RateStatus.Pending)
			return false;

		return to is RateStatus.Resolved
			or RateStatus.Approximated
			or RateStatus.Unresolvable
			or RateStatus.Cancelled;
	}
}
