using System;
using System.Collections.Generic;

namespace MOCHA.Agents.Domain.Plc;

/// <summary>
/// PLCバッチ読み取り要求
/// </summary>
public sealed record BatchReadRequest(
    IReadOnlyList<string> Specs,
    string? Ip = null,
    int? Port = null,
    string? Transport = null,
    TimeSpan? Timeout = null,
    string? BaseUrl = null);
