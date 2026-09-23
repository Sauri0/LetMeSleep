using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

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

        internal enum DataArgument { Missing, Rejected, Dedicated }

        /// <summary>Classifies the --lms-validation-data argument the application will use as its data directory.</summary>
        internal static DataArgument ResolveDataArgument(IReadOnlyList<string> args, IEnumerable<string> roots, out string path)
        {
            path = null;
            int option = -1;
            for (int i = 0; args != null && i < args.Count; i++) if (args[i] == "--lms-validation-data") { option = i; break; }
            if (option < 0) return DataArgument.Missing;
            if (option + 1 >= args.Count || string.IsNullOrWhiteSpace(args[option + 1]) || !Path.IsPathRooted(args[option + 1])) return DataArgument.Rejected;
            path = Normalize(args[option + 1]);
            return IsDedicatedRunDirectory(path, roots) ? DataArgument.Dedicated : DataArgument.Rejected;
        }

        /// <summary>
        /// For fixtures that boot AlfaApplication (which migrates and rewrites preferences in its data directory):
        /// ignored without the argument, failed when it points anywhere but a dedicated run directory.
        /// </summary>
        internal static string RequireDedicatedDataPath()
        {
            var result = ResolveDataArgument(Environment.GetCommandLineArgs(), Roots, out string path);
            if (result == DataArgument.Missing) Assert.Ignore("Requires --lms-validation-data <dedicated directory below a validation root>.");
            Assert.That(result, Is.EqualTo(DataArgument.Dedicated),
                "Use a dedicated directory below " + string.Join(" or ", Roots) + ", never a root or a real profile: " + (path ?? "<missing>"));
            return path;
        }
    }
}
