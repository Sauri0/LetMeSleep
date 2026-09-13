using System;
using System.Collections.Generic;
using LetMeSleep.Audio;
using LetMeSleep.Core;
using LetMeSleep.Gameplay.Unity;
using UnityEngine;
using GameplayModel = LetMeSleep.Gameplay;

namespace LetMeSleep.Presentation.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class GameplayAudioPresenter : MonoBehaviour
    {
        private sealed class ActorAudioState
        {
            internal Vector3 Position;
            internal double HostTime;
            internal readonly FootstepCadence Steps = new FootstepCadence();
            internal bool Grounded;
            internal bool Attached;
            internal AudioCue WingCue;
            internal Transform Follow;
        }

        private sealed class ToolAudioState
        {
            internal uint OwnerActorId;
            internal uint Revision;
            internal Vector3 Position;
        }

        private readonly struct AudioZone
        {
            internal AudioZone(Vector3 position, GroundMaterial material)
            {
                Position = position;
                Material = material;
            }

            internal Vector3 Position { get; }
            internal GroundMaterial Material { get; }
        }

        private readonly struct EventKey : IEquatable<EventKey>
        {
            private readonly ulong epoch;
            private readonly ulong round;
            private readonly ulong eventId;

            public EventKey(ulong epoch, ulong round, ulong eventId)
            {
                this.epoch = epoch;
                this.round = round;
                this.eventId = eventId;
            }

            public bool Equals(EventKey other) => epoch == other.epoch && round == other.round && eventId == other.eventId;
            public override bool Equals(object obj) => obj is EventKey other && Equals(other);
            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = epoch.GetHashCode();
                    hash = (hash * 397) ^ round.GetHashCode();
                    return (hash * 397) ^ eventId.GetHashCode();
                }
            }
        }

        [SerializeField] private GameplayRuntime gameplay = null;
        [SerializeField] private AlfaAudioDirector audioDirector = null;
        private readonly HashSet<EventKey> playedEvents = new HashSet<EventKey>();
        private readonly Dictionary<uint, ActorAudioState> actorStates =
            new Dictionary<uint, ActorAudioState>();
        private readonly HashSet<uint> liveActors = new HashSet<uint>();
        private readonly List<uint> removedActors = new List<uint>();
        private readonly Dictionary<uint, ToolAudioState> toolStates =
            new Dictionary<uint, ToolAudioState>();
        private readonly List<AudioZone> audioZones = new List<AudioZone>();
        private readonly Dictionary<uint, HumanLocomotionPresenter> locomotionSources =
            new Dictionary<uint, HumanLocomotionPresenter>();
        private bool sharedHumanLocomotionAudio;
        private ulong currentEpoch;
        private ulong currentRound;
        private bool subscribed;

        private void OnEnable()
        {
            foreach (var source in locomotionSources.Values)
            {
                if (!source) continue;
                source.ContactReady -= HandleFootContact;
                source.ContactReady += HandleFootContact;
            }
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
            ClearLocomotionSources(false);
            StopWingLoops();
        }

        public void Bind(GameplayRuntime runtime, AlfaAudioDirector director)
        {
            Unsubscribe();
            ClearLocomotionSources();
            StopWingLoops();
            gameplay = runtime;
            audioDirector = director;
            Subscribe();
        }

        // Explicit integration switch: missing/unregistered visuals stay silent, never fall back
        // to snapshot footsteps. Enable before subscribing to the first round snapshot.
        public void EnableSharedHumanLocomotionAudio() => sharedHumanLocomotionAudio = true;

        public void RegisterLocomotion(HumanLocomotionPresenter source)
        {
            if (!source || !source.IsConfigured)
                throw new ArgumentException("Configure the visual locomotion presenter before registering audio.");
            if (locomotionSources.TryGetValue(source.ActorId, out var previous))
            {
                if (previous == source) return;
                if (previous) previous.ContactReady -= HandleFootContact;
            }
            locomotionSources[source.ActorId] = source;
            source.ContactReady += HandleFootContact;
        }

        public void UnregisterLocomotion(HumanLocomotionPresenter source)
        {
            if (!source) return;
            source.ContactReady -= HandleFootContact;
            if (locomotionSources.TryGetValue(source.ActorId, out var current) && current == source)
                locomotionSources.Remove(source.ActorId);
        }

        private void ClearLocomotionSources(bool forget = true)
        {
            foreach (var source in locomotionSources.Values)
                if (source) source.ContactReady -= HandleFootContact;
            if (forget) locomotionSources.Clear();
        }

        private void HandleFootContact(HumanLocomotionPresenter source, HumanLocomotionPresenter.FootContact contact)
        {
            if (!isActiveAndEnabled || !source ||
                !locomotionSources.TryGetValue(source.ActorId, out var registered) || registered != source ||
                !gameplay || gameplay.World == null || gameplay.LatestSnapshot == null ||
                gameplay.LatestSnapshot.SimulationPhase != GameplayModel.SimulationPhase.Running ||
                !gameplay.World.Actors.TryGetValue(source.ActorId, out var proxy) || proxy.State == null ||
                proxy.Role != PlayerRole.Human || proxy.State.LifeState != GameplayModel.LifeState.Active ||
                !proxy.State.Grounded || proxy.State.StrikeState.Phase != GameplayModel.StrikePhase.None ||
                !audioDirector || !audioDirector.Catalog || !audioDirector.Emitters)
                return;
            EnsureAudioZones(gameplay.World.MapRoot);
            audioDirector.Emitters.Play(SelectFootstepCue(audioDirector.Catalog,
                ResolveGroundMaterial(contact.Position)), contact.Position);
        }

        public void ApplySnapshot(GameplayModel.GameSessionState snapshot)
        {
            if (snapshot == null || audioDirector == null)
                return;
            if (snapshot.SimulationPhase != GameplayModel.SimulationPhase.Running)
            {
                StopWingLoops();
                return;
            }
            if (snapshot.SessionEpoch != currentEpoch || snapshot.RoundId != currentRound)
            {
                currentEpoch = snapshot.SessionEpoch;
                currentRound = snapshot.RoundId;
                playedEvents.Clear();
                StopWingLoops();
                toolStates.Clear();
                audioZones.Clear();
                audioDirector.EnterRound();
            }

            AlfaAudioCatalog catalog = audioDirector.Catalog;
            AudioEmitterPool emitters = audioDirector.Emitters;
            if (catalog == null || emitters == null || gameplay == null || gameplay.World == null)
                return;

            EnsureAudioZones(gameplay.World.MapRoot);

            liveActors.Clear();
            bool localActivity = false;
            for (int i = 0; i < snapshot.Actors.Count; i++)
            {
                GameplayModel.ActorSnapshot actor = snapshot.Actors[i];
                liveActors.Add(actor.ActorId);
                if (!gameplay.World.Actors.TryGetValue(actor.ActorId, out GameplayActorProxy proxy))
                    continue;

                bool first = !actorStates.TryGetValue(actor.ActorId, out ActorAudioState previous);
                if (first)
                {
                    previous = new ActorAudioState();
                    actorStates.Add(actor.ActorId, previous);
                }

                if (actor.Role == PlayerRole.Mosquito)
                {
                    if (actor.BiteAttachment.HasValue &&
                        actor.BiteAttachment.Value.VictimId == gameplay.LocalActorId)
                        localActivity = true;
                    UpdateWingLoop(actor, proxy.transform, previous, catalog, emitters);
                    bool attached = actor.SurfaceAttachment.HasValue || actor.BiteAttachment.HasValue;
                    if (!first && attached != previous.Attached)
                        emitters.Play(attached ? catalog.MosquitoPerch : catalog.MosquitoDetach,
                            proxy.transform.position);
                    previous.Attached = attached;
                }
                else
                {
                    GroundMaterial ground = ResolveGroundMaterial(proxy.transform.position);
                    if (!first && !actor.Grounded && previous.Grounded && actor.Velocity.Y > 0.5f)
                        emitters.Play(catalog.HumanJump, proxy.transform.position);
                    if (!first && actor.Grounded && !previous.Grounded)
                        emitters.Play(SelectLandCue(catalog, ground), proxy.transform.position);
                    Vector3 position = actor.Position.ToUnity();
                    Vector3 displacement = position - previous.Position;
                    displacement.y = 0f;
                    float planarSpeed = Mathf.Sqrt(actor.Velocity.X * actor.Velocity.X +
                        actor.Velocity.Z * actor.Velocity.Z);
                    bool walking = !first && previous.Grounded && actor.Grounded &&
                        actor.LifeState == GameplayModel.LifeState.Active;
                    if (!sharedHumanLocomotionAudio && !locomotionSources.ContainsKey(actor.ActorId) &&
                        previous.Steps.Advance(displacement.magnitude, planarSpeed,
                        snapshot.HostTime - previous.HostTime, Time.unscaledTime, walking))
                        emitters.Play(SelectFootstepCue(catalog, ground), proxy.transform.position);
                }

                previous.Position = actor.Position.ToUnity();
                previous.HostTime = snapshot.HostTime;
                previous.Grounded = actor.Grounded;

                if (actor.ActorId == gameplay.LocalActorId)
                    localActivity |= actor.StrikeState.Phase != GameplayModel.StrikePhase.None ||
                        actor.LifeState == GameplayModel.LifeState.PreparingBite ||
                        actor.LifeState == GameplayModel.LifeState.Biting ||
                        actor.LifeState == GameplayModel.LifeState.Stunned ||
                        actor.LifeState == GameplayModel.LifeState.Recovering;
            }

            bool urgent = snapshot.TimeRemainingTicks <= 20u * 30u ||
                (snapshot.BloodGoal > 0f && snapshot.BloodCollected / snapshot.BloodGoal >= 0.85f);
            audioDirector.SetRoundIntensity(localActivity, urgent);
            ApplyToolAudio(snapshot.ToolPickups, catalog, emitters);

            removedActors.Clear();
            foreach (uint actorId in actorStates.Keys)
                if (!liveActors.Contains(actorId)) removedActors.Add(actorId);
            for (int i = 0; i < removedActors.Count; i++)
            {
                uint actorId = removedActors[i];
                ActorAudioState state = actorStates[actorId];
                if (state.WingCue != null)
                    emitters.Stop(state.WingCue, state.Follow);
                actorStates.Remove(actorId);
            }
        }

        public void ApplyEvent(in GameplayModel.GameplayEvent item)
        {
            if (audioDirector == null || audioDirector.Emitters == null || audioDirector.Catalog == null)
                return;
            var key = new EventKey(item.SessionEpoch, item.RoundId, item.EventId);
            if (!playedEvents.Add(key))
                return;

            Vector3 position = item.Position.ToUnity();
            AudioCue cue = null;
            switch (item.Kind)
            {
                case GameplayModel.GameplayEventKind.StrikeStarted:
                    cue = audioDirector.Catalog.StrikeSwing;
                    break;
                case GameplayModel.GameplayEventKind.StrikeImpact:
                    cue = audioDirector.Catalog.StrikeImpact;
                    break;
                case GameplayModel.GameplayEventKind.MosquitoKnockedDown:
                    cue = audioDirector.Catalog.MosquitoKnockedDown;
                    break;
                case GameplayModel.GameplayEventKind.BiteStarted:
                    cue = audioDirector.Catalog.BiteStarted;
                    break;
                case GameplayModel.GameplayEventKind.HumanFainted:
                    cue = audioDirector.Catalog.HumanFainted;
                    break;
                case GameplayModel.GameplayEventKind.Recovered:
                    cue = audioDirector.Catalog.Recovered;
                    break;
                case GameplayModel.GameplayEventKind.DoorChanged:
                    if (item.Door.HasValue)
                        cue = Mathf.Abs(item.Door.Value.TargetAngleRadians) > 0.01f
                            ? audioDirector.Catalog.DoorOpen
                            : audioDirector.Catalog.DoorClose;
                    break;
            }

            if (cue != null)
                audioDirector.Emitters.Play(cue, position);
        }

        private void HandleRoundFinished(GameplayModel.RoundEndReason reason, PlayerRole winner)
        {
            StopWingLoops();
            if (audioDirector == null)
                return;
            if (winner == PlayerRole.Human)
                audioDirector.FinishHumansWin();
            else if (winner == PlayerRole.Mosquito)
                audioDirector.FinishMosquitoesWin();
        }

        private void HandleSnapshot(GameplayModel.GameSessionState snapshot) => ApplySnapshot(snapshot);
        private void HandleEvent(GameplayModel.GameplayEvent item) => ApplyEvent(in item);

        private void Subscribe()
        {
            if (subscribed || gameplay == null)
                return;
            gameplay.SnapshotApplied += HandleSnapshot;
            gameplay.EventReady += HandleEvent;
            gameplay.RoundFinished += HandleRoundFinished;
            subscribed = true;
            if (gameplay.LatestSnapshot != null)
                ApplySnapshot(gameplay.LatestSnapshot);
        }

        private void Unsubscribe()
        {
            if (!subscribed || gameplay == null)
                return;
            gameplay.SnapshotApplied -= HandleSnapshot;
            gameplay.EventReady -= HandleEvent;
            gameplay.RoundFinished -= HandleRoundFinished;
            subscribed = false;
        }

        private void StopWingLoops()
        {
            if (audioDirector != null && audioDirector.Emitters != null && audioDirector.Catalog != null)
            {
                audioDirector.Emitters.Stop(audioDirector.Catalog.MosquitoWingLoop);
                audioDirector.Emitters.Stop(audioDirector.Catalog.MosquitoWingPerchLoop);
                audioDirector.Emitters.Stop(audioDirector.Catalog.MosquitoWingBiteLoop);
            }
            actorStates.Clear();
            toolStates.Clear();
            audioZones.Clear();
        }

        private void ApplyToolAudio(
            IReadOnlyList<GameplayModel.ToolPickupSnapshot> pickups,
            AlfaAudioCatalog catalog, AudioEmitterPool emitters)
        {
            for (int i = 0; i < pickups.Count; i++)
            {
                GameplayModel.ToolPickupSnapshot pickup = pickups[i];
                Vector3 position = pickup.Position.ToUnity();
                if (!toolStates.TryGetValue(pickup.PickupId, out ToolAudioState previous))
                {
                    toolStates[pickup.PickupId] = new ToolAudioState
                    {
                        OwnerActorId = pickup.OwnerActorId,
                        Revision = pickup.Revision,
                        Position = position
                    };
                    continue;
                }
                if (pickup.Revision > previous.Revision)
                {
                    if (previous.OwnerActorId == 0 && pickup.OwnerActorId != 0)
                    {
                        Vector3 pickupPosition = previous.Position;
                        if (gameplay != null && gameplay.World != null &&
                            gameplay.World.Actors.TryGetValue(pickup.OwnerActorId, out GameplayActorProxy owner))
                            pickupPosition = owner.transform.position;
                        emitters.Play(catalog.ToolPickup, pickupPosition);
                    }
                    else if (previous.OwnerActorId != 0 && pickup.OwnerActorId == 0)
                        emitters.Play(catalog.ToolDrop, position);
                }
                previous.OwnerActorId = pickup.OwnerActorId;
                previous.Revision = pickup.Revision;
                previous.Position = position;
            }
        }

        private void EnsureAudioZones(Transform root)
        {
            if (audioZones.Count > 0 || root == null)
                return;
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (!candidate.name.StartsWith("AudioZone_", StringComparison.OrdinalIgnoreCase))
                    continue;
                audioZones.Add(new AudioZone(candidate.position, MaterialForZone(candidate.name)));
            }
        }

        private GroundMaterial ResolveGroundMaterial(Vector3 position)
        {
            float bestDistance = float.PositiveInfinity;
            GroundMaterial result = GroundMaterial.Wood;
            for (int i = 0; i < audioZones.Count; i++)
            {
                Vector3 delta = audioZones[i].Position - position;
                float distance = delta.x * delta.x + delta.z * delta.z + delta.y * delta.y * 4f;
                if (distance >= bestDistance)
                    continue;
                bestDistance = distance;
                result = audioZones[i].Material;
            }
            return result;
        }

        private static GroundMaterial MaterialForZone(string zone)
        {
            if (Contains(zone, "Bedroom"))
                return GroundMaterial.Cloth;
            if (Contains(zone, "Kitchen") || Contains(zone, "Bathroom") || Contains(zone, "Utility"))
                return GroundMaterial.Tile;
            return GroundMaterial.Wood;
        }

        private static AudioCue SelectFootstepCue(AlfaAudioCatalog catalog, GroundMaterial material)
        {
            return material == GroundMaterial.Tile ? catalog.HumanFootstepTile :
                material == GroundMaterial.Cloth ? catalog.HumanFootstepCloth : catalog.HumanFootstep;
        }

        private static AudioCue SelectLandCue(AlfaAudioCatalog catalog, GroundMaterial material)
        {
            return material == GroundMaterial.Tile ? catalog.HumanLandTile :
                material == GroundMaterial.Cloth ? catalog.HumanLandCloth : catalog.HumanLand;
        }

        private static bool Contains(string source, string value)
        {
            return source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void UpdateWingLoop(
            GameplayModel.ActorSnapshot actor, Transform follow, ActorAudioState state,
            AlfaAudioCatalog catalog, AudioEmitterPool emitters)
        {
            AudioCue desired = !actor.SurfaceAttachment.HasValue && !actor.BiteAttachment.HasValue &&
                (actor.LifeState == GameplayModel.LifeState.Flying ||
                 actor.LifeState == GameplayModel.LifeState.ApproachingSurface)
                ? catalog.MosquitoWingLoop : null;
            if (state.Follow != follow)
            {
                if (state.WingCue != null) emitters.Stop(state.WingCue, state.Follow);
                state.WingCue = null;
                state.Follow = follow;
            }
            if (desired == state.WingCue)
            {
                if (desired != null)
                    emitters.Play(desired, follow.position, follow);
                return;
            }

            if (state.WingCue != null)
                emitters.Stop(state.WingCue, follow);
            state.WingCue = desired;
            if (desired != null)
                emitters.Play(desired, follow.position, follow);
        }

        private enum GroundMaterial
        {
            Wood,
            Tile,
            Cloth
        }
    }
}
