using System.Collections.Generic;
using UnityEngine;

namespace LetMeSleep.Audio
{
    [DisallowMultipleComponent]
    public sealed class AudioEmitterPool : MonoBehaviour
    {
        private sealed class Voice
        {
            public AudioSource Source;
            public AudioCue Cue;
            public Transform Follow;
            public double StartedAt;
            public float FadeRemaining;
            public float FadeStartVolume;
            public bool StopIfFollowMissing;
        }

        private static readonly AnimationCurve Rolloff = new AnimationCurve(
            new Keyframe(0.00f, 1.00f),
            new Keyframe(0.08f, 0.92f),
            new Keyframe(0.25f, 0.68f),
            new Keyframe(0.50f, 0.36f),
            new Keyframe(0.75f, 0.14f),
            new Keyframe(1.00f, 0.00f));

        [SerializeField, Range(1, 64)] private int capacity = 48;
        [SerializeField] private bool warnOnUnroutedCue = true;
        private readonly List<Voice> voices = new List<Voice>(48);
        private readonly HashSet<int> warnedCueIds = new HashSet<int>();

        public int Capacity => capacity;
        public int PlayingCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < voices.Count; i++)
                    if (voices[i].Source.isPlaying) count++;
                return count;
            }
        }

        private void Awake()
        {
            EnsureCapacity();
        }

        private void Update()
        {
            for (int i = 0; i < voices.Count; i++)
            {
                Voice voice = voices[i];
                if (!voice.Source.isPlaying)
                {
                    voice.Cue = null;
                    voice.Follow = null;
                    continue;
                }

                if (voice.Cue != null && voice.Cue.Loop && voice.StopIfFollowMissing &&
                    voice.Follow == null && voice.FadeRemaining <= 0f)
                {
                    voice.FadeStartVolume = voice.Source.volume;
                    voice.FadeRemaining = 0.04f;
                }

                if (voice.FadeRemaining > 0f)
                {
                    voice.FadeRemaining -= Time.unscaledDeltaTime;
                    voice.Source.volume = voice.FadeStartVolume * Mathf.Clamp01(voice.FadeRemaining / 0.04f);
                    if (voice.FadeRemaining <= 0f)
                    {
                        voice.Source.Stop();
                        voice.Cue = null;
                        voice.Follow = null;
                        continue;
                    }
                }

                if (voice.Follow != null)
                    voice.Source.transform.position = voice.Follow.position;
            }
        }

        public bool Play(AudioCue cue, Vector3 position, Transform follow = null)
        {
            if (cue == null || !cue.TrySelectClip(out AudioClip clip))
                return false;

            if (cue.Loop && follow != null && IsPlaying(cue, follow))
                return true;

            if (Count(cue) >= cue.MaximumSimultaneous)
                return false;

            EnsureCapacity();
            Voice voice = FindVoice(cue.Priority);
            if (voice == null)
                return false;

            if (warnOnUnroutedCue && cue.Output == null && warnedCueIds.Add(cue.GetInstanceID()))
                Debug.LogWarning($"LMS_AUDIO_UNROUTED cue={cue.CueId}", cue);

            AudioSource source = voice.Source;
            source.Stop();
            source.clip = clip;
            source.outputAudioMixerGroup = cue.Output;
            source.volume = cue.Volume;
            source.pitch = Random.Range(cue.MinimumPitch, cue.MaximumPitch);
            source.spatialBlend = cue.SpatialBlend;
            source.priority = cue.Priority;
            source.minDistance = cue.MinimumDistance;
            source.maxDistance = cue.MaximumDistance;
            source.rolloffMode = AudioRolloffMode.Custom;
            source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, Rolloff);
            source.loop = cue.Loop;
            source.playOnAwake = false;
            source.dopplerLevel = 0f;
            source.transform.position = follow != null ? follow.position : position;

            voice.Cue = cue;
            voice.Follow = follow;
            voice.StartedAt = AudioSettings.dspTime;
            voice.FadeRemaining = 0f;
            voice.FadeStartVolume = cue.Volume;
            voice.StopIfFollowMissing = follow != null;
            source.Play();
            return true;
        }

        public void Stop(AudioCue cue, Transform follow = null)
        {
            if (cue == null)
                return;
            for (int i = 0; i < voices.Count; i++)
            {
                Voice voice = voices[i];
                if (voice.Cue != cue || !voice.Source.isPlaying ||
                    (follow != null && voice.Follow != follow))
                    continue;
                voice.FadeStartVolume = voice.Source.volume;
                voice.FadeRemaining = 0.04f;
            }
        }

        public void StopAllVoices()
        {
            for (int i = 0; i < voices.Count; i++)
            {
                voices[i].Source.Stop();
                voices[i].Cue = null;
                voices[i].Follow = null;
                voices[i].FadeRemaining = 0f;
            }
        }

        private bool IsPlaying(AudioCue cue, Transform follow)
        {
            for (int i = 0; i < voices.Count; i++)
                if (voices[i].Cue == cue && voices[i].Follow == follow && voices[i].Source.isPlaying)
                    return true;
            return false;
        }

        private int Count(AudioCue cue)
        {
            int count = 0;
            for (int i = 0; i < voices.Count; i++)
                if (voices[i].Cue == cue && voices[i].Source.isPlaying) count++;
            return count;
        }

        private Voice FindVoice(int incomingPriority)
        {
            Voice candidate = null;
            for (int i = 0; i < voices.Count; i++)
            {
                Voice voice = voices[i];
                if (!voice.Source.isPlaying)
                    return voice;
                if (voice.Source.priority < incomingPriority)
                    continue;
                if (candidate == null || voice.Source.priority > candidate.Source.priority ||
                    (voice.Source.priority == candidate.Source.priority && voice.StartedAt < candidate.StartedAt))
                    candidate = voice;
            }
            return candidate;
        }

        private void EnsureCapacity()
        {
            capacity = Mathf.Clamp(capacity, 1, 64);
            while (voices.Count < capacity)
            {
                var child = new GameObject($"Voice_{voices.Count:00}");
                child.transform.SetParent(transform, false);
                var source = child.AddComponent<AudioSource>();
                source.playOnAwake = false;
                voices.Add(new Voice { Source = source });
            }
        }
    }
}
