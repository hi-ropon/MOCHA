namespace MOCHA.Models.Architecture;

/// <summary>
/// ファンクションブロック操作結果
/// </summary>
public sealed class FunctionBlockResult
{
    private FunctionBlockResult(bool succeeded, string? error, FunctionBlock? value)
    {
        Succeeded = succeeded;
        Error = error;
        Value = value;
    }

    /// <summary>成功可否</summary>
    public bool Succeeded { get; }
    /// <summary>エラーメッセージ</summary>
    public string? Error { get; }
    /// <summary>結果値</summary>
    public FunctionBlock? Value { get; }

    /// <summary>
    /// 成功結果を生成
    /// </summary>
    /// <param name="value">ファンクションブロック</param>
    /// <returns>結果</returns>
    public static FunctionBlockResult Success(FunctionBlock value)
    {
        return new FunctionBlockResult(true, null, value);
    }

    /// <summary>
    /// 失敗結果を生成
    /// </summary>
    /// <param name="error">エラーメッセージ</param>
    /// <returns>結果</returns>
    public static FunctionBlockResult Fail(string error)
    {
        return new FunctionBlockResult(false, error, null);
    }
}
