using System.Collections.Generic;
using System.Net;
using Docoppolis.WebServer.Routing.Handlers;
using Docoppolis.WebServer.Sessions;

namespace Docoppolis.WebServer.Routing;

public sealed class Route
{
    public Route(string verb, string path, RouteHandler handler)
    {
        Verb = verb;
        Path = path;
        Handler = handler;
    }

    public string Verb { get; }

    public string Path { get; }

    public RouteHandler Handler { get; }

    public ResponsePacket Handle(HttpListenerRequest request, Session session, Dictionary<string, string> parameters)
    {
        return Handler.Handle(request, session, parameters);
    }
}
