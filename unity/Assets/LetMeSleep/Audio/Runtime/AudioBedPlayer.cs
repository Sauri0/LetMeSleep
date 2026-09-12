using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

namespace LetMeSleep.Audio
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class AudioBedPlayer : MonoBehaviour
    {
        [SerializeField] private AudioClip clip = null;
        [SerializeField] private AudioMixerGroup output = null;
        [SerializeField, Range(0f, 1f)] private float volume = 0.5f;
        [SerializeField, Min(0f)] private float fadeSeconds = 0.75f;
        [SerializeField] private bool playOnStart = false;
        [SerializeField] private bool spatial = false;
        private AudioSource source;
        private Coroutine fade;

        public AudioClip Clip => clip;

        private void Awake()
        {
            ConfigureSource();
        }

        private void Start()
        {
            if (playOnStart)
                Play();
        }

        public void Play()
        {
            ConfigureSource();
            if (clip == null)
                return;
            source.clip = clip;
            source.volume = fadeSeconds > 0f ? 0f : volume;
            if (!source.isPlaying)
                source.Play();
            StartFade(volume, false);
        }

        public void Stop()
        {
            if (source == null || !source.isPlaying)
                return;
            StartFade(0f, true);
        }

        private void ConfigureSource()
        {
            if (source == null)
                source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.outputAudioMixerGroup = output;
            source.spatialBlend = spatial ? 1f : 0f;
            source.dopplerLevel = 0f;
        }

        private void StartFade(float target, bool stopAfter)
        {
            if (fade != null)
                StopCoroutine(fade);
            fade = StartCoroutine(FadeTo(target, stopAfter));
        }

        private IEnumerator FadeTo(float target, bool stopAfter)
        {
            float start = source.volume;
            if (fadeSeconds <= 0f)
            {
                source.volume = target;
            }
            else
            {
                float elapsed = 0f;
                while (elapsed < fadeSeconds)
                {
                    elapsed += Time.unscaledDeltaTime;
                    source.volume = Mathf.Lerp(start, target, Mathf.Clamp01(elapsed / fadeSeconds));
                    yield return null;
                }
                source.volume = target;
            }
            if (stopAfter)
                source.Stop();
            fade = null;
        }
    }
}
