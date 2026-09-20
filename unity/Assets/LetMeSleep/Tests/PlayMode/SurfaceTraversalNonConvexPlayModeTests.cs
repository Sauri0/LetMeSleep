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
    public sealed class SurfaceTraversalNonConvexPlayModeTests
    {
        private Fixture fixture;

        [SetUp]
        public void SetUp() => fixture = new Fixture();

        [TearDown]
        public void TearDown()
        {
            fixture?.Dispose();
            fixture = null;
        }

        [Test]
        public void ConnectedFloorAndWallAreNeighborsAndArrivalIsFree()
        {
            var origin = new Vector3(0, .057f, .39f);
            var previous = fixture.Acquire(origin, Vector3.down);

            Assert.That(fixture.World.TryFollowSurface(2, previous.Attachment, origin.ToFloat(),
                Vector3.forward.ToFloat(), out var next), Is.True);
            Assert.That(next.Attachment.SurfaceId, Is.EqualTo(2));
            Assert.That(Vector3.Dot(next.WorldNormal.ToUnity(), Vector3.back), Is.GreaterThan(.999f));

            Vector3 center = next.WorldPoint.ToUnity() + next.WorldNormal.ToUnity() * .057f;
            Assert.That(fixture.PenetratesMap(center, .054f), Is.False,
                "A valid transition must leave the mosquito sphere clear of authored geometry.");
        }

        [Test]
        public void ParallelUndersideStepOfTwoPointTwoZeroFiveMillimetersUsesItsRiser()
        {
            fixture.DisableDefaultWallAndCeiling();
            fixture.Floor.center = new Vector3(-.5f, .1f, 0);
            fixture.Floor.size = new Vector3(1, .2f, 2);
            fixture.Box("Adjacent higher underside", 4, new Vector3(.49f, .102205f, 0),
                new Vector3(1, .2f, 2));
            fixture.RefreshGeometry();

            var previous = fixture.Acquire(new Vector3(-.006f, -.057f, 0), Vector3.up);
            Assert.That(fixture.World.TryFollowSurface(2, previous.Attachment,
                new Float3(.010f, -.057f, 0), new Float3(1, 0, 0), out var next), Is.True);
            Assert.That(next.Attachment.SurfaceId, Is.EqualTo(4));
            Assert.That(next.WorldNormal.Y, Is.LessThan(-.98f));
        }

        [Test]
        public void ThreeMillimeterOpenGapDoesNotBecomeAParallelNeighbor()
        {
            fixture.DisableDefaultWallAndCeiling();
            fixture.Floor.center = new Vector3(-.5f, .1f, 0);
            fixture.Floor.size = new Vector3(1, .2f, 2);
            fixture.Box("Separate underside", 4, new Vector3(.503f, .102205f, 0),
                new Vector3(1, .2f, 2));
            fixture.RefreshGeometry();

            var previous = fixture.Acquire(new Vector3(-.006f, -.057f, 0), Vector3.up);
            bool followed = fixture.World.TryFollowSurface(2, previous.Attachment,
                new Float3(.010f, -.057f, 0), new Float3(1, 0, 0), out var next);
            Assert.That(followed && next.Attachment.SurfaceId == 4, Is.False,
                "An open gap cannot be inferred as a mesh join.");
        }

        [Test]
        public void FloatingPlateWithoutVerticalRiserIsRejected()
        {
            fixture.DisableDefaultWallAndCeiling();
            fixture.Floor.center = new Vector3(-.5f, -.1f, 0);
            fixture.Floor.size = new Vector3(1, .2f, 2);
            fixture.Box("Floating thin plate", 4, new Vector3(.5f, .002705f, 0),
                new Vector3(1, .001f, 2));
            fixture.RefreshGeometry();

            var previous = fixture.Acquire(new Vector3(-.006f, .057f, 0), Vector3.down);
            bool followed = fixture.World.TryFollowSurface(2, previous.Attachment,
                new Float3(.010f, .060205f, 0), new Float3(1, 0, 0), out var next);
            Assert.That(followed && next.Attachment.SurfaceId == 4, Is.False,
                "A nearby plate needs a physical riser before it can be traversed.");
        }

        [Test]
        public void PenetratingArrivalIsRejectedEvenWhenTheMeshJoinIsConnected()
        {
            var origin = new Vector3(0, .057f, .39f);
            var previous = fixture.Acquire(origin, Vector3.down);
            Vector3 wallArrival = new Vector3(0, .057f, .393f);
            fixture.Blocker("Arrival blocker", wallArrival + Vector3.right * .045f,
                new Vector3(.02f, .08f, .08f));
            fixture.RefreshGeometry();

            Assert.That(fixture.PenetratesMap(wallArrival, .054f), Is.True,
                "The synthetic blocker must overlap the otherwise valid arrival sphere.");
            bool followed = fixture.World.TryFollowSurface(2, previous.Attachment, origin.ToFloat(),
                Vector3.forward.ToFloat(), out var next);
            Assert.That(followed && next.Attachment.SurfaceId == 2, Is.False,
                "FreeMosquito must reject a geometrically penetrating destination.");
        }

        [TestCase(true)]
        [TestCase(false)]
        public void SmallBevelRequiresAnActualConnectingFace(bool includeBevel)
        {
            fixture.DisableDefaultWallAndCeiling();
            fixture.Bevel(includeBevel);
            fixture.RefreshGeometry();
            var previous = fixture.Acquire(new Vector3(0, .978f, -.057f), Vector3.forward);
            bool followed = fixture.World.TryFollowSurface(2, previous.Attachment,
                new Float3(0, 1.005f, -.057f), new Float3(0, 1, 0), out var next);
            Assert.That(followed, Is.EqualTo(includeBevel),
                "Two nearby faces need a physically witnessed connecting bevel.");
            if (followed)
            {
                Assert.That(next.WorldNormal.Y, Is.GreaterThan(.98f));
                Assert.That(fixture.PenetratesMap(next.WorldPoint.ToUnity() + next.WorldNormal.ToUnity() * .057f, .054f), Is.False);
            }
        }

        [TestCase(true)]
        [TestCase(false)]
        public void BevelConnectivityIsPreservedAfterRotationAndTranslation(bool connected)
        {
            fixture.DisableDefaultWallAndCeiling();
            Transform support = fixture.Bevel(true, connected ? 0 : .0075f);
            support.SetPositionAndRotation(new Vector3(13, 7, -11), Quaternion.Euler(23, 61, -37));
            fixture.RefreshGeometry();
            var previous = fixture.Acquire(support.TransformPoint(new Vector3(0, .978f, -.057f)),
                support.TransformDirection(Vector3.forward));
            bool followed = fixture.World.TryFollowSurface(2, previous.Attachment,
                support.TransformPoint(new Vector3(0, 1.005f, -.057f)).ToFloat(),
                support.TransformDirection(Vector3.up).ToFloat(), out var next);
            Assert.That(followed, Is.EqualTo(connected),
                "A nearby diagonal fragment separated by 7.5mm must not act as a connecting face.");
            if (followed)
            {
                Assert.That(Vector3.Dot(next.WorldNormal.ToUnity(), support.up), Is.GreaterThan(.98f));
                Assert.That(fixture.PenetratesMap(next.WorldPoint.ToUnity() + next.WorldNormal.ToUnity() * .057f, .054f), Is.False);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AuthorityCrossesTheBevelWithoutDetachingOrPenetrating(bool reverse)
        {
            fixture.DisableDefaultWallAndCeiling();
            fixture.Bevel(true);
            fixture.RefreshGeometry();
            foreach (var actor in fixture.World.Actors.Values.ToArray()) Object.DestroyImmediate(actor.gameObject);
            ((IDictionary<uint, GameplayActorProxy>)fixture.World.Actors).Clear();
            var authority = new GameplayAuthority(fixture.World);
            authority.BeginRound(new GameplayRoundConfig(1, 1, "house-patio-v1", "bevel-authority", 300, 100),
                new[]
                {
                    new SpawnActor(1, "h", PlayerRole.Human, new Float3(10, 0, 10)),
                    new SpawnActor(2, "m", PlayerRole.Mosquito,
                        (reverse ? new Vector3(0, 1.12f, .2f) : new Vector3(0, .78f, -.12f)).ToFloat())
                });
            uint sequence = 0;
            ActorSnapshot Self() => authority.CaptureSnapshot().Actors.Single(a => a.ActorId == 2);
            void Input(Vector3 aim, bool move)
            {
                aim.Normalize();
                float yaw = Mathf.Atan2(aim.x, aim.z);
                float pitch = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(aim.y, -1, 1)), -1.919863f, 1.553343f);
                Assert.That(authority.SubmitInput("m", new PlayerInputCommand(
                    new CommandHeader(1, 1, 2, ++sequence, authority.CurrentTick, Self().ViewRevision),
                    move ? new Float2(0, 1) : default, 0, yaw, pitch, MathEx.Aim(yaw, pitch))),
                    Is.EqualTo(CommandReject.None));
            }
            void Tick(Vector3 aim, bool move)
            {
                var previous = Self().Position;
                Input(aim, move);
                authority.Advance(new HostTick(authority.CurrentTick + 1));
                Assert.That((Self().Position - previous).Length, Is.LessThan(.04f), "Unbounded motor step");
                Assert.That(fixture.PenetratesMap(Self().Position.ToUnity(), .054f), Is.False,
                    "Traversal penetrated authored geometry at tick " + authority.CurrentTick);
            }
            Vector3 oldNormal = reverse ? Vector3.up : Vector3.back;
            Input(-oldNormal, false);
            Assert.That(authority.SubmitAction("m", new PlayerActionCommand(
                new CommandHeader(1, 1, 2, 1, authority.CurrentTick, Self().ViewRevision),
                ActionKind.PerchToggle, Self().ViewForward)), Is.EqualTo(CommandReject.None));
            for (int i = 0; i < 20; i++) Tick(-oldNormal, false);
            Assert.That(Self().LifeState, Is.EqualTo(LifeState.Surface), "Initial approach failed");
            Vector3 tangent = reverse ? Vector3.back : Vector3.up;
            for (int i = 0; i < 15; i++) Tick(tangent, false);
            Vector3 wantedNormal = reverse ? Vector3.back : Vector3.up;
            for (int i = 0; i < 70; i++)
            {
                var self = Self();
                Assert.That(self.SurfaceAttachment.HasValue, Is.True, "Detached at tick " + authority.CurrentTick);
                Assert.That(fixture.World.ResolveSurface(self.SurfaceAttachment.Value, out var support), Is.True);
                Vector3 normal = support.WorldNormal.ToUnity();
                if (Vector3.Dot(normal, oldNormal) < .98f)
                {
                    tangent = SurfaceVisualFrame.TransportForward(oldNormal.ToFloat(), normal.ToFloat(), tangent.ToFloat()).ToUnity();
                    oldNormal = normal;
                }
                if (Vector3.Dot(normal, wantedNormal) > .98f && self.LifeState == LifeState.Surface)
                {
                    for (int j = 0; j < 5; j++) Tick(tangent, false);
                    Assert.That(Self().LifeState, Is.EqualTo(LifeState.Surface));
                    return;
                }
                Tick(tangent, true);
            }
            Assert.Fail("Did not arrive on the opposite face within the bounded traversal window");
        }

        private sealed class Fixture : IDisposable
        {
            private readonly List<Mesh> meshes = new List<Mesh>();
            private readonly GameObject owner = new GameObject("Nonconvex traversal fixture");
            private readonly Transform map;

            public readonly UnityGameplayWorld World;
            public readonly MeshBox Floor;
            private readonly MeshBox wall;
            private readonly MeshBox ceiling;

            public Fixture()
            {
                World = owner.AddComponent<UnityGameplayWorld>();
                map = new GameObject("Authored map").transform;
                map.SetParent(owner.transform, false);
                World.MapRoot = map;
                Floor = Box("Floor", 1, new Vector3(0, -.05f, 0), new Vector3(2, .1f, 2));
                wall = Box("Wall", 2, new Vector3(0, .5f, .5f), new Vector3(2, 1, .1f));
                ceiling = Box("Ceiling", 3, new Vector3(0, 1.05f, 0), new Vector3(2, .1f, 2));
                World.BeginRound(new[]
                {
                    new SpawnActor(1, "human", PlayerRole.Human, new Float3(10, 0, 10)),
                    new SpawnActor(2, "mosquito", PlayerRole.Mosquito, new Float3(0, .057f, 0))
                }, Array.Empty<DoorDefinition>());
                Physics.SyncTransforms();
            }

            public MeshBox Box(string name, uint id, Vector3 center, Vector3 size)
            {
                var go = new GameObject(name);
                go.transform.SetParent(map, false);
                var box = new MeshBox(go, center, size);
                meshes.Add(box.Mesh);
                go.AddComponent<GameplaySurface>().SurfaceId = id;
                return box;
            }

            public Transform Bevel(bool includeBevel, float disconnection = 0)
            {
                var go = new GameObject("7.5mm authored bevel"); go.transform.SetParent(map, false);
                var vertices = new List<Vector3>(); var triangles = new List<int>();
                void Quad(Vector3 a, Vector3 b)
                {
                    int start = vertices.Count;
                    vertices.Add(a + Vector3.left); vertices.Add(a + Vector3.right);
                    vertices.Add(b + Vector3.right); vertices.Add(b + Vector3.left);
                    triangles.AddRange(new[] { start, start+2, start+1, start, start+3, start+2 });
                }
                Quad(Vector3.zero, new Vector3(0, .9925f - disconnection, 0));
                if (includeBevel) Quad(new Vector3(0, .9925f, 0), new Vector3(0, 1, .0075f));
                Quad(new Vector3(0, 1, .0075f + disconnection), new Vector3(0, 1, 1));
                var mesh = new Mesh { name = "Owned bevel witness mesh" };
                mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds(); mesh.RecalculateNormals();
                meshes.Add(mesh); go.AddComponent<MeshCollider>().sharedMesh = mesh;
                go.AddComponent<GameplaySurface>().SurfaceId = 10;
                return go.transform;
            }

            public void Blocker(string name, Vector3 center, Vector3 size)
            {
                var go = new GameObject(name);
                go.transform.SetParent(map, false);
                go.transform.localPosition = center;
                go.AddComponent<BoxCollider>().size = size;
            }

            public void DisableDefaultWallAndCeiling()
            {
                wall.gameObject.SetActive(false);
                ceiling.gameObject.SetActive(false);
            }

            public void RefreshGeometry()
            {
                World.RegisterGeometry();
                Physics.SyncTransforms();
            }

            public SurfaceContact Acquire(Vector3 origin, Vector3 rayDirection)
            {
                Assert.That(World.TrySurface(new SurfaceQuery(2, origin.ToFloat(), rayDirection.ToFloat(), .12f),
                    out var contact), Is.True, "Cannot acquire the initial synthetic support.");
                return contact;
            }

            public bool PenetratesMap(Vector3 center, float radius)
            {
                var probe = new GameObject("Penetration probe");
                probe.transform.position = center;
                var sphere = probe.AddComponent<SphereCollider>();
                sphere.radius = radius;
                sphere.isTrigger = true;
                Physics.SyncTransforms();
                try
                {
                    foreach (var candidate in map.GetComponentsInChildren<Collider>())
                    {
                        if (!candidate.enabled || !candidate.gameObject.activeInHierarchy) continue;
                        if (Physics.ComputePenetration(sphere, center, Quaternion.identity,
                            candidate, candidate.transform.position, candidate.transform.rotation, out _, out _))
                            return true;
                    }
                    return false;
                }
                finally
                {
                    Object.DestroyImmediate(probe);
                }
            }

            public void Dispose()
            {
                Object.DestroyImmediate(owner);
                foreach (var mesh in meshes)
                    if (mesh) Object.DestroyImmediate(mesh);
            }
        }

        private sealed class MeshBox
        {
            private readonly MeshCollider collider;
            private Vector3 centerValue;
            private Vector3 sizeValue;

            public readonly GameObject gameObject;
            public readonly Mesh Mesh;

            public Vector3 center
            {
                get => centerValue;
                set { centerValue = value; Rebuild(); }
            }

            public Vector3 size
            {
                get => sizeValue;
                set { sizeValue = value; Rebuild(); }
            }

            public MeshBox(GameObject owner, Vector3 center, Vector3 size)
            {
                gameObject = owner;
                centerValue = center;
                sizeValue = size;
                Mesh = new Mesh { name = "Owned nonconvex synthetic box" };
                collider = owner.AddComponent<MeshCollider>();
                Rebuild();
            }

            private void Rebuild()
            {
                Vector3 half = sizeValue * .5f;
                Mesh.vertices = new[]
                {
                    new Vector3(-half.x, -half.y, -half.z) + centerValue,
                    new Vector3(half.x, -half.y, -half.z) + centerValue,
                    new Vector3(half.x, half.y, -half.z) + centerValue,
                    new Vector3(-half.x, half.y, -half.z) + centerValue,
                    new Vector3(-half.x, -half.y, half.z) + centerValue,
                    new Vector3(half.x, -half.y, half.z) + centerValue,
                    new Vector3(half.x, half.y, half.z) + centerValue,
                    new Vector3(-half.x, half.y, half.z) + centerValue
                };
                Mesh.triangles = new[]
                {
                    0, 2, 1, 0, 3, 2, 4, 5, 6, 4, 6, 7,
                    0, 1, 5, 0, 5, 4, 3, 7, 6, 3, 6, 2,
                    0, 4, 7, 0, 7, 3, 1, 2, 6, 1, 6, 5
                };
                Mesh.RecalculateNormals();
                Mesh.RecalculateBounds();
                collider.sharedMesh = null;
                collider.sharedMesh = Mesh;
                collider.convex = false;
            }
        }
    }
}
