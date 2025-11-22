using MOCHA.Models.Agents;

namespace MOCHA.Services.Agents;

public interface IDeviceAgentRepository
{
    Task<IReadOnlyList<DeviceAgentProfile>> GetAsync(string userId, CancellationToken cancellationToken = default);

    Task<DeviceAgentProfile> UpsertAsync(string userId, string number, string name, CancellationToken cancellationToken = default);
}
