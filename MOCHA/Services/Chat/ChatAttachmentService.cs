using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Logging;
using MOCHA.Models.Chat;

namespace MOCHA.Services.Chat;

/// <summary>
/// 画像添付の簡易アップロードサービス（Base64 生成のみ）
/// </summary>
internal sealed class ChatAttachmentService : IChatAttachmentService
{
    private const long _maxSizeBytes = 10 * 1024 * 1024;
    private const int _readBufferSize = 81920;
    private static readonly HashSet<string> _allowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png",
        "image/jpeg"
    };

    private readonly ILogger<ChatAttachmentService> _logger;

    /// <summary>
    /// ロガー注入による初期化
    /// </summary>
    /// <param name="logger">ロガー</param>
    public ChatAttachmentService(ILogger<ChatAttachmentService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ImageAttachment> UploadAsync(IBrowserFile file, CancellationToken cancellationToken = default)
    {
        if (file is null)
        {
            throw new ArgumentNullException(nameof(file));
        }

        if (!_allowedTypes.Contains(file.ContentType))
        {
            throw new InvalidOperationException("対応していない形式の画像です");
        }

        if (file.Size > _maxSizeBytes)
        {
            throw new InvalidOperationException("画像サイズが上限を超えています（最大10MB）");
        }

        await using var stream = file.OpenReadStream(_maxSizeBytes, cancellationToken);
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, _readBufferSize, cancellationToken);
        var bytes = memory.ToArray();
        return await UploadAsync(file.Name, file.ContentType, bytes, cancellationToken);
    }

    /// <inheritdoc />
    public Task<ImageAttachment> UploadAsync(string fileName, string contentType, byte[] data, CancellationToken cancellationToken = default)
    {
        if (!_allowedTypes.Contains(contentType))
        {
            throw new InvalidOperationException("対応していない形式の画像です");
        }

        if (data is null || data.Length == 0)
        {
            throw new InvalidOperationException("画像が空です");
        }

        if (data.LongLength > _maxSizeBytes)
        {
            throw new InvalidOperationException("画像サイズが上限を超えています（最大10MB）");
        }

        var base64 = Convert.ToBase64String(data);
        var dataUrl = $"data:{contentType};base64,{base64}";
        var attachment = new ImageAttachment(
            Guid.NewGuid().ToString("N"),
            fileName,
            contentType,
            data.LongLength,
            dataUrl,
            dataUrl,
            DateTimeOffset.UtcNow);

        _logger.LogDebug("画像を一時保存しました: {FileName} ({Size} bytes)", fileName, data.LongLength);
        return Task.FromResult(attachment);
    }

    /// <inheritdoc />
    public Task DeleteAsync(string attachmentId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(attachmentId))
        {
            return Task.CompletedTask;
        }

        _logger.LogDebug("画像を破棄しました: {AttachmentId}", attachmentId);
        return Task.CompletedTask;
    }
}
