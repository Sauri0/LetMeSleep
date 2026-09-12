using System.Collections;
using System.Reflection;
using LetMeSleep.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace LetMeSleep.Tests.PlayMode
{
    public sealed class MosquitoFollowCameraPlayModeTests
    {
        [UnityTest]
        public IEnumerator SurfacePerchesKeepAThirdPersonOrbitAndRecoverInFreeSpace()
        {
            var cameraObject = new GameObject("Camera", typeof(Camera));
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.enabled = false;
            MosquitoFollowCamera follow = cameraObject.AddComponent<MosquitoFollowCamera>();
            var anchor = new GameObject("Anchor").transform;
            var pivot = new GameObject("Pivot").transform;
            var solidObject = new GameObject("Solid");
            BoxCollider solid = solidObject.AddComponent<BoxCollider>();

            follow.SetCollisionFilter(collider => collider == solid);
            var cases = new[]
            {
                new SurfaceCase(
                    "floor", new Vector3(0f, -0.09f, 0f), new Vector3(10f, 0.18f, 10f),
                    new Vector3(0f, 0.056f, 0f)),
                new SurfaceCase(
                    "wall", new Vector3(-0.09f, 1f, 0f), new Vector3(0.18f, 10f, 10f),
                    new Vector3(0.056f, 1f, 0f)),
                new SurfaceCase(
                    "ceiling", new Vector3(0f, 2.09f, 0f), new Vector3(10f, 0.18f, 10f),
                    new Vector3(0f, 1.944f, 0f))
            };

            foreach (SurfaceCase item in cases)
            {
                solid.transform.position = item.SolidCenter;
                solid.transform.localScale = item.SolidSize;
                anchor.position = item.Anchor;
                pivot.position = item.Anchor;
                camera.transform.SetPositionAndRotation(item.Anchor, Quaternion.identity);
                Physics.SyncTransforms();

                follow.BindAnchors(anchor, pivot);
                follow.SetView(Quaternion.identity, 0.85f);
                yield return new WaitForSecondsRealtime(0.5f);

                Assert.That(follow.ResolvedDistance, Is.GreaterThan(0.65f),
                    $"A {item.Name} perch must preserve a usable third-person orbit.");
                Assert.That(ContainsCollider(Physics.OverlapSphere(
                        camera.transform.position, 0.079f, -1, QueryTriggerInteraction.Ignore), solid),
                    Is.False,
                    $"The camera must remain outside the {item.Name} solid.");
            }

            solid.enabled = false;
            Vector3 freePosition = new Vector3(0f, 1f, 0f);
            anchor.position = freePosition;
            pivot.position = freePosition;
            camera.transform.position = freePosition;
            follow.BindAnchors(anchor, pivot);
            follow.SetView(Quaternion.identity, 0.85f);
            yield return new WaitForSecondsRealtime(0.5f);

            Assert.That(follow.ResolvedDistance, Is.GreaterThan(0.75f),
                "The third-person orbit must recover after leaving a surface.");

            Object.Destroy(cameraObject);
            Object.Destroy(anchor.gameObject);
            Object.Destroy(pivot.gameObject);
            Object.Destroy(solidObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ThinAngledDoorBlocksTheCameraAndTheNegativeControlTraversesIt()
        {
            var cameraObject = new GameObject("Camera", typeof(Camera));
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.enabled = false;
            MosquitoFollowCamera follow = cameraObject.AddComponent<MosquitoFollowCamera>();
            var anchor = new GameObject("Anchor").transform;
            var pivot = new GameObject("Pivot").transform;
            var doorObject = new GameObject("ThinAngledDoor");
            BoxCollider door = doorObject.AddComponent<BoxCollider>();
            Vector3 pivotPosition = new Vector3(0f, 1f, 0f);

            anchor.position = pivotPosition;
            pivot.position = pivotPosition;
            door.transform.SetPositionAndRotation(
                new Vector3(0f, 1f, -0.42f), Quaternion.Euler(0f, 45f, 0f));
            door.transform.localScale = new Vector3(1.2f, 1.2f, 0.03f);
            camera.transform.SetPositionAndRotation(pivotPosition, Quaternion.identity);
            Physics.SyncTransforms();

            follow.SetCollisionFilter(collider => collider == door);
            follow.BindAnchors(anchor, pivot);
            follow.SetView(Quaternion.identity, 0.85f);
            float deadline = Time.realtimeSinceStartup + 0.5f;
            while (Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                Assert.That(ContainsCollider(Physics.OverlapSphere(
                        camera.transform.position, 0.079f, -1, QueryTriggerInteraction.Ignore), door),
                    Is.False,
                    "The camera sphere must not overlap a closed thin door on any frame.");
                Assert.That(SegmentHitsCollider(pivot.position, camera.transform.position, door),
                    Is.False,
                    "The camera path must not cross a closed thin door on any frame.");
            }

            Assert.That(follow.ResolvedDistance, Is.InRange(0.1f, 0.65f),
                "The camera must stop at a useful distance on the near side of the thin door.");

            follow.SetCollisionFilter(_ => false);
            SetCollisionMask(follow, 0);
            camera.transform.SetPositionAndRotation(pivotPosition, Quaternion.identity);
            follow.BindAnchors(anchor, pivot);
            follow.SetView(Quaternion.identity, 0.85f);
            bool negativeControlDetectedTraversal = false;
            deadline = Time.realtimeSinceStartup + 0.5f;
            while (Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                negativeControlDetectedTraversal |= ContainsCollider(Physics.OverlapSphere(
                    camera.transform.position, 0.079f, -1, QueryTriggerInteraction.Ignore), door);
                negativeControlDetectedTraversal |= SegmentHitsCollider(
                    pivot.position, camera.transform.position, door);
            }

            Assert.That(negativeControlDetectedTraversal, Is.True,
                "The negative control must detect traversal when both collision gates are disabled.");
            Assert.That(follow.ResolvedDistance, Is.GreaterThan(0.75f),
                "The negative control must reach the far side of the thin door.");

            Object.Destroy(cameraObject);
            Object.Destroy(anchor.gameObject);
            Object.Destroy(pivot.gameObject);
            Object.Destroy(doorObject);
            yield return null;
        }

        private static bool ContainsCollider(Collider[] colliders, Collider target)
        {
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] == target)
                    return true;
            }

            return false;
        }

        private static bool SegmentHitsCollider(Vector3 start, Vector3 end, Collider target)
        {
            Vector3 displacement = end - start;
            float distance = displacement.magnitude;
            if (distance <= 0.0001f)
                return false;

            RaycastHit[] hits = Physics.RaycastAll(
                start, displacement / distance, distance, -1, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].collider == target)
                    return true;
            }

            return false;
        }

        private static void SetCollisionMask(MosquitoFollowCamera follow, LayerMask mask)
        {
            FieldInfo field = typeof(MosquitoFollowCamera).GetField(
                "collisionMask", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(follow, mask);
        }

        private readonly struct SurfaceCase
        {
            public SurfaceCase(string name, Vector3 solidCenter, Vector3 solidSize, Vector3 anchor)
            {
                Name = name;
                SolidCenter = solidCenter;
                SolidSize = solidSize;
                Anchor = anchor;
            }

            public string Name { get; }
            public Vector3 SolidCenter { get; }
            public Vector3 SolidSize { get; }
            public Vector3 Anchor { get; }
        }
    }
}
