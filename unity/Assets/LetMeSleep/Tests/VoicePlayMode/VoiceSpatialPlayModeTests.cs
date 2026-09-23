using System.Collections;
using System.Collections.Generic;
using LetMeSleep.Audio;
using LetMeSleep.Bootstrap;
using LetMeSleep.Core;
using LetMeSleep.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LetMeSleep.Tests.VoicePlayMode
{
    public sealed class VoiceSpatialPlayModeTests
    {
        private readonly List<Object> owned = new List<Object>();
        private readonly List<AudioListener> disabled = new List<AudioListener>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object item in owned) if (item) Object.DestroyImmediate(item);
            owned.Clear();
            foreach (AudioListener listener in disabled) if (listener) listener.enabled = true;
            disabled.Clear();
        }

        [UnityTest]
        public IEnumerator Playout_RendersThroughTheSpatializedSourceAndStopsWhenIdle()
        {
            ListenerTap tap = MakeListener(Vector3.zero);
            VoicePlayoutStream stream = MakeStream(new Vector3(-3f, 0f, 0f), 7);
            AudioSource source = stream.GetComponent<AudioSource>();
            Assert.That(source.dopplerLevel, Is.EqualTo(0f), "A talking mosquito must not warble.");
            Assert.That(source.priority, Is.EqualTo(VoicePlayoutStream.SourcePriority));
            Assert.That(source.isPlaying, Is.False, "Idle peers hold no real voice.");
            tap.Recording = true;
            yield return Talk(stream, 1.0f);
            float[] mix = tap.Take();
            if (stream.Engine.RenderCalls == 0) Assert.Ignore("No audio output device: the DSP thread did not run.");
            Assert.That(stream.Engine.RenderedInputSamples, Is.GreaterThan(9000), "The audio thread consumed the voice.");
            Assert.That(stream.Engine.Starvations, Is.LessThanOrEqualTo(1));
            Energy(mix, tap.Channels, out double left, out double right);
            Assert.That(left, Is.GreaterThan(right * 4), "A speaker 3 m to the left is clearly on the left.");
            Assert.That(right, Is.GreaterThan(0), "The far ear is not digitally silent (crossfeed).");

            stream.EndStream();
            float waited = 0;
            while (source.isPlaying && waited < 3f) { waited += Time.unscaledDeltaTime; yield return null; }
            Assert.That(source.isPlaying, Is.False, "The source stops after the stream drains and goes quiet.");
        }

        [UnityTest]
        public IEnumerator FadeOutAndDestroy_RemovesTheVoiceWithoutResidualAudio()
        {
            ListenerTap tap = MakeListener(Vector3.zero);
            VoicePlayoutStream stream = MakeStream(new Vector3(0f, 0f, 2f), 8);
            GameObject root = stream.gameObject;
            yield return Talk(stream, .5f);
            if (stream.Engine.RenderCalls == 0) Assert.Ignore("No audio output device: the DSP thread did not run.");
            stream.FadeOutAndDestroy();
            float waited = 0;
            while (root && waited < 2f) { waited += Time.unscaledDeltaTime; yield return null; }
            Assert.That(root == null, Is.True, "The playout object is gone once its output (reverb included) is silent.");
            tap.Recording = true;
            yield return new WaitForSecondsRealtime(.3f);
            float[] after = tap.Take();
            float peak = 0; foreach (float v in after) peak = Mathf.Max(peak, Mathf.Abs(v));
            Assert.That(peak, Is.LessThan(1e-4f), "Leaving the room leaves no residual voice.");
        }

        [Test]
        public void Probe_GradesOcclusionForWallsDoorsAndOpenAir()
        {
            var probe = new VoiceAcousticProbe();
            Vector3 mouth = new Vector3(0f, 1.5f, 0f), ear = new Vector3(0f, 1.5f, 4f);
            Assert.That(probe.Occlusion(mouth, ear).Blocked, Is.EqualTo(0f), "Nothing in between.");
            GameObject wall = Box(new Vector3(0f, 1.5f, 2f), new Vector3(8f, 3f, .2f));
            Physics.SyncTransforms();
            VoiceOcclusionSample blocked = probe.Occlusion(mouth, ear);
            Assert.That(blocked.Blocked, Is.EqualTo(1f).Within(.001f)); Assert.That(blocked.Walls, Is.EqualTo(1));
            Object.DestroyImmediate(wall); owned.Remove(wall);
            // Wall with a 0.6 m doorway on the direct path: the centre ray and the vertical offsets pass, the side ones do not.
            Box(new Vector3(-2.3f, 1.5f, 2f), new Vector3(4f, 3f, .2f));
            Box(new Vector3(2.3f, 1.5f, 2f), new Vector3(4f, 3f, .2f));
            Physics.SyncTransforms();
            Assert.That(probe.Occlusion(mouth, ear).Blocked, Is.InRange(.2f, .4f), "An open door lets most of the voice through.");
        }

        [Test]
        public void Probe_TellsAClosedRoomFromOpenAir()
        {
            var probe = new VoiceAcousticProbe();
            VoiceRoomSample outside = probe.Room(new Vector3(0f, 1.5f, 0f));
            Assert.That(outside.Interior, Is.EqualTo(0f)); Assert.That(outside.Wet, Is.LessThan(.05f));
            MakeRoom(new Vector3(40f, 1.5f, 0f), new Vector3(4f, 3f, 5f));
            Physics.SyncTransforms();
            VoiceRoomSample inside = probe.Room(new Vector3(40f, 1.5f, 0f));
            Assert.That(inside.Interior, Is.GreaterThan(.9f));
            Assert.That(inside.DecaySeconds, Is.GreaterThan(outside.DecaySeconds + .1f));
            Assert.That(20f * Mathf.Log10(inside.Wet / outside.Wet), Is.GreaterThan(15f), "Room reverb ≥ 15 dB more send than open air.");
        }

        [UnityTest]
        public IEnumerator Presenter_SmoothsOcclusionAndRoutesEachDirectionByRole()
        {
            MakeListener(Vector3.zero);
            VoicePlayoutStream stream = MakeStream(Vector3.zero, 9);
            var presenter = new VoicePeerPresenter(stream, new VoiceAcousticProbe());
            var human = new VoiceEndpoint(PlayerRole.Human, false, new Vector3(0f, 1.53f, 0f));
            var mosquito = new VoiceEndpoint(PlayerRole.Mosquito, false, new Vector3(0f, 1.5f, 4f));
            double now = 0;
            presenter.PresentSpatial(mosquito, human, null, now, 0f);
            Assert.That(presenter.Occlusion, Is.Zero);
            Box(new Vector3(0f, 1.5f, 2f), new Vector3(8f, 3f, .2f));
            Physics.SyncTransforms();
            now += .12; presenter.PresentSpatial(mosquito, human, null, now, .12f);
            float afterOneUpdate = presenter.Occlusion;
            for (int i = 0; i < 10; i++) { now += .1; presenter.PresentSpatial(mosquito, human, null, now, .1f); }
            Assert.That(afterOneUpdate, Is.InRange(.2f, .75f), "The wall fades in (150 ms attack), it does not snap.");
            Assert.That(presenter.Occlusion, Is.GreaterThan(.95f));
            Assert.That(presenter.Parameters.LowPassHz, Is.LessThan(2000f));

            var far = new VoiceEndpoint(PlayerRole.Mosquito, false, new Vector3(0f, 1.5f, 10f));
            presenter.PresentSpatial(far, human, null, now + .1, .1f);
            Assert.That(presenter.LocalCanHearPeer, Is.False, "Mosquito → human cuts at 8 m.");
            Assert.That(presenter.PeerCanHearLocal, Is.True, "Human → mosquito carries to 12 m.");
            Assert.That(presenter.Parameters.DirectGain, Is.Zero, "Zero voice outside the range.");
            yield return null;
        }

        private IEnumerator Talk(VoicePlayoutStream stream, float seconds)
        {
            var frame = new float[240];
            long position = 0;
            double start = Time.realtimeSinceStartupAsDouble, sent = 0;
            while (Time.realtimeSinceStartupAsDouble - start < seconds)
            {
                double due = Time.realtimeSinceStartupAsDouble - start + .06;
                while (sent < due)
                {
                    for (int i = 0; i < frame.Length; i++, position++) frame[i] = .3f * Mathf.Sin(2 * Mathf.PI * 220f * position / 12000f);
                    stream.Submit(frame, 1f);
                    sent += .02;
                }
                yield return null;
            }
        }

        private ListenerTap MakeListener(Vector3 position)
        {
            foreach (AudioListener existing in Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (existing.enabled) { existing.enabled = false; disabled.Add(existing); }
            var go = new GameObject("VoiceTestListener");
            go.transform.SetPositionAndRotation(position, Quaternion.identity);
            go.AddComponent<AudioListener>();
            owned.Add(go);
            return go.AddComponent<ListenerTap>();
        }

        private VoicePlayoutStream MakeStream(Vector3 position, uint actor)
        {
            var go = new GameObject("VoicePlayout-" + actor);
            go.transform.position = position;
            go.AddComponent<AudioSource>();
            var stream = go.AddComponent<VoicePlayoutStream>();
            stream.Initialize(actor);
            owned.Add(go);
            return stream;
        }

        private GameObject Box(Vector3 center, Vector3 size)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.transform.position = center; box.transform.localScale = size;
            owned.Add(box);
            return box;
        }

        private void MakeRoom(Vector3 center, Vector3 inner)
        {
            const float t = .2f;
            Box(center + new Vector3(0, -inner.y / 2 - t / 2, 0), new Vector3(inner.x + 2 * t, t, inner.z + 2 * t));
            Box(center + new Vector3(0, inner.y / 2 + t / 2, 0), new Vector3(inner.x + 2 * t, t, inner.z + 2 * t));
            Box(center + new Vector3(0, 0, inner.z / 2 + t / 2), new Vector3(inner.x + 2 * t, inner.y, t));
            Box(center + new Vector3(0, 0, -inner.z / 2 - t / 2), new Vector3(inner.x + 2 * t, inner.y, t));
            Box(center + new Vector3(inner.x / 2 + t / 2, 0, 0), new Vector3(t, inner.y, inner.z));
            Box(center + new Vector3(-inner.x / 2 - t / 2, 0, 0), new Vector3(t, inner.y, inner.z));
        }

        private static void Energy(float[] mix, int channels, out double left, out double right)
        {
            left = right = 0;
            for (int i = 0; i + 1 < mix.Length; i += channels) { left += mix[i] * mix[i]; right += mix[i + 1] * mix[i + 1]; }
        }
    }
}
