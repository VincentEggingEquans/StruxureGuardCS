using StruxureGuard.Core.Logging;
using StruxureGuard.Core.Tools.Infrastructure;

namespace StruxureGuard.Core.Tools.UrlDecoder;

public sealed class UrlDecoderTool : ITool
{
    public const string OutputKeyDecodedPath = "DecodedPath";

    public string ToolKey => ToolKeys.UrlDecoder;

    public ValidationResult Validate(ToolRunContext ctx)
    {
        var r = new ValidationResult();
        try
        {
            var url = ctx.Parameters.GetRequiredString(UrlDecoderParameterKeys.Url).Trim();
            if (url.Length == 0)
                r.AddError("urldecoder.url", "URL is leeg.");
        }
        catch (Exception ex)
        {
            r.AddError("urldecoder.validate", ex.Message);
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

        Log.Info("urldecoder", $"Execute runId='{ctx.RunId}'");

        var decoded = UrlDecoderEngine.Decode(opt);

        progress?.Report(new ToolProgressInfo(
            Done: 1,
            Total: 1,
            CurrentItem: null,
            Message: "Decoded",
            Phase: "Decode",
            Percent: 100));

        // short summary; full output in Outputs payload
        var res = ToolResult.Ok(summary: $"Decoded len={decoded.Length}")
            .WithOutput(OutputKeyDecodedPath, decoded);

        if (decoded.Length == 0)
            res.WithWarning("Geen fragment gevonden of fragment is leeg.");

        return Task.FromResult(res);
    }

    private static UrlDecoderOptions BuildOptions(ToolParameters p)
    {
        return new UrlDecoderOptions
        {
            Url = p.GetRequiredString(UrlDecoderParameterKeys.Url),
            EnsureLeadingSlash = p.GetBool(UrlDecoderParameterKeys.EnsureLeadingSlash, true),
            UseFragmentOnly = p.GetBool(UrlDecoderParameterKeys.UseFragmentOnly, true)
        };
    }
}
