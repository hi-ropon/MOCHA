using System;

namespace MOCHA.Models.Architecture;

/// <summary>
/// ユニット内の機器
/// </summary>
public sealed class UnitDevice
{
    private UnitDevice(Guid id, string name, string? model, string? maker, string? description, int order)
    {
        Id = id;
        Name = name;
        Model = model;
        Maker = maker;
        Description = description;
        Order = order;
    }

    /// <summary>機器ID</summary>
    public Guid Id { get; }
    /// <summary>機器名</summary>
    public string Name { get; }
    /// <summary>型式</summary>
    public string? Model { get; }
    /// <summary>メーカー</summary>
    public string? Maker { get; }
    /// <summary>説明</summary>
    public string? Description { get; }
    /// <summary>並び順</summary>
    public int Order { get; }

    /// <summary>
    /// ドラフトから生成
    /// </summary>
    /// <param name="draft">入力ドラフト</param>
    /// <param name="order">並び順</param>
    /// <returns>機器</returns>
    public static UnitDevice FromDraft(UnitDeviceDraft draft, int order)
    {
        return new UnitDevice(
            Guid.NewGuid(),
            NormalizeRequired(draft.Name),
            NormalizeOptional(draft.Model),
            NormalizeOptional(draft.Maker),
            NormalizeOptional(draft.Description),
            order);
    }

    /// <summary>
    /// 永続化情報から復元
    /// </summary>
    /// <param name="id">機器ID</param>
    /// <param name="name">機器名</param>
    /// <param name="model">型式</param>
    /// <param name="maker">メーカー</param>
    /// <param name="description">説明</param>
    /// <param name="order">並び順</param>
    /// <returns>機器</returns>
    public static UnitDevice Restore(Guid id, string name, string? model, string? maker, string? description, int order)
    {
        return new UnitDevice(
            id == Guid.Empty ? Guid.NewGuid() : id,
            NormalizeRequired(name),
            NormalizeOptional(model),
            NormalizeOptional(maker),
            NormalizeOptional(description),
            order);
    }

    private static string NormalizeRequired(string value)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException("値が空です", nameof(value));
        }

        return normalized;
    }

    private static string? NormalizeOptional(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
}
