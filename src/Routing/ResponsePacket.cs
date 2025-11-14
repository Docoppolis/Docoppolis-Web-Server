using System;
using System.Net;
using System.Text;
using Docoppolis.WebServer.Errors;

namespace Docoppolis.WebServer.Routing;

public sealed class ResponsePacket
{
    public byte[] Data { get; init; } = Array.Empty<byte>();

    public string ContentType { get; init; } = "application/octet-stream";

    public Encoding Encoding { get; init; } = Encoding.UTF8;

    public int StatusCode { get; init; } = (int)HttpStatusCode.OK;

    public string? Error { get; init; }

    public ServerError? ErrorCode { get; init; }

    public static ResponsePacket Ok(byte[] data, string contentType = "application/octet-stream") =>
        new()
        {
            Data = data,
            ContentType = contentType,
            StatusCode = (int)HttpStatusCode.OK
        };

    public static ResponsePacket FromError(ServerError errorType)
    {
        return errorType switch
        {
            ServerError.OK => Ok(Array.Empty<byte>()),
            ServerError.ExpiredSession => new ResponsePacket
            {
                Data = Encoding.UTF8.GetBytes("Session Expired. Please log in again."),
                ContentType = "text/plain",
                StatusCode = (int)HttpStatusCode.Unauthorized,
                Error = "Session Expired"
            },
            ServerError.NotAuthorized => new ResponsePacket
            {
                Data = Encoding.UTF8.GetBytes("You are not authorized to access this resource."),
                ContentType = "text/plain",
                StatusCode = (int)HttpStatusCode.Forbidden,
                Error = "NotAuthorized"
            },
            ServerError.FileNotFound => new ResponsePacket
            {
                Data = Encoding.UTF8.GetBytes("File not found."),
                ContentType = "text/plain",
                StatusCode = (int)HttpStatusCode.NotFound,
                Error = "FileNotFound"
            },
            ServerError.PageNotFound => new ResponsePacket
            {
                Data = Encoding.UTF8.GetBytes("Page not found."),
                ContentType = "text/plain",
                StatusCode = (int)HttpStatusCode.NotFound,
                Error = "PageNotFound"
            },
            ServerError.ServerError => new ResponsePacket
            {
                Data = Encoding.UTF8.GetBytes("Internal server error."),
                ContentType = "text/plain",
                StatusCode = (int)HttpStatusCode.InternalServerError,
                Error = "ServerError"
            },
            ServerError.UnknownType => new ResponsePacket
            {
                Data = Encoding.UTF8.GetBytes("Unknown content type."),
                ContentType = "text/plain",
                StatusCode = (int)HttpStatusCode.UnsupportedMediaType,
                Error = "UnknownType"
            },
            ServerError.ValidationError => new ResponsePacket
            {
                Data = Encoding.UTF8.GetBytes("Request validation failed."),
                ContentType = "text/plain",
                StatusCode = (int)HttpStatusCode.BadRequest,
                Error = "ValidationError"
            },
            _ => new ResponsePacket
            {
                Data = Encoding.UTF8.GetBytes("Unexpected error."),
                ContentType = "text/plain",
                StatusCode = (int)HttpStatusCode.InternalServerError,
                Error = "Unexpected"
            }
        };
    }
}
