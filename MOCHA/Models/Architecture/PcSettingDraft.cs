using System;
using System.Collections.Generic;

namespace MOCHA.Models.Architecture;

/// <summary>
/// PC設定入力用ドラフト
/// </summary>
public sealed class PcSettingDraft
{
    /// <summary>OS</summary>
    public string Os { get; init; } = string.Empty;
    /// <summary>役割</summary>
    public string? Role { get; init; }
    /// <summary>リポジトリURL一覧</summary>
    public IReadOnlyCollection<string> RepositoryUrls { get; init; } = new List<string>();

    /// <summary>
    /// 入力値のバリデーション
    /// </summary>
    /// <returns>検証結果</returns>
    public (bool IsValid, string? Error) Validate()
    {
        if (string.IsNullOrWhiteSpace(Os))
        {
            return (false, "OSを入力してください");
        }

        var urls = RepositoryUrls ?? Array.Empty<string>();
        if (urls.Count > 20)
        {
            return (false, "リポジトリURLは20件までにしてください");
        }

        foreach (var url in urls)
        {
            var text = url?.Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                return (false, "リポジトリURLは http/https の絶対URLで入力してください");
            }
        }

        return (true, null);
    }
}
