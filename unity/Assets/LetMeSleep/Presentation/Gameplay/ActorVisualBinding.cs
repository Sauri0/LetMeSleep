using System.Collections.Generic;
using LetMeSleep.Content.Characters;
using LetMeSleep.Core;
using LetMeSleep.Gameplay.Unity;
using UnityEngine;
using GameplayModel = LetMeSleep.Gameplay;
using Ids = LetMeSleep.Presentation.Gameplay.CharacterMotionIds;

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
            public Transform Shoulder, Upper, Lower, Hand, Palm;
            public bool IsValid => Upper && Lower && Hand && Lower.IsChildOf(Upper) && Hand.IsChildOf(Lower);
        }
        private ArmChain leftArm, rightArm;
        private readonly Dictionary<string, GameObject> tools = new Dictionary<string, GameObject>();
        private ToolView strikeTool;
        private readonly float[] motionDurations = new float[32];
        private HumanLocomotionPresenter locomotion;
        private bool usingLocomotion, locomotionDiscontinuity;
        private bool localGaitInterpolation;
        private int landedFrame = -1;
        // v0.3.0 animation pass: presentation-only expressive layers.
        // The whole Human_Swat clip spans this many authority strike seconds (normalized clip time t = 1.9 x
        // authority seconds): cocked at t .23, impact at t .47 when the sweep ends at .25 s.
        private const float SwatAuthoritySeconds = 1f / 1.9f;
        private float StrikeClipTimeScale => HasMotion(Ids.HumanSwat) ? MotionDuration(Ids.HumanSwat) / SwatAuthoritySeconds : 1.9f;
        private const float MosquitoTumbleSeconds = .5f;  // Mosquito_Fall's tumble (author_mosquito_motion FALL_TUMBLE_SECONDS)
        // v0.3.0 round 3: 140 deg still read as a straight-arm push in third person; the swat keeps the elbow at
        // or below 120 deg (constant, so the limit never pops between phases) and the clavicle, a forward lean
        // and the torso twist make up the reach. The first-person local human (no torso help) keeps 140 deg.
        public const float StrikeElbowInnerDegrees = 120f, LocalStrikeElbowInnerDegrees = 140f;
        private const float MaximumTorsoAssistDegrees = 24f, MaximumSpineAssistDegrees = 14f, MaximumLeanAssistDegrees = 12f;
        private const float OwnerCrossfadeSeconds = .18f;
        // Entering the gait while the body already moves: the legs join the stride almost at once (no skate),
        // the torso and arms keep the longer blend.
        private const float MovingLegCrossfadeSeconds = .05f;
        private CharacterSecondaryMotion secondary;
        private PoseCrossfade crossfade;
        private readonly GameplayMoodPolicy mood = new GameplayMoodPolicy();
        private VisualAttentionRig moodRig;
        private bool lastOwnerLocomotion, pendingCrossfade;
        private float flightPitch, flightRoll, lastFlightYaw;
        private bool hasFlightYaw;
        private float recoverPlayback = 1f;
        private float bitingSince = -1f;
        private double yawnRequestedUntil = double.MinValue;
        private Transform chestBone, spineBone;
        private bool opponentNear;
        private float nextOpponentCheck;
        // v0.3.0 round 4 (director 1): Human_Crouch is a crouch-amount axis (normalized time = crouch 0..1,
        // author_motion crouch()), scrubbed from the authoritative CrouchFraction instead of being replayed
        // from standing each time the static crouch is (re)entered.
        private const float VisualCrouchRate = 6f;
        private float visualCrouch = -1f;
        /// <summary>Crouch amount the static crouch pose shows (the authoritative CrouchFraction, eased).</summary>
        public float VisualCrouch => Mathf.Max(0f, visualCrouch);
        // A held flyswatter points forward and out beside the thigh (not across the crotch) while it is not swinging.
        private const float ToolCarryDegrees = 75f;
        /// <summary>Degrees the idle wrist turned the held tool outward on the last frame (review).</summary>
        public float LastToolCarryDegrees { get; private set; }
        public float LastArmReachResidualMeters { get; private set; }
        public float LastElbowInnerDegrees { get; private set; } = 180f;
        /// <summary>Reach assists used by the last strike frame (clavicle, chest), for review.</summary>
        public float LastClavicleAssistDegrees { get; private set; }
        public float LastTorsoAssistDegrees { get; private set; }
        public float LastLeanAssistDegrees { get; private set; }
        /// <summary>World goal of the swat effector (palm or tool impact point) on the last solved frame.</summary>
        public Vector3 LastStrikeGoal { get; private set; }
        /// <summary>Distance between the effector and its goal after the last solve (reach residual included).</summary>
        public float LastStrikeEffectorError { get; private set; }
        /// <summary>Time.frameCount of the last frame the swat arm IK solved (diagnostics and tests).</summary>
        public int LastStrikeSolveFrame { get; private set; } = -1;
        public int CurrentMotion => currentMotion;
        public int TemporaryMotion => temporaryMotion;
        public bool UsingLocomotion => usingLocomotion;
        public CharacterSecondaryMotion Secondary => secondary;
        public GameplayMoodPolicy MoodPolicy => mood;
        public Quaternion FlightTilt => Quaternion.Euler(flightPitch, 0f, flightRoll);

        public void BindLocomotion(HumanLocomotionPresenter value)
        {
            if (locomotion) locomotion.Suspend();
            locomotion = value;
            usingLocomotion = false;
            locomotionDiscontinuity = true;
        }

        private bool HasLocomotion => locomotion && locomotion.isActiveAndEnabled && locomotion.IsConfigured;
        // v0.3.0: with the upper-body strike layer and the crouched gait the legs keep walking while
        // striking or crouching; older controllers keep the previous full-body fallback.
        private bool CanUseLocomotion(GameplayModel.ActorSnapshot state) => HasLocomotion &&
            proxy.Role == PlayerRole.Human && state != null && state.Grounded &&
            state.LifeState == GameplayModel.LifeState.Active &&
            (state.CrouchFraction <= 0.1f || locomotion.SupportsCrouchWalk) &&
            (state.StrikeState.Phase == GameplayModel.StrikePhase.None || locomotion.SupportsStrikeLayer) &&
            temporaryMotion < 0 && PlanarSpeed(state.Velocity) > 0.1f;

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
            BindTool(GameplayModel.GameplayTools.Flyswatter, instance);
        }

        public void BindTool(string toolId, GameObject instance)
        {
            if (!GameplayModel.GameplayTools.IsPickup(toolId)) return;
            if (tools.TryGetValue(toolId, out GameObject previous) && previous != null && previous != instance)
                Destroy(previous);
            if (instance == null) tools.Remove(toolId);
            else
            {
                tools[toolId] = instance;
                instance.SetActive(false);
            }
            if (toolId == GameplayModel.GameplayTools.Flyswatter)
                strikeTool = instance != null ? instance.GetComponent<ToolView>() : null;
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
                    chestBone = FindDescendant(view.transform, "Chest");
                    spineBone = FindDescendant(view.transform, "Spine");
                }
                CacheMotionDurations();
                if (view.Animator != null)
                {
                    secondary = new CharacterSecondaryMotion(proxy.Role, view.transform, view.Animator.transform);
                    Transform rigRoot = FindDescendant(view.Animator.transform, "Root");
                    crossfade = new PoseCrossfade(PoseCrossfade.CollectRig(rigRoot), PoseCrossfade.IsLegBone);
                }
            }
        }

        public void ApplySnapshot(GameplayModel.ActorSnapshot state, uint hostTick)
        {
            if (state == null || proxy == null || state.ActorId != proxy.ActorId)
                return;

            if (visualCrouch < 0f) UpdateVisualCrouch(state.CrouchFraction); // a spawn never ramps
            bool discontinuity = current == null || ShouldCut(current, state);
            if (current != null && !current.Grounded && state.Grounded) landedFrame = Time.frameCount;
            ObserveTransition(current, state);
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
            if (proxy.Role == PlayerRole.Human && IsSoftTemporary(temporaryMotion) && InterruptsSoftTemporary(state))
            {
                // Land, Yawn and a standing Swat give way at once to movement: the gait (with its
                // strike layer) takes over instead of sliding a full-body clip across the floor.
                temporaryMotion = -1; temporaryUntil = 0;
            }
            uint tickDelta = unchecked(currentHostTick - previousHostTick);
            snapshotInterval = tickDelta == 0 ? 0.05f : Mathf.Clamp(tickDelta / 30f, 1f / 60f, 0.10f);
            snapshotArrivalTime = Time.unscaledTime;

            foreach (var pair in tools)
                if (pair.Value != null && pair.Value.activeSelf != (state.EquippedToolId == pair.Key))
                    pair.Value.SetActive(state.EquippedToolId == pair.Key);

            if (cut)
                SetWorldPose(state.Position.ToUnity(), state.BodyRotation.ToUnity());
            if (Time.unscaledTime >= temporaryUntil)
                ApplyMotion(state, currentMotion < 0);
        }

        public void ApplyEvent(in GameplayModel.GameplayEvent item)
        {
            if (view == null || proxy == null)
                return;
            bool asSource = item.SourceActorId == ActorId;
            bool asTarget = !asSource && item.TargetActorId != 0 && item.TargetActorId == ActorId;
            if (!asSource && !asTarget)
                return;
            mood.Notify(item.Kind, asSource, asTarget, Time.unscaledTimeAsDouble);
            if (asTarget)
            {
                if (item.Kind == GameplayModel.GameplayEventKind.BiteStarted && proxy.Role == PlayerRole.Human && !localActor)
                    secondary?.Kick(-.06f);   // flinch when bitten
                else if (item.Kind == GameplayModel.GameplayEventKind.StrikeImpact && proxy.Role == PlayerRole.Mosquito)
                    secondary?.KickWobble(18f); // comic rebound of the swatted mosquito
                return;
            }
            int motion = -1;
            float duration = -1f;
            switch (item.Kind)
            {
                case GameplayModel.GameplayEventKind.StrikeStarted:
                    if (proxy.Role == PlayerRole.Human)
                    {
                        if (!localActor) secondary?.Kick(-.045f);
                        bool moving = current != null && PlanarSpeed(current.Velocity) > .1f;
                        if (current != null && current.CrouchFraction > .25f)
                        { temporaryMotion = -1; temporaryUntil = 0; ApplyMotion(current, false); }
                        else if (moving && HasLocomotion && locomotion.SupportsStrikeLayer && current.Grounded &&
                            current.LifeState == GameplayModel.LifeState.Active)
                        {
                            // Legs keep walking; the masked Swat layer and the arm solve carry the stroke.
                            if (IsSoftTemporary(temporaryMotion)) { temporaryMotion = -1; temporaryUntil = 0; }
                        }
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
                    if (proxy.Role == PlayerRole.Mosquito)
                    {
                        // v0.3.0 round 3: the swatted mosquito tumbles at once (Fall: 1.5 turns in 0.5 s, then belly
                        // up) instead of a 0.25 s Hit flinch that left the fall to the last frames before touchdown;
                        // Falling keeps playing the same clip and Stunned then crossfades into StunnedLoop.
                        motion = HasMotion(Ids.MosquitoFall) ? Ids.MosquitoFall : Ids.MosquitoHit;
                        duration = motion == Ids.MosquitoFall ? MosquitoTumbleSeconds : .25f;
                        secondary?.Kick(-.28f); secondary?.KickWobble(24f);
                    }
                    break;
            }
            if (motion == Ids.HumanSwat && proxy.Role == PlayerRole.Human)
                // The Swat clip is authored on the strike timeline scaled by StrikeClipTimeScale (see UpdateLocomotionOverlays).
                PlayTemporary(motion, -1f, StrikeClipTimeScale);
            else if (motion >= 0)
                PlayTemporary(motion, duration);
        }

        private bool IsSoftTemporary(int motion) => proxy != null && proxy.Role == PlayerRole.Human &&
            (motion == Ids.HumanLand || motion == Ids.HumanYawn || (motion == Ids.HumanSwat && HasLocomotion && locomotion.SupportsStrikeLayer));

        private bool InterruptsSoftTemporary(GameplayModel.ActorSnapshot state)
        {
            if (state.LifeState != GameplayModel.LifeState.Active || !state.Grounded) return true;
            if (PlanarSpeed(state.Velocity) > .1f) return true;
            return temporaryMotion == Ids.HumanYawn &&
                (state.CrouchFraction > .25f || state.StrikeState.Phase != GameplayModel.StrikePhase.None);
        }

        /// <summary>Discrete state changes between two authoritative snapshots (landing, take-off, perching).</summary>
        private void ObserveTransition(GameplayModel.ActorSnapshot from, GameplayModel.ActorSnapshot to)
        {
            if (from == null || to == null || view == null) return;
            if (proxy.Role == PlayerRole.Human)
            {
                if (!from.Grounded && to.Grounded && to.LifeState == GameplayModel.LifeState.Active)
                {
                    float impact = Mathf.Abs(Mathf.Min(0f, from.Velocity.Y));
                    if (!localActor) secondary?.Kick(-.17f * Mathf.Clamp01(impact / 5f) - .03f);
                    if (HasMotion(Ids.HumanLand) && PlanarSpeed(to.Velocity) <= .1f && to.CrouchFraction <= .25f &&
                        to.StrikeState.Phase == GameplayModel.StrikePhase.None && temporaryMotion < 0)
                        PlayTemporary(Ids.HumanLand, .45f);
                }
                else if (from.Grounded && !to.Grounded && to.Velocity.Y > .5f && !localActor)
                    secondary?.Kick(.10f);
                return;
            }
            if (from.LifeState == GameplayModel.LifeState.ApproachingSurface && to.LifeState == GameplayModel.LifeState.Surface)
                secondary?.Kick(-.15f);
            else if (from.LifeState == GameplayModel.LifeState.Surface && to.LifeState == GameplayModel.LifeState.Flying)
                secondary?.Kick(.08f);
            else if (from.LifeState == GameplayModel.LifeState.Falling && to.LifeState == GameplayModel.LifeState.Stunned)
                secondary?.Kick(-.2f);
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
            float crouchTarget = current.CrouchFraction;
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
                    if (!localActor) crouchTarget = Mathf.Lerp(previous.CrouchFraction, current.CrouchFraction, t);
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
            UpdateVisualCrouch(crouchTarget);

            if (HasLocomotion)
            {
                bool eligible = CanUseLocomotion(current);
                if (!eligible && usingLocomotion)
                {
                    ReleaseLocomotion();
                    ApplyMotion(current, false);
                }
                UpdateLocomotionOverlays();
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
            // The gait graph and the controller cannot blend into each other: crossfade their switch (and any
            // immediate Play) from the last rendered pose instead of popping.
            if (crossfade != null)
            {
                if (usingLocomotion != lastOwnerLocomotion || pendingCrossfade)
                {
                    bool enteringStride = usingLocomotion && !lastOwnerLocomotion && PlanarSpeed(current.Velocity) > .1f;
                    crossfade.Begin(OwnerCrossfadeSeconds, enteringStride ? MovingLegCrossfadeSeconds : OwnerCrossfadeSeconds);
                }
                lastOwnerLocomotion = usingLocomotion; pendingCrossfade = false;
                crossfade.Apply(Time.deltaTime);
            }
            ScrubCrouchPose();
            ApplySecondary();
            ApplyToolCarry();
            ApplyAuthoritativeHands();
            ApplyMood();
            view.RefreshAnchors();
            if (HasLocomotion) locomotion.PublishContacts(Time.frameCount, landedFrame == Time.frameCount);
            ApplyBiteAnchor();
        }

        private void UpdateLocomotionOverlays()
        {
            float crouch = Mathf.Clamp01((current.CrouchFraction - .1f) / .4f);
            var strike = current.StrikeState;
            float weight = 0f, seconds = 0f;
            if (strike.Phase != GameplayModel.StrikePhase.None)
            {
                float elapsed = GameplayModel.StrikeVisualTrajectory.Elapsed(strike) +
                    Mathf.Clamp(Time.unscaledTime - snapshotArrivalTime, 0f, snapshotInterval);
                weight = 1f; seconds = elapsed * StrikeClipTimeScale;
            }
            locomotion.SetOverlayState(crouch, weight, seconds);
        }

        private void ApplySecondary()
        {
            if (secondary == null) return;
            bool biting = proxy.Role == PlayerRole.Mosquito && current.LifeState == GameplayModel.LifeState.Biting;
            if (biting && bitingSince < 0) bitingSince = Time.unscaledTime;
            if (!biting) bitingSince = -1f;
            secondary.Apply(new CharacterSecondaryMotion.Input
            {
                DeltaSeconds = Time.deltaTime,
                GaitActive = usingLocomotion && HasLocomotion,
                GaitPhase = HasLocomotion ? locomotion.Current.Phase : 0,
                PlanarSpeed = PlanarSpeed(current.Velocity),
                Biting = biting,
                BitingSeconds = biting ? Time.unscaledTime - bitingSince : 0f,
                CameraOnBody = localActor && proxy.Role == PlayerRole.Human
            });
        }

        private void ApplyMood()
        {
            if (!moodRig && view) moodRig = view.GetComponent<VisualAttentionRig>();
            double now = Time.unscaledTimeAsDouble;
            if (proxy.Role == PlayerRole.Mosquito && Time.unscaledTime >= nextOpponentCheck)
            {
                nextOpponentCheck = Time.unscaledTime + .25f;
                opponentNear = OpponentWithin(2f);
            }
            var result = mood.Evaluate(new GameplayMoodPolicy.Frame(proxy.Role, current.LifeState, PlanarSpeed(current.Velocity),
                current.Grounded, current.CrouchFraction, current.StrikeState.Phase != GameplayModel.StrikePhase.None,
                opponentNear), now);
            if (moodRig && moodRig.IsConfigured)
            {
                // The first-person camera rides on the local human's head: its moods never pitch or roll it.
                moodRig.MoodHeadPoseEnabled = !(localActor && proxy.Role == PlayerRole.Human);
                moodRig.SetMood(result.Mood, result.Weight, result.Mood == FacialMood.Yawning ? .35f : .12f);
            }
            // The policy opens a yawn on one evaluation; the body clip keeps retrying while the face still yawns
            // (a Land or another short temporary may hold the body for a moment).
            if (result.StartYawn) yawnRequestedUntil = now + 2.0;
            if (now < yawnRequestedUntil && result.Mood == FacialMood.Yawning && !localActor && proxy.Role == PlayerRole.Human &&
                HasMotion(Ids.HumanYawn) && temporaryMotion < 0 && !usingLocomotion)
            {
                PlayTemporary(Ids.HumanYawn);
                yawnRequestedUntil = double.MinValue;
            }
            else if (result.Mood != FacialMood.Yawning) yawnRequestedUntil = double.MinValue;
        }

        private bool OpponentWithin(float meters)
        {
            if (!world || world.Actors == null) return false;
            Vector3 origin = transform.position, forward = transform.forward;
            foreach (var pair in world.Actors)
            {
                var other = pair.Value;
                if (!other || other == proxy || other.Role == proxy.Role || !other.gameObject.activeInHierarchy) continue;
                Vector3 offset = other.transform.position + Vector3.up * .9f - origin;
                float distance = offset.magnitude;
                if (distance < meters && (distance < .01f || Vector3.Dot(forward, offset / distance) > .2f)) return true;
            }
            return false;
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

            int previousMotion = currentMotion;
            currentMotion = motion;
            if (IsRecover(motion)) PrepareRecover(state, motion);
            ApplyAnimatorSpeed(state, motion);
            if (IsFlightLoop(motion) && view.Animator != null &&
                TryGetMotion(motion, out CharacterView.MotionBinding flightBinding))
            {
                // Wingbeats keep their phase between Hover and Fly; a fresh flight starts at a per-actor phase.
                float phase = MosquitoWingbeat.InitialPhase(ActorId);
                if (IsFlightLoop(previousMotion))
                    phase = Mathf.Repeat(view.Animator.GetCurrentAnimatorStateInfo(0).normalizedTime, 1f);
                float fade = immediate ? 0f : CrossFadeSeconds(state);
                view.PlayMotion(motion, fade);
                view.Animator.CrossFadeInFixedTime(flightBinding.StateName, fade, 0, phase * MotionDuration(motion));
                if (immediate) pendingCrossfade = true;
                return;
            }
            if (IsRecover(motion) && view.Animator != null && TryGetMotion(motion, out CharacterView.MotionBinding recoverBinding))
            {
                // The mosquito's 0.4 s Recover starts on the knocked-out belly-up pose: a short fade keeps its roll
                // (director r4: a .14 s fade swallowed most of it and the roll read as a 2-frame snap).
                float recoverFade = proxy.Role == PlayerRole.Mosquito ? .05f : .14f;
                view.PlayMotion(motion, recoverFade);
                view.Animator.CrossFadeInFixedTime(recoverBinding.StateName, recoverFade, 0, recoverStart * MotionDuration(motion));
                return;
            }
            if (IsStaticCrouch(motion) && view.Animator != null && TryGetMotion(motion, out CharacterView.MotionBinding crouchBinding))
            {
                // Enter the crouch axis at the crouch the body already has (1 when stopping a sneak): never replay
                // the stand-to-crouch ramp, which popped the body (and the first-person eye) upright for ~0.5 s.
                // Coming off the gait graph the restored controller sits on its default (standing Idle) state: an
                // Animator crossfade from there would lift the body for its duration, so the crouch is played at
                // once and the frozen-pose crossfade blends from the last rendered (sneaking) pose instead.
                bool atOnce = immediate || previousMotion < 0;
                float crouchFade = atOnce ? 0f : CrossFadeSeconds(state);
                view.PlayMotion(motion, crouchFade);
                view.Animator.CrossFadeInFixedTime(crouchBinding.StateName, crouchFade, 0, CrouchPoseTime() * MotionDuration(motion));
                if (atOnce) pendingCrossfade = true;
                return;
            }
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
                pendingCrossfade = true;
                return;
            }
            view.PlayMotion(motion, CrossFadeSeconds(state));
        }

        private float recoverStart;
        private bool IsRecover(int motion) => proxy != null &&
            (proxy.Role == PlayerRole.Human ? motion == Ids.HumanRecover : motion == Ids.MosquitoRecover);
        private bool IsFlightLoop(int motion) => proxy != null && proxy.Role == PlayerRole.Mosquito &&
            (motion == Ids.MosquitoHover || motion == Ids.MosquitoFly);

        /// <summary>
        /// Recovering lasts ~0.4 s in the authority but the Recover clips 1.2-2.0 s: play the end of the clip,
        /// up to 3x faster, so it finishes at RecoveryEndTick instead of being cut back to Idle mid-way.
        /// </summary>
        private void PrepareRecover(GameplayModel.ActorSnapshot state, int motion)
        {
            float length = MotionDuration(motion);
            float remaining = state.RecoveryEndTick > currentHostTick ? (state.RecoveryEndTick - currentHostTick) / 30f : .4f;
            remaining = Mathf.Clamp(remaining, .1f, length);
            recoverPlayback = Mathf.Clamp(length / remaining, 1f, 3f);
            recoverStart = Mathf.Clamp01(1f - remaining * recoverPlayback / Mathf.Max(.001f, length));
        }

        private bool IsStaticCrouch(int motion) => proxy != null && proxy.Role == PlayerRole.Human && motion == Ids.HumanCrouch;

        /// <summary>Normalized time of the crouch axis clip for the crouch the body shows now.</summary>
        private float CrouchPoseTime() => Mathf.Clamp01(VisualCrouch);

        private void UpdateVisualCrouch(float target)
        {
            if (float.IsNaN(target) || float.IsInfinity(target)) target = 0f;
            target = Mathf.Clamp01(target);
            // The authority ramps crouching at 5/s in 30 Hz steps; the pose follows a little faster, smoothly.
            visualCrouch = visualCrouch < 0f || float.IsNaN(visualCrouch) ? target
                : Mathf.MoveTowards(visualCrouch, target, Time.deltaTime * VisualCrouchRate);
        }

        /// <summary>
        /// Holds the static crouch on the crouch axis: the Animator plays the state at normal speed (so its
        /// crossfades run), and whenever it is settled on it the time is set back to the crouch amount, so a
        /// crouched body stays crouched however long it stands still and follows crouching down or up.
        /// </summary>
        private void ScrubCrouchPose()
        {
            if (!IsStaticCrouch(currentMotion) || temporaryMotion >= 0 || usingLocomotion || view.Animator == null ||
                !view.Animator.isActiveAndEnabled || view.Animator.runtimeAnimatorController == null ||
                view.Animator.IsInTransition(0) || !TryGetMotion(currentMotion, out CharacterView.MotionBinding binding))
                return;
            AnimatorStateInfo info = view.Animator.GetCurrentAnimatorStateInfo(0);
            if (!info.IsName(binding.StateName)) return;
            float wanted = CrouchPoseTime();
            if (Mathf.Abs(Mathf.Min(1f, info.normalizedTime) - wanted) > .002f)
                view.Animator.Play(binding.StateName, 0, wanted);
        }

        /// <summary>
        /// Director r4 (6): while it is not swinging, the held flyswatter sticks out of the fist (thumb side, up and
        /// forward) across the front of the crotch. With the arm hanging, the forearm twists (a roll about its own
        /// axis, up to 75 deg, a natural pronation/supination range) so the handle points forward and out and the
        /// net sits in front of and outside the thigh. The twist fades as the swat's pose weight rises (the arm
        /// solve then owns the hand) and when a clip raises the forearm. The tool stays rigidly in the hand (it
        /// follows the hand's grip socket).
        /// </summary>
        private void ApplyToolCarry()
        {
            LastToolCarryDegrees = 0f;
            if (proxy.Role != PlayerRole.Human || rightArm == null || !rightArm.IsValid || !strikeTool ||
                !strikeTool.gameObject.activeInHierarchy || !strikeTool.Grip || !strikeTool.Impact)
                return;
            float swing = current.StrikeState.Phase != GameplayModel.StrikePhase.None ? StrikeSwingPath.Weight(StrikeElapsed()) : 0f;
            float weight = 1f - swing;
            if (weight <= .001f) return;
            Vector3 forearm = rightArm.Hand.position - rightArm.Lower.position;
            if (!Finite(forearm) || forearm.sqrMagnitude < 1e-6f) return;
            forearm.Normalize();
            float hanging = Mathf.Clamp01((Vector3.Dot(forearm, -transform.up) - .55f) / .3f);
            if (hanging <= .001f) return;
            view.RefreshAnchors();
            Vector3 along = strikeTool.Impact.position - strikeTool.Grip.position;
            if (!Finite(along) || along.sqrMagnitude < 1e-6f) return;
            Vector3 wanted = transform.forward * .55f + transform.right * .85f;
            Vector3 from = Vector3.ProjectOnPlane(along, forearm), to = Vector3.ProjectOnPlane(wanted, forearm);
            if (from.sqrMagnitude < 1e-6f || to.sqrMagnitude < 1e-6f) return;
            float degrees = Mathf.Clamp(Vector3.SignedAngle(from, to, forearm), -ToolCarryDegrees, ToolCarryDegrees) * weight * hanging;
            if (Mathf.Abs(degrees) < .01f) return;
            rightArm.Hand.rotation = Quaternion.AngleAxis(degrees, forearm) * rightArm.Hand.rotation;
            LastToolCarryDegrees = Mathf.Abs(degrees);
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

        private void PlayTemporary(int motion, float durationOverride = -1f, float speed = 1f)
        {
            ReleaseLocomotion();
            temporaryMotion = motion;
            speed = Mathf.Clamp(float.IsNaN(speed) ? 1f : speed, .25f, 4f);
            float clip = MotionDuration(motion) / speed;
            float duration = durationOverride > 0f ? Mathf.Min(durationOverride, clip) : clip;
            temporaryUntil = Time.unscaledTime + Mathf.Clamp(duration, 0.08f, 3.2f);
            currentMotion = motion;
            if (view.Animator != null)
                view.Animator.speed = speed;
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
            if (IsFlightLoop(motion))
            {
                // 8-12 Hz from flight speed, independent of the distance travelled (was 2.6-18.75 Hz).
                view.Animator.speed = MosquitoWingbeat.Playback(state.Velocity.Length, MotionDuration(motion));
                return;
            }
            if (IsRecover(motion))
            {
                view.Animator.speed = recoverPlayback;
                return;
            }
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
            // Mosquito wingbeats no longer follow distance (Fly used to): only SurfaceWalk steps do.
            return proxy.Role == PlayerRole.Human
                ? motion == 1 || motion == 2
                : motion == 6;
        }

        private bool HasMotion(int id) => TryGetMotion(id, out _);

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
                if (!state.Grounded)
                {
                    // Never the faint clip (Fall) for an ordinary jump: rising tuck, then falling flail.
                    if (!HasMotion(Ids.HumanJumpAir)) return Ids.HumanJump;
                    return state.Velocity.Y > 0.10f || !HasMotion(Ids.HumanFallAir) ? Ids.HumanJumpAir : Ids.HumanFallAir;
                }
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
                // Dizzy on its back; without the loop the end pose of Fall is held (never the Hit pose).
                case GameplayModel.LifeState.Stunned: return HasMotion(Ids.MosquitoStunnedLoop) ? Ids.MosquitoStunnedLoop : Ids.MosquitoFall;
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
            visualCrouch = -1f;
            secondary?.Reset();
            crossfade?.Cancel();
        }

        private void ApplyAuthoritativeHands()
        {
            if (proxy.Role != PlayerRole.Human ||
                current.StrikeState.Phase == GameplayModel.StrikePhase.None)
                return;
            float elapsed = StrikeElapsed();
            if (current.StrikeState.Hand < 0)
                AlignArm(leftArm, 5u, -1f, elapsed);
            else
                AlignArm(rightArm, 6u, 1f, elapsed);
        }

        /// <summary>Authority strike seconds, continuous between the ~30 Hz snapshots.</summary>
        private float StrikeElapsed() => Mathf.Min(GameplayModel.StrikeVisualTrajectory.Duration,
            GameplayModel.StrikeVisualTrajectory.Elapsed(current.StrikeState) +
            Mathf.Clamp(Time.unscaledTime - snapshotArrivalTime, 0f, snapshotInterval));

        private ArmChain FindArm(string side) => new ArmChain
        {
            Shoulder = FindDescendant(view.transform, "Shoulder." + side),
            Upper = FindDescendant(view.transform, "UpperArm." + side),
            Lower = FindDescendant(view.transform, "LowerArm." + side),
            Hand = FindDescendant(view.transform, "Hand." + side),
            Palm = FindDescendant(view.transform, "Socket.Grip." + side)
        };

        /// <summary>Where the arm solve must put its end this frame (see StrikeSwingPath).</summary>
        private struct SwingSolve
        {
            public Transform End;
            public Vector3 EndTarget;
            public Quaternion HandRotation;
            public bool RotatesHand;
            public float Reach;
        }

        /// <summary>
        /// v0.3.0 round 3 (anim-r3): the swat follows StrikeSwingPath (cocked above the ear, a diagonal whip
        /// into the authoritative target, follow-through across the body) instead of the authority's straight
        /// shoulder-to-target line, with the elbow held at or below 120 deg in third person (140 deg for the
        /// first-person local human, whose torso cannot help). The clavicle, a forward lean of the waist and
        /// the chest/waist twist make up the rest of the reach, so the palm (bare hand) or the flyswatter's
        /// impact point arrives on the authoritative target when the sweep ends. The held tool stays rigidly
        /// mounted to the hand: the hand is rotated (radial alignment plus a wrist snap), never detached.
        /// </summary>
        private void AlignArm(ArmChain arm, uint localSurfaceId, float side, float elapsed)
        {
            if (arm == null || !arm.IsValid || !proxy.BodySurfaces.TryGetValue(
                    proxy.ActorId * 100 + localSurfaceId, out GameplayBodySurface forearm) ||
                !(forearm.Collider is CapsuleCollider))
                return;
            float weight = StrikeSwingPath.Weight(elapsed);
            if (weight <= .001f) return;
            bool hasTool = HoldsStrikeTool();
            view.RefreshAnchors();
            Transform effector = hasTool ? strikeTool.Impact : arm.Palm ? arm.Palm : arm.Hand;
            Vector3 shoulder = arm.Upper.position, rest = effector.position;
            Vector3 up = transform.up, forward = transform.forward, outward = transform.right * side;
            Vector3 target = current.StrikeState.Target.ToUnity();
            if (!Finite(target) || !Finite(shoulder) || !Finite(rest)) return;
            Vector3 cocked = hasTool
                ? shoulder + up * .70f + outward * .16f - forward * .24f
                : shoulder + up * .40f + outward * .14f - forward * .10f;
            Vector3 follow = hasTool
                ? shoulder - up * .50f - outward * .40f + forward * .45f
                : shoulder - up * .55f - outward * .32f + forward * .28f;
            // The hand cocks at crown height and the arc bulges up and well out to the striking side, so the whip
            // comes over and around the head instead of dragging across the eyes. Director r4 (6): the flyswatter
            // swings a little wider and its net rolls edge-on while it passes the head (StrikeSwingPath.ToolRoll).
            Vector3 bulge = hasTool ? up * StrikeSwingPath.ToolBulgeUp + outward * StrikeSwingPath.ToolBulgeOut
                : up * StrikeSwingPath.HandBulgeUp + outward * StrikeSwingPath.HandBulgeOut;
            Vector3 path = StrikeSwingPath.Evaluate(elapsed, cocked, target, follow, bulge);
            Vector3 goal = Vector3.Lerp(rest, path, weight);
            Vector3 sweepAxis = Vector3.Cross(cocked - shoulder, target - shoulder);
            sweepAxis = sweepAxis.sqrMagnitude > 1e-8f ? sweepAxis.normalized : outward;
            float wrist = StrikeSwingPath.WristDegrees(elapsed) * weight;
            float limit = localActor ? LocalStrikeElbowInnerDegrees : StrikeElbowInnerDegrees;
            LastStrikeGoal = goal;
            // Solve the arm, then let the clavicle and the torso close whatever the bent arm cannot reach: every
            // assist is driven by the residual of a real solve (the palm is rigid with a flexed hand, so its reach
            // cannot be predicted from the clip pose), and the arm is re-solved after each joint turns.
            bool solvable = true;
            float Residual()
            {
                // Two passes: the palm's offset from the wrist (and the tool's hand rotation) is re-measured once
                // the forearm has turned.
                for (int pass = 0; pass < 2 && solvable; pass++)
                {
                    SwingSolve solve = Swing(arm, goal, hasTool, weight, wrist, sweepAxis, effector, limit);
                    if (!SolveWrist(arm, solve, outward, up)) { solvable = false; break; }
                    if (solve.RotatesHand) arm.Hand.rotation = solve.HandRotation;
                }
                if (hasTool) view.RefreshAnchors();
                return solvable ? Vector3.Distance(effector.position, goal) : 0f;
            }
            AssistReach(arm, weight, goal, Residual);
            Residual();
            if (!solvable) return;
            if (hasTool)
            {
                // Forearm roll about the handle, once, after the solve: the tool keeps its direction and stays in the
                // hand, the net turns edge-on mid-sweep (its centre leaves the arc by up to ~15 cm there; the roll is
                // 0 at the impact, where the net is on the authoritative target).
                float roll = StrikeSwingPath.ToolRoll(elapsed) * weight;
                view.RefreshAnchors();
                Vector3 handle = strikeTool.Impact.position - strikeTool.Grip.position;
                if (Mathf.Abs(roll) > .01f && handle.sqrMagnitude > 1e-6f)
                    arm.Hand.rotation = Quaternion.AngleAxis(roll * side, handle.normalized) * arm.Hand.rotation;
            }
            LastElbowInnerDegrees = TwoBoneSolver.InnerAngle(arm.Upper.position, arm.Lower.position, arm.Hand.position);
            LastStrikeSolveFrame = Time.frameCount;
            view.RefreshAnchors();
            LastStrikeEffectorError = Vector3.Distance(effector.position, goal);
        }

        /// <summary>Analytic two-bone solve of the real wrist onto the solve's wrist target, the elbow inner angle
        /// capped by the solve's reach; segments rotate about their joints and are never stretched.</summary>
        private bool SolveWrist(ArmChain arm, SwingSolve solve, Vector3 outward, Vector3 up)
        {
            Vector3 root = arm.Upper.position, elbow = arm.Lower.position, wrist = arm.Hand.position;
            float upperLength = Vector3.Distance(root, elbow), lowerLength = Vector3.Distance(elbow, wrist);
            if (!Finite(solve.EndTarget) || upperLength < .001f || lowerLength < .001f) return false;
            Vector3 delta = solve.EndTarget - root;
            Vector3 axis = delta.sqrMagnitude > .000001f ? delta.normalized : (wrist - root).normalized;
            if (axis.sqrMagnitude < .5f) axis = transform.forward;
            float length = Mathf.Clamp(delta.magnitude, Mathf.Abs(upperLength - lowerLength) + .0001f,
                Mathf.Max(Mathf.Abs(upperLength - lowerLength) + .0001f, solve.Reach));
            LastArmReachResidualMeters = Mathf.Max(0f, delta.magnitude - length);
            Vector3 bend = Vector3.ProjectOnPlane(elbow - root, axis);
            if (bend.sqrMagnitude < .000001f) bend = Vector3.ProjectOnPlane(outward - transform.forward * .3f, axis);
            if (bend.sqrMagnitude < .000001f) bend = Vector3.ProjectOnPlane(up, axis);
            if (bend.sqrMagnitude < .000001f) bend = Vector3.ProjectOnPlane(transform.forward, axis);
            float along = (upperLength * upperLength - lowerLength * lowerLength + length * length) / (2 * length);
            Vector3 solvedElbow = root + axis * along + bend.normalized *
                Mathf.Sqrt(Mathf.Max(0, upperLength * upperLength - along * along));
            Vector3 reachable = root + axis * length;
            arm.Upper.rotation = Quaternion.FromToRotation(elbow - root, solvedElbow - root) * arm.Upper.rotation;
            arm.Lower.rotation = Quaternion.FromToRotation(arm.Hand.position - arm.Lower.position,
                reachable - arm.Lower.position) * arm.Lower.rotation;
            return true;
        }

        /// <summary>
        /// End of the arm solve for the current shoulder: the palm itself for a bare-hand slap, or the wrist
        /// short of the goal by the held tool, whose hand rotation (radial from the shoulder, plus the wrist
        /// snap about the sweep axis) is returned too. Reach keeps the elbow inner angle at the limit.
        /// </summary>
        private SwingSolve Swing(ArmChain arm, Vector3 goal, bool hasTool, float weight, float wristDegrees,
            Vector3 sweepAxis, Transform effector, float elbowLimit)
        {
            // The tool hangs on a presentation anchor: refresh it after every assist moved the hand bones.
            if (hasTool) view.RefreshAnchors();
            Vector3 shoulder = arm.Upper.position, elbow = arm.Lower.position, wrist = arm.Hand.position;
            float upperLength = Vector3.Distance(shoulder, elbow), forearmLength = Vector3.Distance(elbow, wrist);
            var result = new SwingSolve { HandRotation = arm.Hand.rotation };
            if (hasTool)
            {
                Vector3 offset = effector.position - wrist;
                Vector3 direction = GameplayModel.StrikeVisualTrajectory.ToolOffset((goal - shoulder).ToFloat(),
                    offset.magnitude, Mathf.Abs(upperLength - forearmLength) + .0001f, (elbow - shoulder).ToFloat()).ToUnity();
                result.End = arm.Hand;
                result.EndTarget = goal;
                if (offset.sqrMagnitude > .000001f && direction.sqrMagnitude > .000001f)
                {
                    direction = Quaternion.AngleAxis(wristDegrees, sweepAxis) * direction;
                    Quaternion aligned = Quaternion.FromToRotation(offset, direction) * arm.Hand.rotation;
                    Quaternion desired = Quaternion.Slerp(arm.Hand.rotation, aligned, weight);
                    result.EndTarget = goal - desired * Quaternion.Inverse(arm.Hand.rotation) * offset;
                    result.HandRotation = desired;
                    result.RotatesHand = true;
                }
                result.Reach = Mathf.Min(upperLength + forearmLength - .0001f,
                    TwoBoneSolver.MaximumReach(upperLength, forearmLength, elbowLimit));
                return result;
            }
            // The palm is rigid with the hand (the clip's wrist is kept): aim the wrist short of the goal by the
            // palm's current offset, so the elbow limit applies to the real shoulder-elbow-wrist angle.
            result.End = arm.Hand;
            result.EndTarget = goal - (effector.position - wrist);
            result.Reach = Mathf.Min(upperLength + forearmLength - .0001f,
                TwoBoneSolver.MaximumReach(upperLength, forearmLength, elbowLimit));
            return result;
        }

        /// <summary>
        /// A swat beyond the bent-elbow reach is made up first by the clavicle (up to 20 deg toward the goal),
        /// then, in third person only, by the waist leaning forward (up to 12 deg on top of the clip's lean),
        /// the chest twisting (up to 24 deg) and the waist twisting (up to 14 deg), all weighted like the
        /// stroke. Each joint turns the shoulder toward the goal by the residual left after solving the arm.
        /// The first-person eye rides on the head, so the local human only uses the clavicle.
        /// </summary>
        private void AssistReach(ArmChain arm, float weight, Vector3 goal, System.Func<float> residual)
        {
            LastClavicleAssistDegrees = LastTorsoAssistDegrees = LastLeanAssistDegrees = 0f;
            if (weight <= .001f) return;
            if (arm.Shoulder && arm.Upper.IsChildOf(arm.Shoulder))
                LastClavicleAssistDegrees = TurnToward(arm.Shoulder, arm, goal, 20f * weight, residual, Vector3.zero, false);
            if (localActor) return;
            if (spineBone && arm.Upper.IsChildOf(spineBone))
                LastLeanAssistDegrees = TurnToward(spineBone, arm, goal, MaximumLeanAssistDegrees * weight, residual, transform.right, true);
            if (chestBone && arm.Upper.IsChildOf(chestBone))
                LastTorsoAssistDegrees = TurnToward(chestBone, arm, goal, MaximumTorsoAssistDegrees * weight, residual, transform.up, false);
            if (spineBone && chestBone && chestBone.IsChildOf(spineBone))
                LastTorsoAssistDegrees += TurnToward(spineBone, arm, goal, MaximumSpineAssistDegrees * weight, residual, transform.up, false);
        }

        /// <summary>
        /// Turns a joint so the shoulder swings toward the goal by the arc the solved arm is still short
        /// (at most maximumDegrees, a few passes); with an axis the turn is about it only, and forwardOnly
        /// (the waist lean) never pitches back.
        /// </summary>
        private static float TurnToward(Transform joint, ArmChain arm, Vector3 goal, float maximumDegrees,
            System.Func<float> residual, Vector3 axis, bool forwardOnly)
        {
            bool aboutAxis = axis.sqrMagnitude > .5f;
            float used = 0f;
            for (int pass = 0; pass < 3 && used < maximumDegrees - .01f; pass++)
            {
                float deficit = residual();
                Vector3 from = arm.Upper.position - joint.position, to = goal - joint.position;
                if (aboutAxis) { from = Vector3.ProjectOnPlane(from, axis); to = Vector3.ProjectOnPlane(to, axis); }
                if (deficit <= .002f || from.sqrMagnitude < 1e-6f || to.sqrMagnitude < 1e-6f) return used;
                Vector3 turnAxis;
                if (aboutAxis)
                {
                    float signed = Vector3.SignedAngle(from, to, axis);
                    if (forwardOnly && signed <= .01f) return used;
                    turnAxis = signed >= 0 ? axis.normalized : -axis.normalized;
                }
                else
                {
                    turnAxis = Vector3.Cross(from, to);
                    if (turnAxis.sqrMagnitude < 1e-10f) return used;
                    turnAxis.Normalize();
                }
                float angle = Mathf.Min(maximumDegrees - used, Vector3.Angle(from, to), deficit / from.magnitude * Mathf.Rad2Deg);
                if (angle <= .01f) return used;
                joint.rotation = Quaternion.AngleAxis(angle, turnAxis) * joint.rotation;
                used += angle;
            }
            return used;
        }

        private bool HoldsStrikeTool() => GameplayModel.GameplayTools.IsFlyswatter(current.StrikeState.ToolId) &&
            strikeTool && strikeTool.Impact && strikeTool.Grip;

        private void SetWorldPose(Vector3 position, Quaternion rotation)
        {
            rotation = ResolveVisualRotation(rotation);
            transform.SetPositionAndRotation(position + HoverBob(), rotation);
        }

        /// <summary>Remote hovering mosquitoes bob 12 mm at 0.8 Hz (the local camera follows its own body anyway).</summary>
        private Vector3 HoverBob()
        {
            if (localActor || proxy == null || proxy.Role != PlayerRole.Mosquito || current == null ||
                current.LifeState != GameplayModel.LifeState.Flying) return Vector3.zero;
            float hover = 1f - Mathf.Clamp01(current.Velocity.Length / 1.2f);
            float phase = MosquitoWingbeat.InitialPhase(ActorId ^ 0x5bd1e995u) * 2f * Mathf.PI;
            return Vector3.up * (.012f * hover * Mathf.Sin(Time.unscaledTime * 2f * Mathf.PI * .8f + phase));
        }

        /// <summary>
        /// Flight attitude (presentation only): nose down with forward speed (up to 18 deg), nose up when
        /// climbing, and banking into turns from the yaw rate (up to 28 deg). Eased with 1 - exp(-10 dt).
        /// </summary>
        private Quaternion ResolveFlightTilt(Quaternion bodyRotation, float elapsed)
        {
            float pitchTarget = 0f, rollTarget = 0f;
            float yaw = bodyRotation.eulerAngles.y;
            if (current.LifeState == GameplayModel.LifeState.Flying)
            {
                Vector3 velocity = current.Velocity.ToUnity();
                float forward = Vector3.Dot(velocity, bodyRotation * Vector3.forward);
                pitchTarget = Mathf.Clamp01(forward / MosquitoWingbeat.MaximumSpeed) * 18f -
                    Mathf.Clamp(velocity.y / MosquitoWingbeat.MaximumSpeed, -1f, 1f) * 8f;
                if (hasFlightYaw && elapsed > 1e-4f)
                {
                    float yawRate = Mathf.DeltaAngle(lastFlightYaw, yaw) / elapsed;
                    rollTarget = Mathf.Clamp(-yawRate * .08f, -28f, 28f);
                }
            }
            if (elapsed > 1e-4f || !hasFlightYaw) { lastFlightYaw = yaw; hasFlightYaw = true; }
            float blend = 1f - Mathf.Exp(-10f * elapsed);
            flightPitch = Mathf.Lerp(flightPitch, pitchTarget, blend);
            flightRoll = Mathf.Lerp(flightRoll, rollTarget, blend);
            if (float.IsNaN(flightPitch) || float.IsNaN(flightRoll)) flightPitch = flightRoll = 0f;
            return bodyRotation * Quaternion.Euler(flightPitch, 0f, flightRoll);
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
                Vector3 stableUp = current.BodyRotation.Up.ToUnity();
                Vector3 biteUp = Vector3.ProjectOnPlane(stableUp, biteForward);
                if (biteUp.sqrMagnitude < .000001f) biteUp = Vector3.ProjectOnPlane(Vector3.up, biteForward);
                if (biteUp.sqrMagnitude < .000001f) biteUp = Vector3.ProjectOnPlane(Vector3.forward, biteForward);
                visualRotation = Quaternion.LookRotation(biteForward, biteUp.normalized);
                returningFromSurface = true; // Reuse smooth visual release on detach to flight.
                flightPitch = flightRoll = 0f; hasFlightYaw = false;
            }
            else if (supported && world.ResolveSurface(current.SurfaceAttachment.Value, out var contact) &&
                TrySurfaceHeading(contact.WorldNormal, elapsed, out var up, out var forward))
            {
                Quaternion target = Quaternion.LookRotation(forward.ToUnity(), up.ToUnity());
                // Ease the approach tilt; attached feet must face the actual surface.
                visualRotation = !hasVisualRotation || current.LifeState == GameplayModel.LifeState.Surface
                    ? target : Quaternion.Slerp(visualRotation, target, 1f - Mathf.Exp(-24f * elapsed));
                returningFromSurface = true;
                flightPitch = flightRoll = 0f; hasFlightYaw = false;
            }
            else if (hasVisualRotation && returningFromSurface && current.LifeState == GameplayModel.LifeState.Flying)
            {
                Quaternion flight = ResolveFlightTilt(bodyRotation, elapsed);
                visualRotation = Quaternion.Slerp(visualRotation, flight, 1f - Mathf.Exp(-24f * elapsed));
                if (Quaternion.Angle(visualRotation, flight) < .1f)
                {
                    visualRotation = flight;
                    returningFromSurface = false;
                }
            }
            else
            {
                visualRotation = ResolveFlightTilt(bodyRotation, elapsed);
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
            bool valid = GameplayModel.SurfaceVisualFrame.TryResolve(normal, current.BodyRotation.Forward,
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
