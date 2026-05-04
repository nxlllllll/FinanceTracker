using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.Behaviours.Authorization;

public sealed class AuthorizedHandlerAdapter<TRequest, TEntity, TValue, TError>(
	IEntityLoader<TRequest, TEntity> loader,
	IAuthorizedHandler<TRequest, TEntity, TValue, TError> handler
) : IRequestHandler<TRequest, Result<TValue, TError>>
	where TRequest : IRequest<Result<TValue, TError>>, IAuthorizable
	where TError : AppException
{
	public async Task<Result<TValue, TError>> Handle(
		TRequest request,
		CancellationToken ct)
	{
		TEntity entity = await loader.LoadAsync(request: request, ct: ct);
		return await handler.HandleAsync(request: request, entity: entity, ct: ct);
	}
}