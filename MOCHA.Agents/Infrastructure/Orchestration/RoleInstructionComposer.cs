using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MOCHA.Agents.Infrastructure.Orchestration;

/// <summary>
/// ロールに応じてベーステンプレートへ追記を行うコンポーザー
/// </summary>
public static class RoleInstructionComposer
{
    /// <summary>
    /// ベーステンプレートとロール一覧から指示を合成
    /// </summary>
    /// <param name="baseTemplate">ベーステンプレート</param>
    /// <param name="roleValues">ロール一覧</param>
    /// <returns>合成したテンプレート</returns>
    public static string Compose(string? baseTemplate, IReadOnlyCollection<string> roleValues)
    {
        var template = string.IsNullOrWhiteSpace(baseTemplate) ? OrganizerInstructions.Base : baseTemplate;
        if (roleValues is null || roleValues.Count == 0)
        {
            return template;
        }

        var roles = roleValues.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim()).ToArray();
        if (roles.Length == 0)
        {
            return template;
        }

        var builder = new StringBuilder(template);

        if (roles.Any(IsAdministrator))
        {
            builder.Append(OrganizerInstructions.AdministratorAppendix);
        }

        if (roles.Any(IsDeveloper))
        {
            builder.Append(OrganizerInstructions.DeveloperAppendix);
        }

        return builder.ToString();
    }

    private static bool IsAdministrator(string role) =>
        role.Equals("Administrator", StringComparison.OrdinalIgnoreCase);

    private static bool IsDeveloper(string role) =>
        role.Equals("Developer", StringComparison.OrdinalIgnoreCase);
}
