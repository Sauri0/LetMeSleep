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
        private UnityGameplayWorld subscribedWorld;
        private bool subscribed;

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
        {
            humanPrefab = human;
            humanFirstPersonPrefab = humanFirstPerson;
            mosquitoPrefab = mosquito;
            flyswatterPrefab = flyswatter;
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
                    Destroy(visuals[actorId].gameObject);
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
            if (proxy == null) return;
            if (visuals.TryGetValue(proxy.ActorId,out var existing))
            {
                if(existing && existing.transform.parent==proxy.transform) return;
                if(existing) Destroy(existing.gameObject);
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
            visuals.Add(proxy.ActorId, binding);
            if(VisualAttentionFactory.TryInstall(instance,false,out var attention,out var reason))
            {
                var target=instance.GetComponent<GameplayAttentionTarget>();
                if(!target) target=instance.AddComponent<GameplayAttentionTarget>();
                target.Bind(gameplay,proxy,attention);
            }
            else Debug.LogWarning($"LMS_FACIAL_SKIPPED actor={proxy.ActorId}: {reason}",instance);
            if (proxy.Role == PlayerRole.Human)
                binding.BindFlyswatter(AttachFlyswatter(view));
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
                humanCamera.BindEye(view.GetAnchor("CameraEye"));
            else if (proxy.Role == PlayerRole.Mosquito && mosquitoCamera != null)
            {
                mosquitoCamera.SetCollisionFilter(gameplay.World.IsWorldCollider);
                mosquitoCamera.BindAnchors(proxy.transform, view.GetAnchor("CameraTarget"));
            }
        }

        private GameObject AttachFlyswatter(CharacterView view)
        {
            if (flyswatterPrefab == null)
            {
                Debug.LogError("LMS_FLYSWATTER_PREFAB_MISSING", this);
                return null;
            }
            Transform socket = view.GetAnchor("ToolSocket_R");
            if (socket == null)
            {
                Debug.LogError($"LMS_TOOL_SOCKET_MISSING actor={view.name}", view);
                return null;
            }
            GameObject instance = Instantiate(flyswatterPrefab, socket);
            instance.name = "Tool_Flyswatter";
            DisableColliders(instance);
            ToolView tool = instance.GetComponent<ToolView>();
            if (tool == null || tool.Grip == null)
            {
                Debug.LogError("LMS_FLYSWATTER_BINDING_MISSING", instance);
                Destroy(instance);
                return null;
            }
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
                if (!GameplayModel.GameplayTools.IsFlyswatter(state.ToolId))
                    continue;
                if (!pickupVisuals.TryGetValue(state.PickupId, out GameObject instance) || instance == null)
                {
                    if (flyswatterPrefab == null)
                        continue;
                    instance = Instantiate(flyswatterPrefab, transform);
                    instance.name = $"Pickup_{state.PickupId}_Flyswatter";
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
            if (state == null)
                return;
            if (state.Role == PlayerRole.Human && humanCamera != null)
            {
                humanCamera.SetView(
                    gameplay.LocalViewYaw, gameplay.LocalViewPitch,
                    state.ViewRevision, true);
            }
            else if (state.Role == PlayerRole.Mosquito && mosquitoCamera != null)
            {
                Quaternion rotation = Quaternion.Euler(
                    -gameplay.LocalViewPitch * Mathf.Rad2Deg,
                    gameplay.LocalViewYaw * Mathf.Rad2Deg, 0f);
                mosquitoCamera.SetView(rotation, gameplay.MosquitoCameraDistance);
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
            foreach (ActorVisualBinding visual in visuals.Values)
                if (visual != null) Destroy(visual.gameObject);
            visuals.Clear();
            foreach (GameObject pickup in pickupVisuals.Values)
                if (pickup != null) Destroy(pickup);
            pickupVisuals.Clear();
        }
    }
}
