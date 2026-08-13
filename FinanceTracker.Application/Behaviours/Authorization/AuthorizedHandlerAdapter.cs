using FinanceTracker.Core.Domains.Abstractions;
using FinanceTracker.Core.Domains.Abstractions.Aggregate;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Platform.Concurrency;
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
	public async Task<Result<TValue, TError>> Handle(
		TRequest request,
		CancellationToken ct)
	{
		string commandType = request.GetType().Name;
		Result<TEntity, TError> entity = await loader.LoadAsync(request: request, ct: ct);
		if (entity.IsFailure)
		{
			logger.ZLogWarning(message: $"Authorization failed for {commandType}: {entity.Error!.Message}");
			return Result<TValue, TError>.Failure(error: entity.Error!);
		}

		PreconditionFailedException? mismatch = CheckExpectedVersion(request: request, entity: entity.Value!);
		if (mismatch is null)
			return await handler.HandleAsync(request: request, entity: entity.Value!, ct: ct);

		if (mismatch is not TError typedMismatch)
		{
			throw new InvalidOperationException(message:
				$"{typeof(TRequest).Name} carries an expected version, but its error type " +
				$"{typeof(TError).Name} cannot hold a {nameof(PreconditionFailedException)}. " +
				$"Widen the handler's TError to {nameof(AppException)} or drop {nameof(IHasExpectedVersion)} from the request."
			);
		}

		logger.ZLogInformation(message: $"Precondition failed for {commandType} on {mismatch.Id?.ToString() ?? "an unidentified entity"}: {mismatch.Message}");
		return Result<TValue, TError>.Failure(error: typedMismatch);

	}

	private static PreconditionFailedException? CheckExpectedVersion(TRequest request, TEntity entity)
	{
		if (request is not IHasExpectedVersion { ExpectedVersion: { } expectedVersion })
			return null;

		if (entity is not IHasVersion versionedEntity)
		{
			throw new InvalidOperationException(message:
				$"{typeof(TRequest).Name} carries an expected version, but {typeof(TEntity).Name} " +
				   $"does not implement {nameof(IHasVersion)}, so the precondition cannot be checked. " +
				   $"Either load a versioned entity or drop {nameof(IHasExpectedVersion)} from the request."
			);
		}

		int actualVersion = versionedEntity.Version;
		if (actualVersion == expectedVersion)
			return null;

		return new PreconditionFailedException(
			message: $"{typeof(TEntity).Name} was modified since it was last fetched (expected version {expectedVersion}, actual {actualVersion}).",
			id: (entity as IHasId)?.Id,
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

		string commandType = request.GetType().Name;
		logger.ZLogWarning(message: $"Authorization failed for {commandType}: {entity.Error!.Message}");
		return Result<TValue, TError>.Failure(error: entity.Error!);
	}
}
