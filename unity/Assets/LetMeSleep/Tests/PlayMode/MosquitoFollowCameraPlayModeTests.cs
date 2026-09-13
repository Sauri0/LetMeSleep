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

        [Test]
        public void BodyOcclusionUsesHysteresisAndRestoresOwnedRendererStates()
        {
            var cameraObject=new GameObject("OcclusionCamera",typeof(Camera));
            var actor=GameObject.CreatePrimitive(PrimitiveType.Cube);
            var child=GameObject.CreatePrimitive(PrimitiveType.Cube);
            var other=GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                actor.transform.localScale=Vector3.one*.2f;
                child.transform.SetParent(actor.transform,false); child.transform.localScale=Vector3.one*.25f;
                var own=actor.GetComponent<Renderer>(); var preHidden=child.GetComponent<Renderer>(); var foreign=other.GetComponent<Renderer>();
                preHidden.enabled=false; preHidden.forceRenderingOff=true;
                var camera=cameraObject.GetComponent<Camera>(); camera.enabled=false; camera.nearClipPlane=.02f; camera.aspect=1;
                var follow=cameraObject.AddComponent<MosquitoFollowCamera>();
                follow.BindAnchors(actor.transform,actor.transform);
                Assert.That(follow.BindLocalVisual(actor.transform,new[]{own,foreign},System.Array.Empty<Transform>()),Is.False);
                Assert.That(foreign.forceRenderingOff,Is.False);
                Assert.That(follow.BindLocalVisual(actor.transform,new[]{own,preHidden},System.Array.Empty<Transform>()),Is.True);
                camera.transform.position=actor.transform.position; ObserveOcclusion(follow);
                Assert.That(follow.IsLocalVisualHidden,Is.True); Assert.That(own.forceRenderingOff,Is.True);
                Assert.That(foreign.forceRenderingOff,Is.False); Assert.That(actor.GetComponent<Collider>().enabled,Is.True);
                var body=follow.BodyWorldBounds;
                float band=(follow.BodyEntryMargin+follow.BodyExitMargin)*.5f;
                camera.transform.position=body.center+Vector3.forward*(body.extents.z+band); ObserveOcclusion(follow);
                Assert.That(follow.IsLocalVisualHidden,Is.True,"Hidden state must persist inside the exit band.");
                camera.transform.position=body.center+Vector3.forward*(body.extents.z+follow.BodyExitMargin+.001f); ObserveOcclusion(follow);
                Assert.That(follow.IsLocalVisualHidden,Is.False); Assert.That(own.forceRenderingOff,Is.False);
                Assert.That(preHidden.forceRenderingOff,Is.True); Assert.That(preHidden.enabled,Is.False);
                camera.transform.position=body.center+Vector3.forward*(body.extents.z+band); ObserveOcclusion(follow);
                Assert.That(follow.IsLocalVisualHidden,Is.False,"A visible actor must not hide again inside the hysteresis band.");
                camera.transform.position=body.center; ObserveOcclusion(follow);
                follow.BindAnchors(other.transform,other.transform);
                Assert.That(own.forceRenderingOff,Is.False,"Changing actor must restore the old actor.");
                follow.BindLocalVisual(other.transform,new[]{foreign},System.Array.Empty<Transform>());
                camera.transform.position=other.transform.position; ObserveOcclusion(follow);
                follow.enabled=false; Assert.That(foreign.forceRenderingOff,Is.False,"Disable restores rendering.");
                follow.enabled=true; ObserveOcclusion(follow); follow.Unbind();
                Assert.That(foreign.forceRenderingOff,Is.False,"Unbind restores rendering.");
                follow.BindAnchors(other.transform,other.transform);
                follow.BindLocalVisual(other.transform,new[]{foreign},System.Array.Empty<Transform>()); ObserveOcclusion(follow);
                Object.DestroyImmediate(follow); Assert.That(foreign.forceRenderingOff,Is.False,"Destroying only the component restores rendering.");
            }
            finally { Object.DestroyImmediate(cameraObject); Object.DestroyImmediate(actor); Object.DestroyImmediate(other); }
        }

        [Test]
        public void CoreSkinBoundsExcludeWingExcursionAndFollowBoneScale()
        {
            var cameraObject=new GameObject("BoundsCamera",typeof(Camera));
            var actor=new GameObject("SkinnedActor"); var mesh=new Mesh();
            try
            {
                var body=new GameObject("Thorax").transform; body.SetParent(actor.transform,false);
                var wing=new GameObject("Wing.L").transform; wing.SetParent(actor.transform,false);
                mesh.vertices=new[]{new Vector3(-.1f,-.1f,-.1f),new Vector3(.1f,.1f,.1f),new Vector3(0,.1f,-.1f),new Vector3(4,0,0)};
                mesh.triangles=new[]{0,1,2,1,2,3};
                mesh.bindposes=new[]{Matrix4x4.identity,Matrix4x4.identity};
                mesh.boneWeights=new[]{new BoneWeight{boneIndex0=0,weight0=1},new BoneWeight{boneIndex0=0,weight0=1},new BoneWeight{boneIndex0=0,weight0=1},new BoneWeight{boneIndex0=1,weight0=1}};
                mesh.RecalculateBounds();
                var skin=actor.AddComponent<SkinnedMeshRenderer>(); skin.sharedMesh=mesh; skin.bones=new[]{body,wing}; skin.rootBone=body;
                actor.transform.localScale=Vector3.one*.5f;
                var camera=cameraObject.GetComponent<Camera>(); camera.enabled=false; camera.nearClipPlane=.02f;
                var follow=cameraObject.AddComponent<MosquitoFollowCamera>(); follow.BindAnchors(actor.transform,actor.transform);
                Assert.That(follow.BindLocalVisual(actor.transform,new Renderer[]{skin},new[]{body}),Is.True);
                ObserveOcclusion(follow);
                Assert.That(follow.BodyWorldBounds.size.x,Is.EqualTo(.1f).Within(.0001f),"Apply authored scale once; wing vertex must not inflate the body envelope.");
                body.localPosition=Vector3.right*.2f; ObserveOcclusion(follow);
                Assert.That(follow.BodyWorldBounds.center.x,Is.EqualTo(.1f).Within(.0001f));
            }
            finally { Object.DestroyImmediate(cameraObject); Object.DestroyImmediate(actor); Object.DestroyImmediate(mesh); }
        }

        [UnityTest]
        public IEnumerator WallMayCollapseOrbitWithoutEnteringWorldOrShowingOwnBody()
        {
            var cameraObject=new GameObject("WallCamera",typeof(Camera));
            var actor=GameObject.CreatePrimitive(PrimitiveType.Cube); actor.transform.localScale=Vector3.one*.2f;
            var wallObject=new GameObject("NearWall",typeof(BoxCollider));
            var wall=wallObject.GetComponent<BoxCollider>(); wall.size=new Vector3(3,3,.02f); wall.transform.position=new Vector3(0,0,-.2f);
            var camera=cameraObject.GetComponent<Camera>(); camera.enabled=false; camera.nearClipPlane=.02f; camera.aspect=1;
            camera.transform.position=new Vector3(0,0,-.85f);
            var follow=cameraObject.AddComponent<MosquitoFollowCamera>(); follow.SetCollisionFilter(c=>c==wall);
            follow.BindAnchors(actor.transform,actor.transform);
            var eye=new GameObject("AimForward").transform; eye.SetParent(actor.transform,false); eye.position=new Vector3(0,0,.15f);
            follow.BindLocalVisual(actor.transform,new[]{actor.GetComponent<Renderer>()},System.Array.Empty<Transform>(),eye);
            follow.SetView(Quaternion.identity,.85f);
            Physics.SyncTransforms();
            yield return new WaitForSecondsRealtime(.1f);
            Assert.That(follow.EffectiveRequestedDistance,Is.EqualTo(.85f).Within(.001f));
            Assert.That(follow.ResolvedDistance,Is.LessThan(.13f));
            Assert.That(follow.IsLocalVisualHidden,Is.True);
            Assert.That(ContainsCollider(Physics.OverlapSphere(camera.transform.position,.079f,-1,QueryTriggerInteraction.Ignore),wall),Is.False);
            Assert.That(actor.GetComponent<Collider>().enabled,Is.True);
            wall.enabled=false; follow.SetView(Quaternion.identity,0);
            yield return new WaitForSecondsRealtime(.1f);
            Assert.That(follow.DesiredDistance,Is.Zero,"Wheel zero must remain intentional first person.");
            Assert.That(follow.IsFirstPerson,Is.True); Assert.That(follow.ResolvedDistance,Is.LessThan(.001f));
            Assert.That(Vector3.Distance(camera.transform.position,eye.position),Is.LessThan(.001f));
            Assert.That(follow.IsLocalVisualHidden,Is.True,"First person must hide its body even when the eye is outside its bounds.");
            follow.SetView(Quaternion.identity,.85f);
            yield return new WaitForSecondsRealtime(.5f);
            Assert.That(follow.ResolvedDistance,Is.GreaterThan(.7f)); Assert.That(follow.IsLocalVisualHidden,Is.False);
            Object.Destroy(cameraObject); Object.Destroy(actor); Object.Destroy(wallObject); yield return null;
        }

        private static void ObserveOcclusion(MosquitoFollowCamera follow)
        {
            var method=typeof(MosquitoFollowCamera).GetMethod("UpdateLocalOcclusion",BindingFlags.Instance|BindingFlags.NonPublic);
            Assert.That(method,Is.Not.Null); method.Invoke(follow,null);
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
