using System;
using System.IO;
using System.Text;
using Docoppolis.WebServer.Configuration;
using Docoppolis.WebServer.Hosting;
using Docoppolis.WebServer.Routing;
using Docoppolis.WebServer.Routing.Handlers;
using Docoppolis.WebServer.Utilities;

namespace Docoppolis.WebServer.Application;

/// <summary>
/// Main program class.
/// </summary>
internal static class Program
{

    /// <summary>
    /// The main entry point for the application.
    /// 
    /// </summary>
    /// <param name="args"></param>
    private static void Main(string[] args)
    {

        // Loads config from config.json, sets server address, max connections, session expiration, etc.
        var config = ConfigLoader.Load();

        Server.PublicAddress = $"{config.Host}:{config.Port}";
        Server.Configure(config.MaxSimultaneousConnections, config.SessionExpirationSeconds);

        Console.WriteLine($"[INFO] Starting server at http://{config.Host}:{config.Port}/");
        Console.WriteLine($"[INFO] Website path: {config.WebsitePath}");

        // Set error handler for server
        Server.OnError = ErrorHandler;

        // Register routes for the application
        RegisterRoutes();

        // List registered routes at startup
        Console.WriteLine("[ROUTES]");
        foreach (var route in Server.Router.Routes)
        {
            Console.WriteLine($"  {route.Verb} {route.Path}");
        }

        // Start the server
        Server.Start(Paths.GetWebsitePath());
        Console.ReadLine();
    }


    /// <summary>
    /// Registers the routes for the web application. AddRoute() creates routes with specified HTTP verbs, paths, and handlers.
    /// Handlers determine if the session is authorized based on the type of RouteHandler used (Anonymous, Authenticated, AuthenticatedExpirable).
    /// Addroute() creates the ResponsePacket returned to the client or determines what to do if the session is not authorized (e.g., redirect to /login).
    /// TODO: Implement a modular routing system so routes can be defined in separate endpoint/controller files instead of manually listed here.
    /// </summary>
    private static void RegisterRoutes()
    {
        
        // Creates a GET route at /login for demonstration purposes with default anonymous handler
        Server.AddRoute("GET", "/login", (req, session, qs) =>
        {
            // Serve the login HTML page
            var full = Path.Combine(Server.Router.WebsitePath, "Pages", "login.html");
            var bytes = File.ReadAllBytes(full);
            return new ResponsePacket { Data = bytes, ContentType = "text/html" };
        });

        // Creates a POST route at /login for demonstration purposes with default anonymous handler
        Server.AddRoute("POST", "/login", (req, session, qs) =>
        {
            // Simple hardcoded authentication for demonstration purposes
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

            // Invalid credentials
            return new ResponsePacket
            {
                Data = Encoding.UTF8.GetBytes("Invalid credentials"),
                ContentType = "text/plain",
                StatusCode = 401
            };
        });

        // Creates a GET route at /dashboard for demonstration purposes with authenticated expirable handler
        Server.AddRoute("GET", "/dashboard", new AuthenticatedExpirableRouteHandler(
            (req, session, qs) =>
            {
                // If not authorized, redirect to login
                if (!session.Authorized)
                {
                    return Server.Redirect("/login");
                }

                // Serve the dashboard HTML page
                var full = Path.Combine(Server.Router.WebsitePath, "Pages", "dashboard.html");
                var bytes = File.ReadAllBytes(full);
                return new ResponsePacket { Data = bytes, ContentType = "text/html" };
            }));

        // Creates a GET route at /admin for demonstration purposes with authenticated expirable handler
        Server.AddRoute("GET", "/admin", new AuthenticatedExpirableRouteHandler(
            (req, session, qs) =>
            {
                // If not authorized, redirect to login
                if (!session.Authorized)
                {
                    return Server.Redirect("/login");
                }

                // If failed to get role or not admin, return not authorized error
                if (!session.Objects.TryGetValue("role", out var role) || role != "admin")
                {
                    return ResponsePacket.FromError(Errors.ServerError.NotAuthorized);
                }

                // Serve the admin HTML page
                var full = Path.Combine(Server.Router.WebsitePath, "Pages", "admin.html");
                var bytes = File.ReadAllBytes(full);
                return new ResponsePacket { Data = bytes, ContentType = "text/html" };
            }));

        // Adds a PUT route at /demo/ajax for demonstration purposes with anonymous handler
        Server.AddRoute("PUT", "/demo/ajax", new AnonymousRouteHandler(
            (req, session, qs) =>
            {
                // Echoes back the "number" query string parameter
                string data = "You said " + qs["number"];
                _ = Encoding.UTF8.GetBytes(data);
                return Server.Redirect("demo/ajax");
            }
        ));

        // Adds a GET route at /demo/ajax for demonstration purposes with anonymous handler
        Server.AddRoute("GET", "/demo/ajax", new AnonymousRouteHandler(
            (req, session, qs) =>
            {
                // Echoes back the "number" query string parameter
                string data = "You said " + qs["number"];
                byte[] bytes = Encoding.UTF8.GetBytes(data);
                return new ResponsePacket { Data = bytes, ContentType = "text/plain" };
            }
        ));
    }

    /// <summary>
    /// Handles errors by logging them to the console.
    /// </summary>
    /// <param name="ex"></param>
    private static void ErrorHandler(Exception ex)
    {
        Console.WriteLine($"[ERROR] {ex.Message}");
    }
}
