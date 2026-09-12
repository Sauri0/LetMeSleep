using System;
using System.Collections.Generic;
using LetMeSleep.Content.Characters;
using LetMeSleep.Gameplay.Unity;
using UnityEngine;

namespace LetMeSleep.Presentation.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class LobbyVisualPresenter : MonoBehaviour
    {
        private const float HumanStrideMeters = 1.2f;

        private sealed class VisualState
        {
            internal CharacterView View;
            internal GameplayAttentionTarget AttentionTarget;
            internal int Motion = -1;
            internal float WalkClipDuration = 1f;
        }

        [SerializeField] private LobbyMovementRuntime lobby = null;
        private readonly Dictionary<string, VisualState> visuals =
            new Dictionary<string, VisualState>(StringComparer.Ordinal);
        private bool subscribed;

        private void OnEnable() => Subscribe();
        private void Start() => Subscribe();
        private void OnDisable() => Unsubscribe();

        public void Bind(LobbyMovementRuntime runtime)
        {
            Unsubscribe();
            lobby = runtime;
            Subscribe();
        }

        private void HandleVisualCreated(string playerId, GameObject instance)
        {
            if (string.IsNullOrEmpty(playerId) || instance == null)
                return;
            CharacterView view = instance.GetComponentInChildren<CharacterView>(true);
            if (view == null)
            {
                Debug.LogError($"LMS_LOBBY_CHARACTER_VIEW_MISSING player={playerId}", instance);
                return;
            }
            if(visuals.TryGetValue(playerId,out var previous) && previous.View==view)
            {
                if(previous.AttentionTarget) previous.AttentionTarget.enabled=true;
                ApplyLatest(playerId); return;
            }
            if(previous?.AttentionTarget) previous.AttentionTarget.enabled=false;
            GameplayAttentionTarget attentionTarget=null;
            if(VisualAttentionFactory.TryInstall(view.gameObject,false,out var attention,out var reason))
            {
                attentionTarget=view.GetComponent<GameplayAttentionTarget>();
                if(!attentionTarget) attentionTarget=view.gameObject.AddComponent<GameplayAttentionTarget>();
                attentionTarget.Bind(lobby,playerId,attention); attentionTarget.enabled=true;
            }
            else Debug.LogWarning($"LMS_FACIAL_SKIPPED lobby={playerId}: {reason}",view);
            view.SetFirstPersonVisibility(false);
            if (view.Animator != null)
                view.Animator.applyRootMotion = false;
            visuals[playerId] = new VisualState
            {
                View = view,
                AttentionTarget = attentionTarget,
                WalkClipDuration = FindClipDuration(view, "Human_Walk")
            };
            ApplyLatest(playerId);
        }

        private void HandleSnapshot(LobbySnapshot snapshot)
        {
            if (snapshot == null)
                return;
            for (int i = 0; i < snapshot.Poses.Count; i++)
            {
                LobbyPose pose = snapshot.Poses[i];
                // Client Bind can create visuals before this presenter subscribes. Recover from the live roster.
                if(lobby && lobby.TryGetVisual(pose.PlayerId,out var instance) && instance &&
                    (!visuals.TryGetValue(pose.PlayerId,out var known) || !known.View ||
                    (known.View.transform!=instance.transform && !known.View.transform.IsChildOf(instance.transform))))
                    HandleVisualCreated(pose.PlayerId,instance);
                if (visuals.TryGetValue(pose.PlayerId, out VisualState visual))
                    ApplyPose(visual, in pose);
            }
        }

        private void ApplyLatest(string playerId)
        {
            LobbySnapshot snapshot = lobby != null ? lobby.LatestSnapshot : null;
            if (snapshot == null || !visuals.TryGetValue(playerId, out VisualState visual))
                return;
            for (int i = 0; i < snapshot.Poses.Count; i++)
            {
                LobbyPose pose = snapshot.Poses[i];
                if (!string.Equals(pose.PlayerId, playerId, StringComparison.Ordinal))
                    continue;
                ApplyPose(visual, in pose);
                return;
            }
        }

        private static void ApplyPose(VisualState visual, in LobbyPose pose)
        {
            if (visual.View == null)
                return;
            Vector2 planar = new Vector2(pose.Velocity.x, pose.Velocity.z);
            int motion = planar.sqrMagnitude > 0.04f ? 1 : 0;
            if (motion != visual.Motion)
            {
                visual.Motion = motion;
                visual.View.PlayMotion(motion, 0.10f);
            }
            ApplyPlaybackSpeed(visual, motion, planar.magnitude);
            if (motion == 1)
                SynchronizeLoop(visual.View, motion, pose.MotionPhase);
        }

        private static void ApplyPlaybackSpeed(VisualState visual, int motion, float planarSpeed)
        {
            CharacterView view = visual.View;
            if (view.Animator == null)
                return;
            if (motion == 0)
            {
                view.Animator.speed = 1f;
                return;
            }

            view.Animator.speed = Mathf.Clamp(
                visual.WalkClipDuration * planarSpeed / HumanStrideMeters, 0.35f, 2.5f);
        }

        private static float FindClipDuration(CharacterView view, string clipName)
        {
            RuntimeAnimatorController controller = view.Animator.runtimeAnimatorController;
            if (controller != null)
            {
                AnimationClip[] clips = controller.animationClips;
                for (int i = 0; i < clips.Length; i++)
                {
                    if (clips[i] == null || clips[i].name != clipName)
                        continue;
                    return clips[i].length;
                }
            }
            return 1f;
        }

        private static void SynchronizeLoop(CharacterView view, int motion, float phase)
        {
            if (view.Animator == null || view.Motions == null)
                return;
            for (int i = 0; i < view.Motions.Length; i++)
            {
                CharacterView.MotionBinding binding = view.Motions[i];
                if (binding == null || binding.Id != motion || !binding.Loop)
                    continue;
                AnimatorStateInfo info = view.Animator.GetCurrentAnimatorStateInfo(0);
                if (!info.IsName(binding.StateName))
                    return;
                float rendered = Mathf.Repeat(info.normalizedTime, 1f);
                float authoritative = Mathf.Repeat(phase, 1f);
                float error = Mathf.Abs(Mathf.DeltaAngle(rendered * 360f, authoritative * 360f)) / 360f;
                if (error > 0.20f)
                    view.Animator.Play(binding.StateName, 0, authoritative);
                return;
            }
        }

        private void Subscribe()
        {
            if (subscribed || lobby == null)
                return;
            lobby.VisualCreated += HandleVisualCreated;
            lobby.SnapshotApplied += HandleSnapshot;
            subscribed = true;
            LobbySnapshot snapshot = lobby.LatestSnapshot;
            if (snapshot == null && lobby.IsBound && lobby.IsHost)
                snapshot = lobby.CaptureSnapshot();
            if (snapshot == null)
                return;
            for (int i = 0; i < snapshot.Poses.Count; i++)
                if (lobby.TryGetVisual(snapshot.Poses[i].PlayerId, out GameObject instance))
                    HandleVisualCreated(snapshot.Poses[i].PlayerId, instance);
            HandleSnapshot(snapshot);
        }

        private void Unsubscribe()
        {
            if (subscribed && lobby != null)
            {
                lobby.VisualCreated -= HandleVisualCreated;
                lobby.SnapshotApplied -= HandleSnapshot;
            }
            foreach(var visual in visuals.Values) if(visual.AttentionTarget) visual.AttentionTarget.enabled=false;
            visuals.Clear();
            subscribed = false;
        }
    }
}
