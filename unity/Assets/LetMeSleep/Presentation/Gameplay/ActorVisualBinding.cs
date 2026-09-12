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
                SynchronizeLoopPhase(state, motion);
                return;
            }

            currentMotion = motion;
            if (immediate && TryGetMotion(motion, out CharacterView.MotionBinding binding) && view.Animator != null)
            {
                view.PlayMotion(motion, 0f);
                view.Animator.Play(binding.StateName, 0, Mathf.Repeat(state.MotionPhase, 1f));
                return;
            }
            view.PlayMotion(motion, CrossFadeSeconds(state));
        }

        private void SynchronizeLoopPhase(GameplayModel.ActorSnapshot state, int motion)
        {
            if (localActor || view.Animator == null || !TryGetMotion(motion, out CharacterView.MotionBinding binding) || !binding.Loop)
                return;
            AnimatorStateInfo info = view.Animator.GetCurrentAnimatorStateInfo(0);
            if (!info.IsName(binding.StateName))
                return;
            float rendered = Mathf.Repeat(info.normalizedTime, 1f);
            float authoritative = Mathf.Repeat(state.MotionPhase, 1f);
            float phaseError = Mathf.Abs(Mathf.DeltaAngle(rendered * 360f, authoritative * 360f)) / 360f;
            if (phaseError > 0.20f)
                view.Animator.Play(binding.StateName, 0, authoritative);
        }

        private void PlayTemporary(int motion)
        {
            temporaryMotion = motion;
            float duration = MotionDuration(motion);
            temporaryUntil = Time.unscaledTime + Mathf.Clamp(duration, 0.08f, 0.45f);
            currentMotion = motion;
            view.PlayMotion(motion, 0.06f);
        }

        private float MotionDuration(int motion)
        {
            if (!TryGetMotion(motion, out CharacterView.MotionBinding binding) ||
                view.Animator == null || view.Animator.runtimeAnimatorController == null)
                return 0.18f;
            AnimationClip[] clips = view.Animator.runtimeAnimatorController.animationClips;
            for (int i = 0; i < clips.Length; i++)
                if (clips[i] != null && clips[i].name == binding.ClipName)
                    return clips[i].length;
            return 0.18f;
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
                case GameplayModel.LifeState.ApproachingSurface: return 3;
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
            transform.SetPositionAndRotation(position, rotation);
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
