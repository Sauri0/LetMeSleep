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
            // v0.3.0 moods: lower lids close separately; upper lids tilt about the eye's forward axis.
            public bool lower;
            public Vector3 tiltAxis=Vector3.forward;
            public float tiltSign;
            public void Restore()
            {
                if(written && binding.Bone && Quaternion.Angle(binding.Bone.localRotation,after)<.01f) binding.Bone.localRotation=before;
                written=false;
            }
            public void Apply(float closure,float tiltDegrees=0)
            {
                if(!binding.Bone) return;
                before=binding.Bone.localRotation;
                Quaternion tilt=lower || Mathf.Abs(tiltDegrees)<.001f ? Quaternion.identity : Quaternion.AngleAxis(tiltSign*tiltDegrees,tiltAxis);
                after=before*tilt*Quaternion.AngleAxis(binding.ClosedAngleDegrees*closure,binding.LocalAxis.normalized);
                binding.Bone.localRotation=after; written=true;
            }
        }
        private sealed class MoodBone
        {
            public Transform bone;
            public Vector3 beforePosition,afterPosition;
            public Quaternion beforeRotation,afterRotation;
            public Vector3 beforeScale,afterScale;
            public bool written;
            // Scale-only writers (pupils, eyeballs) share their bone with the look-at joint, which restores
            // the rotation first; their restore must not depend on it.
            public bool scaleOnly;
            public void Begin() { beforePosition=bone.localPosition; beforeRotation=bone.localRotation; beforeScale=bone.localScale; }
            public void End() { afterPosition=bone.localPosition; afterRotation=bone.localRotation; afterScale=bone.localScale; written=true; }
            public void Restore()
            {
                if(written && bone && scaleOnly)
                { if((bone.localScale-afterScale).sqrMagnitude<1e-10f) bone.localScale=beforeScale; }
                else if(written && bone && Quaternion.Angle(bone.localRotation,afterRotation)<.01f &&
                    (bone.localPosition-afterPosition).sqrMagnitude<1e-10f && (bone.localScale-afterScale).sqrMagnitude<1e-10f)
                { bone.localPosition=beforePosition; bone.localRotation=beforeRotation; bone.localScale=beforeScale; }
                written=false;
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
        private System.Random random,microRandom;
        private double nextMicro;
        private float microYaw,microPitch;
        private bool headTrackingEnabled=true;
        public bool HeadTrackingEnabled=>headTrackingEnabled;
        // v0.3.0 expressions (PER-02/PER-03/PER-07). Lids, blink shapes, pupils, brows and jaw are
        // written here only, after animation, and restored like every other write of this rig.
        private FacialMood mood=FacialMood.Neutral;
        private FacialMoodShape moodTarget=FacialMoodShape.Neutral,moodCurrent=FacialMoodShape.Neutral;
        private float moodBlendSeconds=.12f,dizzyPhase;
        private MoodBone leftBrow,rightBrow,jaw,leftPupil,rightPupil,leftEyeball,rightEyeball;
        private float leftBrowSign,rightBrowSign;
        private int eyeUpAxis=-1,eyeLookAxis=-1;
        private int smileShape=-1,mouthOShape=-1,frownShape=-1;
        private float oldSmile,oldMouthO,oldFrown,lastSmile,lastMouthO,lastFrown;
        private bool mouthWritten;
        // Human pupil decals sit ~2 mm off the faceted globe; turning or narrowing them lets facet creases poke
        // through (bitten edges, white holes). They are pushed out along the look axis while the lid is open:
        // enough to clear the narrowing across a 16 deg decal plus a 3.5% (~2.3 mm) crease margin, fading to
        // the authored depth as the lid closes so the closed lid shell (1.09 r) always covers them.
        private const float HumanPupilCreaseLift=.035f,HumanPupilMin=.82f,HumanPupilMax=1.3f,HumanSquintMax=.3f;
        private static readonly float PupilEdgeSin2=Mathf.Pow(Mathf.Sin(16*Mathf.Deg2Rad),2),PupilEdgeCos2=Mathf.Pow(Mathf.Cos(16*Mathf.Deg2Rad),2);
        public bool SupportsEyeScale=>leftEyeball!=null && rightEyeball!=null;
        public FacialMood Mood=>mood;
        public FacialMoodShape MoodShape=>moodCurrent;
        public bool SupportsBrows=>leftBrow!=null && rightBrow!=null;
        public bool SupportsJaw=>jaw!=null;
        public bool SupportsLowerLids{ get { foreach(var lid in leftLids) if(lid.lower) return true; return false; } }
        /// <summary>v0.3.0 round 3: the human mouth morphs (Smile, MouthO, Frown) are optional; older heads lack them.</summary>
        public bool SupportsMouthShapes=>smileShape>=0 && mouthOShape>=0 && frownShape>=0;
        /// <summary>
        /// Mood head pitch/roll (and the dizzy sway) on top of the look-at. The first-person local human turns it
        /// off: its camera rides on the head, and a mood must never move the player's view.
        /// </summary>
        public bool MoodHeadPoseEnabled { get; set; } = true;
        /// <summary>Scale of the human pupil decals along the look axis on the last evaluation (1 = authored).</summary>
        public float LastPupilLift { get; private set; } = 1;
        /// <summary>Degrees of mood head pose (pitch/roll/dizzy sway) applied on the last evaluation.</summary>
        public float LastMoodHeadDegrees { get; private set; }
        /// <summary>
        /// Sets the expression target; channels ease toward it over blendSeconds. A blend of 0 or less cuts to the
        /// target on the next evaluation, and so does a cheer (Excited/Happy) that interrupts a yawn (director r4:
        /// the victory face never drags the yawn's drooping lids and O mouth through its first frames).
        /// </summary>
        public void SetMood(FacialMood value,float weight=1,float blendSeconds=.12f)
        {
            if(float.IsNaN(weight) || float.IsInfinity(weight)) weight=0;
            if(float.IsNaN(blendSeconds) || float.IsInfinity(blendSeconds)) blendSeconds=.12f;
            bool cut=blendSeconds<=0 || (mood==FacialMood.Yawning && (value==FacialMood.Excited || value==FacialMood.Happy));
            mood=value;
            moodTarget=FacialMoodShape.Lerp(FacialMoodShape.Neutral,FacialMoodShape.For(value,leftLids.Length>0),Mathf.Clamp01(weight));
            moodBlendSeconds=Mathf.Max(.01f,blendSeconds);
            if(cut) moodCurrent=moodTarget;
        }
        // Contact owners observe the completed facial pose, including eyes and lids.
        public event Action AfterEvaluation;
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
            ConfigureMood(value);
            random=new System.Random(GetInstanceID()); clock=0; nextBlink=2.2+random.NextDouble()*1.8;
            microRandom=new System.Random(GetInstanceID() ^ 0x31A7); nextMicro=.7+microRandom.NextDouble()*.7;
            microYaw=microPitch=0;
            configured=true; return true;
        }
        private void ConfigureMood(Bindings value)
        {
            mood=FacialMood.Neutral; moodTarget=moodCurrent=FacialMoodShape.Neutral; dizzyPhase=0;
            leftBrow=rightBrow=jaw=leftPupil=rightPupil=leftEyeball=rightEyeball=null; eyeUpAxis=eyeLookAxis=-1;
            var head=value.Head;
            Vector3 headForward=head.TransformDirection(value.HeadForward).normalized;
            Vector3 down=-head.TransformDirection(value.HeadUp).normalized;
            SetupLids(leftLids,value.LeftEye,value.RightEye,headForward,down);
            SetupLids(rightLids,value.RightEye,value.LeftEye,headForward,down);
            // Human brows and jaw are ordinary head children; optional, found by their rig names.
            var browLeft=FindUnder(head,"Brow.L"); var browRight=FindUnder(head,"Brow.R");
            if(browLeft && browRight && browLeft!=browRight)
            {
                leftBrow=new MoodBone{bone=browLeft}; rightBrow=new MoodBone{bone=browRight};
                leftBrowSign=InnerDownSign(headForward,browRight.position-browLeft.position,down);
                rightBrowSign=InnerDownSign(headForward,browLeft.position-browRight.position,down);
            }
            var jawBone=FindUnder(head,"Jaw"); if(jawBone) jaw=new MoodBone{bone=jawBone};
            smileShape=mouthOShape=frownShape=-1;
            if(value.Eyelids && value.Eyelids.sharedMesh && leftLids.Length==0)
            {
                smileShape=value.Eyelids.sharedMesh.GetBlendShapeIndex("Smile");
                mouthOShape=value.Eyelids.sharedMesh.GetBlendShapeIndex("MouthO");
                frownShape=value.Eyelids.sharedMesh.GetBlendShapeIndex("Frown");
            }
            // Mosquito pupils are separate discs on their own bones: scale them across the look axis.
            if(leftLids.Length>0 && value.LeftEye && value.RightEye && LookAxisIndex(value.EyeForward)>=0)
            { leftPupil=new MoodBone{bone=value.LeftEye,scaleOnly=true}; rightPupil=new MoodBone{bone=value.RightEye,scaleOnly=true}; }
            // Human pupils are decals on the Eye bones in front of static head-weighted globes: size them across the
            // look axis only (a depth change would sink them into the white) and flatten them vertically to squint.
            else if(leftLids.Length==0 && value.LeftEye && value.RightEye && LookAxisIndex(value.EyeUp)>=0 &&
                LookAxisIndex(value.EyeForward)>=0 && LookAxisIndex(value.EyeUp)!=LookAxisIndex(value.EyeForward))
            {
                leftEyeball=new MoodBone{bone=value.LeftEye,scaleOnly=true}; rightEyeball=new MoodBone{bone=value.RightEye,scaleOnly=true};
                eyeUpAxis=LookAxisIndex(value.EyeUp); eyeLookAxis=LookAxisIndex(value.EyeForward);
            }
        }
        private static void SetupLids(LidState[] lids,Transform eye,Transform otherEye,Vector3 headForward,Vector3 down)
        {
            foreach(var lid in lids)
            {
                var bone=lid.binding.Bone;
                lid.lower=bone.name.IndexOf("Lower",StringComparison.OrdinalIgnoreCase)>=0;
                // The certified head frame (not each pupil's own axes) keeps the two tilts mirror images.
                Vector3 forward=headForward;
                lid.tiltAxis=bone.InverseTransformDirection(forward).normalized;
                Vector3 inner=eye && otherEye ? otherEye.position-eye.position : Vector3.zero;
                lid.tiltSign=InnerDownSign(forward,inner,down);
            }
        }
        // Positive rotation about forward moves a point on the inner side by forward x inner.
        private static float InnerDownSign(Vector3 forward,Vector3 inner,Vector3 down)
        {
            if(inner.sqrMagnitude<1e-10f) return 0;
            return Vector3.Dot(Vector3.Cross(forward,inner.normalized),down)>=0 ? 1 : -1;
        }
        private static int LookAxisIndex(Vector3 axis)
        {
            axis=axis.normalized;
            for(int i=0;i<3;i++) if(Mathf.Abs(axis[i])>.98f) return i;
            return -1;
        }
        private static Transform FindUnder(Transform root,string name)
        {
            for(int i=0;i<root.childCount;i++)
            {
                var child=root.GetChild(i);
                if(child.name==name) return child;
                var found=FindUnder(child,name);
                if(found) return found;
            }
            return null;
        }
        private void ApplyMoodBones(Vector3 headForward,Vector3 headUp)
        {
            var shape=moodCurrent;
            if(leftBrow!=null && rightBrow!=null && (Mathf.Abs(shape.BrowLift)>1e-5f || Mathf.Abs(shape.BrowTilt)>.01f))
            {
                ApplyBrow(leftBrow,leftBrowSign,shape,headForward,headUp);
                ApplyBrow(rightBrow,rightBrowSign,shape,headForward,headUp);
            }
            if(jaw!=null && jaw.bone && Mathf.Abs(shape.Jaw)>.01f)
            {
                jaw.Begin();
                // Rotating about up x forward moves the chin (forward of the pivot) down: the mouth opens.
                jaw.bone.rotation=Quaternion.AngleAxis(shape.Jaw,Vector3.Cross(headUp,headForward).normalized)*jaw.bone.rotation;
                jaw.End();
            }
            if(leftPupil!=null && rightPupil!=null && Mathf.Abs(shape.Pupil-1)>.001f)
            {
                int axis=LookAxisIndex(binding.EyeForward);
                var scale=Vector3.one*Mathf.Clamp(shape.Pupil,.5f,1.5f); scale[axis]=1;
                ScalePupil(leftPupil,scale); ScalePupil(rightPupil,scale);
            }
            if(leftEyeball!=null && rightEyeball!=null && eyeUpAxis>=0 && eyeLookAxis>=0)
            {
                ScalePupil(leftEyeball,HumanPupilScale(shape,leftClosure));
                ScalePupil(rightEyeball,HumanPupilScale(shape,rightClosure));
            }
        }
        /// <summary>Human decal pupils: size across the look axis, the squint vertically, and the look-axis lift
        /// that keeps the whole decal outside the faceted globe while the lid is open.</summary>
        private Vector3 HumanPupilScale(FacialMoodShape shape,float closure)
        {
            float size=Mathf.Clamp(shape.Pupil,HumanPupilMin,HumanPupilMax);
            var scale=Vector3.one*size;
            scale[eyeUpAxis]*=1-.5f*Mathf.Clamp(shape.Squint,0,HumanSquintMax);
            float narrowest=Mathf.Min(1,Mathf.Min(scale[eyeUpAxis],size));
            // A decal edge at 16 deg narrowed by k sits at r*sqrt(k^2 sin^2 + d^2 cos^2): d restores >= r.
            float needed=Mathf.Sqrt(Mathf.Max(1,(1-narrowest*narrowest*PupilEdgeSin2)/PupilEdgeCos2));
            float open=1-Mathf.Clamp01(closure);
            scale[eyeLookAxis]=1+(needed-1+HumanPupilCreaseLift)*open;
            LastPupilLift=scale[eyeLookAxis];
            return scale;
        }
        private float leftClosure,rightClosure;
        /// <summary>Largest upper-lid closure (blink or mood) written on the last evaluation, 0..1.</summary>
        public float LastLidClosure=>Mathf.Max(leftClosure,rightClosure);
        private void ApplyMouthShapes()
        {
            if(!binding.Eyelids || !SupportsMouthShapes) return;
            var renderer=binding.Eyelids;
            oldSmile=renderer.GetBlendShapeWeight(smileShape); oldMouthO=renderer.GetBlendShapeWeight(mouthOShape); oldFrown=renderer.GetBlendShapeWeight(frownShape);
            lastSmile=100*Mathf.Clamp01(moodCurrent.Smile); lastMouthO=100*Mathf.Clamp01(moodCurrent.MouthOpen); lastFrown=100*Mathf.Clamp01(moodCurrent.Frown);
            renderer.SetBlendShapeWeight(smileShape,lastSmile); renderer.SetBlendShapeWeight(mouthOShape,lastMouthO); renderer.SetBlendShapeWeight(frownShape,lastFrown);
            mouthWritten=true;
        }
        private static void ApplyBrow(MoodBone brow,float sign,FacialMoodShape shape,Vector3 headForward,Vector3 headUp)
        {
            if(!brow.bone) return;
            brow.Begin();
            brow.bone.position+=headUp*shape.BrowLift;
            brow.bone.rotation=Quaternion.AngleAxis(sign*shape.BrowTilt,headForward)*brow.bone.rotation;
            brow.End();
        }
        private static void ScalePupil(MoodBone pupil,Vector3 scale)
        {
            if(!pupil.bone) return;
            pupil.Begin();
            pupil.bone.localScale=Vector3.Scale(pupil.beforeScale,scale);
            pupil.End();
        }
        /// <summary>Mood head pose (chin down/up, tilt, dizzy sway) on top of the clamped look-at; recorded as the
        /// head joint's own write so it is restored with it. Skipped while head tracking is locked (bite).</summary>
        private void ApplyMoodHead()
        {
            LastMoodHeadDegrees=0;
            if(!MoodHeadPoseEnabled || head==null || !head.bone || !head.written) return;
            float pitch=moodCurrent.HeadPitch, roll=moodCurrent.HeadRoll+(reduced ? 0 : 6*moodCurrent.Dizzy*Mathf.Sin(dizzyPhase));
            if(Mathf.Abs(pitch)<.01f && Mathf.Abs(roll)<.01f) return;
            Vector3 forward=head.bone.TransformDirection(head.forward), up=head.bone.TransformDirection(head.up);
            Vector3 right=Vector3.Cross(up,forward);
            if(right.sqrMagnitude<1e-8f || forward.sqrMagnitude<1e-8f) return;
            head.bone.rotation=Quaternion.AngleAxis(roll,forward.normalized)*Quaternion.AngleAxis(pitch,right.normalized)*head.bone.rotation;
            head.after=head.bone.localRotation;
            LastMoodHeadDegrees=Mathf.Sqrt(pitch*pitch+roll*roll);
        }
        /// <summary>Blend-shape lids (human) only show below the brow line past ~40% closure: map mood lids so a
        /// light squint is visible, while 0 stays fully open and 1 fully closed.</summary>
        private float MoodUpperLid(float value)
        {
            value=Mathf.Clamp01(value);
            if(leftLids.Length>0) return value;
            return value<.08f ? 0 : Mathf.Lerp(.42f,1f,(value-.08f)/.92f);
        }
        private static bool Axes(Vector3 forward,Vector3 up)=>forward.sqrMagnitude>.5f && up.sqrMagnitude>.5f && Vector3.Cross(forward,up).sqrMagnitude>.1f;
        private static Joint Make(Transform bone,Vector3 forward,Vector3 up,float yaw,float pitch)=>new Joint
        {bone=bone,forward=forward.normalized,up=up.normalized,yawLimit=yaw,pitchLimit=pitch};
        public void SetLookTarget(Transform value) { target=value; hasPoint=false; }
        public void SetLookPoint(Vector3 value) { target=null; point=value; hasPoint=true; }
        public void ClearLookTarget() { target=null; hasPoint=false; }
        public void SetHeadTrackingEnabled(bool enabled)
        {
            if(headTrackingEnabled==enabled) return;
            headTrackingEnabled=enabled;
            if(!enabled)
            {
                // The mouth constraint needs an immediate lock. Release starts from zero
                // and uses the existing exponential tracking response; eyes/blink stay live.
                head?.Restore(); neck?.Restore();
                if(head!=null) head.yaw=head.pitch=0;
                if(neck!=null) neck.yaw=neck.pitch=0;
            }
        }
        public void SetReducedMotion(bool value)
        {
            reduced=value;
            if(value)
            {
                Restore(); microYaw=microPitch=0; nextMicro=clock+.8;
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
            if(head==null || !head.bone) { Restore(); configured=false; return; }
            float dt=Mathf.Max(0,deltaSeconds);
            bool looking=!reduced && (target || hasPoint);
            Vector3 destination=target ? target.position : point;
            // Head limits are a TOTAL correction budget over this frame's authored pose,
            // not another allowance on top of the neck's correction.
            Quaternion headBaseRotation=head.bone.rotation;
            Quaternion headBaseFrame=Quaternion.LookRotation(head.bone.TransformDirection(head.forward),head.bone.TransformDirection(head.up));
            if(headTrackingEnabled)
            {
                Apply(neck,destination,looking,dt); Apply(head,destination,looking,dt);
                ClampHeadCorrection(head,headBaseFrame,headBaseRotation);
                ApplyMoodHead();
            }
            if(!reduced && clock>=nextMicro)
            {
                // Shared tiny target offset avoids divergent eyes; Apply supplies the existing smooth response.
                microYaw=(float)(microRandom.NextDouble()*2-1)*1.1f;
                microPitch=(float)(microRandom.NextDouble()*2-1)*.65f;
                nextMicro=clock+1.1+microRandom.NextDouble()*1.3;
            }
            float eyeYaw=reduced ? 0 : microYaw, eyePitch=reduced ? 0 : microPitch;
            moodCurrent=FacialMoodShape.Lerp(moodCurrent,moodTarget,1-Mathf.Exp(-dt/moodBlendSeconds));
            if(!moodCurrent.IsFinite) moodCurrent=FacialMoodShape.Neutral;
            // Dizzy: the two pupils circle in opposite directions (cartoon "stars" look), 1.3 turns per second.
            dizzyPhase=(dizzyPhase+dt*1.3f*2*Mathf.PI)%(2*Mathf.PI);
            float dizzyYaw=(reduced ? 6 : 12)*moodCurrent.Dizzy*Mathf.Cos(dizzyPhase), dizzyPitch=(reduced ? 6 : 12)*moodCurrent.Dizzy*Mathf.Sin(dizzyPhase);
            Apply(left,destination,looking,dt,eyeYaw+dizzyYaw,eyePitch+dizzyPitch); Apply(right,destination,looking,dt,eyeYaw-dizzyYaw,eyePitch-dizzyPitch);
            clock+=dt;
            if(clock>nextBlink+.25) nextBlink=clock+3.2+random.NextDouble()*2.6;
            float closureLeft=Blink((float)(clock-nextBlink))*.01f;
            float closureRight=Blink((float)(clock-nextBlink)-.012f)*.01f;
            // Director r4 (9): a cheering (Excited) face keeps both eyes wide open; no spontaneous blink catches
            // one lid half closed in the victory loop.
            if(mood==FacialMood.Excited) closureLeft=closureRight=0;
            float blinkLeft=closureLeft,blinkRight=closureRight;
            float moodUpper=MoodUpperLid(moodCurrent.Upper);
            closureLeft=Mathf.Max(closureLeft,moodUpper);
            closureRight=Mathf.Max(closureRight,moodUpper);
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
            float lower=Mathf.Clamp(moodCurrent.Lower,0,FacialMoodShape.MaximumLowerLid);
            foreach(var lid in leftLids) lid.Apply(lid.lower ? Mathf.Max(blinkLeft,lower) : closureLeft,moodCurrent.Tilt);
            foreach(var lid in rightLids) lid.Apply(lid.lower ? Mathf.Max(blinkRight,lower) : closureRight,moodCurrent.Tilt);
            leftClosure=closureLeft; rightClosure=closureRight;
            ApplyMouthShapes();
            ApplyMoodBones(binding.Head.TransformDirection(binding.HeadForward).normalized,binding.Head.TransformDirection(binding.HeadUp).normalized);
            AfterEvaluation?.Invoke();
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
        }
        private static void Apply(Joint joint,Vector3 destination,bool looking,float dt,float eyeYawOffset=0,float eyePitchOffset=0)
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
            yaw=Mathf.Clamp(yaw+eyeYawOffset,-joint.yawLimit,joint.yawLimit);
            pitch=Mathf.Clamp(pitch+eyePitchOffset,-joint.pitchLimit,joint.pitchLimit);
            float blend=1-Mathf.Exp(-10*dt);
            joint.yaw=Mathf.LerpAngle(joint.yaw,yaw,blend); joint.pitch=Mathf.LerpAngle(joint.pitch,pitch,blend);
            joint.before=bone.localRotation;
            bone.rotation=basis*Quaternion.Euler(joint.pitch,joint.yaw,0)*Quaternion.Inverse(basis)*bone.rotation;
            joint.after=bone.localRotation; joint.written=true;
        }
        private static void ClampHeadCorrection(Joint joint,Quaternion baseFrame,Quaternion baseRotation)
        {
            Vector3 direction=Quaternion.Inverse(baseFrame)*joint.bone.TransformDirection(joint.forward);
            float yaw=Mathf.Atan2(direction.x,direction.z)*Mathf.Rad2Deg;
            float pitch=-Mathf.Atan2(direction.y,Mathf.Sqrt(direction.x*direction.x+direction.z*direction.z))*Mathf.Rad2Deg;
            float boundedYaw=Mathf.Clamp(yaw,-joint.yawLimit,joint.yawLimit);
            float boundedPitch=Mathf.Clamp(pitch,-joint.pitchLimit,joint.pitchLimit);
            if(Mathf.Abs(yaw-boundedYaw)>.0001f || Mathf.Abs(pitch-boundedPitch)>.0001f)
                joint.bone.rotation=baseFrame*Quaternion.Euler(boundedPitch,boundedYaw,0)*Quaternion.Inverse(baseFrame)*baseRotation;
            // Restore must recognize the final written local rotation, including this combined clamp.
            joint.after=joint.bone.localRotation;
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
            if(mouthWritten && binding!=null && binding.Eyelids && SupportsMouthShapes)
            {
                var renderer=binding.Eyelids;
                if(Mathf.Abs(renderer.GetBlendShapeWeight(smileShape)-lastSmile)<.01f) renderer.SetBlendShapeWeight(smileShape,oldSmile);
                if(Mathf.Abs(renderer.GetBlendShapeWeight(mouthOShape)-lastMouthO)<.01f) renderer.SetBlendShapeWeight(mouthOShape,oldMouthO);
                if(Mathf.Abs(renderer.GetBlendShapeWeight(frownShape)-lastFrown)<.01f) renderer.SetBlendShapeWeight(frownShape,oldFrown);
            }
            mouthWritten=false;
            leftPupil?.Restore(); rightPupil?.Restore(); leftEyeball?.Restore(); rightEyeball?.Restore(); jaw?.Restore(); rightBrow?.Restore(); leftBrow?.Restore();
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
