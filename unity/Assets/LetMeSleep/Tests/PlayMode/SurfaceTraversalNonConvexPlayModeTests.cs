using System;
using System.Collections.Generic;
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
