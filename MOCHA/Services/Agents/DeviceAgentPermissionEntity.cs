namespace MOCHA.Services.Agents;

/// <summary>
/// 装置エージェント利用許可の永続化エンティティ
/// </summary>
internal sealed class DeviceAgentPermissionEntity
{
    /// <summary>
    /// 主キー
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// ユーザーID
    /// </summary>
    public string UserObjectId { get; set; } = string.Empty;

    /// <summary>
    /// 許可した装置エージェント番号
    /// </summary>
    public string AgentNumber { get; set; } = string.Empty;

    /// <summary>
    /// 割り付け日時
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }
}
