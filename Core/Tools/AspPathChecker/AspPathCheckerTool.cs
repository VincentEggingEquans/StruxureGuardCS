using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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

    public string ToolKey => Tools.ToolKeys.AspPathChecker;

    public ValidationResult Validate(ToolRunContext ctx)
    {
        var r = new ValidationResult();

        var aspText = ctx.Parameters.GetString(AspPathCheckerParameterKeys.AspText);
        var pathText = ctx.Parameters.GetString(AspPathCheckerParameterKeys.PathText);
        var mode = (ctx.Parameters.GetString(AspPathCheckerParameterKeys.Mode) ?? "build").Trim();

        if (!mode.Equals("build", StringComparison.OrdinalIgnoreCase) &&
            !mode.Equals("check", StringComparison.OrdinalIgnoreCase))
        {
            r.AddError("asppath.mode", $"Ongeldige mode '{mode}'. Verwacht 'build' of 'check'.");
        }

        if (string.IsNullOrWhiteSpace(aspText))
            r.AddError("asppath.asp", "ASP input is leeg.");

        if (string.IsNullOrWhiteSpace(pathText))
            r.AddError("asppath.path", "Path input is leeg.");

        var outputFile = ctx.Parameters.GetString(AspPathCheckerParameterKeys.OutputFile);
        if (string.IsNullOrWhiteSpace(outputFile))
            r.AddError("asppath.output", "OutputFile ontbreekt.");

        return r;
    }

    public Task<ToolResult> RunAsync(
        ToolRunContext ctx,
        IProgress<ToolProgressInfo>? progress,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var mode = (ctx.Parameters.GetString(AspPathCheckerParameterKeys.Mode) ?? "build")
            .Trim()
            .ToLowerInvariant();

        var aspRaw = ctx.Parameters.GetString(AspPathCheckerParameterKeys.AspText) ?? "";
        var pathRaw = ctx.Parameters.GetString(AspPathCheckerParameterKeys.PathText) ?? "";
        var outputFile = ctx.Parameters.GetString(AspPathCheckerParameterKeys.OutputFile) ?? "";

        // --- Parse ASP names (TSV: Name column) ---
        var aspLines = ExtractColumnOrLines(
            raw: aspRaw,
            wantedColumnName: "Name",
            logPrefix: "asp");

        // --- Parse Paths (TSV: Path column) ---
        var pathLines = ExtractColumnOrLines(
            raw: pathRaw,
            wantedColumnName: "Path",
            logPrefix: "path");

        Log.Info("asppath",
            $"Run start mode='{mode}' aspRawLen={aspRaw.Length} pathRawLen={pathRaw.Length} aspExtracted={aspLines.Count} pathExtracted={pathLines.Count}");

        // Build match map: asp -> first unused path containing asp (case-insensitive)
        var matches = new Dictionary<int, int>(); // aspIdx -> pathIdx
        var usedPathIdx = new HashSet<int>();

        for (int a = 0; a < aspLines.Count; a++)
        {
            var asp = aspLines[a];
            if (string.IsNullOrWhiteSpace(asp))
                continue;

            int? best = null;
            for (int p = 0; p < pathLines.Count; p++)
            {
                if (usedPathIdx.Contains(p)) continue;

                var path = pathLines[p];
                if (path.IndexOf(asp, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    best = p;
                    break;
                }
            }

            if (best.HasValue)
            {
                matches[a] = best.Value;
                usedPathIdx.Add(best.Value);
            }
        }

        var rows = new List<AspPathRowDto>(aspLines.Count);

        for (int a = 0; a < aspLines.Count; a++)
        {
            var asp = aspLines[a];

            if (matches.TryGetValue(a, out var pIdx))
            {
                rows.Add(new AspPathRowDto(
                    AspName: asp,
                    Path: pathLines[pIdx],
                    Status: "Match gevonden (nog niet gecontroleerd)"));
            }
            else
            {
                rows.Add(new AspPathRowDto(
                    AspName: asp,
                    Path: "",
                    Status: "Geen path gevonden (geen match)"));
            }
        }

        var aspWithoutMatch = aspLines.Count - matches.Count;
        var pathsWithoutAsp = pathLines.Count - matches.Count;

        Log.Info("asppath",
            $"BuildList done asp={aspLines.Count} paths={pathLines.Count} matches={matches.Count} aspNoMatch={aspWithoutMatch} pathsNoAsp={Math.Max(0, pathsWithoutAsp)}");

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

            EnsureOutputFolder(outputFile);

            // List-based check (geen disk-check): bestaat = match => Path niet leeg
            var total = indices.Count;
            for (int i = 0; i < total; i++)
            {
                ct.ThrowIfCancellationRequested();

                if (i == 0 || (i % 25) == 0 || i == total - 1)
                {
                    progress?.Report(new ToolProgressInfo(
                        Done: i,
                        Total: total,
                        CurrentItem: null,
                        Message: "Paden controleren...",
                        Phase: "check",
                        Percent: ToolProgressInfo.ComputePercent(i, total),
                        IsIndeterminate: false));
                }

                var idx = indices[i];
                if (idx < 0 || idx >= rows.Count)
                    continue;

                checkedCount++;

                var r = rows[idx];
                var aspName = (r.AspName ?? "").Trim();
                var p = (r.Path ?? "").Trim();

                var exists = !string.IsNullOrWhiteSpace(p);
                if (!exists)
                {
                    missingCount++;
                    LogMissing(outputFile,
                        aspName.Length > 0 ? aspName : "<GEEN ASP>",
                        "<GEEN PATH GEVONDEN (GEEN MATCH)>");
                }

                rows[idx] = r with
                {
                    Status = exists ? "Bestaat" : "Ontbreekt (gelogd)"
                };
            }

            progress?.Report(new ToolProgressInfo(
                Done: total,
                Total: total,
                CurrentItem: null,
                Message: "Klaar",
                Phase: "check",
                Percent: 100,
                IsIndeterminate: false));

            Log.Info("asppath", $"Check done checked={checkedCount} missing={missingCount}");
        }

        var dto = new AspPathCheckerResultDto(
            Rows: rows,
            AspCount: aspLines.Count,
            PathCount: pathLines.Count,
            MatchCount: matches.Count,
            AspWithoutMatchCount: aspWithoutMatch,
            PathsWithoutAspCount: Math.Max(0, pathsWithoutAsp),
            CheckedCount: checkedCount,
            MissingCount: missingCount,
            OutputFile: outputFile);

        var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = false });

        var summary = mode == "check"
            ? $"ASP={aspLines.Count} Paths={pathLines.Count} Matches={matches.Count} Checked={checkedCount} Missing={missingCount}"
            : $"ASP={aspLines.Count} Paths={pathLines.Count} Matches={matches.Count}";

        Log.Info("asppath", $"Run done mode='{mode}' {summary}");

        return Task.FromResult(
            ToolResult.Ok(summary)
                .WithOutput(OutputKeyResultJson, json));
    }

    private static List<string> ExtractColumnOrLines(string raw, string wantedColumnName, string logPrefix)
    {
        var lines = SplitLinesKeepEmpty(raw);
        var nonEmpty = lines.Where(l => !string.IsNullOrWhiteSpace(l)).ToList();

        bool tsvDetected = nonEmpty.Count > 0 && nonEmpty[0].Contains('\t');
        bool headerDetected = false;
        int colIndex = -1;
        int dupSkipped = 0;

        var extracted = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (tsvDetected && nonEmpty.Count >= 1)
        {
            var header = nonEmpty[0];
            var cols = header.Split('\t');
            colIndex = Array.FindIndex(cols, c => c.Trim().Equals(wantedColumnName, StringComparison.OrdinalIgnoreCase));
            headerDetected = colIndex >= 0;

            if (headerDetected)
            {
                for (int i = 1; i < nonEmpty.Count; i++)
                {
                    var parts = nonEmpty[i].Split('\t');
                    if (colIndex >= parts.Length) continue;

                    var v = (parts[colIndex] ?? "").Trim();
                    if (string.IsNullOrWhiteSpace(v)) continue;

                    if (seen.Add(v))
                        extracted.Add(v);
                    else
                        dupSkipped++;
                }

                Log.Info("asppath",
                    $"{logPrefix}: tsvDetected=True headerDetected=True col='{wantedColumnName}' colIndex={colIndex} inputLines={nonEmpty.Count} extracted={extracted.Count} dupSkipped={dupSkipped}");

                return extracted;
            }
        }

        // Fallback: treat as simple lines
        foreach (var l in nonEmpty)
        {
            var v = l.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(v)) continue;

            if (seen.Add(v))
                extracted.Add(v);
            else
                dupSkipped++;
        }

        Log.Info("asppath",
            $"{logPrefix}: tsvDetected={tsvDetected} headerDetected={headerDetected} col='{wantedColumnName}' colIndex={colIndex} inputLines={nonEmpty.Count} extracted={extracted.Count} dupSkipped={dupSkipped}");

        return extracted;
    }

    private static List<string> SplitLinesKeepEmpty(string s)
    {
        if (string.IsNullOrEmpty(s)) return new List<string>();

        s = s.Replace("\r\n", "\n").Replace("\r", "\n");
        return s.Split('\n').ToList();
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

        return result.Distinct().OrderBy(x => x).ToList();
    }

    private static void EnsureOutputFolder(string outputFile)
    {
        try
        {
            var dir = Path.GetDirectoryName(outputFile);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);
        }
        catch (Exception ex)
        {
            Log.Warn("asppath", $"EnsureOutputFolder failed output='{outputFile}': {ex.GetType().Name}: {ex.Message}\n{ex}");
        }
    }

    private static void LogMissing(string outputFile, string aspName, string missingPath)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        var line = $"[{timestamp}] ASP '{aspName}' ontbreekt → Path: {missingPath}{Environment.NewLine}";

        try
        {
            File.AppendAllText(outputFile, line);
            Log.Warn("asppath", $"Missing logged asp='{aspName}' path='{missingPath}'");
        }
        catch (Exception ex)
        {
            Log.Error("asppath", $"LogMissing failed output='{outputFile}': {ex.GetType().Name}: {ex.Message}\n{ex}");
        }
    }
}
