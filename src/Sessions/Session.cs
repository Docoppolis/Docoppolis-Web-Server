using System;
using System.Collections.Generic;

namespace Docoppolis.WebServer.Sessions;

public sealed class Session
{
    public DateTime LastConnection { get; private set; }

    public bool Authorized { get; set; }

    public Dictionary<string, string> Objects { get; }

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
