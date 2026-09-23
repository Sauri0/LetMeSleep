using System;
using System.Collections.Generic;
using System.Linq;
using LetMeSleep.Core;
using LetMeSleep.Gameplay;
using LetMeSleep.Gameplay.Unity;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LetMeSleep.Tests.PlayMode
{
    // Host publication of a round end. RoundFinished is the only path to RoomSession.FinishRound (Results).
    public sealed class GameplayRuntimeRoundEndPlayModeTests
    {
        private GameObject owner;
        private GameplayRuntime runtime;
        private readonly List<GameSessionState> published = new List<GameSessionState>();
        private readonly List<(RoundEndReason Reason, PlayerRole Winner)> finished = new List<(RoundEndReason, PlayerRole)>();

        [SetUp]
        public void SetUp()
        {
            published.Clear(); finished.Clear();
            owner = new GameObject("Round end fixture");
            runtime = owner.AddComponent<GameplayRuntime>();
            runtime.CaptureLocalInput = false; runtime.AutomaticTick = false; runtime.IsHost = true;
            var map = new GameObject("Map"); map.transform.SetParent(owner.transform, false);
            var floor = new GameObject("Floor"); floor.transform.SetParent(map.transform, false);
            floor.transform.localPosition = new Vector3(0, -.05f, 0);
            floor.AddComponent<BoxCollider>().size = new Vector3(20, .1f, 20);
            runtime.World.MapRoot = map.transform;
            runtime.LocalActorId = 1; runtime.LocalPrincipal = "host-human";
            runtime.BeginRound(new GameplayRoundConfig(7, 1, "round-end-fixture", "fixture-hash"), new[]
            {
                new SpawnActor(1, "host-human", PlayerRole.Human, Float3.Zero),
                new SpawnActor(2, "guest-mosquito", PlayerRole.Mosquito, new Float3(0, 1.5f, 2))
            });
            runtime.SnapshotReady += published.Add;
            runtime.RoundFinished += (reason, winner) => finished.Add((reason, winner));
        }

        [TearDown]
        public void TearDown() { if (owner) Object.DestroyImmediate(owner); }

        [Test]
        public void TeamLeftOutsideAdvanceIsPublishedOnceOnTheNextHostTick()
        {
            runtime.TickHost();
            Assert.That(runtime.LatestSnapshot.SimulationPhase, Is.EqualTo(SimulationPhase.Running));
            Assert.That(finished, Is.Empty);

            // The only mosquito leaves (room view no longer lists it): the authority ends the round outside Advance.
            runtime.Authority.RemoveActor(2, ActorRemovalReason.Disconnected);
            Assert.That(runtime.Authority.IsRunning, Is.False);

            runtime.TickHost();
            runtime.TickHost();

            Assert.That(finished, Has.Count.EqualTo(1), "RoundFinished moves the room to Results exactly once.");
            Assert.That(finished[0].Reason, Is.EqualTo(RoundEndReason.OpponentLeft));
            Assert.That(finished[0].Winner, Is.EqualTo(PlayerRole.Human));
            Assert.That(published.Last().SimulationPhase, Is.EqualTo(SimulationPhase.Ended), "Clients need the reliable Ended snapshot.");
            Assert.That(published.Count(state => state.SimulationPhase == SimulationPhase.Ended), Is.EqualTo(1));
            Assert.That(runtime.LatestSnapshot.SimulationPhase, Is.EqualTo(SimulationPhase.Ended), "The host HUD shows the results instead of a frozen round.");
            Assert.That(runtime.LatestSnapshot.Actors.Select(actor => actor.ActorId), Is.EqualTo(new uint[] { 1 }));
        }

        [Test]
        public void ThrowingSnapshotListenerAtTimeExpiryCannotStrandTheRoomInPlaying()
        {
            runtime.BeginRound(new GameplayRoundConfig(8, 2, "round-end-fixture", "fixture-hash", roundSeconds: 30), new[]
            {
                new SpawnActor(1, "host-human", PlayerRole.Human, Float3.Zero),
                new SpawnActor(2, "guest-mosquito", PlayerRole.Mosquito, new Float3(0, 1.5f, 2))
            });
            runtime.SnapshotReady += state =>
            { if (state.SimulationPhase == SimulationPhase.Ended) throw new System.IO.InvalidDataException("wire validation"); };
            int thrown = 0;
            for (int tick = 0; tick < 1000 && runtime.Authority.IsRunning; tick++)
            {
                try { runtime.TickHost(); }
                catch (System.IO.InvalidDataException) { thrown++; }
            }
            Assert.That(runtime.Authority.CurrentTick, Is.EqualTo(900));
            Assert.That(thrown, Is.EqualTo(1));
            Assert.That(finished, Has.Count.EqualTo(1), "The room still reaches Results after a publication failure.");
            Assert.That(finished[0].Reason, Is.EqualTo(RoundEndReason.TimeExpired));
            Assert.DoesNotThrow(runtime.TickHost, "A published end is not retried every frame.");
            Assert.That(finished, Has.Count.EqualTo(1));
        }

        [Test]
        public void StoppedRoundIsNotReportedAsFinished()
        {
            runtime.StopRound();
            runtime.TickHost();
            Assert.That(finished, Is.Empty, "Leaving/teardown aborts locally without announcing results.");
        }
    }
}
