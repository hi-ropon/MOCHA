namespace MOCHA.Agents.Domain.Plc;

/// <summary>
/// PLCデバイスのデータ型
/// </summary>
public enum DeviceDataType
{
    Unknown = 0,
    Bit = 1,
    Word = 2,
    DoubleWord = 3,
    Float = 4
}
