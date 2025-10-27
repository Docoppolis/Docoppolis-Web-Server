public sealed class AuthContext
{
    public bool IsAuthenticated { get; set; }
    public string? UserId { get; set; }
    public string[] Roles { get; set; }
}