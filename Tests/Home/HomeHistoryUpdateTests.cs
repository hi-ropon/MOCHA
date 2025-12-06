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

    /// <summary>
    /// Home.SendAsync のプライベート呼び出し
    /// </summary>
    /// <param name="home">対象コンポーネント</param>
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

    /// <summary>
    /// プライベートフィールド設定
    /// </summary>
    /// <param name="home">対象コンポーネント</param>
    /// <param name="fieldName">フィールド名</param>
    /// <param name="value">設定値</param>
    private static void SetPrivateField(Home home, string fieldName, object? value)
    {
        var field = typeof(Home).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, $"{fieldName} が見つかること");
        field.SetValue(home, value);
    }

    /// <summary>
    /// テスト用コンポーネント初期化
    /// </summary>
    /// <param name="home">対象コンポーネント</param>
    private static void InitializeComponent(Home home)
    {
        var renderer = new TestRenderer();
        renderer.AttachComponent(home);
    }

    /// <summary>
    /// プロパティ設定
    /// </summary>
    /// <param name="home">対象コンポーネント</param>
    /// <param name="propertyName">プロパティ名</param>
    /// <param name="value">設定値</param>
    private static void SetProperty(Home home, string propertyName, object? value)
    {
        var property = typeof(Home).GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.IsNotNull(property, $"{propertyName} が見つかること");
        property.SetValue(home, value);
    }

    /// <summary>
    /// 会話ID設定
    /// </summary>
    /// <param name="home">対象コンポーネント</param>
    /// <param name="value">会話ID</param>
    private static void SetConversationId(Home home, string value) => SetPrivateField(home, "ConversationId", value);

    /// <summary>
    /// コールバックを挟んでイベントを返すオーケストレーター
    /// </summary>
    private sealed class CallbackOrchestrator : IChatOrchestrator
    {
        private readonly Func<Task> _beforeActionRequest;
        private readonly string _conversationId;

        /// <summary>
        /// コールバックと会話IDを受け取るコンストラクター
        /// </summary>
        /// <param name="beforeActionRequest">アクション要求前に実行する処理</param>
        /// <param name="conversationId">会話ID</param>
        public CallbackOrchestrator(Func<Task> beforeActionRequest, string conversationId)
        {
            _beforeActionRequest = beforeActionRequest;
            _conversationId = conversationId;
        }

        /// <summary>
        /// ユーザー発話処理
        /// </summary>
        /// <param name="user">ユーザー情報</param>
        /// <param name="conversationId">会話ID</param>
        /// <param name="text">発話内容</param>
        /// <param name="agentNumber">エージェント番号</param>
        /// <param name="cancellationToken">キャンセル通知</param>
        /// <returns>イベントストリーム</returns>
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

    /// <summary>
    /// メモリ保持のチャットリポジトリ
    /// </summary>
    private sealed class RecordingChatRepository : IChatRepository
    {
        private readonly List<ConversationSummary> _summaries = new();

        /// <summary>
        /// 会話サマリ一覧
        /// </summary>
        public IReadOnlyList<ConversationSummary> Summaries => _summaries;

        /// <summary>
        /// サマリの種データ投入
        /// </summary>
        /// <param name="id">会話ID</param>
        /// <param name="title">タイトル</param>
        /// <param name="userId">ユーザーID</param>
        /// <param name="agentNumber">エージェント番号</param>
        /// <param name="updatedAt">更新日時</param>
        public void SeedSummary(string id, string title, string userId, string? agentNumber, DateTimeOffset updatedAt)
        {
            _summaries.Add(new ConversationSummary(id, title, updatedAt, agentNumber, userId));
        }

        /// <summary>
        /// メッセージ追加
        /// </summary>
        public Task AddMessageAsync(string userObjectId, string conversationId, ChatMessage message, string? agentNumber, CancellationToken cancellationToken = default)
        {
            var preview = message.Content.Length > 30 ? message.Content[..30] + "…" : message.Content;
            return UpsertConversationAsync(userObjectId, conversationId, preview, agentNumber, cancellationToken);
        }

        /// <summary>
        /// 会話削除
        /// </summary>
        public Task DeleteConversationAsync(string userObjectId, string conversationId, string? agentNumber, CancellationToken cancellationToken = default)
        {
            _summaries.RemoveAll(s => s.Id == conversationId && s.UserId == userObjectId && s.AgentNumber == agentNumber);
            return Task.CompletedTask;
        }

        /// <summary>
        /// 会話要約一覧取得
        /// </summary>
        public Task<IReadOnlyList<ConversationSummary>> GetSummariesAsync(string userObjectId, string? agentNumber, CancellationToken cancellationToken = default)
        {
            var result = _summaries
                .Where(s => s.UserId == userObjectId)
                .Where(s => string.Equals(s.AgentNumber, agentNumber, StringComparison.Ordinal))
                .OrderByDescending(s => s.UpdatedAt)
                .ToList();
            return Task.FromResult<IReadOnlyList<ConversationSummary>>(result);
        }

        /// <summary>
        /// 会話タイトル保存または更新
        /// </summary>
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

        /// <summary>
        /// メッセージ一覧取得（空）
        /// </summary>
        public Task<IReadOnlyList<ChatMessage>> GetMessagesAsync(string userObjectId, string conversationId, string? agentNumber = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ChatMessage>>(Array.Empty<ChatMessage>());
        }
    }

    /// <summary>
    /// 単一エージェントのみを返すフェイクリポジトリ
    /// </summary>
    private sealed class FakeDeviceAgentRepository : IDeviceAgentRepository
    {
        private readonly string _agentNumber;

        /// <summary>
        /// エージェント番号指定の初期化
        /// </summary>
        /// <param name="agentNumber">エージェント番号</param>
        public FakeDeviceAgentRepository(string agentNumber)
        {
            _agentNumber = agentNumber;
        }

        /// <summary>
        /// エージェント削除（何もしない）
        /// </summary>
        public Task DeleteAsync(string userId, string number, CancellationToken cancellationToken = default) => Task.CompletedTask;

        /// <summary>
        /// ユーザー別エージェント取得
        /// </summary>
        public Task<IReadOnlyList<DeviceAgentProfile>> GetAsync(string userId, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<DeviceAgentProfile> list = new[]
            {
                new DeviceAgentProfile(_agentNumber, "Agent", DateTimeOffset.UtcNow)
            };
            return Task.FromResult(list);
        }

        /// <summary>
        /// 全エージェント取得
        /// </summary>
        public Task<IReadOnlyList<DeviceAgentProfile>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return GetAsync(string.Empty, cancellationToken);
        }

        /// <summary>
        /// 番号指定でエージェント取得
        /// </summary>
        public Task<IReadOnlyList<DeviceAgentProfile>> GetByNumbersAsync(IEnumerable<string> agentNumbers, CancellationToken cancellationToken = default)
        {
            var numbers = new HashSet<string>(agentNumbers ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            if (numbers.Count == 0 || numbers.Contains(_agentNumber))
            {
                return GetAsync(string.Empty, cancellationToken);
            }

            return Task.FromResult<IReadOnlyList<DeviceAgentProfile>>(Array.Empty<DeviceAgentProfile>());
        }

        /// <summary>
        /// エージェント追加または更新
        /// </summary>
        public Task<DeviceAgentProfile> UpsertAsync(string userId, string number, string name, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new DeviceAgentProfile(number, name, DateTimeOffset.UtcNow));
        }
    }

    /// <summary>
    /// 許可制御をそのまま通すフェイクサービス
    /// </summary>
    private sealed class PassthroughAccessService : IDeviceAgentAccessService
    {
        private readonly IDeviceAgentRepository _repository;

        /// <summary>
        /// リポジトリ注入による初期化
        /// </summary>
        /// <param name="repository">エージェントリポジトリ</param>
        public PassthroughAccessService(IDeviceAgentRepository repository)
        {
            _repository = repository;
        }

        /// <summary>
        /// 利用可能エージェント取得
        /// </summary>
        public Task<IReadOnlyList<DeviceAgentProfile>> GetAvailableAgentsAsync(string userId, CancellationToken cancellationToken = default)
        {
            return _repository.GetAsync(userId, cancellationToken);
        }

        /// <summary>
        /// 割り付け一覧取得
        /// </summary>
        public Task<IReadOnlyList<string>> GetAssignmentsAsync(string userId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        }

        /// <summary>
        /// 割り付け更新（何もしない）
        /// </summary>
        public Task UpdateAssignmentsAsync(string userId, IEnumerable<string> agentNumbers, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// 全エージェント定義取得
        /// </summary>
        public Task<IReadOnlyList<DeviceAgentProfile>> ListDefinitionsAsync(CancellationToken cancellationToken = default)
        {
            return _repository.GetAllAsync(cancellationToken);
        }
    }

    /// <summary>
    /// 静的値を返す IOptionsMonitor 実装
    /// </summary>
    private sealed class StaticOptionsMonitor<T> : IOptionsMonitor<T>
    {
        /// <summary>
        /// 現在値を受け取るコンストラクター
        /// </summary>
        /// <param name="currentValue">返却する値</param>
        public StaticOptionsMonitor(T currentValue)
        {
            CurrentValue = currentValue;
        }

        /// <summary>
        /// 現在値
        /// </summary>
        public T CurrentValue { get; }

        /// <summary>
        /// 名前付きオプション取得
        /// </summary>
        /// <param name="name">オプション名</param>
        /// <returns>現在値</returns>
        public T Get(string? name) => CurrentValue;

        /// <summary>
        /// 変更通知登録（何もしない）
        /// </summary>
        /// <param name="listener">リスナー</param>
        /// <returns>破棄用ハンドル</returns>
        public IDisposable OnChange(Action<T, string?> listener) => new NoopDisposable();

        private sealed class NoopDisposable : IDisposable
        {
            /// <summary>
            /// 破棄処理（何もしない）
            /// </summary>
            public void Dispose()
            {
            }
        }
    }

#pragma warning disable BL0006 // RenderTree 型のテスト利用を許容
    /// <summary>
    /// テスト用レンダラー
    /// </summary>
    private sealed class TestRenderer : Renderer
    {
        /// <summary>
        /// テストレンダラー初期化
        /// </summary>
        public TestRenderer() : base(new ServiceCollection().BuildServiceProvider(), NullLoggerFactory.Instance)
        {
            Dispatcher = new InlineDispatcher();
        }

        /// <summary>
        /// ディスパッチャ
        /// </summary>
        public override Dispatcher Dispatcher { get; }

        /// <summary>
        /// コンポーネントのルートへのアタッチ
        /// </summary>
        /// <param name="component">アタッチ対象</param>
        public void AttachComponent(IComponent component)
        {
            AssignRootComponentId(component);
        }

        /// <summary>
        /// 画面更新処理（何もしない）
        /// </summary>
        protected override Task UpdateDisplayAsync(in RenderBatch renderBatch) => Task.CompletedTask;

        /// <summary>
        /// 例外ハンドリング（再スロー）
        /// </summary>
        protected override void HandleException(Exception exception) => throw exception;

        /// <summary>
        /// インライン実行用ディスパッチャ
        /// </summary>
        private sealed class InlineDispatcher : Dispatcher
        {
            /// <summary>
            /// 常にアクセス可を返す
            /// </summary>
            public override bool CheckAccess() => true;

            /// <summary>
            /// 同期ワーク項目の実行
            /// </summary>
            public override Task InvokeAsync(Action workItem)
            {
                workItem();
                return Task.CompletedTask;
            }

            /// <summary>
            /// 非同期ワーク項目の実行
            /// </summary>
            public override Task InvokeAsync(Func<Task> workItem)
            {
                return workItem();
            }

            /// <summary>
            /// 戻り値付き同期ワーク項目の実行
            /// </summary>
            public override Task<TResult> InvokeAsync<TResult>(Func<TResult> workItem)
            {
                return Task.FromResult(workItem());
            }

            /// <summary>
            /// 戻り値付き非同期ワーク項目の実行
            /// </summary>
            public override Task<TResult> InvokeAsync<TResult>(Func<Task<TResult>> workItem)
            {
                return workItem();
            }
        }
    }
#pragma warning restore BL0006
}
