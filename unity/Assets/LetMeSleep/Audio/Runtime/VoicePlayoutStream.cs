using System;
using UnityEngine;

namespace LetMeSleep.Audio
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class VoicePlayoutStream : MonoBehaviour
    {
        private const int SampleRate = 12000;
        private const int BufferSamples = SampleRate;
        private readonly object gate = new object();
        private readonly float[] ring = new float[BufferSamples];
        private AudioSource source;
        private AudioLowPassFilter lowPass;
        private readonly VoiceMosquitoTimbre mosquitoTimbre = new VoiceMosquitoTimbre();
        private int read, write, count;
        private bool initialized, mosquitoVoice;
        public uint ActorId { get; private set; }
        public float Level { get; private set; }

        public void Initialize(uint actorId)
        {
            if (actorId == 0) throw new ArgumentOutOfRangeException(nameof(actorId));
            ActorId = actorId;
            source = GetComponent<AudioSource>();
            source.playOnAwake = false; source.loop = true; source.spatialBlend = 1f; source.pitch = 1f;
            source.clip = AudioClip.Create("VoicePlayout", BufferSamples, 1, SampleRate, true, OnAudioRead, OnAudioSetPosition);
            lowPass = GetComponent<AudioLowPassFilter>();
            if (lowPass == null) lowPass = gameObject.AddComponent<AudioLowPassFilter>();
            lowPass.cutoffFrequency = 22000f; initialized = true; source.Play();
        }

        public void Submit(float[] samples, float volume)
        {
            if (!initialized || samples == null || samples.Length != 240) return;
            if (float.IsNaN(volume) || float.IsInfinity(volume)) return;
            volume = Mathf.Clamp(volume, 0f, 2f);
            float[] presented = mosquitoVoice ? mosquitoTimbre.Process(samples) : samples;
            float square = 0;
            lock (gate)
            {
                for (int i = 0; i < presented.Length; i++)
                {
                    float value = Mathf.Clamp(presented[i], -1f, 1f) * volume;
                    if (count == ring.Length) { read = (read + 1) % ring.Length; count--; }
                    ring[write] = value; write = (write + 1) % ring.Length; count++; square += value * value;
                }
            }
            Level = Mathf.Sqrt(square / samples.Length);
            if (!source.isPlaying) source.Play();
        }

        public void SetMosquitoTimbre(bool enabled)
        {
            mosquitoVoice = enabled; mosquitoTimbre.Reset();
            if (source != null) source.pitch = 1f;
        }

        /// <summary>Presentation hook for distance/door/room acoustics. Values are local and non-authoritative.</summary>
        public void ApplyAcoustics(float gain, float lowPassHertz)
        {
            if (!initialized) return;
            source.volume = Mathf.Clamp(gain, 0f, 2f);
            lowPass.cutoffFrequency = Mathf.Clamp(lowPassHertz, 500f, 22000f);
        }

        public void SetMuted(bool muted)
        {
            if (!initialized) return;
            source.mute = muted;
            if (muted) Clear();
        }

        public void Clear()
        {
            lock (gate) { Array.Clear(ring, 0, ring.Length); read = write = count = 0; }
            mosquitoTimbre.Reset(); Level = 0;
        }

        private void OnAudioRead(float[] data)
        {
            lock (gate)
            {
                for (int i = 0; i < data.Length; i++)
                {
                    if (count > 0) { data[i] = ring[read]; read = (read + 1) % ring.Length; count--; }
                    else data[i] = 0;
                }
            }
        }

        private void OnAudioSetPosition(int position) { }
        private void OnDisable() { Clear(); if (source != null) source.Stop(); }
        private void OnDestroy() { Clear(); if (source != null) { source.Stop(); source.clip = null; } }
    }
}
