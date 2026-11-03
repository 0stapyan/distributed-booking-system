namespace Shared.Auth;

public class JwtOptions
{
    public string Authority { get; set; } = "";

    public string MetadataAddress { get; set; } = "";

    public string Audience { get; set; } = "account";
}