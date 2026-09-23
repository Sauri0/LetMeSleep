using LetMeSleep.Bootstrap;
using NUnit.Framework;

namespace LetMeSleep.Tests.EditMode
{
    public sealed class PushToTalkBindingsTests
    {
        [TestCase("<Keyboard>/b", "B")]
        [TestCase("<Keyboard>/space", "SPACE")]
        [TestCase(PushToTalkBindings.DefaultPath, "V")]
        public void LabelComesFromThePersistedBindingNotFromARoomRuntime(string path, string expected)
        {
            // Ajustes outside a room used to show the runtime's initial 'V' even when B was saved.
            Assert.That(PushToTalkBindings.Label(path), Is.EqualTo(expected));
        }

        [TestCase(null)] [TestCase("")] [TestCase("   ")]
        public void MissingBindingFallsBackToTheDefaultKey(string path)
        {
            Assert.That(PushToTalkBindings.Label(path), Is.EqualTo("V"));
        }

        [Test]
        public void WaitingPromptIsSpanishAndDistinctFromAnyKeyLabel()
        {
            Assert.That(PushToTalkBindings.WaitingLabel, Is.EqualTo("PRESIONÁ UNA TECLA"));
            Assert.That(PushToTalkBindings.WaitingNotice, Does.Contain("Esc para cancelar"));
        }
    }
}
