using Microsoft.Extensions.Logging;

namespace FinanceTracker.Tests.Unit.Helpers;

public sealed class CapturingLogger<T> : ILogger<T>
{
	public int LogCount { get; private set; }
	public bool WarningLogged { get; private set; }
	public bool ErrorLogged { get; private set; }

	public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
	public bool IsEnabled(LogLevel logLevel) => true;

	public void Log<TState>(
		LogLevel logLevel,
		EventId eventId,
		TState state,
		Exception? exception,
		Func<TState, Exception?, string> formatter)
	{
		if (logLevel == LogLevel.Warning)
			WarningLogged = true;

		if (logLevel == LogLevel.Error)
			ErrorLogged = true;

		LogCount++;
	}
}
