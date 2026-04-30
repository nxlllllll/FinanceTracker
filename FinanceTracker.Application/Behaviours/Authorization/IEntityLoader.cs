namespace FinanceTracker.Application.Behaviours.Authorization;

public interface IEntityLoader<in TRequest, TEntity> where TRequest : IAuthorizable
{
	Task<TEntity> LoadAsync(
		TRequest request,
		CancellationToken ct
	);
}