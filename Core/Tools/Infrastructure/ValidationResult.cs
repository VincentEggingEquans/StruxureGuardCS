namespace StruxureGuard.Core.Tools.Infrastructure;

public sealed class ValidationResult
{
    public List<ValidationIssue> Issues { get; } = new();

    public bool IsValid => Issues.All(i => i.Severity != ValidationSeverity.Error);

    public IEnumerable<ValidationIssue> Errors => Issues.Where(i => i.Severity == ValidationSeverity.Error);
    public IEnumerable<ValidationIssue> Warnings => Issues.Where(i => i.Severity == ValidationSeverity.Warning);

    public ValidationResult AddError(string code, string message)
    {
        Issues.Add(new ValidationIssue(ValidationSeverity.Error, code, message));
        return this;
    }

    public ValidationResult AddWarning(string code, string message)
    {
        Issues.Add(new ValidationIssue(ValidationSeverity.Warning, code, message));
        return this;
    }

    public override string ToString()
    {
        if (Issues.Count == 0) return "OK";
        return string.Join(Environment.NewLine, Issues.Select(i => $"{i.Severity}: {i.Code} - {i.Message}"));
    }
}
