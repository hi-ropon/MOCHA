using System;

namespace MOCHA.Models.Architecture;

/// <summary>
/// PLCユニットに紐づくモジュール
/// </summary>
public sealed class PlcUnitModule
{
    public PlcUnitModule(Guid id, string name, string? specification)
    {
        Id = id;
        Name = name;
        Specification = string.IsNullOrWhiteSpace(specification) ? null : specification.Trim();
    }

    public Guid Id { get; }
    public string Name { get; }
    public string? Specification { get; }

    public static PlcUnitModule FromDraft(PlcModuleDraft draft)
    {
        return new PlcUnitModule(Guid.NewGuid(), draft.Name.Trim(), draft.Specification);
    }
}
