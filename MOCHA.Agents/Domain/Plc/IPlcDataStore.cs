using System.Collections.Generic;

namespace MOCHA.Agents.Domain.Plc;

/// <summary>
/// PLCコメント・プログラムを保持するストア
/// </summary>
public interface IPlcDataStore
{
    /// <summary>
    /// データクリア
    /// </summary>
    void Clear();

    /// <summary>
    /// コメントを設定
    /// </summary>
    void SetComments(IDictionary<string, string> comments);

    /// <summary>
    /// プログラムを設定
    /// </summary>
    void SetPrograms(IEnumerable<ProgramFile> programs);

    /// <summary>
    /// ファンクションブロックを設定
    /// </summary>
    void SetFunctionBlocks(IEnumerable<FunctionBlockData> blocks);

    /// <summary>
    /// コメント取得
    /// </summary>
    bool TryGetComment(string device, out string? comment);

    /// <summary>
    /// コメントコレクション
    /// </summary>
    IReadOnlyDictionary<string, string> Comments { get; }

    /// <summary>
    /// プログラムコレクション
    /// </summary>
    IReadOnlyDictionary<string, IReadOnlyList<ProgramLine>> Programs { get; }

    /// <summary>
    /// ファンクションブロック一覧
    /// </summary>
    IReadOnlyCollection<FunctionBlockData> FunctionBlocks { get; }

    /// <summary>
    /// ファンクションブロック取得
    /// </summary>
    bool TryGetFunctionBlock(string name, out FunctionBlockData? block);
}
