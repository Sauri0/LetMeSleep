using System;
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
    public sealed class GameplayBotSteeringTraversalPlayModeTests
    {
        private GameObject owner;
        private UnityGameplayWorld world;
        private MethodInfo steer;
        private readonly List<Mesh> meshes = new List<Mesh>();

        [SetUp]
        public void SetUp()
        {
            owner = new GameObject("Bot steering traversal fixture");
            world = owner.AddComponent<UnityGameplayWorld>();
            var map = new GameObject("Map");
            map.transform.SetParent(owner.transform, false);
            world.MapRoot = map.transform;
            Box("Floor", new Vector3(0, -.05f, 0), new Vector3(8, .1f, 8));
            world.BeginRound(new[]
            {
                new SpawnActor(1, "human", PlayerRole.Human, new Float3(0, .002f, 0))
            }, Array.Empty<DoorDefinition>());
            steer = typeof(UnityGameplayWorld).GetMethod("SteerBot",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(steer, Is.Not.Null);
            typeof(UnityGameplayWorld).GetField("captureBotSteeringDiagnostic",
                    BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(world, true);
        }

        [TearDown]
        public void TearDown()
        {
            if (owner) Object.DestroyImmediate(owner);
            foreach (var mesh in meshes) Object.DestroyImmediate(mesh);
            meshes.Clear();
            owner = null; world = null; steer = null;
        }

        [Test]
        public void LowStepTraversableByHumanMotorKeepsDirectRoute()
        {
            Box("Low step", new Vector3(.5f, .075f, 0), new Vector3(.5f, .15f, 1.5f));

            Float3 result = Steer(new Float3(1, 0, 0));
            MotorResult moved = RunMotor(20);
            TestContext.Out.WriteLine("steering={0} traversal={1}",
                Diagnostic("lastBotSteeringDiagnostic"), Diagnostic("lastBotTraversalDiagnostic"));

            Assert.That(moved.Position.X, Is.GreaterThan(.8f),
                "The real motor with desired velocity reapplied each tick must cross the complete low step.");
            Assert.That(moved.Grounded, Is.True);
            Assert.That(result.X, Is.GreaterThan(.98f));
            Assert.That(Mathf.Abs(result.Z), Is.LessThan(.02f));
        }

        [Test]
        public void LowStepSnapshotsAtBotCadenceKeepDirectRoute()
        {
            Box("Low step", new Vector3(.5f, .075f, 0), new Vector3(.5f, .15f, 1.5f));
            var position = new Float3(0, .002f, 0);
            float verticalVelocity = 0;
            bool grounded = true, sawAirborne = false;
            int decisions = 0;
            MotorResult moved = default;
            for (int currentTick = 1; currentTick <= 20; currentTick++)
            {
                verticalVelocity = grounded ? -.5f : verticalVelocity - 12f / 30f;
                moved = world.MoveHuman(new MotorQuery(1, position,
                    new Float3(3.1f, verticalVelocity, 0), 1f / 30f, 1.72f, .25f, 0, grounded));
                position = moved.Position;
                verticalVelocity = moved.Velocity.Y;
                grounded = moved.Grounded;
                sawAirborne |= !grounded;

                // Runtime observes actor 1 at current ticks 1, 4, 7... before the
                // following authority move: (currentTick + 1 + actorId) % 3 == 0.
                if ((currentTick + 2) % 3 != 0) continue;
                decisions++;
                Float3 result = Steer(new Float3(1, 0, 0), position,
                    moved.Velocity, grounded);
                TestContext.Out.WriteLine("tick={0} grounded={1} vy={2:R} x={3:R} steer={4:R},{5:R}",
                    currentTick, grounded, moved.Velocity.Y, position.X, result.X, result.Z);
                Assert.That(result.X, Is.GreaterThan(.98f),
                    "The bot must keep its direct route at every real cadence snapshot while crossing the step.");
                Assert.That(Mathf.Abs(result.Z), Is.LessThan(.02f));
            }

            TestContext.Out.WriteLine("sawAirborne={0}", sawAirborne);
            Assert.That(decisions, Is.EqualTo(7));
            Assert.That(moved.Position.X, Is.GreaterThan(.8f));
        }

        [Test]
        public void FullHeightFrontalWallStillRequiresDetour()
        {
            Box("High wall", new Vector3(.5f, 1, 0), new Vector3(.1f, 2, 1.5f));

            Float3 result = Steer(new Float3(1, 0, 0));

            Assert.That(Float3.Dot(result.Normalized, new Float3(1, 0, 0)), Is.LessThan(.9f),
                "A wall that the full capsule cannot cross must not be reported as direct travel.");
        }

        [Test]
        public void FullHeightObliqueWallStillRequiresDetour()
        {
            Box("High oblique wall", new Vector3(.55f, 1, .05f), new Vector3(.1f, 2, 2),
                Quaternion.Euler(0, 45, 0));

            Float3 result = Steer(new Float3(1, 0, 0));

            Assert.That(Float3.Dot(result.Normalized, new Float3(1, 0, 0)), Is.LessThan(.9f),
                "Sliding along a tall oblique wall is not proof that the requested route crosses it.");
        }

        [Test]
        public void LowStepDoesNotLicenseTallObliqueWallBehindIt()
        {
            Box("Low step", new Vector3(.4f, .075f, 0), new Vector3(.3f, .15f, 1.2f));
            Box("Tall oblique wall behind step", new Vector3(.85f, 1, 0), new Vector3(.1f, 2, .8f),
                Quaternion.Euler(0, 45, 0));

            Float3 result = Steer(new Float3(1, 0, 0));

            Assert.That(Float3.Dot(result.Normalized, new Float3(1, 0, 0)), Is.LessThan(.9f),
                "Every predicted contact must be traversable; the first low step cannot authorize a later wall.");
        }

        [Test]
        public void PlatformDropDoesNotTurnTallBarrierIntoAReachableStep()
        {
            Box("Raised platform", new Vector3(-.225f, .075f, 0), new Vector3(.55f, .15f, 1.5f));
            Box("Thirty centimetre barrier", new Vector3(.75f, .15f, 0), new Vector3(.2f, .3f, 1.5f));
            var start = new Float3(0, .152f, 0);
            ResetHuman(start);

            Float3 result = Steer(new Float3(1, 0, 0), start);
            MotorResult moved = RunMotor(20, start);

            Assert.That(moved.Position.X, Is.LessThan(.42f),
                "The real motor descends from the platform and must remain before the 30 cm barrier.");
            Assert.That(Float3.Dot(result.Normalized, new Float3(1, 0, 0)), Is.LessThan(.9f),
                "The predictor must snap to lower support before classifying the following obstacle height.");
        }

        [Test]
        public void LowStepUnderLowCeilingStillRequiresDetour()
        {
            Box("Low step", new Vector3(.5f, .075f, 0), new Vector3(.5f, .15f, 1.5f));
            Box("Low ceiling", new Vector3(.55f, 1.82f, 0), new Vector3(.5f, .1f, 1.5f));

            Float3 result = Steer(new Float3(1, 0, 0));

            Assert.That(Float3.Dot(result.Normalized, new Float3(1, 0, 0)), Is.LessThan(.9f),
                "A low obstacle is not stepable when the complete raised capsule lacks headroom.");
        }

        [Test]
        public void TallCompoundStairKeepsRouteThroughRealAirborneSnapshots()
        {
            // One non-convex collider, with the riser/run measured on the Yate stair.
            // Its total height exceeds the step limit; each individual rise does not.
            var boxes = new List<Bounds>();
            const float rise = .16875f, run = .30625f;
            for (int step = 0; step < 6; step++)
            {
                float top = (step + 1) * rise;
                boxes.Add(new Bounds(new Vector3(.65f + (step + .5f) * run, top * .5f, 0),
                    new Vector3(run, top, 1.34f)));
            }
            CompoundBoxes("Tall compound staircase", boxes);
            Box("Upper landing", new Vector3(3.2f, 3 * rise, 0), new Vector3(1.425f, 6 * rise, 1.34f));
            Physics.SyncTransforms();
            MotorResult moved = world.MoveHuman(new MotorQuery(1, new Float3(0, .002f, 0),
                new Float3(0, -.4f, 0), 1f / 30f, 1.72f, .25f, 0, false));
            Assert.That(moved.Grounded, Is.True, "The initial floor support must come from the real motor.");
            bool sawAirborne = false;
            Float3 commanded = new Float3(1, 0, 0);
            for (int tick = 1; tick <= 100 && moved.Position.X < 3; tick++)
            {
                if ((tick - 1) % 3 == 0)
                {
                    commanded = Steer(new Float3(1, 0, 0), moved.Position, moved.Velocity, moved.Grounded);
                    TestContext.Out.WriteLine("compound tick={0} position={1} grounded={2} velocity={3} steering={4} traversal={5}",
                        tick, moved.Position, moved.Grounded, moved.Velocity,
                        Diagnostic("lastBotSteeringDiagnostic"), Diagnostic("lastBotTraversalDiagnostic"));
                    Assert.That(commanded.X, Is.GreaterThan(.98f),
                        "The compound staircase must stay direct at real grounded/airborne snapshots. " +
                        Diagnostic("lastBotSteeringDiagnostic") + " " + Diagnostic("lastBotTraversalDiagnostic"));
                }
                float vertical = moved.Grounded ? -.5f : moved.Velocity.Y - 12f / 30f;
                moved = world.MoveHuman(new MotorQuery(1, moved.Position,
                    new Float3(commanded.X * 3.1f, vertical, commanded.Z * 3.1f),
                    1f / 30f, 1.72f, .25f, 0, moved.Grounded));
                sawAirborne |= !moved.Grounded;
            }
            Assert.That(moved.Position.X, Is.GreaterThanOrEqualTo(3));
            Assert.That(sawAirborne, Is.True, "Exercise projected ascent rather than inventing an airborne snapshot.");
            for (int tick = 0; tick < 12 && !moved.Grounded; tick++)
                moved = world.MoveHuman(new MotorQuery(1, moved.Position,
                    new Float3(0, moved.Velocity.Y - 12f / 30f, 0), 1f / 30f, 1.72f, .25f, 0, moved.Grounded));
            Assert.That(moved.Grounded, Is.True);
            Assert.That(moved.Position.Y, Is.EqualTo(6 * rise).Within(.01f));
        }

        [Test]
        public void LowTreadDoesNotLicenseTallWallInSameMesh()
        {
            CompoundBoxes("Low tread and tall wall", new[] {
                new Bounds(new Vector3(.4f, .084375f, 0), new Vector3(.3f, .16875f, 1.5f)),
                new Bounds(new Vector3(.85f, 1, 0), new Vector3(.1f, 2, 1.5f)) });
            Float3 result = Steer(new Float3(1, 0, 0));
            MotorResult moved = RunMotor(20);
            TestContext.Out.WriteLine("compound wall steering={0} traversal={1} actualEnd={2}",
                Diagnostic("lastBotSteeringDiagnostic"), Diagnostic("lastBotTraversalDiagnostic"), moved.Position);
            Assert.That(moved.Position.X, Is.LessThan(.57f), "The real full capsule remains before the tall wall.");
            Assert.That(result.X, Is.LessThan(.9f),
                "A low contact on one mesh part must not authorize its later full-height wall.");
        }

        [Test]
        public void GroundedPlatformEdgeDoesNotCertifyUnsupportedAdvance()
        {
            world.MapRoot.Find("Floor").GetComponent<Collider>().enabled = false;
            Box("Platform ending ahead", new Vector3(-.95f, -.05f, 0), new Vector3(2.1f, .1f, 3));
            Physics.SyncTransforms();
            MotorResult support = world.MoveHuman(new MotorQuery(1, new Float3(0, .002f, 0),
                new Float3(0, -.4f, 0), 1f / 30f, 1.72f, .25f, 0, false));
            Assert.That(support.Grounded, Is.True);
            var actor = Snapshot(support.Position, support.Velocity, support.Grounded);
            var predict = typeof(UnityGameplayWorld).GetMethod("BotHumanCanTraverse", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(predict, Is.Not.Null);
            var arguments = new object[] { actor, Vector3.right, .65f, 0f };
            Assert.That((bool)predict.Invoke(world, arguments), Is.False,
                "Forward movement in air is not certification of a supported traversal.");
            Assert.That(world.Actors[1].transform.position, Is.EqualTo(support.Position.ToUnity()),
                "The support check must not snap or move the actual actor.");
            MotorResult dropped = RunMotor(20, support.Position);
            Assert.That(dropped.Grounded, Is.False);
            Assert.That(dropped.Position.Y, Is.LessThan(-.3f), "The real trajectory must exercise a drop.");
        }

        private Float3 Steer(Float3 desired) => Steer(desired, new Float3(0, .002f, 0));

        private Float3 Steer(Float3 desired, Float3 position) =>
            Steer(desired, position, Float3.Zero, true);

        private Float3 Steer(Float3 desired, Float3 position, Float3 velocity, bool grounded)
        {
            Physics.SyncTransforms();
            var actor = Snapshot(position, velocity, grounded);
            return (Float3)steer.Invoke(world, new object[] { actor, desired });
        }

        private static ActorSnapshot Snapshot(Float3 position, Float3 velocity, bool grounded) =>
            new ActorSnapshot(1, PlayerRole.Human, LifeState.Active, 1,
                position, velocity, Rotation.Yaw(0), Float3.Forward,
                0, 0, 1, 1, grounded, 0, 0, null, null, default, 0);

        private MotorResult RunMotor(int ticks) => RunMotor(ticks, new Float3(0, .002f, 0));

        private string Diagnostic(string field) => typeof(UnityGameplayWorld)
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(world) as string ?? "missing";

        private MotorResult RunMotor(int ticks, Float3 start)
        {
            var position = start;
            bool grounded = true;
            float verticalVelocity = 0;
            MotorResult moved = default;
            for (int tick = 0; tick < ticks; tick++)
            {
                verticalVelocity = grounded ? -.5f : verticalVelocity - 12f / 30f;
                moved = world.MoveHuman(new MotorQuery(1, position,
                    new Float3(3.1f, verticalVelocity, 0),
                    1f / 30f, 1.72f, .25f, 0, grounded));
                position = moved.Position;
                verticalVelocity = moved.Velocity.Y;
                grounded = moved.Grounded;
            }
            return moved;
        }

        private void ResetHuman(Float3 position)
        {
            world.BeginRound(new[]
            {
                new SpawnActor(1, "human", PlayerRole.Human, position)
            }, Array.Empty<DoorDefinition>());
        }

        private BoxCollider Box(string name, Vector3 center, Vector3 size, Quaternion? rotation = null)
        {
            var item = new GameObject(name);
            item.transform.SetParent(world.MapRoot, false);
            item.transform.SetLocalPositionAndRotation(center, rotation ?? Quaternion.identity);
            var collider = item.AddComponent<BoxCollider>();
            collider.size = size;
            return collider;
        }

        private void CompoundBoxes(string name, IEnumerable<Bounds> boxes)
        {
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            int[] faces = { 0, 3, 2, 0, 2, 1, 4, 5, 6, 4, 6, 7, 0, 4, 7, 0, 7, 3,
                1, 2, 6, 1, 6, 5, 0, 1, 5, 0, 5, 4, 3, 7, 6, 3, 6, 2 };
            foreach (Bounds box in boxes)
            {
                Vector3 min = box.min, max = box.max; int offset = vertices.Count;
                vertices.AddRange(new[] {
                    new Vector3(min.x,min.y,min.z), new Vector3(max.x,min.y,min.z),
                    new Vector3(max.x,max.y,min.z), new Vector3(min.x,max.y,min.z),
                    new Vector3(min.x,min.y,max.z), new Vector3(max.x,min.y,max.z),
                    new Vector3(max.x,max.y,max.z), new Vector3(min.x,max.y,max.z) });
                foreach (int index in faces) triangles.Add(offset + index);
            }
            var mesh = new Mesh { name = name }; meshes.Add(mesh);
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
            var item = new GameObject(name); item.transform.SetParent(world.MapRoot, false);
            item.AddComponent<MeshCollider>().sharedMesh = mesh;
        }
    }
}
