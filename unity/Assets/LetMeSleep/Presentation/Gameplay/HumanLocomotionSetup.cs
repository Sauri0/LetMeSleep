using System;
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
    }
}
