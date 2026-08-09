using System.Diagnostics.Metrics;
using FinanceTracker.Core.Services.Metrics;

namespace FinanceTracker.Tests.Unit.Helpers;

public sealed class MetricCollector : IDisposable
{
	private readonly MeterListener _listener;
	private readonly List<Measurement> _measurements = [];
	private readonly Lock _gate = new Lock();

	public MetricCollector(params string[] instrumentNames)
	{
		HashSet<string> wanted = instrumentNames.ToHashSet();

		_listener = new MeterListener
		{
			InstrumentPublished = (instrument, listener) =>
			{
				if (instrument.Meter.Name == FinanceTrackerMetrics.MeterName && wanted.Contains(item: instrument.Name))
					listener.EnableMeasurementEvents(instrument: instrument);
			}
		};

		_listener.SetMeasurementEventCallback<long>(measurementCallback: (instrument, value, tags, _) => Add(instrument: instrument, value: value, tags: tags));
		_listener.SetMeasurementEventCallback<double>(measurementCallback: (instrument, value, tags, _) => Add(instrument: instrument, value: value, tags: tags));

		_listener.Start();
	}

	private void Add(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
	{
		Dictionary<string, string?> copied = new Dictionary<string, string?>(capacity: tags.Length);

		foreach (KeyValuePair<string, object?> tag in tags)
			copied[tag.Key] = tag.Value?.ToString();

		lock (_gate)
			_measurements.Add(item: new Measurement(Instrument: instrument.Name, Value: value, Tags: copied));
	}

	public void Flush() => _listener.RecordObservableInstruments();

	public IReadOnlyList<Measurement> Measurements
	{
		get
		{
			Flush();
			lock (_gate)
				return _measurements.ToList();
		}
	}

	public IReadOnlyList<Measurement> For(string instrument, params (string Key, string Value)[] tags)
	{
		return Measurements.Where(predicate: m => m.Instrument == instrument && tags.All(
			predicate: t => m.Tags.TryGetValue(key: t.Key, value: out string? actual) && actual == t.Value
		)).ToList();
	}

	public double Total(string instrument, params (string Key, string Value)[] tags)
		=> For(instrument: instrument, tags: tags).Sum(selector: m => m.Value);

	public void Dispose() => _listener.Dispose();

	public sealed record Measurement(string Instrument, double Value, IReadOnlyDictionary<string, string?> Tags);
}
