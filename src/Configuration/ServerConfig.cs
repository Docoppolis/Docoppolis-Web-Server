using System.Text.Json.Serialization;

namespace Docoppolis.WebServer.Configuration;

public sealed class ServerConfig
{
    [JsonPropertyName("port")]
    public int Port { get; set; } = 8080;

    [JsonPropertyName("host")]
    public string Host { get; set; } = "localhost";

    [JsonPropertyName("maxSimultaneousConnections")]
    public int MaxSimultaneousConnections { get; set; } = 20;

    [JsonPropertyName("sessionExpirationSeconds")]
    public int SessionExpirationSeconds { get; set; } = 300;

    [JsonPropertyName("websitePath")]
    public string WebsitePath { get; set; } = "./website";
}
