using System;
using System.Collections.Generic;
using System.Linq;

namespace MOCHA.Models.Architecture;

/// <summary>
/// PLCユニットの設定を表す集約
/// </summary>
public sealed class PlcUnit
{
    private PlcUnit(
        Guid id,
        string userId,
        string agentNumber,
        string name,
        string? model,
        string? role,
        string? ipAddress,
        PlcFileUpload? commentFile,
        PlcFileUpload? programFile,
        IReadOnlyCollection<PlcUnitModule> modules,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        Id = id;
        UserId = userId;
        AgentNumber = agentNumber;
        Name = name;
        Model = model;
        Role = role;
        IpAddress = ipAddress;
        CommentFile = commentFile;
        ProgramFile = programFile;
        Modules = modules;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public Guid Id { get; }
    public string UserId { get; }
    public string AgentNumber { get; }
    public string Name { get; }
    public string? Model { get; }
    public string? Role { get; }
    public string? IpAddress { get; }
    public PlcFileUpload? CommentFile { get; }
    public PlcFileUpload? ProgramFile { get; }
    public IReadOnlyCollection<PlcUnitModule> Modules { get; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; }

    public static PlcUnit Create(string userId, string agentNumber, PlcUnitDraft draft, DateTimeOffset? createdAt = null)
    {
        var timestamp = createdAt ?? DateTimeOffset.UtcNow;
        return new PlcUnit(
            Guid.NewGuid(),
            userId,
            agentNumber,
            draft.Name.Trim(),
            NormalizeNullable(draft.Model),
            NormalizeNullable(draft.Role),
            NormalizeNullable(draft.IpAddress),
            draft.CommentFile,
            draft.ProgramFile,
            draft.Modules.Select(PlcUnitModule.FromDraft).ToList(),
            timestamp,
            timestamp);
    }

    public PlcUnit Update(PlcUnitDraft draft)
    {
        return new PlcUnit(
            Id,
            UserId,
            AgentNumber,
            draft.Name.Trim(),
            NormalizeNullable(draft.Model),
            NormalizeNullable(draft.Role),
            NormalizeNullable(draft.IpAddress),
            draft.CommentFile ?? CommentFile,
            draft.ProgramFile ?? ProgramFile,
            draft.Modules.Select(PlcUnitModule.FromDraft).ToList(),
            CreatedAt,
            DateTimeOffset.UtcNow);
    }

    private static string? NormalizeNullable(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
