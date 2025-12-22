namespace StruxureGuard.Core.Tools.Infrastructure;

public sealed class ToolRunContext
{
    public Guid RunId { get; } = Guid.NewGuid();

    public string ToolKey { get; }

    public ToolParameters Parameters { get; }

    public ToolRunContext(string toolKey, ToolParameters parameters)
    {
        ToolKey = toolKey;
        Parameters = parameters;
    }

    // Convenience overload: allow existing call-sites that pass Dictionary/IReadOnlyDictionary
    public ToolRunContext(string toolKey, IReadOnlyDictionary<string, string> parameters)
        : this(toolKey, ToolParameters.From(parameters))
    {
    }
}
