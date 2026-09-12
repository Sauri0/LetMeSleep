using UnityEngine;

namespace LetMeSleep.Audio
{
    [DisallowMultipleComponent]
    public sealed class AlfaAudioDirector : MonoBehaviour
    {
        [SerializeField] private AudioEmitterPool emitters = null;
        [SerializeField] private AlfaAudioCatalog catalog = null;
        [SerializeField] private AudioBedPlayer menuMusic = null;
        [SerializeField] private AudioBedPlayer roundMusic = null;
        [SerializeField] private AudioBedPlayer ambience = null;

        public AudioEmitterPool Emitters => emitters;
        public AlfaAudioCatalog Catalog => catalog;

        public void EnterMenu()
        {
            roundMusic?.Stop();
            menuMusic?.Play();
            ambience?.Play();
        }

        public void EnterRound()
        {
            menuMusic?.Stop();
            roundMusic?.Play();
            ambience?.Play();
            Play2D(catalog != null ? catalog.RoundStart : null);
        }

        public void FinishHumansWin()
        {
            roundMusic?.Stop();
            Play2D(catalog != null ? catalog.HumansWin : null);
        }

        public void FinishMosquitoesWin()
        {
            roundMusic?.Stop();
            Play2D(catalog != null ? catalog.MosquitoesWin : null);
        }

        public void StopAll()
        {
            menuMusic?.Stop();
            roundMusic?.Stop();
            ambience?.Stop();
            emitters?.StopAllVoices();
        }

        private void Play2D(AudioCue cue)
        {
            if (emitters != null && cue != null)
                emitters.Play(cue, Vector3.zero);
        }
    }
}
