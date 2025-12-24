using System;
using System.Collections.Generic;
using System.Linq;

namespace StruxureGuard.Core.Tools.AspPathChecker;

public static class AspPathExtractor
{
    public sealed record Telemetry(
        bool TsvDetected,
        bool HeaderDetected,
        int PathColumnIndex,
        int InputLines,
        int OutputPaths,
        int DuplicatesSkipped);

    public static List<string> Extract(string raw, out Telemetry telemetry)
    {
        raw ??= "";
        var text = raw.Replace("\r\n", "\n").Replace("\r", "\n");

        var lines = text
            .Split('\n')
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();

        if (lines.Count == 0)
        {
            telemetry = new Telemetry(false, false, 0, 0, 0, 0);
            return new List<string>();
        }

        var tsvDetected = lines.Any(l => l.Contains('\t'));

        List<string> paths;

        if (!tsvDetected)
        {
            // Plain list mode (1 path per line)
            paths = DedupPreserveOrder(lines, out var dup);
            telemetry = new Telemetry(false, false, 0, lines.Count, paths.Count, dup);
            return paths;
        }

        // TSV mode
        var headerCols = SplitTsvLine(lines[0]);
        var pathIdx = IndexOfColumn(headerCols, "Path");

        var headerDetected = pathIdx >= 0 && headerCols.Length > 1;
        if (!headerDetected)
            pathIdx = 0; // fallback: first column

        var startRow = headerDetected ? 1 : 0;

        var collected = new List<string>();
        for (int i = startRow; i < lines.Count; i++)
        {
            var cols = SplitTsvLine(lines[i]);
            if (pathIdx < 0 || pathIdx >= cols.Length) continue;

            var v = (cols[pathIdx] ?? "").Trim();
            if (string.IsNullOrWhiteSpace(v)) continue;

            collected.Add(v);
        }

        paths = DedupPreserveOrder(collected, out var duplicates);
        telemetry = new Telemetry(true, headerDetected, pathIdx, lines.Count, paths.Count, duplicates);
        return paths;
    }

    private static string[] SplitTsvLine(string line)
        => (line ?? "").Split('\t');

    private static int IndexOfColumn(string[] headerCols, string colName)
    {
        for (int i = 0; i < headerCols.Length; i++)
        {
            if (string.Equals(headerCols[i].Trim(), colName, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }

    private static List<string> DedupPreserveOrder(List<string> items, out int duplicatesSkipped)
    {
        duplicatesSkipped = 0;

        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>(items.Count);

        foreach (var it in items)
        {
            if (set.Add(it))
                result.Add(it);
            else
                duplicatesSkipped++;
        }

        return result;
    }
}
