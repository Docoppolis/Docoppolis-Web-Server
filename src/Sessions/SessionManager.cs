using System;
using System.Collections.Concurrent;
using System.Net;

namespace Docoppolis.WebServer.Sessions;

public sealed class SessionManager
{
    private readonly ConcurrentDictionary<string, Session> sessionMap = new();

    public Session GetSession(HttpListenerRequest request, HttpListenerResponse response)
    {
        const string cookieName = "SESSION_ID";
        string sessionId;

        if (request.Cookies[cookieName] != null)
        {
            sessionId = request.Cookies[cookieName]!.Value;
        }
        else
        {
            sessionId = Guid.NewGuid().ToString("N");
            var cookie = new Cookie(cookieName, sessionId)
            {
                Path = "/",
                HttpOnly = true,
                Expires = DateTime.UtcNow.AddHours(1)
            };
            response.AppendCookie(cookie);
        }

        return sessionMap.GetOrAdd(sessionId, _ => new Session());
    }

    public Session GetSession(string id = "default")
    {
        return sessionMap.GetOrAdd(id, _ => new Session());
    }
}
