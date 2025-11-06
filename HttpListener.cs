using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using System.Text.RegularExpressions;
using System.Security.Cryptography.X509Certificates;
using System.Reflection;
using System.Net.NetworkInformation;

using Docoppolis.WebServer.Routing;
using Docoppolis.WebServer.Util;
using Docoppolis.SessionManagment;


namespace Docoppolis.WebServer
{
    public static class Server
    {

        public static Action<Exception>? onError;
        public static string publicIP = "localhost:8080"; 
        public static int expirationSeconds = 10; // default session expiration time: 5 minutes

        // The HttpListener instance
        private static HttpListener listener;
        public static Router router = new Router();

        // Semaphore to limit the number of simultaneous connections
        private static int maxSimultaneousConnections = 20;
        private static Semaphore sem = new Semaphore(maxSimultaneousConnections, maxSimultaneousConnections);
        private static string externalIP;

        private static readonly SessionManagment.SessionManager sessionManager = new SessionManagment.SessionManager();

        

        public static void Start(string websitePath)
        {
            List<IPAddress> localHostIPs = GetLocalHostIPs();
            //externalIP = GetExternalIP();
            HttpListener listener = InitializeListener(localHostIPs);
            router.WebsitePath = websitePath;
            SessionManagment.SessionManager sessionManager = new SessionManagment.SessionManager();
            router.ResolveSession =req => sessionManager.GetSession();
            Start(listener);
        }

        public static string GetExternalIP()
        {
            string externalIP;
            externalIP = new WebClient().DownloadString("http://checkip.dyndns.org/");
            externalIP = new Regex(@"\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}").Matches(externalIP)[0].ToString();

            return externalIP;
        }

        public static void Log(HttpListenerRequest request)
        {
            string path = request.Url.AbsolutePath;
            Console.WriteLine(request.RemoteEndPoint + " " + request.HttpMethod + " " + path);
        }

        public static string ErrorHandler(ServerError error)
        {
            string ret = null;

            switch (error)
            {
                case ServerError.ExpiredSession:
                    ret = "/ErrorPages/expiredSession.html";
                    break;
                case ServerError.FileNotFound:
                    ret = "/ErrorPages/fileNotFound.html";
                    break;
                case ServerError.NotAuthorized:
                    ret = "/ErrorPages/notAuthorized.html";
                    break;
                case ServerError.PageNotFound:
                    ret = "/ErrorPages/pageNotFound.html";
                    break;
                case ServerError.ServerError:
                    ret = "/ErrorPages/serverError.html";
                    break;
                case ServerError.UnknownType:
                    ret = "/ErrorPages/unknownType.html";
                    break;
            }

            return ret;
        }

        /// <summary>
        /// Convenience for anonymous routes
        /// </summary>
        public static void AddRoute(string verb, string path, Func<HttpListenerRequest, Session, Dictionary<string, string>, ResponsePacket> handler)
        {
            router.AddRoute(verb, path, handler);
        }
        /// <summary>
        /// Full control: pass any wrapper explicitly
        /// </summary>
        public static void AddRoute(string verb, string path, RouteHandler handler)
        {
            router.AddRoute(verb, path, handler);
        }

        public static ResponsePacket Redirect(string relativePath)
        {
            string target = $"http://{publicIP}{relativePath}";

            return new ResponsePacket
            {
                Data = Array.Empty<byte>(),
                ContentType = "text/plain",
                StatusCode = (int)HttpStatusCode.Redirect, // 302
                Error = "Redirecting to " + target
            };
        }

        /// <summary>
        /// Returns list of IP addresses assigned to localhost network devices, such as hardwired ethernet, wireless, etc.
        /// </summary>
        private static List<IPAddress> GetLocalHostIPs()
        {
            IPHostEntry host;
            host = Dns.GetHostEntry(Dns.GetHostName());
            List<IPAddress> ret = host.AddressList.Where(GetLocalHostIPs => GetLocalHostIPs.AddressFamily == AddressFamily.InterNetwork).ToList();

            return ret;
        }

        private static HttpListener InitializeListener(List<IPAddress> localhostIPs)
        {
            listener = new HttpListener();
            listener.Prefixes.Add("http://localhost:8080/");

            // Listen to IP address as well
            localhostIPs.ForEach(ip =>
            {
                Console.WriteLine("Listening on IP " + "http://" + ip.ToString() + ":8080/");
                listener.Prefixes.Add("http://" + ip.ToString() + ":8080/");
            });

            return listener;
        }

        /// <summary>
        /// Begine listening to connections on a separate worker thread
        /// </summary>
        private static void Start(HttpListener listener)
        {
            listener.Start();
            Task.Run(() => RunServer(listener));
        }

        /// <summary>
        /// Start awaiting for connections, up to the "maxSimultaneousConnections" value
        /// </summary>
        private static void RunServer(HttpListener listener)
        {
            while (true)
            {
                sem.WaitOne();
                StartConnectionListener(listener);
            }
        }

        /// <summary>
        /// Awaits connections
        /// </summary>
        /// <param name="listener"></param>
        /// <returns></returns>
        private static async Task StartConnectionListener(HttpListener listener)
        {
            HttpListenerContext ctx;
            try
            {
                ctx = await listener!.GetContextAsync();
                var req = ctx.Request;

                //var sessionManager = new SessionManagment.SessionManager();
                //SessionManagment.Session session = sessionManager.GetSession(ctx.Request, ctx.Response);
                var session = sessionManager.GetSession(ctx.Request, ctx.Response);

                var packet = router.Route(req, session);

                if (packet.StatusCode >= 400)
                {
                    onError?.Invoke(new Exception(packet.Error ?? $"HTTP {packet.StatusCode}"));
                }

                packet = PostProcess(packet, session); // Post-processing step
                Respond(ctx.Response, packet); // write response once, here

                session.UpdateLastConnectionTime();
            }
            catch (Exception ex)
            {
                onError?.Invoke(ex);
                Console.WriteLine("Exception: " + ex.Message);
            }
            finally
            {
                // Close the stream if you open it elsewhere; Respond closes it already.
                sem.Release();
            }
        }

        private static void Respond(HttpListenerResponse response, ResponsePacket resp)
        {
            response.ContentType = resp.ContentType;
            response.ContentLength64 = resp.Data.Length;
            response.ContentEncoding = resp.Encoding;
            response.StatusCode = resp.StatusCode;

            // If this is a redirect, attach the Location header
            if (resp.StatusCode == (int)HttpStatusCode.Redirect && resp.Error != null)
            {
                response.RedirectLocation = resp.Error.Replace("Redirecting to ", "").Trim();
            }

            response.OutputStream.Write(resp.Data, 0, resp.Data.Length);
            response.OutputStream.Close();
        }

        public static string validationTokenName = "__csrf__";
        private static ResponsePacket PostProcess(ResponsePacket packet, Session session)
        {
            try
            {
                if (packet.ContentType.Contains("text/html", StringComparison.OrdinalIgnoreCase))
                {
                    string html = Encoding.UTF8.GetString(packet.Data);

                    string validationPlaceholder = "<!--CSRF_TOKEN-->";

                    if (!session.Objects.ContainsKey(validationTokenName))
                    {
                        session.Objects[validationTokenName] = Guid.NewGuid().ToString("N");
                    }

                    html = html.Replace(validationPlaceholder,
                        "<input name='" + validationTokenName + "' type='hidden' value='" +
                        session.Objects[validationTokenName].ToString() + "' />");

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
                return packet; // Fallback: return unmodified
            }
        }
    }

}
