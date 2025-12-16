using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MOCHA.Agents.Application;
using MOCHA.Agents.Domain;
using MOCHA.Agents.Infrastructure.Orchestration;
using MOCHA.Models.Agents;
using MOCHA.Models.Auth;
using MOCHA.Models.Chat;
using MOCHA.Services.Agents;
using MOCHA.Services.Chat;

namespace MOCHA.Tests;

using ServiceChatTurn = MOCHA.Models.Chat.ChatTurn;

/// <summary>
/// AgentOrchestratorChatClient のロール連動挙動を検証するテスト
/// </summary>
[TestClass]
public class AgentOrchestratorChatClientTests
{
    /// <summary>
    /// 未ログインはベーステンプレートのみを設定する
    /// </summary>
    [TestMethod]
    public async Task SendAsync_未ログイン_ベーステンプレートを設定()
    {
        var orchestrator = new CapturingOrchestrator();
        var roleProvider = new FakeRoleProvider(Array.Empty<UserRoleId>());
        var client = CreateClient(orchestrator, roleProvider);
        var turn = new ServiceChatTurn("conv-1", new[]
        {
            new ChatMessage(ChatRole.User, "ping")
        });

        var stream = await client.SendAsync(turn);
        await foreach (var _ in stream)
        {
        }

        Assert.IsNotNull(orchestrator.Context);
        Assert.AreEqual(OrganizerInstructions.Base, orchestrator.Context!.InstructionTemplate);
        Assert.AreEqual(0, orchestrator.Context!.UserRoles.Count);
    }

    /// <summary>
    /// 管理者ロールはベースに管理者追記が付与される
    /// </summary>
    [TestMethod]
    public async Task SendAsync_管理者ロール_管理者追記を付与()
    {
        var orchestrator = new CapturingOrchestrator();
        var roleProvider = new FakeRoleProvider(new[] { UserRoleId.Predefined.Administrator });
        var client = CreateClient(orchestrator, roleProvider);
        var turn = new ServiceChatTurn("conv-2", new[]
        {
            new ChatMessage(ChatRole.User, "ping")
        })
        {
            UserId = "user-admin"
        };

        var stream = await client.SendAsync(turn);
        await foreach (var _ in stream)
        {
        }

        Assert.IsNotNull(orchestrator.Context);
        StringAssert.StartsWith(orchestrator.Context!.InstructionTemplate, OrganizerInstructions.Base);
        StringAssert.Contains(orchestrator.Context!.InstructionTemplate!, OrganizerInstructions.AdministratorAppendix.Trim());
        Assert.IsFalse(orchestrator.Context!.InstructionTemplate!.Contains(OrganizerInstructions.DeveloperAppendix.Trim(), StringComparison.Ordinal));
        CollectionAssert.Contains(orchestrator.Context!.UserRoles.ToList(), UserRoleId.Predefined.Administrator.Value);
    }

    /// <summary>
    /// 一般ロールはベースのみが適用される
    /// </summary>
    [TestMethod]
    public async Task SendAsync_一般ロール_ベースのみを設定()
    {
        var orchestrator = new CapturingOrchestrator();
        var roleProvider = new FakeRoleProvider(new[] { UserRoleId.Predefined.Operator });
        var client = CreateClient(orchestrator, roleProvider);
        var turn = new ServiceChatTurn("conv-3", new[]
        {
            new ChatMessage(ChatRole.User, "ping")
        })
        {
            UserId = "user-op"
        };

        var stream = await client.SendAsync(turn);
        await foreach (var _ in stream)
        {
        }

        Assert.IsNotNull(orchestrator.Context);
        Assert.AreEqual(OrganizerInstructions.Base, orchestrator.Context!.InstructionTemplate);
        CollectionAssert.Contains(orchestrator.Context!.UserRoles.ToList(), UserRoleId.Predefined.Operator.Value);
    }

    /// <summary>
    /// 開発者ロールはベースに開発者追記が付与される
    /// </summary>
    [TestMethod]
    public async Task SendAsync_開発者ロール_開発者追記を付与()
    {
        var orchestrator = new CapturingOrchestrator();
        var roleProvider = new FakeRoleProvider(new[] { UserRoleId.Predefined.Developer });
        var client = CreateClient(orchestrator, roleProvider);
        var turn = new ServiceChatTurn("conv-4", new[]
        {
            new ChatMessage(ChatRole.User, "ping")
        })
        {
            UserId = "user-dev"
        };

        var stream = await client.SendAsync(turn);
        await foreach (var _ in stream)
        {
        }

        Assert.IsNotNull(orchestrator.Context);
        StringAssert.StartsWith(orchestrator.Context!.InstructionTemplate, OrganizerInstructions.Base);
        StringAssert.Contains(orchestrator.Context!.InstructionTemplate!, OrganizerInstructions.DeveloperAppendix.Trim());
        Assert.IsFalse(orchestrator.Context!.InstructionTemplate!.Contains(OrganizerInstructions.AdministratorAppendix.Trim(), StringComparison.Ordinal));
        CollectionAssert.Contains(orchestrator.Context!.UserRoles.ToList(), UserRoleId.Predefined.Developer.Value);
    }

    private AgentOrchestratorChatClient CreateClient(IAgentOrchestrator orchestrator, IUserRoleProvider roleProvider)
    {
        var repository = new InMemoryDelegationRepository();
        var options = Options.Create(new AgentDelegationOptions());
        var service = new AgentDelegationSettingService(repository, options, NullLogger<AgentDelegationSettingService>.Instance);
        return new AgentOrchestratorChatClient(orchestrator, roleProvider, service);
    }

    /// <summary>
    /// 管理者と開発者を両方持つ場合は両方の追記が付与される
    /// </summary>
    [TestMethod]
    public async Task SendAsync_管理者と開発者ロール_両追記を付与()
    {
        var orchestrator = new CapturingOrchestrator();
        var roleProvider = new FakeRoleProvider(new[]
        {
            UserRoleId.Predefined.Administrator,
            UserRoleId.Predefined.Developer
        });
        var client = CreateClient(orchestrator, roleProvider);
        var turn = new ServiceChatTurn("conv-5", new[]
        {
            new ChatMessage(ChatRole.User, "ping")
        })
        {
            UserId = "user-both"
        };

        var stream = await client.SendAsync(turn);
        await foreach (var _ in stream)
        {
        }

        Assert.IsNotNull(orchestrator.Context);
        StringAssert.StartsWith(orchestrator.Context!.InstructionTemplate, OrganizerInstructions.Base);
        StringAssert.Contains(orchestrator.Context!.InstructionTemplate!, OrganizerInstructions.AdministratorAppendix.Trim());
        StringAssert.Contains(orchestrator.Context!.InstructionTemplate!, OrganizerInstructions.DeveloperAppendix.Trim());
        CollectionAssert.Contains(orchestrator.Context!.UserRoles.ToList(), UserRoleId.Predefined.Administrator.Value);
        CollectionAssert.Contains(orchestrator.Context!.UserRoles.ToList(), UserRoleId.Predefined.Developer.Value);
    }

    private sealed class CapturingOrchestrator : IAgentOrchestrator
    {
        public ChatContext? Context { get; private set; }

        public Task<IAsyncEnumerable<AgentEvent>> ReplyAsync(MOCHA.Agents.Domain.ChatTurn userTurn, ChatContext context, CancellationToken cancellationToken = default)
        {
            Context = context;
            return Task.FromResult<IAsyncEnumerable<AgentEvent>>(GetEvents());
        }

        private static async IAsyncEnumerable<AgentEvent> GetEvents()
        {
            await Task.CompletedTask;
            yield break;
        }
    }

    private sealed class FakeRoleProvider : IUserRoleProvider
    {
        private readonly IReadOnlyCollection<UserRoleId> _roles;

        public FakeRoleProvider(IReadOnlyCollection<UserRoleId> roles)
        {
            _roles = roles;
        }

        public Task<IReadOnlyCollection<UserRoleId>> GetRolesAsync(string userId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_roles);
        }

        public Task AssignAsync(string userId, UserRoleId role, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task RemoveAsync(string userId, UserRoleId role, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<bool> IsInRoleAsync(string userId, string role, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class InMemoryDelegationRepository : IAgentDelegationSettingRepository
    {
        private sealed class KeyComparer : IEqualityComparer<(string User, string Agent)>
        {
            public bool Equals((string User, string Agent) x, (string User, string Agent) y)
            {
                return string.Equals(x.User, y.User, StringComparison.OrdinalIgnoreCase)
                       && string.Equals(x.Agent, y.Agent, StringComparison.OrdinalIgnoreCase);
            }

            public int GetHashCode((string User, string Agent) obj)
            {
                var userHash = obj.User?.ToLowerInvariant().GetHashCode() ?? 0;
                var agentHash = obj.Agent?.ToLowerInvariant().GetHashCode() ?? 0;
                return HashCode.Combine(userHash, agentHash);
            }
        }

        private readonly Dictionary<(string User, string Agent), AgentDelegationSetting> _store = new(new KeyComparer());

        public Task<AgentDelegationSetting?> GetAsync(string userId, string agentNumber, CancellationToken cancellationToken = default)
        {
            _store.TryGetValue((userId, agentNumber), out var setting);
            return Task.FromResult(setting);
        }

        public Task<AgentDelegationSetting> UpsertAsync(string userId, AgentDelegationSetting setting, CancellationToken cancellationToken = default)
        {
            _store[(userId, setting.AgentNumber)] = setting;
            return Task.FromResult(setting);
        }
    }
}
