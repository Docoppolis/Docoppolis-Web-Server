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
    private static readonly Router RouterInstance = new();
    private static readonly SessionManager SessionManager = new();

    private static HttpListener? listener;

    private static int maxSimultaneousConnections = 20;
    private static Semaphore connectionSemaphore = new(maxSimultaneousConnections, maxSimultaneousConnections);

    public static Action<Exception>? OnError { get; set; }

    public static string PublicAddress { get; set; } = "localhost:8080";

    public static int SessionExpirationSeconds { get; set; } = 10;

    public static Router Router => RouterInstance;

    public static string ValidationTokenName { get; } = "__csrf__";

    public static void Configure(int maxConnections, int sessionExpirationSeconds)
    {
        maxSimultaneousConnections = maxConnections;
        SessionExpirationSeconds = sessionExpirationSeconds;
        connectionSemaphore = new Semaphore(maxSimultaneousConnections, maxSimultaneousConnections);
    }

    public static void Start(string websitePath)
    {
        var localHostIPs = GetLocalHostIPs();
        var httpListener = InitializeListener(localHostIPs);
        RouterInstance.WebsitePath = websitePath;
        Start(httpListener);
    }

    public static string GetExternalIP()
    {
        string externalIP = new WebClient().DownloadString("http://checkip.dyndns.org/");
        externalIP = new Regex(@"\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}").Matches(externalIP)[0].ToString();
        return externalIP;
    }

    public static void Log(HttpListenerRequest request)
    {
        string path = request.Url?.AbsolutePath ?? "/";
        Console.WriteLine($"{request.RemoteEndPoint} {request.HttpMethod} {path}");
    }

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

    public static void AddRoute(string verb, string path, Func<HttpListenerRequest, Session, Dictionary<string, string>, ResponsePacket> handler)
    {
        RouterInstance.AddRoute(verb, path, handler);
    }

    public static void AddRoute(string verb, string path, RouteHandler handler)
    {
        RouterInstance.AddRoute(verb, path, handler);
    }

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

    private static List<IPAddress> GetLocalHostIPs()
    {
        IPHostEntry host = Dns.GetHostEntry(Dns.GetHostName());
        return host.AddressList
            .Where(address => address.AddressFamily == AddressFamily.InterNetwork)
            .ToList();
    }

    private static HttpListener InitializeListener(IEnumerable<IPAddress> localhostIPs)
    {
        listener = new HttpListener();
        listener.Prefixes.Add("http://localhost:8080/");

        foreach (var ip in localhostIPs)
        {
            Console.WriteLine($"Listening on IP http://{ip}:8080/");
            listener.Prefixes.Add($"http://{ip}:8080/");
        }

        return listener;
    }

    private static void Start(HttpListener httpListener)
    {
        httpListener.Start();
        Task.Run(() => RunServer(httpListener));
    }

    private static void RunServer(HttpListener httpListener)
    {
        while (true)
        {
            connectionSemaphore.WaitOne();
            StartConnectionListener(httpListener);
        }
    }

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
