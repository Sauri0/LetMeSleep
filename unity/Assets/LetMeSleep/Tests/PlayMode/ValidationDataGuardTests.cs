using NUnit.Framework;

namespace LetMeSleep.Tests.PlayMode
{
    public sealed class ValidationDataGuardTests
    {
        private static readonly string[] Roots = { "N:/LetMeSleep/Validation/V020", "N:/LetMeSleep/Validation/V030" };

        [TestCase("N:/LetMeSleep/Validation/V020")]
        [TestCase("N:/LetMeSleep/Validation/V020/")]
        [TestCase(@"N:\LetMeSleep\Validation\V020\")]
        [TestCase("N:/LetMeSleep/Validation/V030/")]
        [TestCase("N:/LetMeSleep/Validation")]
        [TestCase("N:/LetMeSleep/Validation/V0201")]
        [TestCase("N:/LetMeSleep/UserData/Unity")]
        [TestCase("")]
        public void ValidationRootsAndForeignFoldersAreNeverAcceptedForDeletion(string path)
        {
            // Passing the root with a trailing slash used to satisfy StartsWith and wipe all V020 evidence.
            Assert.That(ValidationDataGuard.IsDedicatedRunDirectory(path, Roots), Is.False);
        }

        [TestCase("N:/LetMeSleep/Validation/V020/ModularPersistence-01")]
        [TestCase("N:/LetMeSleep/Validation/V030/fixes/run-01/data/")]
        public void DedicatedRunDirectoriesBelowARootAreAccepted(string path)
        {
            Assert.That(ValidationDataGuard.IsDedicatedRunDirectory(path, Roots), Is.True);
        }
    }
}
