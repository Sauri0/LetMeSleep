using System;
using System.IO;
using System.Text;

namespace LetMeSleep.Bootstrap
{
    public enum PreferenceDocumentKind { Invalid, Current, UnsupportedVersion }

    // Storage only: the application owns JSON parsing and sanitizing field values.
    public sealed class PreferenceFileStore
    {
        private const int MaxBytes = 16384;
        private readonly string path;
        private readonly Func<string, PreferenceDocumentKind> classify;
        public bool RecoveredFromBackup { get; private set; }
        public bool WriteBlocked { get; private set; }

        public PreferenceFileStore(string path, Func<string, PreferenceDocumentKind> classify)
        {
            this.path = Path.GetFullPath(path);
            this.classify = classify ?? throw new ArgumentNullException(nameof(classify));
        }

        public string Load()
        {
            RecoveredFromBackup = false;
            WriteBlocked = false;
            var primary = Read(path, out var kind);
            if (kind == PreferenceDocumentKind.Current) return primary;
            // A newer format must not be downgraded using an older backup.
            if (kind == PreferenceDocumentKind.UnsupportedVersion) { WriteBlocked = true; return null; }
            var backup = Read(path + ".backup", out kind);
            if (kind == PreferenceDocumentKind.Current) { RecoveredFromBackup = true; return backup; }
            if (kind == PreferenceDocumentKind.UnsupportedVersion) WriteBlocked = true;
            return null;
        }

        public void Save(string json)
        {
            if (json == null || Encoding.UTF8.GetByteCount(json) > MaxBytes || classify(json) != PreferenceDocumentKind.Current)
                throw new InvalidDataException("Invalid preferences document.");
            // Recheck the on-disk version in case another application replaced it since Load.
            Read(path, out var kind);
            if (kind != PreferenceDocumentKind.Current)
            {
                Read(path + ".backup", out var backupKind);
                if (backupKind == PreferenceDocumentKind.UnsupportedVersion) WriteBlocked = true;
            }
            if (WriteBlocked || kind == PreferenceDocumentKind.UnsupportedVersion)
                throw new InvalidDataException("Preferences belong to an unsupported version.");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temp = path + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                byte[] bytes = new UTF8Encoding(false).GetBytes(json);
                using (var file = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { file.Write(bytes, 0, bytes.Length); file.Flush(true); }
                if (!File.Exists(path)) File.Move(temp, path);
                else
                {
                    // Preserve damaged input separately instead of overwriting a recoverable backup with it.
                    string backup = kind == PreferenceDocumentKind.Current ? path + ".backup"
                        : path + ".rejected-" + Guid.NewGuid().ToString("N");
                    File.Replace(temp, path, backup);
                }
            }
            finally
            {
                if (File.Exists(temp))
                    try { File.Delete(temp); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        }

        private string Read(string file, out PreferenceDocumentKind kind)
        {
            kind = PreferenceDocumentKind.Invalid;
            if (!File.Exists(file) || new FileInfo(file).Length > MaxBytes) return null;
            string json = File.ReadAllText(file);
            kind = classify(json);
            return json;
        }
    }
}
