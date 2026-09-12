using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace LetMeSleep.UI
{
    public static class AlfaUiRuntime
    {
        public static AlfaUiController Create(IMenuActions actions, AlfaUiDependencies dependencies = null)
        {
            if (actions == null) throw new System.ArgumentNullException(nameof(actions));
            dependencies = dependencies ?? new AlfaUiDependencies();

            EnsureEventSystem();
            var root = new GameObject("AlfaUI", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster), typeof(AlfaUiController));
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            root.GetComponent<UnityEngine.UI.GraphicRaycaster>().blockingObjects = UnityEngine.UI.GraphicRaycaster.BlockingObjects.None;

            if (dependencies.PersistentAcrossScenes) Object.DontDestroyOnLoad(root);
            var controller = root.GetComponent<AlfaUiController>();
            controller.Initialize(actions, dependencies);
            return controller;
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            Object.DontDestroyOnLoad(eventSystem);
        }
    }
}
