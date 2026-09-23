using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace LetMeSleep.Presentation.Gameplay
{
    /// <summary>Explicitly driven by the owner of the final interpolated human pose.</summary>
    [DisallowMultipleComponent]
    public sealed class HumanLocomotionPresenter : MonoBehaviour
    {
        public enum PoseOwner { Controller, Locomotion }
        [Serializable]
        public sealed class GaitClip
        {
            public AnimationClip Clip;
            public float Speed;
            public float DistancePerCycle;
        }
        public readonly struct FootContact
        {
            public readonly HumanLocomotionClock.Contact Timing;
            public readonly Vector3 Position;
            public FootContact(HumanLocomotionClock.Contact timing, Vector3 position)
            { Timing = timing; Position = position; }
        }

        public event Action<HumanLocomotionPresenter, FootContact> ContactReady;
        public uint ActorId { get; private set; }
        public bool IsConfigured => clock != null;
        public bool OwnsPose => graph.IsValid();
        public HumanLocomotionClock.Sample Current => clock != null ? clock.Current : default;
        public string ProfileRevision { get; private set; }
        /// <summary>v0.3.0: an upper-body strike layer (masked Chest subtree) keeps the legs walking while striking.</summary>
        public bool SupportsStrikeLayer => strikeClip && upperBodyMask;
        /// <summary>v0.3.0: a crouched gait clip sampled on the same clock replaces the static crouch while moving.</summary>
        public bool SupportsCrouchWalk => crouchClip;
        public float StrikeLayerWeight { get; private set; }
        /// <summary>The upper-body mask of the strike layer (shared per rig layout by HumanLocomotionSetup).</summary>
        public AvatarMask UpperBodyMask => upperBodyMask;
        public float CrouchWeight { get; private set; }
        private HumanLocomotionClock clock;
        private Animator animator;
        private AnimationClip[] clips;
        private AnimationClip strikeClip, crouchClip;
        private AvatarMask upperBodyMask;
        private bool ownsMask;
        private Transform leftFoot, rightFoot;
        private PlayableGraph graph;
        private AnimationClipPlayable[] clipPlayables;
        private AnimationClipPlayable crouchPlayable, strikePlayable;
        private AnimationMixerPlayable mixer;
        private AnimationLayerMixerPlayable layers;
        private RuntimeAnimatorController savedController;
        private float savedSpeed;
        private bool savedRootMotion;
        private AnimatorCullingMode savedCulling;
        private int evaluatedFrame = -1, publishedFrame = -1;
        private float crouchTarget, strikeTarget, strikeSeconds;

        // Caller certifies these are the new gait clips. No installation on old prefabs by inference.
        public void Configure(uint actorId, Animator target, GaitClip[] gaits,
            Transform left, Transform right, string profileRevision)
        {
            if (!target || gaits == null || gaits.Length == 0 || !left || !right ||
                !target.transform.IsChildOf(transform) || !left.IsChildOf(target.transform) ||
                !right.IsChildOf(target.transform) || left == right || string.IsNullOrWhiteSpace(profileRevision))
                throw new ArgumentException("Explicit new gait clips, owned animator and distinct foot anchors are required.");
            var profiles = new HumanLocomotionClock.Profile[gaits.Length];
            var nextClips = new AnimationClip[gaits.Length];
            for (int i = 0; i < gaits.Length; i++)
            {
                if (gaits[i] == null || !gaits[i].Clip || gaits[i].Clip.legacy)
                    throw new ArgumentException("A valid nonlegacy clip is required for every gait.");
                nextClips[i] = gaits[i].Clip;
                profiles[i] = new HumanLocomotionClock.Profile(gaits[i].Speed,
                    gaits[i].DistancePerCycle, nextClips[i].length);
            }
            var next = new HumanLocomotionClock(profiles);
            Suspend();
            ActorId = actorId; animator = target; clips = nextClips;
            leftFoot = left; rightFoot = right; ProfileRevision = profileRevision; clock = next;
            strikeClip = crouchClip = null; ReleaseMask();
        }

        /// <summary>Optional v0.3.0 layers; null arguments leave the corresponding feature off. With ownsMask the
        /// presenter destroys the (per-actor, runtime-created) mask when it is replaced or destroyed.</summary>
        public void ConfigureOverlays(AnimationClip strike, AvatarMask upperBody, AnimationClip crouchWalk, bool ownsMask = false)
        {
            if (clock == null) throw new InvalidOperationException("Configure the gait clips first.");
            if ((strike && strike.legacy) || (crouchWalk && (crouchWalk.legacy || crouchWalk.length <= 0)))
                throw new ArgumentException("Overlay clips must be nonlegacy.");
            if (strike && (!upperBody || upperBody.transformCount == 0))
                throw new ArgumentException("The strike layer needs an upper-body transform mask.");
            Suspend();
            if (upperBodyMask != upperBody) ReleaseMask();
            strikeClip = strike; crouchClip = crouchWalk;
            upperBodyMask = strike ? upperBody : null;
            this.ownsMask = upperBodyMask && ownsMask;
            if (!strike && upperBody && ownsMask) Destroy(upperBody);
        }

        private void ReleaseMask()
        {
            if (ownsMask && upperBodyMask) Destroy(upperBodyMask);
            upperBodyMask = null; ownsMask = false;
        }

        /// <summary>Per frame, before EvaluateRenderedPose: crouch amount 0..1, strike layer weight and clip time.</summary>
        public void SetOverlayState(float crouch, float strikeWeight, float strikeTimeSeconds)
        {
            crouchTarget = SupportsCrouchWalk && Finite(crouch) ? Mathf.Clamp01(crouch) : 0;
            strikeTarget = SupportsStrikeLayer && Finite(strikeWeight) ? Mathf.Clamp01(strikeWeight) : 0;
            strikeSeconds = Finite(strikeTimeSeconds) ? Mathf.Max(0, strikeTimeSeconds) : 0;
        }

        // Call after base interpolation, before hands/anchors/facial final writers.
        // eligible=false covers stop, crouch, jump, strike and recovery; release before controller action setup.
        public PoseOwner EvaluateRenderedPose(Vector3 position, bool eligible, bool discontinuity,
            int frameId, float deltaSeconds)
        {
            if (clock == null) return PoseOwner.Controller;
            if (!isActiveAndEnabled || !animator || !animator.enabled || !eligible ||
                discontinuity || deltaSeconds <= 0 || deltaSeconds > 0.25f ||
                float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds) ||
                !Finite(position.x) || !Finite(position.y) || !Finite(position.z))
            { Suspend(); return PoseOwner.Controller; }
            if (evaluatedFrame == frameId) return OwnsPose ? PoseOwner.Locomotion : PoseOwner.Controller;
            uint generation = clock.Generation;
            var sample = clock.Advance(position.x, position.z, deltaSeconds, frameId, true);
            if (generation != clock.Generation) { ReleasePose(); evaluatedFrame = -1; return PoseOwner.Controller; }
            bool fresh = !graph.IsValid();
            AcquirePose();
            // Crouch and strike weights ease in/out; a fresh graph starts at the requested state.
            CrouchWeight = fresh ? crouchTarget : Mathf.MoveTowards(CrouchWeight, crouchTarget, deltaSeconds * 5f);
            StrikeLayerWeight = fresh ? strikeTarget : Mathf.MoveTowards(StrikeLayerWeight, strikeTarget, deltaSeconds * 10f);
            float standing = 1f - CrouchWeight;
            for (int i = 0; i < clips.Length; i++)
            {
                clipPlayables[i].SetTime((sample.Phase % 1) * clips[i].length);
                mixer.SetInputWeight(i, 0);
            }
            mixer.SetInputWeight(sample.LowerProfile, standing * (1f - (float)sample.Blend));
            if (sample.UpperProfile != sample.LowerProfile)
                mixer.SetInputWeight(sample.UpperProfile, standing * (float)sample.Blend);
            if (crouchPlayable.IsValid())
            {
                crouchPlayable.SetTime((sample.Phase % 1) * crouchClip.length);
                mixer.SetInputWeight(clips.Length, CrouchWeight);
            }
            if (layers.IsValid())
            {
                strikePlayable.SetTime(Mathf.Min(strikeSeconds, strikeClip.length));
                layers.SetInputWeight(1, StrikeLayerWeight);
            }
            graph.Evaluate(0);
            evaluatedFrame = frameId;
            return PoseOwner.Locomotion;
        }

        // Call after foot-anchor refresh/optional support correction, once per frame.
        public void PublishContacts(int frameId, bool suppress = false)
        {
            if (frameId != evaluatedFrame || publishedFrame == frameId || clock == null) return;
            publishedFrame = frameId;
            if (!clock.TryConsumeContact(out var timing) || suppress) return;
            Transform foot = timing.Left ? leftFoot : rightFoot;
            if (foot) ContactReady?.Invoke(this, new FootContact(timing, foot.position));
        }

        public void Suspend()
        {
            clock?.Suspend();
            evaluatedFrame = publishedFrame = -1;
            ReleasePose();
        }

        private void AcquirePose()
        {
            if (graph.IsValid()) return;
            savedController = animator.runtimeAnimatorController;
            savedSpeed = animator.speed; savedRootMotion = animator.applyRootMotion;
            savedCulling = animator.cullingMode;
            animator.runtimeAnimatorController = null;
            animator.speed = 1; animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            try
            {
                graph = PlayableGraph.Create("LMS_HumanLocomotion");
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                mixer = AnimationMixerPlayable.Create(graph, clips.Length + (crouchClip ? 1 : 0));
                clipPlayables = new AnimationClipPlayable[clips.Length];
                for (int i = 0; i < clips.Length; i++)
                {
                    var playable = CreateClip(clips[i]);
                    graph.Connect(playable, 0, mixer, i);
                    clipPlayables[i] = playable;
                }
                crouchPlayable = default;
                if (crouchClip)
                {
                    crouchPlayable = CreateClip(crouchClip);
                    graph.Connect(crouchPlayable, 0, mixer, clips.Length);
                }
                Playable source = mixer;
                layers = default; strikePlayable = default;
                if (SupportsStrikeLayer)
                {
                    layers = AnimationLayerMixerPlayable.Create(graph, 2);
                    graph.Connect(mixer, 0, layers, 0);
                    strikePlayable = CreateClip(strikeClip);
                    graph.Connect(strikePlayable, 0, layers, 1);
                    layers.SetInputWeight(0, 1);
                    layers.SetInputWeight(1, 0);
                    layers.SetLayerMaskFromAvatarMask(1, upperBodyMask);
                    source = layers;
                }
                var output = AnimationPlayableOutput.Create(graph, "Human gait", animator);
                output.SetSourcePlayable(source);
                graph.Play();
            }
            catch { ReleasePose(true); throw; }
        }

        private AnimationClipPlayable CreateClip(AnimationClip clip)
        {
            var playable = AnimationClipPlayable.Create(graph, clip);
            playable.SetSpeed(0); playable.SetApplyFootIK(false); playable.SetApplyPlayableIK(false);
            return playable;
        }

        private void ReleasePose(bool restoreWithoutGraph = false)
        {
            bool owned = graph.IsValid();
            if (owned) graph.Destroy();
            layers = default; strikePlayable = default; crouchPlayable = default;
            if ((!owned && !restoreWithoutGraph) || !animator) return;
            animator.runtimeAnimatorController = savedController;
            animator.speed = savedSpeed; animator.applyRootMotion = savedRootMotion;
            animator.cullingMode = savedCulling;
            savedController = null;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private void OnDisable() => Suspend();
        private void OnDestroy() { Suspend(); ReleaseMask(); ContactReady = null; }
    }
}
