using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using StruxureGuard.Core.Logging;

namespace StruxureGuard.Core.Tools.AspPathChecker;

public sealed class AspPathCheckerEngine
{
    public sealed record BuildResult(
        List<AspPathRowDto> Rows,
        int AspCount,
        int PathCount,
        int MatchCount,
        int AspWithoutMatchCount,
        int PathsWithoutAspCount);

    public sealed record CheckResult(
        List<AspPathRowDto> Rows,
        int CheckedCount,
        int MissingCount);

    public BuildResult Build(
        string aspRaw,
        string pathRaw,
        Action<int, int, string?>? onProgress,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        // Extract (TSV-aware, dedup, preserve order)
        var asps = AspNameExtractor.Extract(aspRaw ?? "", out var aspT);
        var paths = PathValueExtractor.Extract(pathRaw ?? "", out var pathT);

        Log.Info("asppath",
            $"Parse[asp] TSV={aspT.TsvDetected} header={aspT.HeaderDetected} colIndex={aspT.NameColumnIndex} linesIn={aspT.InputLines} extracted={aspT.OutputNames} dupSkipped={aspT.DuplicatesSkipped}");

        Log.Info("asppath",
            $"Parse[path] TSV={pathT.TsvDetected} header={pathT.HeaderDetected} colIndex={pathT.PathColumnIndex} linesIn={pathT.InputLines} extracted={pathT.OutputPaths} dupSkipped={pathT.DuplicatesSkipped}");

        // Match: asp substring in path (case-insensitive), first unused path wins
        var matches = new Dictionary<int, int>(); // asp_idx -> path_idx
        var usedPath = new HashSet<int>();

        for (int aspIdx = 0; aspIdx < asps.Count; aspIdx++)
        {
            ct.ThrowIfCancellationRequested();

            var asp = (asps[aspIdx] ?? "").Trim();
            if (asp.Length == 0) continue;

            int? bestMatchIdx = null;
            for (int pIdx = 0; pIdx < paths.Count; pIdx++)
            {
                if (usedPath.Contains(pIdx)) continue;

                var p = (paths[pIdx] ?? "").Trim();
                if (p.Length == 0) continue;

                if (p.Contains(asp, StringComparison.OrdinalIgnoreCase))
                {
                    bestMatchIdx = pIdx;
                    break;
                }
            }

            if (bestMatchIdx is int hit)
            {
                matches[aspIdx] = hit;
                usedPath.Add(hit);
            }

            if (aspIdx % 50 == 0)
                onProgress?.Invoke(aspIdx, asps.Count, asp);
        }

        var rows = new List<AspPathRowDto>(asps.Count);
        for (int i = 0; i < asps.Count; i++)
        {
            var asp = asps[i];
            if (matches.TryGetValue(i, out var pIdx))
            {
                rows.Add(new AspPathRowDto(
                    AspName: asp,
                    Path: paths[pIdx],
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

        var aspWithoutMatch = asps.Count - matches.Count;
        var pathsWithoutAsp = paths.Count - matches.Count;

        Log.Info("asppath",
            $"Build done asp={asps.Count} paths={paths.Count} matches={matches.Count} aspNoMatch={aspWithoutMatch} pathsNoAsp={pathsWithoutAsp}");

        return new BuildResult(
            Rows: rows,
            AspCount: asps.Count,
            PathCount: paths.Count,
            MatchCount: matches.Count,
            AspWithoutMatchCount: aspWithoutMatch,
            PathsWithoutAspCount: Math.Max(0, pathsWithoutAsp));
    }

    public CheckResult Check(
        List<AspPathRowDto> rows,
        IReadOnlyList<int> indices,
        Action<int, int>? onProgress,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var checkedCount = 0;
        var missingCount = 0;

        for (int i = 0; i < indices.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var idx = indices[i];
            if (idx < 0 || idx >= rows.Count)
                continue;

            checkedCount++;

            var r = rows[idx];
            var exists = !string.IsNullOrWhiteSpace(r.Path);

            if (!exists)
                missingCount++;

            rows[idx] = r with { Status = exists ? "Bestaat" : "Ontbreekt" };

            if (i % 25 == 0)
                onProgress?.Invoke(i, indices.Count);
        }

        Log.Info("asppath", $"Check done checked={checkedCount} missing={missingCount}");
        return new CheckResult(rows, checkedCount, missingCount);
    }
}
