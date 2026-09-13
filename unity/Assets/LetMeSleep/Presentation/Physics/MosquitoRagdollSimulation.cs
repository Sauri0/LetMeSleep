using System;
using UnityEngine;

namespace LetMeSleep.Presentation
{
    /// <summary>Opt-in local physics or remote pose playback. The caller owns authority, motor suspension,
    /// query isolation, recovery clearance/blending and any other transform writers.</summary>
    [DefaultExecutionOrder(1150)]
    [DisallowMultipleComponent]
    public sealed class MosquitoRagdollSimulation : MonoBehaviour, IDisposable
    {
        public MosquitoRagdollMode Mode { get; private set; } = MosquitoRagdollMode.Prepared;
        public GameObject PhysicalRoot { get; private set; }
        public int BodyCount => bodies == null ? 0 : bodies.Length;
        public int JointCount => joints == null ? 0 : joints.Length;
        public int ColliderCount => colliders == null ? 0 : colliders.Length;
        public bool HasEnvironmentContact
        {
            get
            {
                if (Mode != MosquitoRagdollMode.LocalSimulation) return false;
                foreach (var part in parts) if (part && part.HasEnvironmentContact) return true;
                return false;
            }
        }
        public bool IsSleeping
        {
            get
            {
                if (Mode != MosquitoRagdollMode.LocalSimulation) return false;
                foreach (var body in bodies) if (!body || !body.IsSleeping()) return false;
                return true;
            }
        }

        private Animator animator;
        private MosquitoRagdollSettings settings;
        private Transform[] bones, auxiliary;
        private Rigidbody[] bodies;
        private MosquitoRagdollPart[] parts;
        private Collider[] colliders;
        private ConfigurableJoint[] joints;
        private MosquitoRagdollBuilder.Attachment[] attachments;
        private MosquitoLocalPose[] restoreBodies, restoreAuxiliary;
        private Vector3[] bindPositions;
        private Quaternion[] bindRotations;
        private bool restoreAnimatorEnabled, borrowed, initialized, wasSleeping;
        private float settledFor;
        private MosquitoRagdollPose heldPose;

        internal void Initialize(GameObject physicalRoot, Animator animator, MosquitoRagdollSettings settings,
            Transform[] bones, Transform[] auxiliary, Rigidbody[] bodies, MosquitoRagdollPart[] parts,
            Collider[] colliders, ConfigurableJoint[] joints, MosquitoRagdollBuilder.Attachment[] attachments)
        {
            PhysicalRoot = physicalRoot; this.animator = animator; this.settings = settings;
            this.bones = bones; this.auxiliary = auxiliary; this.bodies = bodies; this.parts = parts;
            this.colliders = colliders; this.joints = joints; this.attachments = attachments;
            bindPositions = new Vector3[bodies.Length];
            bindRotations = new Quaternion[bodies.Length];
            for (int i = 0; i < bodies.Length; i++)
            {
                bindPositions[i] = bodies[i].transform.position;
                bindRotations[i] = bodies[i].transform.rotation;
            }
            initialized = true;
        }

        /// <summary>Read-only inspection. Callers must not mutate the owned physics objects.</summary>
        public Rigidbody GetBody(MosquitoBodyId body) { RequireReady(); return bodies[RagdollValue.Index(body)]; }
        public ConfigurableJoint GetJoint(int index) { RequireReady(); return joints[index]; }
        public Collider GetCollider(int index) { RequireReady(); return colliders[index]; }

        /// <summary>Capture after animation and before releasing it. Adds rigid actor motion at each origin.
        /// For articulated clip velocity, use the two-sample overload instead.</summary>
        public MosquitoRagdollPose CaptureAnimatedPose(Vector3 velocity, Vector3 angularVelocity, Vector3 velocityReferencePoint)
        {
            RequirePrepared();
            RagdollValue.RequireFinite(velocity, nameof(velocity));
            RagdollValue.RequireFinite(angularVelocity, nameof(angularVelocity));
            RagdollValue.RequireFinite(velocityReferencePoint, nameof(velocityReferencePoint));
            var poses = new MosquitoBodyPose[bodies.Length];
            for (int i = 0; i < poses.Length; i++)
            {
                ValidateAnimatedScale(bones[i]);
                poses[i] = new MosquitoBodyPose(bones[i].position, bones[i].rotation,
                    velocity + Vector3.Cross(angularVelocity, bones[i].position - velocityReferencePoint), angularVelocity);
            }
            return new MosquitoRagdollPose(poses, ReadAuxiliary());
        }

        /// <summary>Finite-difference two evaluated world poses. Actor motion is already included.
        /// The caller supplies the actual elapsed sample time, not an unrelated physics timestep.</summary>
        public MosquitoRagdollPose CaptureAnimatedPose(MosquitoRagdollPose previous, float sampleSeconds)
        {
            RequirePrepared();
            if (previous == null) throw new ArgumentNullException(nameof(previous));
            if (!RagdollValue.Finite(sampleSeconds) || sampleSeconds < .0001f || sampleSeconds > .25f)
                throw new ArgumentOutOfRangeException(nameof(sampleSeconds), "Sample interval must be between .0001 and .25 seconds.");
            var poses = new MosquitoBodyPose[bodies.Length];
            for (int i = 0; i < poses.Length; i++)
            {
                ValidateAnimatedScale(bones[i]);
                var prior = previous.GetBody((MosquitoBodyId)i);
                Quaternion delta = RagdollValue.Normalized(bones[i].rotation * Quaternion.Inverse(prior.Rotation));
                if (delta.w < 0) delta = new Quaternion(-delta.x, -delta.y, -delta.z, -delta.w);
                delta.ToAngleAxis(out float degrees, out Vector3 axis);
                Vector3 angular = degrees < .00001f ? Vector3.zero : axis * (degrees * Mathf.Deg2Rad / sampleSeconds);
                poses[i] = new MosquitoBodyPose(bones[i].position, bones[i].rotation,
                    (bones[i].position - prior.Position) / sampleSeconds, angular);
            }
            return new MosquitoRagdollPose(poses, ReadAuxiliary());
        }

        /// <summary>Start explicitly on the instance chosen by the caller to simulate. Does not choose a host.</summary>
        public void BeginLocalSimulation(MosquitoRagdollPose pose)
        {
            RequirePrepared();
            if (pose == null) throw new ArgumentNullException(nameof(pose));
            BorrowAnimation();
            try
            {
                PlaceKinematic(pose);
                foreach (var attachment in attachments) attachment.Refresh();
                foreach (Collider collider in colliders) collider.enabled = true;
                UnityEngine.Physics.SyncTransforms();

                // Rebuild the native reference frames AFTER compound mass properties and kinematic
                // flags are finalized. All 18 bodies temporarily use the cached bind pose; neither
                // the skin nor a physics step sees this intermediate setup. This also handles re-entry.
                for (int i = 0; i < bodies.Length; i++)
                {
                    bodies[i].position = bindPositions[i];
                    bodies[i].rotation = bindRotations[i];
                    bodies[i].PublishTransform();
                }
                // Native shapes must participate in the dynamic actor before reading its inertia.
                // No simulation step or user callback occurs during this synchronous setup.
                foreach (var body in bodies) { body.isKinematic = false; body.detectCollisions = true; }
                UnityEngine.Physics.SyncTransforms();
                foreach (var body in bodies) MosquitoRagdollBuilder.FinalizeMassProperties(body, settings);
                foreach (var joint in joints)
                {
                    var connected = joint.connectedBody;
                    var anchor = joint.anchor;
                    var connectedAnchor = joint.connectedAnchor;
                    joint.connectedBody = null;
                    joint.connectedBody = connected;
                    joint.anchor = anchor;
                    joint.connectedAnchor = connectedAnchor;
                }
                PlaceBodies(pose);
                // Ignore only collisions between our own new colliders. Existing actor/world colliders are untouched.
                // Set exclusions after switching ALL bodies to dynamic, before the first physics step.
                for (int i = 0; i < colliders.Length; i++)
                for (int j = i + 1; j < colliders.Length; j++)
                    if (colliders[i].attachedRigidbody != colliders[j].attachedRigidbody)
                        UnityEngine.Physics.IgnoreCollision(colliders[i], colliders[j], true);
                for (int i = 0; i < bodies.Length; i++)
                {
                    var body = bodies[i];
                    var sample = pose.GetBody((MosquitoBodyId)i);
                    body.angularVelocity = sample.AngularVelocity;
                    // Unity stores velocity at COM, while our transferable contract stores bone-origin velocity.
                    body.linearVelocity = sample.OriginVelocity + Vector3.Cross(sample.AngularVelocity, body.worldCenterOfMass - sample.Position);
                    body.detectCollisions = true;
                    body.WakeUp();
                }
                heldPose = pose;
                settledFor = 0;
                Mode = MosquitoRagdollMode.LocalSimulation;
            }
            catch
            {
                DeactivatePhysics();
                ReturnAnimation();
                throw;
            }
        }

        /// <summary>Impulse in N·s, applied ONCE to the selected body at a world hit point.</summary>
        public void ApplyImpulse(MosquitoBodyId body, Vector3 impulseNewtonSeconds, Vector3 worldPoint)
        {
            RequireReady();
            if (Mode != MosquitoRagdollMode.LocalSimulation) throw new InvalidOperationException("Only local simulation accepts forces.");
            int index = RagdollValue.Index(body);
            RagdollValue.RequireFinite(impulseNewtonSeconds, nameof(impulseNewtonSeconds));
            RagdollValue.RequireFinite(worldPoint, nameof(worldPoint));
            if (impulseNewtonSeconds.sqrMagnitude == 0) return;
            foreach (var rb in bodies) rb.WakeUp();
            settledFor = 0;
            bodies[index].AddForceAtPosition(impulseNewtonSeconds, worldPoint, ForceMode.Impulse);
        }

        /// <summary>Remote instances have kinematic proxies, disabled colliders and no force integration.</summary>
        public void BeginRemotePose(MosquitoRagdollPose pose)
        {
            RequirePrepared();
            if (pose == null) throw new ArgumentNullException(nameof(pose));
            BorrowAnimation();
            PlaceKinematic(pose);
            heldPose = pose;
            Mode = MosquitoRagdollMode.RemotePose;
        }

        /// <summary>The transport/interpolator supplies an already evaluated world pose.</summary>
        public void ApplyRemotePose(MosquitoRagdollPose pose)
        {
            RequireReady();
            if (Mode != MosquitoRagdollMode.RemotePose) throw new InvalidOperationException("BeginRemotePose first.");
            if (pose == null) throw new ArgumentNullException(nameof(pose));
            PlaceKinematic(pose);
            heldPose = pose;
        }

        public MosquitoRagdollPose CapturePose()
        {
            RequireReady();
            if (Mode == MosquitoRagdollMode.Prepared) throw new InvalidOperationException("Use CaptureAnimatedPose while animation owns the rig.");
            if (Mode != MosquitoRagdollMode.LocalSimulation) return heldPose;
            var poses = new MosquitoBodyPose[bodies.Length];
            for (int i = 0; i < poses.Length; i++)
            {
                var body = bodies[i];
                Vector3 angular = body.angularVelocity;
                Vector3 originVelocity = body.linearVelocity - Vector3.Cross(angular, body.worldCenterOfMass - body.position);
                poses[i] = new MosquitoBodyPose(body.position, body.rotation, originVelocity, angular);
            }
            var locals = new MosquitoLocalPose[auxiliary.Length];
            for (int i = 0; i < locals.Length; i++) locals[i] = heldPose.GetAuxiliary(i);
            return new MosquitoRagdollPose(poses, locals);
        }

        /// <summary>Capture the current physical pose and stop simulation explicitly, without restoring animation.</summary>
        public MosquitoRagdollPose HoldPose()
        {
            var pose = CapturePose();
            PlaceKinematic(pose);
            heldPose = pose;
            Mode = MosquitoRagdollMode.Held;
            return pose;
        }

        /// <summary>Explicitly returns the pre-release local pose and the borrowed Animator's enabled state.
        /// This is cleanup/restoration, not a recovery blend or gameplay state transition.</summary>
        public void RestoreAnimation()
        {
            RequireReady();
            DeactivatePhysics();
            ReturnAnimation();
            heldPose = null;
            Mode = MosquitoRagdollMode.Prepared;
        }

        /// <summary>Disable owned collision immediately; destroy proxies and this component at end of frame.
        /// Disabling/destroying the visual owner also disposes the module. Build again after re-enabling.</summary>
        public void Dispose()
        {
            Cleanup();
            if (this) Destroy(this);
        }

        private void FixedUpdate()
        {
            if (Mode != MosquitoRagdollMode.LocalSimulation || !initialized) return;
            if (!OwnedObjectsExist()) { Dispose(); return; }
            // An environmental collision can wake a sleeping island without ApplyImpulse.
            // Require a fresh settling interval, even when that first velocity is still small.
            if (wasSleeping && !IsSleeping) settledFor = 0;
            bool slow = true;
            foreach (var body in bodies)
            {
                if (body.linearVelocity.sqrMagnitude > settings.SettleLinearSpeed * settings.SettleLinearSpeed ||
                    body.angularVelocity.sqrMagnitude > settings.SettleAngularSpeed * settings.SettleAngularSpeed) slow = false;
            }
            settledFor = slow && HasEnvironmentContact ? settledFor + Time.fixedDeltaTime : 0f;
            if (settledFor >= settings.SettleSeconds)
                foreach (var body in bodies) body.Sleep();
            // A removed/disabled support cannot leave the whole rig sleeping in the air.
            if (IsSleeping && !HasEnvironmentContact)
                foreach (var body in bodies) body.WakeUp();
            wasSleeping = IsSleeping;
            foreach (var body in bodies)
                if (!body.IsSleeping()) body.AddForce(settings.Gravity, ForceMode.Acceleration);
        }

        private void LateUpdate()
        {
            if (!initialized || Mode == MosquitoRagdollMode.Prepared || Mode == MosquitoRagdollMode.Disposed) return;
            if (!OwnedObjectsExist()) { Dispose(); return; }
            if (Mode != MosquitoRagdollMode.LocalSimulation) { WriteSkin(heldPose); return; }
            for (int i = 0; i < auxiliary.Length; i++) heldPose.GetAuxiliary(i).Write(auxiliary[i]);
            for (int i = 0; i < bones.Length; i++) bones[i].SetPositionAndRotation(bodies[i].position, bodies[i].rotation);
        }

        private void PlaceKinematic(MosquitoRagdollPose pose)
        {
            DeactivatePhysics();
            PlaceBodies(pose);
            WriteSkin(pose);
        }

        private void PlaceBodies(MosquitoRagdollPose pose)
        {
            for (int i = 0; i < bodies.Length; i++)
            {
                var p = pose.GetBody((MosquitoBodyId)i);
                bodies[i].position = p.Position;
                bodies[i].rotation = p.Rotation;
                // Publish from physics instead of issuing a second Transform teleport back to physics.
                bodies[i].PublishTransform();
            }
        }

        private void WriteSkin(MosquitoRagdollPose pose)
        {
            for (int i = 0; i < auxiliary.Length; i++) pose.GetAuxiliary(i).Write(auxiliary[i]);
            // Parent before child. Facial bones are deliberately absent from both arrays.
            for (int i = 0; i < bones.Length; i++)
            {
                var p = pose.GetBody((MosquitoBodyId)i);
                bones[i].SetPositionAndRotation(p.Position, p.Rotation);
            }
        }

        private void BorrowAnimation()
        {
            restoreBodies = new MosquitoLocalPose[bones.Length];
            for (int i = 0; i < bones.Length; i++) restoreBodies[i] = MosquitoLocalPose.Read(bones[i]);
            restoreAuxiliary = ReadAuxiliary();
            restoreAnimatorEnabled = animator && animator.enabled;
            if (animator) animator.enabled = false;
            borrowed = true;
        }

        private void ReturnAnimation()
        {
            if (!borrowed) return;
            for (int i = 0; i < auxiliary.Length; i++) if (auxiliary[i]) restoreAuxiliary[i].Write(auxiliary[i]);
            for (int i = 0; i < bones.Length; i++) if (bones[i]) restoreBodies[i].Write(bones[i]);
            if (animator) animator.enabled = restoreAnimatorEnabled;
            borrowed = false;
        }

        private MosquitoLocalPose[] ReadAuxiliary()
        {
            var poses = new MosquitoLocalPose[auxiliary.Length];
            for (int i = 0; i < poses.Length; i++) poses[i] = MosquitoLocalPose.Read(auxiliary[i]);
            return poses;
        }

        private void DeactivatePhysics()
        {
            settledFor = 0;
            wasSleeping = false;
            if (colliders != null) foreach (var collider in colliders) if (collider) collider.enabled = false;
            if (bodies != null) foreach (var body in bodies)
            {
                if (!body) continue;
                body.detectCollisions = false;
                if (!body.isKinematic) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
                body.isKinematic = true;
            }
            if (parts != null) foreach (var part in parts) if (part) part.ClearContacts();
        }

        private void OnDisable() { if (initialized) Dispose(); }
        private void OnDestroy() => Cleanup();
        private void Cleanup()
        {
            if (Mode == MosquitoRagdollMode.Disposed) return;
            DeactivatePhysics();
            ReturnAnimation();
            Mode = MosquitoRagdollMode.Disposed;
            if (PhysicalRoot) { PhysicalRoot.SetActive(false); Destroy(PhysicalRoot); }
            heldPose = null;
        }
        private bool OwnedObjectsExist()
        {
            if (!PhysicalRoot) return false;
            foreach (var bone in bones) if (!bone) return false;
            foreach (var bone in auxiliary) if (!bone) return false;
            foreach (var body in bodies) if (!body) return false;
            foreach (var part in parts) if (!part) return false;
            foreach (var joint in joints) if (!joint) return false;
            foreach (var collider in colliders) if (!collider) return false;
            return true;
        }
        private void RequireReady()
        {
            if (!initialized || Mode == MosquitoRagdollMode.Disposed || !isActiveAndEnabled || !OwnedObjectsExist())
                throw new InvalidOperationException("Ragdoll module is unavailable or disposed.");
        }
        private void RequirePrepared()
        {
            RequireReady();
            if (Mode != MosquitoRagdollMode.Prepared) throw new InvalidOperationException("RestoreAnimation before changing simulation ownership.");
        }
        private static void ValidateAnimatedScale(Transform bone)
        {
            if ((bone.lossyScale - Vector3.one * .5f).sqrMagnitude > .00001f)
                throw new InvalidOperationException("Physical R4 poses cannot contain animated body scale: " + bone.name);
        }
    }
}
