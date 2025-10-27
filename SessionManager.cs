using System;
using System.Collections.Generic;
using System.Net;
using Docoppolis.WebServer;


namespace Docoppolis.SessionManagment
{
    public class Session
    {
        public DateTime LastConnection { get; set; }
        public bool Authorized { get; set; }

        public Dictionary<string, string> Objects { get; set; }

        public Session()
        {
            Objects = new Dictionary<string, string>();
            UpdateLastConnectionTime();
        }

        public void UpdateLastConnectionTime()
        {
            LastConnection = DateTime.Now;
        }

        public bool IsExpired(int expirationSeconds)
        {
            return (DateTime.Now - LastConnection).TotalSeconds > expirationSeconds;
        }
    }

    public class SessionManager
    {
        private readonly Dictionary<string, Session> sessionMap = new Dictionary<string, Session>();

        public SessionManager(){ }

        public Session GetSession(HttpListenerRequest request, HttpListenerResponse response)
        {
            const string cookieName = "SESSION_ID";
            string sessionId;

            // Step 1: Try to read session ID from cookie
            if (request.Cookies[cookieName] != null)
            {
                sessionId = request.Cookies[cookieName].Value;
            }
            else
            {
                // Step 2: Create a new session ID and set cookie
                sessionId = Guid.NewGuid().ToString("N");
                var cookie = new Cookie(cookieName, sessionId)
                {
                    Path = "/",                     // available to all paths
                    HttpOnly = true,                // JS can't access it
                    Expires = DateTime.UtcNow.AddHours(1) // optional
                };
                response.AppendCookie(cookie);
            }

            // Step 3: Retrieve or create session
            if (!sessionMap.ContainsKey(sessionId))
            {
                sessionMap[sessionId] = new Session();
            }

            // Step 4: Return the session
            return sessionMap[sessionId];
        }

        public Session GetSession(string id = "default")
        {
            if (!sessionMap.ContainsKey(id))
            {
                sessionMap[id] = new Session();
            }
            return sessionMap[id];
        }
    }


}