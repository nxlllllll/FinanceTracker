using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;
using MediatR;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace FinanceTracker.Application.Behaviours.Authorization;

public sealed class AuthorizedHandlerAdapter<TRequest, TEntity, TValue, TError>(
	IEntityLoader<TRequest, TEntity, TError> loader,
	IAuthorizedHandler<TRequest, TEntity, TValue, TError> handler,
	ILogger<AuthorizedHandlerAdapter<TRequest, TEntity, TValue, TError>> logger
) : IRequestHandler<TRequest, Result<TValue, TError>>
	where TRequest : IRequest<Result<TValue, TError>>, IAuthorizable
	where TError : AppException
{
	public async Task<Result<TValue, TError>> Handle(
		TRequest request,
		CancellationToken ct)
	{
		Result<TEntity, TError> entity = await loader.LoadAsync(request: request, ct: ct);
		if (entity.IsSuccess)
			return await handler.HandleAsync(request: request, entity: entity.Value!, ct: ct);
		
		logger.ZLogWarning(message: $"Authorization failed for {request.GetType().Name}: {entity.Error!.Message}");
		return Result<TValue, TError>.Failure(error: entity.Error!);
	}
}