namespace MOCHA.Models.Drawings;

/// <summary>
/// 図面ファイルの保存場所を表す値オブジェクト
/// </summary>
public sealed class DrawingStoragePath
{
    /// <summary>
    /// 保存場所を初期化する
    /// </summary>
    /// <param name="rootPath">保存ルート</param>
    /// <param name="relativePath">ルートからの相対パス</param>
    /// <param name="directoryPath">ディレクトリの絶対パス</param>
    /// <param name="fileName">保存ファイル名</param>
    /// <param name="fullPath">ファイルの絶対パス</param>
    public DrawingStoragePath(
        string rootPath,
        string relativePath,
        string directoryPath,
        string fileName,
        string fullPath)
    {
        RootPath = rootPath;
        RelativePath = relativePath;
        DirectoryPath = directoryPath;
        FileName = fileName;
        FullPath = fullPath;
    }

    /// <summary>保存ルート</summary>
    public string RootPath { get; }
    /// <summary>ルートからの相対パス</summary>
    public string RelativePath { get; }
    /// <summary>ディレクトリ絶対パス</summary>
    public string DirectoryPath { get; }
    /// <summary>保存ファイル名</summary>
    public string FileName { get; }
    /// <summary>ファイル絶対パス</summary>
    public string FullPath { get; }
}
