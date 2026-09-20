using System;
using LetMeSleep.Core;
using LetMeSleep.Gameplay;
using LetMeSleep.Gameplay.Unity;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LetMeSleep.Tests.PlayMode
{
    public sealed class HumanMotorTangencyPlayModeTests
    {
        private GameObject owner;
        private UnityGameplayWorld world;

        [SetUp]
        public void SetUp()
        {
            owner = new GameObject("Human motor tangency fixture");
            world = owner.AddComponent<UnityGameplayWorld>();
            var map = new GameObject("Map");
            map.transform.SetParent(owner.transform, false);
            world.MapRoot = map.transform;
            Box("Floor", new Vector3(0, -.05f, 0), new Vector3(8, .1f, 8));
            world.BeginRound(new[]
            {
                new SpawnActor(1, "human", PlayerRole.Human, new Float3(0, .002f, 0))
            }, Array.Empty<DoorDefinition>());
        }

        [TearDown]
        public void TearDown()
        {
            if (owner) Object.DestroyImmediate(owner);
            owner = null;
            world = null;
        }

        [Test]
        public void GroundSupportReturnedAgainAsTangentDoesNotConsumeHorizontalMotion()
        {
            var moved = Move(new Float3(3.1f, -.5f, 0), 1f / 30f);

            Assert.That(moved.Grounded, Is.True);
            Assert.That(moved.Position.X, Is.GreaterThan(.09f),
                "After grounding, a second tangent hit on the same floor must not stall the horizontal remainder.");
            Assert.That(Mathf.Abs(moved.Position.Z), Is.LessThan(.0001f));
        }

        [Test]
        public void PositiveClosingAgainstThinFrontalWallStillBlocks()
        {
            Box("Thin frontal wall", new Vector3(.5f, 1, 0), new Vector3(.01f, 2, 2));

            var moved = Move(new Float3(10, -.5f, 0), .1f);

            Assert.That(moved.Position.X, Is.InRange(.23f, .247f));
            Assert.That(moved.Velocity.X, Is.LessThan(.001f));
        }

        [Test]
        public void PositiveClosingAgainstThinDiagonalWallStillBlocks()
        {
            var wall = Box("Thin diagonal wall", new Vector3(.6f, 1, 0), new Vector3(.01f, 2, 2),
                Quaternion.Euler(0, 45, 0));

            var moved = Move(new Float3(10, -.5f, 0), .1f);

            float signedDistance = Vector3.Dot(moved.Position.ToUnity() - wall.transform.position,
                wall.transform.right);
            float requiredSeparation = .25f + wall.size.x * .5f - .003f;
            Assert.That(signedDistance, Is.LessThanOrEqualTo(-requiredSeparation),
                "The complete-radius capsule must remain on its starting side of the diagonal plane.");
            Assert.That(Mathf.Abs(moved.Position.Z), Is.GreaterThan(.01f),
                "The remaining displacement should slide along the diagonal instead of crossing it.");
        }

        [Test]
        public void GroundedMotorStillClimbsAuthoredStep()
        {
            Box("Step", new Vector3(.5f, .075f, 0), new Vector3(.5f, .15f, 2));

            MotorResult legacy = RunStep(true);
            MotorResult moved = RunStep(false);

            TestContext.Out.WriteLine("step legacy=({0:R},{1:R},{2:R}) grounded={3} fixed=({4:R},{5:R},{6:R}) grounded={7}",
                legacy.Position.X, legacy.Position.Y, legacy.Position.Z, legacy.Grounded,
                moved.Position.X, moved.Position.Y, moved.Position.Z, moved.Grounded);
            Assert.That(legacy.Position.Y, Is.GreaterThan(.14f));
            Assert.That(legacy.Grounded, Is.True);
            Assert.That(moved.Position.X, Is.GreaterThan(.30f),
                "Six input ticks must advance beyond three complete tick displacements after the lift.");
            Assert.That(moved.Position.Y, Is.GreaterThan(.14f));
            Assert.That(moved.Grounded, Is.True);
            Assert.That(moved.Position.X, Is.GreaterThanOrEqualTo(legacy.Position.X - .002f),
                "Discarding non-closing hits must not regress the measured step advance from the legacy predicate.");
            Assert.That(moved.Position.Y, Is.GreaterThanOrEqualTo(legacy.Position.Y - .002f));
        }

        private MotorResult RunStep(bool preserveLegacyNonClosingHits)
        {
            world.PreserveNonClosingMotorHitsForDiagnostic = preserveLegacyNonClosingHits;
            world.BeginRound(new[]
            {
                new SpawnActor(1, "human", PlayerRole.Human, new Float3(0, .002f, 0))
            }, Array.Empty<DoorDefinition>());
            MotorResult moved = default;
            var position = new Float3(0, .002f, 0);
            for (int i = 0; i < 6; i++)
            {
                moved = world.MoveHuman(new MotorQuery(1, position, new Float3(3.1f, -.5f, 0),
                    1f / 30f, 1.72f, .25f, 0, true));
                position = moved.Position;
            }
            return moved;
        }

        private MotorResult Move(Float3 velocity, float deltaSeconds)
        {
            Physics.SyncTransforms();
            return world.MoveHuman(new MotorQuery(1, new Float3(0, .002f, 0), velocity,
                deltaSeconds, 1.72f, .25f, 0, true));
        }

        private BoxCollider Box(string name, Vector3 center, Vector3 size, Quaternion? rotation = null)
        {
            var item = new GameObject(name);
            item.transform.SetParent(world.MapRoot, false);
            item.transform.SetPositionAndRotation(center, rotation ?? Quaternion.identity);
            var collider = item.AddComponent<BoxCollider>();
            collider.size = size;
            Physics.SyncTransforms();
            return collider;
        }
    }
}
