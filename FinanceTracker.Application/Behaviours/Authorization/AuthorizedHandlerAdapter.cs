using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;
using MediatR;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace FinanceTracker.Application.Behaviours.Authorization;

/// <summary>
/// MediatR <see cref="IRequestHandler{TRequest,TResponse}"/> that wires together an
/// <see cref="IEntityLoader{TRequest,TEntity,TError}"/> and an
/// <see cref="IAuthorizedHandler{TRequest,TEntity,TValue,TError}"/>.
/// <para>
/// Registered automatically by the Application DI for every
/// <see cref="IAuthorizedHandler{TRequest,TEntity,TValue,TError}"/> implementation found in the assembly.
/// </para>
/// </summary>
public sealed class AuthorizedHandlerAdapter<TRequest, TEntity, TValue, TError>(
	IEntityLoader<TRequest, TEntity, TError> loader,
	IAuthorizedHandler<TRequest, TEntity, TValue, TError> handler,
	ILogger<AuthorizedHandlerAdapter<TRequest, TEntity, TValue, TError>> logger
) : IRequestHandler<TRequest, Result<TValue, TError>>
	where TRequest : IRequest<Result<TValue, TError>>, IAuthorizable
	where TError : AppException
{
	/// <inheritdoc/>
	public async Task<Result<TValue, TError>> Handle(
		TRequest request,
		CancellationToken ct)
	{
		Result<TEntity, TError> entity = await loader.LoadAsync(request: request, ct: ct);
		if (entity.IsSuccess)
			return await handler.HandleAsync(request: request, accounts: entity.Value!, ct: ct);

		logger.ZLogWarning(message: $"Authorization failed for {request.GetType().Name}: {entity.Error!.Message}");
		return Result<TValue, TError>.Failure(error: entity.Error!);
	}
}