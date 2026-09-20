using System.Reflection;
using LetMeSleep.Audio;
using NUnit.Framework;
using UnityEngine;

namespace LetMeSleep.Tests.VoicePlayMode
{
    public sealed class RoundMusicPlayModeTests
    {
        private GameObject root;
        private AudioClip clip;
        private AlfaAudioDirector director;
        private AudioBedPlayer[] beds;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Round music fixture");
            clip = AudioClip.Create("Silent fixture", 48000, 1, 48000, false);
            director = root.AddComponent<AlfaAudioDirector>();
            string[] fields = { "roundMusic", "roundRhythm", "roundMelody" };
            beds = new AudioBedPlayer[fields.Length];
            for (int i = 0; i < beds.Length; i++)
            {
                var child = new GameObject(fields[i]);
                child.transform.SetParent(root.transform);
                beds[i] = child.AddComponent<AudioBedPlayer>();
                Set(beds[i], "clip", clip);
                Set(beds[i], "fadeSeconds", 0f);
                Set(director, fields[i], beds[i]);
            }
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(clip);
        }

        [Test]
        public void RoundDoesNotStartContinuousMusicAndUrgencyPlaysOnce()
        {
            director.EnterRound();
            AssertBeds(false);
            director.SetPublicRoundUrgency(false);
            AssertBeds(false);
            director.SetPublicRoundUrgency(true);
            AssertBeds(true);
            // Drive the expired deadline without waiting four real seconds.
            Set(director, "roundMusicStopAt", 0f);
            typeof(AlfaAudioDirector).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(director, null);
            AssertBeds(false);
            director.SetPublicRoundUrgency(false);
            director.SetPublicRoundUrgency(true);
            director.EnterRound(); // Rebinding the same round cannot replay it.
            director.SetPublicRoundUrgency(true);
            AssertBeds(false);
        }

        [Test]
        public void ResultsStopPhraseAndNextRoundCanPlayAgain()
        {
            director.EnterRound();
            director.SetPublicRoundUrgency(true);
            director.FinishHumansWin();
            AssertBeds(false);
            director.SetPublicRoundUrgency(true);
            AssertBeds(false);
            director.EnterRound();
            director.SetPublicRoundUrgency(true);
            AssertBeds(true);
            director.StopAll();
            AssertBeds(false);
        }

        private void AssertBeds(bool expected)
        {
            foreach (var bed in beds) Assert.That(bed.IsPlaying, Is.EqualTo(expected), bed.name);
        }
        private static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}
