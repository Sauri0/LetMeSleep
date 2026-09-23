using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using LetMeSleep.Audio;
using LetMeSleep.Content.Environment;
using LetMeSleep.Core;
using LetMeSleep.Gameplay;
using LetMeSleep.Online;
using LetMeSleep.Presentation.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.TestTools;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace LetMeSleep.Tests.AudioEvidence
{
    /// <summary>
    /// Scripted listening scenarios. Each test records the real listener mix of the CURRENT audio
    /// assets/code into &lt;out&gt;/&lt;scenario&gt;.wav + .json; tools/audio_evidence/lms_audio_evidence.py
    /// measures them. The tests only assert that a recording was produced; quality verdicts live in the
    /// analyzer so the same scenarios can measure before/after states of any front.
    /// </summary>
    [Category("AudioEvidence")]
    public sealed class AudioEvidenceScenarios
    {
        private AudioEvidenceStage stage;

        [TearDown]
        public void TearDown()
        {
            stage?.Dispose();
            stage = null;
            Time.captureDeltaTime = 0f;
        }

        private static Dictionary<string, object> P(params object[] pairs)
        {
            var map = new Dictionary<string, object>();
            for (int i = 0; i + 1 < pairs.Length; i += 2) map[(string)pairs[i]] = pairs[i + 1];
            return map;
        }

        // ------------------------------------------------------------------ 01
        [UnityTest]
        public IEnumerator S01_SpatialPan()
        {
            stage = AudioEvidenceStage.Begin("s01_spatial_pan");
            AlfaAudioDirector director = stage.InstantiateAudioRoot("Evidence_Audio");
            AudioEmitterPool pool = director.Emitters;
            AudioClip probe = AudioEvidenceSignals.PinkNoise("probe_pan", 0.8f);
            stage.Own(probe);
            stage.StartRecording();
            yield return stage.Wait(0.6);
            stage.Recorder.Segment("noise_floor", "silence", 0.1, 0.55);

            // Probe: steady pink noise through the Character cue settings, around the listener at 2 m.
            AudioCue probeCue = stage.CloneCue(AudioEvidenceStage.Cue("HumanFootstep"), probe, volume: 1f, pitch: 1f);
            float[] sweep = { -90, -60, -30, 0, 30, 60, 90, 135, 180, -135 };
            foreach (float azimuth in sweep)
            {
                double start = stage.Recorder.Now;
                pool.Play(probeCue, AudioEvidenceStage.Around(azimuth, 2f));
                yield return stage.Wait(0.95);
                stage.Recorder.Segment("probe@" + azimuth, "pan", start + 0.1, start + 0.7,
                    P("cue", "HumanFootstep_probe", "azimuth", azimuth, "distance", 2f, "signal", "pink"));
            }

            // Real cues left/front/right/back at 2 m; same random seed per cue so the clip/pitch match.
            string[] cues = { "StrikeImpact", "StrikeSwing", "DoorOpen", "HumanFootstep", "MosquitoPerch", "BiteStarted", "ToolPickup" };
            float[] azimuths = { -90, 0, 90, 180 };
            for (int c = 0; c < cues.Length; c++)
            {
                AudioCue cue = AudioEvidenceStage.Cue(cues[c]);
                foreach (float azimuth in azimuths)
                {
                    UnityEngine.Random.InitState(1000 + c);
                    double start = stage.Recorder.Now;
                    pool.Play(cue, AudioEvidenceStage.Around(azimuth, 2f));
                    yield return stage.Wait(1.0);
                    stage.Recorder.Segment(cues[c] + "@" + azimuth, "pan", start, start + 0.95,
                        P("cue", cues[c], "azimuth", azimuth, "distance", 2f, "signal", "cue"));
                }
            }
            stage.Finish();
        }

        // ------------------------------------------------------------------ 02
        [UnityTest]
        public IEnumerator S02_DistanceAttenuation()
        {
            stage = AudioEvidenceStage.Begin("s02_distance");
            AlfaAudioDirector director = stage.InstantiateAudioRoot("Evidence_Audio");
            AudioEmitterPool pool = director.Emitters;
            AudioClip probe = AudioEvidenceSignals.PinkNoise("probe_distance", 1.0f);
            stage.Own(probe);
            stage.StartRecording();
            yield return stage.Wait(0.5);
            stage.Recorder.Segment("noise_floor", "silence", 0.05, 0.45);

            string[] templates = { "StrikeImpact", "HumanFootstep", "DoorOpen", "MosquitoWingLoop", "MosquitoPerch", "BiteStarted" };
            float[] distances = { 0.5f, 1f, 2f, 3f, 6f, 10f, 15f, 20f };
            foreach (string id in templates)
            {
                AudioCue template = AudioEvidenceStage.Cue(id);
                AudioCue cue = stage.CloneCue(template, probe, loop: false, volume: 1f, pitch: 1f);
                foreach (float distance in distances)
                {
                    double start = stage.Recorder.Now;
                    pool.Play(cue, AudioEvidenceStage.Around(0, distance));
                    yield return stage.Wait(1.15);
                    stage.Recorder.Segment(id + "@" + distance + "m", "distance", start + 0.1, start + 0.85,
                        P("cue", id, "distance", distance, "minDistance", template.MinimumDistance,
                          "maxDistance", template.MaximumDistance, "group", template.Output != null ? template.Output.name : "none",
                          "signal", "pink"));
                }
            }

            // The real flight loop at the same distances (steady source).
            AudioCue wing = AudioEvidenceStage.Cue("MosquitoWingLoop");
            GameObject follower = stage.Point("WingProbe", AudioEvidenceStage.Around(0, 1));
            foreach (float distance in distances)
            {
                follower.transform.position = AudioEvidenceStage.Around(0, distance);
                double start = stage.Recorder.Now;
                pool.Play(wing, follower.transform.position, follower.transform);
                yield return stage.Wait(1.2);
                stage.Recorder.Segment("MosquitoWingLoop_real@" + distance + "m", "distance", start + 0.15, start + 1.1,
                    P("cue", "MosquitoWingLoop_real", "distance", distance, "minDistance", wing.MinimumDistance,
                      "maxDistance", wing.MaximumDistance, "signal", "cue"));
                pool.Stop(wing, follower.transform);
                yield return stage.Wait(0.25);
            }
            stage.Finish();
        }

        // ------------------------------------------------------------------ 03
        private sealed class WingState
        {
            public AudioCue Cue;
            public Transform Follow;
            public bool WasAudible;
            public int Restarts;
        }

        // Mirrors GameplayAudioPresenter.UpdateWingLoop (called once per snapshot).
        private static void UpdateWing(WingState state, AudioCue wing, AudioEmitterPool pool, bool audible)
        {
            AudioCue desired = audible ? wing : null;
            if (desired == state.Cue)
            {
                if (desired != null) pool.Play(desired, state.Follow.position, state.Follow);
                return;
            }
            if (state.Cue != null) pool.Stop(state.Cue, state.Follow);
            state.Cue = desired;
            if (desired != null) pool.Play(desired, state.Follow.position, state.Follow);
        }

        [UnityTest]
        public IEnumerator S03_MosquitoFlight()
        {
            stage = AudioEvidenceStage.Begin("s03_mosquito_flight");
            AlfaAudioDirector director = stage.InstantiateAudioRoot("Evidence_Audio");
            AudioEmitterPool pool = director.Emitters;
            AudioCue wing = AudioEvidenceStage.Cue("MosquitoWingLoop");
            stage.StartRecording();
            yield return stage.Wait(0.5);
            stage.Recorder.Segment("noise_floor", "silence", 0.05, 0.45);

            Transform mosquito = stage.Point("Mosquito_A", AudioEvidenceStage.Around(0, 1.5f)).transform;
            stage.Recorder.Track("Mosquito_A", mosquito);

            // (a) Orbit, radius 1.5 m, 4 s per turn, two turns.
            double start = stage.Recorder.Now;
            pool.Play(wing, mosquito.position, mosquito);
            yield return stage.Animate(8.0, t =>
            {
                mosquito.position = AudioEvidenceStage.Around((float)(t / 4.0 * 360.0), 1.5f);
            });
            stage.Recorder.Segment("orbit_r1.5", "orbit", start + 0.2, start + 8.0, P("track", "Mosquito_A", "radius", 1.5f, "periodSeconds", 4f));

            // (b) Fly-by 1 m in front, -10 m to +10 m at 5 m/s.
            start = stage.Recorder.Now;
            yield return stage.Animate(4.0, t => mosquito.position = new Vector3(-10f + 5f * (float)t, AudioEvidenceStage.EarHeight, 1f));
            stage.Recorder.Segment("flyby_5mps", "flyby", start, start + 4.0, P("track", "Mosquito_A", "speed", 5f, "closest", 1f));

            // (c) Head-on approach 12 m -> 0.4 m at 3 m/s, hover, retreat.
            start = stage.Recorder.Now;
            yield return stage.Animate(3.87, t => mosquito.position = AudioEvidenceStage.Around(0, 12f - 3f * (float)t));
            yield return stage.Wait(1.0);
            yield return stage.Animate(3.87, t => mosquito.position = AudioEvidenceStage.Around(0, 0.4f + 3f * (float)t));
            stage.Recorder.Segment("approach_hover_retreat", "approach", start, stage.Recorder.Now, P("track", "Mosquito_A", "speed", 3f));
            pool.Stop(wing, mosquito);
            yield return stage.Wait(0.6);
            stage.Recorder.Untrack("Mosquito_A");

            // (d) Swarm of nine through the presenter rule (nearest six inside 8 m, updated at 30 Hz).
            // Five always inside 8 m; #5 oscillates 7.3-8.7 m (crosses the 8 m cut); #6 oscillates 5.6-8.4 m and
            // competes with #5 for the sixth slot; the last two stay outside.
            float[] radii = { 1.2f, 2f, 3f, 4.5f, 5.5f, 8.0f, 7.0f, 10f, 12f };
            var states = new WingState[radii.Length];
            var candidates = new List<MosquitoBuzzCandidate>();
            var selected = new HashSet<uint>();
            for (int i = 0; i < radii.Length; i++)
            {
                states[i] = new WingState { Follow = stage.Point("Swarm_" + i, AudioEvidenceStage.Around(i * 40f, radii[i])).transform };
                stage.Recorder.Track("Swarm_" + i, states[i].Follow);
            }
            start = stage.Recorder.Now;
            int frame = 0;
            yield return stage.Animate(10.0, t =>
            {
                for (int i = 0; i < radii.Length; i++)
                {
                    float radius = radii[i] + (i == 5 ? 0.7f * Mathf.Sin((float)t * 2f * Mathf.PI * 0.7f) :
                        i == 6 ? 1.4f * Mathf.Sin((float)t * 2f * Mathf.PI * 0.45f + 1f) : 0f);
                    states[i].Follow.position = AudioEvidenceStage.Around(i * 40f + (float)t * (25f + 7f * i), radius, AudioEvidenceStage.EarHeight + 0.3f * Mathf.Sin((float)t + i));
                }
                if ((frame++ & 1) != 0) return; // 30 Hz snapshots
                candidates.Clear();
                for (int i = 0; i < radii.Length; i++)
                    candidates.Add(new MosquitoBuzzCandidate((uint)(i + 1), (states[i].Follow.position - stage.ListenerObject.transform.position).sqrMagnitude));
                MosquitoBuzzPolicy.SelectNearest(candidates, selected);
                for (int i = 0; i < radii.Length; i++)
                {
                    bool audible = selected.Contains((uint)(i + 1));
                    if (audible && !states[i].WasAudible) { states[i].Restarts++; stage.Recorder.Event("wing_start", "{\"mosquito\":" + i + "}"); }
                    if (!audible && states[i].WasAudible) stage.Recorder.Event("wing_stop", "{\"mosquito\":" + i + "}");
                    states[i].WasAudible = audible;
                    UpdateWing(states[i], wing, pool, audible);
                }
            });
            stage.Recorder.Segment("swarm_9_policy", "swarm", start, stage.Recorder.Now,
                P("mosquitoes", radii.Length, "maxVoices", MosquitoBuzzPolicy.MaximumVoices, "maxDistance", MosquitoBuzzPolicy.MaximumDistance,
                  "boundaryMosquito", 5, "restartsBoundary", states[5].Restarts, "restartsCompetitor", states[6].Restarts));
            for (int i = 0; i < radii.Length; i++) UpdateWing(states[i], wing, pool, false);
            yield return stage.Wait(0.8);
            stage.Recorder.Segment("after_swarm_stop", "silence", stage.Recorder.Now - 0.5, stage.Recorder.Now);
            stage.Finish();
        }

        // ------------------------------------------------------------------ 04
        [UnityTest]
        public IEnumerator S04_SwatterLeftRight()
        {
            stage = AudioEvidenceStage.Begin("s04_swatter");
            AlfaAudioDirector director = stage.InstantiateAudioRoot("Evidence_Audio");
            AudioEmitterPool pool = director.Emitters;
            AudioCue swing = AudioEvidenceStage.Cue("StrikeSwing");
            AudioCue impact = AudioEvidenceStage.Cue("StrikeImpact");
            AudioCue knocked = AudioEvidenceStage.Cue("MosquitoKnockedDown");
            stage.StartRecording();
            yield return stage.Wait(0.5);
            stage.Recorder.Segment("noise_floor", "silence", 0.05, 0.45);

            // Own swing: swatter passes the right side of the head, impact 1 m ahead.
            var cases = new (string label, Vector3 swing, Vector3 hit)[]
            {
                ("own_swing_hit_front", new Vector3(0.35f, 1.5f, 0.3f), new Vector3(0.1f, 1.5f, 1.0f)),
                ("other_human_left_3m", AudioEvidenceStage.Around(-90, 3f), AudioEvidenceStage.Around(-85, 2.6f)),
                ("other_human_right_3m", AudioEvidenceStage.Around(90, 3f), AudioEvidenceStage.Around(85, 2.6f)),
                ("other_human_left_8m", AudioEvidenceStage.Around(-90, 8f), AudioEvidenceStage.Around(-88, 7.6f)),
                ("other_human_right_8m", AudioEvidenceStage.Around(90, 8f), AudioEvidenceStage.Around(88, 7.6f)),
            };
            for (int i = 0; i < cases.Length; i++)
            {
                UnityEngine.Random.InitState(4000);
                double start = stage.Recorder.Now;
                pool.Play(swing, cases[i].swing);
                yield return stage.Wait(0.14);
                pool.Play(impact, cases[i].hit);
                pool.Play(knocked, cases[i].hit);
                yield return stage.Wait(1.4);
                Vector3 local = stage.ListenerObject.transform.InverseTransformPoint(cases[i].hit);
                stage.Recorder.Segment(cases[i].label, "pan", start, start + 1.45,
                    P("cue", "StrikeSwing+StrikeImpact+MosquitoKnockedDown", "azimuth", Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg,
                      "distance", local.magnitude, "signal", "cue"));
            }
            stage.Finish();
        }

        // ------------------------------------------------------------------ 05
        [UnityTest]
        public IEnumerator S05_Footsteps()
        {
            stage = AudioEvidenceStage.Begin("s05_footsteps");
            AlfaAudioDirector director = stage.InstantiateAudioRoot("Evidence_Audio");
            AudioEmitterPool pool = director.Emitters;
            stage.StartRecording();
            yield return stage.Wait(0.5);
            stage.Recorder.Segment("noise_floor", "silence", 0.05, 0.45);

            var materials = new (string material, string step, string land)[]
            {
                ("wood", "HumanFootstep", "HumanLand"), ("tile", "HumanFootstepTile", "HumanLandTile"), ("cloth", "HumanFootstepCloth", "HumanLandCloth")
            };
            foreach (var entry in materials)
            {
                AudioCue step = AudioEvidenceStage.Cue(entry.step);
                // Own walk (first person): feet 1.55 m below the ear, alternating sides, 2 steps/s.
                double start = stage.Recorder.Now;
                for (int i = 0; i < 8; i++)
                {
                    pool.Play(step, new Vector3((i & 1) == 0 ? -0.12f : 0.12f, 0.05f, 0.1f));
                    yield return stage.Wait(0.5);
                }
                stage.Recorder.Segment("own_walk_" + entry.material, "footsteps", start, stage.Recorder.Now,
                    P("cue", entry.step, "material", entry.material, "steps", 8, "interval", 0.5f, "who", "self"));

                // Another human walking left->right 3 m ahead.
                start = stage.Recorder.Now;
                for (int i = 0; i < 8; i++)
                {
                    pool.Play(step, new Vector3(-2.8f + i * 0.8f, 0.05f, 3f));
                    yield return stage.Wait(0.5);
                }
                stage.Recorder.Segment("other_walk_" + entry.material, "footsteps", start, stage.Recorder.Now,
                    P("cue", entry.step, "material", entry.material, "steps", 8, "interval", 0.5f, "who", "other_3m"));

                start = stage.Recorder.Now;
                pool.Play(AudioEvidenceStage.Cue("HumanJump"), new Vector3(0, 0.9f, 0.1f));
                yield return stage.Wait(0.55);
                pool.Play(AudioEvidenceStage.Cue(entry.land), new Vector3(0, 0.05f, 0.1f));
                yield return stage.Wait(0.9);
                stage.Recorder.Segment("jump_land_" + entry.material, "footsteps", start, stage.Recorder.Now,
                    P("cue", "HumanJump+" + entry.land, "material", entry.material, "who", "self"));
            }
            stage.Finish();
        }

        // ------------------------------------------------------------------ 06
        [UnityTest]
        public IEnumerator S06_DoorBehindWall()
        {
            stage = AudioEvidenceStage.Begin("s06_door_occlusion");
            AlfaAudioDirector director = stage.InstantiateAudioRoot("Evidence_Audio");
            AudioEmitterPool pool = director.Emitters;
            AudioCue doorOpen = AudioEvidenceStage.Cue("DoorOpen");
            AudioCue doorClose = AudioEvidenceStage.Cue("DoorClose");
            AudioClip probe = AudioEvidenceSignals.PinkNoise("probe_door", 1.0f);
            stage.Own(probe);
            AudioCue probeCue = stage.CloneCue(doorOpen, probe, loop: false, volume: 1f, pitch: 1f);
            stage.StartRecording();
            yield return stage.Wait(0.5);
            stage.Recorder.Segment("noise_floor", "silence", 0.05, 0.45);
            Vector3 door = AudioEvidenceStage.Around(0, 4f);

            IEnumerator Take(string condition, bool control = false)
            {
                double start = stage.Recorder.Now;
                if (control)
                {
                    // Positive control: what an occluded source with -6 dB and a 1.2 kHz low-pass sounds like.
                    GameObject reference = stage.Point("ControlOccluded", door);
                    var source = reference.AddComponent<AudioSource>();
                    source.clip = probe; source.spatialBlend = 1f; source.dopplerLevel = 0f; source.volume = 0.5f;
                    source.rolloffMode = AudioRolloffMode.Custom; source.maxDistance = doorOpen.MaximumDistance;
                    source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, Get<AnimationCurve>(typeof(AudioEmitterPool), "Rolloff"));
                    source.outputAudioMixerGroup = doorOpen.Output;
                    reference.AddComponent<AudioLowPassFilter>().cutoffFrequency = 1200f;
                    source.Play();
                }
                else pool.Play(probeCue, door);
                yield return stage.Wait(1.2);
                stage.Recorder.Segment("probe_" + condition, "occlusion", start + 0.1, start + 0.85,
                    P("condition", condition, "distance", 4f, "signal", "pink", "control", control));
                if (control) yield break;
                UnityEngine.Random.InitState(6000);
                start = stage.Recorder.Now;
                pool.Play(doorOpen, door);
                yield return stage.Wait(1.1);
                pool.Play(doorClose, door);
                yield return stage.Wait(0.9);
                stage.Recorder.Segment("door_" + condition, "occlusion", start, stage.Recorder.Now,
                    P("condition", condition, "distance", 4f, "signal", "cue", "cue", "DoorOpen+DoorClose"));
            }

            yield return Take("open");
            GameObject wall = stage.Box("Wall_Between", AudioEvidenceStage.Around(0, 2f, 1.5f), new Vector3(6f, 3f, 0.2f));
            yield return Take("wall_between");
            Object.Destroy(wall);
            GameObject room = stage.Room("ClosedRoom_AroundDoor", new Vector3(0, 1.5f, 4.5f), new Vector3(3f, 3f, 3f));
            yield return Take("door_inside_closed_room");
            Object.Destroy(room);
            yield return null;
            yield return Take("control_lowpass_minus6db", control: true);
            stage.Finish();
        }

        private static T Get<T>(Type type, string staticField)
        {
            FieldInfo info = type.GetField(staticField, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.That(info, Is.Not.Null, type.Name + "." + staticField);
            return (T)info.GetValue(null);
        }

        // ------------------------------------------------------------------ 07
        [UnityTest]
        public IEnumerator S07_InteriorExterior()
        {
            stage = AudioEvidenceStage.Begin("s07_interior_exterior");
            AlfaAudioDirector director = stage.InstantiateAudioRoot("Evidence_Audio");
            AudioEmitterPool pool = director.Emitters;
            AudioClip impulse = AudioEvidenceSignals.Impulse("probe_impulse");
            stage.Own(impulse);
            AudioCue impulseCue = stage.CloneCue(AudioEvidenceStage.Cue("StrikeImpact"), impulse, loop: false, volume: 1f, pitch: 1f);
            AudioCue impact = AudioEvidenceStage.Cue("StrikeImpact");
            AudioCue door = AudioEvidenceStage.Cue("DoorClose");
            stage.StartRecording();
            yield return stage.Wait(0.5);
            stage.Recorder.Segment("noise_floor", "silence", 0.05, 0.45);

            IEnumerator Take(string condition)
            {
                double start = stage.Recorder.Now;
                pool.Play(impulseCue, AudioEvidenceStage.Around(20, 3f));
                yield return stage.Wait(1.6);
                stage.Recorder.Segment("impulse_" + condition, "reverb", start, start + 1.55, P("condition", condition, "distance", 3f, "signal", "impulse"));
                UnityEngine.Random.InitState(7000);
                start = stage.Recorder.Now;
                pool.Play(impact, AudioEvidenceStage.Around(20, 3f));
                yield return stage.Wait(1.3);
                stage.Recorder.Segment("impact_" + condition, "reverb", start, start + 1.25, P("condition", condition, "distance", 3f, "signal", "cue", "cue", "StrikeImpact"));
                UnityEngine.Random.InitState(7001);
                start = stage.Recorder.Now;
                pool.Play(door, AudioEvidenceStage.Around(-30, 3f));
                yield return stage.Wait(1.3);
                stage.Recorder.Segment("door_" + condition, "reverb", start, start + 1.25, P("condition", condition, "distance", 3f, "signal", "cue", "cue", "DoorClose"));
            }

            yield return Take("exterior_open");
            GameObject room = stage.Room("Interior_Room", new Vector3(0, 1.5f, 0.5f), new Vector3(7f, 3f, 8f));
            yield return Take("interior_closed_room");
            // Positive control: a Unity reverb zone around the same room (what the game does not have).
            GameObject zoneObject = stage.Point("Control_ReverbZone", new Vector3(0, 1.5f, 0.5f));
            var zone = zoneObject.AddComponent<AudioReverbZone>();
            zone.reverbPreset = AudioReverbPreset.Bathroom;
            zone.minDistance = 6f; zone.maxDistance = 9f;
            yield return stage.Wait(0.2);
            yield return Take("control_reverbzone_bathroom");
            // Same zone, but a plain AudioSource without mixer group: does the zone reach mixer-routed sources?
            {
                GameObject plain = stage.Point("Control_PlainSource_InZone", AudioEvidenceStage.Around(20, 3f));
                var source = plain.AddComponent<AudioSource>();
                source.clip = impulse; source.spatialBlend = 1f; source.dopplerLevel = 0f; source.rolloffMode = AudioRolloffMode.Linear;
                source.minDistance = 1f; source.maxDistance = 30f;
                double start = stage.Recorder.Now;
                source.Play();
                yield return stage.Wait(1.6);
                stage.Recorder.Segment("impulse_control_reverbzone_plain_source", "reverb", start, start + 1.55,
                    P("condition", "control_reverbzone_plain_source", "distance", 3f, "signal", "impulse", "mixerGroup", "none"));
            }
            Object.Destroy(zoneObject);
            Object.Destroy(room);
            yield return stage.Wait(0.2);
            // Positive control of the analyzer: per-source AudioReverbFilter (always inside the captured chain).
            {
                GameObject filtered = stage.Point("Control_ReverbFilter", AudioEvidenceStage.Around(20, 3f));
                var source = filtered.AddComponent<AudioSource>();
                source.clip = impulse; source.spatialBlend = 1f; source.dopplerLevel = 0f; source.rolloffMode = AudioRolloffMode.Linear;
                source.minDistance = 1f; source.maxDistance = 30f;
                source.outputAudioMixerGroup = AudioEvidenceStage.Cue("StrikeImpact").Output;
                filtered.AddComponent<AudioReverbFilter>().reverbPreset = AudioReverbPreset.Bathroom;
                double start = stage.Recorder.Now;
                source.Play();
                yield return stage.Wait(1.6);
                stage.Recorder.Segment("impulse_control_reverbfilter_bathroom", "reverb", start, start + 1.55,
                    P("condition", "control_reverbfilter_bathroom", "distance", 3f, "signal", "impulse"));
            }
            stage.Finish();
        }

        // ------------------------------------------------------------------ 08
        private static readonly (string id, string path, bool menu)[] Maps =
        {
            ("menu-private-lobby", "Assets/LetMeSleep/Content/Environment/AlfaMaps/Prefabs/PrivateLobby.prefab", true),
            ("hf-casa-del-patio-v1", "Assets/LetMeSleep/Content/Environment/HiggsfieldMaps/hf-casa-del-patio-v1/Prefabs/hf-casa-del-patio-v1.prefab", false),
            ("hf-campamento-pinar-v2", "Assets/LetMeSleep/Content/Environment/HiggsfieldMaps/hf-campamento-pinar-v2/Prefabs/hf-campamento-pinar-v2.prefab", false),
            ("hf-puerto-del-faro-v1", "Assets/LetMeSleep/Content/Environment/HiggsfieldMaps/hf-puerto-del-faro-v1/Prefabs/hf-puerto-del-faro-v1.prefab", false),
            ("hf-isla-del-laguito-v2", "Assets/LetMeSleep/Content/Environment/HiggsfieldMaps/hf-isla-del-laguito-v2/Prefabs/hf-isla-del-laguito-v2.prefab", false),
            ("hf-yate-a-la-deriva-v3", "Assets/LetMeSleep/Content/Environment/HiggsfieldMaps/hf-yate-a-la-deriva-v3/Prefabs/hf-yate-a-la-deriva-v3.prefab", false),
        };

        private static readonly string[] SourceHints = { "fire", "fogon", "fogón", "chimen", "hearth", "flame", "water", "lake", "ocean", "sea", "stream", "arroyo", "river", "fountain", "fan", "fridge", "clock", "engine", "motor" };

        [UnityTest]
        public IEnumerator S08_MapAmbience()
        {
            stage = AudioEvidenceStage.Begin("s08_map_ambience");
            AlfaAudioDirector director = stage.InstantiateAudioRoot("Evidence_Audio");
            var presenterObject = new GameObject("Evidence_GameplayAudioPresenter");
            presenterObject.SetActive(false);
            var presenter = presenterObject.AddComponent<GameplayAudioPresenter>();
            stage.Own(presenterObject);
            MethodInfo ensureZones = typeof(GameplayAudioPresenter).GetMethod("EnsureAudioZones", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo resolveGround = typeof(GameplayAudioPresenter).GetMethod("ResolveGroundMaterial", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo zoneList = typeof(GameplayAudioPresenter).GetField("audioZones", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(ensureZones, Is.Not.Null); Assert.That(resolveGround, Is.Not.Null); Assert.That(zoneList, Is.Not.Null);

            stage.StartRecording();
            yield return stage.Wait(0.5);
            stage.Recorder.Segment("noise_floor", "silence", 0.05, 0.45);

            foreach (var entry in Maps)
            {
                GameObject prefab = AudioEvidenceStage.LoadAsset<GameObject>(entry.path);
                GameObject map = Object.Instantiate(prefab);
                map.name = entry.id;
                var definition = map.GetComponent<EnvironmentMapDefinition>();
                Bounds bounds = definition != null ? definition.PlayBounds : new Bounds(Vector3.zero, Vector3.one * 20);
                Transform[] all = map.GetComponentsInChildren<Transform>(true);
                int sources = map.GetComponentsInChildren<AudioSource>(true).Length;
                int reverbZones = map.GetComponentsInChildren<AudioReverbZone>(true).Length;
                int audioZoneNodes = all.Count(t => t.name.StartsWith("AudioZone_", StringComparison.OrdinalIgnoreCase));
                var hints = all.Where(t => SourceHints.Any(h => t.name.IndexOf(h, StringComparison.OrdinalIgnoreCase) >= 0))
                    .Select(t => t.name).Distinct().Take(12).ToArray();

                // Footstep material resolution the presenter would use on this map (cost measured too).
                var list = (System.Collections.IList)zoneList.GetValue(presenter);
                list.Clear();
                long allocated = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();
                var watch = Stopwatch.StartNew();
                const int calls = 20;
                for (int i = 0; i < calls; i++) ensureZones.Invoke(presenter, new object[] { map.transform });
                watch.Stop();
                allocated = Math.Max(0, UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong() - allocated); // lower bound (a GC may run)
                var materials = new Dictionary<string, int>();
                var random = new System.Random(5);
                for (int i = 0; i < 40; i++)
                {
                    var probe = new Vector3(
                        bounds.min.x + (float)random.NextDouble() * bounds.size.x,
                        bounds.min.y + 0.05f,
                        bounds.min.z + (float)random.NextDouble() * bounds.size.z);
                    string material = resolveGround.Invoke(presenter, new object[] { probe }).ToString();
                    materials[material] = materials.TryGetValue(material, out int count) ? count + 1 : 1;
                }

                // Listener in the middle of the map, 1.6 m over the floor of the play bounds.
                stage.ListenerObject.transform.position = new Vector3(bounds.center.x, bounds.min.y + AudioEvidenceStage.EarHeight, bounds.center.z);
                Transform fire = all.FirstOrDefault(t => t.name.IndexOf("Fire_Flame", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        t.name.IndexOf("Fire_FacetedFlame", StringComparison.OrdinalIgnoreCase) >= 0 || t.name.IndexOf("EmberBed", StringComparison.OrdinalIgnoreCase) >= 0)
                    ?? all.FirstOrDefault(t => t.name.IndexOf("Fire", StringComparison.OrdinalIgnoreCase) >= 0 &&
                        t.name.IndexOf("Camera", StringComparison.OrdinalIgnoreCase) < 0 && t.name.IndexOf("Fireplace_Back", StringComparison.OrdinalIgnoreCase) < 0);
                double start = stage.Recorder.Now;
                if (entry.menu) director.EnterMenu(); else director.EnterRound();
                yield return stage.Wait(8.0);
                stage.Recorder.Segment("ambience_" + entry.id, "ambience", start + 1.5, start + 8.0,
                    P("map", entry.id, "context", entry.menu ? "menu" : "round", "audioSources", sources, "reverbZones", reverbZones,
                      "audioZoneNodes", audioZoneNodes, "transforms", all.Length, "emitterHints", hints,
                      "groundMaterials", materials.ToDictionary(p => p.Key, p => (object)p.Value),
                      "ensureZonesMsPerCall", watch.Elapsed.TotalMilliseconds / calls, "ensureZonesBytesPerCall", allocated / calls,
                      "fireNode", fire != null ? fire.name : "none"));
                if (fire != null)
                {
                    stage.ListenerObject.transform.position = fire.position + new Vector3(1.5f, 1.2f, 0f);
                    stage.ListenerObject.transform.LookAt(fire.position + Vector3.up * 0.3f);
                    start = stage.Recorder.Now;
                    yield return stage.Wait(4.0);
                    stage.Recorder.Segment("near_fire_" + entry.id, "local_emitter", start + 0.2, start + 4.0,
                        P("map", entry.id, "node", fire.name, "distance", 1.5f));
                    stage.ListenerObject.transform.rotation = Quaternion.identity;
                }
                director.StopAll();
                yield return stage.Wait(1.2);
                stage.Recorder.Segment("after_stop_" + entry.id, "silence", stage.Recorder.Now - 0.4, stage.Recorder.Now, P("map", entry.id));
                Object.Destroy(map);
                yield return null;
            }
            stage.ListenerObject.transform.position = new Vector3(0, AudioEvidenceStage.EarHeight, 0);
            stage.Finish();
        }

        // ------------------------------------------------------------------ 09
        [UnityTest]
        public IEnumerator S09_MusicFlow()
        {
            stage = AudioEvidenceStage.Begin("s09_music_flow");
            // Two roots like the application: menuAudio (MenuAudioPrefab) and the gameplay presentation's own root.
            AlfaAudioDirector menu = stage.InstantiateAudioRoot("MenuAudio");
            stage.StartRecording();
            yield return stage.Wait(0.5);
            stage.Recorder.Segment("noise_floor", "silence", 0.05, 0.45);

            double t0 = stage.Recorder.Now; stage.Recorder.Event("menu.EnterMenu");
            menu.EnterMenu();
            yield return stage.Wait(14);
            stage.Recorder.Segment("menu", "music", t0 + 1.0, stage.Recorder.Now, P("context", "menu"));
            double menuMusicStart = t0;

            double t = stage.Recorder.Now; stage.Recorder.Event("lobby (menu audio unchanged)");
            yield return stage.Wait(5);
            stage.Recorder.Segment("lobby_waiting_room", "music", t, stage.Recorder.Now, P("context", "lobby"));

            t = stage.Recorder.Now; stage.Recorder.Event("menu.EnterQuietMenu");
            menu.EnterQuietMenu();
            yield return stage.Wait(8);
            stage.Recorder.Segment("quiet_customization", "music", t + 1.0, stage.Recorder.Now, P("context", "quiet"));
            stage.Recorder.Segment("transition_menu_to_quiet", "transition", t - 0.5, t + 1.5, P("from", "menu", "to", "quiet"));

            t = stage.Recorder.Now; stage.Recorder.Event("menu.EnterMenu (back)");
            menu.EnterMenu();
            yield return stage.Wait(6);
            stage.Recorder.Segment("transition_quiet_to_menu", "transition", t - 0.5, t + 1.5, P("from", "quiet", "to", "menu"));

            // PrepareGame: menuAudio.gameObject.SetActive(false); presentation instantiated; BeginGame: EnterRound().
            t = stage.Recorder.Now; stage.Recorder.Event("menuAudio.SetActive(false) + presentation.EnterRound");
            menu.gameObject.SetActive(false);
            AlfaAudioDirector round = stage.InstantiateAudioRoot("GameplayPresentationAudio");
            round.EnterRound();
            yield return stage.Wait(12);
            stage.Recorder.Segment("transition_menu_to_round", "transition", t - 0.5, t + 1.5, P("from", "menu", "to", "round", "mechanism", "SetActive(false)"));
            stage.Recorder.Segment("round_start_sting", "sting", t, t + 3.0, P("cue", "RoundStart"));
            stage.Recorder.Segment("round_bed", "ambience", t + 3.5, stage.Recorder.Now, P("context", "round"));

            t = stage.Recorder.Now; stage.Recorder.Event("SetPublicRoundUrgency(true)");
            round.SetPublicRoundUrgency(true);
            yield return stage.Wait(7);
            stage.Recorder.Segment("urgency_phrase", "music", t, t + 4.8, P("context", "urgency"));
            stage.Recorder.Segment("urgency_end", "transition", t + 3.5, t + 5.5, P("from", "urgency", "to", "round"));

            t = stage.Recorder.Now; stage.Recorder.Event("FinishHumansWin");
            round.FinishHumansWin();
            yield return stage.Wait(5);
            stage.Recorder.Segment("results_humans_win", "sting", t, t + 3.2, P("cue", "HumansWin"));

            // StopGame (presentation.SetActive(false) + Destroy) and LoadMap(false) + menuAudio.SetActive(true) + EnterMenu.
            t = stage.Recorder.Now; stage.Recorder.Event("presentation.SetActive(false) + menuAudio.EnterMenu");
            round.gameObject.SetActive(false);
            Object.Destroy(round.gameObject);
            menu.gameObject.SetActive(true);
            menu.EnterMenu();
            menuMusicStart = stage.Recorder.Now;
            yield return stage.Wait(8);
            stage.Recorder.Segment("transition_results_to_lobby", "transition", t - 0.5, t + 1.5, P("from", "results", "to", "lobby", "mechanism", "SetActive(false)"));
            stage.Recorder.Segment("lobby_after_round", "music", t + 1.0, stage.Recorder.Now, P("context", "lobby"));

            // Stay in the menu across the loop point of the 73.846 s stems.
            double loopAt = menuMusicStart + 73.846;
            yield return stage.Wait(Math.Max(0, loopAt + 3.0 - stage.Recorder.Now));
            stage.Recorder.Segment("menu_loop_seam", "loop_seam", loopAt - 2.0, loopAt + 2.0, P("loopPoint", loopAt, "clip", "MUS_Legacy_Menu_*"));

            t = stage.Recorder.Now; stage.Recorder.Event("menu.StopAll");
            menu.StopAll();
            yield return stage.Wait(3);
            stage.Recorder.Segment("stop_fade", "transition", t - 0.5, t + 1.5, P("from", "menu", "to", "silence"));
            stage.Recorder.Segment("residual_after_stop", "silence", t + 1.2, stage.Recorder.Now);
            stage.Finish();
        }

        // ------------------------------------------------------------------ 10
        [UnityTest]
        public IEnumerator S10_UiClicks()
        {
            stage = AudioEvidenceStage.Begin("s10_ui");
            AlfaAudioDirector menu = stage.InstantiateAudioRoot("MenuAudio");
            stage.StartRecording();
            yield return stage.Wait(0.5);
            stage.Recorder.Segment("noise_floor", "silence", 0.05, 0.45);

            var calls = new (string label, Action play)[]
            {
                ("select", menu.PlayUiSelect), ("confirm", menu.PlayUiConfirm), ("error", menu.PlayUiError),
                ("ready_unused_cue", () => menu.Emitters.Play(AudioEvidenceStage.Cue("UiReady"), Vector3.zero))
            };
            foreach (var call in calls)
            {
                double start = stage.Recorder.Now;
                for (int i = 0; i < 3; i++)
                {
                    call.play();
                    yield return stage.Wait(0.45);
                }
                stage.Recorder.Segment("ui_" + call.label, "ui", start, stage.Recorder.Now, P("cue", call.label, "repeats", 3));
            }
            double burst = stage.Recorder.Now;
            int requested = 0;
            yield return stage.Animate(0.6, elapsed => { while (requested < 12 && elapsed >= requested * 0.03) { menu.PlayUiSelect(); requested++; } });
            yield return stage.Wait(0.4);
            stage.Recorder.Segment("ui_select_burst_30ms", "ui", burst, stage.Recorder.Now, P("cue", "select", "requested", requested, "throttleSeconds", 0.065f));

            double music = stage.Recorder.Now;
            menu.EnterMenu();
            yield return stage.Wait(3.0);
            double clicks = stage.Recorder.Now;
            for (int i = 0; i < 4; i++) { menu.PlayUiSelect(); yield return stage.Wait(0.5); menu.PlayUiConfirm(); yield return stage.Wait(0.5); }
            stage.Recorder.Segment("menu_music_only", "music", music + 1.0, clicks, P("context", "menu"));
            stage.Recorder.Segment("ui_over_menu_music", "ui", clicks, stage.Recorder.Now, P("cue", "select+confirm", "background", "menu"));
            menu.StopAll();
            yield return stage.Wait(1.5);
            stage.Finish();
        }

        // ------------------------------------------------------------------ 11
        private sealed class VoiceLink
        {
            public readonly VoiceImaAdpcmCodec Codec = new VoiceImaAdpcmCodec();
            public readonly VoiceJitterBuffer Jitter = new VoiceJitterBuffer();
            public readonly SyntheticVoice Voice;
            public readonly List<(double arrival, uint seq, byte[] payload)> InFlight = new List<(double, uint, byte[])>();
            public readonly System.Random Random = new System.Random(99);
            public uint Stream;
            public uint Sequence;
            public double SendClock;
            public int Sent, Lost, Accepted, Played, Concealed, Rejected;
            public VoiceLink(float f0) { Voice = new SyntheticVoice(f0); }
        }

        [UnityTest]
        public IEnumerator S11_VoiceProximity()
        {
            stage = AudioEvidenceStage.Begin("s11_voice");
            AudioMixerGroup voiceGroup = stage.Mixer.FindMatchingGroups("Voice").FirstOrDefault();
            Assert.That(voiceGroup, Is.Not.Null, "Voice mixer group");
            // Like VoiceRuntimeCoordinator.RebuildRoutes: AudioSource + VoicePlayoutStream per remote actor.
            var speakerObject = new GameObject("VoicePlayout-2");
            stage.Own(speakerObject);
            speakerObject.AddComponent<AudioSource>();
            var stream = speakerObject.AddComponent<VoicePlayoutStream>();
            stream.Initialize(2, voiceGroup);
            stage.Recorder.Track("VoicePlayout-2", speakerObject.transform);
            var link = new VoiceLink(130f);
            // Probe: samples requested per PCMReaderCallback of a streamed 12 kHz clip (the kind VoicePlayoutStream
            // creates). Large requests against an almost empty ring are filled with zeros = audible gaps and latency.
            var requests = new List<int>();
            var probeObject = new GameObject("PcmReadProbe");
            stage.Own(probeObject);
            var probeSource = probeObject.AddComponent<AudioSource>();
            var probeClip = AudioClip.Create("PcmReadProbe", 12000, 1, 12000, true, data => { lock (requests) requests.Add(data.Length); });
            stage.Own(probeClip);
            probeSource.clip = probeClip; probeSource.loop = true; probeSource.volume = 0.001f; probeSource.spatialBlend = 0f;
            stage.StartRecording();
            probeSource.Play();
            yield return stage.Wait(1.2);
            probeSource.Stop();
            lock (requests) stage.Recorder.SetMeta("pcmReaderRequestSizes", requests.ToArray());
            yield return stage.Wait(0.5);
            stage.Recorder.Segment("noise_floor", "silence", 0.05, 0.45);
            Vector3 listenerFeet = new Vector3(0, 0, 0); // human listener: feet at origin, ear/camera at 1.6 m

            IEnumerator Talk(string label, PlayerRole speakerRole, PlayerRole listenerRole, Vector3 speakerFeet, bool occluded,
                bool spatial = true, double seconds = 2.6, double loss = 0, double jitterSeconds = 0, string notes = null,
                Func<double, Vector3> path = null)
            {
                stream.SetMosquitoTimbre(speakerRole == PlayerRole.Mosquito);
                VoiceSpatialResult result = spatial
                    ? VoiceSpatialPolicy.Evaluate(speakerRole, false, new Float3(speakerFeet.x, speakerFeet.y, speakerFeet.z),
                        listenerRole, false, new Float3(listenerFeet.x, listenerFeet.y, listenerFeet.z), occluded)
                    : VoiceSpatialPolicy.NonSpatialWaitingRoom();
                // Runtime placement: remote.Position + up * .25 (VoiceRuntimeCoordinator.UpdateSpatial).
                speakerObject.transform.position = speakerFeet + Vector3.up * .25f;
                stream.SetSpatial(spatial);
                stream.ApplyAcoustics(result.Gain, result.LowPassHertz <= 0 ? VoiceSpatialPolicy.OpenLowPassHertz : result.LowPassHertz);
                if (!result.Audible) stream.Clear();
                link.Stream++; link.Sequence = 1;
                int sent0 = link.Sent, lost0 = link.Lost, played0 = link.Played, concealed0 = link.Concealed, accepted0 = link.Accepted;
                double start = stage.Recorder.Now;
                link.SendClock = stage.Recorder.Clock;
                double sendEnd = link.SendClock + seconds;
                if (path != null) stage.Recorder.Track("VoiceSpeaker", speakerObject.transform);
                yield return stage.Animate(seconds, elapsed =>
                {
                    if (path != null)
                    {
                        // VoiceRuntimeCoordinator.UpdateSpatial runs every Tick: position, policy and acoustics.
                        speakerFeet = path(elapsed);
                        speakerObject.transform.position = speakerFeet + Vector3.up * .25f;
                        result = VoiceSpatialPolicy.Evaluate(speakerRole, false, new Float3(speakerFeet.x, speakerFeet.y, speakerFeet.z),
                            listenerRole, false, new Float3(listenerFeet.x, listenerFeet.y, listenerFeet.z), occluded);
                        stream.ApplyAcoustics(result.Gain, result.LowPassHertz <= 0 ? VoiceSpatialPolicy.OpenLowPassHertz : result.LowPassHertz);
                    }
                    double now = stage.Recorder.Clock;
                    while (link.SendClock <= now && link.SendClock < sendEnd - 0.02)
                    {
                        byte[] payload = link.Codec.Encode(link.Voice.NextFrame());
                        link.Sent++;
                        double arrival = link.SendClock + 0.030 + (jitterSeconds > 0 ? link.Random.NextDouble() * jitterSeconds : 0);
                        if (loss > 0 && link.Random.NextDouble() < loss) link.Lost++;
                        else link.InFlight.Add((arrival, link.Sequence, payload));
                        link.Sequence++;
                        link.SendClock += 0.020;
                    }
                    link.InFlight.Sort((a, b) => a.arrival.CompareTo(b.arrival));
                    while (link.InFlight.Count > 0 && link.InFlight[0].arrival <= now)
                    {
                        var packet = link.InFlight[0];
                        link.InFlight.RemoveAt(0);
                        if (link.Jitter.AcceptAudio(link.Stream, packet.seq, packet.payload, now)) link.Accepted++;
                        else link.Rejected++;
                    }
                    // VoiceOnlineSession.Tick: up to three frames per tick, concealed frames at .85.
                    for (int emitted = 0; emitted < 3 && link.Jitter.TryDequeue(now, out byte[] frame, out bool concealed); emitted++)
                    {
                        if (!link.Codec.TryDecode(new ArraySegment<byte>(frame), out float[] samples)) continue;
                        if (concealed) link.Concealed++;
                        link.Played++;
                        if (result.Audible) stream.Submit(samples, concealed ? .85f : 1f);
                    }
                });
                link.Jitter.AcceptEnd(link.Stream, link.Sequence, stage.Recorder.Clock);
                link.InFlight.Clear();
                if (path != null) stage.Recorder.Untrack("VoiceSpeaker");
                stage.Recorder.Segment(label, "voice", start, start + seconds - 0.05,
                    P("speaker", speakerRole.ToString(), "listener", listenerRole.ToString(), "distance", (speakerFeet - listenerFeet).magnitude,
                      "azimuth", Mathf.Atan2(speakerFeet.x, speakerFeet.z) * Mathf.Rad2Deg, "occluded", occluded, "spatial", spatial,
                      "policyGain", result.Gain, "policyLowPass", result.LowPassHertz, "audible", result.Audible,
                      "mosquitoTimbre", speakerRole == PlayerRole.Mosquito, "f0", 130f, "loss", loss, "jitterSeconds", jitterSeconds,
                      "framesSent", link.Sent - sent0, "framesLost", link.Lost - lost0, "framesAccepted", link.Accepted - accepted0,
                      "framesPlayed", link.Played - played0, "framesConcealed", link.Concealed - concealed0, "notes", notes ?? "",
                      "track", path != null ? "VoiceSpeaker" : "", "startupWindow", 1.0));
                yield return stage.Wait(0.6);
            }

            yield return Talk("reference_nonspatial_waiting_room", PlayerRole.Human, PlayerRole.Human, new Vector3(0, 0, 2), false, spatial: false);
            foreach (float distance in new[] { 1f, 3f, 6f, 8f, 10f, 11.5f, 14f })
                yield return Talk("human_front_" + distance + "m", PlayerRole.Human, PlayerRole.Human, new Vector3(0, 0, distance), false);
            yield return Talk("human_left_3m", PlayerRole.Human, PlayerRole.Human, new Vector3(-3, 0, 0), false);
            yield return Talk("human_right_3m", PlayerRole.Human, PlayerRole.Human, new Vector3(3, 0, 0), false);
            yield return Talk("human_behind_3m", PlayerRole.Human, PlayerRole.Human, new Vector3(0, 0, -3), false);

            GameObject wall = stage.Box("Wall_Between", new Vector3(0, 1.5f, 1.5f), new Vector3(6f, 3f, 0.2f));
            yield return Talk("human_front_3m_wall_occluded", PlayerRole.Human, PlayerRole.Human, new Vector3(0, 0, 3), true,
                notes: "occluded=true as HasLineOfSight would report");
            Object.Destroy(wall);

            GameObject room = stage.Room("Interior_Room", new Vector3(0, 1.5f, 1f), new Vector3(6f, 3f, 7f));
            yield return Talk("human_front_3m_interior_room", PlayerRole.Human, PlayerRole.Human, new Vector3(0, 0, 3), false,
                notes: "both inside a closed room; no reverb/interior handling exists");
            GameObject zoneObject = stage.Point("Control_ReverbZone", new Vector3(0, 1.5f, 1f));
            var zone = zoneObject.AddComponent<AudioReverbZone>();
            zone.reverbPreset = AudioReverbPreset.Bathroom; zone.minDistance = 5f; zone.maxDistance = 8f;
            yield return Talk("control_human_3m_reverbzone", PlayerRole.Human, PlayerRole.Human, new Vector3(0, 0, 3), false,
                notes: "positive control with an AudioReverbZone");
            Object.Destroy(zoneObject); Object.Destroy(room);

            yield return Talk("mosquito_to_human_3m", PlayerRole.Mosquito, PlayerRole.Human, new Vector3(1.5f, 1.2f, 2.5f), false);
            yield return Talk("mosquito_to_human_7m", PlayerRole.Mosquito, PlayerRole.Human, new Vector3(0, 1.2f, 7f), false);
            yield return Talk("mosquito_to_human_9m", PlayerRole.Mosquito, PlayerRole.Human, new Vector3(0, 1.2f, 9f), false);
            yield return Talk("human_3m_loss5_jitter40ms", PlayerRole.Human, PlayerRole.Human, new Vector3(0, 0, 3), false,
                seconds: 4.0, loss: 0.05, jitterSeconds: 0.040);
            yield return Talk("human_3m_loss15_jitter80ms", PlayerRole.Human, PlayerRole.Human, new Vector3(0, 0, 3), false,
                seconds: 4.0, loss: 0.15, jitterSeconds: 0.080);
            // A mosquito talking while it circles the human at 3 m and 5 m/s (default dopplerLevel on the voice source).
            yield return Talk("mosquito_circling_3m_5mps", PlayerRole.Mosquito, PlayerRole.Human, new Vector3(0, 1.2f, 3f), false,
                seconds: 5.0, path: t => new Vector3(3f * Mathf.Sin((float)t * 5f / 3f), 1.2f, 3f * Mathf.Cos((float)t * 5f / 3f)),
                notes: "moving speaker, policy re-evaluated every frame");
            yield return Talk("human_walking_away_2_to_13m", PlayerRole.Human, PlayerRole.Human, new Vector3(0, 0, 2f), false,
                seconds: 6.0, path: t => new Vector3(0.5f, 0, 2f + 1.8f * (float)t), notes: "crosses the 4 m clear radius and the 12 m cut");
            stage.Recorder.Event("voice_totals", "{\"sent\":" + link.Sent + ",\"lost\":" + link.Lost + ",\"accepted\":" + link.Accepted +
                ",\"rejected\":" + link.Rejected + ",\"played\":" + link.Played + ",\"concealed\":" + link.Concealed + "}");
            stage.Finish();
        }

        // ------------------------------------------------------------------ 12
        [UnityTest]
        public IEnumerator S12_MixStress()
        {
            stage = AudioEvidenceStage.Begin("s12_mix_stress");
            AlfaAudioDirector round = stage.InstantiateAudioRoot("GameplayPresentationAudio");
            AudioEmitterPool pool = round.Emitters;
            AudioCue wing = AudioEvidenceStage.Cue("MosquitoWingLoop");
            stage.StartRecording();
            yield return stage.Wait(0.5);
            stage.Recorder.Segment("noise_floor", "silence", 0.05, 0.45);
            round.EnterRound();
            yield return stage.Wait(3.2);

            var followers = new List<Transform>();
            for (int i = 0; i < 6; i++)
            {
                Transform follow = stage.Point("Wing_" + i, AudioEvidenceStage.Around(i * 60f, 0.8f + i * 0.4f)).transform;
                followers.Add(follow);
                pool.Play(wing, follow.position, follow);
            }
            double start = stage.Recorder.Now;
            round.SetPublicRoundUrgency(true);
            string[] burst = { "StrikeImpact", "StrikeImpact", "BiteStarted", "BiteStarted", "MosquitoKnockedDown", "HumanFainted", "DoorOpen", "StrikeSwing", "ToolDrop", "Recovered" };
            int requested = 0, accepted = 0;
            for (int wave = 0; wave < 4; wave++)
            {
                for (int i = 0; i < burst.Length; i++)
                {
                    requested++;
                    if (pool.Play(AudioEvidenceStage.Cue(burst[i]), AudioEvidenceStage.Around(i * 36f, 0.6f + (i % 3) * 0.5f))) accepted++;
                }
                for (int i = 0; i < 12; i++)
                {
                    requested++;
                    if (pool.Play(AudioEvidenceStage.Cue("HumanFootstep"), new Vector3(-3 + i * 0.5f, 0.05f, 1.5f))) accepted++;
                }
                yield return stage.Wait(0.5);
            }
            yield return stage.Wait(2.0);
            stage.Recorder.Segment("dense_close_combat", "stress", start, stage.Recorder.Now,
                P("requested", requested, "accepted", accepted, "poolCapacity", pool.Capacity, "wingLoops", 6,
                  "realVoices", AudioSettings.GetConfiguration().numRealVoices));
            round.FinishMosquitoesWin();
            yield return stage.Wait(3.5);
            stage.Recorder.Segment("results_over_loops", "sting", stage.Recorder.Now - 3.5, stage.Recorder.Now, P("cue", "MosquitoesWin", "note", "wing loops left running on purpose"));
            double stopAt = stage.Recorder.Now;
            stage.Recorder.Event("round.StopAll (wing loops still playing)");
            round.StopAll();
            yield return stage.Wait(2.2);
            stage.Recorder.Segment("stopall_with_live_loops", "transition", stopAt - 0.5, stopAt + 1.0,
                P("from", "combat+results", "to", "silence", "mechanism", "AlfaAudioDirector.StopAll"));
            stage.Recorder.Segment("residual_after_stopall", "silence", stopAt + 1.2, stage.Recorder.Now);
            stage.Finish();
        }
    }
}
