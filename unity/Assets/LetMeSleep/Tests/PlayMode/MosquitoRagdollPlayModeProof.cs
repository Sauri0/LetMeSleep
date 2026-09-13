#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using LetMeSleep.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace LetMeSleep.Tests.PlayMode
{
    // EXTERNAL recipe: Director may copy this file into the existing PlayMode test assembly.
    // Uses the real R4 prefab, an isolated PhysicsScene and a synthetic floor. No gameplay boot.
    public sealed class MosquitoRagdollPlayModeProof
    {
        private Scene scene;
        private GameObject actor;
        private MosquitoRagdollSimulation rig;

        [UnityTest, Timeout(30000)]
        public IEnumerator R4TransfersArticulatesContactsRestoresAndCleansUp()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/LetMeSleep/Content/Characters/Prefabs/LMS_Mosquito.prefab");
            Assert.That(prefab, Is.Not.Null, "Real R4 character prefab is required.");
            scene = SceneManager.CreateScene("R4 ragdoll proof " + Guid.NewGuid().ToString("N"), new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            actor = Object.Instantiate(prefab);
            SceneManager.MoveGameObjectToScene(actor, scene);
            foreach (var behaviour in actor.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
            foreach (var collider in actor.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            var animator = actor.GetComponentInChildren<Animator>(true);
            Assert.That(animator, Is.Not.Null);
            animator.enabled = false;
            actor.transform.SetPositionAndRotation(new Vector3(0, .65f, 0), Quaternion.Euler(0, 29, 12));
            PutSkinInBind(actor.transform);
            Transform head = Bone(actor.transform, "Head");
            head.localRotation *= Quaternion.Euler(10, 0, 0); // An evaluated, non-bind entry pose.
            Quaternion headBeforeBuild = head.rotation;
            var settings = new MosquitoRagdollSettings(new Vector3(0, -12, 0), 0);
            rig = MosquitoRagdollBuilder.Build(actor.transform, animator, settings);
            Assert.That(Quaternion.Angle(head.rotation, headBeforeBuild), Is.LessThan(.001f), "Build must not reset the animated skin.");
            Assert.That(Quaternion.Angle(head.rotation, rig.GetBody(MosquitoBodyId.Head).rotation), Is.GreaterThan(9f), "Joint neutral must come from bind, not current animation.");
            Assert.That(rig.BodyCount, Is.EqualTo(18));
            Assert.That(rig.JointCount, Is.EqualTo(17));
            Assert.That(rig.ColliderCount, Is.EqualTo(28));
            Assert.That(rig.PhysicalRoot.transform.parent, Is.Null);
            Assert.That(rig.PhysicalRoot.scene, Is.EqualTo(scene));
            float mass = 0;
            for (int i = 0; i < rig.BodyCount; i++)
            {
                var rb = rig.GetBody((MosquitoBodyId)i);
                Assert.That(Vector3.Distance(rb.transform.lossyScale, Vector3.one), Is.LessThan(.00001f));
                Assert.That(rb.isKinematic, Is.True);
                mass += rb.mass;
            }
            Assert.That(mass, Is.EqualTo(.060f).Within(.000001f));
            for (int i = 0; i < rig.ColliderCount; i++)
            {
                Assert.That(MosquitoRagdollPart.TryGet(rig.GetCollider(i), out var part), Is.True);
                Assert.That(part.Owner, Is.SameAs(rig));
                Assert.That(rig.GetCollider(i).enabled, Is.False);
            }
            for (int i = 0; i < rig.JointCount; i++)
            {
                var joint = rig.GetJoint(i);
                Assert.That(joint.xMotion, Is.EqualTo(ConfigurableJointMotion.Locked));
                Assert.That(joint.angularXDrive.positionSpring + joint.angularYZDrive.positionSpring + joint.slerpDrive.positionSpring, Is.Zero);
                Assert.That(joint.projectionMode, Is.EqualTo(JointProjectionMode.None));
            }

            var floor = new GameObject("Synthetic floor");
            SceneManager.MoveGameObjectToScene(floor, scene);
            floor.transform.position = new Vector3(0, -.05f, 0);
            var floorCollider = floor.AddComponent<BoxCollider>();
            floorCollider.size = new Vector3(6, .1f, 6);
            floorCollider.contactOffset = .0005f;
            var first = rig.CaptureAnimatedPose(Vector3.zero, Vector3.zero, actor.transform.position);
            float dt = Time.fixedDeltaTime;
            TestContext.WriteLine($"R4 configuration: dt={dt:F6}s; solver={settings.SolverIterations}/{settings.SolverVelocityIterations}; inertiaRatio={settings.MaxInertiaRatio}; angularDamping={settings.AngularDamping}; maxDepenetrationVelocity={settings.MaxDepenetrationVelocity}; projection=None; gapGate=0.012m");
            actor.transform.position += Vector3.right * (.1f * dt);
            head.localRotation *= Quaternion.Euler(dt * 40, 0, 0);
            var release = rig.CaptureAnimatedPose(first, dt);
            Assert.That(release.GetBody(MosquitoBodyId.Thorax).OriginVelocity.x, Is.EqualTo(.1f).Within(.001f));
            Assert.That(release.GetBody(MosquitoBodyId.Head).AngularVelocity.magnitude, Is.EqualTo(40 * Mathf.Deg2Rad).Within(.01f));
            Assert.Throws<ArgumentOutOfRangeException>(() => rig.CaptureAnimatedPose(first, 0));
            var savedHeadLocal = head.localRotation;
            rig.BeginLocalSimulation(release);
            var transferred = rig.CapturePose();
            for (int i = 0; i < rig.BodyCount; i++)
            {
                var expected = release.GetBody((MosquitoBodyId)i);
                var actual = transferred.GetBody((MosquitoBodyId)i);
                var inertia = rig.GetBody((MosquitoBodyId)i).inertiaTensor;
                Assert.That(Vector3.Distance(expected.Position, actual.Position), Is.LessThan(.00001f));
                Assert.That(Quaternion.Angle(expected.Rotation, actual.Rotation), Is.LessThan(.05f));
                Assert.That(Vector3.Distance(expected.OriginVelocity, actual.OriginVelocity), Is.LessThan(.001f), "Velocity must be converted to/from COM without drift.");
                Assert.That(inertia.x > 0 && inertia.y > 0 && inertia.z > 0, Is.True);
                Assert.That(inertia.magnitude, Is.LessThan(.001f), "Inertia must come from metre-scale colliders, not identity.");
                float ratio = Mathf.Max(inertia.x, Mathf.Max(inertia.y, inertia.z)) / Mathf.Min(inertia.x, Mathf.Min(inertia.y, inertia.z));
                Assert.That(ratio, Is.LessThanOrEqualTo(settings.MaxInertiaRatio + .001f), "Apply inertia conditioning after refreshing the release pose's compound shapes.");
                var rb = rig.GetBody((MosquitoBodyId)i);
                TestContext.WriteLine($"R4 body {i} {rb.name}: COM={rb.centerOfMass:F6}; inertia={inertia:E4}; angular={rb.angularVelocity:F4}");
            }
            AssertInternalExclusions();
            var jointBodies = new Rigidbody[rig.JointCount];
            var peakGaps = new float[rig.JointCount];
            for (int i = 0; i < rig.JointCount; i++)
            {
                var joint = rig.GetJoint(i);
                jointBodies[i] = joint.GetComponent<Rigidbody>();
                float nativeGap = NativeGap(joint, jointBodies[i]);
                TestContext.WriteLine($"R4 initial joint {i} {joint.name}: gap={nativeGap:F8}m; anchor={joint.anchor:F6}; connectedAnchor={joint.connectedAnchor:F6}; axis={joint.axis:F6}; secondary={joint.secondaryAxis:F6}");
                Assert.That(nativeGap, Is.LessThan(.0002f), "Before simulation, R4 pivots must coincide within 0.2mm.");
            }
            var thorax = rig.GetBody(MosquitoBodyId.Thorax);
            Quaternion initialRelative = Quaternion.Inverse(thorax.rotation) * rig.GetBody(MosquitoBodyId.Head).rotation;
            rig.ApplyImpulse(MosquitoBodyId.Thorax, new Vector3(.001f, 0, .001f), thorax.worldCenterOfMass + Vector3.up * .012f);
            bool touched = false, slept = false;
            float maxGap = 0, maxNativeGap = 0, maxPublicationLag = 0, articulation = 0, minHeight = float.PositiveInfinity;
            int firstBadStep = -1, peakStep = -1, peakJoint = -1;
            int steps = Mathf.CeilToInt(10f / dt);
            for (int step = 0; step < steps; step++)
            {
                yield return new WaitForFixedUpdate(); // Runs the module's gravity/sleep gate exactly once.
                scene.GetPhysicsScene().Simulate(dt); // Local physics scene is not simulated automatically.
                touched |= rig.HasEnvironmentContact;
                slept |= rig.IsSleeping;
                minHeight = Mathf.Min(minHeight, thorax.position.y);
                for (int i = 0; i < rig.JointCount; i++)
                {
                    var joint = rig.GetJoint(i);
                    maxGap = Mathf.Max(maxGap, Vector3.Distance(joint.transform.TransformPoint(joint.anchor), joint.connectedBody.transform.TransformPoint(joint.connectedAnchor)));
                    float gap = NativeGap(joint, jointBodies[i]);
                    peakGaps[i] = Mathf.Max(peakGaps[i], gap);
                    if (gap > maxNativeGap) { maxNativeGap = gap; peakStep = step; peakJoint = i; }
                    if (gap >= .012f && firstBadStep < 0) firstBadStep = step;
                    maxPublicationLag = Mathf.Max(maxPublicationLag, Vector3.Distance(jointBodies[i].position, jointBodies[i].transform.position));
                }
                articulation = Mathf.Max(articulation, Quaternion.Angle(initialRelative,
                    Quaternion.Inverse(thorax.rotation) * rig.GetBody(MosquitoBodyId.Head).rotation));
                Assert.That(float.IsNaN(thorax.position.y) || thorax.position.sqrMagnitude > 100, Is.False, "No nonfinite or explosive motion.");
                if (step < Mathf.CeilToInt(.6f / dt) || (step + 1) % Mathf.Max(1, Mathf.RoundToInt(1f / dt)) == 0)
                {
                    AssertInternalExclusions();
                    LogMotion(step, dt);
                }
            }
            TestContext.WriteLine($"R4 synthetic proof: contact={touched}; sleep={slept}; maxJointGap={maxGap:F6}m; headArticulation={articulation:F2}deg; minThoraxY={minHeight:F4}m");
            TestContext.WriteLine($"R4 native anchors: maxGap={maxNativeGap:F8}m; peakJoint={peakJoint}; peakStep={peakStep}; first12mmStep={firstBadStep}; maxTransformPositionLag={maxPublicationLag:F8}m");
            for (int i = 0; i < peakGaps.Length; i++) TestContext.WriteLine($"R4 peak joint {i} {rig.GetJoint(i).name}: nativeGap={peakGaps[i]:F8}m");
            Assert.That(touched, Is.True, "Must physically contact the synthetic environment.");
            Assert.That(minHeight, Is.InRange(-.02f, .25f), "Must fall and be supported instead of falling through the floor.");
            Assert.That(maxGap, Is.LessThan(.012f), "Initial 12mm gross-separation gate; report measured value for tighter tuning.");
            Assert.That(maxNativeGap, Is.LessThan(.012f), "The 12mm gate also applies to native physics anchors, independently of Transform publication.");
            Assert.That(articulation, Is.GreaterThan(2), "Connected bodies must articulate instead of acting as a rigid statue.");
            Assert.That(slept, Is.True, "Contact and low physical velocities should eventually allow sleep.");

            rig.ApplyImpulse(MosquitoBodyId.Thorax, Vector3.up * .003f, thorax.worldCenterOfMass);
            Assert.That(rig.IsSleeping, Is.False, "A second impact must wake the chain.");
            var stopped = rig.HoldPose();
            Assert.That(rig.Mode, Is.EqualTo(MosquitoRagdollMode.Held));
            rig.RestoreAnimation();
            Assert.That(Quaternion.Angle(head.localRotation, savedHeadLocal), Is.LessThan(.05f));
            Assert.That(animator.enabled, Is.False, "Restore original disabled Animator state.");
            animator.enabled = true; // Borrow and restore the other enabled-state path without running animation.
            rig.BeginRemotePose(stopped);
            Assert.That(animator.enabled, Is.False);
            Assert.Throws<InvalidOperationException>(() => rig.ApplyImpulse(MosquitoBodyId.Head, Vector3.up, Vector3.zero));
            Transform pupil = Bone(actor.transform, "Pupil.L");
            Quaternion pupilPose = Quaternion.Euler(7, 8, 9);
            pupil.localRotation = pupilPose;
            rig.ApplyRemotePose(stopped);
            yield return null; // Includes LateUpdate skin playback.
            Assert.That(Quaternion.Angle(pupil.localRotation, pupilPose), Is.LessThan(.05f), "The six facial bones remain unowned.");
            for (int i = 0; i < rig.BodyCount; i++)
            {
                Assert.That(rig.GetBody((MosquitoBodyId)i).isKinematic, Is.True);
                var expected = stopped.GetBody((MosquitoBodyId)i);
                Assert.That(Vector3.Distance(Bone(actor.transform, MosquitoRagdollPose.GetBoneName((MosquitoBodyId)i)).position, expected.Position), Is.LessThan(.0001f));
            }
            for (int i = 0; i < rig.ColliderCount; i++) Assert.That(rig.GetCollider(i).enabled, Is.False);
            var physicalRoot = rig.PhysicalRoot;
            rig.Dispose();
            Assert.That(physicalRoot.activeSelf, Is.False, "Collision cleanup must be immediate.");
            Assert.That(animator.enabled, Is.True, "Dispose restores the borrowed Animator state.");
            rig.Dispose(); // Idempotent before deferred destruction.
            yield return null;
            Assert.That(physicalRoot == null, Is.True);
            Assert.That(rig == null, Is.True);
            animator.enabled = false;
            rig = MosquitoRagdollBuilder.Build(actor.transform, animator, new MosquitoRagdollSettings(Vector3.down * 12, 0));
            physicalRoot = rig.PhysicalRoot;
            rig.BeginLocalSimulation(rig.CaptureAnimatedPose(Vector3.zero, Vector3.zero, actor.transform.position));
            var ownedColliders = physicalRoot.GetComponentsInChildren<Collider>(true);
            Assert.That(ownedColliders[0].enabled, Is.True);
            actor.SetActive(false);
            Assert.That(physicalRoot.activeSelf, Is.False, "Owner deactivation must not leave active auxiliary bodies.");
            foreach (var collider in ownedColliders) Assert.That(collider.enabled, Is.False);
            yield return null;
            Assert.That(physicalRoot == null, Is.True);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (rig) rig.Dispose();
            if (actor) Object.Destroy(actor);
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            yield return null;
        }

        private static Transform Bone(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            throw new InvalidOperationException("Missing real R4 bone " + name);
        }
        private static float NativeGap(ConfigurableJoint joint, Rigidbody body) => Vector3.Distance(
            body.position + body.rotation * joint.anchor,
            joint.connectedBody.position + joint.connectedBody.rotation * joint.connectedAnchor);

        private void AssertInternalExclusions()
        {
            for (int i = 0; i < rig.ColliderCount; i++)
            for (int j = i + 1; j < rig.ColliderCount; j++)
            {
                var a = rig.GetCollider(i); var b = rig.GetCollider(j);
                if (a.attachedRigidbody == b.attachedRigidbody) continue;
                Assert.That(UnityEngine.Physics.GetIgnoreCollision(a, b), Is.True, $"Self-collision filter lost: {a.transform.parent.name} / {b.transform.parent.name}");
            }
        }

        private void LogMotion(int step, float dt)
        {
            float linear = 0, angular = 0, energy = 0;
            int linearBody = -1, angularBody = -1, sleeping = 0;
            for (int i = 0; i < rig.BodyCount; i++)
            {
                var body = rig.GetBody((MosquitoBodyId)i);
                float v = body.linearVelocity.magnitude, w = body.angularVelocity.magnitude;
                if (v > linear) { linear = v; linearBody = i; }
                if (w > angular) { angular = w; angularBody = i; }
                if (body.IsSleeping()) sleeping++;
                Vector3 inertiaAngular = Quaternion.Inverse(body.rotation * body.inertiaTensorRotation) * body.angularVelocity;
                energy += .5f * (body.mass * v * v + Vector3.Dot(Vector3.Scale(body.inertiaTensor, inertiaAngular), inertiaAngular));
            }
            TestContext.WriteLine($"R4 step={step} t={(step + 1) * dt:F3}s: contact={rig.HasEnvironmentContact}; asleep={sleeping}/18; fastestLinearBody={linearBody} v={linear:F5}m/s; fastestAngularBody={angularBody} w={angular:F5}rad/s; energy={energy:E5}J");
        }
        private static void PutSkinInBind(Transform root)
        {
            var matrices = new Dictionary<Transform, Matrix4x4>();
            foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var bind = renderer.sharedMesh.bindposes;
                var bones = renderer.bones;
                for (int i = 0; i < bones.Length; i++) if (bones[i]) matrices[bones[i]] = renderer.localToWorldMatrix * bind[i].inverse;
            }
            // Hierarchy traversal is parent first; no animation/native asset is modified.
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (matrices.TryGetValue(t, out var world)) t.SetPositionAndRotation(world.GetColumn(3), world.rotation);
        }
    }
}
#endif
