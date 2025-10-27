using System.Diagnostics.SymbolStore;
using System.Security.Cryptography;

namespace Docoppolis.WebServer.Util
{
    public static class StringExtensions
    {
        public static string RightOf(this string s, string delimiter, StringComparison cmp = StringComparison.Ordinal)
        {
            if (s == null) return string.Empty;
            int i = s.IndexOf(delimiter, cmp);
            return (i == 0) ? s.Substring(i + delimiter.Length) : s;
        }

        public static string RightOf(this string s, char delimiter)
        {
            if (s == null) return string.Empty;
            int i = s.IndexOf(delimiter);
            return (i == 0) ? s.Substring(i + 1) : s;
        }

        public static string LeftOf(this string s, string delimiter)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;

            int i = s.IndexOf(delimiter);
            return (i >= 0) ? s.Substring(0, i) : s;
        }

        public static string NormalizePath(string p)
        {
            if (string.IsNullOrWhiteSpace(p)) return "/";
            if (!p.StartsWith("/")) p = "/" + p;
            if (p.Length > 1 && p.EndsWith("/")) p = p.TrimEnd('/');
            return p;
        }
    }
}