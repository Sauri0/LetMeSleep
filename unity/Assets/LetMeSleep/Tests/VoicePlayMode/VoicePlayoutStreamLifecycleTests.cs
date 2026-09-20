using System.Collections;
using LetMeSleep.Audio;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LetMeSleep.Tests.VoicePlayMode
{
    public sealed class VoicePlayoutStreamLifecycleTests
    {
        [UnityTest]
        public IEnumerator InitializeReinitializeAndDestroyReleaseOnlyOwnedClips()
        {
            var go = new GameObject("VoicePlayoutLifecycleTest");
            AudioSource source = go.AddComponent<AudioSource>();
            AudioClip externalClip = AudioClip.Create("ExternalVoiceClip", 240, 1, 12000, false);
            source.clip = externalClip;
            VoicePlayoutStream stream = go.AddComponent<VoicePlayoutStream>();

            stream.Initialize(1);
            AudioClip firstOwned = source.clip;
            Assert.That(firstOwned, Is.Not.Null);
            Assert.That(firstOwned, Is.Not.SameAs(externalClip));
            Assert.That(source.pitch, Is.EqualTo(1f));
            Assert.That(source.spatialBlend, Is.EqualTo(1f));
            Assert.That(source.rolloffMode, Is.EqualTo(AudioRolloffMode.Custom));
            Assert.That(source.maxDistance, Is.EqualTo(10000f));
            stream.SetSpatial(false);
            Assert.That(source.spatialBlend, Is.EqualTo(0f));

            stream.Initialize(2);
            AudioClip secondOwned = source.clip;
            Assert.That(secondOwned, Is.Not.Null);
            Assert.That(secondOwned, Is.Not.SameAs(firstOwned));
            Assert.That(stream.ActorId, Is.EqualTo(2));
            yield return null;

            Assert.That(firstOwned == null, Is.True, "Reinitialization must destroy its previous runtime clip.");
            Assert.That(externalClip == null, Is.False, "A clip assigned from outside the component must not be destroyed.");

            Object.Destroy(go);
            yield return null;

            Assert.That(secondOwned == null, Is.True, "Destroying the component must destroy its current runtime clip.");
            Assert.That(externalClip == null, Is.False, "Destroying the component must leave external clips alive.");
            Object.Destroy(externalClip);
            yield return null;
        }
    }
}
