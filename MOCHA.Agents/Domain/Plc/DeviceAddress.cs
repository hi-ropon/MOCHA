using System;
using System.Linq;

namespace MOCHA.Agents.Domain.Plc;

/// <summary>
/// PLCデバイスアドレス値オブジェクト
/// </summary>
public sealed class DeviceAddress
{
    private DeviceAddress(string device, string address, int length)
    {
        Device = device;
        Address = address;
        Length = length > 0 ? length : 1;
    }

    /// <summary>
    /// デバイス種別
    /// </summary>
    public string Device { get; }

    /// <summary>
    /// アドレス
    /// </summary>
    public string Address { get; }

    /// <summary>
    /// 読み取り長
    /// </summary>
    public int Length { get; }

    /// <summary>
    /// デバイス種別とアドレスの結合表記
    /// </summary>
    public string Display => $"{Device}{Address}";

    /// <summary>
    /// ゲートウェイ向けSpec文字列
    /// </summary>
    /// <returns>正規化済みSpec</returns>
    public string ToSpec()
    {
        return Length > 1 ? $"{Display}:{Length}" : Display;
    }

    /// <summary>
    /// デバイス指定の解析
    /// </summary>
    /// <param name="spec">デバイス指定</param>
    /// <returns>デバイスアドレス</returns>
    public static DeviceAddress Parse(string spec)
    {
        if (string.IsNullOrWhiteSpace(spec))
        {
            return new DeviceAddress("D", "0", 1);
        }

        var span = spec.AsSpan().Trim();
        var length = 1;
        var colon = span.IndexOf(':');
        if (colon >= 0 && int.TryParse(span[(colon + 1)..], out var parsedLength) && parsedLength > 0)
        {
            length = parsedLength;
            span = span[..colon];
        }

        var core = span.ToString();
        if (string.IsNullOrWhiteSpace(core))
        {
            return new DeviceAddress("D", "0", length);
        }

        var device = _devicePrefixes.FirstOrDefault(prefix => core.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                     ?? core[0].ToString().ToUpperInvariant();
        var address = core.Length > device.Length ? core.Substring(device.Length) : "0";
        if (string.IsNullOrWhiteSpace(address))
        {
            address = "0";
        }

        var normalizedDevice = NormalizeDevice(device);
        return new DeviceAddress(normalizedDevice, address, length);
    }

    private static string NormalizeDevice(string device)
    {
        if (string.Equals(device, "T", StringComparison.OrdinalIgnoreCase))
        {
            return "TS";
        }

        return device.ToUpperInvariant();
    }

    private static readonly string[] _devicePrefixes = { "ZR", "TS", "D", "W", "R", "X", "Y", "M", "L", "B" };
}
