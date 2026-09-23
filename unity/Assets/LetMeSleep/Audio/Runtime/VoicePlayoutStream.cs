using System;
using UnityEngine;
using UnityEngine.Audio;

namespace LetMeSleep.Audio
{
    /// <summary>
    /// Plays one remote speaker in 3D. The AudioSource loops a constant 1.0 clip, so the buffer Unity hands to
    /// <see cref="OnAudioFilterRead"/> carries the source's spatial gains (pan, orientation); the voice is rendered
    /// into it by <see cref="VoicePlayoutEngine"/> at the DSP block size. Distance, occlusion, room reverb and
    /// behind/elevation cues come from <see cref="ApplyEnvironment"/> (local presentation, never authoritative).
    /// Doppler is off (a talking mosquito must not warble), priority is high so voices are never virtualized, and the
    /// source only plays while there is audio, so idle peers do not hold a real voice.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class VoicePlayoutStream : MonoBehaviour
    {
        public const int SampleRate = 12000;
        public const int FrameSamples = 240;
        public const int SourcePriority = 8;
        /// <summary>Mixer group name the voice routes to (defined by the mixer owner, see docs/v030/VOICE-V030.md).</summary>
        public const string MixerGroupName = "Voice";
        private const float StopAfterSilentSeconds = 1.5f;
        private const float SpeakingThreshold = .004f; // ≈ −48 dBFS

        private readonly VoicePlayoutEngine engine = new VoicePlayoutEngine();
        private readonly VoiceConcealer concealer = new VoiceConcealer();
        private readonly VoiceMosquitoTimbre mosquitoTimbre = new VoiceMosquitoTimbre();
        private AudioSource source;
        private AudioClip ownedClip;
        private bool initialized, mosquitoVoice, environmentApplied, releasing, subscribed;
        private float lastVolume = 1f, releaseDeadline;

        public uint ActorId { get; private set; }
        /// <summary>RMS of the most recently submitted frame after volume (mouth animation, talk indicator).</summary>
        public float Level { get; private set; }
        /// <summary>RMS of what the audio thread rendered in its last block (echo guard, meters).</summary>
        public float OutputLevel => engine.OutputLevel;
        public bool IsSpeaking => engine.OutputLevel > SpeakingThreshold;
        public VoicePlayoutEngine Engine => engine;
        public bool IsMosquitoVoice => mosquitoVoice;

        public void Initialize(uint actorId, AudioMixerGroup output = null)
        {
            if (actorId == 0) throw new ArgumentOutOfRangeException(nameof(actorId));
            Clear();
            ActorId = actorId;
            source = GetComponent<AudioSource>();
            ReleaseOwnedClip();
            source.playOnAwake = false; source.loop = true; source.spatialBlend = 1f; source.pitch = 1f; source.volume = 1f;
            source.dopplerLevel = 0f; source.spread = 0f; source.priority = SourcePriority;
            source.reverbZoneMix = 0f; source.bypassReverbZones = true; // the voice renders its own room
            source.outputAudioMixerGroup = output;
            source.rolloffMode = AudioRolloffMode.Custom;
            source.minDistance = 1f; source.maxDistance = 10000f;
            source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, AnimationCurve.Constant(0, 10000f, 1f));
            ownedClip = AudioClip.Create("VoicePlayoutCarrier", SampleRate, 1, SampleRate, false);
            var ones = new float[SampleRate];
            for (int i = 0; i < ones.Length; i++) ones[i] = 1f;
            ownedClip.SetData(ones, 0);
            source.clip = ownedClip;
            // Earlier builds filtered with a component; the engine filters internally now.
            if (TryGetComponent(out AudioLowPassFilter legacyFilter)) legacyFilter.enabled = false;
            engine.OutputRate = AudioSettings.outputSampleRate;
            engine.SetParameters(VoicePlayoutParameters.Open(1f), true);
            environmentApplied = false; lastVolume = 1f; releasing = false;
            initialized = true;
            Subscribe(true);
        }

        public void Submit(float[] samples, float volume) => Submit(samples, volume, false);

        /// <summary>Queues one decoded 20 ms frame. Concealed frames are synthesized from the voice's history.</summary>
        public void Submit(float[] samples, float volume, bool concealed)
        {
            if (!initialized || releasing || samples == null || samples.Length != FrameSamples) return;
            if (float.IsNaN(volume) || float.IsInfinity(volume)) return;
            volume = Mathf.Clamp(volume, 0f, 2f);
            float[] frame = concealed ? concealer.Conceal() : concealer.Accept(samples);
            if (mosquitoVoice) frame = mosquitoTimbre.Process(frame);
            float square = 0f;
            for (int i = 0; i < frame.Length; i++)
            {
                float g = lastVolume + (volume - lastVolume) * ((i + 1) / (float)frame.Length);
                frame[i] = Mathf.Clamp(frame[i], -1f, 1f) * g;
                square += frame[i] * frame[i];
            }
            lastVolume = volume;
            Level = Mathf.Sqrt(square / frame.Length);
            engine.Enqueue(frame, 0, frame.Length);
            EnsurePlaying();
        }

        /// <summary>The speaker's stream closed: flush the timbre latency and let the ring drain with a fade.</summary>
        public void EndStream()
        {
            if (!initialized) return;
            if (mosquitoVoice)
            {
                float[] tail = mosquitoTimbre.Flush();
                for (int i = 0; i < tail.Length; i++) tail[i] *= lastVolume;
                engine.Enqueue(tail, 0, tail.Length);
                mosquitoTimbre.Reset();
            }
            concealer.Reset();
            engine.MarkStreamEnd();
        }

        public void SetMosquitoTimbre(bool enabled)
        {
            if (mosquitoVoice != enabled) mosquitoTimbre.Reset();
            mosquitoVoice = enabled;
            if (source != null) source.pitch = 1f;
        }

        /// <summary>Legacy hook (distance gain and low-pass only). Prefer <see cref="ApplyEnvironment"/>.</summary>
        public void ApplyAcoustics(float gain, float lowPassHertz)
        {
            if (!initialized) return;
            var parameters = VoicePlayoutParameters.Open(Mathf.Clamp(gain, 0f, 2f));
            parameters.LowPassHz = lowPassHertz >= VoicePlayoutEngine.OpenLowPassHz ? VoicePlayoutEngine.OpenLowPassHz : Mathf.Clamp(lowPassHertz, 500f, 22000f);
            ApplyEnvironment(parameters);
        }

        /// <summary>Distance, occlusion, room and direction cues. The first call snaps; later calls glide.</summary>
        public void ApplyEnvironment(in VoicePlayoutParameters parameters)
        {
            if (!initialized) return;
            engine.SetParameters(parameters, !environmentApplied);
            environmentApplied = true;
        }

        public void SetMuted(bool muted)
        {
            if (!initialized) return;
            source.mute = muted;
            if (muted) Clear();
        }

        public void SetSpatial(bool spatial)
        {
            if (!initialized) return;
            source.spatialBlend = spatial ? 1f : 0f;
        }

        /// <summary>Drops queued audio: faded on the audio thread when playing, immediately otherwise.</summary>
        public void Clear()
        {
            concealer.Reset(); mosquitoTimbre.Reset(); Level = 0;
            if (source != null && source.isPlaying && isActiveAndEnabled) engine.RequestFadeClear();
            else engine.ClearNow();
        }

        /// <summary>
        /// Leaving the room: the voice fades out in 5 ms on the audio thread; the object is destroyed once the output,
        /// room reverb tail included, is silent (at most 1.5 s), so nothing is cut.
        /// </summary>
        public void FadeOutAndDestroy()
        {
            if (releasing) return;
            releasing = true;
            if (source == null || !source.isPlaying || !isActiveAndEnabled) { Destroy(gameObject); return; }
            engine.RequestFadeClear();
            releaseDeadline = Time.unscaledTime + 1.5f;
        }

        private void Update()
        {
            if (!initialized) return;
            if (releasing)
            {
                bool silent = !engine.HasPendingAudio && engine.SilentSeconds > .02f;
                if (silent || !source.isPlaying || Time.unscaledTime >= releaseDeadline) Destroy(gameObject);
                return;
            }
            if (!source.isPlaying)
            {
                if (engine.HasPendingAudio) EnsurePlaying();
            }
            else if (!engine.HasPendingAudio && engine.SilentSeconds > StopAfterSilentSeconds) source.Stop();
        }

        private void EnsurePlaying()
        {
            if (source == null || source.isPlaying || !isActiveAndEnabled) return;
            engine.OutputRate = AudioSettings.outputSampleRate;
            source.Play();
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (!initialized) return;
            engine.Render(data, channels);
        }

        private void OnAudioConfigurationChanged(bool deviceWasChanged)
        {
            // New output device or rate: stale queued audio would play as a burst; drop it and follow the new rate.
            engine.OutputRate = AudioSettings.outputSampleRate;
            engine.ClearNow();
        }

        private void Subscribe(bool value)
        {
            if (value == subscribed) return;
            subscribed = value;
            if (value) AudioSettings.OnAudioConfigurationChanged += OnAudioConfigurationChanged;
            else AudioSettings.OnAudioConfigurationChanged -= OnAudioConfigurationChanged;
        }

        private void OnEnable() { if (initialized) Subscribe(true); }
        private void OnDisable() { Subscribe(false); engine.ClearNow(); if (source != null) source.Stop(); }
        private void OnDestroy() { Subscribe(false); engine.ClearNow(); initialized = false; ReleaseOwnedClip(); }

        private void ReleaseOwnedClip()
        {
            AudioClip clip = ownedClip;
            ownedClip = null;
            if (source != null)
            {
                source.Stop();
                if (source.clip == clip) source.clip = null;
            }
            if (clip == null) return;
            if (Application.isPlaying) Destroy(clip);
            else DestroyImmediate(clip);
        }
    }
}
