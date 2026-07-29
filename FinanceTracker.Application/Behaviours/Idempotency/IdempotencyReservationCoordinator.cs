using System.Diagnostics;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.Idempotency;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.Utilities.Retry;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZLogger;

namespace FinanceTracker.Application.Behaviours.Idempotency;

public sealed class IdempotencyReservationCoordinator(
	IIdempotencyReadRepository readRepository,
	IIdempotencyWriteRepository writeRepository,
	IOptionsMonitor<IdempotencyOptions> options,
	IDateProvider dateProvider,
	ILogger<IdempotencyReservationCoordinator> logger
) : IIdempotencyReservationCoordinator
{
	public async Task<IdempotencyAcquisition> AcquireAsync(
		Guid idempotencyKey,
		string commandType,
		Guid userId,
		CancellationToken ct = default)
	{
		IdempotencyOptions currentOptions = options.CurrentValue;

		IdempotencyEntry? entry = await readRepository.GetAsync(
			idempotencyKey: idempotencyKey,
			commandType: commandType,
			userId: userId,
			ct: ct
		);

		if (entry is not null)
		{
			IdempotencyAcquisition? resolved = await TryResolveAsync(
				idempotencyKey: idempotencyKey,
				commandType: commandType,
				userId: userId,
				entry: entry,
				options: currentOptions,
				ct: ct
			);

			if (resolved is { } decision)
				return decision;

			logger.ZLogInformation(message: $"[Idempotency] Key {idempotencyKey} is in-flight, waiting for result.");
			return await PollAsync(idempotencyKey: idempotencyKey, commandType: commandType, userId: userId, options: currentOptions, ct: ct);
		}

		Guid reservationId = Guid.CreateVersion7();
		DateTimeOffset now = dateProvider.UtcNow;

		bool reserved = await writeRepository.TryReserveAsync(
			idempotencyKey: idempotencyKey,
			commandType: commandType,
			userId: userId,
			reservationId: reservationId,
			reservedAt: now,
			expiresAt: now.AddHours(hours: currentOptions.ExpiryHours),
			ct: ct
		);

		if (reserved)
			return IdempotencyAcquisition.Reserved(reservationId: reservationId);

		logger.ZLogInformation(message: $"[Idempotency] Key {idempotencyKey} is in-flight, waiting for result.");
		return await PollAsync(idempotencyKey: idempotencyKey, commandType: commandType, userId: userId, options: currentOptions, ct: ct);
	}

	private async Task<IdempotencyAcquisition?> TryResolveAsync(
		Guid idempotencyKey,
		string commandType,
		Guid userId,
		IdempotencyEntry entry,
		IdempotencyOptions options,
		CancellationToken ct)
	{
		if (!String.IsNullOrWhiteSpace(value: entry.ResponseJson))
			return IdempotencyAcquisition.CachedResponse(json: entry.ResponseJson);

		TimeSpan age = dateProvider.UtcNow - entry.ReservedAt;
		if (age.TotalSeconds < options.AbandonedAfterSeconds)
			return null;

		logger.ZLogWarning(message: $"[Idempotency] Key {idempotencyKey} looks abandoned (age: {age.TotalSeconds:F0}s). Reclaiming.");

		bool reclaimed = await writeRepository.DeleteAsync(
			idempotencyKey: idempotencyKey,
			commandType: commandType,
			userId: userId,
			reservationId: entry.ReservationId,
			ct: ct
		);

		if (!reclaimed)
		{
			logger.ZLogInformation(message: $"[Idempotency] Key {idempotencyKey} changed before it could be reclaimed — will keep waiting.");
			return null;
		}

		return IdempotencyAcquisition.Failed(error: new IdempotencyAbandonedException(
			message: $"Idempotency key {idempotencyKey} was abandoned. The original request did not complete. Please retry."
		));
	}

	private async Task<IdempotencyAcquisition> PollAsync(
		Guid idempotencyKey,
		string commandType,
		Guid userId,
		IdempotencyOptions options,
		CancellationToken ct)
	{
		int attempt = 0;
		Stopwatch stopwatch = Stopwatch.StartNew();

		while (stopwatch.ElapsedMilliseconds < options.InFlightMaxWaitMs)
		{
			int delay = Math.Min(
				val1: RetryDelayCalculator.Calculate(attempt: attempt, baseDelayMs: options.InFlightInitialDelayMs, useJitter: options.UseJitter),
				val2: options.InFlightMaxDelayMs
			);

			await Task.Delay(millisecondsDelay: delay, cancellationToken: ct);
			++attempt;

			IdempotencyEntry? entry = await readRepository.GetAsync(idempotencyKey: idempotencyKey, commandType: commandType, userId: userId, ct: ct);

			if (entry is null)
			{
				logger.ZLogWarning(message: $"[Idempotency] Key {idempotencyKey} disappeared during poll — likely abandoned and released by another request.");
				return IdempotencyAcquisition.Failed(error: new IdempotencyAbandonedException(
					message: $"Idempotency key {idempotencyKey} was abandoned during processing. Please retry."
				));
			}

			IdempotencyAcquisition? resolved = await TryResolveAsync(
				idempotencyKey: idempotencyKey,
				commandType: commandType,
				userId: userId,
				entry: entry,
				options: options,
				ct: ct
			);

			if (resolved is { } decision)
				return decision;
		}

		logger.ZLogWarning(message: $"[Idempotency] Key {idempotencyKey} timed out waiting for in-flight result after {options.InFlightMaxWaitMs}ms.");
		return IdempotencyAcquisition.Failed(error: new IdempotencyTimeoutException(
			message: $"Idempotency key {idempotencyKey} timed out waiting for an in-flight request to complete."
		));
	}
}
