using UnityEngine;

namespace LetMeSleep.Audio
{
    [DisallowMultipleComponent]
    public sealed class AlfaAudioCatalog : MonoBehaviour
    {
        [SerializeField] private AudioCue strikeSwing = null;
        [SerializeField] private AudioCue strikeImpact = null;
        [SerializeField] private AudioCue biteStarted = null;
        [SerializeField] private AudioCue mosquitoWingLoop = null;
        [SerializeField] private AudioCue doorOpen = null;
        [SerializeField] private AudioCue doorClose = null;
        [SerializeField] private AudioCue humanFainted = null;
        [SerializeField] private AudioCue recovered = null;
        [SerializeField] private AudioCue roundStart = null;
        [SerializeField] private AudioCue humansWin = null;
        [SerializeField] private AudioCue mosquitoesWin = null;
        [SerializeField] private AudioCue uiReady = null;

        public AudioCue StrikeSwing => strikeSwing;
        public AudioCue StrikeImpact => strikeImpact;
        public AudioCue BiteStarted => biteStarted;
        public AudioCue MosquitoWingLoop => mosquitoWingLoop;
        public AudioCue DoorOpen => doorOpen;
        public AudioCue DoorClose => doorClose;
        public AudioCue HumanFainted => humanFainted;
        public AudioCue Recovered => recovered;
        public AudioCue RoundStart => roundStart;
        public AudioCue HumansWin => humansWin;
        public AudioCue MosquitoesWin => mosquitoesWin;
        public AudioCue UiReady => uiReady;
    }
}
