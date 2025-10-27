using System.Text;
using System.Net.Mime;
using Docoppolis.WebServer.Routing;

namespace Docoppolis.WebServer.Util
{

    internal class ExtensionInfo
    {
        public string ContentType { get; set; }
        public Func<string, string, string, ExtensionInfo, ResponsePacket> Loader { get; set; }
    }

    public static class RequestHelpers
    {
        public static Dictionary<string, string> GetKeyValues(string raw)
        {
            var dict = new Dictionary<string, string>();
            if (string.IsNullOrEmpty(raw))
                return dict;

            var pairs = raw.Split('&');
            foreach (var pair in pairs)
            {
                var kv = pair.Split('=', 2);
                if (kv.Length == 2)
                {
                    string key = Uri.UnescapeDataString(kv[0]);
                    string value = Uri.UnescapeDataString(kv[1]);
                    dict[key] = value;
                }
            }
            return dict;
        }
    }
}