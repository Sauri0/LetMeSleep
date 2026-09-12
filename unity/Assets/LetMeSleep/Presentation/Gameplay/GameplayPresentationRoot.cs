using LetMeSleep.Audio;
using LetMeSleep.Gameplay.Unity;
using UnityEngine;

namespace LetMeSleep.Presentation.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class GameplayPresentationRoot : MonoBehaviour
    {
        [SerializeField] private GameplayRuntime gameplay = null;
        [SerializeField] private GameplayVisualPresenter visuals = null;
        [SerializeField] private GameplayAudioPresenter audioEvents = null;
        [SerializeField] private AlfaAudioDirector audioDirector = null;

        private void Start()
        {
            if (gameplay != null)
                Bind(gameplay);
        }

        public void Bind(GameplayRuntime runtime)
        {
            gameplay = runtime;
            visuals?.Bind(runtime);
            audioEvents?.Bind(runtime, audioDirector);
            if (runtime != null)
                runtime.UseBuiltInCamera = false;
        }
    }
}
