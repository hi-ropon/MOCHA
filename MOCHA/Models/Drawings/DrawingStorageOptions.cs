using System;
using System.IO;

namespace MOCHA.Models.Drawings;

/// <summary>
/// 図面保存用のストレージ設定
/// </summary>
public sealed class DrawingStorageOptions
{
    /// <summary>保存ルートパス</summary>
    public string RootPath { get; set; } = Path.Combine(AppContext.BaseDirectory, "DrawingStorage");
}
