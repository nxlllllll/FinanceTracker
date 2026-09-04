using Microsoft.Extensions.Options;

namespace FinanceTracker.Benchmarks.Infrastructure;

public sealed class FixedOptionsMonitor<T>(T value) : IOptionsMonitor<T>
{
	public T CurrentValue { get; } = value;

	public T Get(string? name) => CurrentValue;

	public IDisposable OnChange(Action<T, string?> listener) => NullDisposable.Instance;

	private sealed class NullDisposable : IDisposable
	{
		public static readonly NullDisposable Instance = new NullDisposable();

		public void Dispose() { }
	}
}
