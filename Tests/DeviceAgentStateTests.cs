using MOCHA.Models.Agents;
using MOCHA.Services.Agents;
using Xunit;

namespace MOCHA.Tests;

public class DeviceAgentStateTests
{
    [Fact]
    public async Task エージェント登録すると選択状態が更新される()
    {
        var repo = new InMemoryDeviceAgentRepository();
        var state = new DeviceAgentState(repo);
        var userId = "user-1";

        await state.LoadAsync(userId);
        Assert.Null(state.SelectedAgentNumber);

        await state.AddOrUpdateAsync(userId, "001", "ライン1");

        Assert.Equal("001", state.SelectedAgentNumber);
        Assert.Contains(state.Agents, a => a.Number == "001" && a.Name == "ライン1");
    }

    [Fact]
    public async Task 別エージェントを選ぶとChangedが発火する()
    {
        var repo = new InMemoryDeviceAgentRepository();
        var state = new DeviceAgentState(repo);
        var userId = "user-1";
        await state.LoadAsync(userId);
        await state.AddOrUpdateAsync(userId, "001", "ライン1");
        await state.AddOrUpdateAsync(userId, "002", "ライン2");
        var changed = false;
        state.Changed += () => changed = true;

        state.Select("001");

        Assert.True(changed);
        Assert.Equal("001", state.SelectedAgentNumber);
    }

    private sealed class InMemoryDeviceAgentRepository : IDeviceAgentRepository
    {
        private readonly List<Entry> _entries = new();
        private readonly object _lock = new();

        public Task<IReadOnlyList<DeviceAgentProfile>> GetAsync(string userId, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                return Task.FromResult<IReadOnlyList<DeviceAgentProfile>>(
                    _entries.Where(e => e.UserId == userId)
                        .Select(e => e.Agent)
                        .ToList());
            }
        }

        public Task<DeviceAgentProfile> UpsertAsync(string userId, string number, string name, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                var existing = _entries.FirstOrDefault(e => e.UserId == userId && e.Agent.Number == number);
                if (existing is null)
                {
                    var agent = new DeviceAgentProfile(number, name, DateTimeOffset.UtcNow);
                    _entries.Add(new Entry(userId, agent));
                    return Task.FromResult(agent);
                }

                existing.Agent.Name = name;
                return Task.FromResult(existing.Agent);
            }
        }

        private sealed class Entry
        {
            public Entry(string userId, DeviceAgentProfile agent)
            {
                UserId = userId;
                Agent = agent;
            }

            public string UserId { get; }
            public DeviceAgentProfile Agent { get; }
        }
    }
}
