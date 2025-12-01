namespace MOCHA.Models.Architecture;

/// <summary>
/// PLCユニット操作の結果
/// </summary>
public sealed class PlcUnitResult
{
    private PlcUnitResult(bool succeeded, string? error, PlcUnit? unit)
    {
        Succeeded = succeeded;
        Error = error;
        Unit = unit;
    }

    /// <summary>成功フラグ</summary>
    public bool Succeeded { get; }
    /// <summary>エラー内容</summary>
    public string? Error { get; }
    /// <summary>操作対象ユニット</summary>
    public PlcUnit? Unit { get; }

    /// <summary>
    /// 成功結果生成
    /// </summary>
    /// <param name="unit">ユニット</param>
    /// <returns>結果</returns>
    public static PlcUnitResult Success(PlcUnit unit)
    {
        return new PlcUnitResult(true, null, unit);
    }

    /// <summary>
    /// 失敗結果生成
    /// </summary>
    /// <param name="error">エラー内容</param>
    /// <returns>結果</returns>
    public static PlcUnitResult Fail(string error)
    {
        return new PlcUnitResult(false, error, null);
    }
}
