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
    /// コメント取得
    /// </summary>
    bool TryGetComment(string device, out string? comment);

    /// <summary>
    /// プログラムコレクション
    /// </summary>
    IReadOnlyDictionary<string, IReadOnlyList<string>> Programs { get; }
}
