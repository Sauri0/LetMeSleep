using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using LetMeSleep.Core;
using LetMeSleep.Gameplay;
using LetMeSleep.Gameplay.Unity;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LetMeSleep.Tests.PlayMode
{
    public sealed class GameplayBotDetourPlayModeTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private GameObject owner;
        private UnityGameplayWorld world;
        private readonly List<Mesh> meshes = new List<Mesh>();
        private readonly List<Collider> obstacles = new List<Collider>();
        private readonly Float3 start = new Float3(.26f, .002f, -.10f);
        private readonly Float3 goal = new Float3(-1.45f, .002f, .10f);

        [SetUp]
        public void SetUp()
        {
            owner = new GameObject("Bot detour fixture");
            world = owner.AddComponent<UnityGameplayWorld>();
            var map = new GameObject("Map"); map.transform.SetParent(owner.transform, false);
            world.MapRoot = map.transform;
            Box(new Vector3(0, -.05f, 0), new Vector3(14, .1f, 14), false);
            // A compact prop widens behind its front contact. A second prop closes
            // the tempting near-side gap; the exposed outer side is physically open.
            Compound(new[] {
                new Bounds(new Vector3(-.5f, .765f, .68f), new Vector3(1, 1.47f, 1.44f)),
                new Bounds(new Vector3(.5f, .765f, .85f), new Vector3(1, 1.47f, 1.1f))
            });
            Box(new Vector3(0, .57f, -.825f), new Vector3(1.73f, 1.08f, .65f));
            Reset(start);
        }

        [TearDown]
        public void TearDown()
        {
            if (owner) Object.DestroyImmediate(owner);
            foreach (var mesh in meshes) Object.DestroyImmediate(mesh);
            meshes.Clear(); obstacles.Clear();
        }

        [Test]
        public void ActiveTaskEscapesReactiveCycleAndReleasesAfterPassingProp()
        {
            var reactive = Replay(false, 120);
            Assert.That(reactive.Reached, Is.False,
                "The synthetic scene must first reproduce a reactive local minimum.");
            Assert.That(reactive.Reversals, Is.GreaterThanOrEqualTo(2));
            Reset(start);
            var persistent = Replay(true, 120);
            Assert.That(persistent.SawDetour, Is.True, "Exercise acquired obstacle memory, not just a lucky reactive route.");
            Assert.That(persistent.Reached, Is.True, "The real motor must reach the far side within the unchanged finite budget.");
            Assert.That(Detour(), Is.Null, "Passing the known collider must release its side before resuming direct travel.");
        }

        [Test]
        public void WallAcrossExposedSideStillBlocksTheRealMotor()
        {
            Box(new Vector3(0, 1.5f, 1.55f), new Vector3(12, 3, .16f));
            var result = Replay(true, 90);
            Assert.That(result.SawDetour, Is.True);
            Assert.That(result.Reached, Is.False);
            Assert.That(result.MaximumZ, Is.LessThan(1.31f),
                "A stored corner cannot authorize crossing the intervening wall.");
        }

        [TestCase("objective")]
        [TestCase("stop")]
        [TestCase("reset")]
        public void IntentLifecycleDropsOldObstacle(string change)
        {
            AcquirePendingDetour();
            if (change == "reset") Invoke("ResetBotSteering");
            else Observe(Snapshot(start, Float3.Zero, true), 4, change == "objective" ? "another task" : null);
            Assert.That(Detour(), Is.Null);
            if (change != "objective") Assert.That(Intents().Count, Is.Zero);
        }

        [Test]
        public void ExpiredOrUnsteeredIntentCannotKeepTheSide()
        {
            AcquirePendingDetour();
            Observe(Snapshot(start, Float3.Zero, true), 91, "task");
            Assert.That(Detour(), Is.Null, "Ninety host ticks expire memory even if there was no intermediate callback.");
            Reset(start); AcquirePendingDetour();
            Observe(Snapshot(start, Float3.Zero, true), 4, "task");
            // A work/stop decision observes the actor but never invokes steering.
            Observe(Snapshot(start, Float3.Zero, true), 7, "task");
            Assert.That(Detour(), Is.Null);
        }

        [Test]
        public void NoPhysicalProgressExpiresWithinThirtyTicks()
        {
            AcquirePendingDetour();
            var actor = Snapshot(start, Float3.Zero, true);
            for (uint tick = 4; tick <= 34; tick += 3)
            {
                Observe(actor, tick, "task");
                Invoke("SteerBot", actor, (goal - start).Normalized);
            }
            Assert.That(Detour(), Is.Null, "A fixed contact must not retain a detour indefinitely.");
        }

        [Test]
        public void UnknownPatrolUsesTheExistingReactiveSelector()
        {
            var actor = Snapshot(start, Float3.Zero, true);
            var desired = (goal - start).Normalized;
            Observe(actor, 1, null);
            var result = (Float3)Invoke("SteerBot", actor, desired);
            Invoke("ResetBotSteering");
            var original = (Float3)Invoke("SteerBotReactive", actor, desired);
            Assert.That((result - original).Length, Is.LessThan(.00001f));
            Assert.That(Intents().Count, Is.Zero);
        }

        private sealed class ReplayResult
        {
            internal bool Reached, SawDetour;
            internal int Reversals;
            internal float MaximumZ = float.NegativeInfinity;
        }
        private ReplayResult Replay(bool persistent, int ticks)
        {
            var result = new ReplayResult();
            Float3 position = start, velocity = Float3.Zero, travel = Float3.Zero, previous = Float3.Zero;
            bool grounded = true;
            for (uint tick = 1; tick <= ticks; tick++)
            {
                var actor = Snapshot(position, velocity, grounded);
                world.SynchronizeActors(new[] { actor }); Physics.SyncTransforms();
                if ((tick - 1) % 3 == 0)
                {
                    Observe(actor, tick, persistent ? "task" : null);
                    travel = (Float3)Invoke("SteerBot", actor, (goal - position).Normalized);
                    if (Float3.Dot(previous, travel) < -.5f) result.Reversals++;
                    previous = travel; result.SawDetour |= Detour() != null;
                    TestContext.Out.WriteLine("persistent={0} tick={1} position={2} steer={3} detour={4}", persistent, tick,
                        position.ToUnity().ToString("F5"), travel.ToUnity().ToString("F5"), Detour() != null);
                }
                float vertical = grounded ? -.5f : velocity.Y - 12f / 30f;
                var moved = world.MoveHuman(new MotorQuery(1, position,
                    new Float3(travel.X * 3.1f, vertical, travel.Z * 3.1f),
                    1f / 30f, 1.72f, .25f, 0, grounded));
                position = moved.Position; velocity = moved.Velocity; grounded = moved.Grounded;
                Assert.That(grounded, Is.True, "The detour fixture has continuous level support.");
                AssertNoPenetration(position);
                result.MaximumZ = Mathf.Max(result.MaximumZ, position.Z);
                if ((goal - position).Length < .28f) { result.Reached = true; break; }
            }
            return result;
        }

        private void AcquirePendingDetour()
        {
            var actor = Snapshot(start, Float3.Zero, true);
            Observe(actor, 1, "task");
            Invoke("SteerBot", actor, (goal - start).Normalized);
            Assert.That(Detour(), Is.Not.Null, "The fixture must acquire its state through a real collider query.");
        }
        private IDictionary Intents() => (IDictionary)typeof(UnityGameplayWorld).GetField("humanSteeringIntents", Private).GetValue(world);
        private object Detour()
        {
            var intent = Intents()[1u];
            return intent?.GetType().GetField("Detour", Private).GetValue(intent);
        }
        private void Observe(ActorSnapshot actor, uint tick, string objective) => Invoke("ObserveBotSteeringIntent", actor, tick, objective);
        private object Invoke(string name, params object[] arguments) => typeof(UnityGameplayWorld).GetMethod(name, Private).Invoke(world, arguments);
        private static ActorSnapshot Snapshot(Float3 position, Float3 velocity, bool grounded) => new ActorSnapshot(1,
            PlayerRole.Human, LifeState.Active, 1, position, velocity, Rotation.Yaw(0), Float3.Forward,
            0, 0, 1, 1, grounded, 0, 0, null, null, default, 0);
        private void Reset(Float3 position)
        {
            Invoke("ResetBotSteering");
            world.BeginRound(new[] { new SpawnActor(1, "human", PlayerRole.Human, position) }, Array.Empty<DoorDefinition>());
            Physics.SyncTransforms();
        }
        private void AssertNoPenetration(Float3 position)
        {
            var query = new GameObject("Body query");
            try
            {
                var capsule = query.AddComponent<CapsuleCollider>();
                capsule.radius = .25f; capsule.height = 1.72f; capsule.center = Vector3.up * .86f;
                foreach (var obstacle in obstacles)
                {
                    bool overlaps = Physics.ComputePenetration(capsule, position.ToUnity(), Quaternion.identity,
                        obstacle, obstacle.transform.position, obstacle.transform.rotation, out _, out float depth);
                    Assert.That(overlaps && depth > .003f, Is.False, "Real capsule penetration: " + depth);
                }
            }
            finally { Object.DestroyImmediate(query); }
        }
        private void Box(Vector3 center, Vector3 size, bool obstruction = true)
        {
            var item = new GameObject("Box"); item.transform.SetParent(world.MapRoot, false);
            item.transform.localPosition = center;
            var collider = item.AddComponent<BoxCollider>(); collider.size = size;
            if (obstruction) obstacles.Add(collider);
        }
        private void Compound(IEnumerable<Bounds> boxes)
        {
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            int[] faces = { 0,3,2,0,2,1,4,5,6,4,6,7,0,4,7,0,7,3,1,2,6,1,6,5,0,1,5,0,5,4,3,7,6,3,6,2 };
            foreach (var box in boxes)
            {
                Vector3 min = box.min, max = box.max; int offset = vertices.Count;
                vertices.AddRange(new[] { new Vector3(min.x,min.y,min.z), new Vector3(max.x,min.y,min.z),
                    new Vector3(max.x,max.y,min.z), new Vector3(min.x,max.y,min.z), new Vector3(min.x,min.y,max.z),
                    new Vector3(max.x,min.y,max.z), new Vector3(max.x,max.y,max.z), new Vector3(min.x,max.y,max.z) });
                foreach (int index in faces) triangles.Add(offset + index);
            }
            var mesh = new Mesh(); meshes.Add(mesh); mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
            var item = new GameObject("Compound prop"); item.transform.SetParent(world.MapRoot, false);
            var collider = item.AddComponent<MeshCollider>(); collider.sharedMesh = mesh; obstacles.Add(collider);
        }
    }
}
