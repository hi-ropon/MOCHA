using System;
using System.Text.Json.Serialization;

namespace MOCHA.Agents.Domain.Plc;

/// <summary>
/// ファンクションブロックの内容
/// </summary>
public sealed record FunctionBlockData(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("safeName")] string SafeName,
    [property: JsonPropertyName("labelContent")] string LabelContent,
    [property: JsonPropertyName("programContent")] string ProgramContent,
    [property: JsonPropertyName("createdAt")] DateTimeOffset? CreatedAt = null,
    [property: JsonPropertyName("updatedAt")] DateTimeOffset? UpdatedAt = null);
