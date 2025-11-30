using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MOCHA.Agents.Infrastructure.Options;
using MOCHA.Components.Pages;
using MOCHA.Models.Agents;
using MOCHA.Models.Chat;
using MOCHA.Services.Agents;
using MOCHA.Services.Chat;

namespace MOCHA.Tests;

/// <summary>
/// Home コンポーネントの履歴更新挙動を検証するテスト
/// </summary>
[TestClass]
public class HomeHistoryUpdateTests
{
    /// <summary>
    /// 送信中に他の履歴へ切り替えても元の会話だけが更新されること
    /// </summary>
    [TestMethod]
    public async Task SendAsync_他の履歴に切替_元の会話だけ更新する()
    {
        var userId = "user-switch";
        var agentNumber = "AG-01";
        var initialConversationId = "conv-new";
        var otherConversationId = "conv-existing";
        var seededUpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-30);

        var repository = new RecordingChatRepository();
        repository.SeedSummary(otherConversationId, "既存の会話", userId, agentNumber, seededUpdatedAt);
        var history = new ConversationHistoryState(repository);
        await history.LoadAsync(userId, agentNumber);

        var agentRepository = new FakeDeviceAgentRepository(agentNumber);
        var accessService = new PassthroughAccessService(agentRepository);
        var agentState = new DeviceAgentState(agentRepository, accessService);
        await agentState.LoadAsync(userId);

        var home = new Home();
        InitializeComponent(home);

        var orchestrator = new CallbackOrchestrator(
            () =>
            {
                SetConversationId(home, otherConversationId);
                return Task.CompletedTask;
            },
            initialConversationId);

        SetProperty(home, "HistoryState", history);
        SetProperty(home, "ChatRepository", repository);
        SetProperty(home, "AgentState", agentState);
        SetProperty(home, "Orchestrator", orchestrator);
        SetProperty(home, "LlmOptions", new StaticOptionsMonitor<LlmOptions>(new LlmOptions()));
        SetProperty(home, "AuthenticationStateTask", Task.FromResult(new AuthenticationState(new ClaimsPrincipal(
            new ClaimsIdentity(new[] { new Claim("oid", userId), new Claim("name", "Tester") }, "test")))));

        SetPrivateField(home, "_user", new UserContext(userId, "Tester"));
        SetPrivateField(home, "ConversationId", initialConversationId);
        SetPrivateField(home, "Input", "action please");

        await InvokeSendAsync(home);

        var summaries = repository.Summaries.Where(s => s.UserId == userId).ToList();
        var other = summaries.Single(s => s.Id == otherConversationId);
        Assert.AreEqual(seededUpdatedAt, other.UpdatedAt, "他の履歴の更新日時を変えないこと");

        var created = summaries.Single(s => s.Id == initialConversationId);
        Assert.IsTrue(created.UpdatedAt > seededUpdatedAt, "送信元の会話が更新されていること");
    }

    private static async Task InvokeSendAsync(Home home)
    {
        var method = typeof(Home).GetMethod("SendAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method, "SendAsync が見つかること");
        if (method.Invoke(home, Array.Empty<object>()) is Task task)
        {
            await task;
        }
        else
        {
            Assert.Fail("SendAsync が Task を返すこと");
        }
    }

    private static void SetPrivateField(Home home, string fieldName, object? value)
    {
        var field = typeof(Home).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, $"{fieldName} が見つかること");
        field.SetValue(home, value);
    }

    private static void InitializeComponent(Home home)
    {
        var renderer = new TestRenderer();
        renderer.AttachComponent(home);
    }

    private static void SetProperty(Home home, string propertyName, object? value)
    {
        var property = typeof(Home).GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.IsNotNull(property, $"{propertyName} が見つかること");
        property.SetValue(home, value);
    }

    private static void SetConversationId(Home home, string value) => SetPrivateField(home, "ConversationId", value);

    private sealed class CallbackOrchestrator : IChatOrchestrator
    {
        private readonly Func<Task> _beforeActionRequest;
        private readonly string _conversationId;

        public CallbackOrchestrator(Func<Task> beforeActionRequest, string conversationId)
        {
            _beforeActionRequest = beforeActionRequest;
            _conversationId = conversationId;
        }

        public async IAsyncEnumerable<ChatStreamEvent> HandleUserMessageAsync(
            UserContext user,
            string? conversationId,
            string text,
            string? agentNumber,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            yield return ChatStreamEvent.FromMessage(new ChatMessage(ChatRole.Assistant, "ack"));

            await _beforeActionRequest();

            yield return new ChatStreamEvent(
                ChatStreamEventType.ActionRequest,
                ActionRequest: new AgentActionRequest("fake_action", _conversationId, new Dictionary<string, object?>()));

            yield return ChatStreamEvent.Completed(_conversationId);
        }
    }

    private sealed class RecordingChatRepository : IChatRepository
    {
        private readonly List<ConversationSummary> _summaries = new();

        public IReadOnlyList<ConversationSummary> Summaries => _summaries;

        public void SeedSummary(string id, string title, string userId, string? agentNumber, DateTimeOffset updatedAt)
        {
            _summaries.Add(new ConversationSummary(id, title, updatedAt, agentNumber, userId));
        }

        public Task AddMessageAsync(string userObjectId, string conversationId, ChatMessage message, string? agentNumber, CancellationToken cancellationToken = default)
        {
            var preview = message.Content.Length > 30 ? message.Content[..30] + "…" : message.Content;
            return UpsertConversationAsync(userObjectId, conversationId, preview, agentNumber, cancellationToken);
        }

        public Task DeleteConversationAsync(string userObjectId, string conversationId, string? agentNumber, CancellationToken cancellationToken = default)
        {
            _summaries.RemoveAll(s => s.Id == conversationId && s.UserId == userObjectId && s.AgentNumber == agentNumber);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ConversationSummary>> GetSummariesAsync(string userObjectId, string? agentNumber, CancellationToken cancellationToken = default)
        {
            var result = _summaries
                .Where(s => s.UserId == userObjectId)
                .Where(s => string.Equals(s.AgentNumber, agentNumber, StringComparison.Ordinal))
                .OrderByDescending(s => s.UpdatedAt)
                .ToList();
            return Task.FromResult<IReadOnlyList<ConversationSummary>>(result);
        }

        public Task UpsertConversationAsync(string userObjectId, string conversationId, string title, string? agentNumber, CancellationToken cancellationToken = default)
        {
            var trimmed = title.Length > 30 ? title[..30] + "…" : title;
            var existing = _summaries.FirstOrDefault(s => s.Id == conversationId && s.UserId == userObjectId);
            if (existing is null)
            {
                _summaries.Add(new ConversationSummary(conversationId, trimmed, DateTimeOffset.UtcNow, agentNumber, userObjectId));
            }
            else
            {
                existing.Title = trimmed;
                existing.AgentNumber ??= agentNumber;
                existing.UpdatedAt = DateTimeOffset.UtcNow;
            }

            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ChatMessage>> GetMessagesAsync(string userObjectId, string conversationId, string? agentNumber = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ChatMessage>>(Array.Empty<ChatMessage>());
        }
    }

    private sealed class FakeDeviceAgentRepository : IDeviceAgentRepository
    {
        private readonly string _agentNumber;

        public FakeDeviceAgentRepository(string agentNumber)
        {
            _agentNumber = agentNumber;
        }

        public Task DeleteAsync(string userId, string number, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<DeviceAgentProfile>> GetAsync(string userId, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<DeviceAgentProfile> list = new[]
            {
                new DeviceAgentProfile(_agentNumber, "Agent", DateTimeOffset.UtcNow)
            };
            return Task.FromResult(list);
        }

        public Task<IReadOnlyList<DeviceAgentProfile>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return GetAsync(string.Empty, cancellationToken);
        }

        public Task<IReadOnlyList<DeviceAgentProfile>> GetByNumbersAsync(IEnumerable<string> agentNumbers, CancellationToken cancellationToken = default)
        {
            var numbers = new HashSet<string>(agentNumbers ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            if (numbers.Count == 0 || numbers.Contains(_agentNumber))
            {
                return GetAsync(string.Empty, cancellationToken);
            }

            return Task.FromResult<IReadOnlyList<DeviceAgentProfile>>(Array.Empty<DeviceAgentProfile>());
        }

        public Task<DeviceAgentProfile> UpsertAsync(string userId, string number, string name, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new DeviceAgentProfile(number, name, DateTimeOffset.UtcNow));
        }
    }

    private sealed class PassthroughAccessService : IDeviceAgentAccessService
    {
        private readonly IDeviceAgentRepository _repository;

        public PassthroughAccessService(IDeviceAgentRepository repository)
        {
            _repository = repository;
        }

        public Task<IReadOnlyList<DeviceAgentProfile>> GetAvailableAgentsAsync(string userId, CancellationToken cancellationToken = default)
        {
            return _repository.GetAsync(userId, cancellationToken);
        }

        public Task<IReadOnlyList<string>> GetAssignmentsAsync(string userId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        }

        public Task UpdateAssignmentsAsync(string userId, IEnumerable<string> agentNumbers, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<DeviceAgentProfile>> ListDefinitionsAsync(CancellationToken cancellationToken = default)
        {
            return _repository.GetAllAsync(cancellationToken);
        }
    }

    private sealed class StaticOptionsMonitor<T> : IOptionsMonitor<T>
    {
        public StaticOptionsMonitor(T currentValue)
        {
            CurrentValue = currentValue;
        }

        public T CurrentValue { get; }

        public T Get(string? name) => CurrentValue;

        public IDisposable OnChange(Action<T, string?> listener) => new NoopDisposable();

        private sealed class NoopDisposable : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }

#pragma warning disable BL0006 // RenderTree 型のテスト利用を許容
    private sealed class TestRenderer : Renderer
    {
        public TestRenderer() : base(new ServiceCollection().BuildServiceProvider(), NullLoggerFactory.Instance)
        {
            Dispatcher = new InlineDispatcher();
        }

        public override Dispatcher Dispatcher { get; }

        public void AttachComponent(IComponent component)
        {
            AssignRootComponentId(component);
        }

        protected override Task UpdateDisplayAsync(in RenderBatch renderBatch) => Task.CompletedTask;

        protected override void HandleException(Exception exception) => throw exception;

        private sealed class InlineDispatcher : Dispatcher
        {
            public override bool CheckAccess() => true;

            public override Task InvokeAsync(Action workItem)
            {
                workItem();
                return Task.CompletedTask;
            }

            public override Task InvokeAsync(Func<Task> workItem)
            {
                return workItem();
            }

            public override Task<TResult> InvokeAsync<TResult>(Func<TResult> workItem)
            {
                return Task.FromResult(workItem());
            }

            public override Task<TResult> InvokeAsync<TResult>(Func<Task<TResult>> workItem)
            {
                return workItem();
            }
        }
    }
#pragma warning restore BL0006
}
