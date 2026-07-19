namespace FinanceTracker.Api.Infrastructure;

/// <summary>Identity of the authenticated caller, extracted from validated JWT claims.</summary>
public interface ICurrentUserProvider
{
	/// <summary>User id from the <c>sub</c> claim.</summary>
	Guid UserId { get; }

	/// <summary>Session id from the <c>sid</c> claim — enables per-session operations (e.g. revoke).</summary>
	Guid SessionId { get; }
}
