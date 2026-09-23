using System;
using UnityEngine;

namespace LetMeSleep.Audio
{
    /// <summary>
    /// Push-to-talk microphone owner. No other path calls Microphone.Start. Frames leave through
    /// <see cref="VoiceCaptureProcessor"/> (high-pass, gate, AGC, limiter, PTT fades).
    /// <see cref="ReleasePushToTalk"/> keeps capturing for the 40 ms release tail and then stops;
    /// <see cref="EndPushToTalk"/> stops at once (mute, focus loss, pause: the microphone closes immediately).
    /// An empty device name means the system default. A device that stops delivering samples (unplugged,
    /// permission denied) stops the capture and raises <see cref="CaptureFailed"/> instead of hanging.
    /// </summary>
    public sealed class VoiceMicrophoneCapture : MonoBehaviour
    {
        public const int PreferredRate = 48000;
        private const float StallSeconds = .75f;
        private const float ReleaseTimeoutSeconds = .3f;
        private AudioClip clip;
        private VoiceSampleFramer framer;
        private readonly VoiceCaptureProcessor processor = new VoiceCaptureProcessor();
        private string activeDevice, lastDevice;
        private bool usingDefault, releaseRequested;
        private int readPosition, lastWritePosition;
        private float lastProgressAt, releaseDeadline;
        private float[] readBuffer = new float[4096];
        private static string[] cachedDevices = Array.Empty<string>();
        private static float cachedDevicesAt = -10f;

        public event Action<float[]> FrameCaptured;
        public event Action CaptureStopped;
        /// <summary>Human-readable reason (Spanish, UI-ready) when the device fails during a transmission.</summary>
        public event Action<string> CaptureFailed;
        public bool IsCapturing => clip != null;
        public bool IsReleasing => clip != null && releaseRequested;
        public string ActiveDevice => activeDevice;
        public bool UsingDefaultDevice => clip != null && usingDefault;
        public VoiceCaptureProcessor Processor => processor;

        public static string[] Devices => Microphone.devices ?? Array.Empty<string>();

        /// <summary>Device list refreshed at most once per second (Microphone.devices allocates).</summary>
        public static string[] CachedDevices
        {
            get
            {
                float now = Time.realtimeSinceStartup;
                if (now - cachedDevicesAt > 1f || now < cachedDevicesAt) { cachedDevices = Devices; cachedDevicesAt = now; }
                return cachedDevices;
            }
        }

        /// <summary>True when the requested device (or, for an empty name, any device) is present.</summary>
        public static bool IsAvailable(string deviceName)
        {
            string[] devices = CachedDevices;
            if (string.IsNullOrEmpty(deviceName)) return devices.Length > 0;
            for (int i = 0; i < devices.Length; i++) if (string.Equals(devices[i], deviceName, StringComparison.Ordinal)) return true;
            return false;
        }

        public bool BeginPushToTalk(string deviceName = null)
        {
            if (!isActiveAndEnabled) return false;
            if (clip != null)
            {
                // Pressed again during the release tail: keep the same transmission going.
                if (!releaseRequested) return false;
                releaseRequested = false;
                processor.BeginTransmission();
                return true;
            }
            string[] devices = Devices;
            if (devices.Length == 0) return false;
            string selected = null;
            if (!string.IsNullOrEmpty(deviceName))
            {
                for (int i = 0; i < devices.Length; i++) if (string.Equals(devices[i], deviceName, StringComparison.Ordinal)) selected = devices[i];
                if (selected == null) return false; // the runtime never switches the user to another device
            }
            int rate = ChooseRate(selected);
            AudioClip started;
            try { started = Microphone.Start(selected, true, 1, rate); }
            catch (Exception) { started = null; }
            if (started == null) return false;
            usingDefault = selected == null;
            activeDevice = selected;
            clip = started; readPosition = 0; lastWritePosition = 0; releaseRequested = false;
            lastProgressAt = Time.realtimeSinceStartup;
            string identity = selected ?? string.Empty;
            if (!string.Equals(identity, lastDevice, StringComparison.Ordinal)) { processor.Reset(true); lastDevice = identity; }
            processor.BeginTransmission();
            framer = new VoiceSampleFramer(clip.frequency);
            framer.FrameReady += OnFrameReady;
            return true;
        }

        /// <summary>Key released: finish with the release fade, then stop and raise <see cref="CaptureStopped"/>.</summary>
        public void ReleasePushToTalk()
        {
            if (clip == null || releaseRequested) return;
            releaseRequested = true;
            releaseDeadline = Time.realtimeSinceStartup + ReleaseTimeoutSeconds;
            processor.BeginRelease();
        }

        /// <summary>Stops immediately (mute, focus loss, pause, rebinding, room exit).</summary>
        public void EndPushToTalk() => StopCapture();

        /// <summary>RMS of remote voices playing locally (raises the gate against speaker→microphone echo).</summary>
        public void SetFarEndLevel(float rms) => processor.SetFarEndLevel(rms);

        private void Update()
        {
            if (clip == null) return;
            float now = Time.realtimeSinceStartup;
            if (!Microphone.IsRecording(activeDevice)) { Fail("Se desconectó el micrófono."); return; }
            int writePosition = Microphone.GetPosition(activeDevice);
            if (writePosition < 0) { Fail("Se desconectó el micrófono."); return; }
            if (writePosition != lastWritePosition) { lastWritePosition = writePosition; lastProgressAt = now; }
            else if (now - lastProgressAt > StallSeconds) { Fail("El micrófono no entrega audio (revisá el permiso de micrófono de Windows)."); return; }
            int available = writePosition >= readPosition ? writePosition - readPosition : clip.samples - readPosition + writePosition;
            while (available > 0 && clip != null)
            {
                int count = Math.Min(available, Math.Min(readBuffer.Length, clip.samples - readPosition));
                if (count <= 0 || !clip.GetData(readBuffer, readPosition)) { Fail("No se pudo leer el micrófono."); return; }
                framer.Push(readBuffer, 0, count);
                if (clip == null) return; // stopped by the release tail inside Push
                readPosition = (readPosition + count) % clip.samples; available -= count;
            }
            if (releaseRequested && clip != null && (processor.Released || now >= releaseDeadline)) StopCapture();
        }

        private static int ChooseRate(string device)
        {
            try
            {
                Microphone.GetDeviceCaps(device, out int minimum, out int maximum);
                if (minimum == 0 && maximum == 0) return PreferredRate;
                return Mathf.Clamp(PreferredRate, Math.Max(8000, minimum), Math.Max(8000, maximum));
            }
            catch (Exception) { return PreferredRate; }
        }

        private void OnFrameReady(float[] samples)
        {
            if (clip == null) return;
            float[] processed = processor.Process(samples);
            FrameCaptured?.Invoke(processed);
            if (releaseRequested && processor.Released) StopCapture();
        }

        private void Fail(string reason)
        {
            StopCapture();
            CaptureFailed?.Invoke(reason);
        }

        private void OnApplicationFocus(bool hasFocus) { if (!hasFocus) StopCapture(); }
        private void OnApplicationPause(bool pause) { if (pause) StopCapture(); }
        private void OnDisable() => StopCapture();
        private void OnDestroy() => StopCapture();

        private void StopCapture()
        {
            if (clip == null) return;
            AudioClip recorded = clip;
            clip = null;
            try { if (Microphone.IsRecording(activeDevice)) Microphone.End(activeDevice); }
            catch (Exception) { }
            if (framer != null) { framer.FrameReady -= OnFrameReady; framer.Clear(); }
            framer = null; activeDevice = null; usingDefault = false; readPosition = 0; releaseRequested = false;
            // Microphone.Start allocates a new clip per press: release it (it leaked 24–48 KB per press before).
            if (recorded != null) { if (Application.isPlaying) Destroy(recorded); else DestroyImmediate(recorded); }
            CaptureStopped?.Invoke();
        }
    }
}
