using System;

namespace StruxureGuard.Core.Tools.UrlDecoder;

public static class UrlDecoderEngine
{
    public static string Decode(UrlDecoderOptions opt)
    {
        var url = (opt.Url ?? "").Trim();
        if (url.Length == 0) return "";

        // Uri.TryCreate is strict-ish; accept raw fragments too
        string fragment;

        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            // Uri.Fragment includes leading '#'
            fragment = uri.Fragment;
            if (fragment.StartsWith("#", StringComparison.Ordinal))
                fragment = fragment.Substring(1);
        }
        else
        {
            // If user pasted only "#foo%20bar" or "foo%20bar"
            fragment = url;
            if (fragment.StartsWith("#", StringComparison.Ordinal))
                fragment = fragment.Substring(1);
        }

        // Percent-decode (like Python's unquote)
        var decoded = Uri.UnescapeDataString(fragment);

        if (opt.EnsureLeadingSlash && decoded.Length > 0 && !decoded.StartsWith("/", StringComparison.Ordinal))
            decoded = "/" + decoded;

        return decoded;
    }
}
