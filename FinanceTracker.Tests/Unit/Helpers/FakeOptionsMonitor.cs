using Microsoft.Extensions.Options;

namespace FinanceTracker.Tests.Unit.Helpers;

public sealed class FakeOptionsMonitor<T>(T value) : IOptionsMonitor<T>
{
	public T CurrentValue => value;

	public T Get(string? name) => value;

	public IDisposable OnChange(Action<T, string?> listener) => new NullDisposable();

	private sealed class NullDisposable : IDisposable
	{
		public void Dispose() { }
	}
}
