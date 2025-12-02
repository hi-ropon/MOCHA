using System.Collections.Generic;

namespace MOCHA.Agents.Domain.Plc;

/// <summary>
/// PLCデバイス読み取り結果
/// </summary>
public sealed record DeviceReadResult(
    string Device,
    IReadOnlyList<int>? Values,
    bool Success,
    string? Error);
