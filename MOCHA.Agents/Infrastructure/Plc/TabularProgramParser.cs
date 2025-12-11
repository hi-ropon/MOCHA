using System.Collections.Generic;
using System.Text;
using MOCHA.Agents.Domain.Plc;

namespace MOCHA.Agents.Infrastructure.Plc;

/// <summary>
/// タブ付きCSVの行を ProgramLine に変換するパーサ
/// </summary>
public sealed class TabularProgramParser : ITabularProgramParser
{
    /// <inheritdoc />
    public ProgramLine Parse(string? line)
    {
        if (line is null)
        {
            return new ProgramLine(string.Empty, System.Array.Empty<string>());
        }

        var columns = Tokenize(line);
        return new ProgramLine(line, columns);
    }

    private static IReadOnlyList<string> Tokenize(string line)
    {
        var values = new List<string>();
        var buffer = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    buffer.Append('"');
                    i++;
                    continue;
                }

                inQuotes = !inQuotes;
                continue;
            }

            if (!inQuotes && (ch == '\t' || ch == ','))
            {
                values.Add(buffer.ToString());
                buffer.Clear();
                continue;
            }

            if (ch is '\r' or '\n')
            {
                continue;
            }

            buffer.Append(ch);
        }

        values.Add(buffer.ToString());
        return values;
    }
}
