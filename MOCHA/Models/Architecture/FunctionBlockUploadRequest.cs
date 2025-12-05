using Microsoft.AspNetCore.Http;

namespace MOCHA.Models.Architecture;

/// <summary>
/// ファンクションブロック登録用リクエストフォーム
/// </summary>
public sealed class FunctionBlockUploadRequest
{
    /// <summary>エージェント番号</summary>
    public string AgentNumber { get; init; } = string.Empty;
    /// <summary>ファンクションブロック名</summary>
    public string Name { get; init; } = string.Empty;
    /// <summary>ラベルCSV</summary>
    public IFormFile? LabelFile { get; init; }
    /// <summary>プログラムCSV</summary>
    public IFormFile? ProgramFile { get; init; }
}
