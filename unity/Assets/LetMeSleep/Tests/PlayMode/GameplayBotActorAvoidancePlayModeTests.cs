using System;
using System.Reflection;
using LetMeSleep.Core;
using LetMeSleep.Gameplay;
using LetMeSleep.Gameplay.Unity;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LetMeSleep.Tests.PlayMode
{
    public sealed class GameplayBotActorAvoidancePlayModeTests
    {
        // Captured in Camp task-round diagnostic 02 when actors 3 and 5 stopped together.
        private static readonly Float3 CampActorThree = new Float3(5.23446131f, .0451587029f, .181695715f);
        private static readonly Float3 CampActorFive = new Float3(5.727688f, .04515673f, .269587755f);
        private GameObject owner;
        private UnityGameplayWorld world;
        private MethodInfo steer;

        [SetUp]
        public void SetUp()
        {
            owner = new GameObject("Bot actor avoidance fixture");
            world = owner.AddComponent<UnityGameplayWorld>();
            var map = new GameObject("Map");
            map.transform.SetParent(owner.transform, false);
            world.MapRoot = map.transform;
            Box("Floor", new Vector3(0, -.05f, 0), new Vector3(128, .1f, 128));
            steer = typeof(UnityGameplayWorld).GetMethod("SteerBot", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(steer, Is.Not.Null);
        }

        [TearDown]
        public void TearDown()
        {
            if (owner) Object.DestroyImmediate(owner);
            owner = null;
            world = null;
            steer = null;
        }

        [Test]
        public void RecordedCampTangentHumansDetourAndBothAdvance()
        {
            Begin(new SpawnActor(3, "camp-actor-three", PlayerRole.Human, CampActorThree),
                new SpawnActor(5, "camp-actor-five", PlayerRole.Human, CampActorFive));

            float startingSeparation = PlanarDistance(CampActorThree, CampActorFive);
            Assert.That(startingSeparation, Is.EqualTo(.5009966f).Within(.00001f),
                "Exercise the recorded Camp pair, whose .25 m motor capsules were nearly tangent.");

            Float3 towardFive = (CampActorFive - CampActorThree).Normalized;
            Float3 towardThree = (CampActorThree - CampActorFive).Normalized;
            Float3 positionThree = CampActorThree, positionFive = CampActorFive;
            Float3 velocityThree = Float3.Zero, velocityFive = Float3.Zero;
            bool groundedThree = true, groundedFive = true;
            bool firstThreeDetoured = Float3.Dot(Steer(3, positionThree, velocityThree, groundedThree, towardFive), towardFive) < .9f;
            bool firstFiveDetoured = Float3.Dot(Steer(5, positionFive, velocityFive, groundedFive, towardThree), towardThree) < .9f;
            float minimumSeparation = startingSeparation;

            for (int tick = 0; tick < 24; tick++)
            {
                Float3 commandThree = Steer(3, positionThree, velocityThree, groundedThree, towardFive);
                Float3 commandFive = Steer(5, positionFive, velocityFive, groundedFive, towardThree);
                MotorResult movedThree = Move(3, positionThree, velocityThree, groundedThree, commandThree);
                MotorResult movedFive = Move(5, positionFive, velocityFive, groundedFive, commandFive);
                positionThree = movedThree.Position; velocityThree = movedThree.Velocity; groundedThree = movedThree.Grounded;
                positionFive = movedFive.Position; velocityFive = movedFive.Velocity; groundedFive = movedFive.Grounded;
                minimumSeparation = Mathf.Min(minimumSeparation, PlanarDistance(positionThree, positionFive));
            }

            float progressThree = PlanarDistance(CampActorThree, positionThree);
            float progressFive = PlanarDistance(CampActorFive, positionFive);
            Assert.That(firstThreeDetoured, Is.True,
                "The steering query must see actor 5 as a physical rival instead of ordering actor 3 into its capsule.");
            Assert.That(firstFiveDetoured, Is.True,
                "The steering query must see actor 3 as a physical rival instead of ordering actor 5 into its capsule.");
            Assert.That(progressThree, Is.GreaterThan(.2f), "Actor 3 must make measurable progress around the rival.");
            Assert.That(progressFive, Is.GreaterThan(.2f), "Actor 5 must make measurable progress around the rival.");
            Assert.That(minimumSeparation, Is.GreaterThanOrEqualTo(.499f),
                "The pair may touch but the motor must not leave the two .25 m capsules interpenetrating.");
        }

        [Test]
        public void PhysicallyClearHumanNearWallCanSteerOutAndRemainGrounded()
        {
            // The wall face is .25977 m from the human center: clear for the .25 m
            // motor capsule, but overlapping by .00023 m for the old .26 m steering cast.
            Box("Near wall", new Vector3(.26977f, 1, 0), new Vector3(.02f, 2, 2));
            Float3 start = new Float3(0, .002f, 0);
            Begin(new SpawnActor(1, "human", PlayerRole.Human, start));

            Float3 desired = new Float3(-1, 0, 0);
            Float3 direction = Steer(1, start, Float3.Zero, true, desired);
            Float3 position = start, velocity = Float3.Zero;
            bool grounded = true;
            MotorResult moved = default;
            for (int tick = 0; tick < 8; tick++)
            {
                moved = Move(1, position, velocity, grounded, direction);
                position = moved.Position; velocity = moved.Velocity; grounded = moved.Grounded;
            }

            Assert.That(direction.LengthSquared, Is.GreaterThan(.9f),
                "A false overlap from an inflated steering capsule must not return a zero direction.");
            Assert.That(Float3.Dot(direction.Normalized, desired), Is.GreaterThan(.99f),
                "The physically clear human must retain the route away from the wall.");
            Assert.That(position.X, Is.LessThan(-.5f),
                "The real .25 m motor must advance toward the clear exit.");
            Assert.That(grounded, Is.True, "The exit control must retain floor support.");
        }

        [Test]
        public void OwnMotorColliderDoesNotCreateADetour()
        {
            Begin(new SpawnActor(1, "human", PlayerRole.Human, new Float3(0, .002f, 0)));

            Float3 desired = new Float3(1, 0, 0);
            Float3 direction = Steer(1, new Float3(0, .002f, 0), Float3.Zero, true, desired);

            Assert.That(Float3.Dot(direction.Normalized, desired), Is.GreaterThan(.99f));
        }

        [Test]
        public void HumanKeepsDirectRoutePastMosquitoMotorCollider()
        {
            Begin(new SpawnActor(1, "human", PlayerRole.Human, new Float3(0, .002f, 0)),
                new SpawnActor(2, "mosquito", PlayerRole.Mosquito, new Float3(.51f, .057f, 0)));

            Float3 desired = new Float3(1, 0, 0);
            Float3 direction = Steer(1, new Float3(0, .002f, 0), Float3.Zero, true, desired);
            MotorResult moved = Move(1, new Float3(0, .002f, 0), Float3.Zero, true, direction);

            Assert.That(Float3.Dot(direction.Normalized, desired), Is.GreaterThan(.99f),
                "Human locomotion does not block on a mosquito motor collider.");
            Assert.That(moved.Position.X, Is.GreaterThan(.09f));
        }

        private void Begin(params SpawnActor[] roster)
        {
            world.BeginRound(roster, Array.Empty<DoorDefinition>());
            Physics.SyncTransforms();
        }

        private Float3 Steer(uint actorId, Float3 position, Float3 velocity, bool grounded, Float3 desired)
        {
            Physics.SyncTransforms();
            var snapshot = new ActorSnapshot(actorId, PlayerRole.Human, LifeState.Active, 1,
                position, velocity, Rotation.Yaw(0), Float3.Forward,
                0, 0, 1, 1, grounded, 0, 0, null, null, default, 0);
            return (Float3)steer.Invoke(world, new object[] { snapshot, desired });
        }

        private MotorResult Move(uint actorId, Float3 position, Float3 velocity, bool grounded, Float3 direction)
        {
            float verticalVelocity = grounded ? -.5f : velocity.Y - 12f / 30f;
            return world.MoveHuman(new MotorQuery(actorId, position,
                new Float3(direction.X * 3.1f, verticalVelocity, direction.Z * 3.1f),
                1f / 30f, 1.72f, .25f, 0, grounded));
        }

        private BoxCollider Box(string name, Vector3 center, Vector3 size)
        {
            var item = new GameObject(name);
            item.transform.SetParent(world.MapRoot, false);
            item.transform.localPosition = center;
            var collider = item.AddComponent<BoxCollider>();
            collider.size = size;
            return collider;
        }

        private static float PlanarDistance(Float3 first, Float3 second) =>
            new Vector2(first.X - second.X, first.Z - second.Z).magnitude;
    }
}


