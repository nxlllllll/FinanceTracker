namespace FinanceTracker.Benchmarks.Logging;

public static class DbInitSpinner
{
    public static async Task RunAsync(Func<Task> action)
    {
        try
        {
            Console.CursorVisible = false;
        } catch { }

        CancellationTokenSource cts = new CancellationTokenSource();
        Task spinner = Task.Run(function: () => Spin(cts.Token), cancellationToken: cts.Token);

        try
        {
            await action();
        }
        finally
        {
            await cts.CancelAsync();
            await spinner;
        }

        Console.SetCursorPosition(left: 0, top: Console.CursorTop);
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write(value: "  ✓ ");
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write(value: "Database ready");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine(value: "                                       ");
        Console.WriteLine();
        Console.ResetColor();

        try
        {
            Console.CursorVisible = true;
        } catch { }
    }

    private static readonly string[] _frames = ["⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏"];

    private static readonly string[] _messages =
    [
        "Connecting to Docker",
        "Starting PostgreSQL container",
        "Waiting for readiness",
        "Running migrations",
        "Seeding benchmark data",
        "Finalizing setup",
    ];

    private static async Task Spin(CancellationToken ct)
    {
        int frame = 0, msgIdx = 0, elapsed = 0;

        while (!ct.IsCancellationRequested)
        {
            string spinner = _frames[frame % _frames.Length];
            string message = _messages[msgIdx % _messages.Length];

            Console.SetCursorPosition(left: 0, top: Console.CursorTop);
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.Write(value: $"  {spinner} ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write(value: "Initializing database  ");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write(value: $"{message}");

            int dots = (elapsed / 2) % 4;
            Console.Write(value: new string(c: '.', count: dots).PadRight(totalWidth: 4));
            Console.ResetColor();

            int leftover = Console.WindowWidth - Console.CursorLeft - 1;
            if (leftover > 0)
                Console.Write(value: new string(c: ' ', count: leftover));

            frame++;
            elapsed++;

            if (elapsed % 15 == 0) 
                msgIdx++;

            try
            {
                await Task.Delay(millisecondsDelay: 100, cancellationToken: ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}