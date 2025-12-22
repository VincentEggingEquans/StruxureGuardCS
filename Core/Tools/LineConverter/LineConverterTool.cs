using StruxureGuard.Core.Logging;
using StruxureGuard.Core.Tools.Infrastructure;

namespace StruxureGuard.Core.Tools.LineConverter;

public sealed class LineConverterTool : ITool
{
    public const string OutputKeyResultText = "ResultText";

    public string ToolKey => ToolKeys.LineConverter;

    public ValidationResult Validate(ToolRunContext ctx)
    {
        var r = new ValidationResult();

        try
        {
            var input = ctx.Parameters.GetRequiredString(LineConverterParameterKeys.InputText);
            var items = LineConverterEngine.ParseLines(input);

            if (items.Count == 0)
                r.AddError("lineconv.input", "Geen regels gevonden. Plak minimaal 1 regel.");
        }
        catch (Exception ex)
        {
            r.AddError("lineconv.validate", ex.Message);
        }

        return r;
    }

    public Task<ToolResult> RunAsync(
        ToolRunContext ctx,
        IProgress<ToolProgressInfo>? progress,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var opt = BuildOptions(ctx.Parameters);

        Log.Info("lineconv", $"Execute runId='{ctx.RunId}'");

        var (items, resultText) = LineConverterEngine.Execute(opt);

        progress?.Report(new ToolProgressInfo(
            Done: 1,
            Total: 1,
            CurrentItem: null,
            Message: $"Converted {items.Count} lines",
            Phase: "Convert",
            Percent: 100));

        // Summary is short (won't spam logs). Full output goes to Outputs.
        var res = ToolResult.Ok(summary: $"Converted {items.Count} lines")
            .WithOutput(OutputKeyResultText, resultText);

        if (items.Count >= 200)
            res.WithWarning($"Let op: {items.Count} regels omgezet.");

        return Task.FromResult(res);
    }

    private static LineConverterOptions BuildOptions(ToolParameters p)
    {
        var inputText = p.GetRequiredString(LineConverterParameterKeys.InputText);

        var conjunction = p.GetString(LineConverterParameterKeys.Conjunction) ?? "en";
        var dedup = p.GetBool(LineConverterParameterKeys.Deduplicate, false);
        var sort = p.GetBool(LineConverterParameterKeys.Sort, false);
        var oxford = p.GetBool(LineConverterParameterKeys.OxfordComma, false);

        return new LineConverterOptions
        {
            InputText = inputText,
            Conjunction = conjunction,
            Deduplicate = dedup,
            Sort = sort,
            OxfordComma = oxford
        };
    }
}
