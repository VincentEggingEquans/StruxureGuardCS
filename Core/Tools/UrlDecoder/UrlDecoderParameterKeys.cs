namespace StruxureGuard.Core.Tools.UrlDecoder;

public static class UrlDecoderParameterKeys
{
    public const string Url = "Url";

    // Optional switches
    public const string EnsureLeadingSlash = "EnsureLeadingSlash"; // default true
    public const string UseFragmentOnly = "UseFragmentOnly";       // default true (matches Python)
}
