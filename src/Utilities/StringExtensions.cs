using System;

namespace Docoppolis.WebServer.Utilities;

public static class StringExtensions
{
    public static string RightOf(this string s, string delimiter, StringComparison comparison = StringComparison.Ordinal)
    {
        if (s == null)
        {
            return string.Empty;
        }

        int index = s.IndexOf(delimiter, comparison);
        return index == 0 ? s[(index + delimiter.Length)..] : s;
    }

    public static string RightOf(this string s, char delimiter)
    {
        if (s == null)
        {
            return string.Empty;
        }

        int index = s.IndexOf(delimiter);
        return index == 0 ? s[(index + 1)..] : s;
    }

    public static string LeftOf(this string s, string delimiter)
    {
        if (string.IsNullOrEmpty(s))
        {
            return string.Empty;
        }

        int index = s.IndexOf(delimiter, StringComparison.Ordinal);
        return index >= 0 ? s[..index] : s;
    }

    public static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "/";
        }

        if (!path.StartsWith("/", StringComparison.Ordinal))
        {
            path = "/" + path;
        }

        if (path.Length > 1 && path.EndsWith("/", StringComparison.Ordinal))
        {
            path = path.TrimEnd('/');
        }

        return path;
    }
}
