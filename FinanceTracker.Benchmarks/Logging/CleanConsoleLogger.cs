using System;
using BenchmarkDotNet.Loggers;

namespace FinanceTracker.Benchmarks.Logging;

/// <summary>
/// Intercepts the BenchmarkDotNet stream and extracts real progress events.
/// </summary>
public sealed class CleanConsoleLogger : ILogger
{
    public static readonly CleanConsoleLogger Instance = new CleanConsoleLogger();

    public string Id => nameof(CleanConsoleLogger);
    public int Priority => 0;

    public void Write(LogKind logKind, string text) { }

    public void WriteLine(LogKind logKind, string text)
    {
        if (String.IsNullOrWhiteSpace(value: text)) 
            return;

        if (text.StartsWith(value: "// Found ", comparisonType: StringComparison.Ordinal) && text.Contains(value: "benchmark"))
        {
            string[] parts = text.Split(separator: ' ', options: StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3 && Int32.TryParse(s: parts[2], result: out int total))
                BenchmarkConsoleReporter.Instance.OnClassTotalKnown(total: total);
            return;
        }

        if (text.StartsWith(value: "// Benchmark:", comparisonType: StringComparison.Ordinal))
        {
            string after = text["// Benchmark:".Length..].TrimStart();
            int colon = after.IndexOf(value: ':');
            if (colon <= 0)
                return;
            
            string fullName = after[..colon].Trim();
            int dot = fullName.LastIndexOf(value: '.');
            if (dot <= 0)
                return;
            
            string cls = fullName[..dot].Replace(oldValue: "Benchmarks", newValue: String.Empty);
            string method = fullName[(dot + 1)..];

            string rowCount = "";
            int rcStart = text.IndexOf(value: "[RowCount=", comparisonType: StringComparison.Ordinal);
            if (rcStart >= 0)
            {
                int rcEnd = text.IndexOf(value: ']', startIndex: rcStart);
                if (rcEnd > rcStart)
                    rowCount = text[(rcStart + "[RowCount=".Length)..rcEnd];
            }

            BenchmarkConsoleReporter.Instance.OnBenchmarkStarted(className: cls, method: method, rowCount: rowCount);
            return;
        }

        // "// ** Remained N (X %) benchmark(s) to run." — один бенчмарк завершён
        if (text.StartsWith(value: "// ** Remained", comparisonType: StringComparison.Ordinal))
            BenchmarkConsoleReporter.Instance.OnBenchmarkDone();
    }

    public void WriteLine() { }
    public void Flush()     { }
}