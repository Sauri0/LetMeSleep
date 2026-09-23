using System.Collections.Generic;
using UnityEngine;

namespace LetMeSleep.Tests.VoicePlayMode
{
    /// <summary>Captures the listener mix (interleaved) for assertions.</summary>
    public sealed class ListenerTap : MonoBehaviour
    {
        private readonly object gate = new object();
        private readonly List<float> samples = new List<float>();
        public int Channels { get; private set; } = 2;
        public bool Recording;

        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (!Recording) return;
            lock (gate) { Channels = channels; samples.AddRange(data); }
        }

        public float[] Take() { lock (gate) { var copy = samples.ToArray(); samples.Clear(); return copy; } }
    }
}
