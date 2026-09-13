using System;
using HumanBeat = LetMeSleep.Presentation.MenuReactionPolicy.Beat;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace LetMeSleep.Presentation
{
    /// <summary>Owns only explicitly supplied decorative children. Never bind a gameplay actor.</summary>
    [DefaultExecutionOrder(900)]
    [DisallowMultipleComponent]
    public sealed class MainMenuLivingScene : MonoBehaviour
    {
        [Serializable]
        public sealed class Bindings
        {
            public Transform HumanRoot, MosquitoRoot, HumanSeatRoot;
            public Animator HumanAnimator, MosquitoAnimator;
            public VisualAttentionRig HumanAttention, MosquitoAttention;
            public AnimationClip MenuSeatedIdle, MenuLook, MenuSwat, MenuReturn, Flight;
            // Ordered control points. The curve stays inside successive control-point triangles.
            // Passage occurs halfway between the midpoints adjoining point zero.
            public Transform[] FlightPoints;
            public Transform WarmLightAnchor, CoolLightAnchor;
            public float CycleSeconds = 12f;
            public float FirstLookAfterSeconds = 1.2f;
            public Transform HumanReactionAnchor;
            public Vector3 HumanReactionOffset = new Vector3(.4f,1.25f,.78f);
            public float NoticeRadius = 1.8f, SwatRadius = .7f;
            public float LookSeconds = 2f, SwatSeconds = 1.2f, ReturnSeconds = 1.6f;
            [Range(0, 1)] public float SwatContactNormalized = .5f;
            public Vector3 MosquitoRotationOffset;
        }

        private Bindings bindings;
        private PlayableGraph graph;
        private AnimationMixerPlayable humanMixer;
        private AnimationClipPlayable idle, look, swat, returning, flight;
        private bool configured, requestedActive = true, reducedMotion, running;
        private double elapsed;
        private int lastSampleFrame=-1;
        private HumanBeat beat;
        private float beatTime,settleLookTime;
        public string CurrentBeat => beat.ToString();
        public double FlightClipTime => flight.IsValid() ? flight.GetTime() : 0;
        private Light warm, cool;
        private Vector3 humanPosition, mosquitoPosition;
        private Quaternion humanRotation, mosquitoRotation;
        private AnimatorState humanState, mosquitoState;
        public bool IsConfigured => configured;
        public bool ReducedMotion => reducedMotion;
        public bool IsRunning => running;

        private struct AnimatorState
        {
            public bool enabled, rootMotion;
            public AnimatorCullingMode culling;
            public AnimatorUpdateMode update;
            public float speed;
            public static AnimatorState Capture(Animator animator) => new AnimatorState
            { enabled = animator.enabled, rootMotion = animator.applyRootMotion,
              culling = animator.cullingMode, update = animator.updateMode, speed = animator.speed };
            public void Restore(Animator animator)
            {
                if (!animator) return;
                animator.applyRootMotion = rootMotion; animator.cullingMode = culling;
                animator.updateMode = update; animator.speed = speed; animator.enabled = enabled;
            }
        }

        public bool Configure(Bindings value)
        {
            Release();
            if (value == null || !Owned(value.HumanRoot) || !Owned(value.MosquitoRoot) ||
                value.HumanRoot == value.MosquitoRoot || value.HumanRoot.IsChildOf(value.MosquitoRoot) ||
                value.MosquitoRoot.IsChildOf(value.HumanRoot) || !value.HumanSeatRoot ||
                value.HumanSeatRoot.IsChildOf(value.HumanRoot) ||
                !ValidAnimator(value.HumanAnimator, value.HumanRoot) ||
                !ValidAnimator(value.MosquitoAnimator, value.MosquitoRoot) ||
                !ValidAttention(value.HumanAttention,value.HumanRoot) || !ValidAttention(value.MosquitoAttention,value.MosquitoRoot) ||
                !ValidClip(value.MenuSeatedIdle) || !ValidClip(value.MenuSwat) || !ValidClip(value.MenuReturn) || !ValidClip(value.MenuLook) || !ValidClip(value.Flight) ||
                (value.MenuLook && !ValidClip(value.MenuLook)) ||
                value.FlightPoints == null || value.FlightPoints.Length < 4 ||
                !FinitePositive(value.CycleSeconds) || !FinitePositive(value.FirstLookAfterSeconds) ||
                !FinitePositive(value.LookSeconds) || !FinitePositive(value.SwatSeconds) || !FinitePositive(value.ReturnSeconds) ||
                !FinitePositive(value.NoticeRadius) || !FinitePositive(value.SwatRadius) ||
                float.IsNaN(value.SwatContactNormalized) || float.IsInfinity(value.SwatContactNormalized))
            {
                Debug.LogError("MainMenuLivingScene requires decorative children, seated clips and at least four flight anchors.", this);
                return false;
            }
            foreach (var point in value.FlightPoints)
                if (!point || point.IsChildOf(value.MosquitoRoot) || point.IsChildOf(value.HumanRoot)) return false;
            // Copy mutable configuration so a caller cannot replace references during playback.
            bindings = new Bindings
            {
                HumanRoot=value.HumanRoot, MosquitoRoot=value.MosquitoRoot, HumanSeatRoot=value.HumanSeatRoot,
                HumanAnimator=value.HumanAnimator, MosquitoAnimator=value.MosquitoAnimator,
                HumanAttention=value.HumanAttention, MosquitoAttention=value.MosquitoAttention,
                MenuSeatedIdle=value.MenuSeatedIdle, MenuLook=value.MenuLook, MenuSwat=value.MenuSwat, MenuReturn=value.MenuReturn,
                Flight=value.Flight, FlightPoints=(Transform[])value.FlightPoints.Clone(),
                WarmLightAnchor=value.WarmLightAnchor, CoolLightAnchor=value.CoolLightAnchor,
                CycleSeconds=Mathf.Max(value.CycleSeconds,value.FirstLookAfterSeconds+value.LookSeconds+value.SwatSeconds+value.ReturnSeconds+2f),
                FirstLookAfterSeconds=value.FirstLookAfterSeconds, LookSeconds=value.LookSeconds,
                SwatSeconds=value.SwatSeconds, ReturnSeconds=value.ReturnSeconds,
                HumanReactionAnchor=value.HumanReactionAnchor, HumanReactionOffset=value.HumanReactionOffset,
                NoticeRadius=value.NoticeRadius, SwatRadius=value.SwatRadius,
                SwatContactNormalized=Mathf.Clamp01(value.SwatContactNormalized),
                MosquitoRotationOffset=value.MosquitoRotationOffset
            };
            elapsed=0;
            configured = true;
            if (isActiveAndEnabled && requestedActive) Begin();
            return configured;
        }

        private bool Owned(Transform actor) => actor && actor != transform && actor.IsChildOf(transform);
        private static bool ValidAnimator(Animator animator, Transform root) => animator &&
            (animator.transform == root || animator.transform.IsChildOf(root));
        private static bool ValidAttention(VisualAttentionRig attention,Transform root) => !attention ||
            (attention.IsConfigured && attention.IsManualEvaluation && (attention.transform==root || attention.transform.IsChildOf(root)));
        private static bool FinitePositive(float value) => value > 0 && !float.IsInfinity(value) && !float.IsNaN(value);
        private static bool ValidClip(AnimationClip clip) => clip && !clip.legacy && clip.length > 0;
        public void SetSceneActive(bool active)
        {
            requestedActive = active;
            gameObject.SetActive(active);
            if (active && configured && isActiveAndEnabled) Begin(); else End();
        }
        public void SetReducedMotion(bool enabled)
        {
            if (reducedMotion == enabled) return;
            // Returning from a held seated pose must not resume halfway through a swat.
            beat=HumanBeat.Idle; beatTime=0;
            reducedMotion = enabled;
            if (bindings?.HumanAttention) bindings.HumanAttention.SetReducedMotion(enabled);
            if (bindings?.MosquitoAttention) bindings.MosquitoAttention.SetReducedMotion(enabled);
        }
        private void OnEnable() { if (configured && requestedActive) Begin(); }
        private void OnDisable() => End();
        private void OnDestroy() => Release();

        private void Begin()
        {
            if (running || !ReferencesAlive()) return;
            humanPosition=bindings.HumanRoot.localPosition; humanRotation=bindings.HumanRoot.localRotation;
            mosquitoPosition=bindings.MosquitoRoot.localPosition; mosquitoRotation=bindings.MosquitoRoot.localRotation;
            humanState=AnimatorState.Capture(bindings.HumanAnimator);
            mosquitoState=AnimatorState.Capture(bindings.MosquitoAnimator);
            running=true;
            try
            {
                Prepare(bindings.HumanAnimator); Prepare(bindings.MosquitoAnimator);
                graph=PlayableGraph.Create("MainMenuLivingScene");
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                humanMixer=AnimationMixerPlayable.Create(graph,4);
                idle=Clip(bindings.MenuSeatedIdle); swat=Clip(bindings.MenuSwat); returning=Clip(bindings.MenuReturn);
                graph.Connect(idle,0,humanMixer,0); graph.Connect(swat,0,humanMixer,2); graph.Connect(returning,0,humanMixer,3);
                if (bindings.MenuLook) { look=Clip(bindings.MenuLook); graph.Connect(look,0,humanMixer,1); }
                AnimationPlayableOutput.Create(graph,"Seated human",bindings.HumanAnimator).SetSourcePlayable(humanMixer);
                flight=Clip(bindings.Flight);
                AnimationPlayableOutput.Create(graph,"Flying mosquito",bindings.MosquitoAnimator).SetSourcePlayable(flight);
                warm=CreateLight("Menu warm seat light",bindings.WarmLightAnchor,new Color(1f,.76f,.53f),2.2f,4.3f);
                cool=CreateLight("Menu cool fill",bindings.CoolLightAnchor,new Color(.58f,.72f,1f),.45f,3.8f);
                if (warm)
                {
                    // A single local cone models the sitter and seat rather than washing the whole wall.
                    warm.type=LightType.Spot; warm.spotAngle=100f; warm.innerSpotAngle=70f;
                    // The authored anchor is almost lateral to the face; bring the presentation key toward its front.
                    warm.transform.position+=bindings.HumanSeatRoot.forward*.85f;
                    warm.transform.LookAt(bindings.HumanSeatRoot.TransformPoint(new Vector3(0,1.15f,.35f)));
                    warm.shadows=LightShadows.Soft;
                    // URP uses the pipeline additional-light shadow resolution tier; do not set the Built-in-only property.
                    warm.shadowStrength=.8f; warm.shadowBias=.025f; warm.shadowNormalBias=.08f;
                    warm.shadowNearPlane=.05f;
                }
                if(bindings.HumanAttention) bindings.HumanAttention.SetReducedMotion(reducedMotion);
                if(bindings.MosquitoAttention) bindings.MosquitoAttention.SetReducedMotion(reducedMotion);
                lastSampleFrame=-1;
                graph.Play(); // The first pose is sampled in LateUpdate, after Unity animation and before CharacterView anchors.
                }
            catch (Exception exception)
            {
                End(); configured=false;
                Debug.LogException(exception,this);
            }
        }
        private static void Prepare(Animator animator)
        {
            animator.enabled=true; animator.applyRootMotion=false; animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            animator.updateMode=AnimatorUpdateMode.UnscaledTime; animator.speed=1;
        }
        private AnimationClipPlayable Clip(AnimationClip clip)
        {
            var playable=AnimationClipPlayable.Create(graph,clip);
            playable.SetApplyFootIK(false); playable.SetApplyPlayableIK(false); playable.SetSpeed(0);
            return playable;
        }
        private Light CreateLight(string label,Transform anchor,Color color,float intensity,float range)
        {
            if (!anchor) return null;
            var item=new GameObject(label); item.transform.SetParent(transform,false);
            item.transform.SetPositionAndRotation(anchor.position,anchor.rotation);
            var light=item.AddComponent<Light>(); light.type=LightType.Point; light.color=color;
            light.intensity=intensity; light.range=range; light.shadows=LightShadows.None;
            light.renderMode=LightRenderMode.ForcePixel;
            return light;
        }
        private bool ReferencesAlive()
        {
            if (!configured || !bindings.HumanRoot || !bindings.MosquitoRoot || !bindings.HumanSeatRoot ||
                !bindings.HumanAnimator || !bindings.MosquitoAnimator) return false;
            foreach(var point in bindings.FlightPoints) if(!point) return false;
            return true;
        }
        private void Update()
        {
            if (!running) return;
            if (!ReferencesAlive()) { End(); return; }
            // Menu clock deliberately follows unscaled time, without writing any game clock.
            if (!reducedMotion) { elapsed += Time.unscaledDeltaTime; beatTime += Time.unscaledDeltaTime; }
        }
        private void LateUpdate()
        {
            if(!running || lastSampleFrame==Time.frameCount) return;
            if(!ReferencesAlive()) { End(); return; }
            lastSampleFrame=Time.frameCount;
            Sample(Time.unscaledDeltaTime);
        }
        private void Sample(float deltaSeconds=0)
        {
            float phase=FlightPhase(elapsed);
            Vector3 currentPosition,currentTangent;
            Path(phase,out currentPosition,out currentTangent);
            Vector3 reactionPoint=bindings.HumanReactionAnchor ? bindings.HumanReactionAnchor.position :
                bindings.HumanSeatRoot.TransformPoint(bindings.HumanReactionOffset);
            float distance=Vector3.Distance(currentPosition,reactionPoint);
            Vector3 predicted,predictedTangent;
            Path(FlightPhase(elapsed+bindings.SwatSeconds*bindings.SwatContactNormalized),out predicted,out predictedTangent);
            HumanBeat next=MenuReactionPolicy.Next(beat,beatTime,bindings.FirstLookAfterSeconds,bindings.LookSeconds,
                bindings.SwatSeconds,bindings.ReturnSeconds,bindings.LookSeconds+bindings.CycleSeconds,
                distance,Vector3.Distance(predicted,reactionPoint),bindings.NoticeRadius,bindings.SwatRadius,reducedMotion);
            if (next!=beat) ChangeBeat(next);
            float lookWeight=0,swatWeight=0,returnWeight=0;
            if (!reducedMotion)
            {
                if (beat==HumanBeat.Look) lookWeight=Mathf.SmoothStep(0,1,beatTime/.18f);
                else if (beat==HumanBeat.Swat) swatWeight=1;
                else if (beat==HumanBeat.Return) returnWeight=Mathf.SmoothStep(0,1,(bindings.ReturnSeconds-beatTime)/.18f);
                else if (beat==HumanBeat.Settle) lookWeight=1-Mathf.SmoothStep(0,1,beatTime/.4f);
            }
            humanMixer.SetInputWeight(0,1-lookWeight-swatWeight-returnWeight);
            humanMixer.SetInputWeight(1,lookWeight); humanMixer.SetInputWeight(2,swatWeight); humanMixer.SetInputWeight(3,returnWeight);
            idle.SetTime(reducedMotion || beat!=HumanBeat.Idle ? 0 : beatTime % bindings.MenuSeatedIdle.length);
            look.SetTime(beat==HumanBeat.Look ? Mathf.Clamp01(beatTime/bindings.LookSeconds)*bindings.MenuLook.length :
                beat==HumanBeat.Settle ? settleLookTime : bindings.MenuLook.length);
            swat.SetTime(beat==HumanBeat.Swat ? Mathf.Clamp01(beatTime/bindings.SwatSeconds)*bindings.MenuSwat.length : 0);
            returning.SetTime(beat==HumanBeat.Return ? Mathf.Clamp01(beatTime/bindings.ReturnSeconds)*bindings.MenuReturn.length : 0);
            flight.SetTime(reducedMotion ? 0 : elapsed % bindings.Flight.length);
            bindings.HumanRoot.SetPositionAndRotation(bindings.HumanSeatRoot.position,bindings.HumanSeatRoot.rotation);
            Vector3 position,tangent;
            Path(phase,out position,out tangent);
            bindings.MosquitoRoot.position=position;
            if (tangent.sqrMagnitude>.000001f)
            {
                Vector3 ahead,aheadTangent;
                Path(Mathf.Repeat(phase+.002f,1),out ahead,out aheadTangent);
                float bank=reducedMotion ? 0 : Mathf.Clamp(-Vector3.SignedAngle(tangent,aheadTangent,Vector3.up)*2,-12,12);
                bindings.MosquitoRoot.rotation=Quaternion.LookRotation(tangent,Vector3.up)*Quaternion.Euler(0,0,bank)*Quaternion.Euler(bindings.MosquitoRotationOffset);
            }
            if (bindings.HumanAttention) bindings.HumanAttention.PrepareForAnimation();
            if (bindings.MosquitoAttention) bindings.MosquitoAttention.PrepareForAnimation();
            graph.Evaluate(0);
            if (bindings.HumanAttention)
            {
                bindings.HumanAttention.SetLookTarget(bindings.MosquitoRoot);
                bindings.HumanAttention.EvaluateAfterAnimation(deltaSeconds);
            }
            if (bindings.MosquitoAttention)
            {
                bindings.MosquitoAttention.SetLookTarget(bindings.HumanAttention ? bindings.HumanAttention.LookOrigin : bindings.HumanRoot);
                bindings.MosquitoAttention.EvaluateAfterAnimation(deltaSeconds);
            }
        }
        private void ChangeBeat(HumanBeat next)
        {
            if(next==HumanBeat.Settle) settleLookTime=Mathf.Clamp01(beatTime/bindings.LookSeconds)*bindings.MenuLook.length;
            beat=next; beatTime=0;
        }
        private float FlightPhase(double seconds)
        {
            // Initial encounter is brought forward; gestures are still gated by live spatial proximity above.
            float referencePass=bindings.FirstLookAfterSeconds+bindings.LookSeconds+bindings.SwatSeconds*bindings.SwatContactNormalized;
            float relativePhase=((float)(seconds % bindings.CycleSeconds)-referencePass)/bindings.CycleSeconds;
            float warped=relativePhase+.4f/(2*Mathf.PI)*(1-Mathf.Cos(2*Mathf.PI*relativePhase));
            return Mathf.Repeat(warped+.5f/bindings.FlightPoints.Length,1);
        }
        private void Path(float phase,out Vector3 position,out Vector3 tangent)
        {
            var points=bindings.FlightPoints; int count=points.Length;
            float step=phase*count; int index=Mathf.FloorToInt(step)%count; float t=step-Mathf.Floor(step);
            var center=points[index].position;
            var start=(points[(index+count-1)%count].position+center)*.5f;
            var end=(center+points[(index+1)%count].position)*.5f;
            position=(1-t)*(1-t)*start+2*(1-t)*t*center+t*t*end;
            tangent=2*((1-t)*(center-start)+t*(end-center));
        }
        private void End()
        {
            if (!running) return;
            running=false;
            if(bindings.HumanAttention) { bindings.HumanAttention.PrepareForAnimation(); bindings.HumanAttention.ClearLookTarget(); }
            if(bindings.MosquitoAttention) { bindings.MosquitoAttention.PrepareForAnimation(); bindings.MosquitoAttention.ClearLookTarget(); }
            if(graph.IsValid()) graph.Destroy();
            humanState.Restore(bindings.HumanAnimator); mosquitoState.Restore(bindings.MosquitoAnimator);
            if(bindings.HumanRoot) { bindings.HumanRoot.localPosition=humanPosition; bindings.HumanRoot.localRotation=humanRotation; }
            if(bindings.MosquitoRoot) { bindings.MosquitoRoot.localPosition=mosquitoPosition; bindings.MosquitoRoot.localRotation=mosquitoRotation; }
            if(warm) { warm.enabled=false; Destroy(warm.gameObject); }
            if(cool) { cool.enabled=false; Destroy(cool.gameObject); }
            warm=null; cool=null;
        }
        private void Release() { End(); configured=false; bindings=null; elapsed=0; beat=HumanBeat.Idle; beatTime=0; }
    }
}
