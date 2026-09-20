using UnityEngine;

namespace LetMeSleep.Audio
{
    [DisallowMultipleComponent]
    public sealed class AlfaAudioDirector : MonoBehaviour
    {
        [SerializeField] private AudioEmitterPool emitters = null;
        [SerializeField] private AlfaAudioCatalog catalog = null;
        [SerializeField] private AudioBedPlayer menuMusic = null;
        [SerializeField] private AudioBedPlayer menuRhythm = null;
        [SerializeField] private AudioBedPlayer menuMelody = null;
        [SerializeField] private AudioBedPlayer roundMusic = null;
        [SerializeField] private AudioBedPlayer roundRhythm = null;
        [SerializeField] private AudioBedPlayer roundMelody = null;
        [SerializeField] private AudioBedPlayer quietMusic = null;
        [SerializeField] private AudioBedPlayer ambience = null;
        [SerializeField] private AudioBedPlayer nightAir = null;
        private AudioContext context;
        private float nextUiCueTime;
        private bool urgencyPlayed;
        private float roundMusicStopAt = -1f;
        // Public urgency gets one short phrase, never a continuous combat bed.
        private const float UrgencyPhraseSeconds = 4f;

        private void Update()
        {
            if (roundMusicStopAt < 0f || Time.unscaledTime < roundMusicStopAt) return;
            roundMusicStopAt = -1f;
            StopBeds(roundMusic, roundRhythm, roundMelody);
        }

        public AudioEmitterPool Emitters => emitters;
        public AlfaAudioCatalog Catalog => catalog;

        public void EnterMenu()
        {
            context = AudioContext.Menu;
            roundMusicStopAt = -1f;
            StopBeds(roundMusic, roundRhythm, roundMelody, quietMusic);
            PlaySynchronized(menuMusic, menuRhythm, menuMelody);
            ambience?.Play();
            nightAir?.Play();
        }

        public void EnterRound()
        {
            bool changed = context != AudioContext.Round;
            context = AudioContext.Round;
            StopBeds(menuMusic, menuRhythm, menuMelody, quietMusic);
            ambience?.Play();
            nightAir?.Play();
            if (changed)
            {
                urgencyPlayed = false;
                roundMusicStopAt = -1f;
                StopBeds(roundMusic, roundRhythm, roundMelody);
                Play2D(catalog != null ? catalog.RoundStart : null);
            }
        }

        public void SetPublicRoundUrgency(bool urgent)
        {
            if (context != AudioContext.Round || !urgent || urgencyPlayed) return;
            urgencyPlayed = true;
            roundRhythm?.SetLevel(.7f);
            roundMelody?.SetLevel(.55f);
            PlaySynchronized(roundMusic, roundRhythm, roundMelody);
            roundMusicStopAt = Time.unscaledTime + UrgencyPhraseSeconds;
        }

        public void EnterQuietMenu()
        {
            context = AudioContext.QuietMenu;
            roundMusicStopAt = -1f;
            StopBeds(menuMusic, menuRhythm, menuMelody, roundMusic, roundRhythm, roundMelody);
            quietMusic?.Play();
            ambience?.Play();
            nightAir?.Play();
        }

        public void FinishHumansWin()
        {
            context = AudioContext.Results;
            roundMusicStopAt = -1f;
            StopBeds(roundMusic, roundRhythm, roundMelody);
            Play2D(catalog != null ? catalog.HumansWin : null);
        }

        public void FinishMosquitoesWin()
        {
            context = AudioContext.Results;
            roundMusicStopAt = -1f;
            StopBeds(roundMusic, roundRhythm, roundMelody);
            Play2D(catalog != null ? catalog.MosquitoesWin : null);
        }

        public void StopAll()
        {
            context = AudioContext.None;
            urgencyPlayed = false;
            roundMusicStopAt = -1f;
            StopBeds(menuMusic, menuRhythm, menuMelody, roundMusic, roundRhythm,
                roundMelody, quietMusic, ambience, nightAir);
            emitters?.StopAllVoices();
        }

        public void PlayUiSelect() => PlayUi(catalog != null ? catalog.UiSelect : null);
        public void PlayUiConfirm() => PlayUi(catalog != null ? catalog.UiConfirm : null);
        public void PlayUiError() => PlayUi(catalog != null ? catalog.UiError : null);

        private static void PlaySynchronized(params AudioBedPlayer[] beds)
        {
            double start = AudioSettings.dspTime + 0.05;
            for (int i = 0; i < beds.Length; i++)
                beds[i]?.PlayScheduled(start);
        }

        private static void StopBeds(params AudioBedPlayer[] beds)
        {
            for (int i = 0; i < beds.Length; i++)
                beds[i]?.Stop();
        }

        private void Play2D(AudioCue cue)
        {
            if (emitters != null && cue != null)
                emitters.Play(cue, Vector3.zero);
        }

        private void PlayUi(AudioCue cue)
        {
            if (Time.unscaledTime < nextUiCueTime)
                return;
            nextUiCueTime = Time.unscaledTime + 0.065f;
            Play2D(cue);
        }

        private enum AudioContext
        {
            None,
            Menu,
            QuietMenu,
            Round,
            Results
        }
    }
}
