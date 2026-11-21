using ModernTinyWall.Exceptions;
using System;
using System.Globalization;

namespace ModernTinyWall.Models;

internal static class ExceptionHealthAnalyzer
{
    public static string GetFinding(ExceptionRow row, bool duplicateSubject)
    {
        var findings = new System.Collections.Generic.List<string>();
        if (duplicateSubject)
            findings.Add("Multiple rules apply to the same subject; verify their combined effect.");
        if (row.SubjectType == "Global")
            findings.Add("This rule applies globally; review its scope carefully.");
        if (string.Equals(row.Policy, "Unrestricted", StringComparison.OrdinalIgnoreCase))
            findings.Add("This rule allows broad network access for its subject.");
        if (DateTime.TryParseExact(row.Created, "yyyy/MM/dd HH:mm", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var created) && created < DateTime.Now.AddDays(-180))
            findings.Add("This rule is over six months old; confirm it is still needed.");
        return string.Join(" ", findings);
    }

    public static string CreateSummary(int findingsCount)
    {
        return findingsCount == 0
            ? "No obvious duplicate, broad, or ageing rules found. Review the policy scope before changing rules."
            : $"{findingsCount} rule{(findingsCount == 1 ? string.Empty : "s")} to review. Findings are advisory and do not change firewall rules.";
    }
}
