using System.Text;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Reports;

namespace FinanceTracker.Benchmarks.Exporters;

public sealed class AnalyticsHtmlExporter : IExporter
{
    public static readonly AnalyticsHtmlExporter Default = new();
    public string Name => nameof(AnalyticsHtmlExporter);

    private readonly List<Summary> _summaries = [];
    private readonly Lock _lock = new();

    public IEnumerable<string> ExportToFiles(Summary summary, ILogger consoleLogger)
    {
        lock (_lock) _summaries.Add(summary);
        return [];
    }

    public void ExportToLog(Summary summary, ILogger logger) { }

    public async Task<string> Flush(string outputDir)
    {
        List<Summary> summaries;
        lock (_lock) summaries = [.._summaries];
        if (summaries.Count == 0) return string.Empty;

        Directory.CreateDirectory(outputDir);
        string path = Path.Combine(outputDir, $"BenchmarkAnalytics-{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.html");
        await File.WriteAllTextAsync(path, BuildHtml(summaries), Encoding.UTF8);
        return path;
    }

    // ── Row model ─────────────────────────────────────────────────────────

    private sealed record Row(
        string Class, string Method, string RowCount, bool Success,
        double MeanMs, double MedianMs, double StdDevMs, double MinMs, double MaxMs,
        double AllocKb, long Gen0, long Gen1, long Gen2);

    // ── Build ─────────────────────────────────────────────────────────────

    private static string BuildHtml(List<Summary> summaries)
    {
        string tpl     = LoadTemplate();
        var    allRows = summaries.SelectMany(ExtractRows).ToList();
        var    classes = allRows.Select(r => r.Class).Distinct().OrderBy(x => x).ToList();
        string date    = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
        string runtime = summaries[0].HostEnvironmentInfo.RuntimeVersion;
        string? os      = summaries[0].HostEnvironmentInfo.Os.Value.Version;

        string overviewTab  = """<button class="tab-btn active" id="tab-overview" onclick="showTab('overview')">Overview</button>""";
        string overviewPage = $"""<div class="page active" id="page-overview">{OverviewPage(allRows, classes, date)}</div>""";

        return tpl
            .Replace("{{TITLE}}",       "FinanceTracker Benchmarks")
            .Replace("{{RUN_META}}",    $"{runtime} · {os}")
            .Replace("{{RUN_DATE}}",    date)
            .Replace("{{KPI_CHIPS}}",   KpiChips(allRows))
            .Replace("{{TAB_BUTTONS}}", overviewTab + TabButtons(classes, allRows))
            .Replace("{{SECTIONS}}",    overviewPage + ClassPages(allRows, classes));
    }

    private static string LoadTemplate()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Exporters", "analytics-template.html");
        if (!File.Exists(path))
            throw new FileNotFoundException("analytics-template.html not found.", path);
        return File.ReadAllText(path, Encoding.UTF8);
    }

    private static List<Row> ExtractRows(Summary summary)
    {
        var rows = new List<Row>();
        foreach (var report in summary.Reports)
        {
            var    bc    = report.BenchmarkCase;
            bool   ok    = report.Success && report.ResultStatistics is not null;
            double? alloc = ok ? report.GcStats.GetBytesAllocatedPerOperation(bc) / 1024.0 : 0;
            rows.Add(new Row(
                bc.Descriptor.Type.Name.Replace("Benchmarks", ""),
                bc.Descriptor.WorkloadMethod.Name,
                bc.Parameters.ValueInfo, ok,
                ok ? report.ResultStatistics!.Mean              / 1_000_000 : 0,
                ok ? report.ResultStatistics!.Median            / 1_000_000 : 0,
                ok ? report.ResultStatistics!.StandardDeviation / 1_000_000 : 0,
                ok ? report.ResultStatistics!.Min               / 1_000_000 : 0,
                ok ? report.ResultStatistics!.Max               / 1_000_000 : 0,
                alloc ?? double.MinValue,
                ok ? report.GcStats.Gen0Collections : 0,
                ok ? report.GcStats.Gen1Collections : 0,
                ok ? report.GcStats.Gen2Collections : 0));
        }
        return rows;
    }

    // ── KPI chips ─────────────────────────────────────────────────────────

    private static string KpiChips(List<Row> rows)
    {
        int    total   = rows.Count;
        int    failed  = rows.Count(r => !r.Success);
        int    classes = rows.Select(r => r.Class).Distinct().Count();
        var    slowest = rows.Where(r => r.Success).OrderByDescending(r => r.MeanMs).FirstOrDefault();
        string slow    = slowest is not null ? FormatMs(slowest.MeanMs) : "—";

        return $"""
            {Chip("info",  total.ToString(),          "benchmarks")}
            {Chip("ok",    (total - failed).ToString(), "passed")}
            {Chip(failed > 0 ? "fail" : "ok", failed.ToString(), "failed")}
            {Chip("info",  classes.ToString(),        "classes")}
            {Chip("warn",  slow,                      "slowest")}
            """;
    }

    private static string Chip(string k, string v, string l) => $"""
        <div class="kpi-chip {k}">
          <span class="dot"></span>
          <span class="val">{v}</span>
          <span class="lbl">{l}</span>
        </div>
        """;

    // ── Tabs ──────────────────────────────────────────────────────────────

    private static string TabButtons(List<string> classes, List<Row> allRows)
    {
        var sb = new StringBuilder();
        foreach (string cls in classes)
        {
            string slug   = Slug(cls);
            int    count  = allRows.Count(r => r.Class == cls);
            string errDot = allRows.Any(r => r.Class == cls && !r.Success)
                ? """<span class="tab-err"></span>"""
                : "";
            sb.AppendLine($"""
                <button class="tab-btn" id="tab-{slug}" onclick="showTab('{slug}')">
                  {cls}<span class="tab-count">{count}</span>{errDot}
                </button>
                """);
        }
        return sb.ToString();
    }

    // ── Class pages ───────────────────────────────────────────────────────

    private static string ClassPages(List<Row> allRows, List<string> classes)
    {
        var sb = new StringBuilder();
        foreach (string cls in classes)
        {
            var rows = allRows.Where(r => r.Class == cls).ToList();
            sb.AppendLine($"""
                <div class="page" id="page-{Slug(cls)}">
                  {ClassPage(cls, rows)}
                </div>
                """);
        }
        return sb.ToString();
    }

    private static string ClassPage(string cls, List<Row> rows)
    {
        var    methods   = rows.Select(r => r.Method).Distinct().OrderBy(x => x).ToList();
        var    rowCounts = SortedRc(rows);
        int    failed    = rows.Count(r => !r.Success);
        int    passed    = rows.Count(r => r.Success);
        string failBadge = failed > 0 ? $"""<span class="badge badge-red">{failed} failed</span>""" : "";

        return $"""
            <div class="page-header">
              <div>
                <div class="page-title">{cls}</div>
                <div class="page-meta">{methods.Count} methods · {rowCounts.Count} row-count variants · {rows.Count} benchmarks</div>
              </div>
              <div class="badges">
                <span class="badge badge-cyan">{methods.Count} methods</span>
                <span class="badge badge-green">{passed} passed</span>
                {failBadge}
              </div>
            </div>
            {StatCards(rows, rowCounts)}
            <div class="chart-card" style="margin-bottom:14px">
              {MeanChartInner(cls, rows, methods, rowCounts)}
            </div>
            <div class="chart-card" style="margin-bottom:20px">
              {AllocChartInner(cls, rows, methods, rowCounts)}
            </div>
            {Heatmap(cls, rows, methods, rowCounts)}
            <div class="sec-divider">Results by method</div>
            {GroupedTable(cls, rows, methods, rowCounts)}
            {AnomaliesSection(rows, rowCounts)}
            {MemorySection(rows, rowCounts)}
            """;
    }

    // ── Stat cards ────────────────────────────────────────────────────────

    private static string StatCards(List<Row> rows, List<string> rowCounts)
    {
        var ok = rows.Where(r => r.Success).ToList();
        if (ok.Count == 0) return "";

        string maxRc    = rowCounts.LastOrDefault() ?? "";
        var    maxRcOk  = ok.Where(r => r.RowCount == maxRc).ToList();
        double minMean  = ok.Min(r => r.MeanMs);
        double maxMean  = ok.Max(r => r.MeanMs);
        double avgMean  = ok.Average(r => r.MeanMs);
        double peakMb   = (maxRcOk.Count > 0 ? maxRcOk.Max(r => r.AllocKb) : 0) / 1024.0;
        bool   gen2     = ok.Any(r => r.Gen2 > 0);
        int    failed   = rows.Count(r => !r.Success);
        string allocStr = peakMb > 1
            ? peakMb.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + " MB"
            : (peakMb * 1024).ToString("F0", System.Globalization.CultureInfo.InvariantCulture) + " KB";

        return $"""
            <div class="stats-row">
              {SC("cyan",  FormatMs(minMean), "Best mean",  ok.OrderBy(r => r.MeanMs).First().Method)}
              {SC("red",   FormatMs(maxMean), "Worst mean", ok.OrderByDescending(r => r.MeanMs).First().Method)}
              {SC("amber", FormatMs(avgMean), "Avg mean",   $"{ok.Count} benchmarks")}
              {SC(peakMb > 50 ? "red" : "cyan", allocStr,   "Peak alloc", $"@ {CleanRc(maxRc)} rows")}
              {SC(gen2   ? "red" : "green", gen2   ? "Yes" : "None", "Gen2 GC",  gen2   ? "LOH pressure" : "No LOH pressure")}
              {SC(failed > 0 ? "red" : "green", failed.ToString(), "Failed", failed > 0 ? "check setup" : "all passed")}
            </div>
            """;
    }

    private static string SC(string c, string v, string l, string s) => $"""
        <div class="stat-card {c}">
          <div class="stat-label">{l}</div>
          <div class="stat-val {c}">{v}</div>
          <div class="stat-sub">{EscHtml(s)}</div>
        </div>
        """;

    // ── Chart helpers ─────────────────────────────────────────────────────

    private static readonly string[] CC =
    [
        "#38bdf8","#4ade80","#f87171","#fbbf24","#a78bfa",
        "#f472b6","#34d399","#fb923c","#60a5fa","#e879f9"
    ];

    private static string Tooltip => """
        tooltip:{
          backgroundColor:'#161d2c',
          borderColor:'rgba(255,255,255,.08)',
          borderWidth:1,
          titleColor:'#e8ecf4',
          bodyColor:'#8b93a8',
          padding:10
        }
        """;

    private static string Scales => """
        scales:{
          x:{ticks:{color:'#4d5568',font:{family:'JetBrains Mono',size:10}},grid:{color:'rgba(255,255,255,.04)'}},
          y:{ticks:{color:'#4d5568',font:{family:'JetBrains Mono',size:10}},grid:{color:'rgba(255,255,255,.04)'},beginAtZero:true}
        }
        """;

    private static string Legend(IEnumerable<(string method, int i)> items, string chartCanvasId) =>
        string.Concat(items.Select(x =>
        {
            string lid = "leg_" + chartCanvasId + "_" + x.i;
            return $"""
                <span class="legend-item" id="{lid}" onclick="toggleDataset('{lid}',{x.i})">
                  <span class="legend-swatch" style="background:{CC[x.i % CC.Length]}"></span>
                  {EscHtml(x.method)}
                </span>
                """;
        }));

    // ── Mean chart ────────────────────────────────────────────────────────

    private static string MeanChartInner(string cls, List<Row> rows, List<string> methods, List<string> rowCounts)
    {
        string id     = "mc_" + Slug(cls);
        string labels = JsStrArray(rowCounts.Select(CleanRc));

        var datasets = methods.Select((m, i) =>
        {
            string c    = CC[i % CC.Length];
            string data = string.Join(",", rowCounts.Select(rc =>
            {
                var r = rows.FirstOrDefault(x => x.Method == m && x.RowCount == rc);
                return r is { Success: true } ? F4(r.MeanMs) : "null";
            }));
            return "{label:'" + EscJs(m) + "',data:[" + data + "]," +
                   "borderColor:'" + c + "',backgroundColor:'" + c + "22'," +
                   "pointBackgroundColor:'" + c + "'," +
                   "pointRadius:4,pointHoverRadius:6,tension:.35,fill:false,borderWidth:2}";
        });

        string legend = Legend(methods.Select((m, i) => (m, i)), id);

        string chartCall3 = "new Chart(document.getElementById('" + id + "')," +
            "{type:'line',data:{labels:" + labels + ",datasets:[" + string.Join(",", datasets) + "]}," +
            "options:{responsive:true,maintainAspectRatio:false," +
            "interaction:{mode:'index',intersect:false}," +
            "plugins:{legend:{display:false}," + Tooltip + "}," + Scales + "}});";

        return $"""
            <div class="chart-title">
              Mean execution time
              <span style="color:var(--text3);font-weight:400">ms · by row count</span>
            </div>
            <div class="chart-legend">{legend}</div>
            <div class="chart-wrap" style="height:260px">
              <canvas id="{id}" role="img" aria-label="Mean time chart"></canvas>
            </div>
            <script>{chartCall3}</script>
            """;
    }

    // ── Alloc chart — X: методы, datasets: RowCount ──────────────────────

    private static string AllocChartInner(string cls, List<Row> rows, List<string> methods, List<string> rowCounts)
    {
        string id = "ac_" + Slug(cls);
        var    ok = rows.Where(r => r.Success).ToList();
        if (ok.Count == 0) return "<div class=\"chart-title\">Alloc — no data</div>";

        // X = методы, каждый dataset = один RowCount
        string labels = JsStrArray(methods);

        var datasets = rowCounts.Select((rc, i) =>
        {
            string c    = CC[i % CC.Length];
            string data = string.Join(",", methods.Select(m =>
            {
                var r = ok.FirstOrDefault(x => x.Method == m && x.RowCount == rc);
                return r is not null ? F4(r.AllocKb / 1024.0) : "null";
            }));
            return "{label:'" + EscJs(CleanRc(rc)) + "',data:[" + data + "]," +
                   "backgroundColor:'" + c + "cc',borderColor:'" + c + "'," +
                   "borderWidth:1,borderRadius:3,borderSkipped:false}";
        });

        string legend = Legend(rowCounts.Select((rc, i) => (CleanRc(rc), i)), id);

        string chartCall4 = "new Chart(document.getElementById('" + id + "')," +
            "{type:'bar',data:{labels:" + labels + ",datasets:[" + string.Join(",", datasets) + "]}," +
            "options:{responsive:true,maintainAspectRatio:false," +
            "interaction:{mode:'index',intersect:false}," +
            "plugins:{legend:{display:false}," + Tooltip + "}," + Scales + "}});";

        return $"""
            <div class="chart-title">
              Allocated
              <span style="color:var(--text3);font-weight:400">MB · by method</span>
            </div>
            <div class="chart-legend">{legend}</div>
            <div class="chart-wrap" style="height:220px">
              <canvas id="{id}" role="img" aria-label="Allocated MB by method"></canvas>
            </div>
            <script>{chartCall4}</script>
            """;
    }

    // ── Heatmap ───────────────────────────────────────────────────────────

    private static string Heatmap(string cls, List<Row> rows, List<string> methods, List<string> rowCounts)
    {
        var ok = rows.Where(r => r.Success).ToList();
        if (ok.Count == 0) return "";

        double gmin = ok.Min(r => r.MeanMs);
        double gmax = ok.Max(r => r.MeanMs);

        var headerCells = rowCounts.Select(rc => $"<th>{CleanRc(rc)}</th>");

        var bodyRows = methods.Select(m =>
        {
            var cells = rowCounts.Select(rc =>
            {
                var r = rows.FirstOrDefault(x => x.Method == m && x.RowCount == rc);
                if (r is null || !r.Success)
                    return """<td class="heatmap-cell-wrap"><div class="heatmap-cell" style="background:rgba(255,255,255,.03);color:#4d5568">NA</div></td>""";

                double t  = gmax > gmin ? (r.MeanMs - gmin) / (gmax - gmin) : 0;
                string bg = HeatColor(t);
                string tc = t > 0.55 ? "#fff" : "#e8ecf4";
                return $"""
                    <td class="heatmap-cell-wrap">
                      <div class="heatmap-cell"
                           style="background:{bg};color:{tc}"
                           title="{EscHtml(m)} @ {CleanRc(rc)}: {FormatMs(r.MeanMs)}">
                        {FormatMs(r.MeanMs)}
                      </div>
                    </td>
                    """;
            });
            return $"""
                <tr>
                  <td class="heatmap-method" title="{EscHtml(m)}">{EscHtml(m)}</td>
                  {string.Concat(cells)}
                </tr>
                """;
        });

        return $"""
            <div class="heatmap-wrap">
              <div class="heatmap-title">
                Performance heatmap — mean time (cyan = fastest · red = slowest)
              </div>
              <table class="heatmap-table">
                <thead>
                  <tr>
                    <th></th>
                    {string.Concat(headerCells)}
                  </tr>
                </thead>
                <tbody>
                  {string.Concat(bodyRows)}
                </tbody>
              </table>
            </div>
            """;
    }

    // ── Grouped table ─────────────────────────────────────────────────────

    private static string GroupedTable(string cls, List<Row> rows, List<string> methods, List<string> rowCounts)
    {
        string tableId  = "tbl_" + Slug(cls);
        double classMax = rows.Where(r => r.Success).Select(r => r.MeanMs).DefaultIfEmpty(1).Max();
        double classMin = rows.Where(r => r.Success).Select(r => r.MeanMs).DefaultIfEmpty(0).Min();

        string[] colNames = ["Method / RowCount", "RowCount", "Mean", "Median", "StdDev", "Min", "Max", "Alloc MB", "Gen0", "Gen1", "Gen2"];
        bool[]   colNum   = [false, false, true, true, true, true, true, true, true, true, true];

        var headers = colNames.Select((name, i) => $"""
            <th onclick="sortTable('{tableId}',{i},{colNum[i].ToString().ToLower()})">{name}</th>
            """);

        var groupRows = new StringBuilder();
        int methodIdx  = 0;
        foreach (string m in methods)
        {
            string groupId  = Slug(cls) + "_" + methodIdx++;
            var    mRows    = rows.Where(r => r.Method == m).ToList();
            var    mOk      = mRows.Where(r => r.Success).ToList();
            int    mFailed  = mRows.Count(r => !r.Success);
            string mBest    = mOk.Count > 0 ? FormatMs(mOk.Min(r => r.MeanMs)) : "—";
            string mWorst   = mOk.Count > 0 ? FormatMs(mOk.Max(r => r.MeanMs)) : "—";
            double peakMb   = mOk.Count > 0 ? mOk.Max(r => r.AllocKb) / 1024.0 : 0;
            string allocBdg = $"""
            <span class="group-badge" style="color:{(peakMb > 50 ? "var(--amber)" : "var(--text2))")}">
            {peakMb.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)} MB peak
            </span>
            """;
            string failBdg  = mFailed > 0 ? $"""<span class="group-badge" style="color:var(--red)">{mFailed} failed</span>""" : "";

            // Group header
            groupRows.AppendLine($"""
                <tr class="group-row" id="grp-{groupId}" onclick="toggleGroup('{groupId}')" data-group="{groupId}">
                  <td colspan="11">
                    <div class="group-toggle">
                      <span class="arrow">▶</span>
                      {EscHtml(m)}
                      <span class="group-badge">{mRows.Count} results</span>
                      {allocBdg}
                      {failBdg}
                      <span class="group-meta" style="margin-left:auto;padding-right:16px">{mBest} – {mWorst}</span>
                    </div>
                  </td>
                </tr>
                """);

            // Detail panel — сразу после group-row, ДО data-rows → открывается над данными
            groupRows.AppendLine($"""
                <tr class="detail-row" id="det-{groupId}" data-group="{groupId}">
                  <td colspan="11">
                    {MethodDetail(groupId, m, mRows, rowCounts)}
                  </td>
                </tr>
                """);

            // Data rows
            foreach (string rc in rowCounts)
            {
                var r = mRows.FirstOrDefault(x => x.RowCount == rc);
                if (r is null) continue;

                if (!r.Success)
                {
                    groupRows.AppendLine($"""
                        <tr class="data-row" data-group="{groupId}">
                          <td style="padding-left:32px">{EscHtml(m)}</td>
                          <td data-val="{CleanRc(rc)}">{CleanRc(rc)}</td>
                          <td colspan="9" class="cell-na">failed / NA</td>
                        </tr>
                        """);
                    continue;
                }

                string pc       = PerfClass(r.MeanMs, classMin, classMax);
                string barColor = pc == "cell-fast" ? "#4ade80" : pc == "cell-slow" ? "#f87171" : "#fbbf24";
                int    barW     = (int)(classMax > 0 ? r.MeanMs / classMax * 36 : 0);
                string bar      = $"""<span class="inline-bar" style="width:{barW}px;background:{barColor}"></span>""";
                string gen2Sty  = r.Gen2 > 0 ? "style=\"color:var(--red)\"" : "";

                groupRows.AppendLine($"""
                    <tr class="data-row" data-group="{groupId}">
                      <td style="padding-left:32px;color:var(--text2);font-size:11px">{EscHtml(m)}</td>
                      <td data-val="{CleanRc(rc)}">{CleanRc(rc)}</td>
                      <td class="{pc}" data-val="{F4(r.MeanMs)}">{FormatMs(r.MeanMs)}{bar}</td>
                      <td data-val="{F4(r.MedianMs)}">{FormatMs(r.MedianMs)}</td>
                      <td data-val="{F4(r.StdDevMs)}">{FormatMs(r.StdDevMs)}</td>
                      <td data-val="{F4(r.MinMs)}">{FormatMs(r.MinMs)}</td>
                      <td data-val="{F4(r.MaxMs)}">{FormatMs(r.MaxMs)}</td>
                      <td data-val="{F4(r.AllocKb / 1024.0)}">{(r.AllocKb / 1024.0).ToString("F3", System.Globalization.CultureInfo.InvariantCulture)}</td>
                      <td data-val="{r.Gen0}">{r.Gen0}</td>
                      <td data-val="{r.Gen1}">{r.Gen1}</td>
                      <td data-val="{r.Gen2}" {gen2Sty}>{r.Gen2}</td>
                    </tr>
                    """);
            }
        }

        return $"""
            <div class="table-wrap">
              <table id="{tableId}">
                <thead>
                  <tr>{string.Concat(headers)}</tr>
                </thead>
                <tbody>
                  {groupRows.ToString()}
                </tbody>
              </table>
            </div>
            """;
    }

    // ── Method detail panel ───────────────────────────────────────────────

    private static string MethodDetail(string groupId, string method, List<Row> rows, List<string> rowCounts)
    {
        var ok = rows.Where(r => r.Success).ToList();
        if (ok.Count == 0) return "";

        // Statistics
        double avg    = ok.Average(r => r.MeanMs);
        double stddev = ok.Count > 1
            ? Math.Sqrt(ok.Sum(r => Math.Pow(r.MeanMs - avg, 2)) / (ok.Count - 1))
            : 0;
        double cv     = avg > 0 ? stddev / avg * 100 : 0;
        double peakMb = ok.Max(r => r.AllocKb) / 1024.0;
        bool   gen2   = ok.Any(r => r.Gen2 > 0);
        string cvCls  = cv < 10 ? "good" : cv < 30 ? "mid" : "bad";

        string statsSection = $"""
            <div class="detail-section">
              <div class="detail-section-title">Statistics</div>
              <div class="stat-mini-grid">
                <div class="stat-mini">
                  <div class="stat-mini-label">Min mean</div>
                  <div class="stat-mini-val good">{FormatMs(ok.Min(r => r.MeanMs))}</div>
                </div>
                <div class="stat-mini">
                  <div class="stat-mini-label">Max mean</div>
                  <div class="stat-mini-val bad">{FormatMs(ok.Max(r => r.MeanMs))}</div>
                </div>
                <div class="stat-mini">
                  <div class="stat-mini-label">Avg mean</div>
                  <div class="stat-mini-val">{FormatMs(avg)}</div>
                </div>
                <div class="stat-mini">
                  <div class="stat-mini-label">CV (σ/μ)</div>
                  <div class="stat-mini-val {cvCls}">{cv.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)}%</div>
                </div>
                <div class="stat-mini">
                  <div class="stat-mini-label">Peak alloc</div>
                  <div class="stat-mini-val {(peakMb > 50 ? "bad" : "")}">{peakMb.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)} MB</div>
                </div>
                <div class="stat-mini">
                  <div class="stat-mini-label">Gen2 GC</div>
                  <div class="stat-mini-val {(gen2 ? "bad" : "good")}">{(gen2 ? "Yes" : "None")}</div>
                </div>
              </div>
            </div>
            """;

        // Chart — inline script с уникальным id
        string chartSection = "";
        if (ok.Count >= 2)
        {
            string canvasId = "dc_" + groupId;
            var sorted      = ok.OrderBy(r => { int.TryParse(CleanRc(r.RowCount), out int n); return n; }).ToList();
            string labels   = JsStrArray(sorted.Select(r => CleanRc(r.RowCount)));
            string meanData = string.Join(",", sorted.Select(r => F4(r.MeanMs)));
            string minData  = string.Join(",", sorted.Select(r => F4(r.MinMs)));
            string maxData  = string.Join(",", sorted.Select(r => F4(r.MaxMs)));

            string detailScript =
                "(function(){" +
                "var el=document.getElementById('" + canvasId + "');" +
                "if(!el||el._done)return;el._done=true;" +
                "new Chart(el,{type:'line',data:{labels:" + labels + ",datasets:[" +
                "{label:'Mean',data:[" + meanData + "],borderColor:'#38bdf8',backgroundColor:'#38bdf822',pointRadius:4,tension:.35,fill:false,borderWidth:2}," +
                "{label:'Min',data:[" + minData + "],borderColor:'#4ade80',borderDash:[4,3],pointRadius:3,tension:.35,fill:false,borderWidth:1.5}," +
                "{label:'Max',data:[" + maxData + "],borderColor:'#f87171',borderDash:[4,3],pointRadius:3,tension:.35,fill:false,borderWidth:1.5}" +
                "]},options:{responsive:true,maintainAspectRatio:false,interaction:{mode:'index',intersect:false}," +
                "plugins:{legend:{display:false}," + Tooltip + "}," + Scales + "}});" +
                "})();";

            chartSection = $"""
                <div class="detail-section">
                  <div class="detail-section-title">Mean · Min · Max by row count</div>
                  <div style="display:flex;gap:10px;margin-bottom:10px;font-size:10px">
                    <span style="display:flex;align-items:center;gap:4px;color:var(--text2)">
                      <span style="width:8px;height:2px;background:#38bdf8;display:inline-block"></span>Mean
                    </span>
                    <span style="display:flex;align-items:center;gap:4px;color:var(--text2)">
                      <span style="width:8px;height:2px;background:#4ade80;display:inline-block;opacity:.7"></span>Min
                    </span>
                    <span style="display:flex;align-items:center;gap:4px;color:var(--text2)">
                      <span style="width:8px;height:2px;background:#f87171;display:inline-block;opacity:.7"></span>Max
                    </span>
                  </div>
                  <div class="detail-chart-wrap">
                    <canvas id="{canvasId}" role="img" aria-label="Method detail chart for {EscHtml(method)}"></canvas>
                  </div>
                  <script>{detailScript}</script>
                </div>
                """;
        }

        // Degradation table
        string degSection = DegradationSection(ok);

        return $"""
            <div class="detail-inner">
              {chartSection}
              {statsSection}
              {degSection}
            </div>
            """;
    }

    // ── Degradation section ───────────────────────────────────────────────

    private static string DegradationSection(List<Row> ok)
    {
        if (ok.Count < 2) return "";

        var sorted = ok.OrderBy(r => { int.TryParse(CleanRc(r.RowCount), out int n); return n; }).ToList();

        var transitionRows = new StringBuilder();
        for (int i = 1; i < sorted.Count; i++)
        {
            var    prev      = sorted[i - 1];
            var    curr      = sorted[i];
            double ratio     = prev.MeanMs > 0 ? curr.MeanMs / prev.MeanMs : 1;
            string ratioCls  = ratio < 2 ? "ok" : ratio < 5 ? "mid" : "bad";
            string ratioStr  = ratio >= 100 ? $"{ratio:F0}×" : $"{ratio:F1}×";

            transitionRows.AppendLine($"""
                <tr>
                  <td>{CleanRc(prev.RowCount)} → {CleanRc(curr.RowCount)}</td>
                  <td>{FormatMs(curr.MeanMs)}</td>
                  <td class="deg-ratio {ratioCls}">{ratioStr}</td>
                </tr>
                """);
        }

        if (sorted.Count >= 2)
        {
            double total    = sorted[0].MeanMs > 0 ? sorted[^1].MeanMs / sorted[0].MeanMs : 1;
            string totalCls = total < 5 ? "ok" : total < 20 ? "mid" : "bad";
            string totalStr = total >= 100 ? $"{total:F0}×" : $"{total:F1}×";
            transitionRows.AppendLine($"""
                <tr style="border-top:1px solid var(--border2)">
                  <td style="color:var(--text3)">Overall</td>
                  <td></td>
                  <td class="deg-ratio {totalCls}">{totalStr}</td>
                </tr>
                """);
        }

        return $"""
            <div class="detail-section">
              <div class="detail-section-title">Degradation analysis</div>
              <table class="degradation-table">
                <tr>
                  <td style="color:var(--text3);font-size:9px">Transition</td>
                  <td style="color:var(--text3);font-size:9px">Time</td>
                  <td style="color:var(--text3);font-size:9px">Ratio</td>
                </tr>
                {transitionRows}
              </table>
            </div>
            """;
    }

    // ── Anomalies section ─────────────────────────────────────────────────

    private static string AnomaliesSection(List<Row> rows, List<string> rowCounts)
    {
        var items = new List<(string kind, string icon, string title, string detail)>();

        foreach (var r in rows.Where(r => !r.Success))
            items.Add(("error", "✗",
                $"Failed — {r.Method} @ {CleanRc(r.RowCount)}",
                "No results — check GlobalSetup / IterationSetup"));

        foreach (var r in rows.Where(r => r.Success && r.MeanMs > 0 && r.StdDevMs / r.MeanMs > 0.20))
            items.Add(("warn", "~",
                $"High variance — {r.Method} @ {CleanRc(r.RowCount)}",
                $"StdDev {FormatMs(r.StdDevMs)} = {r.StdDevMs / r.MeanMs * 100:F0}% of mean — increase IterationCount"));

        foreach (string m in rows.Select(r => r.Method).Distinct())
        {
            var sorted = rows.Where(r => r.Method == m && r.Success)
                .OrderBy(r => { int.TryParse(CleanRc(r.RowCount), out int n); return n; })
                .ToList();
            if (sorted.Count < 2) continue;
            double ratio = sorted[0].MeanMs > 0 ? sorted[^1].MeanMs / sorted[0].MeanMs : 1;
            if (ratio > 10)
                items.Add(("warn", "↑",
                    $"Degradation — {m}",
                    $"{CleanRc(sorted[0].RowCount)}→{CleanRc(sorted[^1].RowCount)}: {FormatMs(sorted[0].MeanMs)} → {FormatMs(sorted[^1].MeanMs)} ({ratio:F0}×)"));
        }

        foreach (var r in rows.Where(r => r.Success && r.AllocKb > 51200))
            items.Add(("warn", "M",
                $"Heavy alloc — {r.Method} @ {CleanRc(r.RowCount)}",
                $"{FormatMb(r.AllocKb)} per operation — consider pagination"));

        foreach (var r in rows.Where(r => r.Success && r.Gen2 > 0))
            items.Add(("info", "G",
                $"Gen2 GC — {r.Method} @ {CleanRc(r.RowCount)}",
                $"Gen2={r.Gen2} collections — LOH pressure"));

        if (items.Count == 0) return "";

        var cards = items.Select(x => $"""
            <div class="anomaly-card">
              <div class="anomaly-icon {x.kind}">{x.icon}</div>
              <div>
                <div class="anomaly-title">{EscHtml(x.title)}</div>
                <div class="anomaly-detail">{EscHtml(x.detail)}</div>
              </div>
            </div>
            """);

        return $"""
            <div class="sec-divider">Anomalies &amp; warnings</div>
            <div class="anomaly-grid">
              {string.Concat(cards)}
            </div>
            """;
    }

    // ── Memory section ────────────────────────────────────────────────────

    private static string MemorySection(List<Row> rows, List<string> rowCounts)
    {
        string maxRc = rowCounts.LastOrDefault() ?? "";
        var    top   = rows.Where(r => r.RowCount == maxRc && r.Success)
                           .OrderByDescending(r => r.AllocKb)
                           .ToList();
        if (top.Count == 0) return "";

        var cards = top.Select(r =>
        {
            string alloc = FormatMb(r.AllocKb);
            return $"""
                <div class="mem-card">
                  <div class="mem-method" title="{EscHtml(r.Method)}">{EscHtml(r.Method)}</div>
                  <div class="mem-alloc">{alloc}</div>
                  <div class="mem-gc">
                    <div class="mem-gc-item"><span class="g0d"></span>G0 {r.Gen0}</div>
                    <div class="mem-gc-item"><span class="g1d"></span>G1 {r.Gen1}</div>
                    <div class="mem-gc-item"><span class="g2d"></span>G2 {r.Gen2}</div>
                  </div>
                </div>
                """;
        });

        return $"""
            <div class="sec-divider">Memory @ RowCount={CleanRc(maxRc)}</div>
            <div class="mem-grid">
              {string.Concat(cards)}
            </div>
            """;
    }

    // ── Overview page ─────────────────────────────────────────────────────

    private static string OverviewPage(List<Row> allRows, List<string> classes, string date)
    {
        var ok = allRows.Where(r => r.Success).ToList();
        if (ok.Count == 0) return "<p style='color:var(--text3)'>No data</p>";

        int    totalBench   = allRows.Count;
        int    totalFailed  = allRows.Count(r => !r.Success);
        double globalMin    = ok.Min(r => r.MeanMs);
        double globalMax    = ok.Max(r => r.MeanMs);
        double globalAvg    = ok.Average(r => r.MeanMs);
        double globalPeakMb = ok.Max(r => r.AllocKb) / 1024.0;
        bool   anyGen2      = ok.Any(r => r.Gen2 > 0);

        // ── Global stat cards ──
        string globalStats = $"""
            <div class="stats-row" style="grid-template-columns:repeat(auto-fill,minmax(160px,1fr))">
              {SC("cyan",  totalBench.ToString(),   "Total benchmarks",  $"{classes.Count} classes")}
              {SC(totalFailed > 0 ? "red" : "green", totalFailed.ToString(), "Failed", totalFailed > 0 ? "check setup" : "all passed")}
              {SC("cyan",  FormatMs(globalMin),     "Global best",       ok.OrderBy(r => r.MeanMs).First().Method)}
              {SC("red",   FormatMs(globalMax),     "Global worst",      ok.OrderByDescending(r => r.MeanMs).First().Method)}
              {SC("amber", FormatMs(globalAvg),     "Global avg",        $"{ok.Count} results")}
              {SC(globalPeakMb > 100 ? "red" : "cyan", $"{globalPeakMb.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)} MB", "Peak alloc", ok.OrderByDescending(r => r.AllocKb).First().Method)}
              {SC(anyGen2 ? "red" : "green", anyGen2 ? "Yes" : "None",   "Gen2 GC",           anyGen2 ? "LOH pressure" : "Clean")}
            </div>
            """;

        // ── Top 10 slowest ──
        string maxRcGlobal = ok.Select(r => r.RowCount)
            .Distinct()
            .OrderByDescending(x => { int.TryParse(CleanRc(x), out int n); return n; })
            .First();

        var slowest10 = ok.Where(r => r.RowCount == maxRcGlobal)
            .OrderByDescending(r => r.MeanMs).Take(10).ToList();
        double s10Max = slowest10.Max(r => r.MeanMs);

        var slowRows = slowest10.Select(r =>
        {
            string pc    = r.MeanMs >= s10Max * 0.66 ? "cell-slow" : r.MeanMs >= s10Max * 0.33 ? "cell-medium" : "cell-fast";
            string barC  = pc == "cell-slow" ? "#f87171" : pc == "cell-medium" ? "#fbbf24" : "#4ade80";
            int    barW  = (int)(r.MeanMs / s10Max * 160);
            string g2Sty = r.Gen2 > 0 ? "style=\"color:var(--red)\"" : "";
            return $"""
                <tr>
                  <td style="font-weight:600">{EscHtml(r.Method)}</td>
                  <td style="color:var(--text3);font-family:var(--mono);font-size:11px">{EscHtml(r.Class)}</td>
                  <td class="{pc}">{FormatMs(r.MeanMs)}</td>
                  <td>{FormatMb(r.AllocKb)}</td>
                  <td {g2Sty}>{r.Gen2}</td>
                  <td><div style="background:{barC};height:6px;width:{barW}px;border-radius:3px;opacity:.7"></div></td>
                </tr>
                """;
        });

        string slowestTable = $"""
            <div class="sec-divider">Top 10 slowest methods @ RowCount={CleanRc(maxRcGlobal)}</div>
            <div class="table-wrap"><table class="overview-table">
                <thead>
                  <tr>
                    <th style="text-align:left">Method</th>
                    <th style="text-align:left">Class</th>
                    <th>Mean</th>
                    <th>Alloc MB</th>
                    <th>Gen2</th>
                    <th style="text-align:left;width:200px">Bar</th>
                  </tr>
                </thead>
                <tbody>{string.Concat(slowRows)}</tbody>
              </table>
            </div>
            """;

        // ── Top 10 heaviest alloc ──
        var heavy10 = ok.Where(r => r.RowCount == maxRcGlobal)
            .OrderByDescending(r => r.AllocKb).Take(10).ToList();
        double h10Max = heavy10.Max(r => r.AllocKb);

        var heavyRows = heavy10.Select(r =>
        {
            int    barW  = (int)(r.AllocKb / h10Max * 160);
            string g2Sty = r.Gen2 > 0 ? "style=\"color:var(--red)\"" : "";
            return $"""
                <tr>
                  <td style="font-weight:600">{EscHtml(r.Method)}</td>
                  <td style="color:var(--text3);font-family:var(--mono);font-size:11px">{EscHtml(r.Class)}</td>
                  <td style="color:var(--cyan)">{FormatMb(r.AllocKb)}</td>
                  <td>{FormatMs(r.MeanMs)}</td>
                  <td>{r.Gen0}</td><td>{r.Gen1}</td>
                  <td {g2Sty}>{r.Gen2}</td>
                  <td><div style="background:#38bdf8;height:6px;width:{barW}px;border-radius:3px;opacity:.6"></div></td>
                </tr>
                """;
        });

        string heavyTable = $"""
            <div class="sec-divider">Top 10 heaviest allocations @ RowCount={CleanRc(maxRcGlobal)}</div>
            <div class="table-wrap"><table class="overview-table">
                <thead>
                  <tr>
                    <th style="text-align:left">Method</th>
                    <th style="text-align:left">Class</th>
                    <th>Alloc MB</th>
                    <th>Mean</th>
                    <th>Gen0</th><th>Gen1</th><th>Gen2</th>
                    <th style="text-align:left;width:200px">Bar</th>
                  </tr>
                </thead>
                <tbody>{string.Concat(heavyRows)}</tbody>
              </table>
            </div>
            """;

        // ── Worst degradation ──
        var degList = ok
            .Select(r => (r.Class, r.Method))
            .Distinct()
            .Select(pair =>
            {
                var mRows = ok.Where(r => r.Class == pair.Class && r.Method == pair.Method)
                    .OrderBy(r => { int.TryParse(CleanRc(r.RowCount), out int n); return n; })
                    .ToList();
                if (mRows.Count < 2) return default;
                double ratio = mRows[0].MeanMs > 0 ? mRows[^1].MeanMs / mRows[0].MeanMs : 1;
                return (pair.Class, pair.Method, ratio, mRows[0].MeanMs, mRows[^1].MeanMs);
            })
            .Where(x => x != default)
            .OrderByDescending(x => x.ratio)
            .Take(10)
            .ToList();

        var degRows = degList.Select(x =>
        {
            string rc  = x.ratio < 2 ? "cell-fast" : x.ratio < 10 ? "cell-medium" : "cell-slow";
            string rs  = x.ratio >= 100 ? $"{x.ratio:F0}×" : $"{x.ratio:F1}×";
            return $"""
                <tr>
                  <td style="font-weight:600">{EscHtml(x.Method)}</td>
                  <td style="color:var(--text3);font-family:var(--mono);font-size:11px">{EscHtml(x.Class)}</td>
                  <td style="color:var(--green)">{FormatMs(x.Item4)}</td>
                  <td style="color:var(--red)">{FormatMs(x.Item5)}</td>
                  <td class="{rc}" style="font-weight:600">{rs}</td>
                </tr>
                """;
        });

        string degTable = degList.Count > 0 ? $"""
            <div class="sec-divider">Worst degradation (min → max RowCount)</div>
            <div class="table-wrap"><table class="overview-table">
                <thead>
                  <tr>
                    <th style="text-align:left">Method</th>
                    <th style="text-align:left">Class</th>
                    <th>Min mean</th>
                    <th>Max mean</th>
                    <th>Ratio</th>
                  </tr>
                </thead>
                <tbody>{string.Concat(degRows)}</tbody>
              </table>
            </div>
            """ : "";

        // ── Gen2 table ──
        var gen2Rows = ok.Where(r => r.Gen2 > 0)
            .OrderByDescending(r => r.Gen2).Take(10).ToList();

        var gen2RowsHtml = gen2Rows.Select(r => $"""
            <tr>
              <td style="font-weight:600">{EscHtml(r.Method)}</td>
              <td style="color:var(--text3);font-family:var(--mono);font-size:11px">{EscHtml(r.Class)}</td>
              <td>{CleanRc(r.RowCount)}</td>
              <td style="color:var(--red);font-weight:600">{r.Gen2}</td>
              <td>{FormatMb(r.AllocKb)}</td>
              <td>{FormatMs(r.MeanMs)}</td>
            </tr>
            """);

        string gen2Table = gen2Rows.Count > 0 ? $"""
            <div class="sec-divider">Gen2 GC pressure</div>
            <div class="table-wrap"><table class="overview-table">
                <thead>
                  <tr>
                    <th style="text-align:left">Method</th>
                    <th style="text-align:left">Class</th>
                    <th>RowCount</th>
                    <th>Gen2</th>
                    <th>Alloc MB</th>
                    <th>Mean</th>
                  </tr>
                </thead>
                <tbody>{string.Concat(gen2RowsHtml)}</tbody>
              </table>
            </div>
            """ : "";

        // ── Per-class summary cards ──
        var classSummaryCards = classes.Select(cls =>
        {
            var cOk    = ok.Where(r => r.Class == cls).ToList();
            int cFailed = allRows.Count(r => r.Class == cls && !r.Success);
            if (cOk.Count == 0) return "";
            double cMin  = cOk.Min(r => r.MeanMs);
            double cMax  = cOk.Max(r => r.MeanMs);
            string color = cFailed > 0 ? "red" : "cyan";
            return $"""
                <div class="stat-card {color}" style="cursor:pointer" onclick="showTab('{Slug(cls)}')">
                  <div class="stat-label">{EscHtml(cls)}</div>
                  <div class="stat-val {color}">{FormatMs(cMin)} – {FormatMs(cMax)}</div>
                  <div class="stat-sub">{cOk.Count} passed · {cFailed} failed · click to open</div>
                </div>
                """;
        });

        string classSummary = $"""
            <div class="sec-divider">Class summary</div>
            <div class="stats-row" style="grid-template-columns:repeat(auto-fill,minmax(220px,1fr))">
              {string.Concat(classSummaryCards)}
            </div>
            """;

        return $"""
            <div class="page-header">
              <div>
                <div class="page-title">Overview</div>
                <div class="page-meta">{classes.Count} classes · {ok.Count} passed · {date}</div>
              </div>
            </div>
            {globalStats}
            {slowestTable}
            {heavyTable}
            {degTable}
            {gen2Table}
            {classSummary}
            """;
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static List<string> SortedRc(List<Row> rows) =>
        rows.Select(r => r.RowCount).Distinct()
            .OrderBy(x => { int.TryParse(CleanRc(x), out int n); return n; })
            .ToList();

    private static string FormatMs(double ms)
    {
        if (ms >= 1000) return (ms / 1000).ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + " s";
        if (ms >= 1)    return ms.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + " ms";
        return (ms * 1000).ToString("F0", System.Globalization.CultureInfo.InvariantCulture) + " µs";
    }

    private static string FormatMb(double kb)
    {
        double mb = kb / 1024.0;
        return mb >= 1
            ? mb.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + " MB"
            : kb.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + " KB";
    }

    private static string PerfClass(double mean, double min, double max)
    {
        if (max <= min) return "";
        double t = (mean - min) / (max - min);
        return t < 0.33 ? "cell-fast" : t < 0.66 ? "cell-medium" : "cell-slow";
    }

    private static string HeatColor(double t)
    {
        int r, g, b;
        if (t < 0.5)
        {
            double u = t * 2;
            r = (int)(56  + u * (251 - 56));
            g = (int)(189 + u * (191 - 189));
            b = (int)(248 + u * (36  - 248));
        }
        else
        {
            double u = (t - 0.5) * 2;
            r = (int)(251 + u * (248 - 251));
            g = (int)(191 + u * (113 - 191));
            b = (int)(36  + u * (113 - 36));
        }
        int a = (int)(40 + t * 160);
        return $"rgba({r},{g},{b},{a / 255.0:F2})";
    }

    private static string JsStrArray(IEnumerable<string> items)
        => "[" + string.Join(",", items.Select(x => $"'{EscJs(x)}'")) + "]";

    private static string CleanRc(string rc) => rc.Replace("[RowCount=", "").Replace("]", "");
    private static string Slug(string s)     => s.ToLowerInvariant().Replace(" ", "-").Replace(".", "-");
    private static string EscJs(string s)    => s.Replace("\\", "\\\\").Replace("'", "\\'");
    private static string EscHtml(string s)  => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
    private static string F4(double v)       => v.ToString("F4", System.Globalization.CultureInfo.InvariantCulture);
}