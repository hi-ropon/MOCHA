namespace MOCHA.Services.Markdown;

/// <summary>
/// Markdownレンダリングサービス
/// </summary>
public interface IMarkdownRenderer
{
    /// <summary>
    /// Markdownをサニタイズ済みHTMLへ変換
    /// </summary>
    /// <param name="markdown">変換対象Markdown</param>
    /// <returns>HTML文字列</returns>
    string Render(string markdown);
}
