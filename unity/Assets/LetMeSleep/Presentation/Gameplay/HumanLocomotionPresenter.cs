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
        private HumanLocomotionClock clock;
        private Animator animator;
        private AnimationClip[] clips;
        private Transform leftFoot, rightFoot;
        private PlayableGraph graph;
        private AnimationClipPlayable[] clipPlayables;
        private AnimationMixerPlayable mixer;
        private RuntimeAnimatorController savedController;
        private float savedSpeed;
        private bool savedRootMotion;
        private AnimatorCullingMode savedCulling;
        private int evaluatedFrame = -1, publishedFrame = -1;

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
        }

        // Call after base interpolation, before hands/anchors/facial final writers.
        // eligible=false covers stop, crouch, jump, strike and recovery; release before controller action setup.
        public void EvaluateRenderedPose(Vector3 position, bool eligible, bool discontinuity,
            int frameId, float deltaSeconds)
        {
            if (clock == null) return;
            if (!isActiveAndEnabled || !animator || !animator.enabled || !eligible ||
                discontinuity || deltaSeconds <= 0 || deltaSeconds > 0.25f ||
                float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds) ||
                !Finite(position.x) || !Finite(position.y) || !Finite(position.z))
            { Suspend(); return; }
            if (evaluatedFrame == frameId) return;
            uint generation = clock.Generation;
            var sample = clock.Advance(position.x, position.z, deltaSeconds, frameId, true);
            if (generation != clock.Generation) { ReleasePose(); evaluatedFrame = -1; return; }
            AcquirePose();
            for (int i = 0; i < clips.Length; i++)
            {
                clipPlayables[i].SetTime((sample.Phase % 1) * clips[i].length);
                mixer.SetInputWeight(i, 0);
            }
            mixer.SetInputWeight(sample.LowerProfile, 1f - (float)sample.Blend);
            if (sample.UpperProfile != sample.LowerProfile)
                mixer.SetInputWeight(sample.UpperProfile, (float)sample.Blend);
            graph.Evaluate(0);
            evaluatedFrame = frameId;
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
                mixer = AnimationMixerPlayable.Create(graph, clips.Length);
                clipPlayables = new AnimationClipPlayable[clips.Length];
                for (int i = 0; i < clips.Length; i++)
                {
                    var playable = AnimationClipPlayable.Create(graph, clips[i]);
                    playable.SetSpeed(0); playable.SetApplyFootIK(false); playable.SetApplyPlayableIK(false);
                    graph.Connect(playable, 0, mixer, i);
                    clipPlayables[i] = playable;
                }
                var output = AnimationPlayableOutput.Create(graph, "Human gait", animator);
                output.SetSourcePlayable(mixer);
                graph.Play();
            }
            catch { ReleasePose(true); throw; }
        }

        private void ReleasePose(bool restoreWithoutGraph = false)
        {
            bool owned = graph.IsValid();
            if (owned) graph.Destroy();
            if ((!owned && !restoreWithoutGraph) || !animator) return;
            animator.runtimeAnimatorController = savedController;
            animator.speed = savedSpeed; animator.applyRootMotion = savedRootMotion;
            animator.cullingMode = savedCulling;
            savedController = null;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private void OnDisable() => Suspend();
        private void OnDestroy() { Suspend(); ContactReady = null; }
    }
}
