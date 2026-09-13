using System;
using System.IO;
using System.Reflection;
using System.Linq;

// Small coordinator adapter: each called builder retains its own identity, new-output and scope guards.
public static class HiggsfieldMapChecks
{
    public static string Run(string configPath, string output)
    {
        var actions = File.ReadAllLines(configPath).Where(line => !string.IsNullOrWhiteSpace(line)).Select(line => line.Split(new[] { '|' }, 2)).ToArray();
        if (actions.Length == 0 || actions.Length > 7 || actions.Any(a => a.Length != 2))
            throw new ArgumentException("Supply 1..7 explicit package actions.");
        Directory.CreateDirectory(output);
        string receipt = Path.Combine(output, "completed-actions.txt");
        if (File.Exists(receipt)) throw new IOException("Use a new output receipt directory.");
        foreach (var action in actions)
        {
            string typeName, method;
            switch (action[0])
            {
                case "skybox": typeName = "HiggsfieldSkyboxMaterialBuilder"; method = "Build"; break;
                case "catalog": typeName = "HiggsfieldMapCatalogBuilder"; method = "Build"; break;
                case "scene": typeName = "HiggsfieldBootstrapSceneInstaller"; method = "Install"; break;
                default: throw new ArgumentException("Unknown package action.");
            }
            if (!Path.IsPathRooted(action[1]) || !File.Exists(action[1])) throw new IOException("Absolute existing builder config required.");
            var type = Type.GetType("LetMeSleep.Editor." + typeName + ", Assembly-CSharp-Editor", true);
            var build = type.GetMethod(method, BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);
            if (build == null) throw new MissingMethodException(typeName, method);
            build.Invoke(null, new object[] { action[1] });
            File.AppendAllText(receipt, action[0] + " " + action[1] + "\n");
        }
        return receipt;
    }
}
