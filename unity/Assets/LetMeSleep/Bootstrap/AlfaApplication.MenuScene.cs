using LetMeSleep.Content.Characters;
using LetMeSleep.Presentation;
using UnityEngine;

namespace LetMeSleep.Bootstrap
{
    public sealed partial class AlfaApplication
    {
        public GameObject MenuHumanPrefab, MenuFlyswatterPrefab;
        public AnimationClip MenuSeatedIdle, MenuLook, MenuSwat, MenuReturn, MenuMosquitoFlight;
        // v0.3.0 (UI-06 screen 1): bedroom set placed with the lobby, warm "sala" dressing, and the sleeping menu clips.
        public GameObject MenuBedroomPrefab, LobbyDecorPrefab;
        public AnimationClip MenuSleep, MenuSleepSwat;
        private MainMenuLivingScene livingMenu;

        private MenuBedroomSet MenuBedroom()
        {
            if (!map) return null;
            var set = map.GetComponentInChildren<MenuBedroomSet>(true);
            return set && set.IsComplete ? set : null;
        }

        private void CreateLivingMenu()
        {
            // v0.3.0 (UI-06 screen 1): the human sleeps in the bedroom set when the set and the sleeping clips exist;
            // otherwise the v0.2.0 seated sofa corner of the lobby stays the menu scene.
            var bedroom = MenuBedroom();
            if (bedroom && MenuSleep && MenuSleepSwat && MenuHumanPrefab && MosquitoPrefab && MenuMosquitoFlight)
            {
                CreateSleepingMenu(bedroom);
                return;
            }
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
                HumanAttention = CreateMenuAttention(human), MosquitoAttention = CreateMenuAttention(mosquito),
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

        private void CreateSleepingMenu(MenuBedroomSet bedroom)
        {
            menuCharacters = new GameObject("MenuCharacterDisplay");
            menuCharacters.SetActive(false);
            menuCharacters.transform.SetParent(map.transform, false);
            var human = CreateDecorativeCharacter(MenuHumanPrefab, bedroom.SleeperRoot);
            var mosquito = CreateDecorativeCharacter(MosquitoPrefab, bedroom.MosquitoPath[0]);
            if (!human || !mosquito)
            {
                Destroy(menuCharacters); menuCharacters = null;
                return;
            }
            mosquito.transform.localScale *= bedroom.MosquitoScale;
            // Asleep: no facial attention rig on the human (it would open and move the eyes); the eyelids are held shut.
            SkinnedMeshRenderer eyelids = null; string[] shapes = null;
            var contract = human.GetComponentInChildren<VisualAttentionContract>(true);
            if (contract != null && contract.Rig != null && contract.Rig.Eyelids &&
                contract.Rig.LeftBlinkShapes != null && contract.Rig.RightBlinkShapes != null &&
                contract.Rig.LeftBlinkShapes.Length == 4 && contract.Rig.RightBlinkShapes.Length == 4)
            {
                eyelids = contract.Rig.Eyelids;
                shapes = new string[8];
                contract.Rig.LeftBlinkShapes.CopyTo(shapes, 0);
                contract.Rig.RightBlinkShapes.CopyTo(shapes, 4);
            }
            var mosquitoContract = mosquito.GetComponentInChildren<VisualAttentionContract>(true);
            livingMenu = menuCharacters.AddComponent<MainMenuLivingScene>();
            bool configured = livingMenu.Configure(new MainMenuLivingScene.Bindings
            {
                HumanRoot = human.transform, MosquitoRoot = mosquito.transform, HumanSeatRoot = bedroom.SleeperRoot,
                HumanAnimator = human.Animator, MosquitoAnimator = mosquito.Animator,
                HumanAttention = null, MosquitoAttention = CreateMenuAttention(mosquito),
                MenuSleep = MenuSleep, MenuSleepSwat = MenuSleepSwat, Flight = MenuMosquitoFlight,
                SleepEyelids = eyelids, SleepEyelidShapes = shapes,
                FlightPoints = bedroom.MosquitoPath, HumanReactionAnchor = bedroom.EarAnchor,
                WarmLightAnchor = bedroom.WarmLightAnchor, CoolLightAnchor = bedroom.CoolLightAnchor,
                CycleSeconds = 11f, FirstLookAfterSeconds = 2.5f, LookSeconds = .5f, SwatSeconds = MenuSleepSwat.length,
                ReturnSeconds = .5f, NoticeRadius = 1.2f, SwatRadius = .5f, SwatContactNormalized = .5f,
                SleepSwatCooldownSeconds = 9f, MosquitoFaceTarget = bedroom.MosquitoFaceTarget, MosquitoFacing = bedroom.MosquitoFacing,
                // v0.3.0 r2 (director #2): flattened lids, closed-eye lines, smile, head turned toward the menu camera.
                SleepFace = eyelids ? bedroom.SleepFace : null, SleepHeadBone = contract?.Rig?.Head,
                SleepLeftEye = contract?.Rig?.LeftEye, SleepRightEye = contract?.Rig?.RightEye, SleepFaceToward = bedroom.CameraAnchor,
                SleepEyeForward = contract?.Rig?.EyeForward ?? Vector3.up, SleepHeadForward = contract?.Rig?.HeadForward ?? Vector3.forward,
                SleepHeadUp = contract?.Rig?.HeadUp ?? Vector3.up,
                MosquitoPupils = mosquitoContract?.Rig != null ? new[] { mosquitoContract.Rig.LeftEye, mosquitoContract.Rig.RightEye } : null,
                MosquitoPupilForward = mosquitoContract?.Rig?.EyeForward ?? Vector3.forward, MosquitoPupilScale = bedroom.MosquitoPupilScale,
                MosquitoPupilSmoothness = bedroom.MosquitoPupilSmoothness,
                FlightHoverTime = bedroom.FlightHoverTime, FlightHoverWobble = bedroom.FlightHoverWobble, FlightHoverRate = bedroom.FlightHoverRate
            });
            if (!configured) { Destroy(menuCharacters); menuCharacters = null; livingMenu = null; return; }
            livingMenu.SetReducedMotion(settings.ReduceMenuMotion);
            MenuCamera.transform.SetPositionAndRotation(bedroom.CameraAnchor.position, bedroom.CameraAnchor.rotation);
            MenuCamera.fieldOfView = bedroom.CameraFieldOfView;
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

        private static VisualAttentionRig CreateMenuAttention(CharacterView character)
        {
            if (VisualAttentionFactory.TryInstall(character.gameObject, true, out var rig, out var reason)) return rig;
            Debug.LogWarning("LMS_MENU_FACIAL_PENDING: " + character.name + ": " + reason, character);
            return null;
        }

        private static void ConfigurePreviewAttention(GameObject visual, Camera camera)
        {
            if (!VisualAttentionFactory.TryInstallPreview(visual, camera, out var reason))
                Debug.LogWarning("LMS_PREVIEW_FACIAL_PENDING: " + reason, visual);
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
