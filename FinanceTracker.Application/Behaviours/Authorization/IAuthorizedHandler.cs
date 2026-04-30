using MediatR;

namespace FinanceTracker.Application.Behaviours.Authorization;

public interface IAuthorizedHandler<in TRequest, in TEntity, TResponse> where TRequest : IRequest<TResponse>
{
	Task<TResponse> HandleAsync(
		TRequest request,
		TEntity entity,
		CancellationToken ct
	);
}

public interface IAuthorizedHandler<in TRequest, in TEntity> where TRequest : IRequest
{
	Task HandleAsync(
		TRequest request,
		TEntity entity,
		CancellationToken ct
	);
}