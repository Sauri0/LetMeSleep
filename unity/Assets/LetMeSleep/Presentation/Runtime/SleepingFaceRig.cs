using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LetMeSleep.Presentation
{
    /// <summary>
    /// v0.3.0 scenes r2 (art director #2): the sleeping face of the decorative menu human. The closed caricature lids are
    /// two big skin domes (lid shells 1.09 x the huge eyeball); asleep they read as swollen spheres. On a private copy of the
    /// head mesh (the shared asset is never touched) this rig
    /// - flattens every blink target and the eyeball globes toward the face along each eye's forward axis
    ///   (<see cref="Settings.LidProtrusion"/>, 1 = as authored), with normals transformed to match;
    /// - lays a thin dark closed-eye line (#2A1A1A, <see cref="Settings.LineWidth"/>) on the lower edge of each closed lid,
    ///   found from the mesh itself (the seam where the closed upper and lower lids meet), and a small smile over the mouth;
    /// - turns the head a few degrees toward a target (the menu camera) after animation, so the face reads as in UI-06.
    /// Owned by <see cref="MainMenuLivingScene"/>: <see cref="Apply"/> runs after its graph evaluation every frame and
    /// <see cref="Dispose"/> restores the shared mesh and destroys everything it created. Visual only: no collider.
    /// </summary>
    public sealed class SleepingFaceRig : IDisposable
    {
        [Serializable]
        public sealed class Settings
        {
            [Range(.1f, 1f)] public float LidProtrusion = .35f;
            public Color LineColor = new Color(42 / 255f, 26 / 255f, 26 / 255f, 1f);
            [Tooltip("Closed-eye line width in metres (3-4 px of a 1080p menu frame at ~2.4 m).")]
            public float LineWidth = .0062f;
            [Tooltip("Smile width in metres; 0 = no smile.")]
            public float SmileWidth = .05f;
            public float SmileLineWidth = .0055f;
            [Range(0f, 40f)] public float HeadTurnDegrees = 15f;
            [Tooltip("Angle between the eye's forward axis and the closed-eye curve on the lid shell (90 = the outline).")]
            [Range(30f, 85f)] public float LineAngleFromFront = 50f;
            [Tooltip("Angular span of the closed-eye curve around the bottom of the lid, degrees.")]
            [Range(40f, 180f)] public float LineSpanDegrees = 110f;
        }

        public const string LineName = "_LMS_SleepEyeLine";
        public const string SmileName = "_LMS_SleepSmile";

        private readonly Settings settings;
        private readonly SkinnedMeshRenderer head;
        private readonly Transform headBone, turnTarget;
        private Vector3 headForwardLocal;
        private Mesh original, flattened;
        private Material ink;
        private readonly List<GameObject> created = new List<GameObject>();

        public bool IsBuilt => flattened;
        public int LineCount { get; private set; }
        public Mesh FlattenedMesh => flattened;
        public Vector3 LastTurnAxis { get; private set; }
        public float LastTurnDegrees { get; private set; }
        public string Report { get; private set; } = "";
        public Vector3 FaceForwardWorld => headBone ? headBone.TransformDirection(headForwardLocal).normalized : Vector3.forward;

        private SleepingFaceRig(Settings settings, SkinnedMeshRenderer head, Transform headBone, Vector3 headForwardLocal, Transform turnTarget)
        {
            this.settings = settings; this.head = head; this.headBone = headBone; this.headForwardLocal = headForwardLocal; this.turnTarget = turnTarget;
        }

        /// <summary>Builds the rig, or returns null (logging why) when the head is not the expected readable caricature head.</summary>
        public static SleepingFaceRig TryCreate(Settings settings, SkinnedMeshRenderer head, Transform headBone, Transform leftEye, Transform rightEye,
            Vector3 eyeForwardLocal, Vector3 headForwardLocal, Vector3 headUpLocal, string[] leftBlink, string[] rightBlink, Transform turnTarget)
        {
            if (settings == null || !head || !head.sharedMesh || !headBone || !leftEye || !rightEye || leftBlink == null || rightBlink == null)
                return null;
            var rig = new SleepingFaceRig(settings, head, headBone, headForwardLocal, turnTarget);
            try
            {
                if (rig.Build(leftEye, rightEye, eyeForwardLocal, headUpLocal, leftBlink, rightBlink)) return rig;
            }
            catch (Exception exception) { Debug.LogException(exception); }
            rig.Dispose();
            Debug.LogWarning("LMS_SLEEP_FACE_PENDING: the menu head mesh could not be flattened (not readable or unexpected lids).");
            return null;
        }

        private bool Build(Transform leftEye, Transform rightEye, Vector3 eyeForwardLocal, Vector3 headUpLocal, string[] leftBlink, string[] rightBlink)
        {
            original = head.sharedMesh;
            if (!original.isReadable) return false;
            var bones = head.bones;
            var bindposes = original.bindposes;
            int left = Array.IndexOf(bones, leftEye), right = Array.IndexOf(bones, rightEye), headIndex = Array.IndexOf(bones, headBone);
            if (left < 0 || right < 0 || headIndex < 0 || bindposes.Length != bones.Length) return false;
            float s = Mathf.Clamp(settings.LidProtrusion, .1f, 1f);
            var eyes = new[] { Eye(bindposes[left], eyeForwardLocal), Eye(bindposes[right], eyeForwardLocal) };
            Vector3 up = bindposes[headIndex].inverse.MultiplyVector(headUpLocal).normalized;

            var vertices = original.vertices;
            var sourceNormals = original.normals;
            bool hasNormals = sourceNormals.Length == vertices.Length;
            var normals = hasNormals ? (Vector3[])sourceNormals.Clone() : sourceNormals;
            var basis = (Vector3[])vertices.Clone();
            var materials = head.sharedMaterials;
            // Mesh-space scale from the eyeballs themselves (the authored globe radius is 0.066 m).
            var eyeWhite = new bool[vertices.Length];
            var expression = new bool[vertices.Length];
            for (int sub = 0; sub < original.subMeshCount && sub < materials.Length; sub++)
            {
                string material = materials[sub] ? materials[sub].name : "";
                bool white = material.StartsWith("Character_EyeWhite", StringComparison.Ordinal) || material.StartsWith("Human_EyeWhite", StringComparison.Ordinal);
                bool ink = material.StartsWith("Character_Expression", StringComparison.Ordinal);
                if (!white && !ink) continue;
                foreach (int index in original.GetTriangles(sub)) { if (white) eyeWhite[index] = true; else expression[index] = true; }
            }
            var radii = new List<float>();
            for (int i = 0; i < vertices.Length; i++)
                if (eyeWhite[i])
                {
                    int e = Nearest(eyes, vertices[i]);
                    if (Vector3.Dot(vertices[i] - eyes[e].center, eyes[e].forward) > 0f) radii.Add((vertices[i] - eyes[e].center).magnitude);
                }
            if (radii.Count < 8) return false;
            radii.Sort();
            radius = radii[radii.Count / 2];
            scale = radius / AuthoredEyeRadius;
            // Eyeball globes and pupils (inside the lid ellipsoid) are flattened toward the eye centre; the old straight
            // mouth dash collapses to a point (the smile replaces it).
            Vector3 middle = (eyes[0].center + eyes[1].center) * .5f;
            Vector3 lateralAxis = Vector3.Cross(up, (eyes[0].forward + eyes[1].forward).normalized).normalized;
            float eyeGap = Vector3.Distance(eyes[0].center, eyes[1].center);
            var mouthIndices = new List<int>();
            for (int i = 0; i < vertices.Length; i++)
            {
                if (expression[i])
                {
                    Vector3 d = basis[i] - middle;
                    float below = -Vector3.Dot(d, up), side = Mathf.Abs(Vector3.Dot(d, lateralAxis));
                    if (below > eyeGap * .6f && below < eyeGap * 3f && side < eyeGap * .6f) { mouthIndices.Add(i); continue; }
                }
                if (!eyeWhite[i] && !expression[i]) continue;
                int e = Nearest(eyes, basis[i]);
                if (!InsideEye(basis[i], eyes[e], up)) continue;
                vertices[i] = Flatten(basis[i], eyes[e], s);
                if (hasNormals) normals[i] = FlattenNormal(sourceNormals[i], basis[i], eyes[e], s);
            }
            if (mouthIndices.Count > 0)
            {
                Vector3 sum = Vector3.zero;
                foreach (int i in mouthIndices) sum += basis[i];
                mouth = sum / mouthIndices.Count;
                foreach (int i in mouthIndices) vertices[i] = mouth - (eyes[0].forward + eyes[1].forward).normalized * (.004f * scale);
                hasMouth = true;
                mouthCount = mouthIndices.Count;
            }

            // Blink targets: rebuild every blend shape (same order and names) with the closed positions flattened.
            int count = original.blendShapeCount;
            var shapes = new List<(string name, float weight, Vector3[] dv, Vector3[] dn, Vector3[] dt)>();
            var seams = new List<Vector3>[] { new List<Vector3>(), new List<Vector3>() };
            for (int shape = 0; shape < count; shape++)
            {
                string name = original.GetBlendShapeName(shape);
                if (original.GetBlendShapeFrameCount(shape) != 1) return false;
                var dv = new Vector3[vertices.Length]; var dn = new Vector3[vertices.Length]; var dt = new Vector3[vertices.Length];
                original.GetBlendShapeFrameVertices(shape, 0, dv, dn, dt);
                int eye = Array.IndexOf(leftBlink, name) >= 0 ? 0 : Array.IndexOf(rightBlink, name) >= 0 ? 1 : -1;
                bool closed = eye >= 0 && name == (eye == 0 ? leftBlink[leftBlink.Length - 1] : rightBlink[rightBlink.Length - 1]);
                if (eye >= 0)
                {
                    var closedPositions = new List<(Vector3 closed, Vector3 open)>();
                    for (int i = 0; i < dv.Length; i++)
                    {
                        if (dv[i].sqrMagnitude < 1e-12f) continue;
                        Vector3 target = basis[i] + dv[i];
                        if (closed) closedPositions.Add((target, basis[i]));
                        Vector3 flat = Flatten(target, eyes[eye], s);
                        Vector3 n = (hasNormals ? sourceNormals[i] : Vector3.zero) + dn[i];
                        dv[i] = flat - vertices[i];
                        if (hasNormals && n.sqrMagnitude > 1e-8f) dn[i] = FlattenNormal(n.normalized, target, eyes[eye], s) - normals[i];
                    }
                    if (closed) seams[eye] = Seam(closedPositions, eyes[eye], up);
                }
                shapes.Add((name, original.GetBlendShapeFrameWeight(shape, 0), dv, dn, dt));
            }
            // The lid seam only proves the blink targets are the expected closed lids (upper and lower meeting).
            if (seams[0].Count < 3 || seams[1].Count < 3) return false;

            flattened = UnityEngine.Object.Instantiate(original);
            flattened.name = original.name + " (sleeping)";
            flattened.vertices = vertices;
            if (hasNormals) flattened.normals = normals;
            flattened.ClearBlendShapes();
            foreach (var shape in shapes) flattened.AddBlendShapeFrame(shape.name, shape.weight, shape.dv, shape.dn, shape.dt);
            flattened.RecalculateBounds();
            flattened.bounds = original.bounds;
            head.sharedMesh = flattened;

            ink = CreateInk();
            Matrix4x4 toHead = bindposes[headIndex];
            // The face direction in head-bone space comes from the eyes themselves (the rig's HeadForward is the bone's
            // own look convention, not the face normal of this mesh).
            headForwardLocal = toHead.MultiplyVector((eyes[0].forward + eyes[1].forward).normalized).normalized;
            for (int eye = 0; eye < 2; eye++)
            {
                // The closed-eye curve sits on the lower edge of the (flattened) closed lid: a smile-shaped arc on the lid
                // shell (lateral 1.09 r, height 1.2 r) a little inside its outline, from one corner of the eye to the other.
                var frame = eyes[eye];
                Vector3 lateral = Vector3.Cross(up, frame.forward).normalized;
                Vector3 vertical = Vector3.Cross(frame.forward, lateral).normalized;
                float rLat = radius * 1.09f, rFwd = radius * 1.09f, rUp = radius * 1.2f;
                float psi = settings.LineAngleFromFront * Mathf.Deg2Rad;
                var points = new List<Vector3>();
                const int segments = 12;
                for (int k = 0; k <= segments; k++)
                {
                    float theta = Mathf.Lerp(270f - settings.LineSpanDegrees * .5f, 270f + settings.LineSpanDegrees * .5f, k / (float)segments) * Mathf.Deg2Rad;
                    Vector3 onShell = frame.center + frame.forward * (rFwd * Mathf.Cos(psi))
                        + (lateral * (rLat * Mathf.Cos(theta)) + vertical * (rUp * Mathf.Sin(theta))) * Mathf.Sin(psi);
                    Vector3 flat = Flatten(onShell, frame, s);
                    // Just proud of the flattened lid so it never z-fights with it.
                    Vector3 outward = FlattenNormal((onShell - frame.center).normalized, onShell, frame, s);
                    points.Add(flat + outward * (.0016f * scale));
                }
                AddStrip(LineName + (eye == 0 ? ".L" : ".R"), points, frame.forward, settings.LineWidth * scale, toHead);
            }
            Report = "radius " + radius.ToString("F4") + " scale " + scale.ToString("F3") + " mouth " + mouthCount + " seams " + seams[0].Count + "/" + seams[1].Count +
                " eyeL " + eyes[0].center.ToString("F3") + " fwd " + eyes[0].forward.ToString("F2") + " up " + up.ToString("F2");
            Debug.Log("LMS_SLEEP_FACE " + Report);
            if (settings.SmileWidth > 0f && hasMouth)
            {
                Vector3 forward = (eyes[0].forward + eyes[1].forward).normalized;
                float width = settings.SmileWidth * scale;
                var points = new List<Vector3>();
                var triangles = flattened.triangles;
                const int segments = 16;
                for (int k = 0; k <= segments; k++)
                {
                    float t = k / (float)segments * 2f - 1f;
                    // A small closed-mouth smile where the old mouth line was: the ends rise. Each point sits proud of the
                    // bind-pose face surface under it (the cheeks curve forward of the mouth: a fixed offset hid the rising
                    // ends); the margin covers the sleeping pose's mouth deformation, the strip being rigid on the head bone.
                    Vector3 onPlane = mouth + lateralAxis * (t * width * .5f) + up * ((t * t - .35f) * width * .28f);
                    points.Add(OnSurface(vertices, triangles, onPlane, forward, .009f * scale, .006f * scale));
                }
                AddStrip(SmileName, points, forward, settings.SmileLineWidth * scale, toHead);
            }
            return true;
        }

        /// <summary>
        /// The first mesh surface met by a ray cast back along -forward through <paramref name="point"/> (from 0.2 m in
        /// front), plus <paramref name="lift"/> along forward; <paramref name="point"/> + fallback when nothing is hit.
        /// </summary>
        private static Vector3 OnSurface(Vector3[] vertices, int[] triangles, Vector3 point, Vector3 forward, float lift, float fallback)
        {
            Vector3 origin = point + forward * .2f, direction = -forward;
            float best = float.MaxValue;
            for (int i = 0; i + 2 < triangles.Length; i += 3)
            {
                Vector3 a = vertices[triangles[i]], b = vertices[triangles[i + 1]], c = vertices[triangles[i + 2]];
                Vector3 e1 = b - a, e2 = c - a, p = Vector3.Cross(direction, e2);
                float det = Vector3.Dot(e1, p);
                if (Mathf.Abs(det) < 1e-10f) continue;
                float inv = 1f / det;
                Vector3 q = origin - a;
                float u = Vector3.Dot(q, p) * inv;
                if (u < 0f || u > 1f) continue;
                Vector3 r = Vector3.Cross(q, e1);
                float v = Vector3.Dot(direction, r) * inv;
                if (v < 0f || u + v > 1f) continue;
                float distance = Vector3.Dot(e2, r) * inv;
                if (distance > 0f && distance < best) best = distance;
            }
            // A hit far in front of the mouth plane is the nose, not the lips: keep the fixed offset there.
            if (best >= .4f || .2f - best > 4f * fallback) return point + forward * fallback;
            return origin + direction * best + forward * lift;
        }

        private struct EyeFrame { public Vector3 center, forward; }

        private const float AuthoredEyeRadius = .066f;
        private float radius = AuthoredEyeRadius, scale = 1f;
        private Vector3 mouth;
        private bool hasMouth;
        private int mouthCount;

        private static int Nearest(EyeFrame[] eyes, Vector3 point) =>
            (point - eyes[0].center).sqrMagnitude <= (point - eyes[1].center).sqrMagnitude ? 0 : 1;

        /// <summary>Inside the eyeball ellipsoid (slightly taller than wide), with a little margin for the pupil lens.</summary>
        private bool InsideEye(Vector3 point, EyeFrame eye, Vector3 up)
        {
            Vector3 d = point - eye.center;
            float vertical = Vector3.Dot(d, up);
            Vector3 flat = d - up * vertical;
            float r = radius * 1.08f;
            return flat.sqrMagnitude / (r * r) + vertical * vertical / (r * r * 1.25f) <= 1f;
        }

        private static EyeFrame Eye(Matrix4x4 bindpose, Vector3 forwardLocal)
        {
            var inverse = bindpose.inverse;
            return new EyeFrame { center = inverse.MultiplyPoint3x4(Vector3.zero), forward = inverse.MultiplyVector(forwardLocal).normalized };
        }

        /// <summary>Scales the part in front of the eye centre along the eye's forward axis.</summary>
        private static Vector3 Flatten(Vector3 point, EyeFrame eye, float s)
        {
            float along = Vector3.Dot(point - eye.center, eye.forward);
            return along <= 0f ? point : point - eye.forward * (along * (1f - s));
        }

        private static Vector3 FlattenNormal(Vector3 normal, Vector3 point, EyeFrame eye, float s)
        {
            if (Vector3.Dot(point - eye.center, eye.forward) <= 0f) return normal;
            float along = Vector3.Dot(normal, eye.forward);
            Vector3 result = normal - eye.forward * along + eye.forward * (along / s);
            return result.sqrMagnitude > 1e-10f ? result.normalized : normal;
        }

        /// <summary>
        /// The closed upper and lower lids meet on one ring (the lower edge of the closed lid). Vertices of both lids land on
        /// the same closed positions there, so the seam is the set of duplicated closed positions, ordered left to right and
        /// kept to the visible front of the eye.
        /// </summary>
        private static List<Vector3> Seam(List<(Vector3 closed, Vector3 open)> lid, EyeFrame eye, Vector3 up)
        {
            // Flat-shaded meshes split every corner, so a shared closed position only marks the seam when the vertices
            // came from different open positions (one on the upper lid rim, one on the lower lid rim).
            var seam = new List<Vector3>();
            for (int i = 0; i < lid.Count; i++)
            {
                if (Vector3.Dot(lid[i].closed - eye.center, eye.forward) < 0f) continue;
                bool meeting = false;
                for (int j = 0; j < lid.Count && !meeting; j++)
                    if ((lid[i].closed - lid[j].closed).sqrMagnitude < 1e-9f && (lid[i].open - lid[j].open).sqrMagnitude > 1e-8f) meeting = true;
                if (!meeting || seam.Exists(p => (p - lid[i].closed).sqrMagnitude < 1e-9f)) continue;
                seam.Add(lid[i].closed);
            }
            Vector3 lateral = Vector3.Cross(up, eye.forward).normalized;
            seam.Sort((a, b) => Vector3.Dot(a - eye.center, lateral).CompareTo(Vector3.Dot(b - eye.center, lateral)));
            return seam;
        }

        private Material CreateInk()
        {
            // A copy of the head's own expression (or skin) material keeps the character shader and its keywords.
            Material source = null;
            foreach (var m in head.sharedMaterials) if (m && m.name.StartsWith("Character_Expression", StringComparison.Ordinal)) { source = m; break; }
            if (!source) source = head.sharedMaterial;
            var material = new Material(source) { name = "LMS_SleepInk" };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", settings.LineColor);
            if (material.HasProperty("_Color")) material.SetColor("_Color", settings.LineColor);
            return material;
        }

        private void AddStrip(string name, List<Vector3> meshPoints, Vector3 facing, float width, Matrix4x4 toHead)
        {
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            for (int i = 0; i < meshPoints.Count; i++)
            {
                Vector3 tangent = meshPoints[Mathf.Min(i + 1, meshPoints.Count - 1)] - meshPoints[Mathf.Max(i - 1, 0)];
                Vector3 across = Vector3.Cross(facing, tangent).normalized * (width * .5f);
                vertices.Add(toHead.MultiplyPoint3x4(meshPoints[i] + across));
                vertices.Add(toHead.MultiplyPoint3x4(meshPoints[i] - across));
                if (i == 0) continue;
                int b = (i - 1) * 2;
                // Both windings: the strip is seen from the front whatever the bone axes are.
                triangles.AddRange(new[] { b, b + 2, b + 1, b + 1, b + 2, b + 3, b, b + 1, b + 2, b + 1, b + 3, b + 2 });
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            Vector3 normal = toHead.MultiplyVector(facing).normalized;
            var normalsList = new List<Vector3>(); for (int i = 0; i < vertices.Count; i++) normalsList.Add(normal);
            mesh.SetNormals(normalsList);
            mesh.RecalculateBounds();
            var go = new GameObject(name);
            go.transform.SetParent(headBone, false);
            go.layer = head.gameObject.layer;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = ink;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.renderingLayerMask = head.renderingLayerMask;
            created.Add(go);
            LineCount++;
        }

        /// <summary>After animation: turn the head up to <see cref="Settings.HeadTurnDegrees"/> toward the target.</summary>
        public void Apply()
        {
            LastTurnDegrees = 0f;
            if (!headBone || !turnTarget || settings.HeadTurnDegrees <= 0f) return;
            Vector3 forward = headBone.TransformDirection(headForwardLocal).normalized;
            Vector3 toTarget = (turnTarget.position - headBone.position).normalized;
            float angle = Vector3.Angle(forward, toTarget);
            if (angle < .01f) return;
            Vector3 axis = Vector3.Cross(forward, toTarget).normalized;
            float turn = Mathf.Min(settings.HeadTurnDegrees, angle);
            headBone.rotation = Quaternion.AngleAxis(turn, axis) * headBone.rotation;
            LastTurnAxis = axis; LastTurnDegrees = turn;
        }

        public void Dispose()
        {
            foreach (var go in created)
                if (go)
                {
                    var filter = go.GetComponent<MeshFilter>();
                    if (filter && filter.sharedMesh) UnityEngine.Object.Destroy(filter.sharedMesh);
                    UnityEngine.Object.Destroy(go);
                }
            created.Clear();
            if (head && flattened && head.sharedMesh == flattened && original) head.sharedMesh = original;
            if (flattened) UnityEngine.Object.Destroy(flattened);
            if (ink) UnityEngine.Object.Destroy(ink);
            flattened = null; ink = null; LineCount = 0;
        }
    }
}
