using FinanceTracker.Core.Domains.Abstractions.Aggregate;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using MediatR;
using Microsoft.Extensions.Logging;
using ZLogger;
using Unit = FinanceTracker.Core.Results.Unit;

namespace FinanceTracker.Application.Behaviours.Authorization;

/// <summary>
/// MediatR <see cref="IRequestHandler{TRequest,TResponse}"/> that wires together an
/// <see cref="IEntityLoader{TRequest,TEntity,TError}"/> and an
/// <see cref="IAuthorizedHandler{TRequest,TEntity,TValue,TError}"/>.
/// <para>
/// Registered automatically by the Application DI for every
/// <see cref="IAuthorizedHandler{TRequest,TEntity,TValue,TError}"/> implementation found in the assembly.
/// </para>
/// <para>
/// After loading, if the request implements <see cref="IHasExpectedVersion"/> with a non-null value
/// and the loaded entity implements <see cref="IHasVersion"/>, the two are compared before the
/// handler runs. A mismatch means the resource changed since the client last saw it.
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
	/// <inheritdoc/>
	public async Task<Result<TValue, TError>> Handle(
		TRequest request,
		CancellationToken ct)
	{
		Result<TEntity, TError> entity = await loader.LoadAsync(request: request, ct: ct);
		if (entity.IsFailure)
		{
			logger.ZLogWarning(message: $"Authorization failed for {request.GetType().Name}: {entity.Error!.Message}");
			return Result<TValue, TError>.Failure(error: entity.Error!);
		}

		PreconditionFailedException? mismatch = CheckExpectedVersion(request: request, entity: entity.Value!);
		if (mismatch is not null)
		{
			logger.ZLogInformation(message: $"Precondition failed for {request.GetType().Name}: {mismatch.Message}");
			return Result<TValue, TError>.Failure(error: (TError)(object)mismatch);
		}

		return await handler.HandleAsync(request: request, user: entity.Value!, ct: ct);
	}

	private static PreconditionFailedException? CheckExpectedVersion(TRequest request, TEntity entity)
	{
		if (request is not IHasExpectedVersion { ExpectedVersion: { } expectedVersion })
			return null;

		if (entity is not IHasVersion versionedEntity)
			return null;

		int actualVersion = versionedEntity.Version;
		if (actualVersion == expectedVersion)
			return null;

		return new PreconditionFailedException(
			message: $"{typeof(TEntity).Name} was modified since it was last fetched (expected version {expectedVersion}, actual {actualVersion}).",
			id: Guid.Empty,
			expectedVersion: expectedVersion,
			actualVersion: actualVersion
		);
	}
}

public sealed class AuthorizedHandlerAdapter<TRequest, TValue, TError>(
	IEntityLoader<TRequest, TError> loader,
	IAuthorizedHandler<TRequest, TValue, TError> handler,
	ILogger<AuthorizedHandlerAdapter<TRequest, TValue, TError>> logger
) : IRequestHandler<TRequest, Result<TValue, TError>>
	where TRequest : IRequest<Result<TValue, TError>>, IAuthorizable
	where TError : AppException
{
	/// <inheritdoc/>
	public async Task<Result<TValue, TError>> Handle(
		TRequest request,
		CancellationToken ct)
	{
		Result<Unit, TError> entity = await loader.LoadAsync(request: request, ct: ct);
		if (entity.IsSuccess)
			return await handler.HandleAsync(request: request, ct: ct);

		logger.ZLogWarning(message: $"Authorization failed for {request.GetType().Name}: {entity.Error!.Message}");
		return Result<TValue, TError>.Failure(error: entity.Error!);
	}
}
