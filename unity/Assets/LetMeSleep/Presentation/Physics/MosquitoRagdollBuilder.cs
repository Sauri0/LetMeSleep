using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LetMeSleep.Presentation
{
    /// <summary>Explicit factory for the R4 mosquito at its authored .5 world scale.
    /// Does not touch existing colliders, gameplay writers, layers, or global physics settings.</summary>
    public static class MosquitoRagdollBuilder
    {
        internal static readonly string[] BodyNames =
        {
            "Thorax", "Head", "Abdomen01", "Abdomen02", "Wing.L", "Wing.R",
            "Leg101.L", "Leg102.L", "Leg201.L", "Leg202.L", "Leg301.L", "Leg302.L",
            "Leg101.R", "Leg102.R", "Leg201.R", "Leg202.R", "Leg301.R", "Leg302.R"
        };
        internal static readonly string[] AuxiliaryNames =
        {
            "Root", "Proboscis", "Leg103.L", "Leg203.L", "Leg303.L", "Leg103.R", "Leg203.R", "Leg303.R",
            "Socket.Mouth", "Socket.Back", "Socket.CameraTarget", "Socket.AimForward",
            "Socket.GroundContact", "Socket.WingRoot.L", "Socket.WingRoot.R"
        };
        private static readonly int[] Parents = { -1, 0, 0, 2, 0, 0, 0, 6, 0, 8, 0, 10, 0, 12, 0, 14, 0, 16 };
        private static readonly float[] Masses = { .012f, .008f, .006f, .004f, .003f, .003f,
            .0025f, .0015f, .0025f, .0015f, .0025f, .0015f, .0025f, .0015f, .0025f, .0015f, .0025f, .0015f };

        internal sealed class Attachment
        {
            internal Transform Bone;
            internal Vector3 TailLocal;
            internal CapsuleCollider Collider;
            internal float Radius;
            internal void Refresh()
            {
                FitCapsule(Collider, Bone.position, Bone.TransformPoint(TailLocal), Radius);
            }
        }

        private readonly struct SourceFrame
        {
            internal readonly Vector3 Origin, X, Y, Z;
            internal SourceFrame(Dictionary<string, Matrix4x4> bind)
            {
                Vector3 thorax = Position(bind["Thorax"]);
                Vector3 d1 = Position(bind["Head"]) - thorax;
                Vector3 d2 = Position(bind["Abdomen02"]) - thorax;
                const float y1 = -.056f, z1 = -.010f, y2 = .095f, z2 = -.048f;
                float determinant = y1 * z2 - y2 * z1;
                X = (Position(bind["Leg101.L"]) - Position(bind["Leg101.R"])) / .054f;
                Y = (d1 * z2 - d2 * z1) / determinant;
                Z = (d2 * y1 - d1 * y2) / determinant;
                Origin = thorax - Y * .015f - Z * .110f;
                if (Mathf.Abs(X.magnitude - .5f) > .002f || Mathf.Abs(Y.magnitude - .5f) > .002f ||
                    Mathf.Abs(Z.magnitude - .5f) > .002f || Mathf.Abs(Vector3.Dot(X.normalized, Y.normalized)) > .002f ||
                    Mathf.Abs(Vector3.Dot(X.normalized, Z.normalized)) > .002f || Mathf.Abs(Vector3.Dot(Y.normalized, Z.normalized)) > .002f)
                    throw new ArgumentException("Expected the unmodified R4 skeleton at uniform .5 world scale.");
            }
            internal Vector3 Point(Vector3 p) => Origin + X * p.x + Y * p.y + Z * p.z;
            internal Vector3 Point(float x, float y, float z) => Point(new Vector3(x, y, z));
        }

        public static MosquitoRagdollSimulation Build(Transform visualRoot, Animator animator, MosquitoRagdollSettings settings)
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Build is a runtime operation; use a PlayMode test.");
            if (!visualRoot || !visualRoot.gameObject.activeInHierarchy) throw new ArgumentException("An active visual root is required.", nameof(visualRoot));
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (visualRoot.GetComponent<MosquitoRagdollSimulation>()) throw new InvalidOperationException("The visual root already has a ragdoll module.");
            if (animator && animator.transform != visualRoot && !animator.transform.IsChildOf(visualRoot))
                throw new ArgumentException("Animator must belong to this visual root.", nameof(animator));

            var named = new Dictionary<string, Transform>(StringComparer.Ordinal);
            var required = new HashSet<string>(BodyNames, StringComparer.Ordinal);
            required.UnionWith(AuxiliaryNames);
            foreach (Transform t in visualRoot.GetComponentsInChildren<Transform>(true))
            {
                if (!required.Contains(t.name)) continue;
                if (named.ContainsKey(t.name)) throw new ArgumentException("Duplicate R4 bone: " + t.name);
                named.Add(t.name, t);
            }
            foreach (string name in required)
                if (!named.ContainsKey(name)) throw new ArgumentException("Missing R4 bone: " + name);

            var bind = ReadBindMatrices(visualRoot, required);
            foreach (string name in BodyNames)
                RequireBind(bind, name);
            RequireBind(bind, "Proboscis");
            for (int i = 2; i < 8; i++) RequireBind(bind, AuxiliaryNames[i]);
            var frame = new SourceFrame(bind);
            ValidateSkeleton(named, bind, frame);

            var physicalRoot = new GameObject("Mosquito R4 Physics (owned)");
            physicalRoot.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            physicalRoot.transform.localScale = Vector3.one;
            SceneManager.MoveGameObjectToScene(physicalRoot, visualRoot.gameObject.scene);
            MosquitoRagdollSimulation simulation = null;
            try
            {
                var bones = new Transform[BodyNames.Length];
                var auxiliary = new Transform[AuxiliaryNames.Length];
                var bodies = new Rigidbody[BodyNames.Length];
                var parts = new MosquitoRagdollPart[BodyNames.Length];
                var colliders = new List<Collider>();
                var joints = new List<ConfigurableJoint>();
                var attachments = new List<Attachment>();
                for (int i = 0; i < auxiliary.Length; i++) auxiliary[i] = named[AuxiliaryNames[i]];
                for (int i = 0; i < bodies.Length; i++)
                {
                    bones[i] = named[BodyNames[i]];
                    var go = new GameObject(BodyNames[i] + " [physics]") { layer = settings.CollisionLayer };
                    go.transform.SetParent(physicalRoot.transform, false);
                    go.transform.SetPositionAndRotation(Position(bind[BodyNames[i]]), bind[BodyNames[i]].rotation);
                    var rb = go.AddComponent<Rigidbody>();
                    rb.isKinematic = true;
                    rb.detectCollisions = false;
                    rb.useGravity = false;
                    rb.mass = Masses[i] * settings.MassScale;
                    rb.linearDamping = .02f;
                    rb.angularDamping = .15f;
                    rb.maxAngularVelocity = 80f;
                    rb.maxDepenetrationVelocity = 1f;
                    rb.sleepThreshold = 0f; // Sleep uses the explicit speed + contact gate in Simulation.
                    rb.solverIterations = settings.SolverIterations;
                    rb.solverVelocityIterations = settings.SolverVelocityIterations;
                    rb.interpolation = RigidbodyInterpolation.None;
                    rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                    bodies[i] = rb;
                    parts[i] = go.AddComponent<MosquitoRagdollPart>();
                    parts[i].Body = (MosquitoBodyId)i;
                }

                AddCapsule(bodies[0], frame.Point(0, -.008f, .110f), frame.Point(0, .016f, .110f), .019f, colliders);
                AddSphere(bodies[1], frame.Point(0, -.073f, .107f), .018f, colliders);
                AddSphere(bodies[1], frame.Point(.027f, -.094f, .113f), .016f, colliders);
                AddSphere(bodies[1], frame.Point(-.027f, -.094f, .113f), .016f, colliders);
                AddAttachment(bodies[1], named["Proboscis"], bind["Proboscis"], frame.Point(0, -.190f, 0), .0015f, colliders, attachments);
                Vector3 a = frame.Point(0, .039f, .105f), b = frame.Point(0, .110f, .062f), c = frame.Point(0, .204f, -.030f);
                AddCapsule(bodies[2], Vector3.Lerp(a, b, .25f), Vector3.Lerp(a, b, .75f), .014f, colliders);
                AddCapsule(bodies[3], Vector3.Lerp(b, c, .10f), Vector3.Lerp(b, c, .55f), .008f, colliders);
                AddCapsule(bodies[3], Vector3.Lerp(b, c, .55f), Vector3.Lerp(b, c, .95f), .004f, colliders);
                AddWing(bodies[4], frame, 1, colliders);
                AddWing(bodies[5], frame, -1, colliders);
                for (int side = 0; side < 2; side++)
                for (int leg = 0; leg < 3; leg++)
                {
                    int i = 6 + side * 6 + leg * 2;
                    LegPoints(leg, side == 0 ? 1 : -1, out var hip, out var knee, out var ankle, out var toe);
                    AddCapsule(bodies[i], frame.Point(hip), frame.Point(knee), .002f, colliders);
                    AddCapsule(bodies[i + 1], frame.Point(knee), frame.Point(ankle), .0018f, colliders);
                    string footName = AuxiliaryNames[2 + side * 3 + leg];
                    AddAttachment(bodies[i + 1], named[footName], bind[footName], frame.Point(toe), .0013f, colliders, attachments);
                }
                for (int i = 1; i < bodies.Length; i++)
                    joints.Add(AddJoint(i, bodies, frame));
                foreach (Collider collider in colliders)
                {
                    collider.contactOffset = settings.ContactOffset;
                    collider.enabled = false;
                }
                simulation = visualRoot.gameObject.AddComponent<MosquitoRagdollSimulation>();
                simulation.Initialize(physicalRoot, animator, settings, bones, auxiliary, bodies, parts,
                    colliders.ToArray(), joints.ToArray(), attachments.ToArray());
                foreach (var part in parts) part.Owner = simulation;
                return simulation;
            }
            catch
            {
                // Make rollback inert immediately; Destroy itself is deferred by Unity.
                physicalRoot.SetActive(false);
                UnityEngine.Object.Destroy(physicalRoot);
                if (simulation) UnityEngine.Object.Destroy(simulation);
                throw;
            }
        }

        private static Dictionary<string, Matrix4x4> ReadBindMatrices(Transform root, HashSet<string> required)
        {
            var result = new Dictionary<string, Matrix4x4>(StringComparer.Ordinal);
            foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!renderer.sharedMesh) continue;
                var matrices = renderer.sharedMesh.bindposes;
                var bones = renderer.bones;
                if (matrices.Length != bones.Length) throw new ArgumentException("Skin bone/bind matrix counts differ.");
                for (int i = 0; i < bones.Length; i++)
                {
                    if (!bones[i] || !required.Contains(bones[i].name)) continue;
                    if (renderer.transform.IsChildOf(bones[i]))
                        throw new ArgumentException("A renderer under an animated bone cannot provide a stable world bind frame.");
                    Matrix4x4 world = renderer.localToWorldMatrix * matrices[i].inverse;
                    if (!world.ValidTRS()) throw new ArgumentException("Invalid skin bind transform: " + bones[i].name);
                    if (result.TryGetValue(bones[i].name, out var existing) &&
                        (Vector3.Distance(Position(existing), Position(world)) > .0001f || Quaternion.Angle(existing.rotation, world.rotation) > .1f))
                        throw new ArgumentException("Inconsistent skin bind matrices: " + bones[i].name);
                    result[bones[i].name] = world;
                }
            }
            return result;
        }

        private static void RequireBind(Dictionary<string, Matrix4x4> bind, string name)
        {
            if (!bind.ContainsKey(name)) throw new ArgumentException("Skin has no R4 bind matrix for " + name);
        }

        private static void ValidateSkeleton(Dictionary<string, Transform> named, Dictionary<string, Matrix4x4> bind, SourceFrame frame)
        {
            for (int i = 0; i < BodyNames.Length; i++)
            {
                string parent = Parents[i] < 0 ? "Root" : BodyNames[Parents[i]];
                if (named[BodyNames[i]].parent != named[parent]) throw new ArgumentException("Unexpected R4 parent for " + BodyNames[i]);
                Vector3 scale = bind[BodyNames[i]].lossyScale;
                if ((scale - Vector3.one * .5f).sqrMagnitude > .00001f)
                    throw new ArgumentException("R4 body bind scale must be positive uniform .5: " + BodyNames[i]);
            }
            for (int side = 0; side < 2; side++)
            for (int leg = 0; leg < 3; leg++)
            {
                LegPoints(leg, side == 0 ? 1 : -1, out var hip, out var knee, out var ankle, out _);
                int i = 6 + side * 6 + leg * 2;
                string foot = AuxiliaryNames[2 + side * 3 + leg];
                if (named[foot].parent != named[BodyNames[i + 1]] ||
                    Vector3.Distance(Position(bind[BodyNames[i]]), frame.Point(hip)) > .0002f ||
                    Vector3.Distance(Position(bind[BodyNames[i + 1]]), frame.Point(knee)) > .0002f ||
                    Vector3.Distance(Position(bind[foot]), frame.Point(ankle)) > .0002f)
                    throw new ArgumentException("R4 leg bind geometry differs: " + foot);
            }
            if (named["Proboscis"].parent != named["Head"]) throw new ArgumentException("Proboscis must follow Head.");
        }

        private static ConfigurableJoint AddJoint(int i, Rigidbody[] bodies, SourceFrame frame)
        {
            Vector3 x = frame.X.normalized, y = frame.Y.normalized;
            float low, high, lateral, twist;
            if (i == 1) { low = -35; high = 45; lateral = 35; twist = 20; }
            else if (i == 2 || i == 3) { low = i == 2 ? -45 : -50; high = i == 2 ? 55 : 65; lateral = 30; twist = 20; }
            else if (i == 4 || i == 5)
            {
                int sign = i == 4 ? 1 : -1;
                Vector3 span = frame.Point(sign * .220f, .065f, .181f) - bodies[i].position;
                x = Vector3.Cross(span, frame.Z).normalized;
                y = span.normalized;
                low = -65; high = 90; lateral = 55; twist = 0;
            }
            else
            {
                int side = (i - 6) / 6, leg = ((i - 6) % 6) / 2;
                LegPoints(leg, side == 0 ? 1 : -1, out var hip, out var knee, out var ankle, out _);
                Vector3 upper = frame.Point(knee) - frame.Point(hip), lower = frame.Point(ankle) - frame.Point(knee);
                x = Vector3.Cross(upper, lower).normalized; // Positive flex folds both mirrored knees.
                y = upper.normalized;
                bool kneeJoint = (i - 6) % 2 == 1;
                low = kneeJoint ? -25 : -60; high = kneeJoint ? 110 : 75;
                lateral = kneeJoint ? 0 : 45; twist = kneeJoint ? 0 : 25;
            }
            var joint = bodies[i].gameObject.AddComponent<ConfigurableJoint>();
            joint.autoConfigureConnectedAnchor = false;
            joint.connectedBody = bodies[Parents[i]];
            joint.anchor = Vector3.zero;
            joint.connectedAnchor = bodies[Parents[i]].transform.InverseTransformPoint(bodies[i].position);
            joint.axis = bodies[i].transform.InverseTransformDirection(x).normalized;
            joint.secondaryAxis = bodies[i].transform.InverseTransformDirection(Vector3.ProjectOnPlane(y, x).normalized);
            joint.xMotion = joint.yMotion = joint.zMotion = ConfigurableJointMotion.Locked;
            joint.angularXMotion = ConfigurableJointMotion.Limited;
            joint.angularYMotion = lateral == 0 ? ConfigurableJointMotion.Locked : ConfigurableJointMotion.Limited;
            joint.angularZMotion = twist == 0 ? ConfigurableJointMotion.Locked : ConfigurableJointMotion.Limited;
            joint.lowAngularXLimit = new SoftJointLimit { limit = low, contactDistance = 1 };
            joint.highAngularXLimit = new SoftJointLimit { limit = high, contactDistance = 1 };
            joint.angularYLimit = new SoftJointLimit { limit = lateral, contactDistance = 1 };
            joint.angularZLimit = new SoftJointLimit { limit = twist, contactDistance = 1 };
            joint.xDrive = joint.yDrive = joint.zDrive = new JointDrive();
            joint.angularXDrive = joint.angularYZDrive = joint.slerpDrive = new JointDrive();
            joint.projectionMode = JointProjectionMode.None;
            joint.enableCollision = false;
            joint.enablePreprocessing = true;
            return joint;
        }

        private static GameObject ColliderObject(Rigidbody body, string name)
        {
            var go = new GameObject(name) { layer = body.gameObject.layer };
            go.transform.SetParent(body.transform, false);
            return go;
        }
        private static CapsuleCollider AddCapsule(Rigidbody body, Vector3 a, Vector3 b, float radius, List<Collider> colliders)
        {
            var collider = ColliderObject(body, "Capsule").AddComponent<CapsuleCollider>();
            collider.enabled = false;
            FitCapsule(collider, a, b, radius);
            colliders.Add(collider);
            return collider;
        }
        private static void FitCapsule(CapsuleCollider collider, Vector3 a, Vector3 b, float radius)
        {
            Vector3 delta = b - a;
            collider.transform.SetPositionAndRotation((a + b) * .5f, Quaternion.FromToRotation(Vector3.up, delta.normalized));
            collider.direction = 1;
            collider.center = Vector3.zero;
            collider.radius = radius;
            collider.height = delta.magnitude + 2 * radius;
        }
        private static void AddSphere(Rigidbody body, Vector3 center, float radius, List<Collider> colliders)
        {
            var collider = ColliderObject(body, "Sphere").AddComponent<SphereCollider>();
            collider.enabled = false;
            collider.transform.position = center;
            collider.radius = radius;
            colliders.Add(collider);
        }
        private static void AddAttachment(Rigidbody body, Transform bone, Matrix4x4 bind, Vector3 tail, float radius,
            List<Collider> colliders, List<Attachment> attachments)
        {
            attachments.Add(new Attachment { Bone = bone, TailLocal = bind.inverse.MultiplyPoint3x4(tail), Radius = radius,
                Collider = AddCapsule(body, Position(bind), tail, radius, colliders) });
        }
        private static void AddWing(Rigidbody body, SourceFrame frame, int sign, List<Collider> colliders)
        {
            var outline = new[] {
                new Vector3(.017f,0,.082f), new Vector3(.04882f,.01530f,.11743f), new Vector3(.11394f,.04675f,.19067f),
                new Vector3(.224f,.085f,.239f), new Vector3(.18346f,.06120f,.17193f), new Vector3(.10955f,.03230f,.12333f),
                new Vector3(.04667f,.01020f,.09446f), new Vector3(.05914f,.01415f,.11396f),
                new Vector3(.12174f,.03775f,.16144f), new Vector3(.18334f,.06515f,.20816f) };
            for (int i = 0; i < outline.Length; i++) outline[i] = frame.Point(new Vector3(outline[i].x * sign, outline[i].y, outline[i].z));
            Vector3 span = (outline[3] - outline[0]).normalized;
            Vector3 normal = Vector3.Cross(span, outline[2] - outline[0]).normalized;
            Quaternion rotation = Quaternion.LookRotation(normal, Vector3.Cross(normal, span));
            Quaternion inverse = Quaternion.Inverse(rotation);
            Vector3 min = Vector3.one * float.PositiveInfinity, max = Vector3.one * float.NegativeInfinity;
            foreach (Vector3 point in outline)
            {
                Vector3 local = inverse * (point - body.position);
                min = Vector3.Min(min, local); max = Vector3.Max(max, local);
            }
            var collider = ColliderObject(body, "Wing membrane box").AddComponent<BoxCollider>();
            collider.enabled = false;
            collider.transform.SetPositionAndRotation(body.position + rotation * ((min + max) * .5f), rotation);
            Vector3 size = max - min;
            collider.size = new Vector3(Mathf.Max(size.x, .002f), Mathf.Max(size.y, .002f), Mathf.Max(size.z, .002f));
            colliders.Add(collider);
        }
        private static Vector3 Position(Matrix4x4 matrix) => matrix.GetColumn(3);
        private static void LegPoints(int leg, int sign, out Vector3 hip, out Vector3 knee, out Vector3 ankle, out Vector3 toe)
        {
            float y = leg == 0 ? -.033f : leg == 1 ? .006f : .042f;
            float dy = leg == 0 ? -.052f : leg == 1 ? .012f : .067f;
            float kx = leg == 0 ? .097f : leg == 1 ? .112f : .089f;
            float kz = leg == 0 ? .006f : leg == 1 ? -.003f : .002f;
            float ax = leg == 0 ? .107f : leg == 1 ? .094f : .081f;
            hip = new Vector3(sign * .027f, y, .099f - leg * .004f);
            knee = new Vector3(sign * kx, y + dy * .4f, kz);
            ankle = new Vector3(sign * ax, y + dy, -.1076f);
            toe = new Vector3(sign * (ax + .012f), y + dy + .006f, -.1126f);
        }
    }
}
