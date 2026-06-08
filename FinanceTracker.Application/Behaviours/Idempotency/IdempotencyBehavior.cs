using System.Text.Json;
using FinanceTracker.Core.Converters.Json;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.Idempotency;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZLogger;

namespace FinanceTracker.Application.Behaviours.Idempotency;

public sealed class IdempotencyBehavior<TRequest, TResponse>(
    IIdempotencyReadRepository idempotencyReadRepository,
    IIdempotencyWriteRepository idempotencyWriteRepository,
    IOptionsMonitor<IdempotencyOptions> options,
    IDateProvider dateProvider,
    ILogger<IdempotencyBehavior<TRequest, TResponse>> logger
) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
    where TResponse : IResult<TResponse, DomainException>
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken = default)
    {
        if (request is not IIdempotentCommand idempotent)
            return await next(t: cancellationToken);

        if (idempotent.IdempotencyKey == Guid.Empty)
        {
            logger.ZLogWarning(message: $"[Idempotency] {typeof(TRequest).Name} has empty IdempotencyKey.");
            return TResponse.CreateFailure(error: new EmptyIdempotentException(message: $"{typeof(TRequest).Name} implements IIdempotentCommand but IdempotencyKey is Guid.Empty."));
        }

        IdempotencyOptions currentOptions = options.CurrentValue;
        DateTimeOffset now = dateProvider.UtcNow;

        IdempotencyEntry? entry = await idempotencyReadRepository.GetAsync(
            idempotencyKey: idempotent.IdempotencyKey,
            ct: cancellationToken
        );

        if (entry is not null)
        {
            return await HandleExistingEntryAsync(
                entry: entry,
                idempotent: idempotent,
                options: currentOptions,
                now: now,
                cancellationToken: cancellationToken
            );
        }

        DateTimeOffset expiresAt = now.AddHours(hours: currentOptions.ExpiryHours);
        bool reserved = await idempotencyWriteRepository.TryReserveAsync(
            idempotencyKey: idempotent.IdempotencyKey,
            commandType: typeof(TRequest).Name,
            reservedAt: now,
            expiresAt: expiresAt,
            ct: cancellationToken
        );

        if (!reserved)
        {
            logger.ZLogInformation(message: $"[Idempotency] Key {idempotent.IdempotencyKey} is in-flight, waiting for result.");
            return await PollForCompletionAsync(
                idempotent: idempotent,
                options: currentOptions,
                cancellationToken: cancellationToken
            );
        }

        TResponse response = await next(t: cancellationToken);

        if (response is IResult { IsSuccess: true })
        {
            await idempotencyWriteRepository.CompleteAsync(
                idempotencyKey: idempotent.IdempotencyKey,
                responseJson: JsonSerializer.Serialize(value: response, options: FinanceTrackerJsonOptions.Application),
                ct: cancellationToken
            );

            logger.ZLogDebug(message: $"[Idempotency] Completed key {idempotent.IdempotencyKey} for {typeof(TRequest).Name} (expires: {expiresAt:O}).");
        }
        else
        {
            await idempotencyWriteRepository.DeleteAsync(
                idempotencyKey: idempotent.IdempotencyKey,
                ct: cancellationToken
            );

            logger.ZLogWarning(message: $"[Idempotency] Released key {idempotent.IdempotencyKey} for {typeof(TRequest).Name} — command failed, client may retry.");
        }

        return response;
    }

    private async Task<TResponse> HandleExistingEntryAsync(
        IdempotencyEntry entry,
        IIdempotentCommand idempotent,
        IdempotencyOptions options,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (!String.IsNullOrWhiteSpace(value: entry.ResponseJson))
        {
            logger.ZLogInformation(message: $"[Idempotency] Returning cached result for {typeof(TRequest).Name} (key: {idempotent.IdempotencyKey}).");
            return JsonSerializer.Deserialize<TResponse>(json: entry.ResponseJson, options: FinanceTrackerJsonOptions.Application)!;
        }

        TimeSpan age = now - entry.ReservedAt;
        if (age.TotalSeconds >= options.AbandonedAfterSeconds)
        {
            logger.ZLogWarning(message: $"[Idempotency] Key {idempotent.IdempotencyKey} is abandoned (age: {age.TotalSeconds:F0}s). Deleting and retrying.");

            await idempotencyWriteRepository.DeleteAsync(
                idempotencyKey: idempotent.IdempotencyKey,
                ct: cancellationToken
            );

            return TResponse.CreateFailure(error: new EmptyIdempotentException(
                message: $"Idempotency key {idempotent.IdempotencyKey} was abandoned. The original request did not complete. Please retry.")
            );
        }

        logger.ZLogInformation(message: $"[Idempotency] Key {idempotent.IdempotencyKey} is in-flight (age: {age.TotalSeconds:F0}s), waiting for result.");
        return await PollForCompletionAsync(
            idempotent: idempotent,
            options: options,
            cancellationToken: cancellationToken
        );
    }

    private async Task<TResponse> PollForCompletionAsync(
        IIdempotentCommand idempotent,
        IdempotencyOptions options,
        CancellationToken cancellationToken)
    {
        int elapsed = 0;
        int delay = options.InFlightInitialDelayMs;

        while (elapsed < options.InFlightMaxWaitMs)
        {
            await Task.Delay(millisecondsDelay: delay, cancellationToken);
            elapsed += delay;
            delay = Math.Min(delay * 2, options.InFlightMaxDelayMs);

            IdempotencyEntry? entry = await idempotencyReadRepository.GetAsync(
                idempotencyKey: idempotent.IdempotencyKey,
                ct: cancellationToken
            );

            if (entry is null)
            {
                logger.ZLogWarning(message: $"[Idempotency] Key {idempotent.IdempotencyKey} disappeared during poll — likely abandoned and deleted by another request.");
                return TResponse.CreateFailure(error: new EmptyIdempotentException(
                    message: $"Idempotency key {idempotent.IdempotencyKey} was abandoned during processing. Please retry.")
                );
            }

            if (!String.IsNullOrWhiteSpace(value: entry.ResponseJson))
            {
                logger.ZLogInformation(message: $"[Idempotency] Key {idempotent.IdempotencyKey} completed after {elapsed}ms wait.");
                return JsonSerializer.Deserialize<TResponse>(json: entry.ResponseJson, options: FinanceTrackerJsonOptions.Application)!;
            }

            TimeSpan age = dateProvider.UtcNow - entry.ReservedAt;
            if (!(age.TotalSeconds >= options.AbandonedAfterSeconds))
                continue;
            
            logger.ZLogWarning(message: $"[Idempotency] Key {idempotent.IdempotencyKey} became abandoned during poll (age: {age.TotalSeconds:F0}s). Deleting.");

            await idempotencyWriteRepository.DeleteAsync(
                idempotencyKey: idempotent.IdempotencyKey,
                ct: cancellationToken
            );

            return TResponse.CreateFailure(error: new EmptyIdempotentException(
                message: $"Idempotency key {idempotent.IdempotencyKey} was abandoned. The original request did not complete. Please retry.")
            );
        }

        logger.ZLogWarning(message: $"[Idempotency] Key {idempotent.IdempotencyKey} timed out waiting for in-flight result after {options.InFlightMaxWaitMs}ms.");
        return TResponse.CreateFailure(error: new EmptyIdempotentException(
            message: $"Idempotency key {idempotent.IdempotencyKey} timed out waiting for an in-flight request to complete.")
        );
    }
}