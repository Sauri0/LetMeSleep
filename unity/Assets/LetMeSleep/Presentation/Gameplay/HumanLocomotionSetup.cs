using System;
using System.Collections.Generic;
using System.Text;
using LetMeSleep.Content.Characters;
using UnityEngine;

namespace LetMeSleep.Presentation.Gameplay
{
    /// <summary>All-or-nothing lookup; old two-clip controllers never enable the new route.</summary>
    public static class HumanLocomotionSetup
    {
        private static readonly string[] Names = { "Human_WalkSlow", "Human_Walk", "Human_Trot", "Human_Run" };
        private static readonly float[] Speeds = { 1f, 1.55f, 3.1f, 5f };
        private static readonly float[] Distances = { 0.833333333f, 0.96875f, 1.55f, 2.173913043f };

        public static bool TryConfigure(CharacterView view, uint actorId, out HumanLocomotionPresenter presenter)
        {
            presenter = null;
            if (!view || !view.Animator || !view.Animator.runtimeAnimatorController) return false;
            var source = view.Animator.runtimeAnimatorController.animationClips;
            var gaits = new HumanLocomotionPresenter.GaitClip[Names.Length];
            for (int i = 0; i < Names.Length; i++)
            {
                AnimationClip found = null;
                foreach (var clip in source)
                {
                    if (!clip || clip.name != Names[i]) continue;
                    if (found && found != clip) return false;
                    found = clip;
                }
                if (!found || found.legacy || !found.isLooping || found.length <= 0 ||
                    float.IsNaN(found.length) || float.IsInfinity(found.length)) return false;
                gaits[i] = new HumanLocomotionPresenter.GaitClip
                    { Clip = found, Speed = Speeds[i], DistancePerCycle = Distances[i] };
            }
            Transform left = view.GetAnchor("Foot_L"), right = view.GetAnchor("Foot_R");
            if (!left || !right) return false;
            // CharacterView anchors are sibling presentation objects: use their source bones.
            foreach (var anchor in view.Anchors)
            {
                if (anchor.Name == "Foot_L") left = anchor.SourceBone;
                if (anchor.Name == "Foot_R") right = anchor.SourceBone;
            }
            if (!left || !right || left == right || !left.IsChildOf(view.Animator.transform) ||
                !right.IsChildOf(view.Animator.transform)) return false;
            // Do not adopt an unrelated preinstalled writer.
            if (view.GetComponent<HumanLocomotionPresenter>()) return false;
            var created = view.gameObject.AddComponent<HumanLocomotionPresenter>();
            try
            {
                created.Configure(actorId, view.Animator, gaits, left, right, "human-four-gaits-20260913");
                // v0.3.0 optional layers: legs keep walking while the Swat plays on the Chest subtree, and the
                // crouched gait replaces the sliding static crouch. Older controllers simply lack the clips.
                AnimationClip strike = Find(source, "Human_Swat"), crouchWalk = Find(source, "Human_CrouchWalk");
                // Round 3 (anim-r3): one mask per rig layout, shared by every actor and never destroyed with
                // one of them (no per-actor allocation, nothing to leak over respawns).
                AvatarMask mask = strike ? SharedUpperBodyMask(view.Animator.transform) : null;
                if (strike && !mask) strike = null;
                if (strike || crouchWalk) created.ConfigureOverlays(strike, mask, crouchWalk, false);
                presenter = created;
                return true;
            }
            catch (ArgumentException)
            {
                created.enabled = false;
                UnityEngine.Object.Destroy(created);
                return false;
            }
        }

        private static readonly Dictionary<string, AvatarMask> SharedMasks = new Dictionary<string, AvatarMask>();

        /// <summary>
        /// The upper-body mask for this rig layout (transform paths), created once and shared by every actor
        /// with the same skeleton; it lives for the session (HideAndDontSave), so presenters never own it.
        /// </summary>
        public static AvatarMask SharedUpperBodyMask(Transform animatorRoot)
        {
            if (!animatorRoot) return null;
            var key = new StringBuilder();
            foreach (var item in animatorRoot.GetComponentsInChildren<Transform>(true))
                key.Append(RelativePath(animatorRoot, item)).Append('|');
            string signature = key.ToString();
            if (SharedMasks.TryGetValue(signature, out var shared) && shared) return shared;
            shared = UpperBodyMask(animatorRoot);
            if (!shared) return null;
            shared.hideFlags = HideFlags.HideAndDontSave;
            SharedMasks[signature] = shared;
            return shared;
        }

        private static AnimationClip Find(AnimationClip[] clips, string name)
        {
            AnimationClip found = null;
            foreach (var clip in clips)
            {
                if (!clip || clip.name != name) continue;
                if (found && found != clip) return null;
                found = clip;
            }
            return found && !found.legacy && found.length > 0 ? found : null;
        }

        /// <summary>Generic-rig mask: the Chest bone and everything below it (neck, head, shoulders, arms, hands).</summary>
        public static AvatarMask UpperBodyMask(Transform animatorRoot)
        {
            if (!animatorRoot) return null;
            Transform chest = null;
            foreach (var item in animatorRoot.GetComponentsInChildren<Transform>(true))
                if (item.name == "Chest") { if (chest) return null; chest = item; }
            if (!chest) return null;
            var all = animatorRoot.GetComponentsInChildren<Transform>(true);
            var mask = new AvatarMask { name = "LMS_HumanUpperBody" };
            mask.transformCount = all.Length;
            for (int i = 0; i < all.Length; i++)
            {
                mask.SetTransformPath(i, RelativePath(animatorRoot, all[i]));
                mask.SetTransformActive(i, all[i] == chest || all[i].IsChildOf(chest));
            }
            return mask;
        }

        private static string RelativePath(Transform root, Transform item)
        {
            if (item == root) return string.Empty;
            string path = item.name;
            for (var parent = item.parent; parent && parent != root; parent = parent.parent) path = parent.name + "/" + path;
            return path;
        }
    }
}
