using System;
using System.Collections.Generic;
using System.Net;
using Docoppolis.WebServer.Hosting;
using Docoppolis.WebServer.Sessions;

namespace Docoppolis.WebServer.Routing.Handlers;

public abstract class RouteHandler
{
    protected RouteHandler(Func<HttpListenerRequest, Session, Dictionary<string, string>, ResponsePacket> handler)
    {
        Handler = handler;
    }

    protected Func<HttpListenerRequest, Session, Dictionary<string, string>, ResponsePacket> Handler { get; }

    public abstract ResponsePacket Handle(HttpListenerRequest request, Session session, Dictionary<string, string> parameters);
}

public sealed class AnonymousRouteHandler : RouteHandler
{
    public AnonymousRouteHandler(Func<HttpListenerRequest, Session, Dictionary<string, string>, ResponsePacket> handler)
        : base(handler)
    {
    }

    public override ResponsePacket Handle(HttpListenerRequest request, Session session, Dictionary<string, string> parameters)
    {
        return Handler.Invoke(request, session, parameters);
    }
}

public class AuthenticatedRouteHandler : RouteHandler
{
    public AuthenticatedRouteHandler(Func<HttpListenerRequest, Session, Dictionary<string, string>, ResponsePacket> handler)
        : base(handler)
    {
    }

    public override ResponsePacket Handle(HttpListenerRequest request, Session session, Dictionary<string, string> parameters)
    {
        if (session.Authorized)
        {
            return Handler.Invoke(request, session, parameters);
        }

        return Server.Redirect("/login");
    }
}

public sealed class AuthenticatedExpirableRouteHandler : AuthenticatedRouteHandler
{
    public AuthenticatedExpirableRouteHandler(Func<HttpListenerRequest, Session, Dictionary<string, string>, ResponsePacket> handler)
        : base(handler)
    {
    }

    public override ResponsePacket Handle(HttpListenerRequest request, Session session, Dictionary<string, string> parameters)
    {
        if (session.IsExpired(Server.SessionExpirationSeconds))
        {
            session.Authorized = false;
            return ResponsePacket.FromError(Errors.ServerError.ExpiredSession);
        }

        if (session.Authorized)
        {
            return base.Handle(request, session, parameters);
        }

        return Server.Redirect("/login");
    }
}
