using System.IO;
using System.Text.Json;

namespace Docoppolis.WebServer.Config
{
    public static class ConfigLoader
    {
        public static ServerConfig Load(string filePath = "config.json")
        {
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"[WARN] Config file '{filePath}' not found. Using defaults.");
                return new ServerConfig();
            }

            try
            {
                string json = File.ReadAllText(filePath);
                var config = JsonSerializer.Deserialize<ServerConfig>(json);
                if (config == null)
                {
                    Console.WriteLine("[WARN] Config was empty, using defaults.");
                    return new ServerConfig();
                }

                Console.WriteLine($"[CONFIG] Loaded from {filePath}");
                return config;
            }
            catch(Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to load config: {ex.Message}");
                return new ServerConfig();
            }
        }
    }
}