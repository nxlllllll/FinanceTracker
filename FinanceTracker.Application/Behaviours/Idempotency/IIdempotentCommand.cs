namespace FinanceTracker.Application.Behaviours.Idempotency;

public interface IIdempotentCommand
{
	Guid IdempotencyKey { get; }
}