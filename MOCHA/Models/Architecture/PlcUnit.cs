using System;
using System.Collections.Generic;
using System.Linq;

namespace MOCHA.Models.Architecture;

/// <summary>
/// PLCユニットの設定を表す集約
/// </summary>
public sealed class PlcUnit
{
    /// <summary>
    /// ユニット初期化
    /// </summary>
    /// <param name="id">ユニットID</param>
    /// <param name="userId">ユーザーID</param>
    /// <param name="agentNumber">エージェント番号</param>
    /// <param name="name">ユニット名</param>
    /// <param name="manufacturer">メーカー</param>
    /// <param name="model">機種</param>
    /// <param name="role">役割</param>
    /// <param name="ipAddress">IPアドレス</param>
    /// <param name="port">ポート番号</param>
    /// <param name="transport">通信方式</param>
    /// <param name="commentFile">コメントファイル</param>
    /// <param name="programFiles">プログラムファイル</param>
    /// <param name="programDescription">プログラム構成説明</param>
    /// <param name="modules">モジュール一覧</param>
    /// <param name="functionBlocks">ファンクションブロック一覧</param>
    /// <param name="createdAt">作成日時</param>
    /// <param name="updatedAt">更新日時</param>
    private PlcUnit(
        Guid id,
        string userId,
        string agentNumber,
        string name,
        string manufacturer,
        string? model,
        string? role,
        string? ipAddress,
        int? port,
        string? transport,
        string? gatewayHost,
        int? gatewayPort,
        PlcFileUpload? commentFile,
        IReadOnlyCollection<PlcFileUpload> programFiles,
        string? programDescription,
        IReadOnlyCollection<PlcUnitModule> modules,
        IReadOnlyCollection<FunctionBlock> functionBlocks,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        Id = id;
        UserId = userId;
        AgentNumber = agentNumber;
        Name = name;
        Manufacturer = manufacturer;
        Model = model;
        Role = role;
        IpAddress = ipAddress;
        Port = port;
        Transport = NormalizeTransport(transport);
        GatewayHost = gatewayHost;
        GatewayPort = gatewayPort;
        CommentFile = commentFile;
        ProgramFiles = programFiles;
        ProgramDescription = programDescription;
        Modules = modules;
        FunctionBlocks = functionBlocks;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    /// <summary>ユニットID</summary>
    public Guid Id { get; }
    /// <summary>ユーザーID</summary>
    public string UserId { get; }
    /// <summary>エージェント番号</summary>
    public string AgentNumber { get; }
    /// <summary>ユニット名</summary>
    public string Name { get; }
    /// <summary>メーカー</summary>
    public string Manufacturer { get; }
    /// <summary>機種</summary>
    public string? Model { get; }
    /// <summary>役割</summary>
    public string? Role { get; }
    /// <summary>IPアドレス</summary>
    public string? IpAddress { get; }
    /// <summary>ポート番号</summary>
    public int? Port { get; }
    /// <summary>通信方式</summary>
    public string Transport { get; }
    /// <summary>ゲートウェイIP</summary>
    public string? GatewayHost { get; }
    /// <summary>ゲートウェイポート</summary>
    public int? GatewayPort { get; }
    /// <summary>コメントファイル</summary>
    public PlcFileUpload? CommentFile { get; }
    /// <summary>プログラムファイル</summary>
    public IReadOnlyCollection<PlcFileUpload> ProgramFiles { get; }
    /// <summary>プログラム構成説明</summary>
    public string? ProgramDescription { get; }
    /// <summary>モジュール一覧</summary>
    public IReadOnlyCollection<PlcUnitModule> Modules { get; }
    /// <summary>ファンクションブロック一覧</summary>
    public IReadOnlyCollection<FunctionBlock> FunctionBlocks { get; }
    /// <summary>作成日時</summary>
    public DateTimeOffset CreatedAt { get; }
    /// <summary>更新日時</summary>
    public DateTimeOffset UpdatedAt { get; }

    /// <summary>
    /// ドラフトからユニット生成
    /// </summary>
    /// <param name="userId">ユーザーID</param>
    /// <param name="agentNumber">エージェント番号</param>
    /// <param name="draft">ユニットドラフト</param>
    /// <param name="createdAt">作成日時</param>
    /// <returns>生成したユニット</returns>
    public static PlcUnit Create(string userId, string agentNumber, PlcUnitDraft draft, DateTimeOffset? createdAt = null)
    {
        var timestamp = createdAt ?? DateTimeOffset.UtcNow;
        return new PlcUnit(
            Guid.NewGuid(),
            userId,
            agentNumber,
            draft.Name.Trim(),
            NormalizeRequired(draft.Manufacturer),
            NormalizeNullable(draft.Model),
            NormalizeNullable(draft.Role),
            NormalizeNullable(draft.IpAddress),
            draft.Port,
            NormalizeTransport(draft.Transport),
            NormalizeNullable(draft.GatewayHost),
            draft.GatewayPort,
            NormalizeFile(draft.CommentFile),
            NormalizeFiles(draft.ProgramFiles ?? Array.Empty<PlcFileUpload>()),
            NormalizeNullable(draft.ProgramDescription),
            draft.Modules.Select(PlcUnitModule.FromDraft).ToList(),
            Array.Empty<FunctionBlock>(),
            timestamp,
            timestamp);
    }

    /// <summary>
    /// 永続化情報からユニットを復元
    /// </summary>
    /// <param name="id">ユニットID</param>
    /// <param name="userId">ユーザーID</param>
    /// <param name="agentNumber">エージェント番号</param>
    /// <param name="name">ユニット名</param>
    /// <param name="manufacturer">メーカー</param>
    /// <param name="model">機種</param>
    /// <param name="role">役割</param>
    /// <param name="ipAddress">IPアドレス</param>
    /// <param name="port">ポート番号</param>
    /// <param name="transport">通信方式</param>
    /// <param name="commentFile">コメントファイル</param>
    /// <param name="programFiles">プログラムファイル</param>
    /// <param name="programDescription">プログラム構成説明</param>
    /// <param name="modules">モジュール</param>
    /// <param name="createdAt">作成日時</param>
    /// <param name="updatedAt">更新日時</param>
    /// <returns>復元したユニット</returns>
    public static PlcUnit Restore(
        Guid id,
        string userId,
        string agentNumber,
        string name,
        string manufacturer,
        string? model,
        string? role,
        string? ipAddress,
        int? port,
        string? transport,
        string? gatewayHost,
        int? gatewayPort,
        PlcFileUpload? commentFile,
        IReadOnlyCollection<PlcFileUpload> programFiles,
        string? programDescription,
        IReadOnlyCollection<PlcUnitModule> modules,
        IReadOnlyCollection<FunctionBlock> functionBlocks,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        return new PlcUnit(
            id,
            userId,
            agentNumber,
            name,
            NormalizeRequired(manufacturer),
            model,
            role,
            ipAddress,
            port,
            transport,
            gatewayHost,
            gatewayPort,
            commentFile,
            programFiles ?? Array.Empty<PlcFileUpload>(),
            NormalizeNullable(programDescription),
            modules ?? Array.Empty<PlcUnitModule>(),
            functionBlocks ?? Array.Empty<FunctionBlock>(),
            createdAt,
            updatedAt);
    }

    /// <summary>
    /// ドラフトでユニットを更新
    /// </summary>
    /// <param name="draft">更新内容</param>
    /// <returns>更新後ユニット</returns>
    public PlcUnit Update(PlcUnitDraft draft)
    {
        return new PlcUnit(
            Id,
            UserId,
            AgentNumber,
            draft.Name.Trim(),
            NormalizeRequired(draft.Manufacturer),
            NormalizeNullable(draft.Model),
            NormalizeNullable(draft.Role),
            NormalizeNullable(draft.IpAddress),
            draft.Port,
            NormalizeTransport(draft.Transport),
            NormalizeNullable(draft.GatewayHost),
            draft.GatewayPort,
            NormalizeFile(draft.CommentFile) ?? CommentFile,
            NormalizeFiles(draft.ProgramFiles ?? Array.Empty<PlcFileUpload>()),
            NormalizeNullable(draft.ProgramDescription),
            draft.Modules.Select(PlcUnitModule.FromDraft).ToList(),
            FunctionBlocks,
            CreatedAt,
            DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// ファンクションブロックを差し替え
    /// </summary>
    /// <param name="functionBlocks">差し替え後一覧</param>
    /// <returns>更新後ユニット</returns>
    public PlcUnit WithFunctionBlocks(IReadOnlyCollection<FunctionBlock> functionBlocks)
    {
        return new PlcUnit(
            Id,
            UserId,
            AgentNumber,
            Name,
            Manufacturer,
            Model,
            Role,
            IpAddress,
            Port,
            Transport,
            GatewayHost,
            GatewayPort,
            CommentFile,
            ProgramFiles,
            ProgramDescription,
            Modules,
            functionBlocks,
            CreatedAt,
            DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// 文字列の正規化（空白なら null）
    /// </summary>
    /// <param name="value">入力値</param>
    /// <returns>正規化された文字列</returns>
    private static string? NormalizeNullable(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string NormalizeRequired(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static string NormalizeTransport(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "tcp"
            : value.Trim().ToLowerInvariant();
    }

    private static PlcFileUpload? NormalizeFile(PlcFileUpload? file)
    {
        if (file is null)
        {
            return null;
        }

        return new PlcFileUpload
        {
            Id = file.Id == Guid.Empty ? Guid.NewGuid() : file.Id,
            FileName = file.FileName.Trim(),
            ContentType = file.ContentType,
            FileSize = file.FileSize,
            DisplayName = string.IsNullOrWhiteSpace(file.DisplayName) ? file.FileName.Trim() : file.DisplayName.Trim(),
            RelativePath = string.IsNullOrWhiteSpace(file.RelativePath) ? null : file.RelativePath.Trim(),
            StorageRoot = string.IsNullOrWhiteSpace(file.StorageRoot) ? null : file.StorageRoot.Trim(),
            Content = null
        };
    }

    private static IReadOnlyCollection<PlcFileUpload> NormalizeFiles(IEnumerable<PlcFileUpload> files)
    {
        return files.Select(NormalizeFile)
                    .Where(f => f is not null)!
                    .Cast<PlcFileUpload>()
                    .ToList();
    }
}
