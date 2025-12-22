namespace StruxureGuard.Core.Tools.Infrastructure;

public sealed class ToolRunContext
{
    public Guid RunId { get; } = Guid.NewGuid();

    public string ToolKey { get; }
    public IReadOnlyDictionary<string, string> Parameters { get; }

    public ToolRunContext(string toolKey, IReadOnlyDictionary<string, string> parameters)
    {
        ToolKey = toolKey;
        Parameters = parameters;
    }
}
