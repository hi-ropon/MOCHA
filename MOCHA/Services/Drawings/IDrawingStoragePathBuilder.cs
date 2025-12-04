using MOCHA.Models.Drawings;

namespace MOCHA.Services.Drawings;

/// <summary>
/// 図面ファイル保存パスを生成するビルダー
/// </summary>
public interface IDrawingStoragePathBuilder
{
    /// <summary>
    /// 保存先パスを生成する
    /// </summary>
    /// <param name="agentNumber">装置エージェント番号</param>
    /// <param name="fileName">元ファイル名</param>
    /// <returns>構築したパス</returns>
    DrawingStoragePath Build(string agentNumber, string fileName);
}
