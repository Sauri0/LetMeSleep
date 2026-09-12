using System;
using UnityEngine;

namespace LetMeSleep.Presentation
{
    /// <summary>One final visual writer after animation. No actor pose, hit volumes, materials or network writes.</summary>
    [DefaultExecutionOrder(1200)]
    [DisallowMultipleComponent]
    public sealed class VisualAttentionRig : MonoBehaviour
    {
        [Serializable]
        public sealed class BlinkBone
        {
            public Transform Bone;
            public Vector3 LocalAxis=Vector3.up;
            public float ClosedAngleDegrees;
        }
        [Serializable]
        public sealed class Bindings
        {
            public Transform Head, Neck, LeftEye, RightEye;
            public Vector3 HeadForward=Vector3.forward, HeadUp=Vector3.up;
            public Vector3 EyeForward=Vector3.up, EyeUp=Vector3.forward;
            public SkinnedMeshRenderer Eyelids;
            public string[] LeftBlinkShapes={"Blink25.L","Blink50.L","Blink75.L","Blink.L"};
            public string[] RightBlinkShapes={"Blink25.R","Blink50.R","Blink75.R","Blink.R"};
            public BlinkBone[] LeftLids=Array.Empty<BlinkBone>(), RightLids=Array.Empty<BlinkBone>();
            public bool ReadLegacyEyeScaleBlink;
            public float HeadYawLimit=55,HeadPitchLimit=25,EyeYawLimit=22,EyePitchLimit=15;
            public bool ManualEvaluation;
        }
        private sealed class Joint
        {
            public Transform bone;
            public Vector3 forward,up;
            public float yawLimit,pitchLimit,yaw,pitch;
            public Quaternion before,after;
            public bool written;
            public void Restore()
            {
                if(bone && written && Quaternion.Angle(bone.localRotation,after)<.01f) bone.localRotation=before;
                written=false;
            }
        }
        private sealed class LidState
        {
            public BlinkBone binding;
            public Quaternion before,after;
            public bool written;
            public void Restore()
            {
                if(written && binding.Bone && Quaternion.Angle(binding.Bone.localRotation,after)<.01f) binding.Bone.localRotation=before;
                written=false;
            }
            public void Apply(float closure)
            {
                if(!binding.Bone) return;
                before=binding.Bone.localRotation;
                after=before*Quaternion.AngleAxis(binding.ClosedAngleDegrees*closure,binding.LocalAxis.normalized);
                binding.Bone.localRotation=after; written=true;
            }
        }
        private Bindings binding;
        private Joint neck,head,left,right;
        private Transform target;
        private Vector3 point;
        private bool hasPoint,reduced,configured;
        private int[] leftBlink,rightBlink;
        private readonly float[] oldLeft=new float[4],oldRight=new float[4],lastLeft=new float[4],lastRight=new float[4];
        private LidState[] leftLids=Array.Empty<LidState>(),rightLids=Array.Empty<LidState>();
        private Vector3 oldLeftScale,oldRightScale;
        private bool scaleWritten;
        private bool blinkWritten;
        private double clock,nextBlink;
        private System.Random random;
        public bool IsConfigured=>configured;
        public bool IsManualEvaluation=>configured && binding.ManualEvaluation;
        public Transform LookOrigin=>configured ? binding.Head : transform;
        public bool SupportsBlink=>(leftBlink!=null && rightBlink!=null) || (leftLids.Length>0 && rightLids.Length>0);
        public bool Configure(Bindings value)
        {
            Restore(); configured=false;
            if(value==null || !value.Head || !value.Head.IsChildOf(transform) ||
                !Axes(value.HeadForward,value.HeadUp) || !Axes(value.EyeForward,value.EyeUp)) return false;
            foreach(var bone in new[]{value.Neck,value.LeftEye,value.RightEye})
                if(bone && (!bone.IsChildOf(transform) || bone==value.Head)) return false;
            if(value.LeftEye && value.LeftEye==value.RightEye) return false;
            if(value.Eyelids && !value.Eyelids.transform.IsChildOf(transform)) return false;
            binding=value;
            head=Make(value.Head,value.HeadForward,value.HeadUp,value.HeadYawLimit,value.HeadPitchLimit);
            neck=Make(value.Neck,value.HeadForward,value.HeadUp,20,12);
            left=Make(value.LeftEye,value.EyeForward,value.EyeUp,value.EyeYawLimit,value.EyePitchLimit);
            right=Make(value.RightEye,value.EyeForward,value.EyeUp,value.EyeYawLimit,value.EyePitchLimit);
            leftBlink=Shapes(value.Eyelids,value.LeftBlinkShapes); rightBlink=Shapes(value.Eyelids,value.RightBlinkShapes);
            leftLids=Lids(value.LeftLids); rightLids=Lids(value.RightLids);
            if(leftLids==null || rightLids==null) { leftLids=rightLids=Array.Empty<LidState>(); return false; }
            random=new System.Random(GetInstanceID()); clock=0; nextBlink=2.2+random.NextDouble()*1.8;
            configured=true; return true;
        }
        private static bool Axes(Vector3 forward,Vector3 up)=>forward.sqrMagnitude>.5f && up.sqrMagnitude>.5f && Vector3.Cross(forward,up).sqrMagnitude>.1f;
        private static Joint Make(Transform bone,Vector3 forward,Vector3 up,float yaw,float pitch)=>new Joint
        {bone=bone,forward=forward.normalized,up=up.normalized,yawLimit=yaw,pitchLimit=pitch};
        public void SetLookTarget(Transform value) { target=value; hasPoint=false; }
        public void SetLookPoint(Vector3 value) { target=null; point=value; hasPoint=true; }
        public void ClearLookTarget() { target=null; hasPoint=false; }
        public void SetReducedMotion(bool value)
        {
            reduced=value;
            if(value)
            {
                Restore();
                foreach(var joint in new[]{neck,head,left,right}) if(joint!=null) { joint.yaw=0; joint.pitch=0; }
            }
        }
        // Manual menu owner calls this BEFORE graph.Evaluate, then EvaluateAfterAnimation AFTER it.
        public void PrepareForAnimation()=>Restore();
        private void Update() { if(configured && !binding.ManualEvaluation) Restore(); }
        private void LateUpdate() { if(configured && !binding.ManualEvaluation) EvaluateAfterAnimation(Time.unscaledDeltaTime); }
        public void EvaluateAfterAnimation(float deltaSeconds)
        {
            if(!configured || !isActiveAndEnabled) return;
            float dt=Mathf.Max(0,deltaSeconds);
            bool looking=!reduced && (target || hasPoint);
            Vector3 destination=target ? target.position : point;
            Apply(neck,destination,looking,dt); Apply(head,destination,looking,dt);
            Apply(left,destination,looking,dt); Apply(right,destination,looking,dt);
            clock+=dt;
            if(clock>nextBlink+.25) nextBlink=clock+3.2+random.NextDouble()*2.6;
            float closureLeft=Blink((float)(clock-nextBlink))*.01f;
            float closureRight=Blink((float)(clock-nextBlink)-.012f)*.01f;
            if(binding.ReadLegacyEyeScaleBlink && binding.LeftEye && binding.RightEye)
            {
                oldLeftScale=binding.LeftEye.localScale; oldRightScale=binding.RightEye.localScale;
                closureLeft=Mathf.Max(closureLeft,Mathf.Clamp01((1-oldLeftScale.z)/.93f));
                closureRight=Mathf.Max(closureRight,Mathf.Clamp01((1-oldRightScale.z)/.93f));
                binding.LeftEye.localScale=Vector3.one; binding.RightEye.localScale=Vector3.one; scaleWritten=true;
            }
            if(binding.Eyelids && leftBlink!=null && rightBlink!=null)
            {
                BlinkWeightPolicy.Fill(closureLeft,lastLeft); BlinkWeightPolicy.Fill(closureRight,lastRight);
                for(int i=0;i<4;i++)
                {
                    oldLeft[i]=binding.Eyelids.GetBlendShapeWeight(leftBlink[i]); oldRight[i]=binding.Eyelids.GetBlendShapeWeight(rightBlink[i]);
                    binding.Eyelids.SetBlendShapeWeight(leftBlink[i],lastLeft[i]); binding.Eyelids.SetBlendShapeWeight(rightBlink[i],lastRight[i]);
                }
                blinkWritten=true;
            }
            foreach(var lid in leftLids) lid.Apply(closureLeft);
            foreach(var lid in rightLids) lid.Apply(closureRight);
        }
        private static int[] Shapes(SkinnedMeshRenderer renderer,string[] names)
        {
            if(!renderer || !renderer.sharedMesh || names==null || names.Length!=4) return null;
            var result=new int[4];
            for(int i=0;i<4;i++) { result[i]=renderer.sharedMesh.GetBlendShapeIndex(names[i]); if(result[i]<0) return null; }
            return result;
        }
        private LidState[] Lids(BlinkBone[] values)
        {
            if(values==null) return Array.Empty<LidState>();
            var result=new LidState[values.Length];
            for(int i=0;i<values.Length;i++)
            {
                var value=values[i];
                if(value==null || !value.Bone || !value.Bone.IsChildOf(transform) || value.LocalAxis.sqrMagnitude<.1f) return null;
                result[i]=new LidState{binding=value};
            }
            return result;
        }        private static void Apply(Joint joint,Vector3 destination,bool looking,float dt)
        {
            if(joint==null || !joint.bone) return;
            var bone=joint.bone;
            Quaternion basis=Quaternion.LookRotation(bone.TransformDirection(joint.forward),bone.TransformDirection(joint.up));
            Vector3 local=Quaternion.Inverse(basis)*(destination-bone.position);
            float yaw=0,pitch=0;
            if(looking && local.sqrMagnitude>.0001f)
            {
                yaw=Mathf.Clamp(Mathf.Atan2(local.x,local.z)*Mathf.Rad2Deg,-joint.yawLimit,joint.yawLimit);
                pitch=Mathf.Clamp(-Mathf.Atan2(local.y,Mathf.Sqrt(local.x*local.x+local.z*local.z))*Mathf.Rad2Deg,-joint.pitchLimit,joint.pitchLimit);
            }
            float blend=1-Mathf.Exp(-10*dt);
            joint.yaw=Mathf.LerpAngle(joint.yaw,yaw,blend); joint.pitch=Mathf.LerpAngle(joint.pitch,pitch,blend);
            joint.before=bone.localRotation;
            bone.rotation=basis*Quaternion.Euler(joint.pitch,joint.yaw,0)*Quaternion.Inverse(basis)*bone.rotation;
            joint.after=bone.localRotation; joint.written=true;
        }
        private static float Blink(float seconds)
        {
            if(seconds<0 || seconds>=.22f) return 0;
            if(seconds<.07f) return Mathf.SmoothStep(0,100,seconds/.07f);
            if(seconds<.10f) return 100;
            return Mathf.SmoothStep(100,0,(seconds-.10f)/.12f);
        }
        private void Restore()
        {
            // Child local poses first; never undo another animation writer's newer value.
            right?.Restore(); left?.Restore(); head?.Restore(); neck?.Restore();
            if(blinkWritten && binding!=null && binding.Eyelids)
            {
                for(int i=0;i<4;i++)
                {
                    if(Mathf.Abs(binding.Eyelids.GetBlendShapeWeight(leftBlink[i])-lastLeft[i])<.01f) binding.Eyelids.SetBlendShapeWeight(leftBlink[i],oldLeft[i]);
                    if(Mathf.Abs(binding.Eyelids.GetBlendShapeWeight(rightBlink[i])-lastRight[i])<.01f) binding.Eyelids.SetBlendShapeWeight(rightBlink[i],oldRight[i]);
                }
            }
            foreach(var lid in leftLids) lid.Restore();
            foreach(var lid in rightLids) lid.Restore();
            if(scaleWritten && binding!=null)
            {
                if(binding.LeftEye && binding.LeftEye.localScale==Vector3.one) binding.LeftEye.localScale=oldLeftScale;
                if(binding.RightEye && binding.RightEye.localScale==Vector3.one) binding.RightEye.localScale=oldRightScale;
            }
            scaleWritten=false;
            blinkWritten=false;
        }
        private void OnDisable()=>Restore();
        private void OnDestroy()=>Restore();
    }
}
