using System;

namespace MOCHA.Agents.Application;

/// <summary>
/// Organizer プロンプトに差し込むコンテキスト
/// </summary>
public sealed class OrganizerContext
{
    /// <summary>アーキテクチャ設定要約</summary>
    public string Architecture { get; }

    /// <summary>図面管理要約</summary>
    public string Drawings { get; }

    /// <summary>
    /// 新しいコンテキストを初期化
    /// </summary>
    /// <param name="architecture">アーキテクチャ設定要約</param>
    /// <param name="drawings">図面管理要約</param>
    public OrganizerContext(string? architecture = null, string? drawings = null)
    {
        Architecture = architecture?.Trim() ?? string.Empty;
        Drawings = drawings?.Trim() ?? string.Empty;
    }

    /// <summary>空コンテキスト</summary>
    public static OrganizerContext Empty { get; } = new();

    /// <summary>
    /// 空判定
    /// </summary>
    public bool IsEmpty =>
        string.IsNullOrWhiteSpace(Architecture) &&
        string.IsNullOrWhiteSpace(Drawings);
}
