namespace MOCHA.Models.Auth;

public sealed class FakeAuthOptions
{
    public bool Enabled { get; set; }
    public string UserId { get; set; } = "dev-user";
    public string Name { get; set; } = "Developer";
}
