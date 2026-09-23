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

        [TestCase(new string[0], ValidationDataGuard.DataArgument.Missing)]
        [TestCase(new[] { "Unity.exe", "--lms-validation-data" }, ValidationDataGuard.DataArgument.Rejected)]
        [TestCase(new[] { "--lms-validation-data", "relative/data" }, ValidationDataGuard.DataArgument.Rejected)]
        [TestCase(new[] { "--lms-validation-data", "N:/LetMeSleep/UserData/Unity" }, ValidationDataGuard.DataArgument.Rejected)]
        [TestCase(new[] { "--lms-validation-data", "C:/Users/player/AppData/LocalLow/LetMeSleep" }, ValidationDataGuard.DataArgument.Rejected)]
        [TestCase(new[] { "--lms-validation-data", "N:/LetMeSleep/Validation/V030" }, ValidationDataGuard.DataArgument.Rejected)]
        [TestCase(new[] { "-batchmode", "--lms-validation-data", "N:/LetMeSleep/Validation/V030/fixes/run-01/data" }, ValidationDataGuard.DataArgument.Dedicated)]
        public void ApplicationDataArgumentMustNameADedicatedRunDirectory(string[] args, object expected)
        {
            // TrainingBootstrapPlayModeTests boots AlfaApplication, which rewrites preferences in this directory.
            Assert.That(ValidationDataGuard.ResolveDataArgument(args, Roots, out _), Is.EqualTo(expected));
        }

        [TestCase("N:/LetMeSleep/Validation/V020/ModularPersistence-01")]
        [TestCase("N:/LetMeSleep/Validation/V030/fixes/run-01/data/")]
        public void DedicatedRunDirectoriesBelowARootAreAccepted(string path)
        {
            Assert.That(ValidationDataGuard.IsDedicatedRunDirectory(path, Roots), Is.True);
        }
    }
}
