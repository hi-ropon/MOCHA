using System.Collections.Generic;

namespace MOCHA.Agents.Domain.Plc;

/// <summary>
/// PLCバッチ読み取り結果
/// </summary>
public sealed record BatchReadResult(
    IReadOnlyList<DeviceReadResult> Results,
    string? Error = null);
