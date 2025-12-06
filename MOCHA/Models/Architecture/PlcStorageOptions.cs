using System;
using System.IO;

namespace MOCHA.Models.Architecture;

/// <summary>
/// PLC関連ファイルの保存先設定
/// </summary>
public sealed class PlcStorageOptions
{
    /// <summary>保存ルートパス</summary>
    public string RootPath { get; set; } = Path.Combine(AppContext.BaseDirectory, "PlcStorage");
}
