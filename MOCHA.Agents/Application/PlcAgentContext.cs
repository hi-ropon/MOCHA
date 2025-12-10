using System;
using System.Collections.Generic;
using System.Linq;

namespace MOCHA.Agents.Application;

/// <summary>
/// PLCエージェント向け接続コンテキスト
/// </summary>
public sealed class PlcAgentContext
{
    /// <summary>ゲートウェイホスト</summary>
    public string? GatewayHost { get; }

    /// <summary>ゲートウェイポート</summary>
    public int? GatewayPort { get; }

    /// <summary>PLCユニット一覧</summary>
    public IReadOnlyList<PlcAgentUnit> Units { get; }

    /// <summary>
    /// 新しいコンテキストを初期化
    /// </summary>
    /// <param name="gatewayHost">ゲートウェイホスト</param>
    /// <param name="gatewayPort">ゲートウェイポート</param>
    /// <param name="units">ユニット一覧</param>
    public PlcAgentContext(string? gatewayHost = null, int? gatewayPort = null, IReadOnlyCollection<PlcAgentUnit>? units = null)
    {
        GatewayHost = Normalize(gatewayHost);
        GatewayPort = gatewayPort;
        Units = units?.Where(u => u is not null).ToList() ?? new List<PlcAgentUnit>();
    }

    /// <summary>空コンテキスト</summary>
    public static PlcAgentContext Empty { get; } = new();

    /// <summary>空判定</summary>
    public bool IsEmpty => string.IsNullOrWhiteSpace(GatewayHost) && (Units.Count == 0 || Units.All(u => string.IsNullOrWhiteSpace(u.IpAddress)));

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}

/// <summary>
/// PLCユニットの接続情報
/// </summary>
public sealed class PlcAgentUnit
{
    /// <summary>ユニットID</summary>
    public Guid Id { get; }

    /// <summary>ユニット名</summary>
    public string Name { get; }

    /// <summary>ユニットIP</summary>
    public string? IpAddress { get; }

    /// <summary>ユニットポート</summary>
    public int? Port { get; }

    /// <summary>ユニットに紐づくゲートウェイホスト</summary>
    public string? GatewayHost { get; }

    /// <summary>ユニットに紐づくゲートウェイポート</summary>
    public int? GatewayPort { get; }

    /// <summary>
    /// 新しいユニットを初期化
    /// </summary>
    /// <param name="id">ユニットID</param>
    /// <param name="name">ユニット名</param>
    /// <param name="ipAddress">IPアドレス</param>
    /// <param name="port">ポート</param>
    /// <param name="gatewayHost">ゲートウェイホスト</param>
    /// <param name="gatewayPort">ゲートウェイポート</param>
    public PlcAgentUnit(Guid id, string name, string? ipAddress, int? port, string? gatewayHost, int? gatewayPort)
    {
        Id = id;
        Name = string.IsNullOrWhiteSpace(name) ? string.Empty : name.Trim();
        IpAddress = string.IsNullOrWhiteSpace(ipAddress) ? null : ipAddress.Trim();
        Port = port;
        GatewayHost = string.IsNullOrWhiteSpace(gatewayHost) ? null : gatewayHost.Trim();
        GatewayPort = gatewayPort;
    }
}
