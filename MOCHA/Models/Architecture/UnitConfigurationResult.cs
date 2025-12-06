namespace MOCHA.Models.Architecture;

/// <summary>
/// 装置ユニット構成の結果モデル
/// </summary>
public sealed class UnitConfigurationResult
{
    private UnitConfigurationResult(bool succeeded, UnitConfiguration? unit, string? error)
    {
        Succeeded = succeeded;
        Unit = unit;
        Error = error;
    }

    /// <summary>成功フラグ</summary>
    public bool Succeeded { get; }
    /// <summary>ユニット構成</summary>
    public UnitConfiguration? Unit { get; }
    /// <summary>エラーメッセージ</summary>
    public string? Error { get; }

    /// <summary>
    /// 成功結果生成
    /// </summary>
    /// <param name="unit">ユニット構成</param>
    /// <returns>結果</returns>
    public static UnitConfigurationResult Success(UnitConfiguration unit) => new(true, unit, null);

    /// <summary>
    /// 失敗結果生成
    /// </summary>
    /// <param name="error">エラー</param>
    /// <returns>結果</returns>
    public static UnitConfigurationResult Fail(string error) => new(false, null, error);
}
