using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using LetMeSleep.Audio;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Audio;
using Object = UnityEngine.Object;

namespace LetMeSleep.Tests.AudioEvidence
{
    /// <summary>
    /// Shared fixture for scripted listening scenarios. It loads the real generated audio assets
    /// (LMS_AlfaAudioRoot prefab, cues and LMS_AlfaMixer) and applies the game's default preference
    /// volumes, so the recording represents what a player hears with a fresh profile.
    /// Enable with: -runTests ... --lms-audio-evidence &lt;absolute output directory&gt;
    /// Optional: --lms-audio-capture renderer|filter, --lms-audio-mixer defaults|unity
    /// </summary>
    public sealed class AudioEvidenceStage : IDisposable
    {
        public const string AudioRoot = "Assets/LetMeSleep/Audio/Generated";
        public const string CueRoot = AudioRoot + "/Cues/";
        public const string AudioRootPrefab = AudioRoot + "/Prefabs/LMS_AlfaAudioRoot.prefab";
        public const string MixerPath = AudioRoot + "/LMS_AlfaMixer.mixer";
        public const float EarHeight = 1.6f;

        // AlfaApplication.Preferences defaults: Master .8, Music .5, Effects .85 (Ambience/Character/Critical/
        // Mosquito/World/UI) and Voice .8, applied with 20*log10(v) like SetVolume.
        public static readonly Dictionary<string, float> DefaultPreferenceVolumes = new Dictionary<string, float>
        {
            { "Master", .8f }, { "Music", .5f }, { "Voice", .8f }, { "Ambience", .85f }, { "Character", .85f },
            { "Critical", .85f }, { "Mosquito", .85f }, { "World", .85f }, { "UI", .85f }
        };

        private readonly List<Object> owned = new List<Object>();
        private readonly List<AudioListener> disabledListeners = new List<AudioListener>();

        public string Name { get; }
        public string OutputDirectory { get; }
        public GameObject ListenerObject { get; }
        public AudioEvidenceRecorder Recorder { get; }
        public AudioMixer Mixer { get; private set; }

        private AudioEvidenceStage(string name, string outputDirectory)
        {
            Name = name;
            OutputDirectory = outputDirectory;
            foreach (AudioListener existing in Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!existing.enabled) continue;
                existing.enabled = false;
                disabledListeners.Add(existing);
            }
            ListenerObject = new GameObject("AudioEvidence_Listener");
            ListenerObject.transform.SetPositionAndRotation(new Vector3(0f, EarHeight, 0f), Quaternion.identity);
            ListenerObject.AddComponent<AudioListener>();
            Recorder = ListenerObject.AddComponent<AudioEvidenceRecorder>();
            owned.Add(ListenerObject);
        }

        /// <summary>Returns null (and marks the test ignored) unless --lms-audio-evidence was given.</summary>
        public static AudioEvidenceStage Begin(string scenario, int seed = 20260923)
        {
            string directory = ArgumentValue("--lms-audio-evidence");
            if (string.IsNullOrWhiteSpace(directory))
                Assert.Ignore("Audio evidence scenarios run only with --lms-audio-evidence <absolute directory>.");
            Assert.That(Path.IsPathRooted(directory), "--lms-audio-evidence needs an absolute directory.");
            UnityEngine.Random.InitState(seed);
            var stage = new AudioEvidenceStage(scenario, Path.GetFullPath(directory));
            stage.Mixer = LoadAsset<AudioMixer>(MixerPath);
            string mixerMode = ArgumentValue("--lms-audio-mixer") ?? "defaults";
            stage.ApplyMixer(mixerMode);
            stage.Recorder.SetMeta("mixerMode", mixerMode);
            stage.Recorder.SetMeta("seed", seed);
            stage.Recorder.SetMeta("listenerHeight", EarHeight);
            stage.Recorder.SetMeta("gitCommit", Environment.GetEnvironmentVariable("LMS_GIT_COMMIT") ?? "unknown");
            return stage;
        }

        public void StartRecording()
        {
            string requested = ArgumentValue("--lms-audio-capture") ?? "renderer";
            var mode = requested.Equals("filter", StringComparison.OrdinalIgnoreCase)
                ? AudioEvidenceCaptureMode.ListenerFilter : AudioEvidenceCaptureMode.Renderer;
            Recorder.Begin(mode);
            Recorder.SetMeta("requestedCapture", requested);
        }

        public string Finish()
        {
            Recorder.End();
            string path = Recorder.Save(OutputDirectory, Name);
            Debug.Log("LMS_AUDIO_EVIDENCE_WRITTEN " + path);
            return path;
        }

        /// <summary>Advances until `seconds` more of audio have been captured.</summary>
        public IEnumerator Wait(double seconds)
        {
            double target = Recorder.Now + seconds;
            int guard = 0;
            double last = Recorder.Now;
            float lastProgress = Time.realtimeSinceStartup;
            while (Recorder.Now < target - 1e-6)
            {
                yield return null;
                if (Recorder.Now > last) lastProgress = Time.realtimeSinceStartup;
                last = Recorder.Now;
                if (++guard > 2000000 || Time.realtimeSinceStartup - lastProgress > 5f) FailStalled();
            }
        }

        /// <summary>Calls `step(t)` every frame for `seconds` of captured audio, t in [0, seconds].</summary>
        public IEnumerator Animate(double seconds, Action<double> step)
        {
            double start = Recorder.Now;
            double clockStart = Recorder.Clock;
            step(0);
            int guard = 0;
            double last = Recorder.Now;
            float lastProgress = Time.realtimeSinceStartup;
            while (Recorder.Now < start + seconds - 1e-6)
            {
                yield return null;
                step(Math.Min(seconds, Recorder.Clock - clockStart));
                if (Recorder.Now > last) lastProgress = Time.realtimeSinceStartup;
                last = Recorder.Now;
                if (++guard > 2000000 || Time.realtimeSinceStartup - lastProgress > 5f) FailStalled();
            }
        }

        private void FailStalled()
        {
            Recorder.End();
            string path = Recorder.Save(OutputDirectory, Name + "_STALLED");
            Assert.Fail("Recorder stopped advancing (no audio captured for 5 s, mode " + Recorder.Mode + "). Partial: " + path);
        }

        public void ApplyMixer(string mode)
        {
            if (Mixer == null) return;
            foreach (var pair in DefaultPreferenceVolumes)
            {
                float db = mode == "unity" ? 0f : (pair.Value <= .0001f ? -80f : 20f * Mathf.Log10(pair.Value));
                Mixer.SetFloat(pair.Key + "Volume", db);
            }
        }

        public AlfaAudioDirector InstantiateAudioRoot(string name)
        {
            GameObject prefab = LoadAsset<GameObject>(AudioRootPrefab);
#if UNITY_EDITOR
            if (prefab.GetComponent<AlfaAudioDirector>() == null)
            {
                // A Library imported before the Audio scripts compiled can keep a prefab artifact whose
                // MonoBehaviours resolve as missing scripts. Reimport only the audio prefab and cues (Library
                // artifacts; the asset files are not rewritten) and report it in the sidecar.
                const string scriptPath = "Assets/LetMeSleep/Audio/Runtime/AlfaAudioDirector.cs";
                var script = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.MonoScript>(scriptPath);
                var other = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.MonoScript>("Assets/LetMeSleep/Presentation/Gameplay/GameplayAudioPresenter.cs");
                Debug.LogWarning("LMS_AUDIO_EVIDENCE_PREFAB_REIMPORT script=" + (script != null ? script.GetClass()?.FullName ?? "no-class" : "missing") +
                    " presenterScript=" + (other != null ? other.GetClass()?.FullName ?? "no-class" : "missing") +
                    " typeLoaded=" + typeof(AlfaAudioDirector).Assembly.FullName);
                UnityEditor.AssetDatabase.ImportAsset(scriptPath, UnityEditor.ImportAssetOptions.ForceUpdate | UnityEditor.ImportAssetOptions.ForceSynchronousImport);
                script = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.MonoScript>(scriptPath);
                Debug.LogWarning("LMS_AUDIO_EVIDENCE_SCRIPT_AFTER_REIMPORT script=" + (script != null ? script.GetClass()?.FullName ?? "no-class" : "missing"));
                var probeObject = new GameObject("MonoScriptProbe");
                try
                {
                    var added = probeObject.AddComponent<AlfaAudioDirector>();
                    var bound = added != null ? UnityEditor.MonoScript.FromMonoBehaviour(added) : null;
                    Debug.LogWarning("LMS_AUDIO_EVIDENCE_MONOSCRIPT added=" + (added != null) + " monoScript=" +
                        (bound != null ? UnityEditor.AssetDatabase.GetAssetPath(bound) + " guid=" +
                        UnityEditor.AssetDatabase.AssetPathToGUID(UnityEditor.AssetDatabase.GetAssetPath(bound)) : "null"));
                }
                catch (Exception error) { Debug.LogWarning("LMS_AUDIO_EVIDENCE_MONOSCRIPT failed " + error.Message); }
                finally { Object.Destroy(probeObject); }
                UnityEditor.AssetDatabase.ImportAsset(AudioRootPrefab, UnityEditor.ImportAssetOptions.ForceUpdate | UnityEditor.ImportAssetOptions.ForceSynchronousImport);
                UnityEditor.AssetDatabase.ImportAsset(AudioRoot + "/Cues", UnityEditor.ImportAssetOptions.ForceUpdate | UnityEditor.ImportAssetOptions.ImportRecursive | UnityEditor.ImportAssetOptions.ForceSynchronousImport);
                prefab = LoadAsset<GameObject>(AudioRootPrefab);
                Recorder.SetMeta("prefabReimported", true);
            }
#endif
            GameObject instance = Object.Instantiate(prefab);
            instance.name = name;
            owned.Add(instance);
            var director = instance.GetComponent<AlfaAudioDirector>();
            Assert.That(director, Is.Not.Null, "LMS_AlfaAudioRoot must carry AlfaAudioDirector.");
            Recorder.RegisterPool(director.Emitters);
            return director;
        }

        public static AudioCue Cue(string id) => LoadAsset<AudioCue>(CueRoot + id + ".asset");

        /// <summary>Runtime copy of a real cue with the same routing/spatial settings but another clip.</summary>
        public AudioCue CloneCue(AudioCue template, AudioClip clip, bool? loop = null, float? volume = null, float? pitch = null)
        {
            AudioCue cue = ScriptableObject.CreateInstance<AudioCue>();
            cue.name = template.CueId + "_probe";
            owned.Add(cue);
            foreach (FieldInfo field in typeof(AudioCue).GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
                field.SetValue(cue, field.GetValue(template));
            Set(cue, "cueId", template.CueId + "_probe");
            Set(cue, "clips", new[] { clip });
            if (loop.HasValue) Set(cue, "loop", loop.Value);
            if (volume.HasValue) Set(cue, "volume", volume.Value);
            if (pitch.HasValue) { Set(cue, "minimumPitch", pitch.Value); Set(cue, "maximumPitch", pitch.Value); }
            return cue;
        }

        public GameObject Point(string name, Vector3 position)
        {
            var point = new GameObject(name);
            point.transform.position = position;
            owned.Add(point);
            return point;
        }

        /// <summary>Solid box with collider and renderer (a wall/room part). The current audio code ignores it.</summary>
        public GameObject Box(string name, Vector3 center, Vector3 size)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.position = center;
            box.transform.localScale = size;
            owned.Add(box);
            return box;
        }

        /// <summary>Closed room (floor, ceiling, 4 walls) centred on `center` with inner size `inner`.</summary>
        public GameObject Room(string name, Vector3 center, Vector3 inner, float thickness = .2f)
        {
            var root = new GameObject(name);
            owned.Add(root);
            void Part(string part, Vector3 offset, Vector3 size)
            {
                GameObject box = Box(name + "_" + part, center + offset, size);
                box.transform.SetParent(root.transform, true);
            }
            float t = thickness;
            Part("Floor", new Vector3(0, -inner.y / 2 - t / 2, 0), new Vector3(inner.x + 2 * t, t, inner.z + 2 * t));
            Part("Ceiling", new Vector3(0, inner.y / 2 + t / 2, 0), new Vector3(inner.x + 2 * t, t, inner.z + 2 * t));
            Part("WallN", new Vector3(0, 0, inner.z / 2 + t / 2), new Vector3(inner.x + 2 * t, inner.y, t));
            Part("WallS", new Vector3(0, 0, -inner.z / 2 - t / 2), new Vector3(inner.x + 2 * t, inner.y, t));
            Part("WallE", new Vector3(inner.x / 2 + t / 2, 0, 0), new Vector3(t, inner.y, inner.z));
            Part("WallW", new Vector3(-inner.x / 2 - t / 2, 0, 0), new Vector3(t, inner.y, inner.z));
            return root;
        }

        public void Own(Object item)
        {
            if (item != null) owned.Add(item);
        }

        public static Vector3 Around(float azimuthDegrees, float distance, float height = EarHeight)
        {
            float radians = azimuthDegrees * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(radians) * distance, height, Mathf.Cos(radians) * distance);
        }

        public static T LoadAsset<T>(string path) where T : Object
        {
#if UNITY_EDITOR
            T asset = UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.That(asset, Is.Not.Null, "Missing asset " + path);
            return asset;
#else
            Assert.Ignore("Audio evidence loads project assets through the Editor AssetDatabase.");
            return null;
#endif
        }

        public static string ArgumentValue(string option)
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, option);
            if (index >= 0 && index + 1 < args.Length) return args[index + 1];
            string env = Environment.GetEnvironmentVariable("LMS_" + option.TrimStart('-').Replace('-', '_').ToUpperInvariant());
            return string.IsNullOrWhiteSpace(env) ? null : env;
        }

        public static void Set(object target, string field, object value)
        {
            Type type = target.GetType();
            FieldInfo info = null;
            while (type != null && info == null)
            {
                info = type.GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                type = type.BaseType;
            }
            Assert.That(info, Is.Not.Null, target.GetType().Name + "." + field);
            info.SetValue(target, value);
        }

        public static T Get<T>(object target, string field)
        {
            Type type = target.GetType();
            FieldInfo info = null;
            while (type != null && info == null)
            {
                info = type.GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                type = type.BaseType;
            }
            Assert.That(info, Is.Not.Null, target.GetType().Name + "." + field);
            return (T)info.GetValue(target);
        }

        public void Dispose()
        {
            if (Recorder != null && Recorder.IsRecording) Recorder.End();
            for (int i = owned.Count - 1; i >= 0; i--)
                if (owned[i] != null) Object.Destroy(owned[i]);
            owned.Clear();
            foreach (AudioListener listener in disabledListeners)
                if (listener != null) listener.enabled = true;
            if (Mixer != null) ApplyMixer("defaults");
        }
    }

    /// <summary>Deterministic probe signals (created at runtime, never saved as assets).</summary>
    public static class AudioEvidenceSignals
    {
        /// <summary>Pink noise (Paul Kellet filter), seamless loop via crossfaded ends, RMS about -20 dBFS.</summary>
        public static AudioClip PinkNoise(string name, float seconds, int rate = 48000, int seed = 7)
        {
            int length = Mathf.RoundToInt(seconds * rate);
            var data = new float[length];
            var random = new System.Random(seed);
            double b0 = 0, b1 = 0, b2 = 0, b3 = 0, b4 = 0, b5 = 0, b6 = 0;
            for (int i = 0; i < length; i++)
            {
                double white = random.NextDouble() * 2 - 1;
                b0 = 0.99886 * b0 + white * 0.0555179; b1 = 0.99332 * b1 + white * 0.0750759;
                b2 = 0.96900 * b2 + white * 0.1538520; b3 = 0.86650 * b3 + white * 0.3104856;
                b4 = 0.55000 * b4 + white * 0.5329522; b5 = -0.7616 * b5 - white * 0.0168980;
                data[i] = (float)((b0 + b1 + b2 + b3 + b4 + b5 + b6 + white * 0.5362) * 0.11);
                b6 = white * 0.115926;
            }
            Normalize(data, 0.1f);
            int fade = Mathf.Min(length / 4, rate / 20);
            for (int i = 0; i < fade; i++)
            {
                float w = i / (float)fade;
                data[i] = data[i] * w + data[length - fade + i] * (1 - w);
            }
            Array.Resize(ref data, length - fade);
            var clip = AudioClip.Create(name, data.Length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Short broadband click train (for decay/reverb measurement): one 2 ms burst.</summary>
        public static AudioClip Impulse(string name, int rate = 48000)
        {
            var data = new float[rate / 2];
            var random = new System.Random(3);
            int burst = rate / 500;
            for (int i = 0; i < burst; i++)
                data[i] = (float)((random.NextDouble() * 2 - 1) * 0.5 * (1 - i / (double)burst));
            var clip = AudioClip.Create(name, data.Length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static void Normalize(float[] data, float rms)
        {
            double sum = 0;
            for (int i = 0; i < data.Length; i++) sum += data[i] * data[i];
            float current = (float)Math.Sqrt(sum / Math.Max(1, data.Length));
            if (current <= 0) return;
            float gain = rms / current;
            for (int i = 0; i < data.Length; i++) data[i] = Mathf.Clamp(data[i] * gain, -1f, 1f);
        }
    }

    /// <summary>
    /// Speech-like synthetic signal at the voice rate (12 kHz): glottal harmonic series with moving
    /// formants, 4.5 Hz syllable modulation (never fully silent, so any digital silence in a capture is a
    /// dropout) and fricative bursts above 3.5 kHz. Continuous across frames.
    /// </summary>
    public sealed class SyntheticVoice
    {
        public const int Rate = 12000;
        private readonly System.Random random;
        private readonly float baseF0;
        private double phase;
        private long sample;
        private float noiseState;

        public SyntheticVoice(float f0 = 130f, int seed = 11)
        {
            baseF0 = f0;
            random = new System.Random(seed);
        }

        public float[] NextFrame(int samples = 240, float gain = 0.5f)
        {
            var output = new float[samples];
            for (int i = 0; i < samples; i++, sample++)
            {
                double t = sample / (double)Rate;
                double syllable = (t * 4.5) % 1.0;
                const bool pause = false;
                double envelope = 0.3 + 0.7 * Math.Pow(Math.Sin(Math.PI * syllable), 2);
                double f0 = baseF0 * (1 + 0.08 * Math.Sin(2 * Math.PI * 0.7 * t) + 0.03 * Math.Sin(2 * Math.PI * 5.5 * t));
                phase += f0 / Rate;
                phase -= Math.Floor(phase);
                double vowel = 0.5 + 0.5 * Math.Sin(2 * Math.PI * 0.9 * t);
                double f1 = 450 + 350 * vowel, f2 = 1100 + 900 * (1 - vowel), f3 = 2600;
                double voiced = 0;
                for (int k = 1; k * f0 < Rate / 2 - 200; k++)
                {
                    double frequency = k * f0;
                    double formant = Formant(frequency, f1, 90) + 0.7 * Formant(frequency, f2, 120) + 0.35 * Formant(frequency, f3, 180);
                    double tilt = 1.0 / Math.Sqrt(k);
                    voiced += Math.Sin(2 * Math.PI * k * phase) * tilt * (0.05 + formant);
                }
                // Fricative: differentiated white noise in bursts at syllable onsets (energy mostly > 3 kHz).
                float white = (float)(random.NextDouble() * 2 - 1);
                float hiss = white - noiseState;
                noiseState = white;
                // Fricative onset: raised-cosine burst (no gating steps), about 15 dB under the vowels.
                double fricative = (!pause && syllable < 0.12) ? 0.08 * hiss * (0.5 - 0.5 * Math.Cos(2 * Math.PI * syllable / 0.12)) : 0;
                output[i] = (float)Math.Max(-1, Math.Min(1, gain * (envelope * voiced * 0.22 + fricative)));
            }
            return output;
        }

        private static double Formant(double frequency, double centre, double bandwidth)
        {
            double x = (frequency - centre) / bandwidth;
            return Math.Exp(-0.5 * x * x);
        }
    }
}
