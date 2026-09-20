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
    public sealed class GameplayBotDecisionContinuationPlayModeTests
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private GameObject owner;
        private UnityGameplayWorld world;
        private int interval;

        [SetUp]
        public void SetUp()
        {
            owner = new GameObject("Bot decision continuation fixture");
            world = owner.AddComponent<UnityGameplayWorld>();
            var map = new GameObject("Map"); map.transform.SetParent(owner.transform, false);
            world.MapRoot = map.transform;
            Box("Floor", new Vector3(0, -.05f, 0), new Vector3(12, .1f, 12));
            interval = (int)(uint)typeof(GameplayRuntime).GetField("BotDecisionIntervalTicks",
                BindingFlags.Static | BindingFlags.NonPublic).GetRawConstantValue();
            Assert.That(interval, Is.EqualTo(3), "The witness must use the actual authority decision cadence.");
            Reset(new Float3(0, .002f, 0));
        }

        [TearDown]
        public void TearDown() { if (owner) Object.DestroyImmediate(owner); }

        [Test]
        public void RecordedBracketContinuesAcceptedIntervalWithoutCertifyingLaterChair()
        {
#if UNITY_EDITOR
            Object.DestroyImmediate(world.MapRoot.gameObject);
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/LetMeSleep/Content/Environment/HiggsfieldMaps/hf-yate-a-la-deriva-v3/Prefabs/hf-yate-a-la-deriva-v3.prefab");
            Assert.That(asset, Is.Not.Null);
            var map = Object.Instantiate(asset, owner.transform);
            map.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            map.transform.localScale = Vector3.one;
            world.MapRoot = map.transform;
            var position = new Float3(3.076826f, 3.40015936f, -6.80973673f);
            Reset(position);
            Physics.SyncTransforms();
            var support = (RaycastHit?)typeof(UnityGameplayWorld).GetMethod("CastMotor", Hidden).Invoke(world,
                new object[] { 1u, position.ToUnity(), Vector3.down * .01f, 1.72f, .25f, true });
            Assert.That(support.HasValue && support.Value.normal.y > .55f, Is.True,
                "The copied starting pose must have real walkable support.");
            var state = Snapshot(position, new Float3(-.0386328362f, 0, -3.09975815f), true);
            Vector3 target = new Vector3(0, 3.400159f, -5.15f), direction = Vector3.zero;
            // Reach the recorded212 state through six real moves from supported206.
            for (int tick = 0; tick < interval * 2; tick++)
            {
                if (tick % interval == 0) direction = Horizontal(target - state.Position.ToUnity());
                state = Move(state, direction);
            }
            Assert.That(Vector3.Distance(state.Position.ToUnity(), new Vector3(2.549894f, 3.43510842f, -6.562992f)),
                Is.LessThan(.002f), "Exercise the real bracket snapshot rather than inventing an airborne state.");
            direction = Horizontal(target - state.Position.ToUnity());
            var before = world.Actors[1].transform.position;
            var query = Clearance(state, direction);
            Assert.That((bool)query[6], Is.False, "The later chair still rejects the complete .65m horizon.");
            Assert.That((bool)query[8], Is.True, "Three complete supported ticks remain usable before the later contact.");
            Assert.That(world.Actors[1].transform.position, Is.EqualTo(before), "Prediction must not move the actor.");
            var selected = Steer(state, direction, 212);
            Assert.That(Vector3.Dot(selected, direction), Is.GreaterThan(.98f));
            for (int tick = 0; tick < interval; tick++) state = Move(state, selected);
            Assert.That(Vector3.Dot(state.Position.ToUnity() - before, direction), Is.GreaterThanOrEqualTo(.12f));
            Assert.That(state.Grounded, Is.True);
            // Continue with actual steering at cadence, not forced input through the chair.
            for (int tick = 215; tick <= 260 && Vector3.Distance(state.Position.ToUnity(), target) > .2f; tick++)
            {
                if ((tick - 215) % interval == 0)
                    selected = Steer(state, Horizontal(target - state.Position.ToUnity()), (uint)tick);
                state = Move(state, selected);
            }
            Assert.That(Vector3.Distance(state.Position.ToUnity(), target), Is.LessThan(.2f));
            Assert.That(state.Grounded, Is.True);
#else
            Assert.Ignore("Native editor PlayMode fixture requires the authored Yate prefab.");
#endif
        }

        [Test]
        public void LaterWallIsRequeriedBeforeFollowingInterval()
        {
            Box("Low tread", new Vector3(.4f, .084375f, 0), new Vector3(.3f, .16875f, 2));
            Box("Later wall", new Vector3(.85f, 1, 0), new Vector3(.1f, 2, 8));
            var state = Snapshot(new Float3(0, .002f, 0), Float3.Zero, true);
            typeof(UnityGameplayWorld).GetField("captureBotSteeringDiagnostic", Hidden)?.SetValue(world, true);
            var query = Clearance(state, Vector3.right);
            TestContext.Out.WriteLine("full={0} progress={1:R} continuation={2} traversal={3}", query[6], query[7], query[8],
                typeof(UnityGameplayWorld).GetField("lastBotTraversalDiagnostic", Hidden)?.GetValue(world));
            TraceDecisionPrefix(state, Vector3.right);
            Assert.That((bool)query[6], Is.False);
            Assert.That((bool)query[8], Is.False,
                "The initial climb advances only .07593m in three ticks: it must not grant continuation.");
            var initial = state.Position;
            for (int tick = 0; tick < interval; tick++)
            {
                state = Move(state, Vector3.right);
                TestContext.Out.WriteLine("initial real tick={0} position={1} velocity={2} grounded={3}",
                    tick + 1, state.Position.ToUnity().ToString("R"), state.Velocity.ToUnity().ToString("R"), state.Grounded);
            }
            Assert.That(state.Position.X - initial.X, Is.LessThan(.12f));
            Assert.That(state.Position.Y, Is.GreaterThan(initial.Y), "Generate the rising posture with the real motor.");
            query = Clearance(state, Vector3.right);
            TestContext.Out.WriteLine("after climb full={0} progress={1:R} continuation={2} traversal={3}", query[6], query[7], query[8],
                typeof(UnityGameplayWorld).GetField("lastBotTraversalDiagnostic", Hidden)?.GetValue(world));
            TraceDecisionPrefix(state, Vector3.right);
            Assert.That((bool)query[6], Is.False, "The complete horizon still contains the later wall.");
            Assert.That((bool)query[8], Is.True, "The next complete interval must be usable from the measured rising posture.");
            var continuationStart = state.Position;
            bool sawRejection = false; Vector3 selected = Vector3.right;
            for (int tick = 0; tick < interval * 6; tick++)
            {
                if (tick % interval == 0)
                {
                    selected = Steer(state, Vector3.right, (uint)tick + 1);
                    if (tick == 0) Assert.That(Vector3.Dot(selected, Vector3.right), Is.GreaterThan(.98f));
                    if (tick > 0 && Vector3.Dot(selected, Vector3.right) < .9f) sawRejection = true;
                }
                state = Move(state, selected);
                Assert.That(state.Position.X, Is.LessThan(.552f), "The actor must never cross the later tall wall.");
                if (tick == interval - 1)
                    Assert.That(state.Position.X - continuationStart.X, Is.GreaterThanOrEqualTo(.12f),
                        "The granted interval must produce the certified physical advance.");
            }
            Assert.That(sawRejection, Is.True, "A prefix must expire at the next decision, not persist as a free route.");
        }

        private void TraceDecisionPrefix(ActorSnapshot initial, Vector3 direction)
        {
            Vector3 position = initial.Position.ToUnity(); float vertical = initial.Velocity.Y;
            bool grounded = initial.Grounded;
            var tickMethod = typeof(UnityGameplayWorld).GetMethod("BotHumanTick", Hidden);
            var castMethod = typeof(UnityGameplayWorld).GetMethod("CastMotor", Hidden);
            var overlapMethod = typeof(UnityGameplayWorld).GetMethod("Overlap", Hidden);
            var proxy = world.Actors[1];
            for (int tick = 0; tick < interval; tick++)
            {
                vertical = grounded ? -.5f : vertical - 12f / 30f;
                Vector3 velocity = direction * 3.1f + Vector3.up * vertical;
                var arguments = new object[] { 1u, grounded, position, velocity, 1.72f, .25f, tick, false };
                bool accepted = (bool)tickMethod.Invoke(world, arguments);
                position = (Vector3)arguments[2]; velocity = (Vector3)arguments[3]; grounded = (bool)arguments[7];
                vertical = velocity.y;
                var support = (RaycastHit?)castMethod.Invoke(world,
                    new object[] { 1u, position, Vector3.down * .23f, 1.72f, .25f, true });
                var overlapArguments = new object[] { proxy.MotorCollider, position, proxy.transform.rotation, Vector3.zero };
                bool overlapping = (bool)overlapMethod.Invoke(world, overlapArguments);
                TestContext.Out.WriteLine("prefix tick={0} accepted={1} position={2} velocity={3} grounded={4} progress={5:R} support={6} normal={7} overlap={8} correction={9}",
                    tick + 1, accepted, position.ToString("R"), velocity.ToString("R"), grounded, Vector3.Dot(position - initial.Position.ToUnity(), direction),
                    support?.collider?.name ?? "none", (support?.normal ?? Vector3.zero).ToString("R"), overlapping, ((Vector3)overlapArguments[3]).ToString("R"));
                if (!accepted) break;
            }
        }

        [TestCase(0)]
        [TestCase(45)]
        public void InitialTallWallNeverGrantsDecisionContinuation(float yaw)
        {
            Box("Initial wall", new Vector3(.4f, 1, 0), new Vector3(.1f, 2, 3), Quaternion.Euler(0, yaw, 0));
            var state = Snapshot(new Float3(0, .002f, 0), Float3.Zero, true);
            Assert.That((bool)Clearance(state, Vector3.right)[8], Is.False);
            Assert.That(Vector3.Dot(Steer(state, Vector3.right, 1), Vector3.right), Is.LessThan(.9f));
        }

        [Test]
        public void WallWithinDecisionIntervalRejectsPrefix()
        {
            Box("Low tread", new Vector3(.3f, .075f, 0), new Vector3(.2f, .15f, 2));
            Box("Near wall", new Vector3(.48f, 1, 0), new Vector3(.1f, 2, 4));
            var state = Snapshot(new Float3(0, .002f, 0), Float3.Zero, true);
            Assert.That((bool)Clearance(state, Vector3.right)[8], Is.False);
        }

        [Test]
        public void PenetratingInitialBodyCannotUsePrefix()
        {
            var wall = Box("Initial overlap", new Vector3(.15f, 1, 0), new Vector3(.1f, 2, 3));
            Physics.SyncTransforms();
            var actor = world.Actors[1];
            Assert.That(Physics.ComputePenetration(actor.MotorCollider, actor.transform.position, actor.transform.rotation,
                wall, wall.transform.position, wall.transform.rotation, out _, out _), Is.True);
            Assert.That((bool)Clearance(Snapshot(new Float3(0, .002f, 0), Float3.Zero, true), Vector3.right)[8], Is.False);
        }

        [Test]
        public void UnsupportedPrefixCannotCertifyAPlatformExit()
        {
            world.MapRoot.Find("Floor").GetComponent<Collider>().enabled = false;
            Box("Ending platform", new Vector3(-.95f, -.05f, 0), new Vector3(2.1f, .1f, 3));
            Physics.SyncTransforms();
            var state = Snapshot(new Float3(0, .002f, 0), Float3.Zero, true);
            var arguments = new object[] { state, Vector3.right, .65f, true, 0f, false };
            bool full = (bool)typeof(UnityGameplayWorld).GetMethod("BotHumanTraversalPrediction", Hidden).Invoke(world, arguments);
            Assert.That(full, Is.False);
            Assert.That((bool)arguments[5], Is.False, "A later unsupported endpoint is not a rejected-contact continuation.");
        }

        [Test]
        public void InsufficientProgressBeforeWallCannotUsePrefix()
        {
            Box("Close wall", new Vector3(.34f, 1, 0), new Vector3(.1f, 2, 3));
            var state = Snapshot(new Float3(0, .002f, 0), Float3.Zero, true);
            Assert.That((bool)Clearance(state, Vector3.right)[8], Is.False);
            var start = state.Position;
            for (int tick = 0; tick < interval; tick++) state = Move(state, Vector3.right);
            Assert.That(state.Position.X - start.X, Is.LessThan(.12f));
        }

        private object[] Clearance(ActorSnapshot state, Vector3 direction)
        {
            world.SynchronizeActors(new[] { state }); Physics.SyncTransforms();
            var arguments = new object[] { state, direction, .65f, true, null, Vector3.zero, false, 0f, false };
            typeof(UnityGameplayWorld).GetMethod("BotClearanceCore", Hidden).Invoke(world, arguments);
            return arguments;
        }

        private Vector3 Steer(ActorSnapshot state, Vector3 direction, uint tick)
        {
            world.SynchronizeActors(new[] { state }); Physics.SyncTransforms();
            typeof(UnityGameplayWorld).GetMethod("ObserveBotSteeringIntent", Hidden).Invoke(world,
                new object[] { state, tick, "fixture.visible-own-objective" });
            return ((Float3)typeof(UnityGameplayWorld).GetMethod("SteerBot", Hidden).Invoke(world,
                new object[] { state, direction.ToFloat() })).ToUnity();
        }

        private ActorSnapshot Move(ActorSnapshot state, Vector3 direction)
        {
            float vertical = state.Grounded ? -.5f : state.Velocity.Y - 12f / 30f;
            var moved = world.MoveHuman(new MotorQuery(1, state.Position,
                (direction * 3.1f + Vector3.up * vertical).ToFloat(), 1f / 30f, 1.72f, .25f, 0, state.Grounded));
            var body = world.Actors[1].MotorCollider;
            foreach (var other in Physics.OverlapBox(body.bounds.center, body.bounds.extents + Vector3.one * .01f,
                         Quaternion.identity, world.GeometryMask, QueryTriggerInteraction.Ignore))
            {
                if (other == body || !world.IsWorldCollider(other) || other.GetComponentInParent<GameplayActorProxy>()) continue;
                bool forward = Physics.ComputePenetration(body, body.transform.position, body.transform.rotation,
                    other, other.transform.position, other.transform.rotation, out _, out float depth);
                bool reverse = Physics.ComputePenetration(other, other.transform.position, other.transform.rotation,
                    body, body.transform.position, body.transform.rotation, out _, out float reverseDepth);
                Assert.That(forward ? depth : 0, Is.LessThanOrEqualTo(.003f), "Motor contact must not become penetration.");
                Assert.That(reverse ? reverseDepth : 0, Is.LessThanOrEqualTo(.003f));
            }
            return Snapshot(moved.Position, moved.Velocity, moved.Grounded);
        }

        private void Reset(Float3 position) => world.BeginRound(new[]
        { new SpawnActor(1, "human", PlayerRole.Human, position) }, Array.Empty<DoorDefinition>());

        private static ActorSnapshot Snapshot(Float3 position, Float3 velocity, bool grounded) =>
            new ActorSnapshot(1, PlayerRole.Human, LifeState.Active, 1, position, velocity,
                Rotation.Identity, Float3.Forward, 0, 0, 1, 1, grounded, 0, 0, null, null, default, 0);

        private static Vector3 Horizontal(Vector3 value) { value.y = 0; return value.normalized; }

        private BoxCollider Box(string name, Vector3 center, Vector3 size, Quaternion? rotation = null)
        {
            var item = new GameObject(name); item.transform.SetParent(world.MapRoot, false);
            item.transform.SetLocalPositionAndRotation(center, rotation ?? Quaternion.identity);
            var collider = item.AddComponent<BoxCollider>(); collider.size = size;
            Physics.SyncTransforms(); return collider;
        }
    }
}
