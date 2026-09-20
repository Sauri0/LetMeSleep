using System;
using UnityEngine;

namespace LetMeSleep.Audio
{
    /// <summary>Push-to-talk microphone owner. No other path calls Microphone.Start.</summary>
    public sealed class VoiceMicrophoneCapture : MonoBehaviour
    {
        private const int RequestedRate = 12000;
        private AudioClip clip;
        private VoiceSampleFramer framer;
        private string activeDevice;
        private int readPosition;
        private float[] readBuffer = new float[2048];
        public event Action<float[]> FrameCaptured;
        public event Action CaptureStopped;
        public bool IsCapturing => clip != null;
        public string ActiveDevice => activeDevice;
        public static string[] Devices => Microphone.devices ?? Array.Empty<string>();

        public bool BeginPushToTalk(string deviceName = null)
        {
            if (!isActiveAndEnabled || clip != null) return false;
            string selected = ResolveDevice(deviceName);
            if (selected == null) return false;
            AudioClip started = Microphone.Start(selected, true, 1, RequestedRate);
            if (started == null) return false;
            activeDevice = selected; clip = started; readPosition = 0;
            framer = new VoiceSampleFramer(clip.frequency);
            framer.FrameReady += OnFrameReady;
            return true;
        }

        public void EndPushToTalk() => StopCapture();

        private void Update()
        {
            if (clip == null) return;
            int writePosition = Microphone.GetPosition(activeDevice);
            if (writePosition < 0) { StopCapture(); return; }
            int available = writePosition >= readPosition ? writePosition - readPosition : clip.samples - readPosition + writePosition;
            while (available > 0)
            {
                int count = Math.Min(available, Math.Min(readBuffer.Length, clip.samples - readPosition));
                if (count <= 0 || !clip.GetData(readBuffer, readPosition)) { StopCapture(); return; }
                framer.Push(readBuffer, 0, count);
                readPosition = (readPosition + count) % clip.samples; available -= count;
            }
        }

        private static string ResolveDevice(string requested)
        {
            string[] devices = Devices;
            if (devices.Length == 0) return null;
            if (string.IsNullOrEmpty(requested)) return devices[0];
            for (int i = 0; i < devices.Length; i++) if (string.Equals(devices[i], requested, StringComparison.Ordinal)) return devices[i];
            return null;
        }

        private void OnFrameReady(float[] samples) => FrameCaptured?.Invoke(samples);
        private void OnApplicationFocus(bool hasFocus) { if (!hasFocus) StopCapture(); }
        private void OnApplicationPause(bool pause) { if (pause) StopCapture(); }
        private void OnDisable() => StopCapture();
        private void OnDestroy() => StopCapture();

        private void StopCapture()
        {
            if (clip == null) return;
            if (!string.IsNullOrEmpty(activeDevice) && Microphone.IsRecording(activeDevice)) Microphone.End(activeDevice);
            if (framer != null) { framer.FrameReady -= OnFrameReady; framer.Clear(); }
            clip = null; framer = null; activeDevice = null; readPosition = 0;
            CaptureStopped?.Invoke();
        }
    }
}
