using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using StruxureGuard.Core.Logging;
using StruxureGuard.Core.Tools.Infrastructure;

namespace StruxureGuard.Core.Tools.AspPathChecker;

public sealed class AspPathCheckerTool : ITool
{
    public const string OutputKeyResultJson = "ResultJson";

    public string ToolKey => ToolKeys.AspPathChecker;
    public string DisplayName => "ASP Path Checker";
    public string Description => "Match ASP names tegen EBO paden (substring match) op basis van TSV input.";

    public ValidationResult Validate(ToolRunContext ctx)
    {
        var r = new ValidationResult();

        var mode = (ctx.Parameters.GetString(AspPathCheckerParameterKeys.Mode) ?? "build").Trim();
        if (!mode.Equals("build", StringComparison.OrdinalIgnoreCase) &&
            !mode.Equals("check", StringComparison.OrdinalIgnoreCase))
        {
            r.AddError("asppath.mode", $"Ongeldige mode '{mode}'. Verwacht 'build' of 'check'.");
        }

        var aspText = ctx.Parameters.GetString(AspPathCheckerParameterKeys.AspText) ?? "";
        var pathText = ctx.Parameters.GetString(AspPathCheckerParameterKeys.PathText) ?? "";

        if (string.IsNullOrWhiteSpace(aspText))
            r.AddError("asppath.asp", "ASP input is leeg.");

        if (string.IsNullOrWhiteSpace(pathText))
            r.AddError("asppath.path", "Path input is leeg.");

        return r;
    }

    public Task<ToolResult> RunAsync(
        ToolRunContext ctx,
        IProgress<ToolProgressInfo>? progress,
        CancellationToken ct)
    {
        var runId = Guid.NewGuid().ToString("N")[..8];
        var started = DateTime.UtcNow;

        var mode = (ctx.Parameters.GetString(AspPathCheckerParameterKeys.Mode) ?? "build").Trim();

        var aspRaw = ctx.Parameters.GetString(AspPathCheckerParameterKeys.AspText) ?? "";
        var pathRaw = ctx.Parameters.GetString(AspPathCheckerParameterKeys.PathText) ?? "";

        Log.Info("asppath", $"tool-runner start runId={runId} mode={mode}");

        var engine = new AspPathCheckerEngine();

        // BUILD
        var build = engine.Build(
            aspRaw: aspRaw,
            pathRaw: pathRaw,
            onProgress: (done, total, current) =>
            {
                progress?.Report(new ToolProgressInfo(
                    Done: done,
                    Total: total,
                    CurrentItem: current,
                    Message: "Matchen...",
                    Phase: "build",
                    Percent: ToolProgressInfo.ComputePercent(done, total),
                    IsIndeterminate: false));
            },
            ct: ct);

        int checkedCount = 0;
        int missingCount = 0;

        // CHECK (list-based, no disk IO)
        if (mode.Equals("check", StringComparison.OrdinalIgnoreCase))
        {
            var checkAll = (ctx.Parameters.GetString(AspPathCheckerParameterKeys.CheckAll) ?? "true")
                .Trim()
                .Equals("true", StringComparison.OrdinalIgnoreCase);

            List<int> indices;
            if (checkAll)
            {
                indices = Enumerable.Range(0, build.Rows.Count).ToList();
            }
            else
            {
                var selRaw = ctx.Parameters.GetString(AspPathCheckerParameterKeys.SelectedIndices) ?? "";
                indices = selRaw.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => int.TryParse(s.Trim(), out var v) ? v : -1)
                    .Where(v => v >= 0)
                    .Distinct()
                    .OrderBy(v => v)
                    .ToList();
            }

            Log.Info("asppath", $"Check start runId={runId} checkAll={checkAll} indices={indices.Count}");

            var check = engine.Check(
                rows: build.Rows,
                indices: indices,
                onProgress: (done, total) =>
                {
                    progress?.Report(new ToolProgressInfo(
                        Done: done,
                        Total: total,
                        CurrentItem: null,
                        Message: "Checken...",
                        Phase: "check",
                        Percent: ToolProgressInfo.ComputePercent(done, total),
                        IsIndeterminate: false));
                },
                ct: ct);

            checkedCount = check.CheckedCount;
            missingCount = check.MissingCount;

            progress?.Report(new ToolProgressInfo(
                Done: indices.Count,
                Total: indices.Count,
                CurrentItem: null,
                Message: "Klaar",
                Phase: "check",
                Percent: 100,
                IsIndeterminate: false));
        }

        var dto = new AspPathCheckerResultDto(
            Rows: build.Rows,
            AspCount: build.AspCount,
            PathCount: build.PathCount,
            MatchCount: build.MatchCount,
            AspWithoutMatchCount: build.AspWithoutMatchCount,
            PathsWithoutAspCount: build.PathsWithoutAspCount,
            CheckedCount: checkedCount,
            MissingCount: missingCount);

        var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = false });

        var summary = mode.Equals("check", StringComparison.OrdinalIgnoreCase)
            ? $"ASP={build.AspCount} Paths={build.PathCount} Matches={build.MatchCount} Checked={checkedCount} Missing={missingCount}"
            : $"ASP={build.AspCount} Paths={build.PathCount} Matches={build.MatchCount}";

        var finished = DateTime.UtcNow;
        var ms = (long)(finished - started).TotalMilliseconds;

        Log.Info("asppath", $"tool-runner finish runId={runId} ms={ms} summary='{summary}'");

        return Task.FromResult(
            ToolResult.Ok(summary, startedUtc: started, finishedUtc: finished)
                .WithOutput(OutputKeyResultJson, json));
    }
}
