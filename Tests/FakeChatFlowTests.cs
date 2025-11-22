using MOCHA.Models.Chat;
using MOCHA.Services.Chat;
using MOCHA.Services.Copilot;
using MOCHA.Services.Plc;
using Xunit;

namespace MOCHA.Tests;

public class FakeChatFlowTests
{
    [Fact]
    public async Task プレーンテキストならアシスタント応答が返る()
    {
        var orchestrator = new ChatOrchestrator(new FakeCopilotChatClient(), new FakePlcGatewayClient());
        var events = await CollectAsync(orchestrator, "こんにちは");

        Assert.Contains(events, e => e.Type == ChatStreamEventType.Message && e.Message?.Role == ChatRole.Assistant);
        Assert.Contains(events, e => e.Type == ChatStreamEventType.Completed);
    }

    [Fact]
    public async Task 読み取りキーワードならツール経由で値を返す()
    {
        var orchestrator = new ChatOrchestrator(new FakeCopilotChatClient(), new FakePlcGatewayClient());
        var events = await CollectAsync(orchestrator, "Please read D100");

        Assert.Contains(events, e => e.Type == ChatStreamEventType.ActionRequest);
        Assert.Contains(events, e => e.Type == ChatStreamEventType.ToolResult);
        Assert.Contains(events, e =>
            e.Type == ChatStreamEventType.Message &&
            e.Message?.Role == ChatRole.Assistant &&
            e.Message.Content.Contains("D100") &&
            e.Message.Content.Contains("42"));
        Assert.Contains(events, e =>
            e.Type == ChatStreamEventType.Message &&
            e.Message?.Role == ChatRole.Assistant &&
            e.Message.Content.Contains("Copilot Studio") &&
            e.Message.Content.Contains("D100"));
        Assert.Contains(events, e =>
            e.Type == ChatStreamEventType.Message &&
            e.Message?.Role == ChatRole.Assistant &&
            e.Message.Content.Contains("fake Copilot") &&
            e.Message.Content.Contains("D100"));
        Assert.Contains(events, e => e.Type == ChatStreamEventType.Completed);
    }

    private static async Task<List<ChatStreamEvent>> CollectAsync(ChatOrchestrator orchestrator, string text)
    {
        var list = new List<ChatStreamEvent>();
        await foreach (var ev in orchestrator.HandleUserMessageAsync(new UserContext("test-user", "Test User"), null, text))
        {
            list.Add(ev);
        }

        return list;
    }
}
