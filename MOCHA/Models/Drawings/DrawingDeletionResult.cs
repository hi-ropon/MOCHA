namespace MOCHA.Models.Drawings;

/// <summary>
/// 図面削除結果
/// </summary>
public sealed class DrawingDeletionResult
{
    private DrawingDeletionResult(bool succeeded, string? error)
    {
        Succeeded = succeeded;
        Error = error;
    }

    /// <summary>成功フラグ</summary>
    public bool Succeeded { get; }
    /// <summary>エラーメッセージ</summary>
    public string? Error { get; }

    /// <summary>
    /// 成功結果生成
    /// </summary>
    /// <returns>結果</returns>
    public static DrawingDeletionResult Success()
    {
        return new DrawingDeletionResult(true, null);
    }

    /// <summary>
    /// 失敗結果生成
    /// </summary>
    /// <param name="error">エラー内容</param>
    /// <returns>結果</returns>
    public static DrawingDeletionResult Fail(string error)
    {
        return new DrawingDeletionResult(false, error);
    }
}
