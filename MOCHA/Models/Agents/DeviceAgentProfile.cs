namespace MOCHA.Models.Agents;

public class DeviceAgentProfile
{
    public DeviceAgentProfile(string number, string name, DateTimeOffset createdAt)
    {
        Number = number;
        Name = name;
        CreatedAt = createdAt;
    }

    public string Number { get; set; }
    public string Name { get; set; }
    public DateTimeOffset CreatedAt { get; }
}
