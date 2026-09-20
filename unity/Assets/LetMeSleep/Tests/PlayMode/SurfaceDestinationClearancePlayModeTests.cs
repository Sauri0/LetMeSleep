using System;
using System.Linq;
using LetMeSleep.Core;
using LetMeSleep.Gameplay;
using LetMeSleep.Gameplay.Unity;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LetMeSleep.Tests.PlayMode
{
    public sealed class SurfaceDestinationClearancePlayModeTests
    {
        private Fixture fixture;

        [SetUp]
        public void SetUp() => fixture = new Fixture();

        [TearDown]
        public void TearDown() => fixture.Dispose();

        [Test]
        public void RayVisibleSupportIsRejectedWhenTheMosquitoBodyDestinationIsBlocked()
        {
            Collider blocker = fixture.AddBlocker("Off-axis destination blocker",
                new Vector3(.057f, .045f, 0), new Vector3(.02f, .02f, .02f));
            Physics.SyncTransforms();

            RaycastHit first = Physics.RaycastAll(Fixture.MosquitoStart, Vector3.left, .31f,
                    fixture.World.GeometryMask, QueryTriggerInteraction.Collide)
                .Where(hit => hit.collider != fixture.World.Actors[2].MotorCollider)
                .OrderBy(hit => hit.distance).First();
            Assert.That(first.collider, Is.EqualTo(fixture.Support),
                "The blocker must not replace the authored support as first ray hit.");
            Assert.That(Physics.OverlapSphere(Fixture.SurfaceTarget, .054f,
                fixture.World.GeometryMask, QueryTriggerInteraction.Ignore), Does.Contain(blocker),
                "Fixture must block the 54 mm destination body volume.");

            Assert.That(fixture.World.TrySurface(new SurfaceQuery(2, Fixture.MosquitoStart.ToFloat(),
                Vector3.left.ToFloat(), .25f), out _), Is.False);
        }

        [Test]
        public void ApproachingSurfaceDetachesWhenAnotherActorOccupiesTheDestination()
        {
            fixture.BeginApproach();
            ActorSnapshot before = fixture.Mosquito;
            Assert.That(before.LifeState, Is.EqualTo(LifeState.ApproachingSurface));
            Assert.That(before.SurfaceAttachment.HasValue, Is.True);

            GameplayActorProxy other = fixture.World.Actors[3];
            // The other body overlaps the destination sphere but stays off the support ray.
            // The pre-fix re-ray therefore continued approaching instead of detaching.
            other.transform.position = Fixture.SurfaceTarget + Vector3.up * .09f;
            Physics.SyncTransforms();

            fixture.Advance();
            ActorSnapshot after = fixture.Mosquito;
            Assert.That(after.LifeState, Is.EqualTo(LifeState.Flying));
            Assert.That(after.SurfaceAttachment.HasValue, Is.False);
        }

        [Test]
        public void RayVisibleSupportIsRejectedWhenOnlyOtherActorAnatomyOccupiesDestination()
        {
            GameplayActorProxy human = fixture.World.Actors[1];
            GameplayBodySurface anatomy = human.BodySurfaces.Values.First();
            anatomy.transform.position = Fixture.SurfaceTarget;
            Physics.SyncTransforms();

            Collider[] solidsOnly = Physics.OverlapSphere(Fixture.SurfaceTarget, .054f,
                fixture.World.GeometryMask, QueryTriggerInteraction.Ignore);
            Collider[] includingTriggers = Physics.OverlapSphere(Fixture.SurfaceTarget, .054f,
                fixture.World.GeometryMask, QueryTriggerInteraction.Collide);
            Assert.That(solidsOnly.Contains(human.MotorCollider), Is.False,
                "The human locomotion capsule must remain outside this isolated anatomy fixture.");
            Assert.That(solidsOnly.Contains(anatomy.Collider), Is.False);
            Assert.That(includingTriggers.Contains(anatomy.Collider), Is.True,
                "Only the authored anatomy trigger occupies the destination body volume.");

            Assert.That(fixture.World.TrySurface(new SurfaceQuery(2, Fixture.MosquitoStart.ToFloat(),
                Vector3.left.ToFloat(), .25f), out _), Is.False);
        }

        private sealed class Fixture : IDisposable
        {
            internal static readonly Vector3 MosquitoStart = new Vector3(.2f, 0, 0);
            internal static readonly Vector3 SurfaceTarget = new Vector3(.057f, 0, 0);
            private readonly GameObject owner = new GameObject("Surface destination clearance fixture");
            private readonly Transform map;
            private readonly GameplayAuthority authority;
            private uint inputSequence, actionSequence;

            internal readonly UnityGameplayWorld World;
            internal readonly Collider Support;
            internal ActorSnapshot Mosquito => authority.CaptureSnapshot().Actors.Single(actor => actor.ActorId == 2);

            internal Fixture()
            {
                World = owner.AddComponent<UnityGameplayWorld>();
                map = new GameObject("Authored map").transform;
                map.SetParent(owner.transform, false);
                World.MapRoot = map;

                var support = new GameObject("Ray-visible support");
                support.transform.SetParent(map, false);
                var box = support.AddComponent<BoxCollider>();
                box.center = new Vector3(-.01f, 0, 0);
                box.size = new Vector3(.02f, 1, 1);
                Support = box;
                support.AddComponent<GameplaySurface>().SurfaceId = 1;

                authority = new GameplayAuthority(World);
                authority.BeginRound(new GameplayRoundConfig(1, 1, "clearance-fixture", "clearance", 300, 100),
                    new[]
                    {
                        new SpawnActor(1, "human", PlayerRole.Human, new Float3(10, 0, 10)),
                        new SpawnActor(2, "mosquito", PlayerRole.Mosquito, MosquitoStart.ToFloat()),
                        new SpawnActor(3, "other-mosquito", PlayerRole.Mosquito, new Float3(10, 2, 10))
                    });
                Physics.SyncTransforms();
            }

            internal Collider AddBlocker(string name, Vector3 center, Vector3 size)
            {
                var blocker = new GameObject(name);
                blocker.transform.SetParent(map, false);
                blocker.transform.localPosition = center;
                var collider = blocker.AddComponent<BoxCollider>();
                collider.size = size;
                return collider;
            }

            internal void BeginApproach()
            {
                SubmitAim();
                Assert.That(authority.SubmitAction("mosquito", new PlayerActionCommand(
                    Header(++actionSequence), ActionKind.PerchToggle, Vector3.left.ToFloat())),
                    Is.EqualTo(CommandReject.None));
                Advance();
            }

            internal void Advance()
            {
                SubmitAim();
                authority.Advance(new HostTick(authority.CurrentTick + 1));
            }

            private void SubmitAim()
            {
                Assert.That(authority.SubmitInput("mosquito", new PlayerInputCommand(
                    Header(++inputSequence), default, 0, -Mathf.PI / 2, 0, Vector3.left.ToFloat())),
                    Is.EqualTo(CommandReject.None));
            }

            private CommandHeader Header(uint sequence) =>
                new CommandHeader(1, 1, 2, sequence, authority.CurrentTick, Mosquito.ViewRevision);

            public void Dispose()
            {
                Object.DestroyImmediate(owner);
                Physics.SyncTransforms();
            }
        }
    }
}
