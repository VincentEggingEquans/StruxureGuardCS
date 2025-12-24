using System;
using System.Collections.Generic;
using System.Linq;

namespace StruxureGuard.Core.Tools.AspPathChecker;

public static class PathValueExtractor
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
        telemetry = new Telemetry(
            TsvDetected: false,
            HeaderDetected: false,
            PathColumnIndex: -1,
            InputLines: 0,
            OutputPaths: 0,
            DuplicatesSkipped: 0);

        if (string.IsNullOrWhiteSpace(raw))
            return new List<string>();

        var lines = raw
            .Replace("\r\n", "\n")
            .Replace("\r", "\n")
            .Split('\n')
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();

        if (lines.Count == 0)
            return new List<string>();

        var inputLines = lines.Count;
        var tsvDetected = lines.Any(l => l.Contains('\t'));

        // Default: treat as simple newline list
        var extracted = new List<string>();

        bool headerDetected = false;
        int pathCol = -1;

        if (tsvDetected)
        {
            var header = lines[0].Split('\t');
            pathCol = Array.FindIndex(header, h => string.Equals(h.Trim(), "Path", StringComparison.OrdinalIgnoreCase));

            if (pathCol >= 0)
            {
                headerDetected = true;

                for (int i = 1; i < lines.Count; i++)
                {
                    var parts = lines[i].Split('\t');
                    if (pathCol < parts.Length)
                    {
                        var v = parts[pathCol].Trim();
                        if (!string.IsNullOrWhiteSpace(v))
                            extracted.Add(v);
                    }
                }
            }
        }

        // Fallback: newline-list (or TSV without Path column)
        if (extracted.Count == 0)
        {
            extracted = lines;
        }

        int duplicatesSkipped = 0;
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>(extracted.Count);

        foreach (var it in extracted.Select(s => s.Trim()).Where(s => !string.IsNullOrWhiteSpace(s)))
        {
            if (set.Add(it))
                result.Add(it);
            else
                duplicatesSkipped++;
        }

        telemetry = new Telemetry(
            TsvDetected: tsvDetected,
            HeaderDetected: headerDetected,
            PathColumnIndex: pathCol,
            InputLines: inputLines,
            OutputPaths: result.Count,
            DuplicatesSkipped: duplicatesSkipped);

        return result;
    }
}
