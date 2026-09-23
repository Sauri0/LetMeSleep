using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using LetMeSleep.Audio;
using LetMeSleep.Bootstrap;
using LetMeSleep.Core;
using LetMeSleep.Gameplay;
using LetMeSleep.Online;
using LetMeSleep.Tests.AudioEvidence;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace LetMeSleep.Tests.VoiceEvidence
{
    /// <summary>
    /// Voice chat evidence on the production path, recorded at the AudioListener: a remote speaker's synthetic
    /// speech goes through VoiceCaptureProcessor (PTT fades, gate, AGC), IMA ADPCM and the wire codec, a simulated
    /// network (loss, jitter, reordering), VoiceOnlineSession (jitter buffer, concealment flags, StreamClosed) and
    /// VoicePeerPresenter (proximity policy, physics occlusion and room probes, smoothing) into VoicePlayoutStream.
    /// This is the wiring VoiceRuntimeCoordinator uses; only EOS and the microphone are replaced.
    /// Run: tools/audio_evidence/run_unity_audio_evidence.sh &lt;unity&gt; &lt;out&gt; LetMeSleep.Tests.VoiceEvidence
    /// and tools/voice_evidence/voice_report.py &lt;out&gt;.
    /// </summary>
    [Category("AudioEvidence")]
    [Category("VoiceEvidence")]
    public sealed class VoiceEvidenceScenarios
    {
        private const string RemoteMember = "peer-remote";
        private const uint RemoteActor = 2;
        private AudioEvidenceStage stage;

        [TearDown]
        public void TearDown()
        {
            stage?.Dispose();
            stage = null;
        }

        private static Dictionary<string, object> P(params object[] pairs)
        {
            var map = new Dictionary<string, object>();
            for (int i = 0; i + 1 < pairs.Length; i += 2) map[(string)pairs[i]] = pairs[i + 1];
            return map;
        }

        private sealed class LoopbackTransport : IVoiceDatagramTransport
        {
            public event Action<string, ArraySegment<byte>> PacketReceived;
            public bool ContainsMember(string memberId) => memberId == RemoteMember;
            public bool Send(string memberId, ArraySegment<byte> packet, bool reliable) => ContainsMember(memberId);
            public void Receive(byte[] packet) => PacketReceived?.Invoke(RemoteMember, new ArraySegment<byte>(packet));
            public void Dispose() { PacketReceived = null; }
        }

        /// <summary>The remote player: synthetic speech → capture processor → codec → packets with network timing.</summary>
        private sealed class RemoteSpeaker
        {
            public readonly SyntheticVoice Voice = new SyntheticVoice(130f, 11);
            public readonly VoiceCaptureProcessor Capture = new VoiceCaptureProcessor();
            public readonly VoiceImaAdpcmCodec Codec = new VoiceImaAdpcmCodec();
            public readonly List<(double arrival, byte[] packet)> InFlight = new List<(double, byte[])>();
            public readonly Random Random = new Random(99);
            public uint Stream, Sequence;
            public double NextSend, TalkUntil, Loss, Jitter, Reorder;
            public bool Talking, Releasing, UseCapture = true;
            public Func<float[]> Source;
            public int Sent, Lost;

            public void Begin(double now, double seconds, double loss, double jitter, double reorder, bool useCapture, Func<float[]> source = null)
            {
                Stream++; Sequence = 0; NextSend = now; TalkUntil = now + seconds; Talking = true; Releasing = false;
                Loss = loss; Jitter = jitter; Reorder = reorder; UseCapture = useCapture; Source = source;
                if (useCapture) Capture.BeginTransmission();
            }

            public void Pump(double now)
            {
                while (Talking && NextSend <= now)
                {
                    if (!Releasing && NextSend >= TalkUntil)
                    {
                        Releasing = true;
                        if (UseCapture) Capture.BeginRelease();
                    }
                    if (Releasing && (!UseCapture || Capture.Released))
                    {
                        Sequence++;
                        InFlight.Add((NextSend + .03 + Jitter, VoiceWireCodec.Encode(new VoicePacket(VoicePacketKind.End, 7, 9, RemoteActor, Stream, Sequence, Array.Empty<byte>()))));
                        Talking = false;
                        break;
                    }
                    float[] frame = Source != null ? Source() : Voice.NextFrame();
                    if (UseCapture) frame = Capture.Process(frame);
                    Sequence++; Sent++;
                    byte[] packet = VoiceWireCodec.Encode(new VoicePacket(VoicePacketKind.Audio, 7, 9, RemoteActor, Stream, Sequence, Codec.Encode(frame)));
                    double arrival = NextSend + .03 + Random.NextDouble() * Jitter + (Random.NextDouble() < Reorder ? .025 : 0);
                    if (Random.NextDouble() < Loss) Lost++; else InFlight.Add((arrival, packet));
                    NextSend += .02;
                }
            }

            public void Deliver(double now, LoopbackTransport transport)
            {
                InFlight.Sort((a, b) => a.arrival.CompareTo(b.arrival));
                while (InFlight.Count > 0 && InFlight[0].arrival <= now)
                {
                    transport.Receive(InFlight[0].packet);
                    InFlight.RemoveAt(0);
                }
            }
        }

        [UnityTest]
        public IEnumerator V01_VoiceProximityAndEnvironment()
        {
            stage = AudioEvidenceStage.Begin("v01_voice_environment");
            AudioMixerGroup voiceGroup = stage.Mixer.FindMatchingGroups(VoicePlayoutStream.MixerGroupName).FirstOrDefault();
            Assert.That(voiceGroup, Is.Not.Null, "Voice mixer group");
            var transport = new LoopbackTransport();
            var session = new VoiceOnlineSession(transport, () => stage.Recorder.Clock);
            session.UpdateRound(new VoiceRoundContext(7, 9, 1, true, true, new[] { new VoicePeerRoute(RemoteMember, RemoteActor, true, true, false) }));
            var playout = new GameObject("VoicePlayout-" + RemoteActor);
            stage.Own(playout);
            playout.AddComponent<AudioSource>();
            var stream = playout.AddComponent<VoicePlayoutStream>();
            stream.Initialize(RemoteActor, voiceGroup);
            var probe = new VoiceAcousticProbe { Filter = collider => !collider.isTrigger };
            var presenter = new VoicePeerPresenter(stream, probe);
            session.FrameDecoded += frame => { if (stream) stream.Submit(frame.Samples, frame.Volume, frame.Concealed); };
            session.StreamClosed += _ => { if (stream) stream.EndStream(); };
            var speaker = new RemoteSpeaker();
            // A real sender has been talking before: settle the AGC and noise floor (not transmitted).
            speaker.Capture.BeginTransmission();
            for (int i = 0; i < 250; i++) speaker.Capture.Process(speaker.Voice.NextFrame());
            Transform ear = stage.ListenerObject.transform;
            var listener = new VoiceEndpoint(PlayerRole.Human, false, ear.position);
            stage.Recorder.SetMeta("voicePipeline", "SyntheticVoice→VoiceCaptureProcessor→IMA ADPCM→VoiceWireCodec→sim network→VoiceOnlineSession→VoicePeerPresenter→VoicePlayoutStream");
            stage.Recorder.SetMeta("listenerEar", new[] { ear.position.x, ear.position.y, ear.position.z });

            stage.StartRecording();
            yield return stage.Wait(.6);
            stage.Recorder.Segment("noise_floor", "silence", .05, .5);

            double lastClock = stage.Recorder.Clock;
            IEnumerator Talk(string label, PlayerRole role, Func<double, Vector3> mouthAt, double seconds, string kind = "voice",
                double loss = 0, double jitter = 0, double reorder = 0, double hitchAt = -1, double hitchSeconds = 0,
                bool useCapture = true, Func<float[]> source = null, double tail = .45, string notes = "", bool track = false,
                bool checkContinuity = true)
            {
                stream.SetMosquitoTimbre(role == PlayerRole.Mosquito);
                // Geometry appears and disappears instantly between talks (a map does not): sync physics, forget cached
                // rooms and snap the smoothing, as if the speaker teleported.
                Physics.SyncTransforms();
                probe.ClearCache();
                presenter.Reset();
                int starve0 = stream.Engine.Starvations, sent0 = speaker.Sent, lost0 = speaker.Lost;
                double start = stage.Recorder.Now, clock0 = stage.Recorder.Clock;
                speaker.Begin(clock0, seconds, loss, jitter, reorder, useCapture, source);
                if (track) stage.Recorder.Track("VoiceSpeaker", playout.transform);
                var sample = new Dictionary<string, float>();
                bool sampled = false;
                yield return stage.Animate(seconds + tail, t =>
                {
                    double now = stage.Recorder.Clock;
                    float dt = (float)Math.Max(0, now - lastClock); lastClock = now;
                    var endpoint = new VoiceEndpoint(role, false, mouthAt(Math.Min(t, seconds)));
                    presenter.PresentSpatial(endpoint, listener, ear, now, dt);
                    session.SetPeerAudibility(RemoteActor, presenter.PeerCanHearLocal, presenter.LocalCanHearPeer);
                    speaker.Pump(now);
                    speaker.Deliver(now, transport);
                    bool frozen = hitchAt >= 0 && t >= hitchAt && t < hitchAt + hitchSeconds;
                    if (!frozen) session.Tick(now);
                    if (!sampled && t >= .3)
                    {
                        sampled = true;
                        sample["occlusion"] = presenter.Occlusion; sample["walls"] = presenter.Walls;
                        sample["interior"] = presenter.Room.Interior; sample["reverbSend"] = presenter.Parameters.ReverbSend;
                        sample["decay"] = presenter.Room.DecaySeconds; sample["directGain"] = presenter.Parameters.DirectGain;
                        sample["lowPass"] = presenter.Parameters.LowPassHz; sample["behind"] = presenter.Parameters.Behind;
                        sample["elevation"] = presenter.Parameters.Elevation; sample["distance"] = presenter.Distance;
                    }
                });
                if (track) stage.Recorder.Untrack("VoiceSpeaker");
                Vector3 mouth = mouthAt(Math.Min(.4, seconds));
                Vector3 local = ear.InverseTransformPoint(mouth);
                var parameters = P("speaker", role.ToString(), "listener", "Human", "distance", Get(sample, "distance", presenter.Distance),
                    "azimuth", Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, "audible", presenter.LocalCanHearPeer || Get(sample, "directGain", 0) > 0,
                    "policyGain", Get(sample, "directGain", 0), "policyLowPass", Get(sample, "lowPass", 0), "occluded", Get(sample, "occlusion", 0) > .5f,
                    "occlusion", Get(sample, "occlusion", 0), "walls", Get(sample, "walls", 0), "interior", Get(sample, "interior", 0),
                    "reverbSend", Get(sample, "reverbSend", 0), "decay", Get(sample, "decay", 0), "behind", Get(sample, "behind", 0),
                    "elevation", Get(sample, "elevation", 0), "spatial", true, "mosquitoTimbre", role == PlayerRole.Mosquito, "f0", 130f,
                    "loss", loss, "jitterSeconds", jitter, "reorderShare", reorder, "hitchSeconds", hitchSeconds,
                    "framesSent", speaker.Sent - sent0, "framesLost", speaker.Lost - lost0, "starvations", stream.Engine.Starvations - starve0,
                    "targetMs", stream.Engine.TargetSeconds * 1000f, "startupWindow", kind == "voice" && seconds >= 1.5 && checkContinuity ? 1.0 : 0.0,
                    "track", track ? "VoiceSpeaker" : "", "notes", notes);
                double end = kind == "reverb" ? start + seconds + tail - .05 : start + seconds - .05;
                stage.Recorder.Segment(label, kind, start, end, parameters);
                yield return stage.Wait(.45);
            }

            Vector3 Feet(float x, float z) => new Vector3(x, ear.position.y - 1.53f, z);
            Func<double, Vector3> HumanAt(float x, float z) => _ => VoiceEndpoint.MouthOf(PlayerRole.Human, Feet(x, z), 0f);
            Func<double, Vector3> MosquitoAt(Vector3 p) => _ => p;

            // 1. Distance law, human → human, straight ahead (1–20 m).
            foreach (float d in new[] { 1f, 2f, 3f, 5f, 8f, 10f, 12f, 15f, 20f })
                yield return Talk("human_front_" + d.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "m", PlayerRole.Human, HumanAt(0, d), 2.2);
            // 2. Mosquito → human: at the ear, in front, far, above.
            yield return Talk("mosquito_at_right_ear_0.3m", PlayerRole.Mosquito, MosquitoAt(ear.position + new Vector3(.3f, 0f, .05f)), 2.2);
            foreach (float d in new[] { 1f, 3f, 6f, 9f })
                yield return Talk("mosquito_front_" + d + "m", PlayerRole.Mosquito, MosquitoAt(ear.position + new Vector3(0f, 0f, d)), 2.2);
            yield return Talk("mosquito_above_2m", PlayerRole.Mosquito, MosquitoAt(ear.position + new Vector3(0f, 1.8f, .8f)), 2.2);
            // 3. Direction at 3 m.
            yield return Talk("human_left_3m", PlayerRole.Human, HumanAt(-3, 0), 2.2);
            yield return Talk("human_right_3m", PlayerRole.Human, HumanAt(3, 0), 2.2);
            yield return Talk("human_behind_3m", PlayerRole.Human, HumanAt(0, -3), 2.2);
            yield return Talk("human_front_3m_reference", PlayerRole.Human, HumanAt(0, 3), 2.2);
            // 4. Occlusion at 4 m: open, one wall, open 0.6 m doorway, closed door (panel in the doorway), two walls.
            yield return Talk("human_4m_open", PlayerRole.Human, HumanAt(0, 4), 2.2);
            GameObject wall = stage.Box("Wall_2m", new Vector3(0, 1.5f, 2f), new Vector3(8f, 3f, .2f));
            yield return Talk("human_4m_wall", PlayerRole.Human, HumanAt(0, 4), 2.2, notes: "one 20 cm wall between");
            Object.Destroy(wall);
            yield return null;
            GameObject leftPart = stage.Box("Wall_Left", new Vector3(-2.3f, 1.5f, 2f), new Vector3(4f, 3f, .2f));
            GameObject rightPart = stage.Box("Wall_Right", new Vector3(2.3f, 1.5f, 2f), new Vector3(4f, 3f, .2f));
            yield return Talk("human_4m_door_open", PlayerRole.Human, HumanAt(0, 4), 2.2, notes: "0.6 m doorway on the path");
            GameObject panel = stage.Box("Door_Closed", new Vector3(0f, 1.1f, 2f), new Vector3(.6f, 2.2f, .06f));
            GameObject lintel = stage.Box("Door_Lintel", new Vector3(0f, 2.6f, 2f), new Vector3(.6f, .8f, .2f));
            yield return Talk("human_4m_door_closed", PlayerRole.Human, HumanAt(0, 4), 2.2, notes: "same doorway, door closed");
            GameObject second = stage.Box("Wall_4m", new Vector3(0, 1.5f, 4f), new Vector3(8f, 3f, .2f));
            yield return Talk("human_6m_two_walls", PlayerRole.Human, HumanAt(0, 6), 2.2, notes: "two walls between");
            Object.Destroy(leftPart); Object.Destroy(rightPart); Object.Destroy(panel); Object.Destroy(lintel); Object.Destroy(second);
            yield return null;
            // 5. Interior vs exterior (voice and a 60 ms burst for the decay).
            Func<float[]> Burst()
            {
                int frames = 0;
                var random = new Random(5);
                return () =>
                {
                    var frame = new float[240];
                    for (int i = 0; i < 240; i++)
                    {
                        int n = frames * 240 + i;
                        float envelope = Mathf.Clamp01(n / 60f) * Mathf.Clamp01((720 - n) / 60f);
                        frame[i] = (float)(random.NextDouble() * 2 - 1) * .45f * envelope;
                    }
                    frames++;
                    return frame;
                };
            }
            yield return Talk("human_3m_exterior", PlayerRole.Human, HumanAt(0, 3), 2.2);
            yield return Talk("burst_3m_exterior", PlayerRole.Human, HumanAt(0, 3), .06, kind: "reverb", useCapture: false, source: Burst(), tail: 1.0);
            GameObject room = stage.Room("Room_Small", new Vector3(0, ear.position.y - .1f, 1.5f), new Vector3(5f, 3f, 6f));
            yield return Talk("human_3m_interior_small_room", PlayerRole.Human, HumanAt(0, 3), 2.2, notes: "5 x 3 x 6 m room, both inside");
            yield return Talk("burst_3m_interior_small_room", PlayerRole.Human, HumanAt(0, 3), .06, kind: "reverb", useCapture: false, source: Burst(), tail: 1.0);
            Object.Destroy(room);
            yield return null;
            GameObject hall = stage.Room("Room_Hall", new Vector3(0, ear.position.y + 1.4f, 3f), new Vector3(12f, 6f, 16f));
            yield return Talk("human_3m_interior_hall", PlayerRole.Human, HumanAt(0, 3), 2.2, notes: "12 x 6 x 16 m hall");
            yield return Talk("burst_3m_interior_hall", PlayerRole.Human, HumanAt(0, 3), .06, kind: "reverb", useCapture: false, source: Burst(), tail: 1.4);
            Object.Destroy(hall);
            yield return null;
            // 6. Network: loss/jitter, reordering, a main-thread hitch.
            yield return Talk("human_3m_loss5_jitter40ms", PlayerRole.Human, HumanAt(0, 3), 5.0, loss: .05, jitter: .04);
            yield return Talk("human_3m_loss10_jitter60ms", PlayerRole.Human, HumanAt(0, 3), 5.0, loss: .10, jitter: .06);
            yield return Talk("human_3m_reorder5pct", PlayerRole.Human, HumanAt(0, 3), 4.0, reorder: .05);
            yield return Talk("human_3m_hitch80ms", PlayerRole.Human, HumanAt(0, 3), 4.0, hitchAt: 1.6, hitchSeconds: .08, notes: "session not ticked for 80 ms");
            // 7. Push-to-talk: five short presses (fade in 20 ms, release tail 40 ms).
            double pttStart = stage.Recorder.Now;
            for (int i = 0; i < 5; i++)
                yield return Talk("ptt_press_" + (i + 1), PlayerRole.Human, HumanAt(0, 2), .4, tail: .3);
            stage.Recorder.Segment("ptt_presses_x5", "transition", pttStart, stage.Recorder.Now, P("from", "silence", "to", "voice", "mechanism", "PTT press/release x5"));
            // 8. Movement: walking away through the cut, a mosquito circling the head at 1 m, 3 m/s.
            yield return Talk("human_walking_away_2_to_14m", PlayerRole.Human, t => VoiceEndpoint.MouthOf(PlayerRole.Human, Feet(.5f, 2f + 2f * (float)t), 0f), 6.0, track: true,
                checkContinuity: false, notes: "crosses the 12 m cut at t = 5 s: the last second is silent on purpose");
            yield return Talk("mosquito_circling_head_1m", PlayerRole.Mosquito,
                t => ear.position + new Vector3(Mathf.Sin((float)t * 3f), .1f, Mathf.Cos((float)t * 3f)), 5.0, track: true);
            // 9. Leaving the room while the peer talks: 5 ms fade on the audio thread, then nothing.
            stream.SetMosquitoTimbre(false);
            presenter.Reset();
            double leaveStart = stage.Recorder.Now;
            speaker.Begin(stage.Recorder.Clock, 3.0, 0, 0, 0, true);
            bool left = false;
            double leaveAt = 0;
            yield return stage.Animate(1.2, t =>
            {
                double now = stage.Recorder.Clock;
                if (!left && stream)
                {
                    presenter.PresentSpatial(new VoiceEndpoint(PlayerRole.Human, false, HumanAt(0, 3)(0)), listener, ear, now, .016f);
                    speaker.Pump(now); speaker.Deliver(now, transport); session.Tick(now);
                }
                if (!left && t >= .9) { left = true; leaveAt = stage.Recorder.Now; session.Dispose(); stream.FadeOutAndDestroy(); }
            });
            yield return stage.Wait(1.2);
            stage.Recorder.Segment("leave_room_while_talking", "transition", leaveAt - .4, leaveAt + .6, P("from", "voice", "to", "silence", "mechanism", "session.Dispose + VoicePlayoutStream.FadeOutAndDestroy"));
            stage.Recorder.Segment("residual_after_leave", "silence", leaveAt + .4, stage.Recorder.Now - .05);
            stage.Recorder.Segment("voice_before_leave", "voice", leaveStart + .3, leaveAt, P("speaker", "Human", "distance", 3f, "audible", true, "startupWindow", 0.0));
            stage.Recorder.Event("voice_totals", "{\"sent\":" + speaker.Sent + ",\"lost\":" + speaker.Lost + ",\"playoutStarvations\":" + (stream ? stream.Engine.Starvations : -1) + "}");
            stage.Recorder.SetMeta("raycasts", probe.RaycastCount);
            stage.Finish();
        }

        private static float Get(Dictionary<string, float> map, string key, float fallback) => map.TryGetValue(key, out float value) ? value : fallback;
    }
}
