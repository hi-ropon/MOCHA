using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MOCHA.Models.Drawings;

namespace MOCHA.Services.Drawings;

/// <summary>
/// 図面ファイルの読取サービス
/// </summary>
public sealed class DrawingContentReader
{
    private const int _defaultMaxBytes = 12_000;

    /// <summary>
    /// 図面ファイルを読み取る
    /// </summary>
    /// <param name="file">図面ファイル参照</param>
    /// <param name="maxBytes">最大読取バイト数</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>読取結果</returns>
    public async Task<DrawingContentResult> ReadAsync(DrawingFile file, int? maxBytes = null, CancellationToken cancellationToken = default)
    {
        if (file is null)
        {
            throw new ArgumentNullException(nameof(file));
        }

        if (!file.Exists || string.IsNullOrWhiteSpace(file.FullPath))
        {
            return DrawingContentResult.Fail("図面ファイルが見つかりません");
        }

        var limit = maxBytes.GetValueOrDefault(_defaultMaxBytes);
        var ext = file.Extension?.ToLowerInvariant() ?? string.Empty;

        if (IsPlainText(ext))
        {
            return await ReadTextAsync(file, limit, cancellationToken);
        }

        if (ext.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            var message = $"PDF プレビューは未対応です。ファイルパス: {file.FullPath}";
            return DrawingContentResult.Preview(message, file.FullPath);
        }

        var fallback = $"この形式のプレビューは未対応です: {ext}";
        return DrawingContentResult.Preview(fallback, file.FullPath);
    }

    private static bool IsPlainText(string extension)
    {
        return extension is ".txt" or ".log" or ".md" or ".csv";
    }

    private static async Task<DrawingContentResult> ReadTextAsync(DrawingFile file, int maxBytes, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(file.FullPath!);
        var bufferLength = (int)Math.Min(maxBytes, Math.Max(1, stream.Length));
        var buffer = new byte[bufferLength];
        var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
        var content = Encoding.UTF8.GetString(buffer, 0, read);
        var truncated = stream.Length > read;
        return DrawingContentResult.Success(content, file.FullPath, read, truncated);
    }
}
