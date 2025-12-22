namespace StruxureGuard.Core.Tools.Infrastructure;

public enum ValidationSeverity
{
    Error,
    Warning
}

public sealed record ValidationIssue(
    ValidationSeverity Severity,
    string Code,
    string Message);
