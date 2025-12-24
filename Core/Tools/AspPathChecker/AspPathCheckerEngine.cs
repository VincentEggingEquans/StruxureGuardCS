using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
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

    public BuildResult BuildList(string aspText, string pathText)
    {
        var aspLines = SplitLines(aspText);
        var pathLines = SplitLines(pathText);

        var matches = new Dictionary<int, int>(); // asp_idx -> path_idx
        var usedPath = new HashSet<int>();

        for (int aspIdx = 0; aspIdx < aspLines.Count; aspIdx++)
        {
            var asp = aspLines[aspIdx];
            var aspLower = asp.ToLowerInvariant();

            int? bestMatchIdx = null;
            for (int i = 0; i < pathLines.Count; i++)
            {
                if (usedPath.Contains(i)) continue;

                var p = pathLines[i];
                if (p.ToLowerInvariant().Contains(aspLower))
                {
                    bestMatchIdx = i;
                    break; // same behavior as Python: first unused match
                }
            }

            if (bestMatchIdx is not null)
            {
                matches[aspIdx] = bestMatchIdx.Value;
                usedPath.Add(bestMatchIdx.Value);
            }
        }

        var rows = new List<AspPathRowDto>(aspLines.Count + (pathLines.Count - matches.Count));

        // ASP rows
        for (int aspIdx = 0; aspIdx < aspLines.Count; aspIdx++)
        {
            var asp = aspLines[aspIdx];

            if (matches.TryGetValue(aspIdx, out var pathIdx))
            {
                rows.Add(new AspPathRowDto(
                    AspName: asp,
                    Path: pathLines[pathIdx],
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

        // Unused paths rows
        for (int i = 0; i < pathLines.Count; i++)
        {
            if (usedPath.Contains(i)) continue;

            rows.Add(new AspPathRowDto(
                AspName: "",
                Path: pathLines[i],
                Status: "Geen ASP gevonden voor path"));
        }

        var matchCount = matches.Count;

        Log.Info("asppath",
            $"BuildList done asp={aspLines.Count} paths={pathLines.Count} matches={matchCount} " +
            $"aspNoMatch={aspLines.Count - matchCount} pathsNoAsp={pathLines.Count - matchCount}");

        return new BuildResult(
            Rows: rows,
            AspCount: aspLines.Count,
            PathCount: pathLines.Count,
            MatchCount: matchCount,
            AspWithoutMatchCount: aspLines.Count - matchCount,
            PathsWithoutAspCount: pathLines.Count - matchCount);
    }

    public CheckResult CheckRows(
        List<AspPathRowDto> rows,
        IReadOnlyList<int> indicesToCheck,
        string outputFile,
        string rootFolder)
    {
        if (indicesToCheck.Count == 0)
        {
            Log.Info("asppath", "CheckRows skipped (no indices)");
            return new CheckResult(rows, CheckedCount: 0, MissingCount: 0);
        }

        var root = (rootFolder ?? "").Trim();
        var hasRoot = !string.IsNullOrWhiteSpace(root);

        int missing = 0;
        int skippedDiskCheckNoRoot = 0;

        // Copy list so it’s “functional” (nice for testing)
        var updated = rows.ToList();

        bool outputFolderEnsured = false;
        void EnsureOutputIfNeeded()
        {
            if (outputFolderEnsured) return;
            if (string.IsNullOrWhiteSpace(outputFile)) return;
            EnsureOutputFolder(outputFile);
            outputFolderEnsured = true;
        }

        for (int k = 0; k < indicesToCheck.Count; k++)
        {
            var idx = indicesToCheck[k];
            if (idx < 0 || idx >= updated.Count)
                continue;

            var r = updated[idx];

            var aspName = (r.AspName ?? "").Trim();
            var rawPath = NormalizePath(r.Path);

            // Case 1: no match/path
            if (string.IsNullOrWhiteSpace(rawPath))
            {
                // Keep semantic status: it’s a matching problem, not a disk problem
                EnsureOutputIfNeeded();
                if (!string.IsNullOrWhiteSpace(outputFile))
                    LogMissing(outputFile, aspName.Length > 0 ? aspName : "<GEEN ASP>", "<GEEN PATH GEVONDEN (GEEN MATCH)>");

                missing++;
                updated[idx] = r with { Status = "Geen path gevonden (geen match)" };
                continue;
            }

            // Case 2: EBO-like path + no root folder => do NOT mark missing on disk
            if (!hasRoot && LooksLikeEboPath(rawPath))
            {
                skippedDiskCheckNoRoot++;
                updated[idx] = r with { Status = "Match gevonden (niet gecontroleerd - geen rootmap)" };
                continue;
            }

            // Case 3: disk check
            var exists = PathExists(rawPath, root);

            if (!exists)
            {
                EnsureOutputIfNeeded();
                if (!string.IsNullOrWhiteSpace(outputFile))
                    LogMissing(outputFile, aspName.Length > 0 ? aspName : "<GEEN ASP>", rawPath);

                missing++;
            }

            updated[idx] = r with { Status = exists ? "Bestaat" : "Ontbreekt (gelogd)" };
        }

        if (!hasRoot && skippedDiskCheckNoRoot > 0)
        {
            Log.Warn("asppath",
                $"Disk-check skipped for EBO paths because RootFolder is empty. skipped={skippedDiskCheckNoRoot}. " +
                $"Tip: kies een rootmap zodat '/WAA-.../_Service' onder die map gecontroleerd wordt.");
        }

        Log.Info("asppath",
            $"CheckRows done checked={indicesToCheck.Count} missing={missing} skippedNoRoot={skippedDiskCheckNoRoot} output='{outputFile}' root='{root}'");

        return new CheckResult(updated, CheckedCount: indicesToCheck.Count, MissingCount: missing);
    }

    private static bool LooksLikeEboPath(string p)
    {
        var s = (p ?? "").Trim();
        if (s.StartsWith("/"))
            return true;

        // Has forward slashes but is not a Windows drive path and not UNC
        if (s.Contains('/') && !s.Contains(':') && !s.StartsWith(@"\\"))
            return true;

        return false;
    }

    private static string ToRelativeFsPath(string p)
    {
        var s = NormalizePath(p);
        s = s.TrimStart('/', '\\');
        s = s.Replace('/', Path.DirectorySeparatorChar);
        return s;
    }

    private static bool PathExists(string rawPath, string rootFolder)
    {
        var p = NormalizePath(rawPath);
        if (string.IsNullOrWhiteSpace(p))
            return false;

        var root = (rootFolder ?? "").Trim();
        var hasRoot = !string.IsNullOrWhiteSpace(root);

        if (LooksLikeEboPath(p))
        {
            var rel = ToRelativeFsPath(p);
            if (hasRoot)
            {
                var candidate = Path.Combine(root, rel);
                return File.Exists(candidate) || Directory.Exists(candidate);
            }

            // Relative to current working dir (rarely useful for WinForms, but safe fallback)
            return File.Exists(rel) || Directory.Exists(rel);
        }

        // Non-EBO paths: if relative and root provided, combine
        if (hasRoot && !Path.IsPathRooted(p))
        {
            var candidate = Path.Combine(root, p);
            return File.Exists(candidate) || Directory.Exists(candidate);
        }

        return File.Exists(p) || Directory.Exists(p);
    }


    private static List<string> SplitLines(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new List<string>();

        return text
            .Replace("\r\n", "\n")
            .Replace("\r", "\n")
            .Split('\n')
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();
    }

    private static string NormalizePath(string? p)
    {
        if (string.IsNullOrWhiteSpace(p)) return "";
        return p.Trim().Trim('"');
    }

    private static bool PathExists(string fullPath)
        => File.Exists(fullPath) || Directory.Exists(fullPath);

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
            Log.Warn("asppath", $"EnsureOutputFolder failed: {ex.GetType().Name}: {ex.Message}\n{ex}");
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
