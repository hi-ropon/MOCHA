using System.Diagnostics;
using MOCHA.Models.Chat;
using MOCHA.Services.Chat;
using MOCHA.Services.Copilot;
using MOCHA.Services.Plc;

#if DEBUG
namespace MOCHA.Tests;

/// <summary>
/// Copilot/Gateway を実接続せずに動作確認する簡易スモークテスト。
/// 開発中に必要に応じて呼び出して利用する想定（自動では走らない）。
/// </summary>
public static class ChatOrchestratorTests
{
    public static async Task RunSmokeAsync()
    {
        var copilot = new FakeCopilotChatClient();
        var plc = new FakePlcGatewayClient();
        var orchestrator = new ChatOrchestrator(copilot, plc);

        var events = new List<ChatStreamEvent>();
        await foreach (var ev in orchestrator.HandleUserMessageAsync(
                           new UserContext("test-user", "Test User"),
                           conversationId: null,
                           text: "Please read D100",
                           CancellationToken.None))
        {
            events.Add(ev);
        }

        Debug.Assert(events.Any(e => e.Type == ChatStreamEventType.ActionRequest), "should request action");
        Debug.Assert(events.Any(e => e.Type == ChatStreamEventType.ToolResult), "should emit tool result");
        Debug.Assert(events.Any(e => e.Type == ChatStreamEventType.Completed), "should complete stream");
    }
}
#endif
