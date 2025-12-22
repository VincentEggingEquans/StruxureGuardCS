using System.Linq;
using System.Text;

namespace StruxureGuard.Core.Tools.Infrastructure;

public static class ValidationResultExtensions
{
    /// <summary>
    /// Canonical validity check that does NOT rely on ValidationResult.IsValid (property vs method ambiguity).
    /// </summary>
    public static bool IsValidEx(this ValidationResult r)
        => r is not null && !r.Issues.Any(i => i.Severity == ValidationSeverity.Error);

    /// <summary>
    /// Compact single-line format for logs.
    /// </summary>
    public static string ToLogString(this ValidationResult r, int maxItems = 10)
    {
        if (r is null) return "<null>";

        var isValid = r.IsValidEx();

        var sb = new StringBuilder();
        sb.Append("isValid=").Append(isValid)
          .Append(" issues=").Append(r.Issues.Count)
          .Append(" warnings=").Append(r.Warnings.Count());

        var take = Math.Min(maxItems, r.Issues.Count);
        for (int i = 0; i < take; i++)
        {
            var iss = r.Issues[i];
            sb.Append(" | ").Append(iss.Severity).Append(' ')
              .Append(iss.Code).Append(": ")
              .Append(iss.Message);
        }

        if (r.Issues.Count > take)
            sb.Append(" | ...(+").Append(r.Issues.Count - take).Append(')');

        return sb.ToString();
    }

    /// <summary>
    /// Multi-line format (1 issue per line) for detailed logs.
    /// </summary>
    public static IEnumerable<string> ToLogLines(this ValidationResult r)
    {
        if (r is null) yield break;

        foreach (var iss in r.Issues)
            yield return $"{iss.Severity} {iss.Code}: {iss.Message}";
    }
}
