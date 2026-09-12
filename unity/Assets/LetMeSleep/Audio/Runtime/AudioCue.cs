using UnityEngine;
using UnityEngine.Audio;

namespace LetMeSleep.Audio
{
    [CreateAssetMenu(menuName = "Let me sleep/Audio/Audio Cue", fileName = "AudioCue")]
    public sealed class AudioCue : ScriptableObject
    {
        [SerializeField] private string cueId = "unset";
        [SerializeField] private AudioClip[] clips = System.Array.Empty<AudioClip>();
        [SerializeField] private AudioMixerGroup output = null;
        [SerializeField, Range(0f, 1f)] private float volume = 1f;
        [SerializeField, Range(0.5f, 2f)] private float minimumPitch = 0.97f;
        [SerializeField, Range(0.5f, 2f)] private float maximumPitch = 1.03f;
        [SerializeField, Range(0f, 1f)] private float spatialBlend = 1f;
        [SerializeField, Min(0.01f)] private float minimumDistance = 0.7f;
        [SerializeField, Min(0.02f)] private float maximumDistance = 18f;
        [SerializeField, Range(0, 256)] private int priority = 80;
        [SerializeField, Min(1)] private int maximumSimultaneous = 4;
        [SerializeField] private bool loop = false;

        public string CueId => cueId;
        public AudioMixerGroup Output => output;
        public float Volume => volume;
        public float MinimumPitch => minimumPitch;
        public float MaximumPitch => maximumPitch;
        public float SpatialBlend => spatialBlend;
        public float MinimumDistance => minimumDistance;
        public float MaximumDistance => maximumDistance;
        public int Priority => priority;
        public int MaximumSimultaneous => maximumSimultaneous;
        public bool Loop => loop;

        public bool TrySelectClip(out AudioClip clip)
        {
            if (clips == null || clips.Length == 0)
            {
                clip = null;
                return false;
            }

            int start = Random.Range(0, clips.Length);
            for (int offset = 0; offset < clips.Length; offset++)
            {
                clip = clips[(start + offset) % clips.Length];
                if (clip != null)
                    return true;
            }

            clip = null;
            return false;
        }

        private void OnValidate()
        {
            maximumPitch = Mathf.Max(minimumPitch, maximumPitch);
            maximumDistance = Mathf.Max(minimumDistance, maximumDistance);
            maximumSimultaneous = Mathf.Max(1, maximumSimultaneous);
        }
    }
}
