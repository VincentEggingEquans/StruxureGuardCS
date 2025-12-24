using System.Collections.Generic;

namespace StruxureGuard.Core.Tools.AspPathChecker;

public sealed record AspPathRowDto(
    string AspName,
    string Path,
    string Status);

public sealed record AspPathCheckerResultDto(
    List<AspPathRowDto> Rows,
    int AspCount,
    int PathCount,
    int MatchCount,
    int AspWithoutMatchCount,
    int PathsWithoutAspCount,
    int CheckedCount,
    int MissingCount);
