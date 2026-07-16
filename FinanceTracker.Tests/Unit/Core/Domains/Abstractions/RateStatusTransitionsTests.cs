using FinanceTracker.Core.Domains.Abstractions.Rate;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;

namespace FinanceTracker.Tests.Unit.Core.Domains.Abstractions;

public sealed class RateStatusTransitionsTests
{
	private static readonly RateStatus[] AllStatuses = Enum.GetValues<RateStatus>();

	private static readonly RateStatus[] LegalTargetsFromPending =
	[
		RateStatus.Resolved,
		RateStatus.Approximated,
		RateStatus.Unresolvable,
		RateStatus.Cancelled
	];

	[Test]
	public async Task IsOpen_ShouldBeTrue_ForPendingOnly()
	{
		foreach (RateStatus status in AllStatuses)
		{
			bool expected = status == RateStatus.Pending;

			await Assert.That(value: status.IsOpen()).IsEqualTo(expected: expected)
				.Because(message: $"{status}.IsOpen() must be {expected} — only Pending is a state the adjustment job may act on.");
		}
	}

	[Test]
	public async Task To_FromPending_ShouldAllowExactlyTheFourTerminalStates()
	{
		foreach (RateStatus target in AllStatuses)
		{
			Result<RateStatus, DomainException> result = RateStatusTransitions.To(from: RateStatus.Pending, to: target);

			bool shouldBeLegal = LegalTargetsFromPending.Contains(value: target);

			await Assert.That(value: result.IsSuccess).IsEqualTo(expected: shouldBeLegal)
				.Because(message: $"Pending → {target} must be {(shouldBeLegal ? "legal" : "rejected")}.");
		}
	}

	[Test]
	public async Task To_FromAnyTerminalState_ShouldRejectEveryTarget()
	{
		IEnumerable<RateStatus> terminalStates = AllStatuses.Where(predicate: status => status != RateStatus.Pending);

		foreach (RateStatus from in terminalStates)
		{
			foreach (RateStatus to in AllStatuses)
			{
				Result<RateStatus, DomainException> result = RateStatusTransitions.To(from: from, to: to);

				await Assert.That(value: result.IsFailure).IsTrue()
					.Because(message: $"{from} is terminal — {from} → {to} must be rejected, including the no-op {from} → {from}.");

				await Assert.That(value: result.Error).IsTypeOf<InvalidRateStatusTransitionException>();
			}
		}
	}

	[Test]
	public async Task To_WhenRejected_ShouldCarryBothEndsOfTheAttemptedMove()
	{
		Result<RateStatus, DomainException> result = RateStatusTransitions.To(from: RateStatus.Cancelled, to: RateStatus.Resolved);

		InvalidRateStatusTransitionException error = (InvalidRateStatusTransitionException)result.Error!;

		await Assert.That(value: error.From).IsEqualTo(expected: RateStatus.Cancelled);
		await Assert.That(value: error.To).IsEqualTo(expected: RateStatus.Resolved);
	}
}
