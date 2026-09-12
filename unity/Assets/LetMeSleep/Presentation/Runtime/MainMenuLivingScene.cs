using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace LetMeSleep.Presentation
{
    /// <summary>Owns only explicitly supplied decorative children. Never bind a gameplay actor.</summary>
    [DisallowMultipleComponent]
    public sealed class MainMenuLivingScene : MonoBehaviour
    {
        [Serializable]
        public sealed class Bindings
        {
            public Transform HumanRoot, MosquitoRoot, HumanSeatRoot;
            public Animator HumanAnimator, MosquitoAnimator;
            public AnimationClip MenuSeatedIdle, MenuLook, MenuSwat, MenuReturn, Flight;
            // Ordered control points. The curve stays inside successive control-point triangles.
            // Passage occurs halfway between the midpoints adjoining point zero.
            public Transform[] FlightPoints;
            public Transform WarmLightAnchor, CoolLightAnchor;
            public float CycleSeconds = 24f;
            [Range(0, 1)] public float SwatContactNormalized = .5f;
            public Vector3 MosquitoRotationOffset;
        }

        private Bindings bindings;
        private PlayableGraph graph;
        private AnimationMixerPlayable humanMixer;
        private AnimationClipPlayable idle, look, swat, returning, flight;
        private bool configured, requestedActive = true, reducedMotion, running;
        private double elapsed;
        private bool skipInterruptedSequence;
        private Light warm, cool;
        private Vector3 humanPosition, mosquitoPosition;
        private Quaternion humanRotation, mosquitoRotation;
        private AnimatorState humanState, mosquitoState;
        public bool IsConfigured => configured;
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
                !ValidClip(value.MenuSeatedIdle) || !ValidClip(value.MenuSwat) || !ValidClip(value.MenuReturn) || !ValidClip(value.MenuLook) || !ValidClip(value.Flight) ||
                (value.MenuLook && !ValidClip(value.MenuLook)) ||
                value.FlightPoints == null || value.FlightPoints.Length < 4 ||
                float.IsNaN(value.CycleSeconds) || float.IsInfinity(value.CycleSeconds) ||
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
                MenuSeatedIdle=value.MenuSeatedIdle, MenuLook=value.MenuLook, MenuSwat=value.MenuSwat, MenuReturn=value.MenuReturn,
                Flight=value.Flight, FlightPoints=(Transform[])value.FlightPoints.Clone(),
                WarmLightAnchor=value.WarmLightAnchor, CoolLightAnchor=value.CoolLightAnchor,
                CycleSeconds=Mathf.Max(24f,value.CycleSeconds, (value.MenuSwat.length+value.MenuReturn.length+1f)/.4f, (value.MenuLook.length+1f)/.6f),
                SwatContactNormalized=Mathf.Clamp01(value.SwatContactNormalized),
                MosquitoRotationOffset=value.MosquitoRotationOffset
            };
            elapsed=bindings.CycleSeconds*.60f+bindings.MenuSwat.length+bindings.MenuReturn.length;
            configured = true;
            if (isActiveAndEnabled && requestedActive) Begin();
            return configured;
        }

        private bool Owned(Transform actor) => actor && actor != transform && actor.IsChildOf(transform);
        private static bool ValidAnimator(Animator animator, Transform root) => animator &&
            (animator.transform == root || animator.transform.IsChildOf(root));
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
            if (!enabled) skipInterruptedSequence = true;
            reducedMotion = enabled;
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
                warm=CreateLight("Menu warm seat light",bindings.WarmLightAnchor,new Color(1f,.72f,.48f),.7f,3.4f);
                cool=CreateLight("Menu cool fill",bindings.CoolLightAnchor,new Color(.55f,.68f,1f),.25f,4.5f);
                graph.Play(); Sample();
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
            if (!reducedMotion) elapsed += Time.unscaledDeltaTime;
            Sample();
        }
        private void Sample()
        {
            float cycle=bindings.CycleSeconds;
            float time=(float)(elapsed % cycle);
            float reactionStart=cycle*.60f;
            float reactionTime=time-reactionStart;
            float lookStart=reactionStart-(bindings.MenuLook ? bindings.MenuLook.length : 0);
            float lookTime=time-lookStart;
            float sequenceDuration=bindings.MenuLook.length+bindings.MenuSwat.length+bindings.MenuReturn.length;
            if (!reducedMotion && (lookTime <= 0 || lookTime >= sequenceDuration)) skipInterruptedSequence=false;
            float sequenceWeight=reducedMotion || skipInterruptedSequence ? 0 : Envelope(lookTime,sequenceDuration);
            float lookWeight=reactionTime < 0 ? sequenceWeight : 0;
            float swatWeight=reactionTime >= 0 && reactionTime < bindings.MenuSwat.length ? sequenceWeight : 0;
            float returnWeight=reactionTime >= bindings.MenuSwat.length ? sequenceWeight : 0;
            humanMixer.SetInputWeight(0,1-sequenceWeight);
            humanMixer.SetInputWeight(1,lookWeight); humanMixer.SetInputWeight(2,swatWeight);
            humanMixer.SetInputWeight(3,returnWeight);
            // Fill the rest interval with complete idle loops, so both sequence boundaries meet idle phase zero.
            float restDuration=cycle-sequenceDuration;
            float restTime=Mathf.Repeat(time-(reactionStart+bindings.MenuSwat.length+bindings.MenuReturn.length),cycle);
            float idleLoops=Mathf.Max(1,Mathf.Round(restDuration/bindings.MenuSeatedIdle.length));
            idle.SetTime(reducedMotion || skipInterruptedSequence || sequenceWeight>0 ? 0 : Mathf.Repeat(restTime/restDuration*idleLoops,1)*bindings.MenuSeatedIdle.length);
            swat.SetTime(Mathf.Clamp(reactionTime,0,bindings.MenuSwat.length));
            returning.SetTime(Mathf.Clamp(reactionTime-bindings.MenuSwat.length,0,bindings.MenuReturn.length));
            look.SetTime(Mathf.Clamp(lookTime,0,bindings.MenuLook.length));
            // Reduced motion holds an authored seated pose and the authored flight pose, no orbit or flapping.
            flight.SetTime(reducedMotion ? 0 : elapsed % bindings.Flight.length);
            bindings.HumanRoot.SetPositionAndRotation(bindings.HumanSeatRoot.position,bindings.HumanSeatRoot.rotation);
            float contact=reactionStart+bindings.MenuSwat.length*bindings.SwatContactNormalized;
            float phase=Mathf.Repeat((time-contact)/cycle+.5f/bindings.FlightPoints.Length,1);
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
            graph.Evaluate(0);
        }
        private static float Envelope(float time,float duration)
        {
            if(time<=0 || time>=duration) return 0;
            float fade=Mathf.Min(.18f,duration*.2f);
            return Mathf.SmoothStep(0,1,Mathf.Min(time/fade,(duration-time)/fade));
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
            if(graph.IsValid()) graph.Destroy();
            humanState.Restore(bindings.HumanAnimator); mosquitoState.Restore(bindings.MosquitoAnimator);
            if(bindings.HumanRoot) { bindings.HumanRoot.localPosition=humanPosition; bindings.HumanRoot.localRotation=humanRotation; }
            if(bindings.MosquitoRoot) { bindings.MosquitoRoot.localPosition=mosquitoPosition; bindings.MosquitoRoot.localRotation=mosquitoRotation; }
            if(warm) { warm.enabled=false; Destroy(warm.gameObject); }
            if(cool) { cool.enabled=false; Destroy(cool.gameObject); }
            warm=null; cool=null;
        }
        private void Release() { End(); configured=false; bindings=null; elapsed=0; skipInterruptedSequence=false; }
    }
}
