namespace MOCHA.Models.Architecture;

/// <summary>
/// 機器入力用ドラフト
/// </summary>
public sealed class UnitDeviceDraft
{
    /// <summary>機器名</summary>
    public string Name { get; init; } = string.Empty;
    /// <summary>型式</summary>
    public string? Model { get; init; }
    /// <summary>メーカー</summary>
    public string? Maker { get; init; }
    /// <summary>説明</summary>
    public string? Description { get; init; }

    /// <summary>
    /// 入力値検証
    /// </summary>
    /// <returns>検証結果</returns>
    public (bool IsValid, string? Error) Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            return (false, "機器名を入力してください");
        }

        var trimmedName = Name.Trim();
        if (trimmedName.Length > 100)
        {
            return (false, "機器名は100文字以内で入力してください");
        }

        if (!string.IsNullOrWhiteSpace(Model) && Model.Trim().Length > 100)
        {
            return (false, "型式は100文字以内で入力してください");
        }

        if (!string.IsNullOrWhiteSpace(Maker) && Maker.Trim().Length > 100)
        {
            return (false, "メーカーは100文字以内で入力してください");
        }

        if (!string.IsNullOrWhiteSpace(Description) && Description.Trim().Length > 500)
        {
            return (false, "説明は500文字以内で入力してください");
        }

        return (true, null);
    }
}
