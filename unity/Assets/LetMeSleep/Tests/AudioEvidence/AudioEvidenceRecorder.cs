using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using LetMeSleep.Audio;
using Unity.Collections;
using UnityEngine;

namespace LetMeSleep.Tests.AudioEvidence
{
    public enum AudioEvidenceCaptureMode
    {
        /// <summary>Offline, frame-locked render of the listener mix (AudioRenderer). Deterministic, no device needed.</summary>
        Renderer,
        /// <summary>Real-time tap of the listener mix (OnAudioFilterRead on the AudioListener object).</summary>
        ListenerFilter
    }

    /// <summary>
    /// Records exactly what the active AudioListener hears (all AudioSources, mixer groups and listener
    /// filters) into a 32-bit float WAV plus a JSON sidecar with segments, events, per-frame voice counts
    /// and source tracks. Evidence only: it never changes game code or assets.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AudioEvidenceRecorder : MonoBehaviour
    {
        public const int CaptureFrameRate = 60;

        private readonly object gate = new object();
        private readonly List<string> segmentJson = new List<string>();
        private readonly List<string> eventJson = new List<string>();
        private readonly List<string> frameJson = new List<string>();
        private readonly Dictionary<string, Transform> tracked = new Dictionary<string, Transform>();
        private readonly Dictionary<string, Vector3> lastTrackPosition = new Dictionary<string, Vector3>();
        private readonly Dictionary<string, List<string>> trackJson = new Dictionary<string, List<string>>();
        private readonly List<AudioEmitterPool> pools = new List<AudioEmitterPool>();
        private readonly Dictionary<string, object> meta = new Dictionary<string, object>();
        private float[] buffer = new float[48000 * 2 * 20];
        private long written; // interleaved floats
        private volatile bool recording;
        private volatile bool filterTap;
        private int channels = 2;
        private int sampleRate = 48000;
        private int rendererFrames;
        private int emptyRenderFrames;
        private float previousCaptureDelta;
        private string fallbackFrom;
        private int previousTargetFrameRate;

        public AudioEvidenceCaptureMode Mode { get; private set; }
        public int SampleRate => sampleRate;
        public int Channels => channels;
        public bool IsRecording => recording;

        /// <summary>Seconds of audio captured so far (sample-accurate in Renderer mode).</summary>
        public double Now
        {
            get
            {
                lock (gate) return channels == 0 ? 0 : written / (double)channels / sampleRate;
            }
        }

        /// <summary>
        /// Smooth scenario clock in seconds since capture start: sample-exact in Renderer mode, wall clock in
        /// ListenerFilter mode (the audio-thread sample count advances in DSP-buffer steps, which would make moving
        /// sources jump and fake doppler/velocity).
        /// </summary>
        public double Clock
        {
            get
            {
                if (Mode == AudioEvidenceCaptureMode.Renderer || !recording) return Now;
                return Time.realtimeSinceStartupAsDouble - clockOrigin;
            }
        }

        private double clockOrigin;

        public void RegisterPool(AudioEmitterPool pool)
        {
            if (pool != null && !pools.Contains(pool)) pools.Add(pool);
        }

        public void SetMeta(string key, object value) => meta[key] = value;

        public void Track(string name, Transform target)
        {
            tracked[name] = target;
            if (!trackJson.ContainsKey(name)) trackJson[name] = new List<string>();
            if (target != null) lastTrackPosition[name] = target.position;
        }

        public void Untrack(string name)
        {
            tracked.Remove(name);
            lastTrackPosition.Remove(name);
        }

        public bool Begin(AudioEvidenceCaptureMode requested)
        {
            if (recording) throw new InvalidOperationException("Recorder already running.");
            sampleRate = AudioSettings.outputSampleRate;
            channels = ChannelCount(AudioSettings.speakerMode);
            written = 0;
            Mode = requested;
            previousCaptureDelta = Time.captureDeltaTime;
            if (requested == AudioEvidenceCaptureMode.Renderer)
            {
                Time.captureDeltaTime = 1f / CaptureFrameRate;
                if (!AudioRenderer.Start())
                {
                    Time.captureDeltaTime = previousCaptureDelta;
                    Mode = AudioEvidenceCaptureMode.ListenerFilter;
                }
            }
            if (Mode == AudioEvidenceCaptureMode.ListenerFilter)
            {
                if (GetComponent<AudioListener>() == null)
                    throw new InvalidOperationException("ListenerFilter capture needs the recorder on the AudioListener object.");
                filterTap = true;
            }
            clockOrigin = Time.realtimeSinceStartupAsDouble;
            previousTargetFrameRate = Application.targetFrameRate;
            Application.targetFrameRate = 240; // real-time capture: keep frames short but do not spin
            recording = true;
            var config = AudioSettings.GetConfiguration();
            string diagnostics = "LMS_AUDIO_EVIDENCE_BEGIN mode=" + Mode + " requested=" + requested + " rate=" + sampleRate +
                " speakerMode=" + AudioSettings.speakerMode + " driverCaps=" + AudioSettings.driverCapabilities +
                " dsp=" + config.dspBufferSize + " real=" + config.numRealVoices + " batch=" + Application.isBatchMode +
                " dspTime=" + AudioSettings.dspTime.ToString("0.000", CultureInfo.InvariantCulture);
            Debug.Log(diagnostics);
            SetMeta("beginDiagnostics", diagnostics);
            Event("recording_started");
            return true;
        }

        public void End()
        {
            if (!recording) return;
            Event("recording_stopped");
            recording = false;
            filterTap = false;
            Application.targetFrameRate = previousTargetFrameRate;
            if (Mode == AudioEvidenceCaptureMode.Renderer)
            {
                AudioRenderer.Stop();
                Time.captureDeltaTime = previousCaptureDelta;
            }
        }

        public void Event(string label, string detailJson = null)
        {
            eventJson.Add("{\"t\":" + F(Now) + ",\"label\":" + Q(label) + (detailJson != null ? ",\"detail\":" + detailJson : "") + "}");
        }

        /// <summary>Declares a measured interval. `parameters` is a flat map of numbers/strings/bools.</summary>
        public void Segment(string label, string kind, double start, double end, IDictionary<string, object> parameters = null)
        {
            var text = new StringBuilder();
            text.Append("{\"label\":").Append(Q(label)).Append(",\"kind\":").Append(Q(kind))
                .Append(",\"start\":").Append(F(start)).Append(",\"end\":").Append(F(end));
            if (parameters != null) text.Append(",\"params\":").Append(Json(parameters));
            text.Append('}');
            segmentJson.Add(text.ToString());
        }

        private void LateUpdate()
        {
            if (!recording) return;
            if (Mode == AudioEvidenceCaptureMode.Renderer)
            {
                int frames = AudioRenderer.GetSampleCountForCaptureFrame();
                if (rendererFrames + emptyRenderFrames < 3)
                    Debug.Log("LMS_AUDIO_EVIDENCE_RENDER frame=" + (rendererFrames + emptyRenderFrames) + " samples=" + frames +
                        " dt=" + Time.deltaTime.ToString("0.0000", CultureInfo.InvariantCulture) +
                        " dspTime=" + AudioSettings.dspTime.ToString("0.000", CultureInfo.InvariantCulture));
                if (frames > 0)
                {
                    var native = new NativeArray<float>(frames * channels, Allocator.Temp);
                    try
                    {
                        if (AudioRenderer.Render(native)) Append(native);
                        else emptyRenderFrames++;
                    }
                    finally { native.Dispose(); }
                    rendererFrames++;
                }
                else emptyRenderFrames++;
                if (rendererFrames == 0 && emptyRenderFrames >= 30)
                {
                    // Unity 6000.3 in -batchmode: AudioRenderer.Start succeeds but never reports samples.
                    // Fall back to the real-time listener tap so the scenario still records the true mix.
                    AudioRenderer.Stop();
                    Time.captureDeltaTime = previousCaptureDelta;
                    Mode = AudioEvidenceCaptureMode.ListenerFilter;
                    fallbackFrom = "Renderer (0 samples after 30 frames)";
                    SetMeta("captureFallback", fallbackFrom);
                    Debug.LogWarning("LMS_AUDIO_EVIDENCE_FALLBACK renderer produced no samples; using OnAudioFilterRead");
                    filterTap = true;
                    clockOrigin = Time.realtimeSinceStartupAsDouble - Now;
                    Event("capture_fallback_to_listener_filter");
                }
            }
            SampleTelemetry();
        }

        private void OnAudioFilterRead(float[] data, int dataChannels)
        {
            if (!filterTap || !recording || dataChannels != channels) return;
            lock (gate)
            {
                EnsureCapacity(written + data.Length);
                Array.Copy(data, 0, buffer, written, data.Length);
                written += data.Length;
            }
        }

        private void Append(NativeArray<float> data)
        {
            lock (gate)
            {
                EnsureCapacity(written + data.Length);
                NativeArray<float>.Copy(data, 0, buffer, (int)written, data.Length);
                written += data.Length;
            }
        }

        private void EnsureCapacity(long needed)
        {
            if (needed <= buffer.Length) return;
            long size = buffer.Length;
            while (size < needed) size *= 2;
            if (size > int.MaxValue) throw new InvalidOperationException("Capture too long.");
            Array.Resize(ref buffer, (int)size);
        }

        private void SampleTelemetry()
        {
            AudioSource[] sources = FindObjectsByType<AudioSource>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            int playing = 0, audible = 0, virtualized = 0;
            for (int i = 0; i < sources.Length; i++)
            {
                if (!sources[i].isPlaying) continue;
                playing++;
                if (sources[i].isVirtual) virtualized++;
                else audible++;
            }
            int poolPlaying = 0;
            for (int i = 0; i < pools.Count; i++) if (pools[i] != null) poolPlaying += pools[i].PlayingCount;
            frameJson.Add("[" + F(Now) + "," + F(AudioSettings.dspTime) + "," + F(Time.unscaledTime) + "," +
                playing + "," + audible + "," + virtualized + "," + poolPlaying + "]");

            Transform listener = transform;
            foreach (var pair in tracked)
            {
                Transform target = pair.Value;
                if (target == null) continue;
                Vector3 position = target.position;
                Vector3 local = listener.InverseTransformPoint(position);
                float distance = local.magnitude;
                float azimuth = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
                float elevation = distance > 1e-4f ? Mathf.Asin(Mathf.Clamp(local.y / distance, -1f, 1f)) * Mathf.Rad2Deg : 0f;
                float radial = 0f;
                if (lastTrackPosition.TryGetValue(pair.Key, out Vector3 previous) && Time.deltaTime > 0f)
                {
                    float previousDistance = (previous - listener.position).magnitude;
                    radial = (distance - previousDistance) / Time.deltaTime;
                }
                lastTrackPosition[pair.Key] = position;
                trackJson[pair.Key].Add("[" + F(Now) + "," + F(azimuth) + "," + F(elevation) + "," + F(distance) + "," + F(radial) + "]");
            }
        }

        /// <summary>Writes &lt;directory&gt;/&lt;name&gt;.wav (float32) and &lt;name&gt;.json. Returns the WAV path.</summary>
        public string Save(string directory, string name)
        {
            Directory.CreateDirectory(directory);
            string wav = Path.Combine(directory, name + ".wav");
            float[] copy;
            long count;
            lock (gate)
            {
                count = written;
                copy = buffer;
            }
            WriteFloatWav(wav, copy, count, channels, sampleRate);

            var json = new StringBuilder();
            json.Append("{\n\"schema\":\"lms-audio-evidence/1\",\n\"scenario\":").Append(Q(name))
                .Append(",\n\"captureMode\":").Append(Q(Mode.ToString()))
                .Append(",\n\"sampleRate\":").Append(sampleRate)
                .Append(",\n\"channels\":").Append(channels)
                .Append(",\n\"captureFrameRate\":").Append(Mode == AudioEvidenceCaptureMode.Renderer ? CaptureFrameRate : 0)
                .Append(",\n\"durationSeconds\":").Append(F(count / (double)channels / sampleRate))
                .Append(",\n\"rendererFrames\":").Append(rendererFrames)
                .Append(",\n\"emptyRenderFrames\":").Append(emptyRenderFrames)
                .Append(",\n\"unityVersion\":").Append(Q(Application.unityVersion))
                .Append(",\n\"speakerMode\":").Append(Q(AudioSettings.speakerMode.ToString()))
                .Append(",\n\"dspBufferSize\":").Append(DspBufferSize())
                .Append(",\n\"realVoices\":").Append(AudioSettings.GetConfiguration().numRealVoices)
                .Append(",\n\"virtualVoices\":").Append(AudioSettings.GetConfiguration().numVirtualVoices)
                .Append(",\n\"meta\":").Append(Json(meta))
                .Append(",\n\"segments\":[\n").Append(string.Join(",\n", segmentJson)).Append("\n]")
                .Append(",\n\"events\":[\n").Append(string.Join(",\n", eventJson)).Append("\n]")
                .Append(",\n\"frameColumns\":[\"t\",\"dspTime\",\"unscaledTime\",\"playingSources\",\"audibleSources\",\"virtualSources\",\"poolPlaying\"]")
                .Append(",\n\"frames\":[\n").Append(string.Join(",\n", frameJson)).Append("\n]")
                .Append(",\n\"trackColumns\":[\"t\",\"azimuthDeg\",\"elevationDeg\",\"distance\",\"radialVelocity\"]")
                .Append(",\n\"tracks\":{");
            bool first = true;
            foreach (var pair in trackJson)
            {
                json.Append(first ? "\n" : ",\n").Append(Q(pair.Key)).Append(":[").Append(string.Join(",", pair.Value)).Append(']');
                first = false;
            }
            json.Append("\n}\n}\n");
            File.WriteAllText(Path.Combine(directory, name + ".json"), json.ToString(), new UTF8Encoding(false));
            return wav;
        }

        private static int DspBufferSize()
        {
            AudioSettings.GetDSPBufferSize(out int length, out _);
            return length;
        }

        private void OnDestroy()
        {
            if (recording) End();
        }

        internal static int ChannelCount(AudioSpeakerMode mode)
        {
            switch (mode)
            {
                case AudioSpeakerMode.Mono: return 1;
                case AudioSpeakerMode.Stereo: return 2;
                case AudioSpeakerMode.Quad: return 4;
                case AudioSpeakerMode.Surround: return 5;
                case AudioSpeakerMode.Mode5point1: return 6;
                case AudioSpeakerMode.Mode7point1: return 8;
                case AudioSpeakerMode.Prologic: return 2;
                default: return 2;
            }
        }

        internal static void WriteFloatWav(string path, float[] data, long count, int channels, int rate)
        {
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (var writer = new BinaryWriter(stream))
            {
                long bytes = count * 4;
                writer.Write(Encoding.ASCII.GetBytes("RIFF"));
                writer.Write((uint)(4 + 8 + 18 + 8 + 4 + 8 + bytes));
                writer.Write(Encoding.ASCII.GetBytes("WAVE"));
                writer.Write(Encoding.ASCII.GetBytes("fmt "));
                writer.Write(18u);
                writer.Write((ushort)3); // IEEE float
                writer.Write((ushort)channels);
                writer.Write((uint)rate);
                writer.Write((uint)(rate * channels * 4));
                writer.Write((ushort)(channels * 4));
                writer.Write((ushort)32);
                writer.Write((ushort)0);
                writer.Write(Encoding.ASCII.GetBytes("fact"));
                writer.Write(4u);
                writer.Write((uint)(count / Math.Max(1, channels)));
                writer.Write(Encoding.ASCII.GetBytes("data"));
                writer.Write((uint)bytes);
                var chunk = new byte[65536];
                long offset = 0;
                while (offset < count)
                {
                    int floats = (int)Math.Min(chunk.Length / 4, count - offset);
                    Buffer.BlockCopy(data, (int)(offset * 4), chunk, 0, floats * 4);
                    writer.Write(chunk, 0, floats * 4);
                    offset += floats;
                }
            }
        }

        internal static string F(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return "null";
            return value.ToString("0.######", CultureInfo.InvariantCulture);
        }

        internal static string Q(string value)
        {
            if (value == null) return "null";
            var text = new StringBuilder("\"");
            foreach (char c in value)
            {
                if (c == '"' || c == '\\') text.Append('\\').Append(c);
                else if (c < ' ') text.Append("\\u").Append(((int)c).ToString("x4"));
                else text.Append(c);
            }
            return text.Append('"').ToString();
        }

        internal static string Json(IDictionary<string, object> values)
        {
            var text = new StringBuilder("{");
            bool first = true;
            foreach (var pair in values)
            {
                if (!first) text.Append(',');
                first = false;
                text.Append(Q(pair.Key)).Append(':').Append(Value(pair.Value));
            }
            return text.Append('}').ToString();
        }

        private static string Value(object value)
        {
            switch (value)
            {
                case null: return "null";
                case bool b: return b ? "true" : "false";
                case string s: return Q(s);
                case int i: return i.ToString(CultureInfo.InvariantCulture);
                case long l: return l.ToString(CultureInfo.InvariantCulture);
                case uint u: return u.ToString(CultureInfo.InvariantCulture);
                case float f: return F(f);
                case double d: return F(d);
                case Vector3 v: return "[" + F(v.x) + "," + F(v.y) + "," + F(v.z) + "]";
                case IDictionary<string, object> map: return Json(map);
                case System.Collections.IEnumerable list:
                    var items = new List<string>();
                    foreach (object item in list) items.Add(Value(item));
                    return "[" + string.Join(",", items) + "]";
                default: return Q(Convert.ToString(value, CultureInfo.InvariantCulture));
            }
        }
    }
}
