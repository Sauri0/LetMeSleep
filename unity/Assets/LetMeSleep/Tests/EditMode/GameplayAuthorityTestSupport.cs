using System;
using System.Collections.Generic;
using System.Linq;
using LetMeSleep.Core;
using LetMeSleep.Gameplay;
using NUnit.Framework;

namespace LetMeSleep.Tests.EditMode
{
    internal static class GameplayAuthorityTestSupport
    {
        internal const ulong Epoch = 11;
        internal const ulong Round = 21;
        internal const uint HumanOne = 1;
        internal const uint Mosquito = 2;
        internal const uint HumanTwo = 3;
        internal const uint Door = 41;
        internal const float DoorOpenAngle = 1.5f;

        internal sealed class FakeWorld : IGameplayWorld
        {
            internal readonly Queue<DoorInteractionCandidate?> DoorInteractions = new Queue<DoorInteractionCandidate?>();
            internal readonly Queue<DoorSweepResult> DoorSweeps = new Queue<DoorSweepResult>();
            internal readonly Queue<BiteContact?> BiteStarts = new Queue<BiteContact?>();
            internal readonly Queue<BiteContact?> BiteResolutions = new Queue<BiteContact?>();
            internal readonly List<DoorInteractionQuery> DoorQueries = new List<DoorInteractionQuery>();
            internal readonly List<DoorMotionQuery> DoorMotionQueries = new List<DoorMotionQuery>();
            internal readonly List<DoorPose> DoorPoses = new List<DoorPose>();
            internal readonly List<BiteQuery> BiteQueries = new List<BiteQuery>();
            internal readonly List<int> ResolveBiteHumanCounts = new List<int>();
            internal readonly List<MotorQuery> HumanMoves = new List<MotorQuery>();
            internal readonly List<MotorQuery> MosquitoMoves = new List<MotorQuery>();
            internal IReadOnlyList<SpawnActor> BegunActors { get; private set; }
            internal IReadOnlyList<DoorDefinition> BegunDoors { get; private set; }

            public void BeginRound(IReadOnlyList<SpawnActor> actors, IReadOnlyList<DoorDefinition> doors)
            {
                BegunActors = Array.AsReadOnly(actors.ToArray());
                BegunDoors = Array.AsReadOnly(doors.ToArray());
            }

            public void SynchronizeActors(IReadOnlyList<ActorSnapshot> actors) { }

            public MotorResult MoveHuman(in MotorQuery query)
            {
                HumanMoves.Add(query);
                return new MotorResult(query.Position, query.Velocity, true, Float3.Up, query.CrouchFraction);
            }

            public MotorResult MoveMosquito(in MotorQuery query)
            {
                MosquitoMoves.Add(query);
                return new MotorResult(query.Position, query.Velocity, false, Float3.Up, query.CrouchFraction);
            }

            public bool TrySurface(in SurfaceQuery query, out SurfaceContact contact)
            {
                contact = default;
                return false;
            }

            public bool ResolveSurface(in SurfaceAttachment attachment, out SurfaceContact contact)
            {
                contact = default;
                return false;
            }

            public bool TryBiteContact(in BiteQuery query, out BiteContact contact)
            {
                BiteQueries.Add(query);
                var next = BiteStarts.Count == 0 ? null : BiteStarts.Dequeue();
                contact = next.GetValueOrDefault();
                return next.HasValue;
            }

            public bool ResolveBite(uint mosquitoId, in BiteAttachment attachment, int humanCount, out BiteContact contact)
            {
                ResolveBiteHumanCounts.Add(humanCount);
                var next = BiteResolutions.Count == 0 ? null : BiteResolutions.Dequeue();
                contact = next.GetValueOrDefault();
                return next.HasValue;
            }

            public bool TryPlanStrike(uint actorId, Float3 aim, string toolId, out StrikePlan plan)
            {
                plan = default;
                return false;
            }

            public StrikeHit SweepStrike(in StrikeSweep query) => default;

            public bool TryFreeRecoveryPoint(uint actorId, Float3 position, out Float3 point)
            {
                point = position;
                return false;
            }

            public bool HasLineOfSight(uint actorId, Float3 from, uint targetActorId, Float3 to) => true;

            public bool TryDoorInteraction(in DoorInteractionQuery query, out DoorInteractionCandidate candidate)
            {
                DoorQueries.Add(query);
                var next = DoorInteractions.Count == 0 ? null : DoorInteractions.Dequeue();
                candidate = next.GetValueOrDefault();
                return next.HasValue;
            }

            public DoorSweepResult SweepDoor(in DoorMotionQuery query)
            {
                DoorMotionQueries.Add(query);
                return DoorSweeps.Count == 0
                    ? new DoorSweepResult(query.ToAngleRadians, false)
                    : DoorSweeps.Dequeue();
            }

            public void ApplyDoorPose(in DoorPose pose) => DoorPoses.Add(pose);
        }

        internal static GameplayAuthority Start(FakeWorld world, bool secondHuman = false)
        {
            var door = new DoorDefinition(
                Door,
                501,
                new Float3(4, 0, 2),
                Rotation.Identity,
                new Float3(0.9f, 2, 0.04f),
                new Float3(0.8f, 1, 0),
                openAngleRadians: DoorOpenAngle);
            var config = new GameplayRoundConfig(
                Epoch,
                Round,
                "house-patio-v1",
                "test-content",
                doors: new[] { door });
            var roster = new List<SpawnActor>
            {
                new SpawnActor(HumanOne, "human-one", PlayerRole.Human, Float3.Zero),
                new SpawnActor(Mosquito, "mosquito", PlayerRole.Mosquito, new Float3(0, 1, 1))
            };
            if (secondHuman)
                roster.Add(new SpawnActor(HumanTwo, "human-two", PlayerRole.Human, new Float3(1, 0, 0)));
            var authority = new GameplayAuthority(world);
            authority.BeginRound(config, roster);
            return authority;
        }

        internal static CommandHeader Header(uint actor, uint sequence, uint viewRevision = 1)
            => new CommandHeader(Epoch, Round, actor, sequence, 0, viewRevision);

        internal static PlayerActionCommand Use(uint actor, uint sequence, uint viewRevision = 1)
            => new PlayerActionCommand(Header(actor, sequence, viewRevision), ActionKind.Use, Float3.Forward);

        internal static PlayerInputCommand Input(uint actor, uint sequence, bool bite = false, bool use = false, uint viewRevision = 1)
            => new PlayerInputCommand(
                Header(actor, sequence, viewRevision),
                default,
                0,
                0,
                0,
                Float3.Forward,
                bite: bite,
                use: use);

        internal static DoorInteractionCandidate DoorCandidate(uint revision = 1, float distance = 1)
            => new DoorInteractionCandidate(Door, revision, new Float3(4, 1, 2), distance);

        internal static BiteContact BiteOn(uint victim, uint poseRevision = 1)
        {
            var attachment = new BiteAttachment(victim, 700 + victim, new Float3(0, 0, .01f), Float3.Forward, poseRevision);
            return new BiteContact(attachment, new Float3(victim, 1, 0), -Float3.Forward);
        }

        internal static ActorSnapshot Actor(GameSessionState snapshot, uint actorId)
            => snapshot.Actors.Single(actor => actor.ActorId == actorId);

        internal static void Advance(GameplayAuthority authority)
            => authority.Advance(new HostTick(authority.CurrentTick + 1));

        internal static void AssertPosition(Float3 actual, Float3 expected)
        {
            Assert.That(actual.X, Is.EqualTo(expected.X).Within(.00001f));
            Assert.That(actual.Y, Is.EqualTo(expected.Y).Within(.00001f));
            Assert.That(actual.Z, Is.EqualTo(expected.Z).Within(.00001f));
        }
    }
}
