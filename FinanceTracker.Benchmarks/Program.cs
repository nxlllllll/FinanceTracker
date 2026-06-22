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

        string basePath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "FinanceTracker", "FinanceTracker.Benchmarks");

        Directory.CreateDirectory(path: Path.Combine(basePath, "Artifacts"));

        string logPath = Path.Combine(basePath, "Artifacts", $"BenchmarkRun-{DateTime.Now:yyyyMMdd-HHmmss}.log");
        await using StreamWriter logWriter = new StreamWriter(path: logPath, append: false, encoding: Encoding.UTF8);

        await DbInitSpinner.RunAsync(async () => await BenchmarkDatabase.Instance.InitializeAsync());

        await PrintAppliedMigrationsAsync(logWriter: logWriter);

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

        IConfig saveConfig = ManualConfig.CreateEmpty()
            .AddJob(newJobs: Job.Default
                .WithToolchain(toolchain: InProcessEmitToolchain.Instance)
                .WithWarmupCount(count: 1)
                .WithIterationCount(count: 3)
                .WithInvocationCount(count: 1)
                .WithUnrollFactor(factor: 1)
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
            typeof(BudgetLookupBenchmarks),
            typeof(CategoryBenchmarks),
            typeof(CategoryLookupBenchmarks),
            typeof(CurrencyRateBenchmarks),
            typeof(EventStoreBenchmarks),
            typeof(RecurringTransactionBenchmarks),
            typeof(RecurringTransactionLookupBenchmarks),
            typeof(TransactionBenchmarks),
            typeof(TransactionLookupBenchmarks),
            typeof(TransferBenchmarks),
            typeof(TransferLookupBenchmarks),
            typeof(UserBenchmarks),
            typeof(UserLookupBenchmarks),
        ]).RunAll(config: config);
        
        BenchmarkRunner.Run<EventStoreSaveBenchmarks>(config: saveConfig);

        ConsoleProgressExporter.Instance.Finish();

        string reportPath = await AnalyticsHtmlExporter.Default.Flush(outputDir: Path.Combine(basePath, "Reports"));

        BenchmarkConsoleReporter.Instance.OnSuiteEnd(reportPath: Path.GetFullPath(path: reportPath));

        await BenchmarkDatabase.Instance.DisposeAsync();
    }

    private static async Task PrintAppliedMigrationsAsync(StreamWriter logWriter)
    {
        string directory = BenchmarkDatabase.Instance.MigrationsDirectory;
        IReadOnlyList<string> applied = BenchmarkDatabase.Instance.AppliedMigrations;

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine(value: $"Migrations directory: {directory}");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(value: $"Applied {applied.Count} migration(s):");
        foreach (string migration in applied)
            Console.WriteLine(value: $"  - {migration}");
        Console.WriteLine();
        Console.ResetColor();

        await logWriter.WriteLineAsync(value: $"Migrations directory: {directory}");
        await logWriter.WriteLineAsync(value: $"Applied {applied.Count} migration(s):");
        foreach (string migration in applied)
            await logWriter.WriteLineAsync(value: $"  - {migration}");

        await logWriter.WriteLineAsync(value: String.Empty);
        await logWriter.FlushAsync();
    }
}