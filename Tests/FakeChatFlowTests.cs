using MOCHA.Models.Chat;
using MOCHA.Services.Chat;
using MOCHA.Services.Copilot;
using MOCHA.Services.Plc;
using Xunit;

namespace MOCHA.Tests;

public class FakeChatFlowTests
{
    [Fact]
    public async Task HandleUserMessage_WithPlainText_EmitsAssistantMessage()
    {
        var orchestrator = new ChatOrchestrator(new FakeCopilotChatClient(), new FakePlcGatewayClient());
        var events = await CollectAsync(orchestrator, "こんにちは");

        Assert.Contains(events, e => e.Type == ChatStreamEventType.Message && e.Message?.Role == ChatRole.Assistant);
        Assert.Contains(events, e => e.Type == ChatStreamEventType.Completed);
    }

    [Fact]
    public async Task HandleUserMessage_WithReadKeyword_EmitsToolFlow()
    {
        var orchestrator = new ChatOrchestrator(new FakeCopilotChatClient(), new FakePlcGatewayClient());
        var events = await CollectAsync(orchestrator, "Please read D100");

        Assert.Contains(events, e => e.Type == ChatStreamEventType.ActionRequest);
        Assert.Contains(events, e => e.Type == ChatStreamEventType.ToolResult);
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
