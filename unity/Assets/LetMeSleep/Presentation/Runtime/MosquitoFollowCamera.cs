using System;
using System.Collections.Generic;
using UnityEngine;

namespace LetMeSleep.Presentation
{
    [DefaultExecutionOrder(1250)]
    [DisallowMultipleComponent]
    public sealed class MosquitoFollowCamera : MonoBehaviour
    {
        private const int HitCapacity = 12;
        private const int OverlapCapacity = 12;
        private const int DepenetrationIterations = 4;

        [SerializeField] private AlfaPresentationPreset preset = null;
        [SerializeField] private Camera controlledCamera = null;
        [SerializeField] private Transform safeAnchor = null;
        [SerializeField] private Transform pivot = null;
        [SerializeField] private Transform cameraTransform = null;
        [SerializeField] private LayerMask collisionMask = -1;
        [SerializeField, Min(0f)] private float collisionPadding = 0.015f;
        [SerializeField, Min(0f)] private float outwardDampingSeconds = 0.08f;
        [SerializeField, Min(0f)] private float rotationDampingSeconds = 0.05f;

        private struct RenderState { public Renderer Renderer; public bool ForceOff; }
        private struct BodyPart { public Transform Bone; public Bounds Bounds; }
        private RenderState[] localRenderers=Array.Empty<RenderState>();
        private BodyPart[] bodyParts=Array.Empty<BodyPart>();
        private Transform localVisual,firstPersonAnchor;
        private float firstPersonBlend;
        public bool IsFirstPerson=>EffectiveRequestedDistance<=.001f;
        private bool localHidden;
        public bool IsLocalVisualHidden=>localHidden;
        public float EffectiveRequestedDistance { get; private set; }
        public float BodyEntryMargin { get; private set; }
        public float BodyExitMargin { get; private set; }
        public Bounds BodyWorldBounds { get; private set; }

        private readonly RaycastHit[] hits = new RaycastHit[HitCapacity];
        private readonly Collider[] overlaps = new Collider[OverlapCapacity];
        private Func<Collider, bool> collisionFilter;
        private SphereCollider collisionProbe;
        private Quaternion desiredRotation = Quaternion.identity;
        private Quaternion smoothedRotation = Quaternion.identity;
        private float desiredDistance = 0.85f;
        private float smoothedDistance;
        private float distanceVelocity;
        private bool initialized;

        public float DesiredDistance => desiredDistance;
        public float ResolvedDistance => smoothedDistance;

        private void Awake()
        {
            Initialize();
            ApplyPreset();
        }

        private void LateUpdate()
        {
            if (!initialized)
                Initialize();
            if (!initialized)
                return;
            if (!safeAnchor || !pivot || !cameraTransform) { Unbind(); return; }

            float rotationFactor = DampingFactor(rotationDampingSeconds, Time.unscaledDeltaTime);
            smoothedRotation = Quaternion.Slerp(smoothedRotation, desiredRotation, rotationFactor);

            float radius = preset != null ? preset.CameraCollisionRadius : 0.08f;
            Vector3 anchorPosition = ResolveSafePoint(safeAnchor.position, radius);
            Vector3 desiredPivot = pivot.position;
            firstPersonBlend=0;
            if(TryBodyBounds(out var body))
            {
                float transitionDistance=body.extents.magnitude+NearMargin();
                firstPersonBlend=1-Mathf.SmoothStep(0,1,desiredDistance/Mathf.Max(.001f,transitionDistance));
                Vector3 eye=firstPersonAnchor ? firstPersonAnchor.position : body.center;
                desiredPivot=Vector3.Lerp(desiredPivot,eye,firstPersonBlend);
            }

            // Sweep 1: a known safe actor anchor to the visual camera pivot.
            Vector3 resolvedPivot = Sweep(anchorPosition, desiredPivot, radius);
            if (IsBlocked(resolvedPivot, radius))
                resolvedPivot = anchorPosition;

            float maximum = preset != null ? preset.MosquitoMaximumDistance : 2.5f;
            float requested = Mathf.Clamp(desiredDistance, 0f, maximum);
            EffectiveRequestedDistance=requested;
            Vector3 desiredCamera = resolvedPivot - smoothedRotation * Vector3.forward * requested;

            // Sweep 2: resolved pivot to the requested camera position.
            Vector3 resolvedCamera = Sweep(resolvedPivot, desiredCamera, radius);
            if (IsBlocked(resolvedCamera, radius))
                resolvedCamera = resolvedPivot;

            float allowedDistance = Vector3.Distance(resolvedPivot, resolvedCamera);
            if (allowedDistance < smoothedDistance)
            {
                smoothedDistance = allowedDistance;
                distanceVelocity = 0f;
            }
            else
            {
                smoothedDistance = Mathf.SmoothDamp(
                    smoothedDistance, allowedDistance, ref distanceVelocity,
                    outwardDampingSeconds, Mathf.Infinity, Time.unscaledDeltaTime);
            }

            cameraTransform.SetPositionAndRotation(
                resolvedPivot - smoothedRotation * Vector3.forward * smoothedDistance,
                smoothedRotation);
            UpdateLocalOcclusion();
        }

        public void SetView(Quaternion authoritativeViewRotation, float requestedDistance)
        {
            if (!IsFinite(authoritativeViewRotation) || float.IsNaN(requestedDistance) || float.IsInfinity(requestedDistance))
                return;
            desiredRotation = authoritativeViewRotation.normalized;
            float maximum = preset != null ? preset.MosquitoMaximumDistance : 2.5f;
            desiredDistance = Mathf.Clamp(requestedDistance, 0f, maximum);
        }

        public void ApplyPreset()
        {
            if (preset == null || controlledCamera == null)
                return;
            controlledCamera.fieldOfView = preset.MosquitoVerticalFov;
            controlledCamera.nearClipPlane = preset.MosquitoNearPlane;
            controlledCamera.farClipPlane = preset.FarPlane;
        }

        public void BindAnchors(Transform anchor, Transform cameraPivot)
        {
            ClearLocalVisual();
            safeAnchor = anchor;
            pivot = cameraPivot;
            initialized = false;
            Initialize();
        }

        public bool BindLocalVisual(Transform visual,Renderer[] renderers,Transform[] coreBones,Transform firstPersonView=null)
        {
            ClearLocalVisual();
            if(!visual || renderers==null || renderers.Length==0) return false;
            var unique=new HashSet<Renderer>();
            foreach(var renderer in renderers)
                if(!renderer || !unique.Add(renderer) || (renderer.transform!=visual && !renderer.transform.IsChildOf(visual))) return false;
            foreach(var bone in coreBones ?? Array.Empty<Transform>())
                if(!bone || (bone!=visual && !bone.IsChildOf(visual))) return false;
            if(firstPersonView && firstPersonView!=visual && !firstPersonView.IsChildOf(visual)) return false;
            localVisual=visual; firstPersonAnchor=firstPersonView;
            localRenderers=new RenderState[renderers.Length];
            for(int i=0;i<renderers.Length;i++) localRenderers[i]=new RenderState{Renderer=renderers[i],ForceOff=renderers[i].forceRenderingOff};
            var wanted=new HashSet<Transform>(coreBones ?? Array.Empty<Transform>());
            var bounds=new Dictionary<Transform,Bounds>();
            foreach(var renderer in renderers)
            {
                if(!(renderer is SkinnedMeshRenderer skin) || !skin.sharedMesh || !skin.sharedMesh.isReadable) continue;
                var vertices=skin.sharedMesh.vertices; var weights=skin.sharedMesh.boneWeights;
                var poses=skin.sharedMesh.bindposes; var bones=skin.bones;
                if(vertices.Length!=weights.Length || poses.Length!=bones.Length) continue;
                for(int i=0;i<vertices.Length;i++)
                {
                    var weight=weights[i];
                    AddBodyVertex(weight.boneIndex0,weight.weight0,vertices[i],bones,poses,wanted,bounds);
                    AddBodyVertex(weight.boneIndex1,weight.weight1,vertices[i],bones,poses,wanted,bounds);
                    AddBodyVertex(weight.boneIndex2,weight.weight2,vertices[i],bones,poses,wanted,bounds);
                    AddBodyVertex(weight.boneIndex3,weight.weight3,vertices[i],bones,poses,wanted,bounds);
                }
            }
            bodyParts=new BodyPart[bounds.Count]; int part=0;
            foreach(var pair in bounds) bodyParts[part++]=new BodyPart{Bone=pair.Key,Bounds=pair.Value};
            return true;
        }
        private static void AddBodyVertex(int index,float weight,Vector3 vertex,Transform[] bones,Matrix4x4[] poses,
            HashSet<Transform> wanted,Dictionary<Transform,Bounds> result)
        {
            if(weight<=0 || index<0 || index>=bones.Length || !bones[index] || !wanted.Contains(bones[index])) return;
            Vector3 local=poses[index].MultiplyPoint3x4(vertex);
            if(result.TryGetValue(bones[index],out var bounds)) { bounds.Encapsulate(local); result[bones[index]]=bounds; }
            else result.Add(bones[index],new Bounds(local,Vector3.zero));
        }
        private bool TryBodyBounds(out Bounds result)
        {
            result=default; bool found=false;
            if(!localVisual) return false;
            foreach(var part in bodyParts)
            {
                if(!part.Bone) continue;
                for(int corner=0;corner<8;corner++)
                {
                    var extent=part.Bounds.extents;
                    var point=part.Bone.TransformPoint(part.Bounds.center+new Vector3((corner&1)==0?-extent.x:extent.x,
                        (corner&2)==0?-extent.y:extent.y,(corner&4)==0?-extent.z:extent.z));
                    if(!found) { result=new Bounds(point,Vector3.zero); found=true; } else result.Encapsulate(point);
                }
            }
            // Non-skinned test/legacy visuals use their actual renderer bounds, never a guessed species radius.
            if(!found) foreach(var state in localRenderers) if(state.Renderer)
            { if(!found) { result=state.Renderer.bounds; found=true; } else result.Encapsulate(state.Renderer.bounds); }
            return found;
        }
        private float NearMargin()
        {
            if(!controlledCamera) return collisionPadding;
            float tangent=Mathf.Tan(controlledCamera.fieldOfView*.5f*Mathf.Deg2Rad);
            float nearSphere=controlledCamera.nearClipPlane*Mathf.Sqrt(1+tangent*tangent*(1+controlledCamera.aspect*controlledCamera.aspect));
            return Mathf.Max(collisionPadding,nearSphere);
        }
        private void UpdateLocalOcclusion()
        {
            if(!TryBodyBounds(out var body)) { RestoreLocalRenderers(); return; }
            BodyWorldBounds=body; BodyEntryMargin=NearMargin();
            BodyExitMargin=BodyEntryMargin+Mathf.Max(collisionPadding,body.extents.magnitude*.15f);
            body.Expand(2*(localHidden ? BodyExitMargin : BodyEntryMargin));
            bool hide=firstPersonBlend>.001f || body.Contains(cameraTransform.position);
            if(hide && !localHidden)
            {
                for(int i=0;i<localRenderers.Length;i++) if(localRenderers[i].Renderer)
                { localRenderers[i].ForceOff=localRenderers[i].Renderer.forceRenderingOff; localRenderers[i].Renderer.forceRenderingOff=true; }
                localHidden=true;
            }
            else if(!hide) RestoreLocalRenderers();
        }
        private void RestoreLocalRenderers()
        {
            if(!localHidden) return;
            foreach(var state in localRenderers) if(state.Renderer) state.Renderer.forceRenderingOff=state.ForceOff;
            localHidden=false;
        }
        private void ClearLocalVisual()
        {
            RestoreLocalRenderers(); localVisual=null; firstPersonAnchor=null; firstPersonBlend=0; localRenderers=Array.Empty<RenderState>(); bodyParts=Array.Empty<BodyPart>();
        }
        public void Unbind()
        {
            ClearLocalVisual(); safeAnchor=null; pivot=null; initialized=false;
        }
        private void OnDisable()=>RestoreLocalRenderers();
        private void OnDestroy()
        {
            ClearLocalVisual();
            if(collisionProbe) Destroy(collisionProbe.gameObject);
        }
        public void SetCollisionFilter(Func<Collider, bool> filter) => collisionFilter = filter;

        private void Initialize()
        {
            if (controlledCamera == null)
                controlledCamera = GetComponent<Camera>();
            if (cameraTransform == null && controlledCamera != null)
                cameraTransform = controlledCamera.transform;
            if (safeAnchor == null || pivot == null || cameraTransform == null)
                return;

            desiredRotation = cameraTransform.rotation;
            smoothedRotation = desiredRotation;
            desiredDistance = preset != null ? preset.MosquitoDefaultDistance : desiredDistance;
            smoothedDistance = Vector3.Distance(pivot.position, cameraTransform.position);
            initialized = true;
        }

        private Vector3 Sweep(Vector3 start, Vector3 end, float radius)
        {
            Vector3 displacement = end - start;
            float distance = displacement.magnitude;
            if (distance <= 0.0001f)
                return start;

            Vector3 direction = displacement / distance;
            int count = Physics.SphereCastNonAlloc(
                start, radius, direction, hits, distance, collisionMask,
                QueryTriggerInteraction.Ignore);
            float nearest = distance;
            for (int i = 0; i < count; i++)
            {
                Collider collider = hits[i].collider;
                if (!IsCollisionCandidate(collider))
                    continue;
                nearest = Mathf.Min(nearest, Mathf.Max(0f, hits[i].distance - collisionPadding));
            }
            return start + direction * nearest;
        }

        private Vector3 ResolveSafePoint(Vector3 point, float radius)
        {
            if (!IsBlocked(point, radius))
                return point;

            EnsureCollisionProbe(radius);
            collisionProbe.enabled = true;
            try
            {
                for (int iteration = 0; iteration < DepenetrationIterations; iteration++)
                {
                    int count = Physics.OverlapSphereNonAlloc(
                        point, radius, overlaps, collisionMask, QueryTriggerInteraction.Ignore);
                    bool moved = false;
                    for (int i = 0; i < count; i++)
                    {
                        Collider collider = overlaps[i];
                        if (!IsCollisionCandidate(collider))
                            continue;
                        if (!Physics.ComputePenetration(
                                collisionProbe, point, Quaternion.identity,
                                collider, collider.transform.position, collider.transform.rotation,
                                out Vector3 direction, out float distance) || distance <= 0f)
                            continue;

                        point += direction * (distance + collisionPadding);
                        moved = true;
                    }

                    if (!moved || !IsBlocked(point, radius))
                        break;
                }
            }
            finally
            {
                collisionProbe.enabled = false;
            }

            return point;
        }

        private void EnsureCollisionProbe(float radius)
        {
            if (collisionProbe == null)
            {
                var probeObject = new GameObject("CameraCollisionProbe")
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    layer = 2
                };
                probeObject.transform.SetParent(transform, false);
                collisionProbe = probeObject.AddComponent<SphereCollider>();
                collisionProbe.isTrigger = true;
                collisionProbe.enabled = false;
            }

            collisionProbe.radius = radius;
        }

        private bool IsBlocked(Vector3 point, float radius)
        {
            int count = Physics.OverlapSphereNonAlloc(
                point, radius, overlaps, collisionMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider collider = overlaps[i];
                if (IsCollisionCandidate(collider))
                    return true;
            }
            return false;
        }

        private bool IsCollisionCandidate(Collider collider)
        {
            return collider != null && collider != collisionProbe &&
                (collisionFilter == null || collisionFilter(collider)) &&
                !collider.transform.IsChildOf(safeAnchor);
        }

        private static float DampingFactor(float dampingSeconds, float deltaTime)
        {
            return dampingSeconds <= 0f ? 1f : 1f - Mathf.Exp(-deltaTime / dampingSeconds);
        }

        private static bool IsFinite(Quaternion value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
                !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
                !float.IsNaN(value.z) && !float.IsInfinity(value.z) &&
                !float.IsNaN(value.w) && !float.IsInfinity(value.w) &&
                value.x * value.x + value.y * value.y + value.z * value.z +
                value.w * value.w > 0.000001f;
        }
    }
}
