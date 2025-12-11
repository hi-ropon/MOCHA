namespace MOCHA.Agents.Domain.Plc;

/// <summary>
/// タブ/CSV形式のプログラム行を解析するパーサ
/// </summary>
public interface ITabularProgramParser
{
    /// <summary>
    /// 単一行を解析
    /// </summary>
    /// <param name="line">入力行</param>
    /// <returns>分解済みプログラム行</returns>
    ProgramLine Parse(string? line);
}
