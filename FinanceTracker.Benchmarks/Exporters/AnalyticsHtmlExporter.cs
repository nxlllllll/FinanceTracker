using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;

namespace FinanceTracker.Benchmarks.Exporters;

public sealed class AnalyticsHtmlExporter : IExporter
{
	public static readonly AnalyticsHtmlExporter Default = new AnalyticsHtmlExporter();
	public string Name => nameof(AnalyticsHtmlExporter);

	private readonly List<Summary> _summaries = [];
	private readonly Lock _lock = new Lock();

	private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
	};

	public IEnumerable<string> ExportToFiles(Summary summary, ILogger consoleLogger)
	{
		lock (_lock)
			_summaries.Add(item: summary);

		return [];
	}

	public void ExportToLog(Summary summary, ILogger logger) { }

	public async Task<string> Flush(string outputDir)
	{
		List<Summary> summaries;
		lock (_lock)
			summaries = [.. _summaries];

		if (summaries.Count == 0)
			return String.Empty;

		Directory.CreateDirectory(path: outputDir);
		string path = Path.Combine(outputDir, $"BenchmarkAnalytics-{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.html");
		await File.WriteAllTextAsync(path: path, contents: BuildHtml(summaries: summaries), encoding: Encoding.UTF8);
		return path;
	}

	private sealed record BenchmarkRow(
		string Class,
		string Method,
		string RowCount,
		bool Success,
		double MeanMs,
		double MedianMs,
		double StdDevMs,
		double MinMs,
		double MaxMs,
		double AllocKb,
		long Gen0,
		long Gen1,
		long Gen2);

	private sealed record ReportData(
		string Title,
		string RunMeta,
		string RunDate,
		IReadOnlyList<BenchmarkRow> Rows);

	private static string BuildHtml(List<Summary> summaries)
	{
		string template = LoadResource(name: "analytics-template.html");
		string styles = LoadResource(name: "analytics-styles.css");
		string scripts = LoadResource(name: "analytics-scripts.js");

		List<BenchmarkRow> rows = [.. summaries.SelectMany(selector: ExtractRows)];

		string runtime = summaries[0].HostEnvironmentInfo.RuntimeVersion;
		string? os = summaries[0].HostEnvironmentInfo.Os.Value.Version;
		string date = DateTime.Now.ToString(format: "yyyy-MM-dd HH:mm");

		ReportData data = new ReportData(
			Title: "FinanceTracker Benchmarks",
			RunMeta: $"{runtime} · {os}",
			RunDate: date,
			Rows: rows
		);

		string dataJson = JsonSerializer.Serialize(value: data, options: JsonOptions);

		return template
			.Replace(oldValue: "{{TITLE}}", newValue: data.Title)
			.Replace(oldValue: "{{RUN_META}}", newValue: data.RunMeta)
			.Replace(oldValue: "{{RUN_DATE}}", newValue: date)
			.Replace(oldValue: "{{STYLES}}", newValue: styles)
			.Replace(oldValue: "{{SCRIPTS}}", newValue: scripts)
			.Replace(oldValue: "{{DATA_JSON}}", newValue: dataJson);
	}

	private static List<BenchmarkRow> ExtractRows(Summary summary)
	{
		List<BenchmarkRow> rows = [];
		foreach (BenchmarkReport report in summary.Reports)
		{
			BenchmarkCase bc = report.BenchmarkCase;
			bool ok = report is { Success: true, ResultStatistics: not null };
			double? allocKb = ok ? report.GcStats.GetBytesAllocatedPerOperation(benchmarkCase: bc) / 1024.0 : 0;

			rows.Add(item: new BenchmarkRow(
				Class: bc.Descriptor.Type.Name.Replace(oldValue: "Benchmarks", newValue: String.Empty),
				Method: bc.Descriptor.WorkloadMethod.Name,
				RowCount: bc.Parameters.ValueInfo,
				Success: ok,
				MeanMs: ok ? report.ResultStatistics!.Mean / 1_000_000 : 0,
				MedianMs: ok ? report.ResultStatistics!.Median / 1_000_000 : 0,
				StdDevMs: ok ? report.ResultStatistics!.StandardDeviation / 1_000_000 : 0,
				MinMs: ok ? report.ResultStatistics!.Min / 1_000_000 : 0,
				MaxMs: ok ? report.ResultStatistics!.Max / 1_000_000 : 0,
				AllocKb: allocKb ?? 0,
				Gen0: ok ? report.GcStats.Gen0Collections : 0,
				Gen1: ok ? report.GcStats.Gen1Collections : 0,
				Gen2: ok ? report.GcStats.Gen2Collections : 0
			));
		}
		return rows;
	}

	private static string LoadResource(string name)
	{
		Assembly assembly = typeof(AnalyticsHtmlExporter).Assembly;
		string resourceName = assembly.GetManifestResourceNames().FirstOrDefault(predicate: n => n.EndsWith(value: name, comparisonType: StringComparison.OrdinalIgnoreCase))
			?? throw new FileNotFoundException(message: $"Embedded resource '{name}' not found.");

		using Stream stream = assembly.GetManifestResourceStream(name: resourceName)!;
		using StreamReader reader = new StreamReader(stream: stream, encoding: Encoding.UTF8);
		return reader.ReadToEnd();
	}
}
