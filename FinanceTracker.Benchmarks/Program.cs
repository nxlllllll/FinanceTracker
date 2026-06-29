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

        Job job = Job.Default.WithToolchain(toolchain: InProcessEmitToolchain.Instance).WithWarmupCount(count: 1).WithIterationCount(count: 3);

        IConfig config = CreateConfig(job: job, logWriter: logWriter);

        IConfig saveConfig = CreateConfig(
            job: job.WithInvocationCount(count: 1).WithUnrollFactor(factor: 1),
            logWriter: logWriter
        );
        
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
        
        BenchmarkRunner.Run<EventStoreSaveBenchmarks>(config: saveConfig);

        string reportPath = await AnalyticsHtmlExporter.Default.Flush(outputDir: Path.Combine(basePath, "Reports"));

        BenchmarkConsoleReporter.Instance.OnSuiteEnd(reportPath: Path.GetFullPath(path: reportPath));

        await BenchmarkDatabase.Instance.DisposeAsync();
    }

    private static IConfig CreateConfig(Job job, StreamWriter logWriter)
    {
        return ManualConfig.CreateEmpty()
            .AddJob(newJobs: job)
            .AddLogger(loggers: CleanConsoleLogger.Instance)
            .AddLogger(loggers: new StreamLogger(writer: logWriter))
            .AddEventProcessor(eventProcessors: BenchmarkEventProcessor.Instance)
            .AddExporter(exporters: AnalyticsHtmlExporter.Default)
            .AddColumnProvider(newColumnProviders: DefaultColumnProviders.Instance)
            .AddDiagnoser(diagnosers: MemoryDiagnoser.Default)
            .WithOption(option: ConfigOptions.DisableOptimizationsValidator, value: true);
    }

    private static async Task PrintAppliedMigrationsAsync(StreamWriter logWriter)
    {
        string directoryLine = $"Migrations directory: {BenchmarkDatabase.Instance.MigrationsDirectory}";
        IReadOnlyList<string> applied = BenchmarkDatabase.Instance.AppliedMigrations;
        string[] appliedLines = [$"Applied {applied.Count} migration(s):", ..applied.Select(selector: migration => $"  - {migration}")];

        PrintToConsole(directoryLine: directoryLine, appliedLines: appliedLines);
        await WriteToLogAsync(logWriter: logWriter, lines: [directoryLine, ..appliedLines]);
    }

    private static void PrintToConsole(string directoryLine, IReadOnlyList<string> appliedLines)
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine(value: directoryLine);

        Console.ForegroundColor = ConsoleColor.Green;
        foreach (string line in appliedLines)
            Console.WriteLine(value: line);

        Console.WriteLine();
        Console.ResetColor();
    }

    private static async Task WriteToLogAsync(StreamWriter logWriter, IReadOnlyList<string> lines)
    {
        foreach (string line in lines)
            await logWriter.WriteLineAsync(value: line);

        await logWriter.WriteLineAsync(value: String.Empty);
        await logWriter.FlushAsync();
    }
}