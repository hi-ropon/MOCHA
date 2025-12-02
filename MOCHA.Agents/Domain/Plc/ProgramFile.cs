using System.Collections.Generic;

namespace MOCHA.Agents.Domain.Plc;

/// <summary>
/// PLCプログラムファイル
/// </summary>
public sealed record ProgramFile(string Name, IReadOnlyList<string> Lines);
