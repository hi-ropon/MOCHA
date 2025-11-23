namespace MOCHA.Models.Agents;

/// <summary>
/// 装置エージェントの識別情報と作成日時を保持するドメインモデル。
/// </summary>
public sealed class DeviceAgentProfile
{
    /// <summary>
    /// エージェント番号・名称・作成日時を指定して初期化する。
    /// </summary>
    /// <param name="number">装置エージェントの番号。</param>
    /// <param name="name">装置エージェントの表示名。</param>
    /// <param name="createdAt">登録日時。</param>
    public DeviceAgentProfile(string number, string name, DateTimeOffset createdAt)
    {
        Number = number;
        Name = name;
        CreatedAt = createdAt;
    }

    /// <summary>
    /// 装置エージェントの番号。
    /// </summary>
    public string Number { get; set; }
    /// <summary>
    /// 装置エージェントの表示名。
    /// </summary>
    public string Name { get; set; }
    /// <summary>
    /// 登録日時。
    /// </summary>
    public DateTimeOffset CreatedAt { get; }
}
