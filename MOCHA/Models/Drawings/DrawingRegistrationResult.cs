namespace MOCHA.Models.Drawings;

/// <summary>
/// 図面登録・更新の結果
/// </summary>
public sealed class DrawingRegistrationResult
{
    private DrawingRegistrationResult(bool succeeded, string? error, DrawingDocument? document)
    {
        Succeeded = succeeded;
        Error = error;
        Document = document;
    }

    /// <summary>成功フラグ</summary>
    public bool Succeeded { get; }
    /// <summary>エラーメッセージ</summary>
    public string? Error { get; }
    /// <summary>登録・更新された図面</summary>
    public DrawingDocument? Document { get; }

    /// <summary>
    /// 成功結果生成
    /// </summary>
    /// <param name="document">登録図面</param>
    /// <returns>結果</returns>
    public static DrawingRegistrationResult Success(DrawingDocument document)
    {
        return new DrawingRegistrationResult(true, null, document);
    }

    /// <summary>
    /// 失敗結果生成
    /// </summary>
    /// <param name="error">エラー内容</param>
    /// <returns>結果</returns>
    public static DrawingRegistrationResult Fail(string error)
    {
        return new DrawingRegistrationResult(false, error, null);
    }
}
