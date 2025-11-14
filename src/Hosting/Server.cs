// NOTE: Server.cs currently handles multiple concerns (listener setup,
// request loop, session attachment, routing delegation, response writing, post-processing). 
// TODO: Once the architecture stabilizes, consider extracting these responsibilities into dedicated classes to improve clarity.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Docoppolis.WebServer.Errors;
using Docoppolis.WebServer.Routing;
using Docoppolis.WebServer.Routing.Handlers;
using Docoppolis.WebServer.Sessions;

namespace Docoppolis.WebServer.Hosting;

public static class Server
{
    private static readonly Router RouterInstance = new(); // Singleton router instance
    private static readonly SessionManager SessionManager = new(); // Singleton session manager instance

    private static HttpListener? listener; // HTTP listener instance

    private static int maxSimultaneousConnections = 20; // Default max simultaneous connections (can be configured)
    private static Semaphore connectionSemaphore = new(maxSimultaneousConnections, maxSimultaneousConnections); // Semaphore to limit concurrent connections

    public static Action<Exception>? OnError { get; set; } // Error handling delegate

    public static string PublicAddress { get; set; } = "localhost:8080"; // Default public address (can be configured)

    public static int SessionExpirationSeconds { get; set; } = 100; // Default session expiration time in seconds (can be configured)

    public static Router Router => RouterInstance; // Exposes the router instance. Allows access to RouterInstance outside the Server class.

    public static string ValidationTokenName { get; } = "__csrf__"; // CSRF validation token name. Must be placed in HTML forms to perform validation

    /// <summary>
    /// Configures the server with specified max connections and session expiration time.
    /// </summary>
    /// <param name="maxConnections"></param>
    /// <param name="sessionExpirationSeconds"></param>
    public static void Configure(int maxConnections, int sessionExpirationSeconds)
    {
        maxSimultaneousConnections = maxConnections;
        SessionExpirationSeconds = sessionExpirationSeconds;
        connectionSemaphore = new Semaphore(maxSimultaneousConnections, maxSimultaneousConnections);
    }

    /// <summary>
    /// Starts the server with the specified website path.
    /// </summary>
    /// <param name="websitePath"></param>
    public static void Start(string websitePath)
    {
        var localHostIPs = GetLocalHostIPs();
        var httpListener = InitializeListener(localHostIPs);
        RouterInstance.WebsitePath = websitePath;
        Start(httpListener);
    }

    /// <summary>
    /// Gets the external IP address of the server.
    /// TODO: This retrieves the machine's WAN/public IP address.
    /// currently unused because the server only binds to localhost and LAN IPs.
    /// Keep for potential future remote-hosting support.
    /// </summary>
    /// <returns></returns>
    public static string GetExternalIP()
    {
        string externalIP = new WebClient().DownloadString("http://checkip.dyndns.org/"); // Retrieve external IP from web service
        externalIP = new Regex(@"\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}").Matches(externalIP)[0].ToString(); // Extract IP address using regex
        return externalIP;
    }

    /// <summary>
    /// Logs the request details.
    /// </summary>
    /// <param name="request"></param>
    public static void Log(HttpListenerRequest request)
    {
        string path = request.Url?.AbsolutePath ?? "/";
        Console.WriteLine($"{request.RemoteEndPoint} {request.HttpMethod} {path}");
    }

    /// <summary>
    /// Maps server errors to specific error page paths.
    /// </summary>
    /// <param name="error"></param>
    /// <returns></returns>
    public static string? ErrorHandler(ServerError error)
    {
        return error switch
        {
            ServerError.ExpiredSession => "/ErrorPages/expiredSession.html",
            ServerError.FileNotFound => "/ErrorPages/fileNotFound.html",
            ServerError.NotAuthorized => "/ErrorPages/notAuthorized.html",
            ServerError.PageNotFound => "/ErrorPages/pageNotFound.html",
            ServerError.ServerError => "/ErrorPages/serverError.html",
            ServerError.UnknownType => "/ErrorPages/unknownType.html",
            _ => null
        };
    }

    /// <summary>
    /// Adds a route to the server's router with the specified HTTP verb, path, and handler. Defaults to AnonymousRouteHandler.
    /// </summary>
    /// <param name="verb"></param>
    /// <param name="path"></param>
    /// <param name="handler"></param>
    public static void AddRoute(string verb, string path, Func<HttpListenerRequest, Session, Dictionary<string, string>, ResponsePacket> handler)
    {
        RouterInstance.AddRoute(verb, path, handler);
    }

    /// <summary>
    /// Adds a route to the server's router with the specified HTTP verb, path, and custom RouteHandler. Overload for addtional handler types.
    /// </summary>
    /// <param name="verb"></param>
    /// <param name="path"></param>
    /// <param name="handler"></param>
    public static void AddRoute(string verb, string path, RouteHandler handler)
    {
        RouterInstance.AddRoute(verb, path, handler);
    }

    /// <summary>
    /// Generates a redirect ResponsePacket to the specified relative path.
    /// </summary>
    /// <param name="relativePath"></param>
    /// <returns>A ResponsePacket with redirect information.</returns>
    public static ResponsePacket Redirect(string relativePath)
    {
        string target = $"http://{PublicAddress}{relativePath}";

        return new ResponsePacket
        {
            Data = Array.Empty<byte>(),
            ContentType = "text/plain",
            StatusCode = (int)HttpStatusCode.Redirect,
            Error = "Redirecting to " + target
        };
    }

    /// <summary>
    /// Gets the list of local host IP addresses.
    /// </summary>
    /// <returns></returns>
    private static List<IPAddress> GetLocalHostIPs()
    {
        IPHostEntry host = Dns.GetHostEntry(Dns.GetHostName());
        return host.AddressList
            .Where(address => address.AddressFamily == AddressFamily.InterNetwork)
            .ToList();
    }

    /// <summary>
    /// Initializes the HTTP listener with the specified local host IP addresses.
    /// </summary>
    /// <param name="localhostIPs"></param>
    /// <returns>Returns the initialized HttpListener.</returns>
    private static HttpListener InitializeListener(IEnumerable<IPAddress> localhostIPs)
    {
        listener = new HttpListener();
        listener.Prefixes.Add("http://localhost:8080/"); // Default localhost binding

        foreach (var ip in localhostIPs)
        {
            Console.WriteLine($"Listening on IP http://{ip}:8080/");
            listener.Prefixes.Add($"http://{ip}:8080/");
        }

        return listener;
    }

    /// <summary>
    /// Starts the HTTP listener and begins processing incoming requests.
    /// </summary>
    /// <param name="httpListener"></param>
    private static void Start(HttpListener httpListener)
    {
        httpListener.Start();
        Task.Run(() => RunServer(httpListener)); // Run the server loop in a separate task
    }

    /// <summary>
    /// Runs the server loop to handle incoming requests.
    /// </summary>
    /// <param name="httpListener"></param>
    private static async Task RunServer(HttpListener httpListener)
    {
        // Continuously listen for incoming connections
        while (true)
        {
            connectionSemaphore.WaitOne(); // Wait for an available connection slot
            await StartConnectionListener(httpListener); // Start handling the connection
        }
    }

    /// <summary>
    /// Starts handling a single incoming connection.
    /// </summary>
    /// <param name="httpListener"></param>
    private static async Task StartConnectionListener(HttpListener httpListener)
    {
        try
        {
            var context = await httpListener.GetContextAsync().ConfigureAwait(false);
            var request = context.Request;
            var session = SessionManager.GetSession(context.Request, context.Response);
            var packet = RouterInstance.Route(request, session);

            if (packet.StatusCode >= 400)
            {
                OnError?.Invoke(new Exception(packet.Error ?? $"HTTP {packet.StatusCode}"));
            }

            packet = PostProcess(packet, session);
            Respond(context.Response, packet);

            session.UpdateLastConnectionTime();
        }
        catch (Exception ex)
        {
            OnError?.Invoke(ex);
            Console.WriteLine("Exception: " + ex.Message);
        }
        finally
        {
            connectionSemaphore.Release();
        }
    }

    private static void Respond(HttpListenerResponse response, ResponsePacket resp)
    {
        response.ContentType = resp.ContentType;
        response.ContentLength64 = resp.Data.Length;
        response.ContentEncoding = resp.Encoding;
        response.StatusCode = resp.StatusCode;

        if (resp.StatusCode == (int)HttpStatusCode.Redirect && resp.Error != null)
        {
            response.RedirectLocation = resp.Error.Replace("Redirecting to ", string.Empty).Trim();
        }

        response.OutputStream.Write(resp.Data, 0, resp.Data.Length);
        response.OutputStream.Close();
    }

    private static ResponsePacket PostProcess(ResponsePacket packet, Session session)
    {
        try
        {
            if (packet.ContentType.Contains("text/html", StringComparison.OrdinalIgnoreCase))
            {
                string html = Encoding.UTF8.GetString(packet.Data);

                const string validationPlaceholder = "<!--CSRF_TOKEN-->";

                if (!session.Objects.ContainsKey(ValidationTokenName))
                {
                    session.Objects[ValidationTokenName] = Guid.NewGuid().ToString("N");
                }

                html = html.Replace(validationPlaceholder,
                    "<input name='" + ValidationTokenName + "' type='hidden' value='" +
                    session.Objects[ValidationTokenName].ToString() + "' />");

                packet = new ResponsePacket
                {
                    Data = Encoding.UTF8.GetBytes(html),
                    ContentType = packet.ContentType,
                    StatusCode = packet.StatusCode
                };
            }

            return packet;
        }
        catch (Exception ex)
        {
            Console.WriteLine("[ERROR] PostProcess failed: " + ex.Message);
            return packet;
        }
    }
}
