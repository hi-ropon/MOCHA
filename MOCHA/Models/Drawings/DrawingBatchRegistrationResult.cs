using System.Collections.Generic;

namespace MOCHA.Models.Drawings;

/// <summary>
/// 図面複数登録の結果
/// </summary>
public sealed class DrawingBatchRegistrationResult
{
    private DrawingBatchRegistrationResult(bool succeeded, string? error, IReadOnlyList<DrawingDocument>? documents)
    {
        Succeeded = succeeded;
        Error = error;
        Documents = documents;
    }

    /// <summary>成功フラグ</summary>
    public bool Succeeded { get; }
    /// <summary>エラーメッセージ</summary>
    public string? Error { get; }
    /// <summary>登録された図面一覧</summary>
    public IReadOnlyList<DrawingDocument>? Documents { get; }

    /// <summary>
    /// 成功結果生成
    /// </summary>
    /// <param name="documents">登録図面一覧</param>
    /// <returns>結果</returns>
    public static DrawingBatchRegistrationResult Success(IReadOnlyList<DrawingDocument> documents)
    {
        return new DrawingBatchRegistrationResult(true, null, documents);
    }

    /// <summary>
    /// 失敗結果生成
    /// </summary>
    /// <param name="error">エラー内容</param>
    /// <returns>結果</returns>
    public static DrawingBatchRegistrationResult Fail(string error)
    {
        return new DrawingBatchRegistrationResult(false, error, null);
    }
}
