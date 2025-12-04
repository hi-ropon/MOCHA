namespace MOCHA.Models.Architecture;

/// <summary>
/// PLCファイル保存先のメタ情報
/// </summary>
public sealed class PlcFileStoragePath
{
    /// <summary>
    /// 保存パス初期化
    /// </summary>
    /// <param name="rootPath">ルートパス</param>
    /// <param name="relativePath">ルートからの相対パス</param>
    /// <param name="directoryPath">ディレクトリパス</param>
    /// <param name="storedFileName">保存ファイル名</param>
    /// <param name="fullPath">フルパス</param>
    public PlcFileStoragePath(string rootPath, string relativePath, string directoryPath, string storedFileName, string fullPath)
    {
        RootPath = rootPath;
        RelativePath = relativePath;
        DirectoryPath = directoryPath;
        StoredFileName = storedFileName;
        FullPath = fullPath;
    }

    /// <summary>ルートパス</summary>
    public string RootPath { get; }
    /// <summary>ルートからの相対パス</summary>
    public string RelativePath { get; }
    /// <summary>保存ディレクトリ</summary>
    public string DirectoryPath { get; }
    /// <summary>保存時ファイル名</summary>
    public string StoredFileName { get; }
    /// <summary>フルパス</summary>
    public string FullPath { get; }
}
