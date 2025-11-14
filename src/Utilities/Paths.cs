using System;
using System.IO;

namespace Docoppolis.WebServer.Utilities;

public static class Paths
{
    public static string GetWebsitePath()
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Website"));
    }
}
