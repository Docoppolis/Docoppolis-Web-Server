using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Docoppolis.WebServer.Util
{

    public static class Paths
    {

        public static string GetWebsitePath()
        {
            return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Website"));
        }

    }

}