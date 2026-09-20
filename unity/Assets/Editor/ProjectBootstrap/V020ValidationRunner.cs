using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace LetMeSleep.Editor
{
    /// <summary>Runs the external, versioned five-map PhysX harness in a batch editor.</summary>
    public static class V020ValidationRunner
    {
        public static void RunSurfaceChecks()
        {
            var args = Environment.GetCommandLineArgs();
            string assemblyPath = Argument(args, "-surfaceAssembly");
            string config = Argument(args, "-surfaceConfig");
            string output = Argument(args, "-surfaceOutput");
            if (!File.Exists(assemblyPath) || !File.Exists(config) || Directory.Exists(output))
                throw new IOException("Existing assembly/config and a fresh evidence directory required.");
            var assembly = Assembly.LoadFrom(assemblyPath);
            var method = assembly.GetType("HiggsfieldMapChecks", true).GetMethod("Run", BindingFlags.Public | BindingFlags.Static);
            if (method == null) throw new MissingMethodException("HiggsfieldMapChecks.Run");
            Debug.Log("Surface evidence: " + method.Invoke(null, new object[] { config, output }));
            // The harness report is the authority on scoped PASS/FAIL; process exit alone is not.
        }

        static string Argument(string[] args, string flag)
        {
            int index = Array.IndexOf(args, flag);
            if (index < 0 || index + 1 >= args.Length || Array.LastIndexOf(args, flag) != index || !Path.IsPathRooted(args[index + 1]))
                throw new ArgumentException("One absolute " + flag + " path required.");
            return Path.GetFullPath(args[index + 1]);
        }
    }
}
