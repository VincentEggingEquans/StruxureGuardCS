namespace StruxureGuard.UI.Tools;

public sealed class UrlDecoderToolForm : PlaceholderToolFormBase
{
    public UrlDecoderToolForm() : base(
        "URL Decoder",
        "Decode/convert URL → EBO (port from Python URLtoEBO).",
        logTag: "url-decoder")
    { }
}
