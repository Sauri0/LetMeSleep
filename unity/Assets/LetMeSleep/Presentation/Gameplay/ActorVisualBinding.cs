using LetMeSleep.Content.Characters;
using LetMeSleep.Core;
using LetMeSleep.Gameplay.Unity;
using UnityEngine;
using GameplayModel = LetMeSleep.Gameplay;

namespace LetMeSleep.Presentation.Gameplay
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1100)]
    public sealed class ActorVisualBinding : MonoBehaviour
    {
        private const float MaximumExtrapolationSeconds = 0.10f;
        private const float HumanStrideMeters = 1.2f;
        private const float MosquitoStrideMeters = 0.3f;
        // SurfaceWalk source contract cc4c871: .116 / .58 * .5 = .100 Unity m/cycle.
        private const float MosquitoSurfaceStrideMeters = 0.1f;
        private const float MaximumSurfaceWalkPlayback = 8f;

        private GameplayActorProxy proxy;
        private UnityGameplayWorld world;
        private CharacterView view;
        private bool localActor;
        private GameplayModel.ActorSnapshot previous;
        private GameplayModel.ActorSnapshot current;
        private uint currentHostTick;
        private uint previousHostTick;
        private float snapshotArrivalTime;
        private float snapshotInterval = 0.05f;
        private int currentMotion = -1;
        private int temporaryMotion = -1;
        private float temporaryUntil;
        private bool warnedBiteOffset;
        private VisualAttentionRig attention;
        private bool biteSamplePending, loggedBiteContact, warnedBiteResidual;
        private int biteSampleFrame;
        public float LastBitePreCorrectionMeters { get; private set; }
        public float LastBiteFinalResidualMeters { get; private set; }
        public int LastBiteSampleFrame { get; private set; } = -1;
        private sealed class ArmChain
        {
            public Transform Upper, Lower, Hand;
            public bool IsValid => Upper && Lower && Hand && Lower.IsChildOf(Upper) && Hand.IsChildOf(Lower);
        }
        private ArmChain leftArm, rightArm;
        private GameObject flyswatter;
        private ToolView strikeTool;
        private readonly float[] motionDurations = new float[32];
        private HumanLocomotionPresenter locomotion;
        private bool usingLocomotion, locomotionDiscontinuity;
        private bool localGaitInterpolation;
        private int landedFrame = -1;

        public void BindLocomotion(HumanLocomotionPresenter value)
        {
            if (locomotion) locomotion.Suspend();
            locomotion = value;
            usingLocomotion = false;
            locomotionDiscontinuity = true;
        }

        private bool HasLocomotion => locomotion && locomotion.isActiveAndEnabled && locomotion.IsConfigured;
        private bool CanUseLocomotion(GameplayModel.ActorSnapshot state) => HasLocomotion &&
            proxy.Role == PlayerRole.Human && state != null && state.Grounded &&
            state.LifeState == GameplayModel.LifeState.Active && state.CrouchFraction <= 0.1f &&
            state.StrikeState.Phase == GameplayModel.StrikePhase.None && temporaryMotion < 0 &&
            PlanarSpeed(state.Velocity) > 0.1f;

        private void ReleaseLocomotion()
        {
            if (locomotion) locomotion.Suspend();
            if (usingLocomotion) currentMotion = -1; // Controller state must be explicitly reapplied.
            usingLocomotion = false;
        }

        public uint ActorId => proxy != null ? proxy.ActorId : 0;
        public CharacterView View => view;

        public void BindFlyswatter(GameObject instance)
        {
            flyswatter = instance;
            strikeTool = instance != null ? instance.GetComponent<ToolView>() : null;
            if (flyswatter != null)
                flyswatter.SetActive(false);
        }

        public void Initialize(
            GameplayActorProxy actorProxy, UnityGameplayWorld gameplayWorld,
            CharacterView characterView, bool isLocal)
        {
            proxy = actorProxy;
            world = gameplayWorld;
            view = characterView;
            localActor = isLocal;
            if (view != null)
            {
                view.SetFirstPersonVisibility(isLocal && proxy.Role == PlayerRole.Human);
                if (proxy.Role == PlayerRole.Human)
                {
                    leftArm = FindArm("L");
                    rightArm = FindArm("R");
                }
                CacheMotionDurations();
            }
        }

        public void ApplySnapshot(GameplayModel.ActorSnapshot state, uint hostTick)
        {
            if (state == null || proxy == null || state.ActorId != proxy.ActorId)
                return;

            bool discontinuity = current == null || ShouldCut(current, state);
            if (current != null && !current.Grounded && state.Grounded) landedFrame = Time.frameCount;
            locomotionDiscontinuity |= discontinuity;
            // New human gait measures rendered displacement; smooth local visuals too, so
            // 30Hz snapshot jumps are not mistaken for 144Hz teleport-speed movement.
            bool gaitTransition = localActor && CanUseLocomotion(current) != CanUseLocomotion(state);
            locomotionDiscontinuity |= gaitTransition;
            bool cut = discontinuity || (localActor && (!CanUseLocomotion(state) || gaitTransition));
            previous = cut ? state : current;
            previousHostTick = cut ? hostTick : currentHostTick;
            current = state;
            currentHostTick = hostTick;
            if (proxy.Role == PlayerRole.Human && temporaryMotion == 12 &&
                (state.CrouchFraction > .25f || state.LifeState != GameplayModel.LifeState.Active))
            {
                // Crouch remains the lower-body pose; the arm solve supplies the stroke.
                temporaryMotion = -1; temporaryUntil = 0;
            }
            uint tickDelta = unchecked(currentHostTick - previousHostTick);
            snapshotInterval = tickDelta == 0 ? 0.05f : Mathf.Clamp(tickDelta / 30f, 1f / 60f, 0.10f);
            snapshotArrivalTime = Time.unscaledTime;

            if (flyswatter != null)
            {
                bool equipped = GameplayModel.GameplayTools.IsFlyswatter(state.EquippedToolId);
                if (flyswatter.activeSelf != equipped)
                    flyswatter.SetActive(equipped);
            }

            if (cut)
                SetWorldPose(state.Position.ToUnity(), state.BodyRotation.ToUnity());
            if (Time.unscaledTime >= temporaryUntil)
                ApplyMotion(state, currentMotion < 0);
        }

        public void ApplyEvent(in GameplayModel.GameplayEvent item)
        {
            if (view == null || item.SourceActorId != ActorId)
                return;
            int motion = -1;
            switch (item.Kind)
            {
                case GameplayModel.GameplayEventKind.StrikeStarted:
                    if (proxy.Role == PlayerRole.Human)
                    {
                        if (current != null && current.CrouchFraction > .25f)
                        { temporaryMotion = -1; temporaryUntil = 0; ApplyMotion(current, false); }
                        else motion = 12;
                    }
                    break;
                case GameplayModel.GameplayEventKind.BiteStarted:
                    if (proxy.Role == PlayerRole.Mosquito) motion = 7;
                    break;
                case GameplayModel.GameplayEventKind.BiteEnded:
                    if (proxy.Role == PlayerRole.Mosquito) motion = 9;
                    break;
                case GameplayModel.GameplayEventKind.MosquitoKnockedDown:
                    if (proxy.Role == PlayerRole.Mosquito) motion = 10;
                    break;
            }
            if (motion >= 0)
                PlayTemporary(motion);
        }

        private void LateUpdate()
        {
            if (proxy == null || view == null || current == null)
            {
                ReleaseLocomotion();
                ReleaseBiteAttention();
                return;
            }

            if (temporaryMotion >= 0 && Time.unscaledTime >= temporaryUntil)
            {
                temporaryMotion = -1;
                ApplyMotion(current, false);
            }

            bool interpolateLocal = localActor && CanUseLocomotion(current) && Time.deltaTime > 0;
            if (localActor && interpolateLocal != localGaitInterpolation)
            {
                // Entry/exit (including event-only strikes and pause) starts at current pose.
                previous = current;
                previousHostTick = currentHostTick;
                snapshotArrivalTime = Time.unscaledTime;
                locomotionDiscontinuity = true;
                localGaitInterpolation = interpolateLocal;
            }
            if (localActor && !interpolateLocal)
            {
                SetWorldPose(current.Position.ToUnity(), current.BodyRotation.ToUnity());
            }
            else
            {
                float elapsed = Mathf.Max(0f, Time.unscaledTime - snapshotArrivalTime);
                Vector3 position;
                Quaternion rotation;
                if (elapsed <= snapshotInterval)
                {
                    float t = snapshotInterval <= 0f ? 1f : elapsed / snapshotInterval;
                    position = Vector3.Lerp(previous.Position.ToUnity(), current.Position.ToUnity(), t);
                    rotation = Quaternion.Slerp(previous.BodyRotation.ToUnity(), current.BodyRotation.ToUnity(), t);
                }
                else
                {
                    float extrapolation = Mathf.Min(elapsed - snapshotInterval, MaximumExtrapolationSeconds);
                    position = current.Position.ToUnity() + current.Velocity.ToUnity() * extrapolation;
                    rotation = current.BodyRotation.ToUnity();
                }
                SetWorldPose(position, rotation);
            }

            if (HasLocomotion)
            {
                bool eligible = CanUseLocomotion(current);
                if (!eligible && usingLocomotion)
                {
                    ReleaseLocomotion();
                    ApplyMotion(current, false);
                }
                var owner = locomotion.EvaluateRenderedPose(transform.position, eligible, locomotionDiscontinuity,
                    Time.frameCount, Time.deltaTime);
                if (owner == HumanLocomotionPresenter.PoseOwner.Controller)
                {
                    bool reapply = usingLocomotion || currentMotion < 0;
                    ReleaseLocomotion();
                    if (reapply) ApplyMotion(current, true, false);
                }
                else usingLocomotion = true;
                locomotionDiscontinuity = false;
            }
            ApplyAuthoritativeHands();
            view.RefreshAnchors();
            if (HasLocomotion) locomotion.PublishContacts(Time.frameCount, landedFrame == Time.frameCount);
            ApplyBiteAnchor();
        }

        private void ApplyMotion(GameplayModel.ActorSnapshot state, bool immediate, bool allowLocomotion = true)
        {
            if (allowLocomotion && CanUseLocomotion(state))
            {
                usingLocomotion = true;
                return; // Manual gait owns speed, state and phase; no old 1.2m resync/clamp.
            }
            if (usingLocomotion) ReleaseLocomotion();
            int motion = SelectMotion(state);
            if (motion == currentMotion)
            {
                ApplyAnimatorSpeed(state, motion);
                SynchronizeLoopPhase(state, motion);
                return;
            }

            currentMotion = motion;
            ApplyAnimatorSpeed(state, motion);
            if (IsSurfaceWalk(motion) && view.Animator != null &&
                TryGetMotion(motion, out CharacterView.MotionBinding surfaceBinding))
            {
                float fadeSeconds = immediate ? 0f : CrossFadeSeconds(state);
                view.PlayMotion(motion, fadeSeconds);
                float phase = AnimationPhase(state, motion);
                if (immediate)
                    view.Animator.Play(surfaceBinding.StateName, 0, phase);
                else
                    view.Animator.CrossFadeInFixedTime(surfaceBinding.StateName, fadeSeconds, 0,
                        phase * MotionDuration(motion));
                return;
            }
            if (immediate && TryGetMotion(motion, out CharacterView.MotionBinding binding) && view.Animator != null)
            {
                view.PlayMotion(motion, 0f);
                view.Animator.Play(binding.StateName, 0, AnimationPhase(state, motion));
                return;
            }
            view.PlayMotion(motion, CrossFadeSeconds(state));
        }

        private void SynchronizeLoopPhase(GameplayModel.ActorSnapshot state, int motion)
        {
            if ((localActor && !IsSurfaceWalk(motion)) || !UsesAuthoritativeDistancePhase(motion) ||
                view.Animator == null || !TryGetMotion(motion, out CharacterView.MotionBinding binding) || !binding.Loop)
                return;
            if (IsSurfaceWalk(motion) && view.Animator.IsInTransition(0))
                return;
            AnimatorStateInfo info = view.Animator.GetCurrentAnimatorStateInfo(0);
            if (!info.IsName(binding.StateName))
                return;
            float rendered = Mathf.Repeat(info.normalizedTime, 1f);
            float authoritative = AnimationPhase(state, motion);
            float phaseError = Mathf.Abs(Mathf.DeltaAngle(rendered * 360f, authoritative * 360f)) / 360f;
            if (phaseError > 0.20f)
                view.Animator.Play(binding.StateName, 0, authoritative);
        }

        private void PlayTemporary(int motion)
        {
            ReleaseLocomotion();
            temporaryMotion = motion;
            float duration = MotionDuration(motion);
            temporaryUntil = Time.unscaledTime + Mathf.Clamp(duration, 0.08f, 2.5f);
            currentMotion = motion;
            if (view.Animator != null)
                view.Animator.speed = 1f;
            view.PlayMotion(motion, 0.06f);
        }

        private float MotionDuration(int motion)
        {
            return motion >= 0 && motion < motionDurations.Length && motionDurations[motion] > 0f
                ? motionDurations[motion]
                : 0.18f;
        }

        private void CacheMotionDurations()
        {
            if (view.Animator == null || view.Animator.runtimeAnimatorController == null || view.Motions == null)
                return;
            AnimationClip[] clips = view.Animator.runtimeAnimatorController.animationClips;
            for (int i = 0; i < view.Motions.Length; i++)
            {
                CharacterView.MotionBinding binding = view.Motions[i];
                if (binding == null || binding.Id < 0 || binding.Id >= motionDurations.Length)
                    continue;
                for (int clipIndex = 0; clipIndex < clips.Length; clipIndex++)
                {
                    AnimationClip clip = clips[clipIndex];
                    if (clip == null || clip.name != binding.ClipName)
                        continue;
                    motionDurations[binding.Id] = clip.length;
                    break;
                }
            }
        }

        private void ApplyAnimatorSpeed(GameplayModel.ActorSnapshot state, int motion)
        {
            if (view.Animator == null || temporaryMotion >= 0)
                return;
            if (!UsesAuthoritativeDistancePhase(motion))
            {
                view.Animator.speed = 1f;
                return;
            }

            if (IsSurfaceWalk(motion))
            {
                Vector3 velocity = state.Velocity.ToUnity();
                if (state.SurfaceAttachment.HasValue && world != null &&
                    world.ResolveSurface(state.SurfaceAttachment.Value, out var contact))
                    velocity = Vector3.ProjectOnPlane(velocity, contact.WorldNormal.ToUnity());
                // The cap is representational headroom, not a gameplay speed change.
                view.Animator.speed = Mathf.Clamp(
                    MotionDuration(motion) * velocity.magnitude / MosquitoSurfaceStrideMeters,
                    0f, MaximumSurfaceWalkPlayback);
                return;
            }

            float speed = proxy.Role == PlayerRole.Human
                ? PlanarSpeed(state.Velocity)
                : state.Velocity.Length;
            float distancePerCycle = proxy.Role == PlayerRole.Human
                ? HumanStrideMeters
                : MosquitoStrideMeters;
            float clipDuration = MotionDuration(motion);
            float playback = clipDuration * speed / distancePerCycle;
            view.Animator.speed = Mathf.Clamp(playback, 0.35f, 2.5f);
        }

        private bool IsSurfaceWalk(int motion)
        {
            return proxy != null && proxy.Role == PlayerRole.Mosquito && motion == 6;
        }

        private float AnimationPhase(GameplayModel.ActorSnapshot state, int motion)
        {
            // Authority exposes accumulated distance / .3 for both mosquito modes.
            // Convert the accumulated phase before wrapping, including on reentry.
            float phase = IsSurfaceWalk(motion)
                ? state.MotionPhase * (MosquitoStrideMeters / MosquitoSurfaceStrideMeters)
                : state.MotionPhase;
            return Mathf.Repeat(phase, 1f);
        }

        private bool UsesAuthoritativeDistancePhase(int motion)
        {
            return proxy.Role == PlayerRole.Human
                ? motion == 1 || motion == 2
                : motion == 2 || motion == 6;
        }

        private bool TryGetMotion(int id, out CharacterView.MotionBinding result)
        {
            if (view != null && view.Motions != null)
            {
                for (int i = 0; i < view.Motions.Length; i++)
                {
                    CharacterView.MotionBinding item = view.Motions[i];
                    if (item != null && item.Id == id)
                    {
                        result = item;
                        return true;
                    }
                }
            }
            result = null;
            return false;
        }

        private int SelectMotion(GameplayModel.ActorSnapshot state)
        {
            if (proxy.Role == PlayerRole.Human)
            {
                if (state.StrikeState.Phase != GameplayModel.StrikePhase.None)
                    return state.CrouchFraction > .25f ? 3 : 12;
                if (state.LifeState == GameplayModel.LifeState.Fainted) return 10;
                if (state.LifeState == GameplayModel.LifeState.Recovering) return 11;
                if (state.LifeState == GameplayModel.LifeState.Falling) return 9;
                if (!state.Grounded) return state.Velocity.Y > 0.10f ? 4 : 9;
                if (state.CrouchFraction > 0.25f) return 3;
                float speed = PlanarSpeed(state.Velocity);
                if (speed > 2.45f) return 2;
                return speed > 0.10f ? 1 : 0;
            }

            switch (state.LifeState)
            {
                case GameplayModel.LifeState.ApproachingSurface: return 4;
                case GameplayModel.LifeState.Surface: return state.Velocity.Length > 0.08f ? 6 : 5;
                case GameplayModel.LifeState.PreparingBite: return 7;
                case GameplayModel.LifeState.Biting: return 8;
                case GameplayModel.LifeState.Falling: return 11;
                case GameplayModel.LifeState.Stunned: return 10;
                case GameplayModel.LifeState.Recovering: return 12;
                case GameplayModel.LifeState.Flying: return state.Velocity.Length > 0.12f ? 2 : 1;
                default: return 1;
            }
        }

        private void ApplyBiteAnchor()
        {
            if (!TryBiteSurface(out _, out var target, out _)) { ReleaseBiteAttention(); return; }
            Transform tip = view.GetAnchor("ProboscisTip");
            if (tip == null) { ReleaseBiteAttention(); return; }
            if (!attention)
            {
                attention = view.GetComponent<VisualAttentionRig>();
                if (attention) attention.AfterEvaluation += MeasureBiteResidual;
            }
            if (attention) attention.SetHeadTrackingEnabled(false);
            view.RefreshAnchors();

            Vector3 correction = target - tip.position;
            LastBitePreCorrectionMeters = correction.magnitude;
            if (!warnedBiteOffset && correction.sqrMagnitude > 0.0225f)
            {
                warnedBiteOffset = true;
                Debug.LogError($"LMS_BITE_VISUAL_OFFSET actor={ActorId} meters={correction.magnitude:F4}", this);
            }
            transform.position += correction;
            view.RefreshAnchors();
            biteSamplePending = true;
            biteSampleFrame = Time.frameCount;
            if (!attention || !attention.isActiveAndEnabled || !attention.IsConfigured)
                MeasureBiteResidual();
        }

        private bool TryBiteSurface(out GameplayBodySurface surface, out Vector3 point, out Vector3 normal)
        {
            surface = null; point = normal = default;
            if (!proxy || proxy.Role != PlayerRole.Mosquito || current == null ||
                !current.BiteAttachment.HasValue || !world ||
                (current.LifeState != GameplayModel.LifeState.PreparingBite && current.LifeState != GameplayModel.LifeState.Biting)) return false;
            var attachment = current.BiteAttachment.Value;
            if (!world.Actors.TryGetValue(attachment.VictimId, out var victim) || !victim ||
                !victim.gameObject.activeInHierarchy || victim.State == null ||
                victim.State.PoseRevision != attachment.PoseRevision ||
                !victim.BodySurfaces.TryGetValue(attachment.SurfaceId, out surface) || !surface ||
                !surface.gameObject.activeInHierarchy) return false;
            point = surface.transform.TransformPoint(attachment.LocalPoint.ToUnity());
            normal = surface.transform.TransformDirection(attachment.LocalNormal.ToUnity());
            if (!Finite(point) || !Finite(normal) || normal.sqrMagnitude < .000001f) return false;
            normal.Normalize();
            return true;
        }

        private void MeasureBiteResidual()
        {
            if (!biteSamplePending || biteSampleFrame != Time.frameCount) return;
            biteSamplePending = false;
            if (!view || !TryBiteSurface(out _, out var target, out _)) return;
            view.RefreshAnchors(); // Measure the source bone after the final facial writer.
            var tip = view.GetAnchor("ProboscisTip");
            if (!tip) return;
            LastBiteFinalResidualMeters = Vector3.Distance(target, tip.position);
            LastBiteSampleFrame = Time.frameCount;
            if (!loggedBiteContact)
            {
                loggedBiteContact = true;
                Debug.Log($"LMS_BITE_CONTACT actor={ActorId} preCorrection={LastBitePreCorrectionMeters:F4} finalResidual={LastBiteFinalResidualMeters:F4} frame={LastBiteSampleFrame}", this);
            }
            if (!warnedBiteResidual && LastBiteFinalResidualMeters > .001f)
            {
                warnedBiteResidual = true;
                Debug.LogError($"LMS_BITE_VISUAL_RESIDUAL actor={ActorId} preCorrection={LastBitePreCorrectionMeters:F4} finalResidual={LastBiteFinalResidualMeters:F4} frame={LastBiteSampleFrame}", this);
            }
        }

        private void ReleaseBiteAttention()
        {
            if (attention) attention.SetHeadTrackingEnabled(true);
            biteSamplePending = loggedBiteContact = warnedBiteResidual = false;
            LastBiteSampleFrame = -1;
        }
        private void OnDisable()
        {
            ReleaseLocomotion();
            ReleaseBiteAttention();
            if (attention) attention.AfterEvaluation -= MeasureBiteResidual;
            attention = null;
            hasSurfaceHeading = false;
        }

        private void ApplyAuthoritativeHands()
        {
            if (proxy.Role != PlayerRole.Human ||
                current.StrikeState.Phase == GameplayModel.StrikePhase.None)
                return;
            if (current.StrikeState.Hand < 0)
                AlignArm(leftArm, 5u, -1f);
            else
                AlignArm(rightArm, 6u, 1f);
        }

        private ArmChain FindArm(string side) => new ArmChain
        {
            Upper = FindDescendant(view.transform, "UpperArm." + side),
            Lower = FindDescendant(view.transform, "LowerArm." + side),
            Hand = FindDescendant(view.transform, "Hand." + side)
        };

        private void AlignArm(ArmChain arm, uint localSurfaceId, float side)
        {
            if (arm == null || !arm.IsValid || !proxy.BodySurfaces.TryGetValue(
                    proxy.ActorId * 100 + localSurfaceId, out GameplayBodySurface forearm) ||
                forearm.Collider == null)
                return;
            var capsule = forearm.Collider as CapsuleCollider;
            if (capsule == null)
                return;
            Vector3 shoulder = arm.Upper.position, elbow = arm.Lower.position, wrist = arm.Hand.position;
            float upperLength = Vector3.Distance(shoulder, elbow), lowerLength = Vector3.Distance(elbow, wrist);
            // The collision center follows Authority's timeline. The anatomical proxy
            // has different lengths and shoulder height; do not feed its clamped wrist
            // back into the real rig as a second, unrelated contact trajectory.
            var strike = current.StrikeState;
            bool hasTool = GameplayModel.GameplayTools.IsFlyswatter(strike.ToolId) &&
                strikeTool && strikeTool.Impact && strikeTool.Grip;
            view.RefreshAnchors();
            Vector3 restContact = hasTool ? strikeTool.Impact.position : wrist;
            Vector3 contact = GameplayModel.StrikeVisualTrajectory.Contact(strike, restContact.ToFloat()).ToUnity();
            Quaternion handRotation = arm.Hand.rotation;
            Vector3 target = contact;
            if (hasTool)
            {
                // Rotate the hand and mounted grip together; never detach the tool or
                // translate individual bones. Measure the full real Hand -> Impact
                // offset, including the authored hand/socket offset, not only .365 m.
                Vector3 offset = restContact - wrist;
                Vector3 direction = GameplayModel.StrikeVisualTrajectory.ToolOffset((contact - shoulder).ToFloat(),
                    offset.magnitude, Mathf.Abs(upperLength - lowerLength) + .0001f,
                    (elbow - shoulder).ToFloat()).ToUnity();
                if (offset.sqrMagnitude > .000001f && direction.sqrMagnitude > .000001f)
                {
                    Quaternion aligned = Quaternion.FromToRotation(offset, direction) * handRotation;
                    Quaternion desired = Quaternion.Slerp(handRotation, aligned,
                        GameplayModel.StrikeVisualTrajectory.PoseWeight(strike));
                    Vector3 rotatedOffset = desired * Quaternion.Inverse(handRotation) * offset;
                    handRotation = desired;
                    target = contact - rotatedOffset;
                }
            }
            if (!Finite(target) || !Finite(shoulder) || !Finite(elbow) || !Finite(wrist) ||
                upperLength < .001f || lowerLength < .001f) return;
            Vector3 delta = target - shoulder;
            Vector3 axis = delta.sqrMagnitude > .000001f ? delta.normalized : (wrist - shoulder).normalized;
            if (axis.sqrMagnitude < .5f) axis = transform.forward;
            float length = Mathf.Clamp(delta.magnitude, Mathf.Abs(upperLength - lowerLength) + .0001f,
                upperLength + lowerLength - .0001f);
            Vector3 bend = Vector3.ProjectOnPlane(elbow - shoulder, axis);
            if (bend.sqrMagnitude < .000001f) bend = Vector3.ProjectOnPlane(transform.right * side - transform.forward * .3f, axis);
            if (bend.sqrMagnitude < .000001f) bend = Vector3.ProjectOnPlane(transform.up, axis);
            if (bend.sqrMagnitude < .000001f) bend = Vector3.ProjectOnPlane(transform.forward, axis);
            float along = (upperLength * upperLength - lowerLength * lowerLength + length * length) / (2 * length);
            Vector3 solvedElbow = shoulder + axis * along + bend.normalized *
                Mathf.Sqrt(Mathf.Max(0, upperLength * upperLength - along * along));
            Vector3 reachableTarget = shoulder + axis * length;
            // Rotate real segments around their joints. Never stretch local offsets to
            // reach a collider target authored with different proportions.
            arm.Upper.rotation = Quaternion.FromToRotation(elbow - shoulder, solvedElbow - shoulder) * arm.Upper.rotation;
            arm.Lower.rotation = Quaternion.FromToRotation(arm.Hand.position - arm.Lower.position,
                reachableTarget - arm.Lower.position) * arm.Lower.rotation;
            // Bare-hand local finger/wrist pose remains authored. A held tool needs
            // explicit wrist orientation as well as position to put Impact on the
            // sweep; the socket, grip and tool remain rigidly mounted to that hand.
            if (hasTool) arm.Hand.rotation = handRotation;
        }

        private void SetWorldPose(Vector3 position, Quaternion rotation)
        {
            rotation = ResolveVisualRotation(rotation);
            transform.SetPositionAndRotation(position, rotation);
        }

        private Quaternion visualRotation;
        private float lastVisualPoseTime;
        private bool hasVisualRotation;
        private bool returningFromSurface;
        private bool hasSurfaceHeading;
        private Vector3 previousSurfaceNormal, previousSurfaceForward;

        private Quaternion ResolveVisualRotation(Quaternion bodyRotation)
        {
            if (proxy == null || proxy.Role != PlayerRole.Mosquito || current == null)
                return bodyRotation;

            float now = Time.unscaledTime;
            float elapsed = hasVisualRotation ? Mathf.Clamp(now - lastVisualPoseTime, 0f, .1f) : 0f;
            lastVisualPoseTime = now;
            bool supported = current.SurfaceAttachment.HasValue && world != null &&
                (current.LifeState == GameplayModel.LifeState.ApproachingSurface ||
                 current.LifeState == GameplayModel.LifeState.Surface);
            if (!supported) hasSurfaceHeading = false;
            if (TryBiteSurface(out _, out _, out var biteNormal))
            {
                Vector3 biteForward = -biteNormal;
                Vector3 stableUp = hasVisualRotation ? visualRotation * Vector3.up : Vector3.up;
                Vector3 biteUp = Vector3.ProjectOnPlane(stableUp, biteForward);
                if (biteUp.sqrMagnitude < .000001f) biteUp = Vector3.ProjectOnPlane(Vector3.up, biteForward);
                if (biteUp.sqrMagnitude < .000001f) biteUp = Vector3.ProjectOnPlane(Vector3.forward, biteForward);
                visualRotation = Quaternion.LookRotation(biteForward, biteUp.normalized);
                returningFromSurface = true; // Reuse smooth visual release on detach to flight.
            }
            else if (supported && world.ResolveSurface(current.SurfaceAttachment.Value, out var contact) &&
                TrySurfaceHeading(contact.WorldNormal, elapsed, out var up, out var forward))
            {
                Quaternion target = Quaternion.LookRotation(forward.ToUnity(), up.ToUnity());
                // Ease the approach tilt; attached feet must face the actual surface.
                visualRotation = !hasVisualRotation || current.LifeState == GameplayModel.LifeState.Surface
                    ? target : Quaternion.Slerp(visualRotation, target, 1f - Mathf.Exp(-24f * elapsed));
                returningFromSurface = true;
            }
            else if (hasVisualRotation && returningFromSurface && current.LifeState == GameplayModel.LifeState.Flying)
            {
                visualRotation = Quaternion.Slerp(visualRotation, bodyRotation, 1f - Mathf.Exp(-24f * elapsed));
                if (Quaternion.Angle(visualRotation, bodyRotation) < .1f)
                {
                    visualRotation = bodyRotation;
                    returningFromSurface = false;
                }
            }
            else
            {
                visualRotation = bodyRotation;
                returningFromSurface = false;
            }
            hasVisualRotation = true;
            return visualRotation;
        }

        private bool TrySurfaceHeading(GameplayModel.Float3 normal, float elapsed,
            out GameplayModel.Float3 up, out GameplayModel.Float3 forward)
        {
            Vector3 previous = hasVisualRotation ? visualRotation * Vector3.forward : Vector3.zero;
            if (hasSurfaceHeading)
            {
                // Transport heading with the support plane before applying new view intent.
                previous = GameplayModel.SurfaceVisualFrame.TransportForward(previousSurfaceNormal.ToFloat(),
                    normal, previousSurfaceForward.ToFloat()).ToUnity();
            }
            bool valid = GameplayModel.SurfaceVisualFrame.TryResolve(normal, current.ViewForward,
                previous.ToFloat(), elapsed * 12f, out up, out forward);
            hasSurfaceHeading = valid;
            if (valid) { previousSurfaceNormal = up.ToUnity(); previousSurfaceForward = forward.ToUnity(); }
            return valid;
        }

        private static float PlanarSpeed(GameplayModel.Float3 velocity)
        {
            return Mathf.Sqrt(velocity.X * velocity.X + velocity.Z * velocity.Z);
        }
        private static bool Finite(Vector3 value) =>
            !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
            !float.IsNaN(value.z) && !float.IsInfinity(value.z);

        private static Transform FindDescendant(Transform root, string exactName)
        {
            if (root.name == exactName)
                return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDescendant(root.GetChild(i), exactName);
                if (found != null)
                    return found;
            }
            return null;
        }

        private static float CrossFadeSeconds(GameplayModel.ActorSnapshot state)
        {
            if (state.LifeState == GameplayModel.LifeState.Recovering) return 0.14f;
            if (state.StrikeState.Phase != GameplayModel.StrikePhase.None ||
                state.LifeState == GameplayModel.LifeState.PreparingBite ||
                state.LifeState == GameplayModel.LifeState.Biting) return 0.06f;
            return 0.10f;
        }

        private static bool ShouldCut(GameplayModel.ActorSnapshot from, GameplayModel.ActorSnapshot to)
        {
            if (from.LifeState != to.LifeState ||
                (to.Position - from.Position).LengthSquared > 9f)
                return true;
            if (from.SurfaceAttachment.HasValue != to.SurfaceAttachment.HasValue ||
                from.BiteAttachment.HasValue != to.BiteAttachment.HasValue)
                return true;
            if (from.SurfaceAttachment.HasValue)
            {
                GameplayModel.SurfaceAttachment a = from.SurfaceAttachment.Value;
                GameplayModel.SurfaceAttachment b = to.SurfaceAttachment.Value;
                if (a.SurfaceId != b.SurfaceId || a.Revision != b.Revision) return true;
            }
            if (from.BiteAttachment.HasValue)
            {
                GameplayModel.BiteAttachment a = from.BiteAttachment.Value;
                GameplayModel.BiteAttachment b = to.BiteAttachment.Value;
                if (a.VictimId != b.VictimId || a.SurfaceId != b.SurfaceId || a.PoseRevision != b.PoseRevision) return true;
            }
            return false;
        }
    }
}
