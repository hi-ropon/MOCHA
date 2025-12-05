using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MOCHA.Components.Pages;
using MOCHA.Models.Agents;
using MOCHA.Models.Chat;
using MOCHA.Services.Agents;
using MOCHA.Services.Chat;

namespace MOCHA.Tests;

/// <summary>
/// Home コンポーネントのキーボード送信挙動のテスト
/// </summary>
[TestClass]
public class HomeComposerKeyboardTests
{
    /// <summary>
    /// Enter キーだけで送信できること
    /// </summary>
    [TestMethod]
    public async Task HandleKeyDown_Enter押下_送信する()
    {
        var home = new Home();
        InitializeComponent(home);

        var orchestrator = new RecordingOrchestrator();
        var agentState = new DeviceAgentState(new StubDeviceAgentRepository(), new StubDeviceAgentAccessService());

        SetProperty(home, "Orchestrator", orchestrator);
        SetProperty(home, "AgentState", agentState);
        SetProperty(home, "ChatRepository", new NullChatRepository());
        SetProperty(home, "HistoryState", new ConversationHistoryState(new NullChatRepository()));
        SetProperty(
            home,
            "AuthenticationStateTask",
            Task.FromResult(
                new AuthenticationState(
                    new ClaimsPrincipal(
                        new ClaimsIdentity(
                            new[] { new Claim("oid", "user-enter"), new Claim("name", "Tester") },
                            "test")))));

        SetPrivateField(home, "_user", new UserContext("user-enter", "Tester"));
        SetPrivateField(home, "Input", "enter send");
        SetSelectedAgent(agentState, "AG-01");

        await InvokeHandleKeyDownAsync(home, new KeyboardEventArgs { Key = "Enter" });

        var messages = GetMessages(home);
        Assert.AreEqual(1, messages.Count, "ユーザーメッセージを追加すること");
        Assert.AreEqual(ChatRole.User, messages[0].Role, "ユーザー役割で送信すること");
        Assert.AreEqual("enter send", messages[0].Content, "入力内容を送信すること");
        Assert.AreEqual(1, orchestrator.Received.Count, "オーケストレーターへ送信すること");
        Assert.AreEqual("enter send", orchestrator.Received[0].text, "Enter押下で送信内容を渡すこと");
        Assert.AreEqual(string.Empty, GetInput(home), "送信後に入力欄をクリアすること");
    }

    /// <summary>
    /// Home.HandleKeyDown のプライベート呼び出し
    /// </summary>
    /// <param name="home">対象コンポーネント</param>
    /// <param name="args">キーボードイベント</param>
    private static async Task InvokeHandleKeyDownAsync(Home home, KeyboardEventArgs args)
    {
        var method = typeof(Home).GetMethod("HandleKeyDown", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method, "HandleKeyDown が見つかること");

        if (method.Invoke(home, new object[] { args }) is Task task)
        {
            await task;
        }
        else
        {
            Assert.Fail("HandleKeyDown が Task を返すこと");
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
    /// プライベートプロパティ設定
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
    /// 入力値取得
    /// </summary>
    /// <param name="home">対象コンポーネント</param>
    /// <returns>入力値</returns>
    private static string GetInput(Home home)
    {
        var field = typeof(Home).GetField("Input", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, "Input が見つかること");
        return field.GetValue(home) as string ?? string.Empty;
    }

    /// <summary>
    /// メッセージ一覧取得
    /// </summary>
    /// <param name="home">対象コンポーネント</param>
    /// <returns>メッセージリスト</returns>
    private static List<ChatMessage> GetMessages(Home home)
    {
        var field = typeof(Home).GetField("Messages", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, "Messages が見つかること");
        return (List<ChatMessage>)field.GetValue(home)!;
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
    /// 選択エージェント設定
    /// </summary>
    /// <param name="state">状態オブジェクト</param>
    /// <param name="agentNumber">エージェント番号</param>
    private static void SetSelectedAgent(DeviceAgentState state, string agentNumber)
    {
        var property = typeof(DeviceAgentState).GetProperty("SelectedAgentNumber", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.IsNotNull(property, "SelectedAgentNumber が見つかること");
        property.SetValue(state, agentNumber);
    }

    /// <summary>
    /// Enter 送信呼び出しを記録するオーケストレーター
    /// </summary>
    private sealed class RecordingOrchestrator : IChatOrchestrator
    {
        /// <summary>
        /// 受信記録
        /// </summary>
        public List<(UserContext user, string? conversationId, string text, string? agentNumber)> Received { get; } = new();

        /// <summary>
        /// ユーザー発話処理
        /// </summary>
        /// <param name="user">ユーザー</param>
        /// <param name="conversationId">会話ID</param>
        /// <param name="text">発話本文</param>
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
            Received.Add((user, conversationId, text, agentNumber));
            yield return ChatStreamEvent.Completed(conversationId ?? Guid.NewGuid().ToString("N"));
            await Task.CompletedTask;
        }
    }

    /// <summary>
    /// 空実装のチャットリポジトリ
    /// </summary>
    private sealed class NullChatRepository : IChatRepository
    {
        /// <inheritdoc />
        public Task AddMessageAsync(string userObjectId, string conversationId, ChatMessage message, string? agentNumber, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task DeleteConversationAsync(string userObjectId, string conversationId, string? agentNumber, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<ChatMessage>> GetMessagesAsync(string userObjectId, string conversationId, string? agentNumber = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ChatMessage>>(Array.Empty<ChatMessage>());
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<ConversationSummary>> GetSummariesAsync(string userObjectId, string? agentNumber, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ConversationSummary>>(Array.Empty<ConversationSummary>());
        }

        /// <inheritdoc />
        public Task UpsertConversationAsync(string userObjectId, string conversationId, string title, string? agentNumber, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 空実装の装置エージェントリポジトリ
    /// </summary>
    private sealed class StubDeviceAgentRepository : IDeviceAgentRepository
    {
        /// <inheritdoc />
        public Task<DeviceAgentProfile> UpsertAsync(string userId, string number, string name, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new DeviceAgentProfile(number, name, DateTimeOffset.UtcNow));
        }

        /// <inheritdoc />
        public Task DeleteAsync(string userId, string number, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<DeviceAgentProfile>> GetAsync(string userId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<DeviceAgentProfile>>(Array.Empty<DeviceAgentProfile>());
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<DeviceAgentProfile>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<DeviceAgentProfile>>(Array.Empty<DeviceAgentProfile>());
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<DeviceAgentProfile>> GetByNumbersAsync(IEnumerable<string> agentNumbers, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<DeviceAgentProfile>>(Array.Empty<DeviceAgentProfile>());
        }
    }

    /// <summary>
    /// 空実装の装置エージェントアクセスサービス
    /// </summary>
    private sealed class StubDeviceAgentAccessService : IDeviceAgentAccessService
    {
        /// <inheritdoc />
        public Task<IReadOnlyList<DeviceAgentProfile>> GetAvailableAgentsAsync(string userId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<DeviceAgentProfile>>(Array.Empty<DeviceAgentProfile>());
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<string>> GetAssignmentsAsync(string userId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<DeviceAgentProfile>> ListDefinitionsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<DeviceAgentProfile>>(Array.Empty<DeviceAgentProfile>());
        }

        /// <inheritdoc />
        public Task UpdateAssignmentsAsync(string userId, IEnumerable<string> agentNumbers, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

#pragma warning disable BL0006
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
        /// <param name="component">対象コンポーネント</param>
        public void AttachComponent(IComponent component)
        {
            AssignRootComponentId(component);
        }

        /// <summary>
        /// 画面更新処理
        /// </summary>
        /// <param name="renderBatch">レンダー結果</param>
        /// <returns>完了タスク</returns>
        protected override Task UpdateDisplayAsync(in RenderBatch renderBatch) => Task.CompletedTask;

        /// <summary>
        /// 例外ハンドリング
        /// </summary>
        /// <param name="exception">例外</param>
        protected override void HandleException(Exception exception) => throw exception;

        /// <summary>
        /// 同期実行ディスパッチャ
        /// </summary>
        private sealed class InlineDispatcher : Dispatcher
        {
            /// <summary>
            /// 同期アクセス可否
            /// </summary>
            /// <returns>常に true</returns>
            public override bool CheckAccess() => true;

            /// <summary>
            /// 同期ワーク実行
            /// </summary>
            /// <param name="workItem">処理</param>
            public override Task InvokeAsync(Action workItem)
            {
                workItem();
                return Task.CompletedTask;
            }

            /// <summary>
            /// 非同期ワーク実行
            /// </summary>
            /// <param name="workItem">処理</param>
            public override Task InvokeAsync(Func<Task> workItem)
            {
                return workItem();
            }

            /// <summary>
            /// 戻り値付き同期ワーク実行
            /// </summary>
            /// <param name="workItem">処理</param>
            /// <typeparam name="TResult">戻り値</typeparam>
            public override Task<TResult> InvokeAsync<TResult>(Func<TResult> workItem)
            {
                return Task.FromResult(workItem());
            }

            /// <summary>
            /// 戻り値付き非同期ワーク実行
            /// </summary>
            /// <param name="workItem">処理</param>
            /// <typeparam name="TResult">戻り値</typeparam>
            public override Task<TResult> InvokeAsync<TResult>(Func<Task<TResult>> workItem)
            {
                return workItem();
            }
        }
    }
#pragma warning restore BL0006
}
