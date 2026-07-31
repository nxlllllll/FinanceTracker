namespace FinanceTracker.Application.Behaviours.Idempotency;

public enum IdempotencyAcquisitionKind
{
	CachedResponse,
	Reserved,
	Failed
}
