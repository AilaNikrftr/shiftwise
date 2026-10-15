namespace ShiftWise.Api.Auth;

public class JwtOptions
{
    public string Issuer { get; set; } = "shiftwise";
    public string Audience { get; set; } = "shiftwise-client";

    // HS256 needs at least 32 bytes. Program.cs refuses to start with anything shorter.
    public string SigningKey { get; set; } = "";
    public int ExpiryMinutes { get; set; } = 480;
}
