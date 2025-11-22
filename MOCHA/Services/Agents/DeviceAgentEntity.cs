namespace MOCHA.Services.Agents;

public class DeviceAgentEntity
{
    public int Id { get; set; }
    public string UserObjectId { get; set; } = default!;
    public string Number { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}
