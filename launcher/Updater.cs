using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;

namespace LetMeSleep.Updater {
    public sealed class Asset {
        public string name { get; set; }
        public string browser_download_url { get; set; }
        public long size { get; set; }
    }
    public sealed class Release {
        public string tag_name { get; set; }
        public bool draft { get; set; }
        public Asset[] assets { get; set; }
    }
    public sealed class Build {
        public string version { get; set; }
        public string executable { get; set; }
        public Dictionary<string,string> files { get; set; }
    }
    public sealed class Installation {
        public string DirectoryPath;
        public GameVersion Version;
        public string Executable { get { return Path.Combine(DirectoryPath, "Let-me-sleep.exe"); } }
    }
    public sealed class Updater {
        const long MaxZip = 2147483648L;
        const long MaxExpanded = 6442450944L;
        public const string ReleasesUrl = "https://api.github.com/repos/Sauri0/LetMeSleep/releases?per_page=100";
        public readonly string Root;
        readonly Action<string,int> progress;
        readonly CancellationToken cancel;
        static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = 8 * 1024 * 1024 };
        public Updater(string root, Action<string,int> status, CancellationToken cancellation) {
            Root = Path.GetFullPath(root); progress = status; cancel = cancellation;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
        }
        public static GameVersion ParseVersion(string value) { return GameVersion.Parse(value); }
        public static Release SelectRelease(IEnumerable<Release> releases) {
            // Public numbered playtest releases are included, even when GitHub marks them prerelease.
            return releases.Where(r => !r.draft && ParseVersion(r.tag_name) != null && HasAssets(r))
                .OrderByDescending(r => ParseVersion(r.tag_name)).FirstOrDefault();
        }
        static bool HasAssets(Release r) {
            var name = "Let-me-sleep-" + ParseVersion(r.tag_name) + "-Windows.zip";
            return r.assets != null && r.assets.Count(a => a.name == name && a.size > 0 && a.size <= MaxZip) == 1
                && r.assets.Count(a => a.name == name + ".sha256.txt" && a.size > 0 && a.size <= 1024) == 1;
        }
        public static void CheckAssetUrl(string url, string tag, string name) {
            var expected = "https://github.com/Sauri0/LetMeSleep/releases/download/" + tag + "/" + name;
            if (!String.Equals(url, expected, StringComparison.Ordinal)) throw new InvalidDataException("El enlace de actualización no pertenece a esta publicación.");
        }
        void Download(string url, Stream destination, long limit, long expected, bool report) {
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.UserAgent = "LetMeSleep-Updater/1.0";
            request.Timeout = 15000; request.ReadWriteTimeout = 30000;
            request.AutomaticDecompression = DecompressionMethods.GZip;
            using (cancel.Register(request.Abort))
            using (var response = (HttpWebResponse)request.GetResponse()) {
                if (response.ResponseUri.Scheme != "https") throw new InvalidDataException("Descarga sin HTTPS rechazada.");
                if (response.ContentLength > limit) throw new InvalidDataException("La descarga excede el tamaño permitido.");
                using (var input = response.GetResponseStream()) {
                    byte[] buffer = new byte[65536]; long total = 0; int count, lastPercent = -1;
                    while ((count = input.Read(buffer, 0, buffer.Length)) != 0) {
                        cancel.ThrowIfCancellationRequested(); total += count;
                        if (total > limit) throw new InvalidDataException("La descarga excede el tamaño permitido.");
                        destination.Write(buffer, 0, count);
                        int percent = expected > 0 ? (int)Math.Min(95, total * 95 / expected) : 0;
                        if (report && percent != lastPercent) { progress("Descargando el juego… " + (total / 1048576) + " MB", percent); lastPercent = percent; }
                    }
                    if (expected >= 0 && total != expected) throw new InvalidDataException("La descarga quedó incompleta. Volvé a intentar.");
                }
            }
        }
        string GetText(string url, int limit) {
            using (var stream = new MemoryStream()) { Download(url, stream, limit, -1, false); return Encoding.UTF8.GetString(stream.ToArray()); }
        }
        public static string Hash(string file) {
            using (var sha = SHA256.Create()) using (var stream = File.OpenRead(file))
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
        }
        public static void VerifyChecksum(string zip, string checksum, string filename) {
            var match = Regex.Match(checksum.Trim(), @"^([a-fA-F0-9]{64})[ \t]+\*?" + Regex.Escape(filename) + "$", RegexOptions.CultureInvariant);
            if (!match.Success || !String.Equals(Hash(zip), match.Groups[1].Value, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("La descarga no pasó la verificación. Tu versión anterior está intacta.");
        }
        public static string SafePath(string root, string relative) {
            if (String.IsNullOrWhiteSpace(relative) || relative.IndexOf(':') >= 0 || relative.IndexOf('\\') >= 0 || relative.StartsWith("/"))
                throw new InvalidDataException("Ruta inválida en el paquete.");
            foreach (string part in relative.TrimEnd('/').Split('/')) {
                if (part.Length == 0 || part == "." || part == ".." || part.TrimEnd(' ', '.') != part || part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
                    || Regex.IsMatch(part, @"^(CON|PRN|AUX|NUL|COM[0-9]|LPT[0-9])(\.|$)", RegexOptions.IgnoreCase))
                    throw new InvalidDataException("Ruta insegura en el paquete.");
            }
            string prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string path = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
            if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Ruta fuera de la instalación.");
            return path;
        }
        public static void Extract(string zip, string target, string packageName, CancellationToken cancel) {
            using (var archive = ZipFile.OpenRead(zip)) {
                if (archive.Entries.Count > 10000) throw new InvalidDataException("Demasiados archivos en el paquete.");
                long total = 0; var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var entry in archive.Entries) {
                    cancel.ThrowIfCancellationRequested();
                    string prefix = packageName + "/";
                    if (entry.FullName == prefix) continue;
                    if (!entry.FullName.StartsWith(prefix, StringComparison.Ordinal)) throw new InvalidDataException("Estructura de paquete incorrecta.");
                    var relative = entry.FullName.Substring(prefix.Length);
                    string path = SafePath(target, relative);
                    if (!paths.Add(path)) throw new InvalidDataException("Archivo duplicado en el paquete.");
                    if (((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000) throw new InvalidDataException("Enlace simbólico rechazado.");
                    total = checked(total + entry.Length);
                    if (total > MaxExpanded) throw new InvalidDataException("El paquete descomprimido es demasiado grande.");
                    if (entry.FullName.EndsWith("/")) { Directory.CreateDirectory(path); continue; }
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    using (var input = entry.Open()) using (var output = new FileStream(path, FileMode.CreateNew)) {
                        byte[] buffer = new byte[65536]; int count; long written = 0;
                        while ((count = input.Read(buffer, 0, buffer.Length)) > 0) {
                            cancel.ThrowIfCancellationRequested(); written += count;
                            if (written > entry.Length) throw new InvalidDataException("Tamaño extraído incorrecto.");
                            output.Write(buffer, 0, count);
                        }
                        if (written != entry.Length) throw new InvalidDataException("Archivo incompleto.");
                    }
                }
            }
        }
        public static Installation ValidateInstallation(string directory, GameVersion expected) {
            var buildPath = Path.Combine(directory, "BUILD.json");
            if (new FileInfo(buildPath).Length > 1048576) throw new InvalidDataException("Manifiesto demasiado grande.");
            var build = Json.Deserialize<Build>(File.ReadAllText(buildPath));
            var version = ParseVersion(build.version);
            if (version == null || (expected != null && version != expected) || build.executable != "Let-me-sleep.exe"
                || build.files == null || !build.files.ContainsKey("Let-me-sleep.exe")) throw new InvalidDataException("Identidad de versión incorrecta.");
            foreach (var file in build.files) {
                string path = SafePath(directory, file.Key);
                if (!Regex.IsMatch(file.Value ?? "", "^[a-fA-F0-9]{64}$") || !String.Equals(Hash(path), file.Value, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Archivo de instalación dañado: " + file.Key);
            }
            return new Installation { DirectoryPath = directory, Version = version };
        }
        public Installation Current() {
            try {
                string slot = File.ReadAllText(Path.Combine(Root, "current.txt")).Trim();
                if (!ValidSlot(slot)) return null;
                return ValidateInstallation(SafePath(Root, "versions/" + slot), null);
            } catch { return null; }
        }
        public static void Activate(string root, string slot) {
            if (!ValidSlot(slot)) throw new InvalidDataException("Instalación inválida.");
            string current = Path.Combine(root, "current.txt"), next = Path.Combine(root, "pointer-" + Guid.NewGuid().ToString("N") + ".tmp");
            using (var file = new FileStream(next, FileMode.CreateNew, FileAccess.Write, FileShare.None)) {
                byte[] bytes = Encoding.UTF8.GetBytes(slot); file.Write(bytes, 0, bytes.Length); file.Flush(true);
            }
            if (File.Exists(current)) File.Replace(next, current, null); else File.Move(next, current);
        }
        static bool ValidSlot(string slot) {
            var match = Regex.Match(slot ?? "", @"^(.+)-[a-f0-9]{32}$");
            return match.Success && ParseVersion(match.Groups[1].Value) != null;
        }
        public Installation EnsureLatest() {
            Directory.CreateDirectory(Root);
            progress("Buscando actualizaciones…", 0);
            var current = Current();
            var releases = Json.Deserialize<Release[]>(GetText(ReleasesUrl, 8 * 1024 * 1024));
            var latest = SelectRelease(releases);
            if (latest == null) throw new InvalidDataException("Todavía no hay una versión completa disponible.");
            var version = ParseVersion(latest.tag_name);
            if (current != null && current.Version >= version) { progress("Todo listo · " + current.Version, 100); return current; }
            string package = "Let-me-sleep-" + version + "-Windows", filename = package + ".zip";
            var asset = latest.assets.Single(a => a.name == filename);
            var checkAsset = latest.assets.Single(a => a.name == filename + ".sha256.txt");
            CheckAssetUrl(asset.browser_download_url, latest.tag_name, asset.name);
            CheckAssetUrl(checkAsset.browser_download_url, latest.tag_name, checkAsset.name);
            string checksum = GetText(checkAsset.browser_download_url, 1024);
            string job = SafePath(Root, "download-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(job);
            try {
                string zip = Path.Combine(job, "game.zip"), staging = Path.Combine(job, "game");
                using (var file = new FileStream(zip, FileMode.CreateNew)) Download(asset.browser_download_url, file, MaxZip, asset.size, true);
                cancel.ThrowIfCancellationRequested(); progress("Verificando la descarga…", 96);
                VerifyChecksum(zip, checksum, filename);
                Directory.CreateDirectory(staging); Extract(zip, staging, package, cancel);
                progress("Preparando " + version + "…", 98);
                ValidateInstallation(staging, version); cancel.ThrowIfCancellationRequested();
                string slot = version + "-" + Guid.NewGuid().ToString("N");
                string destination = SafePath(Root, "versions/" + slot);
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                Directory.Move(staging, destination);
                Activate(Root, slot);
                progress("Todo listo · " + version, 100);
                return new Installation { DirectoryPath = destination, Version = version };
            } finally {
                // Only this call's randomly named workspace is removed; installed versions and preferences are never deleted.
                string safeJob = SafePath(Root, Path.GetFileName(job));
                if (Directory.Exists(safeJob)) { try { Directory.Delete(safeJob, true); } catch (IOException) {} catch (UnauthorizedAccessException) {} }
            }
        }
    }
}
