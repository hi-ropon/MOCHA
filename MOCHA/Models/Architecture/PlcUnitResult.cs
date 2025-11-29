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

    public bool Succeeded { get; }
    public string? Error { get; }
    public PlcUnit? Unit { get; }

    public static PlcUnitResult Success(PlcUnit unit)
    {
        return new PlcUnitResult(true, null, unit);
    }

    public static PlcUnitResult Fail(string error)
    {
        return new PlcUnitResult(false, error, null);
    }
}
