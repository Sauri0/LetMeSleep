using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace LetMeSleep.Updater {
    public sealed class InstallLocation {
        readonly string settingsDirectory;
        public readonly string DefaultRoot;
        public InstallLocation(string settings, string defaultRoot) {
            settingsDirectory = settings; DefaultRoot = Normalize(defaultRoot);
        }
        public static string Normalize(string path) {
            if (String.IsNullOrWhiteSpace(path) || !Regex.IsMatch(path, @"^[A-Za-z]:[\\/]"))
                throw new IOException("Elegí una carpeta en un disco local.");
            string full = Path.GetFullPath(path).TrimEnd('\\', '/');
            if (full.Length <= 2) throw new IOException("Elegí una carpeta, no la raíz del disco.");
            return full;
        }
        public string Load() {
            try { return Normalize(File.ReadAllText(Path.Combine(settingsDirectory, "install-path.txt"))); }
            catch { return DefaultRoot; }
        }
        public void Save(string directory) {
            string normalized = Normalize(directory);
            // Probe the chosen folder before remembering it. No elevation and no existing files replaced.
            Directory.CreateDirectory(normalized);
            string probe = Path.Combine(normalized, "write-probe-" + Guid.NewGuid().ToString("N") + ".tmp");
            using (new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose)) {}
            Directory.CreateDirectory(settingsDirectory);
            string target = Path.Combine(settingsDirectory, "install-path.txt");
            string next = Path.Combine(settingsDirectory, "path-" + Guid.NewGuid().ToString("N") + ".tmp");
            using (var file = new FileStream(next, FileMode.CreateNew)) {
                byte[] text = Encoding.UTF8.GetBytes(normalized); file.Write(text, 0, text.Length); file.Flush(true);
            }
            if (File.Exists(target)) File.Replace(next, target, null); else File.Move(next, target);
        }
    }
}
