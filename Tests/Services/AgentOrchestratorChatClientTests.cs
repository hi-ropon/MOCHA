using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MOCHA.Agents.Application;
using MOCHA.Agents.Domain;
using MOCHA.Agents.Infrastructure.Orchestration;
using MOCHA.Models.Auth;
using MOCHA.Models.Chat;
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
    /// 開発者ロールはテンプレートを上書きしない
    /// </summary>
    [TestMethod]
    public async Task SendAsync_開発者ロール_テンプレートはnull()
    {
        var orchestrator = new CapturingOrchestrator();
        var roleProvider = new FakeRoleProvider(new[] { UserRoleId.Predefined.Developer });
        var client = new AgentOrchestratorChatClient(orchestrator, roleProvider);
        var turn = new ServiceChatTurn("conv-1", new[]
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
        Assert.IsNull(orchestrator.Context!.InstructionTemplate);
        CollectionAssert.Contains(orchestrator.Context!.UserRoles.ToList(), UserRoleId.Predefined.Developer.Value);
    }

    /// <summary>
    /// 管理者ロールもテンプレートを上書きしない
    /// </summary>
    [TestMethod]
    public async Task SendAsync_管理者ロール_テンプレートはnull()
    {
        var orchestrator = new CapturingOrchestrator();
        var roleProvider = new FakeRoleProvider(new[] { UserRoleId.Predefined.Administrator });
        var client = new AgentOrchestratorChatClient(orchestrator, roleProvider);
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
        Assert.IsNull(orchestrator.Context!.InstructionTemplate);
        CollectionAssert.Contains(orchestrator.Context!.UserRoles.ToList(), UserRoleId.Predefined.Administrator.Value);
    }

    /// <summary>
    /// 一般ロールは制限テンプレートを設定する
    /// </summary>
    [TestMethod]
    public async Task SendAsync_一般ロール_制限テンプレートを設定()
    {
        var orchestrator = new CapturingOrchestrator();
        var roleProvider = new FakeRoleProvider(new[] { UserRoleId.Predefined.Operator });
        var client = new AgentOrchestratorChatClient(orchestrator, roleProvider);
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
        Assert.AreEqual(OrganizerInstructions.RestrictedTemplate, orchestrator.Context!.InstructionTemplate);
        CollectionAssert.Contains(orchestrator.Context!.UserRoles.ToList(), UserRoleId.Predefined.Operator.Value);
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
}
