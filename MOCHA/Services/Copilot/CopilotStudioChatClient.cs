using System.Runtime.CompilerServices;
using Microsoft.Agents.CopilotStudio.Client;
using Microsoft.Agents.CopilotStudio.Client.Discovery;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MOCHA.Models.Chat;

namespace MOCHA.Services.Copilot;

/// <summary>
/// Copilot Studio SDK を利用したクライアント。設定不足時はフェイクにフォールバックする。
/// </summary>
public sealed class CopilotStudioChatClient : ICopilotChatClient
{
    private readonly CopilotClient? _client;
    private readonly ICopilotChatClient _fallback = new FakeCopilotChatClient();
    private readonly ILogger<CopilotStudioChatClient> _logger;
    private readonly bool _configured;

    /// <summary>
    /// 設定と HTTP クライアントファクトリを用いて Copilot Studio クライアントを初期化する。
    /// 設定が不足している場合はフェイククライアントにフォールバックする。
    /// </summary>
    /// <param name="options">Copilot Studio 設定。</param>
    /// <param name="httpClientFactory">HTTP クライアントファクトリ。</param>
    /// <param name="logger">ロガー。</param>
    public CopilotStudioChatClient(
        IOptions<CopilotStudioOptions> options,
        IHttpClientFactory httpClientFactory,
        ILogger<CopilotStudioChatClient> logger)
    {
        _logger = logger;
        var opt = options.Value;
        _configured = opt.Enabled &&
                      (!string.IsNullOrWhiteSpace(opt.DirectConnectUrl) ||
                       !string.IsNullOrWhiteSpace(opt.EnvironmentId)) &&
                      !string.IsNullOrWhiteSpace(opt.SchemaName);

        if (!_configured)
        {
            _logger.LogWarning("Copilot Studio 接続設定が不足しているため、フェイククライアントを使用します。");
            return;
        }

        try
        {
            var settings = new ConnectionSettings
            {
                SchemaName = opt.SchemaName,
                EnvironmentId = opt.EnvironmentId ?? string.Empty,
                Cloud = opt.Cloud,
                CopilotAgentType = opt.AgentType,
                DirectConnectUrl = opt.DirectConnectUrl,
                CustomPowerPlatformCloud = opt.CustomPowerPlatformCloud,
                UseExperimentalEndpoint = opt.UseExperimentalEndpoint,
                EnableDiagnostics = opt.EnableDiagnostics
            };

            Func<string, Task<string>> tokenProvider = _ => Task.FromResult(opt.AccessToken ?? string.Empty);

            _client = new CopilotClient(
                settings,
                httpClientFactory,
                tokenProvider,
                logger,
                opt.HttpClientName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Copilot Studio クライアントの初期化に失敗しました。フェイクにフォールバックします。");
            _configured = false;
        }
    }

    /// <summary>
    /// Copilot Studio へチャットターンを送り、応答ストリームを取得する。
    /// 設定不足や送信失敗時はフェイククライアントに切り替える。
    /// </summary>
    /// <param name="turn">送信するターン。</param>
    /// <param name="cancellationToken">キャンセル通知。</param>
    /// <returns>イベントストリーム。</returns>
    public Task<IAsyncEnumerable<ChatStreamEvent>> SendAsync(ChatTurn turn, CancellationToken cancellationToken = default)
    {
        if (!_configured || _client is null)
        {
            return _fallback.SendAsync(turn, cancellationToken);
        }

        async IAsyncEnumerable<ChatStreamEvent> Enumerate([EnumeratorCancellation] CancellationToken ct = default)
        {
            var latest = turn.Messages.LastOrDefault();
            if (latest is null)
            {
                yield return ChatStreamEvent.Fail("empty turn");
                yield break;
            }

            IAsyncEnumerable<IActivity> stream;
            bool fallback = false;
            try
            {
                stream = _client.AskQuestionAsync(latest.Content, turn.ConversationId, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Copilot Studio への送信に失敗しました。フェイクにフォールバックします。");
                fallback = true;
                stream = AsyncEnumerable.Empty<IActivity>();
            }

            if (fallback)
            {
                var fallbackEvents = await _fallback.SendAsync(turn, ct);
                await foreach (var ev in fallbackEvents.WithCancellation(ct))
                {
                    yield return ev;
                }
                yield break;
            }

            await foreach (var activity in stream.WithCancellation(ct))
            {
                if (string.Equals(activity.Type, "endOfConversation", StringComparison.OrdinalIgnoreCase))
                {
                    yield return ChatStreamEvent.Completed(turn.ConversationId);
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(activity.Text))
                {
                    yield return ChatStreamEvent.FromMessage(new ChatMessage(ChatRole.Assistant, activity.Text));
                }
            }

            yield return ChatStreamEvent.Completed(turn.ConversationId);
        }

        return Task.FromResult<IAsyncEnumerable<ChatStreamEvent>>(Enumerate(cancellationToken));
    }

    /// <summary>
    /// ツールの実行結果を Copilot Studio に送信する（現状はログのみ）。
    /// フェイク動作時はフェイククライアントに委譲する。
    /// </summary>
    /// <param name="result">ツール実行結果。</param>
    /// <param name="cancellationToken">キャンセル通知。</param>
    /// <returns>送信タスク。</returns>
    public Task SubmitActionResultAsync(CopilotActionResult result, CancellationToken cancellationToken = default)
    {
        if (!_configured || _client is null)
        {
            return _fallback.SubmitActionResultAsync(result, cancellationToken);
        }

        // SDK にはアクション結果を送る API が未定義のため、現状はログのみ。
        _logger.LogInformation("ActionResult を Copilot Studio に送信予定: {Action} {Conversation}", result.ActionName, result.ConversationId);
        return Task.CompletedTask;
    }
}
