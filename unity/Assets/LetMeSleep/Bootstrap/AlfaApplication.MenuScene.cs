using LetMeSleep.Content.Characters;
using LetMeSleep.Presentation;
using UnityEngine;

namespace LetMeSleep.Bootstrap
{
    public sealed partial class AlfaApplication
    {
        public GameObject MenuHumanPrefab, MenuFlyswatterPrefab;
        public AnimationClip MenuSeatedIdle, MenuLook, MenuSwat, MenuReturn, MenuMosquitoFlight;
        private MainMenuLivingScene livingMenu;

        private void CreateLivingMenu()
        {
            var anchors = map.PresentationAnchors;
            var seat = anchors.Find("HumanMenuSeatedRoot");
            var points = new Transform[8];
            for (int i = 0; i < points.Length; i++) points[i] = anchors.Find("MenuMosquitoPath_" + i.ToString("00"));
            // Missing authored poses must be visible in validation, never replaced with a standing idle.
            if (!seat || !MenuSeatedIdle || !MenuLook || !MenuSwat || !MenuReturn || !MenuMosquitoFlight ||
                !MenuHumanPrefab || !MenuFlyswatterPrefab || System.Array.Exists(points, point => !point))
            {
                Debug.LogWarning("LMS_LIVING_MENU_CONTENT_PENDING: seated clips, seat, flight path or tool missing.", this);
                return;
            }

            menuCharacters = new GameObject("MenuCharacterDisplay");
            menuCharacters.SetActive(false);
            menuCharacters.transform.SetParent(map.transform, false);
            var human = CreateDecorativeCharacter(MenuHumanPrefab, seat);
            var mosquito = CreateDecorativeCharacter(MosquitoPrefab, points[0]);
            if (!human || !mosquito || !AttachMenuFlyswatter(human))
            {
                Destroy(menuCharacters); menuCharacters = null;
                return;
            }

            livingMenu = menuCharacters.AddComponent<MainMenuLivingScene>();
            bool configured = livingMenu.Configure(new MainMenuLivingScene.Bindings
            {
                HumanRoot = human.transform, MosquitoRoot = mosquito.transform, HumanSeatRoot = seat,
                HumanAnimator = human.Animator, MosquitoAnimator = mosquito.Animator,
                MenuSeatedIdle = MenuSeatedIdle, MenuLook = MenuLook, MenuSwat = MenuSwat,
                MenuReturn = MenuReturn, Flight = MenuMosquitoFlight,
                FlightPoints = points, WarmLightAnchor = anchors.Find("MenuWarmLight"),
                CoolLightAnchor = anchors.Find("MenuFillLight"), SwatContactNormalized = .5f
            });
            if (!configured) { Destroy(menuCharacters); menuCharacters = null; livingMenu = null; return; }
            livingMenu.SetReducedMotion(settings.ReduceMenuMotion);
            // Fixed camera: room and action occupy the right, leaving the existing navigation rail quiet.
            // Relative offsets keep the composition attached to the real seat if the lobby root moves.
            MenuCamera.transform.position = seat.position + map.transform.TransformVector(new Vector3(-2.4f, 1.5f, -3.85f));
            MenuCamera.transform.LookAt(seat.position + map.transform.TransformVector(new Vector3(-1.2f, 1.1f, -.25f)));
            MenuCamera.fieldOfView = 42f;
            livingMenu.SetSceneActive(true);
            if (ui) PresentPreferences();
        }

        private CharacterView CreateDecorativeCharacter(GameObject prefab, Transform anchor)
        {
            if (!prefab) return null;
            var instance = Instantiate(prefab, anchor.position, anchor.rotation, menuCharacters.transform);
            foreach (var collider in instance.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            var view = instance.GetComponent<CharacterView>();
            if (!view || !view.Animator) { Destroy(instance); return null; }
            ApplyAppearance(view, appearance);
            return view;
        }

        private bool AttachMenuFlyswatter(CharacterView human)
        {
            human.RefreshAnchors();
            var socket = human.GetAnchor("ToolSocket_R");
            if (!socket) { Debug.LogError("LMS_MENU_TOOL_SOCKET_MISSING", human); return false; }
            var instance = Instantiate(MenuFlyswatterPrefab, socket);
            instance.name = "Menu_Flyswatter";
            foreach (var collider in instance.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            var tool = instance.GetComponent<ToolView>();
            if (!tool || !tool.Grip) { Destroy(instance); return false; }
            var delta = socket.rotation * Quaternion.Inverse(tool.Grip.rotation);
            instance.transform.rotation = delta * instance.transform.rotation;
            instance.transform.position += socket.position - tool.Grip.position;
            return true;
        }
    }
}
