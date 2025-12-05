using System;
using System.Text.Json.Serialization;

namespace MOCHA.Models.Architecture;

/// <summary>
/// PLCファンクションブロック
/// </summary>
public sealed class FunctionBlock
{
    /// <summary>ファンクションブロックID</summary>
    public Guid Id { get; }
    /// <summary>表示名</summary>
    public string Name { get; }
    /// <summary>ファイル保存用の安全な名前</summary>
    public string SafeName { get; }
    /// <summary>ラベルファイル</summary>
    public PlcFileUpload LabelFile { get; }
    /// <summary>プログラムファイル</summary>
    public PlcFileUpload ProgramFile { get; }
    /// <summary>作成日時</summary>
    public DateTimeOffset CreatedAt { get; }
    /// <summary>更新日時</summary>
    public DateTimeOffset UpdatedAt { get; }

    [JsonConstructor]
    internal FunctionBlock(
        Guid id,
        string name,
        string safeName,
        PlcFileUpload labelFile,
        PlcFileUpload programFile,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        Id = id;
        Name = name;
        SafeName = safeName;
        LabelFile = labelFile;
        ProgramFile = programFile;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    /// <summary>
    /// 新規作成
    /// </summary>
    /// <param name="name">表示名</param>
    /// <param name="safeName">保存用安全名</param>
    /// <param name="labelFile">ラベルファイル</param>
    /// <param name="programFile">プログラムファイル</param>
    /// <param name="createdAt">作成日時</param>
    /// <returns>生成したファンクションブロック</returns>
    public static FunctionBlock Create(
        string name,
        string safeName,
        PlcFileUpload labelFile,
        PlcFileUpload programFile,
        DateTimeOffset? createdAt = null)
    {
        var now = createdAt ?? DateTimeOffset.UtcNow;
        return new FunctionBlock(
            Guid.NewGuid(),
            name,
            safeName,
            labelFile,
            programFile,
            now,
            now);
    }

    /// <summary>
    /// 永続化情報から復元
    /// </summary>
    /// <param name="id">ID</param>
    /// <param name="name">表示名</param>
    /// <param name="safeName">保存用安全名</param>
    /// <param name="labelFile">ラベルファイル</param>
    /// <param name="programFile">プログラムファイル</param>
    /// <param name="createdAt">作成日時</param>
    /// <param name="updatedAt">更新日時</param>
    /// <returns>復元したファンクションブロック</returns>
    public static FunctionBlock Restore(
        Guid id,
        string name,
        string safeName,
        PlcFileUpload labelFile,
        PlcFileUpload programFile,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        return new FunctionBlock(id, name, safeName, labelFile, programFile, createdAt, updatedAt);
    }

    /// <summary>
    /// 名前やファイルを更新
    /// </summary>
    /// <param name="name">新しい表示名</param>
    /// <param name="safeName">新しい保存用安全名</param>
    /// <param name="labelFile">更新後ラベルファイル</param>
    /// <param name="programFile">更新後プログラムファイル</param>
    /// <returns>更新後のファンクションブロック</returns>
    public FunctionBlock Update(string name, string safeName, PlcFileUpload labelFile, PlcFileUpload programFile)
    {
        return new FunctionBlock(
            Id,
            name,
            safeName,
            labelFile,
            programFile,
            CreatedAt,
            DateTimeOffset.UtcNow);
    }
}