using FinanceTracker.Core.Services.EventStore;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace FinanceTracker.Infrastructure.Database.EventStore;

/// <summary>
/// Singleton, thread-safe implementation of <see cref="IEventSchemaHealthState"/>.
/// </summary>
public sealed class EventSchemaHealthState(
	ILogger<EventSchemaHealthState> logger
) : IEventSchemaHealthState
{
	private readonly Lock _latch = new Lock();

	private volatile bool _isCompatible = true;
	private string? _diagnosis;

	public bool IsCompatible => _isCompatible;

	public string? Diagnosis
	{
		get
		{
			lock (_latch)
				return _diagnosis;
		}
	}

	public void MarkIncompatible(string diagnosis)
	{
		lock (_latch)
		{
			if (!_isCompatible)
				return;

			_diagnosis = diagnosis;
			_isCompatible = false;
		}

		logger.ZLogCritical(message: $"""
			[Upcasting] This build cannot read events already written to the store: {diagnosis}
			Readiness is now failing so the instance leaves the load balancer instead of serving errors
			for every aggregate with the same mismatch. Nothing here recovers without a deploy.
		""");
	}
}
