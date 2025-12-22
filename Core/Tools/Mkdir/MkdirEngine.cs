using System.Diagnostics;
using StruxureGuard.Core.Logging;

namespace StruxureGuard.Tools.Mkdir;

public static class MkdirEngine
{
    /// <summary>
    /// Returns counts of existing directories + existing target files (based on naming mode).
    /// </summary>
    public static (int existingDirCount, int existingTargetFileCount) DetectConflicts(MkdirOptions opt)
    {
        Validate(opt);

        int existingDir = 0;
        int existingFiles = 0;

        for (int i = 0; i < opt.FolderNames.Count; i++)
        {
            var folderName = opt.FolderNames[i];
            var dir = Path.Combine(opt.BasePath, folderName);

            if (Directory.Exists(dir))
                existingDir++;

            if (opt.CopyFileToEachFolder && !string.IsNullOrWhiteSpace(opt.SourceFilePath))
            {
                var target = GetTargetFilePath(dir, folderName, opt.SourceFilePath, opt.CopyNamingMode);
                if (File.Exists(target))
                    existingFiles++;
            }
        }

        return (existingDir, existingFiles);
    }

    public static async Task<MkdirResult> ExecuteAsync(
        MkdirOptions opt,
        IProgress<MkdirProgress>? progress,
        CancellationToken ct)
    {
        Validate(opt);

        return await Task.Run(() =>
        {
            var res = new MkdirResult { TotalRequested = opt.FolderNames.Count };
            var total = opt.FolderNames.Count;

            var swTotal = Stopwatch.StartNew();

            if (opt.VerboseLogging)
            {
                Log.Info("mkdir",
                    $"Execute: base='{opt.BasePath}', total={total}, copy={opt.CopyFileToEachFolder}, naming={opt.CopyNamingMode}, " +
                    $"overwrite={opt.OverwriteExistingCopiedFiles}, skipIfExists={opt.SkipCopyIfTargetExists}");
            }

            for (int i = 0; i < total; i++)
            {
                ct.ThrowIfCancellationRequested();

                var folderName = opt.FolderNames[i];
                var dirPath = Path.Combine(opt.BasePath, folderName);

                var swItem = opt.VerboseLogging ? Stopwatch.StartNew() : null;

                try
                {
                    // Directory
                    bool existed = Directory.Exists(dirPath);
                    Directory.CreateDirectory(dirPath);

                    if (existed) res.DirectoriesAlreadyExisted++;
                    else res.DirectoriesCreated++;

                    if (opt.VerboseLogging)
                        Log.Info("mkdir", $"Item[{i + 1}/{total}] {(existed ? "Exists" : "Created")}: {dirPath}");

                    // Copy (optional)
                    if (opt.CopyFileToEachFolder && !string.IsNullOrWhiteSpace(opt.SourceFilePath))
                    {
                        var target = GetTargetFilePath(dirPath, folderName, opt.SourceFilePath, opt.CopyNamingMode);

                        if (File.Exists(target))
                        {
                            if (opt.SkipCopyIfTargetExists)
                            {
                                res.FilesSkippedBecauseExists++;
                                if (opt.VerboseLogging)
                                    Log.Info("mkdir", $"Item[{i + 1}/{total}] Skip copy (exists): {target}");
                            }
                            else if (opt.OverwriteExistingCopiedFiles)
                            {
                                File.Copy(opt.SourceFilePath, target, overwrite: true);
                                res.FilesCopied++;
                                res.FilesOverwritten++;
                                if (opt.VerboseLogging)
                                    Log.Info("mkdir", $"Item[{i + 1}/{total}] Overwrite copy: {opt.SourceFilePath} -> {target}");
                            }
                            else
                            {
                                // Explicit behavior: allow exception if exists and no policy was selected.
                                File.Copy(opt.SourceFilePath, target, overwrite: false);
                                res.FilesCopied++;
                                if (opt.VerboseLogging)
                                    Log.Info("mkdir", $"Item[{i + 1}/{total}] Copy: {opt.SourceFilePath} -> {target}");
                            }
                        }
                        else
                        {
                            File.Copy(opt.SourceFilePath, target, overwrite: false);
                            res.FilesCopied++;
                            if (opt.VerboseLogging)
                                Log.Info("mkdir", $"Item[{i + 1}/{total}] Copy: {opt.SourceFilePath} -> {target}");
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    throw; // cancellation must win immediately
                }
                catch (Exception ex)
                {
                    res.FailedItems++;
                    Log.Error("mkdir",
                        $"Item[{i + 1}/{total}] FAILED name='{folderName}' dir='{dirPath}': {ex.GetType().Name}: {ex.Message}\n{ex}");
                }
                finally
                {
                    progress?.Report(new MkdirProgress(i + 1, total, folderName));

                    if (opt.VerboseLogging && swItem is not null)
                        Log.Info("mkdir", $"Item[{i + 1}/{total}] END took={swItem.ElapsedMilliseconds}ms");
                }
            }

            swTotal.Stop();
            Log.Info("mkdir",
                $"Done: total={res.TotalRequested}, created={res.DirectoriesCreated}, existed={res.DirectoriesAlreadyExisted}, " +
                $"copied={res.FilesCopied}, overwritten={res.FilesOverwritten}, skipped={res.FilesSkippedBecauseExists}, " +
                $"failed={res.FailedItems}. Took {swTotal.ElapsedMilliseconds}ms");

            return res;
        }, ct);
    }


    private static string GetTargetFilePath(string dirPath, string folderName, string sourceFilePath, CopyNamingMode mode)
    {
        var ext = Path.GetExtension(sourceFilePath);
        return mode switch
        {
            CopyNamingMode.RenameToFolderName => Path.Combine(dirPath, folderName + ext),
            _ => Path.Combine(dirPath, Path.GetFileName(sourceFilePath))
        };
    }

    public static void Validate(MkdirOptions opt)
    {
        if (string.IsNullOrWhiteSpace(opt.BasePath))
            throw new ArgumentException("BasePath is empty.", nameof(opt.BasePath));

        if (!Directory.Exists(opt.BasePath))
            throw new DirectoryNotFoundException($"BasePath does not exist: {opt.BasePath}");

        if (opt.FolderNames.Count == 0)
            throw new ArgumentException("FolderNames is empty.", nameof(opt.FolderNames));

        if (opt.CopyFileToEachFolder)
        {
            if (string.IsNullOrWhiteSpace(opt.SourceFilePath))
                throw new ArgumentException("CopyFileToEachFolder is enabled but SourceFilePath is empty.");

            if (!File.Exists(opt.SourceFilePath))
                throw new FileNotFoundException("Source file does not exist.", opt.SourceFilePath);
        }
    }
}
