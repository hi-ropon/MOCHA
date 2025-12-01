namespace MOCHA.Services.Agents;

/// <summary>
/// 装置エージェントの永続化用エンティティ
/// </summary>
internal sealed class DeviceAgentEntity
{
    /// <summary>
    /// 主キー
    /// </summary>
    public int Id { get; set; }
    /// <summary>
    /// ユーザーID
    /// </summary>
    public string UserObjectId { get; set; } = default!;
    /// <summary>
    /// エージェント番号
    /// </summary>
    public string Number { get; set; } = string.Empty;
    /// <summary>
    /// エージェント名
    /// </summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>
    /// 登録日時
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }
}
