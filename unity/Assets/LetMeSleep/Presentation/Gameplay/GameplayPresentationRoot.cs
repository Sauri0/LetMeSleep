using LetMeSleep.Audio;
using LetMeSleep.Gameplay.Unity;
using UnityEngine;

namespace LetMeSleep.Presentation.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class GameplayPresentationRoot : MonoBehaviour
    {
        [SerializeField] private GameplayRuntime gameplay = null;
        [SerializeField] private LobbyMovementRuntime lobby = null;
        [SerializeField] private GameplayVisualPresenter visuals = null;
        [SerializeField] private LobbyVisualPresenter lobbyVisuals = null;
        [SerializeField] private GameplayAudioPresenter audioEvents = null;
        [SerializeField] private GameplayVfxPresenter vfxEvents = null;
        [SerializeField] private AlfaAudioDirector audioDirector = null;

        private void Start()
        {
            if (gameplay != null)
                Bind(gameplay);
            if (lobby != null)
                Bind(lobby);
        }

        public void Bind(GameplayRuntime runtime)
        {
            gameplay = runtime;
            visuals?.Bind(runtime);
            audioEvents?.Bind(runtime, audioDirector);
            vfxEvents?.Bind(runtime);
            if (runtime != null)
                runtime.UseBuiltInCamera = false;
        }

        public void Bind(LobbyMovementRuntime runtime)
        {
            lobby = runtime;
            lobbyVisuals?.Bind(runtime);
        }
    }
}
