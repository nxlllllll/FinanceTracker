using MediatR;

namespace FinanceTracker.Application.Behaviours.Authorization;

public sealed class AuthorizedHandlerAdapter<TRequest, TEntity, TResponse>(
	IEntityLoader<TRequest, TEntity> loader,
	IAuthorizedHandler<TRequest, TEntity, TResponse> handler
) : IRequestHandler<TRequest, TResponse> where TRequest : IRequest<TResponse>, IAuthorizable
{
	public async Task<TResponse> Handle(
		TRequest request,
		CancellationToken ct)
	{
		TEntity entity = await loader.LoadAsync(request: request, ct: ct);
		return await handler.HandleAsync(request: request, entity: entity, ct: ct);
	}
}

public sealed class AuthorizedHandlerAdapter<TRequest, TEntity>(
	IEntityLoader<TRequest, TEntity> loader,
	IAuthorizedHandler<TRequest, TEntity> handler
) : IRequestHandler<TRequest> where TRequest : IRequest, IAuthorizable
{
	public async Task Handle(
		TRequest request,
		CancellationToken ct)
	{
		TEntity entity = await loader.LoadAsync(request: request, ct: ct);
		await handler.HandleAsync(request: request, entity: entity, ct: ct);
	}
}