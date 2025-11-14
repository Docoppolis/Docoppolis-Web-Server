using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using Docoppolis.WebServer.Errors;
using Docoppolis.WebServer.Hosting;
using Docoppolis.WebServer.Routing.Handlers;
using Docoppolis.WebServer.Sessions;
using Docoppolis.WebServer.Utilities;

namespace Docoppolis.WebServer.Routing;

public sealed class Router
{
    private static readonly Dictionary<string, ExtensionInfo> ExtensionMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ico"] = new ExtensionInfo(ImageLoader, "image/x-icon", "Images"),
        ["png"] = new ExtensionInfo(ImageLoader, "image/png", "Images"),
        ["jpg"] = new ExtensionInfo(ImageLoader, "image/jpeg", "Images"),
        ["gif"] = new ExtensionInfo(ImageLoader, "image/gif", "Images"),
        ["bmp"] = new ExtensionInfo(ImageLoader, "image/bmp", "Images"),
        ["css"] = new ExtensionInfo(FileLoader, "text/css", "CSS"),
        ["js"] = new ExtensionInfo(FileLoader, "text/javascript", "Scripts"),
        ["html"] = new ExtensionInfo(PageLoader, "text/html", "Pages"),
        [""] = new ExtensionInfo(PageLoader, "text/html", "Pages")
    };

    private readonly Dictionary<string, CacheEntry> cache = new();

    public Router()
    {
        Routes = new List<Route>();
    }

    public string WebsitePath { get; set; } = ".";

    public IList<Route> Routes { get; }

    public void AddRoute(string verb, string path, Func<HttpListenerRequest, Session, Dictionary<string, string>, ResponsePacket> handler)
    {
        Routes.Add(new Route(verb, StringExtensions.NormalizePath(path), new AnonymousRouteHandler(handler)));
    }

    public void AddRoute(string verb, string path, RouteHandler handler)
    {
        Routes.Add(new Route(verb, StringExtensions.NormalizePath(path), handler));
    }

    public ResponsePacket Route(HttpListenerRequest request, Session session)
    {
        Console.WriteLine($"[IN] {request.HttpMethod} {request.Url!.AbsolutePath}{request.Url.Query}");

        string verb = request.HttpMethod;
        string path = request.Url.AbsolutePath;

        var queryParameters = RequestHelpers.GetKeyValues(request.Url.Query.TrimStart('?'));

        if (request.HasEntityBody &&
            (verb.Equals("POST", StringComparison.OrdinalIgnoreCase) ||
             verb.Equals("PUT", StringComparison.OrdinalIgnoreCase)))
        {
            using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
            string body = reader.ReadToEnd();
            var bodyParams = RequestHelpers.GetKeyValues(body);
            foreach (var kv in bodyParams)
            {
                queryParameters[kv.Key] = kv.Value;
            }
        }

        if (!verb.Equals("GET", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("[DEBUG] Session has CSRF: " +
                (session.Objects.ContainsKey(Server.ValidationTokenName) ? session.Objects[Server.ValidationTokenName] : "NONE"));

            if (queryParameters.TryGetValue(Server.ValidationTokenName, out var providedToken))
            {
                Console.WriteLine("[DEBUG] Form sent CSRF: " + providedToken);
            }
            else
            {
                Console.WriteLine("[DEBUG] Form sent no CSRF key");
            }

            bool verified = VerifyCsrf(session, queryParameters);
            if (!verified)
            {
                return ResponsePacket.FromError(ServerError.ValidationError);
            }

            Console.WriteLine("[DEBUG] VerifyCSRF result: " + verified);
        }

        var match = Routes.FirstOrDefault(r =>
            r.Verb.Equals(verb, StringComparison.OrdinalIgnoreCase) &&
            r.Path.Equals(path, StringComparison.OrdinalIgnoreCase));

        if (match != null)
        {
            return match.Handle(request, session, queryParameters);
        }

        return ServeStaticContent(path);
    }

    private ResponsePacket ServeStaticContent(string path)
    {
        string clean = path.TrimStart('/');
        if (string.IsNullOrWhiteSpace(clean))
        {
            clean = "index.html";
        }

        string ext = Path.GetExtension(clean).TrimStart('.');

        if (!ExtensionMap.TryGetValue(ext, out var info))
        {
            return ResponsePacket.FromError(ServerError.UnknownType);
        }

        string relative = clean;
        if (!string.IsNullOrEmpty(info.Subfolder))
        {
            string prefix = info.Subfolder + "/";
            if (!relative.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                relative = Path.Combine(info.Subfolder, relative);
            }
        }

        string full = Path.Combine(WebsitePath, relative);
        Console.WriteLine($"[ROUTE] path='{path}' -> full='{full}'");

        if (!File.Exists(full))
        {
            bool isPage = IsPage(ext, info);
            return ResponsePacket.FromError(isPage ? ServerError.PageNotFound : ServerError.FileNotFound);
        }

        if (cache.TryGetValue(full, out var entry))
        {
            var lastModified = File.GetLastWriteTime(full);
            if (entry.LastModified == lastModified)
            {
                Console.WriteLine($"[CACHE] Hit: {full}");
                return new ResponsePacket
                {
                    Data = entry.Data,
                    ContentType = entry.ContentType,
                    StatusCode = (int)HttpStatusCode.OK
                };
            }
        }
        else
        {
            Console.WriteLine($"[CACHE] Miss: {full}");
        }

        byte[] bytes = info.Loader(full);
        cache[full] = new CacheEntry(bytes, File.GetLastWriteTime(full), info.ContentType);

        return new ResponsePacket
        {
            Data = bytes,
            ContentType = info.ContentType,
            StatusCode = (int)HttpStatusCode.OK
        };
    }

    private static byte[] FileLoader(string fullPath) => File.ReadAllBytes(fullPath);

    private static byte[] ImageLoader(string fullPath) => FileLoader(fullPath);

    private static byte[] PageLoader(string fullPath) => FileLoader(fullPath);

    private static bool IsPage(string ext, ExtensionInfo info)
    {
        return ext.Equals("html", StringComparison.OrdinalIgnoreCase)
               || string.IsNullOrEmpty(ext)
               || info.ContentType.StartsWith("text/html", StringComparison.OrdinalIgnoreCase)
               || string.Equals(info.Subfolder, "Pages", StringComparison.OrdinalIgnoreCase);
    }

    private static bool VerifyCsrf(Session session, Dictionary<string, string> parameters)
    {
        if (!parameters.TryGetValue(Server.ValidationTokenName, out var clientToken))
        {
            Console.WriteLine("Warning - CSRF token is missing from request");
            return false;
        }

        if (!session.Objects.TryGetValue(Server.ValidationTokenName, out var storedToken))
        {
            Console.WriteLine("Warning - CSRF token is missing from session");
            return false;
        }

        if (storedToken == null)
        {
            Console.WriteLine("Warning - Stored CSRF token is null");
            return false;
        }

        bool match = string.Equals(storedToken.ToString(), clientToken, StringComparison.Ordinal);
        if (!match)
        {
            Console.WriteLine($"Warning - CSRF token mismatch. Session: {storedToken}, Client: {clientToken}");
        }

        return match;
    }

    private sealed record CacheEntry(byte[] Data, DateTime LastModified, string ContentType);

    private sealed record ExtensionInfo(Func<string, byte[]> Loader, string ContentType, string Subfolder);
}
