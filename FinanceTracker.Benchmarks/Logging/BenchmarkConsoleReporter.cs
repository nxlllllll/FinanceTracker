using System.Diagnostics;

namespace FinanceTracker.Benchmarks.Logging;

public sealed class BenchmarkConsoleReporter
{
    public static readonly BenchmarkConsoleReporter Instance = new();

    private readonly Stopwatch _totalTimer = new Stopwatch();
    private readonly Stopwatch _classTimer = new Stopwatch();
    private readonly Lock _lock = new Lock();

    private string _currentClass  = "";
    private string _currentMethod = "";
    private int _classTotal;
    private int _pendingTotal;
    private int _classDone;
    private int _classRow;
    private int _nextRow;
    private bool _classOpen;

    private int _totalPassed;
    private int _totalFailed;

    private Thread? _timerThread;
    private volatile bool _timerRunning;
    private int _blinkTick;

    private const int NameWidth    = 22;
    private const int CounterWidth = 18;
    
    public void OnSuiteStart()
    {
        _totalTimer.Restart();
        try
        {
            Console.CursorVisible = false;
        } catch { }
        _nextRow = Console.CursorTop;
    }

    public void OnClassTotalKnown(int total)
    {
        lock (_lock)
        {
            _pendingTotal = total;
            if (!_classOpen)
                return;
            
            _classTotal = total;
            RenderAndRestore(done: false);
        }
    }

    public void OnBenchmarkStarted(string className, string method, string rowCount)
    {
        lock (_lock)
        {
            if (_currentClass != className)
            {
                _currentClass = className;
                _classDone = 0;
                _classTotal = _pendingTotal;
                _pendingTotal = 0;
                _classOpen = true;
                _classTimer.Restart();

                _classRow = _nextRow;
                _nextRow++;

                Console.SetCursorPosition(left: 0, top: _classRow);
                RenderLineUnsafe(done: false);
                Console.SetCursorPosition(left: 0, top: _nextRow);

                _timerRunning = true;
                _timerThread  = new Thread(start: TimerLoop)
                {
                    IsBackground = true
                };
                _timerThread.Start();
            }

            _currentMethod = rowCount.Length > 0 ? $"{method} [{rowCount}]" : method;
            RenderAndRestore(done: false);
        }
    }

    public void OnBenchmarkDone()
    {
        lock (_lock)
        {
            if (!_classOpen) 
                return;
            
            _classDone++;
            RenderAndRestore(done: false);
        }
    }

    public void OnClassEnd(int passed, int failed)
    {
        _timerRunning = false;
        _timerThread?.Join(millisecondsTimeout: 300);

        lock (_lock)
        {
            if (!_classOpen)
                return;
            
            _classOpen = false;
            _classTotal = passed + failed;
            _classDone = _classTotal;

            _totalPassed += passed;
            _totalFailed += failed;

            RenderLineUnsafe(done: true, passed: passed, failed: failed);
            Console.SetCursorPosition(left: 0, top: _nextRow);
        }
    }

    private void TimerLoop()
    {
        while (_timerRunning)
        {
            Thread.Sleep(millisecondsTimeout: 500);
            if (!_timerRunning) 
                break;
            
            lock (_lock)
            {
                if (!_classOpen)
                    break;
                
                _blinkTick++;
                RenderAndRestore(done: false);
            }
        }
    }

    public void OnSuiteEnd(string? reportPath)
    {
        _totalTimer.Stop();
        int total = _totalPassed + _totalFailed;

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Gray;
        Console.WriteLine(value: "  " + new string(c: '─', count: 52));
        Console.ResetColor();
        Console.WriteLine();

        bool allPassed = _totalFailed == 0;
        Console.Write(value: "  Benchmark run summary: ");
        Console.ForegroundColor = allPassed ? ConsoleColor.Green : ConsoleColor.Red;
        Console.WriteLine(value: allPassed ? "Passed!" : "Failed!");
        Console.ResetColor();
        Console.WriteLine();

        WriteKv(label: "  total:    ", value: total.ToString(), color: ConsoleColor.White);
        WriteKv(label: "  failed:   ", value: _totalFailed.ToString(), color: _totalFailed > 0 ? ConsoleColor.Red : ConsoleColor.Gray);
        WriteKv(label: "  passed:   ", value: _totalPassed.ToString(), color: ConsoleColor.Green);

        TimeSpan e = _totalTimer.Elapsed;
        WriteKv(label: "  duration: ", value: $"{(int)e.TotalMinutes}m {e.Seconds}s {e.Milliseconds}ms", color: ConsoleColor.Gray);

        if (reportPath is not null)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.Write(value: "  Analytics report: ");
            Console.ResetColor();
            WriteHyperlink(text: reportPath, url: "file:///" + reportPath.Replace(oldChar: '\\', newChar: '/'));
            Console.WriteLine();
        }

        Console.WriteLine();
        try
        {
            Console.CursorVisible = true;
        } catch { }
    }

    private void RenderAndRestore(bool done)
    {
        int savedTop  = Console.CursorTop;
        int savedLeft = Console.CursorLeft;
        RenderLineUnsafe(done: done);
        Console.SetCursorPosition(left: savedLeft, top: savedTop);
    }

    private void RenderLineUnsafe(bool done) => RenderLineUnsafe(done: done, passed: _classDone, failed: 0);

    private void RenderLineUnsafe(bool done, int passed, int failed)
    {
        int winWidth = Console.WindowWidth;
        Console.SetCursorPosition(left: 0, top: _classRow);

        int current = done ? passed + failed : _classDone;
        int total = done ? passed + failed : _classTotal;

        ConsoleColor lineColor = done
            ? (failed == 0 ? ConsoleColor.Green : ConsoleColor.Red)
            : ConsoleColor.Yellow;

        if (done)
        {
            Console.ForegroundColor = lineColor;
            Console.Write(value: failed == 0 ? "  ✓ " : "  ✗ ");
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write(value: _blinkTick % 2 == 0 ? "  ● " : "  ○ ");
        }

        Console.ForegroundColor = ConsoleColor.White;
        string name = _currentClass.Length > NameWidth ? _currentClass[..NameWidth] : _currentClass.PadRight(NameWidth);
        Console.Write(value: name);
        Console.Write(value: "  ");

        // таймер=9, счётчик=CounterWidth+2, бар~50, pct=6
        const int otherCols = 9 + CounterWidth + 2 + 140 + 6;
        int methodBudget = Math.Max(20, winWidth - 4 - NameWidth - 2 - 2 - otherCols);

        if (!done)
        {
            string meth = _currentMethod.Length > methodBudget
                ? "…" + _currentMethod[^(methodBudget - 1)..]
                : _currentMethod.PadRight(methodBudget);
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write(meth);
        }
        else
        {
            Console.Write(value: new string(c: ' ', count: methodBudget));
        }
        Console.Write(value: "  ");

        TimeSpan el = _classTimer.Elapsed;
        string timer = el.TotalSeconds >= 60
            ? $"{(int)el.TotalMinutes}m{el.Seconds:D2}s"
            : $"{el.TotalSeconds:F1}s";
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write(value: timer.PadRight(totalWidth: 7));
        Console.Write(value: "  ");

        string counter = done
            ? (failed > 0 ? $"{passed}/{total} ({failed} failed)" : $"{passed}/{total} passed")
            : (total > 0  ? $"{current}/{total} passed"           : $"{current} done");

        Console.ForegroundColor = lineColor;
        Console.Write(value: counter.PadRight(CounterWidth));
        Console.Write(value: "  ");

        const int barDecor = 7; // "[" + "] " + "100%"
        int barW = winWidth - Console.CursorLeft - barDecor - 1;
        barW = Math.Max(4, barW);

        int filled = total > 0 ? (int)Math.Round((double)current / total * barW) : 0;
        int pct = total > 0 ? (int)Math.Round((double)current / total * 100)  : 0;
        filled = Math.Max(0, Math.Min(filled, barW));

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write(value: "[");
        Console.ForegroundColor = done
            ? (failed == 0 ? ConsoleColor.Green : ConsoleColor.Red)
            : ConsoleColor.Yellow;
        Console.Write(value: new string(c: '█', count: filled));
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write(value: new string(c: '░', count: barW - filled));
        Console.Write(value: "] ");
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write(value: total > 0 ? $"{pct,3}%" : "   ");

        Console.ResetColor();

        int leftover = winWidth - Console.CursorLeft - 1;
        if (leftover > 0)
            Console.Write(value: new string(c: ' ', count: leftover));
    }

    private static void WriteKv(string label, string value, ConsoleColor color)
    {
        Console.ForegroundColor = ConsoleColor.Gray;
        Console.Write(value: label);
        Console.ForegroundColor = color;
        Console.WriteLine(value: value);
        Console.ResetColor();
    }

    private static void WriteHyperlink(string text, string url)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write(value: $"\x1b]8;;{url}\x1b\\{text}\x1b]8;;\x1b\\");
        Console.ResetColor();
    }
}