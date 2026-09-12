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
                audioDirector.EnterRound();
            }

            AlfaAudioCatalog catalog = audioDirector.Catalog;
            AudioEmitterPool emitters = audioDirector.Emitters;
            if (catalog == null || emitters == null || gameplay == null || gameplay.World == null)
                return;

            for (int i = 0; i < snapshot.Actors.Count; i++)
            {
                GameplayModel.ActorSnapshot actor = snapshot.Actors[i];
                if (actor.Role != PlayerRole.Mosquito ||
                    !gameplay.World.Actors.TryGetValue(actor.ActorId, out GameplayActorProxy proxy))
                    continue;

                if (ShouldBuzz(actor.LifeState))
                    emitters.Play(catalog.MosquitoWingLoop, proxy.transform.position, proxy.transform);
                else
                    emitters.Stop(catalog.MosquitoWingLoop, proxy.transform);
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
                case GameplayModel.GameplayEventKind.MosquitoKnockedDown:
                    cue = audioDirector.Catalog.StrikeImpact;
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
            gameplay.SnapshotReady += HandleSnapshot;
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
            gameplay.SnapshotReady -= HandleSnapshot;
            gameplay.EventReady -= HandleEvent;
            gameplay.RoundFinished -= HandleRoundFinished;
            subscribed = false;
        }

        private void StopWingLoops()
        {
            if (audioDirector != null && audioDirector.Emitters != null && audioDirector.Catalog != null)
                audioDirector.Emitters.Stop(audioDirector.Catalog.MosquitoWingLoop);
        }

        private static bool ShouldBuzz(GameplayModel.LifeState state)
        {
            return state != GameplayModel.LifeState.Falling &&
                state != GameplayModel.LifeState.Stunned &&
                state != GameplayModel.LifeState.Fainted;
        }
    }
}
