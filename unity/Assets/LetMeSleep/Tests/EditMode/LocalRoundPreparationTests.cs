using System;
using System.Reflection;
using System.Runtime.Serialization;
using LetMeSleep.Online;
using NUnit.Framework;

namespace LetMeSleep.Tests.EditMode
{
    public sealed class LocalRoundPreparationTests
    {
        // Exercise the actual callback barrier without starting native EOS or mocking a transport success.
        private static OnlineGameplaySession Session()
        {
#pragma warning disable SYSLIB0050
            return (OnlineGameplaySession)FormatterServices.GetUninitializedObject(typeof(OnlineGameplaySession));
#pragma warning restore SYSLIB0050
        }
        private static bool Prepare(OnlineGameplaySession s) => (bool)typeof(OnlineGameplaySession)
            .GetMethod("PrepareLocalRound", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(s, null);
        [Test] public void PreparationExceptionFailsOnceAndCannotBecomeReadyOnRetry()
        {
            var s = Session(); int failures = 0, starts = 0; string reason = null;
            s.Failed += r => { failures++; reason = r; };
            s.BeginReceived += (_, __) => { starts++; throw new InvalidOperationException("Map unavailable"); };
            Assert.That(Prepare(s), Is.False); Assert.That(Prepare(s), Is.False);
            Assert.That(reason, Is.EqualTo("LocalRoundPreparationFailed"));
            Assert.That(failures, Is.EqualTo(1)); Assert.That(starts, Is.EqualTo(1));
        }
        [Test] public void NoLocalLoaderCannotAcknowledgeReadiness()
        {
            var s = Session(); string reason = null; s.Failed += r => reason = r;
            Assert.That(Prepare(s), Is.False); Assert.That(reason, Is.EqualTo("LocalRoundPreparationFailed"));
        }
        [Test] public void SuccessfulPreparationCanAcknowledgeReadiness()
        {
            var s = Session(); int starts = 0; s.BeginReceived += (_, __) => starts++;
            Assert.That(Prepare(s), Is.True); Assert.That(starts, Is.EqualTo(1));
        }
        [Test] public void ClosedDuringPreparationCannotAcknowledgeReadiness()
        {
            var s = Session();
            s.BeginReceived += (_, __) => typeof(OnlineGameplaySession).GetField("disposed", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(s, true);
            Assert.That(Prepare(s), Is.False);
        }
    }
}
