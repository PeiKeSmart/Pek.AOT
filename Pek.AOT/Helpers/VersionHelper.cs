using System.Text.RegularExpressions;

using Pek.Extension;

namespace Pek.Helpers;

/// <summary>版本号帮助类</summary>
public class VersionHelper
{
    /// <summary>比较两个版本号</summary>
    /// <param name="version1"></param>
    /// <param name="version2"></param>
    /// <returns></returns>
    public static Int32 Compare(String version1, String version2)
    {
        var regex = new Regex(@"^(\d+(?:\.\d+)*)([A-Za-z]*)$");

        var match1 = regex.Match(version1);
        var match2 = regex.Match(version2);

        if (!match1.Success || !match2.Success)
            throw new ArgumentException("版本号格式不正确");

        var numericPart1 = match1.Groups[1].Value;
        var numericPart2 = match2.Groups[1].Value;

        var numericComparison = CompareNumericVersions(numericPart1, numericPart2);
        if (numericComparison != 0) return numericComparison;

        var alphaPart1 = match1.Groups[2].Value;
        var alphaPart2 = match2.Groups[2].Value;

        if (alphaPart1.IsNullOrWhiteSpace() && !alphaPart2.IsNullOrWhiteSpace()) return -1;
        if (!alphaPart1.IsNullOrWhiteSpace() && alphaPart2.IsNullOrWhiteSpace()) return 1;

        return String.Compare(alphaPart1, alphaPart2, StringComparison.OrdinalIgnoreCase);
    }

    private static Int32 CompareNumericVersions(String version1, String version2)
    {
        var parts1 = version1.Split('.');
        var parts2 = version2.Split('.');

        var maxLength = Math.Max(parts1.Length, parts2.Length);

        for (var i = 0; i < maxLength; i++)
        {
            var v1 = i < parts1.Length ? Int32.Parse(parts1[i]) : 0;
            var v2 = i < parts2.Length ? Int32.Parse(parts2[i]) : 0;

            if (v1 < v2) return -1;
            if (v1 > v2) return 1;
        }

        return 0;
    }
}
