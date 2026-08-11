using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.ReadModels.User;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Worker.Shared.Job;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;
using ZLogger;

namespace FinanceTracker.Worker.BaseCurrencyRecalculation.Job;

/// <summary>
/// Rebuilds category totals for users whose base currency changed.
/// </summary>
[DisallowConcurrentExecution]
public sealed class BaseCurrencyRecalculationJob(
	IBaseCurrencyRecalculationWriteRepository recalculationWriteRepository,
	ICategoryTotalWriteRepository categoryTotalWriteRepository,
	IUserQueryRepository userQueryRepository,
	IUnitOfWork unitOfWork,
	IDateProvider dateProvider,
	IOptionsMonitor<BaseCurrencyRecalculationJobOptions> options,
	ILogger<BaseCurrencyRecalculationJob> logger
) : IJob
{
	public async Task Execute(IJobExecutionContext context)
	{
		BaseCurrencyRecalculationJobOptions currentOptions = options.CurrentValue;

		if (!currentOptions.IsEnabled)
			return;

		CancellationToken ct = context.CancellationToken;

		IReadOnlyList<Core.ReadModels.Currency.BaseCurrencyRecalculation> claimed = await recalculationWriteRepository.ClaimPendingBatchAsync(
			batchSize: currentOptions.BatchSize,
			leaseDuration: TimeSpan.FromMinutes(value: currentOptions.LeaseMinutes),
			now: dateProvider.UtcNow,
			ct: ct
		);

		if (claimed.Count == 0)
			return;

		logger.ZLogInformation(message: $"Rebuilding category totals for {claimed.Count} user(s).");

		int rebuilt = 0;

		foreach (Core.ReadModels.Currency.BaseCurrencyRecalculation request in claimed)
		{
			if (ct.IsCancellationRequested)
				break;

			if (await RebuildAsync(request: request, maxAttempts: currentOptions.MaxAttempts, ct: ct))
				rebuilt++;
		}

		logger.ZLogInformation(message: $"Rebuilt: {rebuilt}/{claimed.Count}.");
	}

	private async Task<bool> RebuildAsync(
		Core.ReadModels.Currency.BaseCurrencyRecalculation request,
		int maxAttempts,
		CancellationToken ct)
	{
		try
		{
			UserReadModel? user = await userQueryRepository.GetByIdAsync(userId: request.UserId, ct: ct);

			if (user is null)
			{
				logger.ZLogWarning(message: $"User {request.UserId} no longer exists; dropping the rebuild.");
				await recalculationWriteRepository.CompleteAsync(userId: request.UserId, targetCurrency: request.TargetCurrency, ct: ct);
				return false;
			}

			if (user.BaseCurrency != request.TargetCurrency)
			{
				logger.ZLogInformation(message: $"""
					User {request.UserId} moved to {user.BaseCurrency.Value} since this rebuild was claimed for {request.TargetCurrency.Value};
					leaving it for the newer request.
				""");
				return false;
			}

			await unitOfWork.ExecuteInTransactionAsync(operation: async () => await categoryTotalWriteRepository.RecalculateAllForUserAsync(
				userId: request.UserId,
				baseCurrency: request.TargetCurrency,
				ct: ct
			), ct: ct);

			bool completed = await recalculationWriteRepository.CompleteAsync(
				userId: request.UserId,
				targetCurrency: request.TargetCurrency,
				ct: ct
			);

			if (completed)
				return true;

			logger.ZLogInformation(message: $"Rebuild for user {request.UserId} into {request.TargetCurrency.Value} was superseded before it could be marked done.");
			return false;

		}
		catch (OperationCanceledException) when (ct.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception exception)
		{
			logger.ZLogError(exception: exception, message: $"Rebuild failed for user {request.UserId} (attempt {request.Attempts + 1} of {maxAttempts}).");

			await recalculationWriteRepository.FailAttemptAsync(
				userId: request.UserId,
				error: exception.Message,
				maxAttempts: maxAttempts,
				ct: CancellationToken.None
			);

			return false;
		}
	}
}
