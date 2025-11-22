using System.Collections.Generic;

namespace MOCHA.Models.Chat;

public enum ChatRole
{
    User,
    Assistant,
    System,
    Tool
}

public record ChatMessage(ChatRole Role, string Content);

public record ChatTurn(string? ConversationId, IReadOnlyList<ChatMessage> Messages);

public record CopilotActionRequest(
    string ActionName,
    string ConversationId,
    IReadOnlyDictionary<string, object?> Payload
);

public record CopilotActionResult(
    string ActionName,
    string ConversationId,
    bool Success,
    IReadOnlyDictionary<string, object?> Payload,
    string? Error = null
);

public enum ChatStreamEventType
{
    Message,
    ActionRequest,
    ToolResult,
    Completed,
    Error
}

public record ChatStreamEvent(
    ChatStreamEventType Type,
    ChatMessage? Message = null,
    CopilotActionRequest? ActionRequest = null,
    CopilotActionResult? ActionResult = null,
    string? Error = null
)
{
    public static ChatStreamEvent FromMessage(ChatMessage message) =>
        new(ChatStreamEventType.Message, Message: message);

    public static ChatStreamEvent Completed(string? conversationId = null) =>
        new(ChatStreamEventType.Completed);

    public static ChatStreamEvent Fail(string error) =>
        new(ChatStreamEventType.Error, Error: error);
}

public record UserContext(string UserId, string DisplayName);
