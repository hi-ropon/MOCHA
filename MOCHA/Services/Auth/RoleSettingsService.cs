using System;
using System.Collections.Generic;
using System.Linq;

namespace MOCHA.Services.Auth
{
    public static class RoleSettingsService
    {
        public static string? NormalizeUserId(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return null;
            }

            return input.Trim().ToLowerInvariant();
        }

        public static (IReadOnlyList<string> ToAssign, IReadOnlyList<string> ToRemove) CalculateDiff(IEnumerable<string> currentRoles, IEnumerable<string> selectedRoles)
        {
            var current = new HashSet<string>(currentRoles ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            var selected = new HashSet<string>(selectedRoles ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);

            var toAssign = selected.Except(current).ToList();
            var toRemove = current.Except(selected).ToList();
            return (toAssign, toRemove);
        }

        public static HashSet<string> Toggle(HashSet<string> selectedRoles, string roleId, bool isChecked)
        {
            var result = new HashSet<string>(selectedRoles ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);
            if (isChecked)
            {
                result.Add(roleId);
            }
            else
            {
                result.Remove(roleId);
            }

            return result;
        }
    }
}
