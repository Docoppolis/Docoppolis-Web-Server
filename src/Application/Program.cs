using System;
using System.IO;
using System.Text;
using Docoppolis.WebServer.Configuration;
using Docoppolis.WebServer.Hosting;
using Docoppolis.WebServer.Routing;
using Docoppolis.WebServer.Routing.Handlers;
using Docoppolis.WebServer.Utilities;

namespace Docoppolis.WebServer.Application;

internal static class Program
{
    private static void Main(string[] args)
    {
        var config = ConfigLoader.Load();

        Server.PublicAddress = $"{config.Host}:{config.Port}";
        Server.Configure(config.MaxSimultaneousConnections, config.SessionExpirationSeconds);

        Console.WriteLine($"[INFO] Starting server at http://{config.Host}:{config.Port}/");
        Console.WriteLine($"[INFO] Website path: {config.WebsitePath}");

        Server.OnError = ErrorHandler;

        RegisterRoutes();

        Console.WriteLine("[ROUTES]");
        foreach (var route in Server.Router.Routes)
        {
            Console.WriteLine($"  {route.Verb} {route.Path}");
        }

        Server.Start(Paths.GetWebsitePath());
        Console.ReadLine();
    }

    private static void RegisterRoutes()
    {
        Server.AddRoute("GET", "/login", (req, session, qs) =>
        {
            var full = Path.Combine(Server.Router.WebsitePath, "Pages", "login.html");
            var bytes = File.ReadAllBytes(full);
            return new ResponsePacket { Data = bytes, ContentType = "text/html" };
        });

        Server.AddRoute("POST", "/login", (req, session, qs) =>
        {
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

        Server.AddRoute("GET", "/dashboard", new AuthenticatedExpirableRouteHandler(
            (req, session, qs) =>
            {
                if (!session.Authorized)
                {
                    return Server.Redirect("/login");
                }

                var full = Path.Combine(Server.Router.WebsitePath, "Pages", "dashboard.html");
                var bytes = File.ReadAllBytes(full);
                return new ResponsePacket { Data = bytes, ContentType = "text/html" };
            }));

        Server.AddRoute("GET", "/admin", new AuthenticatedExpirableRouteHandler(
            (req, session, qs) =>
            {
                if (!session.Authorized)
                {
                    return Server.Redirect("/login");
                }

                if (!session.Objects.TryGetValue("role", out var role) || role != "admin")
                {
                    return ResponsePacket.FromError(Errors.ServerError.NotAuthorized);
                }

                var full = Path.Combine(Server.Router.WebsitePath, "Pages", "admin.html");
                var bytes = File.ReadAllBytes(full);
                return new ResponsePacket { Data = bytes, ContentType = "text/html" };
            }));

        Server.AddRoute("PUT", "/demo/ajax", new AnonymousRouteHandler(
            (req, session, qs) =>
            {
                string data = "You said " + qs["number"];
                _ = Encoding.UTF8.GetBytes(data);
                return Server.Redirect("demo/ajax");
            }
        ));

        Server.AddRoute("GET", "/demo/ajax", new AnonymousRouteHandler(
            (req, session, qs) =>
            {
                string data = "You said " + qs["number"];
                byte[] bytes = Encoding.UTF8.GetBytes(data);
                return new ResponsePacket { Data = bytes, ContentType = "text/plain" };
            }
        ));
    }

    private static void ErrorHandler(Exception ex)
    {
        Console.WriteLine($"[ERROR] {ex.Message}");
    }
}
