namespace StruxureGuard.Core.Tools.UrlDecoder;

public sealed class UrlDecoderOptions
{
    public required string Url { get; init; }

    public bool EnsureLeadingSlash { get; init; } = true;

    /// <summary>
    /// If true: decode only the URL fragment (#...).
    /// If false: decode full path + query + fragment (future option).
    /// </summary>
    public bool UseFragmentOnly { get; init; } = true;
}
