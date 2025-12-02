using System;

namespace MOCHA.Agents.Domain.Plc;

/// <summary>
/// PLCデバイス読み取り要求
/// </summary>
public sealed record DeviceReadRequest(
    string Spec,
    string? Ip = null,
    int? Port = null,
    TimeSpan? Timeout = null,
    string? BaseUrl = null);
