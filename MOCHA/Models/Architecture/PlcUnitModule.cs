using System;

namespace MOCHA.Models.Architecture;

/// <summary>
/// PLCユニットに紐づくモジュール
/// </summary>
public sealed class PlcUnitModule
{
    /// <summary>
    /// モジュール初期化
    /// </summary>
    /// <param name="id">モジュールID</param>
    /// <param name="name">名称</param>
    /// <param name="specification">仕様</param>
    public PlcUnitModule(Guid id, string name, string? specification)
    {
        Id = id;
        Name = name;
        Specification = string.IsNullOrWhiteSpace(specification) ? null : specification.Trim();
    }

    /// <summary>モジュールID</summary>
    public Guid Id { get; }
    /// <summary>モジュール名称</summary>
    public string Name { get; }
    /// <summary>仕様</summary>
    public string? Specification { get; }

    /// <summary>
    /// ドラフトからモジュール生成
    /// </summary>
    /// <param name="draft">モジュールドラフト</param>
    /// <returns>生成したモジュール</returns>
    public static PlcUnitModule FromDraft(PlcModuleDraft draft)
    {
        return new PlcUnitModule(Guid.NewGuid(), draft.Name.Trim(), draft.Specification);
    }
}
