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
        private Transform leftHand;
        private Transform rightHand;
        private GameObject flyswatter;
        private readonly float[] motionDurations = new float[32];

        public uint ActorId => proxy != null ? proxy.ActorId : 0;
        public CharacterView View => view;

        public void BindFlyswatter(GameObject instance)
        {
            flyswatter = instance;
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
                leftHand = FindDescendant(view.transform, "Hand.L");
                rightHand = FindDescendant(view.transform, "Hand.R");
                CacheMotionDurations();
            }
        }

        public void ApplySnapshot(GameplayModel.ActorSnapshot state, uint hostTick)
        {
            if (state == null || proxy == null || state.ActorId != proxy.ActorId)
                return;

            bool cut = current == null || localActor || ShouldCut(current, state);
            previous = cut ? state : current;
            previousHostTick = cut ? hostTick : currentHostTick;
            current = state;
            currentHostTick = hostTick;
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
                    if (proxy.Role == PlayerRole.Human) motion = 12;
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
                return;

            if (localActor)
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

            if (temporaryMotion >= 0 && Time.unscaledTime >= temporaryUntil)
            {
                temporaryMotion = -1;
                ApplyMotion(current, false);
            }

            ApplyAuthoritativeHands();
            view.RefreshAnchors();
            ApplyBiteAnchor();
        }

        private void ApplyMotion(GameplayModel.ActorSnapshot state, bool immediate)
        {
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
                if (state.StrikeState.Phase != GameplayModel.StrikePhase.None) return 12;
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
            if (proxy.Role != PlayerRole.Mosquito || !current.BiteAttachment.HasValue || world == null)
                return;
            GameplayModel.BiteAttachment attachment = current.BiteAttachment.Value;
            if (!world.Actors.TryGetValue(attachment.VictimId, out GameplayActorProxy victim) ||
                victim.State == null || victim.State.PoseRevision != attachment.PoseRevision ||
                !victim.BodySurfaces.TryGetValue(attachment.SurfaceId, out GameplayBodySurface surface))
                return;
            Transform tip = view.GetAnchor("ProboscisTip");
            if (tip == null)
                return;

            Vector3 target = surface.transform.TransformPoint(attachment.LocalPoint.ToUnity());
            Vector3 correction = target - tip.position;
            if (!warnedBiteOffset && correction.sqrMagnitude > 0.0225f)
            {
                warnedBiteOffset = true;
                Debug.LogError($"LMS_BITE_VISUAL_OFFSET actor={ActorId} meters={correction.magnitude:F4}", this);
            }
            transform.position += correction;
            view.RefreshAnchors();
        }

        private void ApplyAuthoritativeHands()
        {
            if (proxy.Role != PlayerRole.Human ||
                current.StrikeState.Phase == GameplayModel.StrikePhase.None)
                return;
            if (current.StrikeState.Hand < 0)
                AlignHand(leftHand, 5u);
            else
                AlignHand(rightHand, 6u);
        }

        private void AlignHand(Transform hand, uint localSurfaceId)
        {
            if (hand == null || !proxy.BodySurfaces.TryGetValue(
                    proxy.ActorId * 100 + localSurfaceId, out GameplayBodySurface forearm) ||
                forearm.Collider == null)
                return;
            var capsule = forearm.Collider as CapsuleCollider;
            if (capsule == null)
                return;
            float endpoint = Mathf.Max(0f, capsule.height * 0.5f - capsule.radius);
            hand.position = forearm.transform.TransformPoint(Vector3.up * endpoint);
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
            if (supported && world.ResolveSurface(current.SurfaceAttachment.Value, out var contact) &&
                GameplayModel.SurfaceVisualFrame.TryResolve(contact.WorldNormal,
                    (bodyRotation * Vector3.forward).ToFloat(),
                    hasVisualRotation ? (visualRotation * Vector3.forward).ToFloat() : GameplayModel.Float3.Zero,
                    elapsed * 12f, out var up, out var forward))
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

        private static float PlanarSpeed(GameplayModel.Float3 velocity)
        {
            return Mathf.Sqrt(velocity.X * velocity.X + velocity.Z * velocity.Z);
        }

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
