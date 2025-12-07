namespace MOCHA.Agents.Domain;

/// <summary>
/// チャット添付画像
/// </summary>
public sealed record ChatAttachment(string FileName, string ContentType, byte[] Data);
