using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.Forms;
using MOCHA.Models.Chat;

namespace MOCHA.Services.Chat;

/// <summary>
/// チャット用画像添付のアップロードと破棄を扱うサービス
/// </summary>
public interface IChatAttachmentService
{
    /// <summary>
    /// 画像の検証とアップロード
    /// </summary>
    /// <param name="file">アップロードファイル</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>作成された添付</returns>
    Task<ImageAttachment> UploadAsync(IBrowserFile file, CancellationToken cancellationToken = default);

    /// <summary>
    /// 生データからの画像アップロード
    /// </summary>
    /// <param name="fileName">ファイル名</param>
    /// <param name="contentType">コンテンツタイプ</param>
    /// <param name="data">画像データ</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>作成された添付</returns>
    Task<ImageAttachment> UploadAsync(string fileName, string contentType, byte[] data, CancellationToken cancellationToken = default);

    /// <summary>
    /// 添付削除
    /// </summary>
    /// <param name="attachmentId">削除対象ID</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    Task DeleteAsync(string attachmentId, CancellationToken cancellationToken = default);
}
