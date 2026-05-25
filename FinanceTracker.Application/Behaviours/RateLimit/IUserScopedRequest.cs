namespace FinanceTracker.Application.Behaviours.RateLimit;

public interface IUserScopedRequest
{
	Guid UserId { get; }
}
