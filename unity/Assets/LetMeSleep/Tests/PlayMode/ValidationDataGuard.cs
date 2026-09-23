using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace LetMeSleep.Tests.PlayMode
{
    /// <summary>
    /// Guards fixtures that write or delete preferences under --lms-validation-data. The directory must sit
    /// strictly below a validation root (LMS_VALIDATION_ROOT, or the V020/V030 roots) and never be a root itself.
    /// </summary>
    internal static class ValidationDataGuard
    {
        internal static IReadOnlyList<string> Roots
        {
            get
            {
                string configured = Environment.GetEnvironmentVariable("LMS_VALIDATION_ROOT");
                var roots = new List<string> { "N:/LetMeSleep/Validation/V020", "N:/LetMeSleep/Validation/V030" };
                if (!string.IsNullOrWhiteSpace(configured)) roots.Insert(0, configured);
                return roots;
            }
        }

        internal static string Normalize(string path)
            => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        internal static bool IsDedicatedRunDirectory(string path) => IsDedicatedRunDirectory(path, Roots);

        internal static bool IsDedicatedRunDirectory(string path, IEnumerable<string> roots)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            string candidate = Normalize(path);
            return roots.Where(root => !string.IsNullOrWhiteSpace(root)).Select(Normalize).Any(root =>
                candidate.Length > root.Length + 1 &&
                candidate.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
        }
    }
}
