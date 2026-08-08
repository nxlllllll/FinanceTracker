using System.Diagnostics;
using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.Metrics;
using FinanceTracker.Core.Services.Tracing;
using MediatR;

namespace FinanceTracker.Application.Behaviours.Tracing;

/// <summary>
/// Opens a span and records throughput and duration for every request.
/// </summary>
public sealed class ObservabilityBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
	where TRequest : notnull
{
	private static readonly string RequestTypeName = typeof(TRequest).Name;

	/// <inheritdoc/>
	public async Task<TResponse> Handle(
		TRequest request,
		RequestHandlerDelegate<TResponse> next,
		CancellationToken cancellationToken = default)
	{
		using Activity? activity = FinanceTrackerActivitySource.Instance.StartActivity(name: RequestTypeName, kind: ActivityKind.Internal);

		activity?.SetTag(key: FinanceTrackerActivitySource.Tags.RequestType, value: RequestTypeName);
		if (request is IAuthorizable authorizable)
			activity?.SetTag(key: FinanceTrackerActivitySource.Tags.UserId, value: authorizable.UserId);

		long startedAt = Stopwatch.GetTimestamp();

		try
		{
			TResponse response = await next(t: cancellationToken);

			activity?.SetStatus(code: ActivityStatusCode.Ok);
			Record(outcome: OutcomeOf(response: response), startedAt: startedAt);

			return response;
		}
		catch (Exception ex)
		{
			activity?.SetStatus(code: ActivityStatusCode.Error, description: ex.Message);
			activity?.AddException(exception: ex);

			Record(outcome: FinanceTrackerMetrics.CommandOutcomes.Error, startedAt: startedAt);

			throw;
		}
	}

	private static string OutcomeOf(TResponse response)
	{
		if (response is IResult { IsFailure: true })
			return FinanceTrackerMetrics.CommandOutcomes.Failure;

		return FinanceTrackerMetrics.CommandOutcomes.Success;
	}

	private static void Record(string outcome, long startedAt)
	{
		KeyValuePair<string, object?> requestType = new KeyValuePair<string, object?>(
			key: FinanceTrackerMetrics.Tags.RequestType,
			value: RequestTypeName
		);

		FinanceTrackerMetrics.CommandExecuted.Add(
			delta: 1,
			tag1: requestType,
			tag2: new KeyValuePair<string, object?>(key: FinanceTrackerMetrics.Tags.Outcome, value: outcome)
		);

		FinanceTrackerMetrics.CommandDuration.Record(
			value: Stopwatch.GetElapsedTime(startingTimestamp: startedAt).TotalSeconds,
			tag: requestType
		);
	}
}
