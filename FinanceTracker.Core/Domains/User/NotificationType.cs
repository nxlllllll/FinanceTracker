namespace FinanceTracker.Core.Domains.User;

/// <summary>
/// How a user is told about events on their own data. A user without one is not notified at all.
/// Mirrors the <c>notification_types</c> lookup table.
/// </summary>
public enum NotificationType
{
	/// <summary>Sent to the address the user signs in with.</summary>
	Email
}
