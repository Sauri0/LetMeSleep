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
            internal float MotionPhase;
            internal bool Grounded;
            internal bool Attached;
            internal AudioCue WingCue;
            internal Transform Follow;
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
        private ulong currentEpoch;
        private ulong currentRound;
        private bool subscribed;

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
            StopWingLoops();
        }

        public void Bind(GameplayRuntime runtime, AlfaAudioDirector director)
        {
            Unsubscribe();
            gameplay = runtime;
            audioDirector = director;
            Subscribe();
        }

        public void ApplySnapshot(GameplayModel.GameSessionState snapshot)
        {
            if (snapshot == null || audioDirector == null)
                return;
            if (snapshot.SessionEpoch != currentEpoch || snapshot.RoundId != currentRound)
            {
                currentEpoch = snapshot.SessionEpoch;
                currentRound = snapshot.RoundId;
                playedEvents.Clear();
                actorStates.Clear();
                audioDirector.EnterRound();
            }

            AlfaAudioCatalog catalog = audioDirector.Catalog;
            AudioEmitterPool emitters = audioDirector.Emitters;
            if (catalog == null || emitters == null || gameplay == null || gameplay.World == null)
                return;

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
                    previous.Follow = proxy.transform;
                    UpdateWingLoop(actor, proxy.transform, previous, catalog, emitters);
                    bool attached = actor.SurfaceAttachment.HasValue || actor.BiteAttachment.HasValue;
                    if (!first && attached != previous.Attached)
                        emitters.Play(attached ? catalog.MosquitoPerch : catalog.MosquitoDetach,
                            proxy.transform.position);
                    previous.Attached = attached;
                }
                else
                {
                    if (!first && actor.Grounded && !previous.Grounded)
                        emitters.Play(catalog.HumanLand, proxy.transform.position);
                    if (!first && actor.Grounded && actor.LifeState == GameplayModel.LifeState.Active &&
                        actor.Velocity.LengthSquared > 0.04f && CrossedFootstep(previous.MotionPhase, actor.MotionPhase))
                        emitters.Play(catalog.HumanFootstep, proxy.transform.position);
                }

                previous.MotionPhase = actor.MotionPhase;
                previous.Grounded = actor.Grounded;

                if (actor.ActorId == gameplay.LocalActorId)
                    localActivity = actor.StrikeState.Phase != GameplayModel.StrikePhase.None ||
                        actor.LifeState == GameplayModel.LifeState.PreparingBite ||
                        actor.LifeState == GameplayModel.LifeState.Biting ||
                        actor.LifeState == GameplayModel.LifeState.Stunned ||
                        actor.LifeState == GameplayModel.LifeState.Recovering;
            }

            bool urgent = snapshot.TimeRemainingTicks <= 20u * 30u ||
                (snapshot.BloodGoal > 0f && snapshot.BloodCollected / snapshot.BloodGoal >= 0.85f);
            audioDirector.SetRoundIntensity(localActivity, urgent);

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
        }

        private static void UpdateWingLoop(
            GameplayModel.ActorSnapshot actor, Transform follow, ActorAudioState state,
            AlfaAudioCatalog catalog, AudioEmitterPool emitters)
        {
            AudioCue desired = SelectWingCue(actor.LifeState, catalog);
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

        private static AudioCue SelectWingCue(GameplayModel.LifeState state, AlfaAudioCatalog catalog)
        {
            if (!ShouldBuzz(state))
                return null;
            if (state == GameplayModel.LifeState.PreparingBite || state == GameplayModel.LifeState.Biting)
                return catalog.MosquitoWingBiteLoop;
            if (state == GameplayModel.LifeState.ApproachingSurface || state == GameplayModel.LifeState.Surface)
                return catalog.MosquitoWingPerchLoop;
            return catalog.MosquitoWingLoop;
        }

        private static bool CrossedFootstep(float previous, float current)
        {
            previous = Mathf.Repeat(previous, 1f);
            current = Mathf.Repeat(current, 1f);
            float delta = Mathf.Repeat(current - previous, 1f);
            if (delta <= 0.0001f || delta > 0.60f)
                return false;
            return (previous < 0.5f && current >= 0.5f) || current < previous;
        }

        private static bool ShouldBuzz(GameplayModel.LifeState state)
        {
            return state != GameplayModel.LifeState.Falling &&
                state != GameplayModel.LifeState.Stunned &&
                state != GameplayModel.LifeState.Fainted &&
                state != GameplayModel.LifeState.Recovering;
        }
    }
}
