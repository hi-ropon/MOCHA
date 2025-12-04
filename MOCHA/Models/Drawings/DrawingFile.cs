using System;
using System.IO;

namespace MOCHA.Models.Drawings;

/// <summary>
/// 図面ドキュメントとファイル実体の参照
/// </summary>
public sealed class DrawingFile
{
    private DrawingFile(
        DrawingDocument document,
        string? fullPath,
        bool exists,
        string? storageRoot,
        string? relativePath)
    {
        Document = document ?? throw new ArgumentNullException(nameof(document));
        FullPath = string.IsNullOrWhiteSpace(fullPath) ? null : fullPath;
        Exists = exists && !string.IsNullOrWhiteSpace(fullPath);
        StorageRoot = string.IsNullOrWhiteSpace(storageRoot) ? null : storageRoot;
        RelativePath = string.IsNullOrWhiteSpace(relativePath) ? null : relativePath;
        Extension = Path.GetExtension(Document.FileName);
    }

    /// <summary>ドキュメント</summary>
    public DrawingDocument Document { get; }
    /// <summary>ファイル絶対パス</summary>
    public string? FullPath { get; }
    /// <summary>存在フラグ</summary>
    public bool Exists { get; }
    /// <summary>拡張子</summary>
    public string Extension { get; }
    /// <summary>保存ルート</summary>
    public string? StorageRoot { get; }
    /// <summary>保存相対パス</summary>
    public string? RelativePath { get; }

    /// <summary>
    /// 図面ファイル参照生成
    /// </summary>
    /// <param name="document">ドキュメント</param>
    /// <param name="fullPath">ファイル絶対パス</param>
    /// <param name="exists">存在フラグ</param>
    /// <param name="storageRoot">保存ルート</param>
    /// <param name="relativePath">保存相対パス</param>
    /// <returns>図面ファイル参照</returns>
    public static DrawingFile Create(
        DrawingDocument document,
        string? fullPath,
        bool exists,
        string? storageRoot = null,
        string? relativePath = null)
    {
        return new DrawingFile(document, fullPath, exists, storageRoot, relativePath);
    }
}
