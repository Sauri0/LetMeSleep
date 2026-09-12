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
        [SerializeField] private AudioCue mosquitoWingPerchLoop = null;
        [SerializeField] private AudioCue mosquitoWingBiteLoop = null;
        [SerializeField] private AudioCue mosquitoPerch = null;
        [SerializeField] private AudioCue mosquitoDetach = null;
        [SerializeField] private AudioCue humanFootstep = null;
        [SerializeField] private AudioCue humanLand = null;
        [SerializeField] private AudioCue mosquitoKnockedDown = null;
        [SerializeField] private AudioCue doorOpen = null;
        [SerializeField] private AudioCue doorClose = null;
        [SerializeField] private AudioCue humanFainted = null;
        [SerializeField] private AudioCue recovered = null;
        [SerializeField] private AudioCue roundStart = null;
        [SerializeField] private AudioCue humansWin = null;
        [SerializeField] private AudioCue mosquitoesWin = null;
        [SerializeField] private AudioCue uiReady = null;
        [SerializeField] private AudioCue uiSelect = null;
        [SerializeField] private AudioCue uiConfirm = null;
        [SerializeField] private AudioCue uiError = null;

        public AudioCue StrikeSwing => strikeSwing;
        public AudioCue StrikeImpact => strikeImpact;
        public AudioCue BiteStarted => biteStarted;
        public AudioCue MosquitoWingLoop => mosquitoWingLoop;
        public AudioCue MosquitoWingPerchLoop => mosquitoWingPerchLoop;
        public AudioCue MosquitoWingBiteLoop => mosquitoWingBiteLoop;
        public AudioCue MosquitoPerch => mosquitoPerch;
        public AudioCue MosquitoDetach => mosquitoDetach;
        public AudioCue HumanFootstep => humanFootstep;
        public AudioCue HumanLand => humanLand;
        public AudioCue MosquitoKnockedDown => mosquitoKnockedDown;
        public AudioCue DoorOpen => doorOpen;
        public AudioCue DoorClose => doorClose;
        public AudioCue HumanFainted => humanFainted;
        public AudioCue Recovered => recovered;
        public AudioCue RoundStart => roundStart;
        public AudioCue HumansWin => humansWin;
        public AudioCue MosquitoesWin => mosquitoesWin;
        public AudioCue UiReady => uiReady;
        public AudioCue UiSelect => uiSelect;
        public AudioCue UiConfirm => uiConfirm;
        public AudioCue UiError => uiError;
    }
}
