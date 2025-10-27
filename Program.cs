using System;
using System.Net;
using System.IO;
using System.Text;
using System.Collections.Generic;

using Docoppolis.WebServer;
using Docoppolis.WebServer.Routing;
using Docoppolis.WebServer.Util;
using Docoppolis.SessionManagment;

namespace ConsoleWebServer
{
    class Program
    {
        static void Main(string[] args)
        {
            Server.onError = ErrorHandler;

            // Anonymous GET login page:
            Server.AddRoute("GET", "/login", (req, session, qs) =>
            {
                var full = Path.Combine(Server.router.WebsitePath, "Pages", "login.html");
                var bytes = File.ReadAllBytes(full);
                return new ResponsePacket { Data = bytes, ContentType = "text/html" };
            });

            // Anonymous POST login page:
            Server.AddRoute("POST", "/login", (req, session, qs) =>
            {
                // The router already merged POST body and query params into 'qs'
                if (qs.TryGetValue("username", out var username) &&
                    qs.TryGetValue("password", out var password) &&
                    ((username == "user" && password == "user") ||
                    (username == "admin" && password == "admin")))
                {
                    session.Authorized = true;
                    session.Objects["username"] = username;
                    session.Objects["role"] = username == "admin" ? "admin" : "user";

                    string redirectPage = username == "admin" ? "/admin" : "/dashboard";
                    return Server.Redirect(redirectPage);
                }

                return new ResponsePacket
                {
                    Data = Encoding.UTF8.GetBytes("Invalid credentials"),
                    ContentType = "text/plain",
                    StatusCode = 401
                };
            });

            // Authenticated (must be logged in):
            Server.AddRoute("GET", "/dashboard", new AuthenticatedExpirableRouteHandler(
                (req, session, qs) =>
                {
                    if (!session.Authorized)
                        return Server.Redirect("/login");

                    var full = Path.Combine(Server.router.WebsitePath, "Pages", "dashboard.html");
                    var bytes = File.ReadAllBytes(full);
                    return new ResponsePacket { Data = bytes, ContentType = "text/html" };
                }));

            // Authenticated + expirable session:
            Server.AddRoute("GET", "/admin", new AuthenticatedExpirableRouteHandler(
                (req, session, qs) =>
                {
                    if (!session.Authorized)
                        return Server.Redirect("/login");

                    if (!session.Objects.TryGetValue("role", out var role) || role != "admin")
                        return ResponsePacket.FromError(ServerError.NotAuthorized);

                    var full = Path.Combine(Server.router.WebsitePath, "Pages", "admin.html");
                    var bytes = File.ReadAllBytes(full);
                    return new ResponsePacket { Data = bytes, ContentType = "text/html" };
                }));

            // AJAX PUT test
            Server.AddRoute("PUT", "/demo/ajax", new AnonymousRouteHandler(
                (req, session, qs) =>
                {
                    string data = "You said " + qs["number"];
                    byte[] bytes = Encoding.UTF8.GetBytes(data);
                    return Server.Redirect("demo/ajax");
                }
            ));

            // AJAX GET test
            Server.AddRoute("GET", "/demo/ajax", new AnonymousRouteHandler(
                (req, session, qs) =>
                {
                    string data = "You said " + qs["number"];
                    byte[] bytes = Encoding.UTF8.GetBytes(data);
                    return new ResponsePacket { Data = bytes, ContentType = "text/plain" };
                }
            ));

            Console.WriteLine("[ROUTES]");
            foreach (var r in Server.router.routes) // add a ListRoutes() that returns the internal list for debugging
                Console.WriteLine($"  {r.Verb} {r.Path}");

            Server.Start(Paths.GetWebsitePath());
            Console.ReadLine();
        }

        public static string RedirectMe(Dictionary<string, string> parms)
        {
            return "/demo/clicked";
        }

        private static void ErrorHandler(Exception ex)
        {
            Console.WriteLine($"[ERROR] {ex.Message}");
        }
    }
}
