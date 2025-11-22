namespace MOCHA.Services.Plc;

public sealed class PlcGatewayOptions
{
    public bool Enabled { get; set; } = true;
    public string BaseAddress { get; set; } = "http://localhost:8000";
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);
}
