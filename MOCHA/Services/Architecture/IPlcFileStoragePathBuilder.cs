using MOCHA.Models.Architecture;

namespace MOCHA.Services.Architecture;

/// <summary>
/// PLC関連ファイルの保存パスを生成するビルダー
/// </summary>
internal interface IPlcFileStoragePathBuilder
{
    /// <summary>
    /// 保存パスを生成する
    /// </summary>
    /// <param name="agentNumber">エージェント番号</param>
    /// <param name="fileName">ファイル名</param>
    /// <param name="category">ファイル種別</param>
    /// <returns>保存パス</returns>
    PlcFileStoragePath Build(string agentNumber, string fileName, string category);
}
