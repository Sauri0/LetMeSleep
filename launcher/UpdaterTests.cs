using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Web.Script.Serialization;
using System.Collections.Generic;

namespace LetMeSleep.Updater {
    static class UpdaterTests {
        static int checks;
        static void Check(bool value, string name) { if (!value) throw new Exception("FAIL " + name); checks++; Console.WriteLine("PASS " + name); }
        static void Reject(Action action, string name) { bool rejected = false; try { action(); } catch { rejected = true; } Check(rejected, name); }
        static Release Release(string tag, bool draft = false, bool complete = true, int series = 0) {
            var name = "Let-me-sleep-" + tag.TrimStart('v') + "-Windows.zip";
            var assets = complete ? new List<Asset> { new Asset { name = name, size = 100 }, new Asset { name = name + ".sha256.txt", size = 100 } } : new List<Asset>();
            if (series == 1) assets.Add(new Asset { name = Updater.SeriesMarker, size = 30 });
            return new Release { tag_name = tag, draft = draft, assets = assets.ToArray() };
        }
        static void Entry(ZipArchive archive, string name, string text) {
            using (var writer = new StreamWriter(archive.CreateEntry(name).Open())) writer.Write(text);
        }
        static void Fixture(string dir, string version, int series = 0) {
            Directory.CreateDirectory(dir); File.WriteAllText(Path.Combine(dir, "Let-me-sleep.exe"), "fixture - never executable");
            var build = new Build { version = version, releaseSeries = series, executable = "Let-me-sleep.exe", files = new Dictionary<string,string> { { "Let-me-sleep.exe", Updater.Hash(Path.Combine(dir,"Let-me-sleep.exe")) } } };
            File.WriteAllText(Path.Combine(dir, "BUILD.json"), new JavaScriptSerializer().Serialize(build));
        }
        static int Main(string[] args) {
            try {
                if (args.Length == 2 && args[0] == "--install-latest-no-launch") {
                    var updater = new Updater(args[1], (s,p) => { if (p == 0 || p >= 96) Console.WriteLine(s); }, CancellationToken.None);
                    var installed = updater.EnsureLatest();
                    Check(File.Exists(installed.Executable), "live GitHub download, checksum, extraction and manifest");
                    var again = updater.EnsureLatest();
                    Check(installed.DirectoryPath == again.DirectoryPath, "second startup keeps same latest installation");
                    Check(updater.Current().Version == installed.Version, "persisted activation validates");
                    Console.WriteLine("LIVE_INSTALL checks=" + checks + " game_launched=false version=" + installed.Version); return 0;
                }
                string root = Path.Combine(Path.GetTempPath(), "LMS-updater-tests-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(root);
                try {
                    Check(Updater.ParseVersion("v0.9.10") > Updater.ParseVersion("0.9.3"), "numeric ordering (10 after 3)");
                    Check(Updater.ParseVersion("v0.9.3-preview") == null, "unsupported tag ignored");
                    string[] stages = { "0.9.3", "0.9.4-alfa", "0.9.4-beta", "0.9.4-omega", "0.9.4-delta", "0.9.4-gamma", "0.9.4", "0.9.5-alfa" };
                    for (int i = 1; i < stages.Length; i++) Check(Updater.ParseVersion(stages[i]) > Updater.ParseVersion(stages[i-1]), "release order " + stages[i-1] + " -> " + stages[i]);
                    Check(Updater.ParseVersion("0.9.4-alfa.1") > Updater.ParseVersion("0.9.4-alfa"), "alfa revision upgrades original candidate");
                    Check(Updater.ParseVersion("0.9.4-alfa.10") > Updater.ParseVersion("0.9.4-alfa.2"), "revision ordering is numeric");
                    Check(Updater.ParseVersion("0.9.4-beta") > Updater.ParseVersion("0.9.4-alfa.999"), "revisions do not skip stage order");
                    Check(Updater.ParseVersion("v0.9.4-alfa.1").ToString() == "0.9.4-alfa.1", "revision manifest and filename roundtrip");
                    foreach (string invalid in new[] { "0.9.4-alfa.0", "0.9.4-alfa.01", "0.9.4-alfa.-1", "0.9.4-alfa.999999999999999" })
                        Check(Updater.ParseVersion(invalid) == null, "reject ambiguous revision " + invalid);
                    Check(Updater.SelectRelease(new[] { Release("v0.9.4-alfa"), Release("v0.9.4-alfa.1"), Release("v0.9.4-alfa.2", false, false) }).tag_name == "v0.9.4-alfa.1", "choose complete revision only");
                    foreach (string badVersion in new[] { "0.9.4/delta", "0.9.4-rc", "0.9.4-GAMMA", "0.09.4-alfa", "launcher-v1.0.1" })
                        Check(Updater.ParseVersion(badVersion) == null, "reject noncanonical version " + badVersion);
                    Check(Updater.SelectRelease(new[] { Release("v0.9.4-omega"), Release("v0.9.4-delta"), Release("v0.9.4-beta"), Release("v0.9.4-gamma",true) }).tag_name == "v0.9.4-delta", "delta follows omega; draft gamma excluded");
                    Check(Updater.SelectRelease(new[] { Release("v0.9.3"), Release("v0.9.10"), Release("v99.0.0", true), Release("v99.1.0", false, false) }).tag_name == "v0.9.10", "newest complete public numbered playtest");
                    Check(Updater.SelectRelease(new[] { Release("v1.0.0", false, false) }) == null, "incomplete release never installed");
                    var relaunch = Release("v0.2.0", series: 1);
                    Check(Updater.SelectRelease(new[] { Release("v0.9.4-alfa.2"), relaunch }).tag_name == "v0.2.0", "explicit new series supersedes old numbering");
                    Check(Updater.SelectRelease(new[] { relaunch, Release("v0.2.1", series: 1) }).tag_name == "v0.2.1", "new series upgrades numerically");
                    Check(Updater.SelectRelease(new[] { Release("v0.9.4-alfa.2"), Release("v0.2.0", complete: false, series: 1) }).tag_name == "v0.9.4-alfa.2", "partial relaunch does not displace usable package");
                    Check(Updater.SelectRelease(new[] { Release("v0.9.4-alfa.2"), Release("v0.2.0", draft: true, series: 1) }).tag_name == "v0.9.4-alfa.2", "draft relaunch is never selected");
                    var invalidMarker = Release("v0.2.0", series: 1); invalidMarker.assets.Last().size = 0;
                    Check(Updater.SelectRelease(new[] { invalidMarker }) == null, "empty series marker rejected");
                    Check(!Updater.KeepInstalled(new Installation { Version = Updater.ParseVersion("0.9.4-alfa.2") }, relaunch), "existing old install upgrades to relaunch");
                    Check(Updater.KeepInstalled(new Installation { Version = Updater.ParseVersion("0.2.0"), ReleaseSeries = 1 }, Release("v0.9.4-alfa.2")), "new install never rolls back to legacy feed");
                    Check(Updater.KeepInstalled(new Installation { Version = Updater.ParseVersion("0.2.0"), ReleaseSeries = 1 }, relaunch), "same relaunch keeps installation");
                    string relaunchDir = Path.Combine(root, "relaunch"); Fixture(relaunchDir, "0.2.0", 1);
                    Check(Updater.ValidateInstallation(relaunchDir, Updater.ParseVersion("0.2.0"), 1).ReleaseSeries == 1, "relaunch manifest preserves series");
                    Reject(() => Updater.ValidateInstallation(relaunchDir, Updater.ParseVersion("0.2.0"), 0), "release and package series mismatch rejected");
                    Updater.CheckAssetUrl("https://github.com/Sauri0/LetMeSleep/releases/download/v0.9.3/test.zip", "v0.9.3", "test.zip"); checks++;
                    Reject(() => Updater.CheckAssetUrl("https://github.com/another/repo/releases/download/v0.9.3/test.zip", "v0.9.3", "test.zip"), "foreign repository rejected");
                    foreach (string bad in new[] { "../escape", "/absolute", "C:/escape", "sub/../../escape", "sub\\escape", "file:stream", "foo./x", "NUL.txt", "folder//file" })
                        Reject(() => Updater.SafePath(root, bad), "reject path " + bad);
                    Check(Updater.SafePath(root, "Licencias-online/license.txt").StartsWith(root), "valid nested path");
                    string data = Path.Combine(root,"data.zip"); File.WriteAllText(data,"valid download");
                    Updater.VerifyChecksum(data, Updater.Hash(data) + "  game.zip\n", "game.zip"); checks++;
                    Reject(() => Updater.VerifyChecksum(data, new string('0',64) + "  game.zip", "game.zip"), "corrupted download rejected");
                    Reject(() => Updater.VerifyChecksum(data, Updater.Hash(data) + "  other.zip", "game.zip"), "wrong checksum filename rejected");
                    string zip = Path.Combine(root,"safe.zip"), extracted = Path.Combine(root,"extracted");
                    using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create)) Entry(archive,"package/sub/file.txt","ok");
                    Updater.Extract(zip,extracted,"package",CancellationToken.None);
                    Check(File.ReadAllText(Path.Combine(extracted,"sub/file.txt")) == "ok", "nested ZIP extracted");
                    foreach (string bad in new[] { "package/../escape", "other/file", "package/file:stream" }) {
                        string badZip = Path.Combine(root,Guid.NewGuid()+".zip");
                        using (var archive = ZipFile.Open(badZip, ZipArchiveMode.Create)) Entry(archive,bad,"bad");
                        Reject(() => Updater.Extract(badZip,Path.Combine(root,Guid.NewGuid().ToString()),"package",CancellationToken.None), "archive rejects " + bad);
                    }
                    string duplicate = Path.Combine(root,"duplicate.zip");
                    using (var archive = ZipFile.Open(duplicate, ZipArchiveMode.Create)) { Entry(archive,"package/a.txt","a"); Entry(archive,"package/A.txt","b"); }
                    Reject(() => Updater.Extract(duplicate,Path.Combine(root,"dup"),"package",CancellationToken.None), "case collision rejected");
                    var cancel = new CancellationTokenSource(); cancel.Cancel();
                    Reject(() => Updater.Extract(zip,Path.Combine(root,"cancelled"),"package",cancel.Token), "cancelled extraction stops");
                    string slot1 = "0.9.2-"+Guid.NewGuid().ToString("N"), slot2 = "0.9.3-"+Guid.NewGuid().ToString("N");
                    string oldDir = Path.Combine(root,"versions",slot1), newDir = Path.Combine(root,"versions",slot2);
                    Fixture(oldDir,"0.9.2"); Fixture(newDir,"0.9.3");
                    Updater.Activate(root,slot1);
                    var local = new Updater(root,(s,p) => {}, CancellationToken.None);
                    Check(local.Current().Version == Updater.ParseVersion("0.9.2"), "first install activation");
                    File.WriteAllText(Path.Combine(root,"interrupted-download.tmp"),"partial");
                    Check(local.Current().Version == Updater.ParseVersion("0.9.2"), "interrupted download leaves installed version active");
                    Updater.ValidateInstallation(newDir,Updater.ParseVersion("0.9.3")); Updater.Activate(root,slot2);
                    Check(local.Current().Version == Updater.ParseVersion("0.9.3") && File.Exists(Path.Combine(oldDir,"Let-me-sleep.exe")), "atomic switch retains previous version");
                    File.AppendAllText(Path.Combine(newDir,"Let-me-sleep.exe"),"corrupt");
                    Check(local.Current() == null, "corrupt installation not launched");
                    Reject(() => Updater.ValidateInstallation(oldDir,Updater.ParseVersion("0.9.3")), "release manifest version mismatch rejected");
                    File.WriteAllText(Path.Combine(root,"current.txt"),"../../outside");
                    Check(local.Current() == null, "unsafe installed pointer rejected");
                    string alfaSlot = "0.9.4-alfa-" + Guid.NewGuid().ToString("N");
                    string alfaDir = Path.Combine(root,"versions",alfaSlot); Fixture(alfaDir,"0.9.4-alfa");
                    Updater.Activate(root,alfaSlot);
                    Check(local.Current().Version == Updater.ParseVersion("0.9.4-alfa"), "stage install survives restart and validates manifest");
                    Reject(() => Updater.ValidateInstallation(alfaDir,Updater.ParseVersion("0.9.4-beta")), "wrong stage package rejected");
                    string settings = Path.Combine(root,"launcher settings");
                    string defaultInstall = Path.Combine(root,"default install");
                    var location = new InstallLocation(settings, defaultInstall);
                    Check(location.Load() == defaultInstall && !Directory.Exists(settings), "first launch does not save or install before user chooses");
                    string chosen = Path.Combine(root,"My games with spaces","LetMeSleep");
                    location.Save(chosen);
                    Check(new InstallLocation(settings,defaultInstall).Load() == chosen, "custom folder with spaces remembered across restarts");
                    Check(!Directory.EnumerateFiles(chosen).Any(), "write probe removed without touching game files");
                    location.Save(defaultInstall);
                    Check(location.Load() == defaultInstall, "folder choice replaced atomically");
                    Reject(() => location.Save("relative/path"), "relative installation rejected");
                    Reject(() => location.Save("C:\\"), "drive root rejected");
                    string blocked = Path.Combine(root,"file-instead-of-folder"); File.WriteAllText(blocked,"keep");
                    Reject(() => location.Save(blocked), "unwritable location rejected before remembering");
                    Check(location.Load() == defaultInstall && File.ReadAllText(blocked) == "keep", "failed choice preserves settings and existing file");
                    Console.WriteLine("UPDATER_TESTS checks=" + checks + " failures=0 game_launched=false");
                } finally {
                    string full = Path.GetFullPath(root), temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd('\\')+"\\";
                    if (!full.StartsWith(temp,StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(full).StartsWith("LMS-updater-tests-")) throw new Exception("Unsafe test cleanup");
                    Directory.Delete(full,true);
                }
                return 0;
            } catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }
    }
}
