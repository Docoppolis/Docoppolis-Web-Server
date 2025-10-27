namespace Docoppolis.WebServer
{
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

}