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
    public string ToolKey => ToolKeys.AspPathChecker;

    public const string OutputKeyResultJson = "ResultJson";

    public ValidationResult Validate(ToolRunContext ctx)
    {
        var r = new ValidationResult();

        var asp = ctx.Parameters.GetString(AspPathCheckerParameterKeys.AspText) ?? "";
        var paths = ctx.Parameters.GetString(AspPathCheckerParameterKeys.PathText) ?? "";

        if (string.IsNullOrWhiteSpace(asp) && string.IsNullOrWhiteSpace(paths))
            r.AddError("asppath.input", "Vul eerst ASP-namen en/of paden in.");

        return r;
    }

    public Task<ToolResult> RunAsync(
        ToolRunContext ctx,
        IProgress<ToolProgressInfo>? progress,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var mode = (ctx.Parameters.GetString(AspPathCheckerParameterKeys.Mode) ?? "build").Trim().ToLowerInvariant();

        var aspText = ctx.Parameters.GetString(AspPathCheckerParameterKeys.AspText) ?? "";
        var pathText = ctx.Parameters.GetString(AspPathCheckerParameterKeys.PathText) ?? "";

        var outputFile = ctx.Parameters.GetString(AspPathCheckerParameterKeys.OutputFile) ?? "";

        var engine = new AspPathCheckerEngine();

        Log.Info("asppath", $"Run start mode='{mode}' aspLen={aspText.Length} pathLen={pathText.Length}");

        progress?.Report(ToolProgressInfo.Indeterminate("Lijst opbouwen...", phase: "build"));

        var built = engine.BuildList(aspText, pathText);

        var rows = built.Rows;
        int checkedCount = 0;
        int missingCount = 0;

        if (mode == "check")
        {
            var checkAll = (ctx.Parameters.GetString(AspPathCheckerParameterKeys.CheckAll) ?? "false")
                .Equals("true", StringComparison.OrdinalIgnoreCase);

            List<int> indices;
            if (checkAll)
            {
                indices = Enumerable.Range(0, rows.Count).ToList();
            }
            else
            {
                indices = ParseIndices(ctx.Parameters.GetString(AspPathCheckerParameterKeys.SelectedIndices));
            }

            Log.Info("asppath", $"Check requested all={checkAll} indices={indices.Count}");

            var total = indices.Count;
            for (int i = 0; i < total; i += 25)
            {
                ct.ThrowIfCancellationRequested();
                progress?.Report(new ToolProgressInfo(
                    Done: Math.Min(i, total),
                    Total: total,
                    CurrentItem: null,
                    Message: "Paden controleren...",
                    Phase: "check",
                    Percent: ToolProgressInfo.ComputePercent(Math.Min(i, total), total),
                    IsIndeterminate: false));
            }

            var checkedResult = engine.CheckRows(rows, indices, outputFile);
            rows = checkedResult.Rows;
            checkedCount = checkedResult.CheckedCount;
            missingCount = checkedResult.MissingCount;

            progress?.Report(new ToolProgressInfo(
                Done: total,
                Total: total,
                CurrentItem: null,
                Message: "Controle klaar.",
                Phase: "check",
                Percent: 100,
                IsIndeterminate: false));
        }

        var dto = new AspPathCheckerResultDto(
            Rows: rows,
            AspCount: built.AspCount,
            PathCount: built.PathCount,
            MatchCount: built.MatchCount,
            AspWithoutMatchCount: built.AspWithoutMatchCount,
            PathsWithoutAspCount: built.PathsWithoutAspCount,
            CheckedCount: checkedCount,
            MissingCount: missingCount,
            OutputFile: outputFile);

        var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = false });

        var summary = mode == "check"
            ? $"Checked={checkedCount} Missing={missingCount} Rows={rows.Count}"
            : $"Rows={rows.Count} Matches={built.MatchCount}";

        Log.Info("asppath", $"Run done mode='{mode}' {summary}");

        return Task.FromResult(
            ToolResult.Ok(summary)
                .WithOutput(OutputKeyResultJson, json));
    }

    private static List<int> ParseIndices(string? csv)
    {
        var result = new List<int>();
        if (string.IsNullOrWhiteSpace(csv)) return result;

        foreach (var part in csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (int.TryParse(part, out var i))
                result.Add(i);
        }

        // de-dup, keep stable order
        return result.Distinct().OrderBy(x => x).ToList();
    }
}
