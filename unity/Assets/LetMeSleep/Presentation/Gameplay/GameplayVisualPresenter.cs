using System.Collections.Generic;
using LetMeSleep.Content.Characters;
using LetMeSleep.Core;
using LetMeSleep.Gameplay.Unity;
using UnityEngine;
using GameplayModel = LetMeSleep.Gameplay;

namespace LetMeSleep.Presentation.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class GameplayVisualPresenter : MonoBehaviour
    {
        [SerializeField] private GameplayRuntime gameplay = null;
        [SerializeField] private GameObject humanPrefab = null;
        [SerializeField] private GameObject humanFirstPersonPrefab = null;
        [SerializeField] private GameObject mosquitoPrefab = null;
        [SerializeField] private GameObject flyswatterPrefab = null;
        [SerializeField] private GameObject slipperPrefab = null;
        [SerializeField] private GameObject electricRacketPrefab = null;
        [SerializeField] private GameObject aerosolPrefab = null;
        [SerializeField] private HumanViewCamera humanCamera = null;
        [SerializeField] private MosquitoFollowCamera mosquitoCamera = null;

        private readonly Dictionary<uint, ActorVisualBinding> visuals =
            new Dictionary<uint, ActorVisualBinding>();
        private readonly Dictionary<uint, GameObject> pickupVisuals =
            new Dictionary<uint, GameObject>();
        private readonly HashSet<uint> aliveActors = new HashSet<uint>();
        private readonly HashSet<uint> livePickups = new HashSet<uint>();
        private readonly List<uint> removedActors = new List<uint>();
        private readonly List<uint> removedPickups = new List<uint>();
        private readonly HashSet<string> diagnosedToolPrefabs = new HashSet<string>();
        private UnityGameplayWorld subscribedWorld;
        private bool subscribed;
        private GameplayAudioPresenter locomotionAudio;

        public void SetLocomotionAudio(GameplayAudioPresenter audio)
        {
            foreach (var visual in visuals.Values)
            {
                if (!visual) continue;
                var source = visual.GetComponent<HumanLocomotionPresenter>();
                if (!source) continue;
                if (locomotionAudio) locomotionAudio.UnregisterLocomotion(source);
                if (audio) audio.RegisterLocomotion(source);
            }
            locomotionAudio = audio;
        }

        private void RemoveLocomotionAudio(ActorVisualBinding visual)
        {
            if (!visual || !locomotionAudio) return;
            var source = visual.GetComponent<HumanLocomotionPresenter>();
            if (source) locomotionAudio.UnregisterLocomotion(source);
        }

        private void OnEnable() => Subscribe();
        private void Start() => Subscribe();
        private void OnDisable()
        {
            Unsubscribe();
            ClearVisuals();
        }

        private void Update()
        {
            if (subscribed && gameplay && gameplay.World!=subscribedWorld) { Unsubscribe(); ClearVisuals(); }
            Subscribe();
            DriveLocalCamera();
        }

        public void Bind(GameplayRuntime runtime)
        {
            Unsubscribe();
            ClearVisuals();
            gameplay = runtime;
            Subscribe();
        }

        public void SetPrefabs(
            GameObject human, GameObject humanFirstPerson, GameObject mosquito,
            GameObject flyswatter)
            => SetPrefabs(human, humanFirstPerson, mosquito, flyswatter, null, null, null);

        public void SetPrefabs(
            GameObject human, GameObject humanFirstPerson, GameObject mosquito,
            GameObject flyswatter, GameObject slipper, GameObject electricRacket,
            GameObject aerosol)
        {
            humanPrefab = human;
            humanFirstPersonPrefab = humanFirstPerson;
            mosquitoPrefab = mosquito;
            flyswatterPrefab = flyswatter;
            slipperPrefab = slipper;
            electricRacketPrefab = electricRacket;
            aerosolPrefab = aerosol;
            diagnosedToolPrefabs.Clear();
        }

        public void SetCameras(HumanViewCamera human, MosquitoFollowCamera mosquito)
        {
            humanCamera = human;
            mosquitoCamera = mosquito;
        }

        public void ApplySnapshot(GameplayModel.GameSessionState snapshot)
        {
            if (snapshot == null || gameplay == null || gameplay.World == null)
                return;

            aliveActors.Clear();
            for (int i = 0; i < snapshot.Actors.Count; i++)
            {
                GameplayModel.ActorSnapshot state = snapshot.Actors[i];
                if (state.Eliminated) continue;
                aliveActors.Add(state.ActorId);
                if (!gameplay.World.Actors.TryGetValue(state.ActorId, out GameplayActorProxy proxy))
                    continue;
                EnsureVisual(proxy);
                if (visuals.TryGetValue(state.ActorId, out ActorVisualBinding visual) && visual != null)
                    visual.ApplySnapshot(state, snapshot.HostTick);
            }

            removedActors.Clear();
            foreach (KeyValuePair<uint, ActorVisualBinding> pair in visuals)
                if (!aliveActors.Contains(pair.Key) || pair.Value == null)
                    removedActors.Add(pair.Key);
            for (int i = 0; i < removedActors.Count; i++)
            {
                uint actorId = removedActors[i];
                if (visuals[actorId] != null)
                {
                    RemoveLocomotionAudio(visuals[actorId]);
                    Destroy(visuals[actorId].gameObject);
                }
                visuals.Remove(actorId);
            }

            ApplyToolPickups(snapshot.ToolPickups);
        }

        public void ApplyEvent(in GameplayModel.GameplayEvent item)
        {
            if (visuals.TryGetValue(item.SourceActorId, out ActorVisualBinding visual) && visual != null)
                visual.ApplyEvent(in item);
        }

        private void EnsureVisual(GameplayActorProxy proxy)
        {
            if (proxy == null || proxy.State?.Eliminated == true) return;
            if (visuals.TryGetValue(proxy.ActorId,out var existing))
            {
                if(existing && existing.transform.parent==proxy.transform) return;
                if(existing) { RemoveLocomotionAudio(existing); Destroy(existing.gameObject); }
                visuals.Remove(proxy.ActorId);
            }
            bool local = gameplay != null && proxy.ActorId == gameplay.LocalActorId;
            GameObject source = SelectPrefab(proxy.Role, local);
            if (source == null)
            {
                Debug.LogError($"LMS_CHARACTER_PREFAB_MISSING actor={proxy.ActorId} role={proxy.Role}", this);
                return;
            }

            GameObject instance = Instantiate(source, proxy.transform);
            instance.name = $"Visual_{proxy.ActorId}_{proxy.Role}";
            instance.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            CharacterView view = instance.GetComponent<CharacterView>();
            if (view == null)
            {
                Debug.LogError($"LMS_CHARACTER_VIEW_MISSING prefab={source.name}", source);
                Destroy(instance);
                return;
            }

            ActorVisualBinding binding = instance.AddComponent<ActorVisualBinding>();
            binding.Initialize(proxy, gameplay.World, view, local);
            if (proxy.Role == PlayerRole.Human && HumanLocomotionSetup.TryConfigure(view, proxy.ActorId, out var gait))
            {
                binding.BindLocomotion(gait);
                if (locomotionAudio) locomotionAudio.RegisterLocomotion(gait);
            }
            visuals.Add(proxy.ActorId, binding);
            // v0.3.0 night legibility: the map's character rim light (if any) only lights this rendering layer.
            if (proxy.Role == PlayerRole.Mosquito) HiggsfieldRimLight.MarkCharacter(instance);
            if(VisualAttentionFactory.TryInstall(instance,false,out var attention,out var reason))
            {
                var target=instance.GetComponent<GameplayAttentionTarget>();
                if(!target) target=instance.AddComponent<GameplayAttentionTarget>();
                target.Bind(gameplay,proxy,attention);
            }
            else Debug.LogWarning($"LMS_FACIAL_SKIPPED actor={proxy.ActorId}: {reason}",instance);
            if (proxy.Role == PlayerRole.Human)
            {
                binding.BindTool(GameplayModel.GameplayTools.Flyswatter, AttachTool(view, GameplayModel.GameplayTools.Flyswatter));
                binding.BindTool(GameplayModel.GameplayTools.Slipper, AttachTool(view, GameplayModel.GameplayTools.Slipper));
                binding.BindTool(GameplayModel.GameplayTools.ElectricRacket, AttachTool(view, GameplayModel.GameplayTools.ElectricRacket));
                binding.BindTool(GameplayModel.GameplayTools.Aerosol, AttachTool(view, GameplayModel.GameplayTools.Aerosol));
            }
            BindLocalCamera(proxy, view, local);
            if (proxy.State != null)
                binding.ApplySnapshot(proxy.State, gameplay.LatestSnapshot != null ? gameplay.LatestSnapshot.HostTick : 0);
        }

        private GameObject SelectPrefab(PlayerRole role, bool local)
        {
            if (role == PlayerRole.Human)
                return local && humanFirstPersonPrefab != null ? humanFirstPersonPrefab : humanPrefab;
            return role == PlayerRole.Mosquito ? mosquitoPrefab : null;
        }

        private void BindLocalCamera(GameplayActorProxy proxy, CharacterView view, bool local)
        {
            if (!local)
                return;
            if (humanCamera != null) humanCamera.enabled = proxy.Role == PlayerRole.Human;
            if (mosquitoCamera != null) mosquitoCamera.enabled = proxy.Role == PlayerRole.Mosquito;
            Camera camera = humanCamera != null ? humanCamera.GetComponent<Camera>() :
                mosquitoCamera != null ? mosquitoCamera.GetComponent<Camera>() : null;
            if (camera != null) camera.enabled = true;
            AudioListener listener = camera != null ? camera.GetComponent<AudioListener>() : null;
            if (listener != null) listener.enabled = true;
            if (proxy.Role == PlayerRole.Human && humanCamera != null)
                humanCamera.BindEye(view.GetAnchor("CameraEye"), view.RefreshAnchors);
            else if (proxy.Role == PlayerRole.Mosquito && mosquitoCamera != null)
            {
                mosquitoCamera.SetCollisionFilter(gameplay.World.IsWorldCollider);
                mosquitoCamera.BindAnchors(proxy.transform, view.GetAnchor("CameraTarget"));
                var coreBones=new List<Transform>();
                foreach(var bone in view.Animator.GetComponentsInChildren<Transform>(true))
                    if(bone.name=="Thorax" || bone.name=="Head" || bone.name=="Abdomen01" || bone.name=="Abdomen02") coreBones.Add(bone);
                mosquitoCamera.BindLocalVisual(view.transform,view.GetComponentsInChildren<Renderer>(true),coreBones.ToArray(),view.GetAnchor("AimForward"));
            }
        }

        private GameObject ToolPrefab(string toolId, bool diagnose = true)
        {
            if (!GameplayModel.GameplayTools.IsPickup(toolId))
            {
                if (diagnose && diagnosedToolPrefabs.Add(toolId))
                    Debug.LogError($"LMS_TOOL_ID_UNSUPPORTED tool={toolId}", this);
                return null;
            }
            GameObject prefab = toolId == GameplayModel.GameplayTools.Flyswatter ? flyswatterPrefab :
                toolId == GameplayModel.GameplayTools.Slipper ? slipperPrefab :
                toolId == GameplayModel.GameplayTools.ElectricRacket ? electricRacketPrefab :
                toolId == GameplayModel.GameplayTools.Aerosol ? aerosolPrefab : null;
            if (prefab == null)
            {
                if (diagnose && diagnosedToolPrefabs.Add(toolId))
                {
                    string message = $"LMS_TOOL_PREFAB_MISSING tool={toolId}";
                    if (toolId == GameplayModel.GameplayTools.Flyswatter) Debug.LogError(message, this);
                    else Debug.LogWarning(message, this);
                }
                return null;
            }
            ToolView view = prefab.GetComponent<ToolView>();
            if (view == null || view.ToolId != toolId || view.Grip == null || view.Impact == null ||
                !view.Grip.IsChildOf(prefab.transform) || !view.Impact.IsChildOf(prefab.transform) ||
                view.GripToImpact <= .001f || (view.Impact.position - view.Grip.position).sqrMagnitude <= .000001f)
            {
                if (diagnose && diagnosedToolPrefabs.Add(toolId))
                    Debug.LogError($"LMS_TOOL_PREFAB_INVALID tool={toolId} prefab={prefab.name}", prefab);
                return null;
            }
            return prefab;
        }

        private GameObject AttachTool(CharacterView view, string toolId)
        {
            GameObject prefab = ToolPrefab(toolId);
            if (prefab == null) return null;
            Transform socket = view.GetAnchor("ToolSocket_R");
            if (socket == null)
            {
                Debug.LogError($"LMS_TOOL_SOCKET_MISSING actor={view.name}", view);
                return null;
            }
            GameObject instance = Instantiate(prefab, socket);
            instance.name = "Tool_" + toolId;
            DisableColliders(instance);
            ToolView tool = instance.GetComponent<ToolView>();
            Quaternion rotationDelta = socket.rotation * Quaternion.Inverse(tool.Grip.rotation);
            instance.transform.rotation = rotationDelta * instance.transform.rotation;
            instance.transform.position += socket.position - tool.Grip.position;
            return instance;
        }

        private void ApplyToolPickups(IReadOnlyList<GameplayModel.ToolPickupSnapshot> pickups)
        {
            livePickups.Clear();
            for (int i = 0; i < pickups.Count; i++)
            {
                GameplayModel.ToolPickupSnapshot state = pickups[i];
                livePickups.Add(state.PickupId);
                GameObject prefab = ToolPrefab(state.ToolId);
                if (prefab == null) { RemovePickupVisual(state.PickupId); continue; }
                if (!pickupVisuals.TryGetValue(state.PickupId, out GameObject instance) || instance == null)
                {
                    instance = Instantiate(prefab, transform);
                    instance.name = $"Pickup_{state.PickupId}_{state.ToolId}";
                    DisableColliders(instance);
                    pickupVisuals[state.PickupId] = instance;
                }
                else if (instance.GetComponent<ToolView>()?.ToolId != state.ToolId)
                {
                    RemovePickupVisual(state.PickupId);
                    instance = Instantiate(prefab, transform);
                    instance.name = $"Pickup_{state.PickupId}_{state.ToolId}";
                    DisableColliders(instance);
                    pickupVisuals[state.PickupId] = instance;
                }
                bool available = state.OwnerActorId == 0;
                if (instance.activeSelf != available)
                    instance.SetActive(available);
                if (available)
                    ApplyWorldToolPose(instance, state.Position.ToUnity(), state.Rotation.ToUnity());
            }

            removedPickups.Clear();
            foreach (KeyValuePair<uint, GameObject> pair in pickupVisuals)
                if (!livePickups.Contains(pair.Key) || pair.Value == null)
                    removedPickups.Add(pair.Key);
            for (int i = 0; i < removedPickups.Count; i++)
            {
                uint pickupId = removedPickups[i];
                if (pickupVisuals[pickupId] != null)
                    Destroy(pickupVisuals[pickupId]);
                pickupVisuals.Remove(pickupId);
            }
        }

        private void RemovePickupVisual(uint pickupId)
        {
            if (!pickupVisuals.TryGetValue(pickupId, out GameObject instance)) return;
            if (instance != null) Destroy(instance);
            pickupVisuals.Remove(pickupId);
        }

        private static void DisableColliders(GameObject instance)
        {
            Collider[] colliders = instance.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
                colliders[i].enabled = false;
        }

        private static void ApplyWorldToolPose(
            GameObject instance, Vector3 gripPosition, Quaternion gameplayRotation)
        {
            ToolView tool = instance.GetComponent<ToolView>();
            if (tool == null || tool.Grip == null || tool.Impact == null)
            {
                instance.transform.SetPositionAndRotation(gripPosition, gameplayRotation);
                return;
            }

            Vector3 localGrip = instance.transform.InverseTransformPoint(tool.Grip.position);
            Vector3 localImpact = instance.transform.InverseTransformPoint(tool.Impact.position);
            Vector3 sourceForward = localImpact - localGrip;
            Quaternion sourceToGameplay = sourceForward.sqrMagnitude > 0.000001f
                ? Quaternion.FromToRotation(sourceForward.normalized, Vector3.forward)
                : Quaternion.identity;
            instance.transform.rotation = gameplayRotation * sourceToGameplay;
            instance.transform.position += gripPosition - tool.Grip.position;
        }

        private void DriveLocalCamera()
        {
            if (gameplay == null || gameplay.LatestSnapshot == null ||
                !visuals.TryGetValue(gameplay.LocalActorId, out ActorVisualBinding visual) || visual == null)
                return;
            GameplayModel.ActorSnapshot state = null;
            for (int i = 0; i < gameplay.LatestSnapshot.Actors.Count; i++)
            {
                if (gameplay.LatestSnapshot.Actors[i].ActorId != gameplay.LocalActorId)
                    continue;
                state = gameplay.LatestSnapshot.Actors[i];
                break;
            }
            if (state == null || state.Eliminated)
                return;
            if (state.Role == PlayerRole.Human && humanCamera != null)
            {
                humanCamera.SetView(
                    gameplay.LocalViewYaw, gameplay.LocalViewPitch,
                    state.ViewRevision, true);
            }
            else if (state.Role == PlayerRole.Mosquito && mosquitoCamera != null)
            {
                mosquitoCamera.SetView(gameplay.LocalCameraRotation, gameplay.MosquitoCameraDistance);
            }
        }

        private void HandleActorCreated(GameplayActorProxy proxy) => EnsureVisual(proxy);
        private void HandleSnapshot(GameplayModel.GameSessionState snapshot) => ApplySnapshot(snapshot);
        private void HandleEvent(GameplayModel.GameplayEvent item) => ApplyEvent(in item);

        private void Subscribe()
        {
            if (subscribed || gameplay == null || gameplay.World == null)
                return;
            subscribedWorld = gameplay.World;
            subscribedWorld.ActorCreated += HandleActorCreated;
            gameplay.SnapshotApplied += HandleSnapshot;
            gameplay.EventReady += HandleEvent;
            subscribed = true;
            foreach (GameplayActorProxy proxy in subscribedWorld.Actors.Values)
                EnsureVisual(proxy);
            if (gameplay.LatestSnapshot != null)
                ApplySnapshot(gameplay.LatestSnapshot);
        }

        private void Unsubscribe()
        {
            if (!subscribed)
                return;
            if (subscribedWorld != null)
                subscribedWorld.ActorCreated -= HandleActorCreated;
            if (gameplay != null)
            {
                gameplay.SnapshotApplied -= HandleSnapshot;
                gameplay.EventReady -= HandleEvent;
            }
            subscribedWorld = null;
            subscribed = false;
        }

        private void ClearVisuals()
        {
            if (humanCamera) humanCamera.BindEye(null);
            if(mosquitoCamera) mosquitoCamera.Unbind();
            foreach (ActorVisualBinding visual in visuals.Values)
                if (visual != null) { RemoveLocomotionAudio(visual); Destroy(visual.gameObject); }
            visuals.Clear();
            foreach (GameObject pickup in pickupVisuals.Values)
                if (pickup != null) Destroy(pickup);
            pickupVisuals.Clear();
        }
    }
}
