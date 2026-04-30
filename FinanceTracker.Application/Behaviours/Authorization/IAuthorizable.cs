namespace FinanceTracker.Application.Behaviours.Authorization;

public interface IAuthorizable
{
	Guid UserId { get; }
}