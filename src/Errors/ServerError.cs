namespace Docoppolis.WebServer.Errors;

public enum ServerError
{
    OK,
    ExpiredSession,
    NotAuthorized,
    FileNotFound,
    PageNotFound,
    ServerError,
    UnknownType,
    ValidationError,
}
