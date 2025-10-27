using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.IO;
using System.Linq;

using Docoppolis.WebServer;
using Docoppolis.WebServer.Util;
using Docoppolis.SessionManagment;
using System.Text.RegularExpressions;
using System.IO.Pipes;
using System.Reflection.Metadata.Ecma335;


namespace Docoppolis.WebServer.Routing
{

    public class ExtensionInfo
    {
        public Func<string, byte[]> Loader { get; set; } = default!;
        public string ContentType { get; set; } = "application/octet-stream";
        public string Subfolder { get; set; } = "";
    }

    public class Router
    {
        public string WebsitePath { get; set; } = ".";
        public Func<HttpListenerRequest, Session>? ResolveSession { get; set; }

        // simple synchronous loader
        private static byte[] FileLoader(string fullPath) => File.ReadAllBytes(fullPath);
        private static byte[] ImageLoader(string fullPath) => FileLoader(fullPath);
        private static byte[] PageLoader(string fullPath) => FileLoader(fullPath);

        private readonly Dictionary<string, ExtensionInfo> _extMap;

        public List<Route> routes = new List<Route>();

        public Router()
        {
            _extMap = new()
            {
                ["ico"] = new() { Loader = ImageLoader, ContentType = "image/x-icon", Subfolder = "Images" },
                ["png"] = new() { Loader = ImageLoader, ContentType = "image/png", Subfolder = "Images" },
                ["jpg"] = new() { Loader = ImageLoader, ContentType = "image/jpeg", Subfolder = "Images" },
                ["gif"] = new() { Loader = ImageLoader, ContentType = "image/gif", Subfolder = "Images" },
                ["bmp"] = new() { Loader = ImageLoader, ContentType = "image/bmp", Subfolder = "Images" },
                ["css"] = new() { Loader = FileLoader, ContentType = "text/css", Subfolder = "CSS" },
                ["js"] = new() { Loader = FileLoader, ContentType = "text/javascript", Subfolder = "Scripts" },
                ["html"] = new() { Loader = PageLoader, ContentType = "text/html", Subfolder = "Pages" },
                [""] = new() { Loader = PageLoader, ContentType = "text/html", Subfolder = "Pages" }, // default
            };
        }

        /// <summary>
        /// Convenience method to add an anonymous route
        /// </summary>
        public void AddRoute(string verb, string path, Func<HttpListenerRequest, Session, Dictionary<string, string>, ResponsePacket> handler)
        {
            routes.Add(new Route(verb, StringExtensions.NormalizePath(path), new AnonymousRouteHandler(handler)));
        }

        /// <summary>
        /// Full control: pass any wrapper explicitly
        /// </summary>
        public void AddRoute(string verb, string path, RouteHandler handler)
        {
            routes.Add(new Route(verb, StringExtensions.NormalizePath(path), handler));
        }

        public ResponsePacket Route(HttpListenerRequest request, Session session)
        {

            Console.WriteLine($"[IN] {request.HttpMethod} {request.Url.AbsolutePath}{request.Url.Query}");

            // Parse request
            string verb = request.HttpMethod;
            string path = request.Url.AbsolutePath;

            // Parse query string from URL
            string queryRaw = request.Url.Query;
            Dictionary<string, string> qs = RequestHelpers.GetKeyValues(queryRaw.TrimStart('?'));

            // Read request from body for POST or PUT
            if (request.HasEntityBody &&
            (verb.Equals("POST", StringComparison.OrdinalIgnoreCase) ||
            verb.Equals("PUT", StringComparison.OrdinalIgnoreCase)))
            {
                using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
                {
                    string body = reader.ReadToEnd();
                    var bodyParams = RequestHelpers.GetKeyValues(body);
                    foreach (var kv in bodyParams)
                    {
                        qs[kv.Key] = kv.Value;
                    }
                }
            }
            
            if(verb != "GET")
            {

                Console.WriteLine("[DEBUG] Session has CSRF: " +
                    (session.Objects.ContainsKey("__csrf__") ? session.Objects["__csrf__"] : "NONE"));

                if (qs.ContainsKey("__csrf__"))
                    Console.WriteLine("[DEBUG] Form sent CSRF: " + qs["__csrf__"]);
                else
                    Console.WriteLine("[DEBUG] Form sent no CSRF key");
                bool verified = VerifyCSRF(session, qs);

                if (!VerifyCSRF(session, qs))
                {
                    return ResponsePacket.FromError(ServerError.ValidationError);
                }
                Console.WriteLine("[DEBUG] VerifyCSRF result: " + verified);
            }

            // Dynamic routes
            var match = routes.FirstOrDefault(r =>
                r.Verb.Equals(verb, StringComparison.OrdinalIgnoreCase) &&
                r.Path.Equals(path, StringComparison.OrdinalIgnoreCase));

            if (match != null)
            {
                return match.Handle(request, session, qs);
            }

            // Static file serving
            string clean = path.TrimStart('/');
            if (string.IsNullOrWhiteSpace(clean)) clean = "index.html";

            // extract extension
            string ext = Path.GetExtension(clean).TrimStart('.').ToLowerInvariant();

            if (!_extMap.TryGetValue(ext, out var info))
            {
                return ResponsePacket.FromError(ServerError.UnknownType);
            }

            string rel = clean;
            if (!string.IsNullOrEmpty(info.Subfolder))
            {
                string prefix = info.Subfolder + "/";
                if (!rel.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    rel = Path.Combine(info.Subfolder, rel);
            }

            string full = Path.Combine(WebsitePath, rel);
            Console.WriteLine($"[ROUTE] path='{path}' -> full='{full}'");

            if (!File.Exists(full))
            {
                bool isPage = IsPage(ext, info);
                return ResponsePacket.FromError(isPage ? ServerError.PageNotFound : ServerError.FileNotFound);
            }

            byte[] bytes = info.Loader(full);
            return new ResponsePacket
            {
                Data = bytes,
                ContentType = info.ContentType,
                StatusCode = (int)HttpStatusCode.OK
            };
        }

        private static bool IsPage(string ext, ExtensionInfo info)
        {
            return ext.Equals("html", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrEmpty(ext)
                || info.ContentType.StartsWith("text/html", StringComparison.OrdinalIgnoreCase)
                || string.Equals(info.Subfolder, "Pages", StringComparison.OrdinalIgnoreCase);
        }

        private bool VerifyCSRF(Session session, Dictionary<string, string> kvParams)
        {
            const string tokenName = "__csrf__"; // or use Server.validationTokenName
            if (!kvParams.TryGetValue(tokenName, out var clientToken))
            {
                Console.WriteLine("Warning - CSRF token is missing from request");
                return false; 
            }

            if (!session.Objects.TryGetValue(tokenName, out var storedToken))
            {
                Console.WriteLine("Warning - CSRF token is missing from session");
                return false; 
            }

            if (storedToken == null)
            {
                Console.WriteLine("Warning - Stored CSRF token is null");
                return false;
            }

            bool match = storedToken.ToString() == clientToken;
            if (!match)
            {
                Console.WriteLine($"Warning - CSRF token mismatch. Session: {storedToken}, Client: {clientToken}");
            }

            return match;
        }
    }

    public class Route
    {
        public string Verb { get; }
        public string Path { get; }
        //public Func<HttpListenerRequest, ResponsePacket> Handler { get; }

        public RouteHandler Handler { get; set; }

        public Route(string verb, string path, RouteHandler handler)
        {
            Verb = verb;
            Path = path;
            Handler = handler;
        }

        public ResponsePacket Handle(HttpListenerRequest req, Session session, Dictionary<string, string> parms)
        {
            return Handler.Handle(req, session, parms);
        }
    }

    public abstract class RouteHandler
    {
        protected readonly Func<HttpListenerRequest, Session, Dictionary<string, string>, ResponsePacket> handler;

        public RouteHandler(Func<HttpListenerRequest, Session, Dictionary<string, string>, ResponsePacket> handler)
        {
            this.handler = handler;
        }

        public abstract ResponsePacket Handle(HttpListenerRequest req, Session session, Dictionary<string, string> parms);
    }


    /// <summary>
    /// An anonymous route handler that does not require authentication
    /// </summary>
    public class AnonymousRouteHandler : RouteHandler
    {
        public AnonymousRouteHandler(Func<HttpListenerRequest, Session, Dictionary<string, string>, ResponsePacket> handler)
            : base(handler)
        {
        }

        public override ResponsePacket Handle(HttpListenerRequest req, Session session, Dictionary<string, string> parms)
        {
            return handler.Invoke(req, session, parms);
        }
    }

    public class AuthenticatedRouteHandler : RouteHandler
    {
        public AuthenticatedRouteHandler(Func<HttpListenerRequest, Session, Dictionary<string, string>, ResponsePacket> handler)
            : base(handler)
        {
        }

        public override ResponsePacket Handle(HttpListenerRequest req, Session session, Dictionary<string, string> parms)
        {

            if (session.Authorized)
            {
                return handler.Invoke(req, session, parms);
            }
            else
            {
                return Server.Redirect("/login");
            }
        }
    }

    public class AuthenticatedExpirableRouteHandler : AuthenticatedRouteHandler
    {
        public AuthenticatedExpirableRouteHandler(Func<HttpListenerRequest, Session, Dictionary<string, string>, ResponsePacket> handler)
            : base(handler)
        {
        }

        public override ResponsePacket Handle(HttpListenerRequest req, Session session, Dictionary<string, string> parms)
        {
            ResponsePacket ret;

            if (session.IsExpired(Server.expirationSeconds))
            {
                session.Authorized = false;
                ret = ResponsePacket.FromError(ServerError.ExpiredSession);
            }
            else if (session.Authorized)
            {
                return handler.Invoke(req, session, parms);
            }
            else
            {
                return Server.Redirect("/login");
            }

            return ret;
        }
    }
}