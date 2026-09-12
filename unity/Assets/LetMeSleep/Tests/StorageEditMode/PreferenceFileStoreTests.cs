using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using LetMeSleep.Bootstrap;
using NUnit.Framework;

namespace LetMeSleep.Tests.Storage.EditMode
{
    public sealed class PreferenceFileStoreTests
    {
        [Test]
        public void NewProfileThenReplacementCreatesPreviousBackup()
        {
            using var scope = new TemporaryDirectory();
            string first = Current("first");
            string second = Current("second");
            var store = Store(scope.PreferencePath);

            store.Save(first);

            Assert.That(File.ReadAllText(scope.PreferencePath), Is.EqualTo(first));
            Assert.That(File.Exists(scope.BackupPath), Is.False);
            AssertNoTemporaryFiles(scope.Root);

            store.Save(second);

            Assert.That(File.ReadAllText(scope.PreferencePath), Is.EqualTo(second));
            Assert.That(File.ReadAllText(scope.BackupPath), Is.EqualTo(first));
            Assert.That(store.Load(), Is.EqualTo(second));
            Assert.That(store.RecoveredFromBackup, Is.False);
            Assert.That(store.WriteBlocked, Is.False);
            AssertNoTemporaryFiles(scope.Root);
        }

        [Test]
        public void MalformedPrimaryRecoversBackupWithoutMutatingFiles()
        {
            using var scope = new TemporaryDirectory();
            const string malformed = "{broken";
            string backup = Current("known-good");
            File.WriteAllText(scope.PreferencePath, malformed);
            File.WriteAllText(scope.BackupPath, backup);
            Dictionary<string, byte[]> before = Snapshot(scope.Root);

            var store = Store(scope.PreferencePath);
            string loaded = store.Load();

            Assert.That(loaded, Is.EqualTo(backup));
            Assert.That(store.RecoveredFromBackup, Is.True);
            Assert.That(store.WriteBlocked, Is.False);
            AssertSnapshot(scope.Root, before);
        }

        [Test]
        public void SaveAfterMalformedPrimaryArchivesItAndPreservesGoodBackup()
        {
            using var scope = new TemporaryDirectory();
            const string malformed = "{broken";
            string backup = Current("known-good");
            string replacement = Current("replacement");
            File.WriteAllText(scope.PreferencePath, malformed);
            File.WriteAllText(scope.BackupPath, backup);
            var store = Store(scope.PreferencePath);
            Assert.That(store.Load(), Is.EqualTo(backup));

            store.Save(replacement);

            Assert.That(File.ReadAllText(scope.PreferencePath), Is.EqualTo(replacement));
            Assert.That(File.ReadAllText(scope.BackupPath), Is.EqualTo(backup));
            string[] rejected = Directory.GetFiles(scope.Root, "preferences.json.rejected-*");
            Assert.That(rejected.Length, Is.EqualTo(1));
            Assert.That(File.ReadAllText(rejected[0]), Is.EqualTo(malformed));
            AssertNoTemporaryFiles(scope.Root);
        }

        [Test]
        public void UnsupportedPrimaryBlocksLoadAndSaveWithoutModification()
        {
            using var scope = new TemporaryDirectory();
            string future = Future("future-primary");
            string backup = Current("older-backup");
            File.WriteAllText(scope.PreferencePath, future);
            File.WriteAllText(scope.BackupPath, backup);
            Dictionary<string, byte[]> before = Snapshot(scope.Root);
            var store = Store(scope.PreferencePath);

            Assert.That(store.Load(), Is.Null);
            Assert.That(store.WriteBlocked, Is.True);
            Assert.That(store.RecoveredFromBackup, Is.False);
            Assert.Throws<InvalidDataException>(() => store.Save(Current("must-not-write")));
            AssertSnapshot(scope.Root, before);

            var freshStore = Store(scope.PreferencePath);
            Assert.Throws<InvalidDataException>(() => freshStore.Save(Current("still-must-not-write")));
            AssertSnapshot(scope.Root, before);
        }

        [Test]
        public void UnsupportedBackupAlsoBlocksSaveWhenPrimaryIsMalformed()
        {
            using var scope = new TemporaryDirectory();
            File.WriteAllText(scope.PreferencePath, "{broken");
            File.WriteAllText(scope.BackupPath, Future("future-backup"));
            Dictionary<string, byte[]> before = Snapshot(scope.Root);

            var freshStore = Store(scope.PreferencePath);
            Assert.Throws<InvalidDataException>(() => freshStore.Save(Current("direct-save-must-not-write")));
            AssertSnapshot(scope.Root, before);

            var store = Store(scope.PreferencePath);

            Assert.That(store.Load(), Is.Null);
            Assert.That(store.WriteBlocked, Is.True);
            Assert.Throws<InvalidDataException>(() => store.Save(Current("must-not-write")));
            AssertSnapshot(scope.Root, before);
        }

        [Test]
        public void Utf8LimitIsInclusiveAndOversizedSavePreservesPrimary()
        {
            using var scope = new TemporaryDirectory();
            string atLimit = CurrentWithByteCount(16384);
            string overLimit = CurrentWithByteCount(16385);
            var store = Store(scope.PreferencePath);

            store.Save(atLimit);
            byte[] before = File.ReadAllBytes(scope.PreferencePath);

            Assert.That(before.Length, Is.EqualTo(16384));
            Assert.Throws<InvalidDataException>(() => store.Save(overLimit));
            Assert.That(File.ReadAllBytes(scope.PreferencePath), Is.EqualTo(before));
            AssertNoTemporaryFiles(scope.Root);
        }

        [Test]
        public void OversizedPrimaryFallsBackToCurrentBackupWithoutMutation()
        {
            using var scope = new TemporaryDirectory();
            string oversized = CurrentWithByteCount(16385);
            string backup = Current("known-good");
            File.WriteAllText(scope.PreferencePath, oversized, new UTF8Encoding(false));
            File.WriteAllText(scope.BackupPath, backup, new UTF8Encoding(false));
            Dictionary<string, byte[]> before = Snapshot(scope.Root);
            var store = Store(scope.PreferencePath);

            Assert.That(store.Load(), Is.EqualTo(backup));
            Assert.That(store.RecoveredFromBackup, Is.True);
            Assert.That(store.WriteBlocked, Is.False);
            AssertSnapshot(scope.Root, before);
        }

        [Test]
        public void ActivationIoFailurePreservesPrimaryAndLeavesNoPartialFile()
        {
            using var scope = new TemporaryDirectory();
            string previous = Current("previous");
            File.WriteAllText(scope.PreferencePath, previous);
            Directory.CreateDirectory(scope.BackupPath);
            var store = Store(scope.PreferencePath);

            Exception error = Assert.Catch(() => store.Save(Current("replacement")));

            Assert.That(error, Is.InstanceOf<IOException>().Or.InstanceOf<UnauthorizedAccessException>());
            Assert.That(File.ReadAllText(scope.PreferencePath), Is.EqualTo(previous));
            Assert.That(Directory.Exists(scope.BackupPath), Is.True);
            AssertNoTemporaryFiles(scope.Root);
        }

        private static PreferenceFileStore Store(string path) => new PreferenceFileStore(path, Classify);

        private static PreferenceDocumentKind Classify(string document)
        {
            if (document != null && document.StartsWith("{\"schema\":1,", StringComparison.Ordinal))
                return PreferenceDocumentKind.Current;
            if (document != null && document.StartsWith("{\"schema\":2,", StringComparison.Ordinal))
                return PreferenceDocumentKind.UnsupportedVersion;
            return PreferenceDocumentKind.Invalid;
        }

        private static string Current(string value) => "{\"schema\":1,\"value\":\"" + value + "\"}";
        private static string Future(string value) => "{\"schema\":2,\"value\":\"" + value + "\"}";

        private static string CurrentWithByteCount(int byteCount)
        {
            const string prefix = "{\"schema\":1,\"padding\":\"";
            const string suffix = "\"}";
            int padding = byteCount - Encoding.UTF8.GetByteCount(prefix + suffix);
            if (padding < 0) throw new ArgumentOutOfRangeException(nameof(byteCount));
            string document = prefix + new string('x', padding) + suffix;
            if (Encoding.UTF8.GetByteCount(document) != byteCount) throw new InvalidOperationException("Fixture byte count mismatch.");
            return document;
        }

        private static Dictionary<string, byte[]> Snapshot(string directory)
            => Directory.GetFiles(directory).ToDictionary(Path.GetFileName, File.ReadAllBytes, StringComparer.Ordinal);

        private static void AssertSnapshot(string directory, Dictionary<string, byte[]> expected)
        {
            Dictionary<string, byte[]> actual = Snapshot(directory);
            Assert.That(actual.Keys, Is.EquivalentTo(expected.Keys));
            foreach (KeyValuePair<string, byte[]> item in expected)
                Assert.That(actual[item.Key], Is.EqualTo(item.Value), item.Key);
        }

        private static void AssertNoTemporaryFiles(string directory)
            => Assert.That(Directory.GetFiles(directory, "preferences.json.tmp-*"), Is.Empty);

        private sealed class TemporaryDirectory : IDisposable
        {
            public TemporaryDirectory()
            {
                Root = Path.Combine(Path.GetTempPath(), "LMS-PreferenceFileStoreTests-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(Root);
            }

            public string Root { get; }
            public string PreferencePath => Path.Combine(Root, "preferences.json");
            public string BackupPath => PreferencePath + ".backup";

            public void Dispose()
            {
                string full = Path.GetFullPath(Root);
                string temporary = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (!full.StartsWith(temporary, StringComparison.OrdinalIgnoreCase)
                    || !Path.GetFileName(full).StartsWith("LMS-PreferenceFileStoreTests-", StringComparison.Ordinal))
                    throw new InvalidOperationException("Unsafe test cleanup path.");
                if (Directory.Exists(full)) Directory.Delete(full, true);
            }
        }
    }
}
