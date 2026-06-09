using System.Text;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using FinanceTracker.Benchmarks.Benchmarks;
using FinanceTracker.Benchmarks.Exporters;
using FinanceTracker.Benchmarks.Infrastructure;
using FinanceTracker.Benchmarks.Logging;

namespace FinanceTracker.Benchmarks;

internal class Program
{
    public static async Task Main(string[] args)
    {
        Console.InputEncoding  = Encoding.UTF8;
        Console.OutputEncoding = Encoding.UTF8;

        await DbInitSpinner.RunAsync(async () => await BenchmarkDatabase.Instance.InitializeAsync());
        

        string basePath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "FinanceTracker", "FinanceTracker.Benchmarks");

        Directory.CreateDirectory(path: Path.Combine(basePath, "Artifacts"));

        string logPath = Path.Combine(basePath, "Artifacts", $"BenchmarkRun-{DateTime.Now:yyyyMMdd-HHmmss}.log");
        await using StreamWriter logWriter = new StreamWriter(path: logPath, append: false, encoding: Encoding.UTF8);

        IConfig config = ManualConfig.CreateEmpty()
            .AddJob(newJobs: Job.Default
                .WithToolchain(toolchain: InProcessEmitToolchain.Instance)
                .WithWarmupCount(count: 1)
                .WithIterationCount(count: 3)
            ).AddLogger(loggers: CleanConsoleLogger.Instance)
            .AddLogger(loggers: new StreamLogger(writer: logWriter))
            .AddExporter(exporters: ConsoleProgressExporter.Instance)
            .AddExporter(exporters: AnalyticsHtmlExporter.Default)
            .AddColumnProvider(newColumnProviders: DefaultColumnProviders.Instance)
            .AddDiagnoser(diagnosers: MemoryDiagnoser.Default)
            .WithOption(option: ConfigOptions.DisableOptimizationsValidator, value: true);

        BenchmarkConsoleReporter.Instance.OnSuiteStart();

        BenchmarkSwitcher.FromTypes(types: [
            typeof(AccountBenchmarks),
            typeof(BudgetBenchmarks),
            typeof(CategoryBenchmarks),
            typeof(CurrencyRateBenchmarks),
            typeof(EventStoreBenchmarks),
            typeof(RecurringTransactionBenchmarks),
            typeof(TransactionBenchmarks),
            typeof(TransferBenchmarks),
            typeof(UserBenchmarks),
        ]).RunAll(config: config);

        ConsoleProgressExporter.Instance.Finish();

        string reportPath = await AnalyticsHtmlExporter.Default.Flush(outputDir: Path.Combine(basePath, "Reports"));

        BenchmarkConsoleReporter.Instance.OnSuiteEnd(reportPath: Path.GetFullPath(path: reportPath));

        await BenchmarkDatabase.Instance.DisposeAsync();
    }
}