using System;
using System.Collections.Generic;
using System.Linq;

namespace MOCHA.Models.Architecture;

/// <summary>
/// ユニット構成リクエスト
/// </summary>
public sealed class UnitConfigurationRequest
{
    /// <summary>エージェント番号</summary>
    public string AgentNumber { get; set; } = string.Empty;

    /// <summary>ユニット名</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>ユニット説明</summary>
    public string? Description { get; set; }

    /// <summary>構成機器</summary>
    public List<UnitDeviceRequest> Devices { get; set; } = new();

    /// <summary>
    /// 入力からドラフト生成
    /// </summary>
    /// <returns>ドラフト</returns>
    public UnitConfigurationDraft ToDraft()
    {
        return new UnitConfigurationDraft
        {
            Name = Name,
            Description = Description,
            Devices = Devices.Select(d => new UnitDeviceDraft
            {
                Name = d.Name ?? string.Empty,
                Model = string.IsNullOrWhiteSpace(d.Model) ? null : d.Model,
                Maker = string.IsNullOrWhiteSpace(d.Maker) ? null : d.Maker,
                Description = string.IsNullOrWhiteSpace(d.Description) ? null : d.Description
            }).ToList()
        };
    }
}

/// <summary>
/// ユニット構成リクエスト内の機器
/// </summary>
public sealed class UnitDeviceRequest
{
    /// <summary>機器名</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>型式</summary>
    public string? Model { get; set; }

    /// <summary>メーカー</summary>
    public string? Maker { get; set; }

    /// <summary>説明</summary>
    public string? Description { get; set; }
}

/// <summary>
/// ユニット構成レスポンス
/// </summary>
public sealed class UnitConfigurationResponse
{
    /// <summary>ユニットID</summary>
    public Guid Id { get; set; }

    /// <summary>ユニット名</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>ユニット説明</summary>
    public string? Description { get; set; }

    /// <summary>エージェント番号</summary>
    public string AgentNumber { get; set; } = string.Empty;

    /// <summary>作成日時</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>構成機器</summary>
    public List<UnitDeviceResponse> Devices { get; set; } = new();
}

/// <summary>
/// ユニット構成機器レスポンス
/// </summary>
public sealed class UnitDeviceResponse
{
    /// <summary>機器ID</summary>
    public Guid Id { get; set; }

    /// <summary>機器名</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>型式</summary>
    public string? Model { get; set; }

    /// <summary>メーカー</summary>
    public string? Maker { get; set; }

    /// <summary>説明</summary>
    public string? Description { get; set; }
}
