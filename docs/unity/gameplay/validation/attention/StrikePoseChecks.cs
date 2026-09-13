using System;
using System.Collections.Generic;
using System.Reflection;
using LetMeSleep.Core;
using LetMeSleep.Gameplay;
using LetMeSleep.Gameplay.Unity;
using LetMeSleep.Content.Characters;
using LetMeSleep.Presentation.Gameplay;
using UnityEngine;
using Object = UnityEngine.Object;

// External synchronous native checks. Synthetic bones; no imported clip/visual-quality claim.
public static class StrikePoseChecks
{
    [Serializable] public sealed class Result { public int cases, failures; public List<string> outcomes = new List<string>(); }
    public static string RunAll()
    {
        var r = new Result();
        Run(r, "both_arms_reach_without_changing_segment_offsets", f => {
            foreach (int side in new[] { -1, 1 }) foreach (float yaw in new[] { 0f, 90f, 180f })
            {
                f.Apply(0, side, .4f, yaw);
                var arm = f.Arms[side < 0 ? 0 : 1];
                Vector3 target = arm[0].position + f.Binding.transform.TransformDirection(new Vector3(side * .12f, -.25f, .25f));
                f.Target(side, target); f.Solve(side, target);
            }
        });
        Run(r, "unreachable_and_near_shoulder_targets_clamp_without_stretch", f => {
            foreach (int side in new[] { -1, 1 }) foreach (float distance in new[] { 0f, .01f, 3f })
            {
                f.Apply(1, side, .5f, 45);
                Vector3 target = f.Arms[side < 0 ? 0 : 1][0].position + f.Binding.transform.forward * distance;
                f.Target(side, target); f.Solve(side, target);
            }
        });
        Run(r, "authored_wrist_local_rotation_and_scale_are_preserved", f => {
            f.Apply(1, 1, .6f, 120);
            var arm = f.Arms[1]; var wrist = Quaternion.Euler(17, -11, 23);
            arm[2].localRotation = wrist;
            Vector3 target = arm[0].position + f.Binding.transform.TransformDirection(new Vector3(.1f, -.2f, .2f));
            f.Target(1, target); f.Solve(1, target);
            Check(Quaternion.Angle(arm[2].localRotation, wrist) < .001f, "Wrist local orientation overwritten");
            foreach (var bone in arm) Near(Vector3.one, bone.localScale);
        });
        Run(r, "crouch_strike_retains_base_motion_through_recovery", f => {
            foreach (var phase in new[] { StrikePhase.Windup, StrikePhase.Active, StrikePhase.Recovery })
            {
                var state = f.Apply(1, 1, .8f, 0, phase);
                Check((int)Invoke(f.Binding, "SelectMotion", state) == 3, "Swat replaced crouch base");
                f.Binding.ApplyEvent(new GameplayEvent(1, 1, 1, 1, GameplayEventKind.StrikeStarted, 1, 0, 1, Float3.Zero, Float3.Forward));
                Check((int)Get(f.Binding, "temporaryMotion") != 12, "Temporary standing Swat overwrote crouch");
            }
            var after = f.Apply(1, 1, 1, 0, StrikePhase.None);
            Check((int)Invoke(f.Binding, "SelectMotion", after) == 3, "Crouch lost after recovery");
        });
        Run(r, "entering_crouch_cancels_only_standing_swat_temporary", f => {
            f.Apply(0, 1, .1f, 0);
            f.Binding.ApplyEvent(new GameplayEvent(1, 1, 1, 1, GameplayEventKind.StrikeStarted, 1, 0, 1, Float3.Zero, Float3.Forward));
            Check((int)Get(f.Binding, "temporaryMotion") == 12, "Fixture did not start standing Swat");
            f.Apply(1, 1, .3f, 0);
            Check((int)Get(f.Binding, "temporaryMotion") == -1 && (int)Get(f.Binding, "currentMotion") == 3, "Standing temporal clip survived crouch entry");
        });
        Run(r, "standing_strike_preserves_swat_and_inactive_strike_skips_ik", f => {
            var state = f.Apply(0, 1, .4f, 0);
            Check((int)Invoke(f.Binding, "SelectMotion", state) == 12, "Standing Swat removed");
            f.Apply(0, 1, 1, 0, StrikePhase.None);
            Quaternion before = f.Arms[1][0].localRotation;
            Invoke(f.Binding, "ApplyAuthoritativeHands");
            Check(Quaternion.Angle(before, f.Arms[1][0].localRotation) < .001f, "IK continued outside strike");
        });
        return JsonUtility.ToJson(r, true);
    }
    private static void Run(Result r, string name, Action<Fixture> test)
    {
        r.cases++;
        try { using (var f = new Fixture()) test(f); r.outcomes.Add("PASS " + name); }
        catch (Exception e) { r.failures++; r.outcomes.Add("FAIL " + name + ": " + e); }
    }
    private sealed class Fixture : IDisposable
    {
        private readonly GameObject root = new GameObject("StrikePoseChecks-owned");
        private readonly GameplayActorProxy actor;
        public readonly ActorVisualBinding Binding;
        public readonly Transform[][] Arms = new Transform[2][];
        public Fixture()
        {
            actor = root.AddComponent<GameplayActorProxy>(); actor.Initialize(new SpawnActor(1, "human", PlayerRole.Human, Float3.Zero));
            var visual = Child(root.transform, "Visual"); var view = visual.gameObject.AddComponent<CharacterView>();
            for (int i = 0; i < 2; i++)
            {
                string side = i == 0 ? "L" : "R";
                var upper = Child(visual, "UpperArm." + side); upper.localPosition = new Vector3(i == 0 ? -.25f : .25f, 1.35f, 0);
                var lower = Child(upper, "LowerArm." + side); lower.localPosition = new Vector3(0, -.27f, .01f);
                var hand = Child(lower, "Hand." + side); hand.localPosition = new Vector3(0, -.23f, .01f);
                Arms[i] = new[] { upper, lower, hand };
            }
            Binding = visual.gameObject.AddComponent<ActorVisualBinding>(); Binding.Initialize(actor, null, view, true);
        }
        public ActorSnapshot Apply(float crouch, int side, float progress, float yaw, StrikePhase phase = StrikePhase.Active)
        {
            var strike = phase == StrikePhase.None ? default : new StrikeState(1, GameplayTools.Hands, side, phase, 0, Float3.Zero, Float3.Forward, -Float3.Forward, progress);
            var state = new ActorSnapshot(1, PlayerRole.Human, LifeState.Active, 1, Float3.Zero, Float3.Zero,
                Quaternion.Euler(0, yaw, 0).ToRotation(), Float3.Forward, yaw * Mathf.Deg2Rad, 0, 0, 1, true, crouch, 0, null, null, strike, 0);
            actor.Apply(state); Binding.ApplySnapshot(state, 1);
            return state;
        }
        public void Target(int side, Vector3 target)
        {
            var surface = actor.BodySurfaces[side < 0 ? 105u : 106u];
            var collider = surface.Collider;
            float endpoint = Mathf.Max(0, collider.height * .5f - collider.radius);
            surface.transform.rotation = Quaternion.identity;
            surface.transform.position = target - Vector3.up * endpoint;
        }
        public void Solve(int side, Vector3 target)
        {
            var arm = Arms[side < 0 ? 0 : 1];
            Vector3 upperLocal = arm[0].localPosition, lowerLocal = arm[1].localPosition, handLocal = arm[2].localPosition;
            Vector3 shoulder = arm[0].position;
            float a = Vector3.Distance(arm[0].position, arm[1].position), b = Vector3.Distance(arm[1].position, arm[2].position);
            Vector3 delta = target - shoulder;
            Vector3 axis = delta.sqrMagnitude > .000001f ? delta.normalized : (arm[2].position - shoulder).normalized;
            float distance = Mathf.Clamp(delta.magnitude, Mathf.Abs(a - b) + .0001f, a + b - .0001f);
            Invoke(Binding, "ApplyAuthoritativeHands");
            Near(upperLocal, arm[0].localPosition); Near(lowerLocal, arm[1].localPosition); Near(handLocal, arm[2].localPosition);
            Near(shoulder, arm[0].position);
            Check(Math.Abs(Vector3.Distance(arm[0].position, arm[1].position) - a) < .0001f, "Upper segment length changed");
            Check(Math.Abs(Vector3.Distance(arm[1].position, arm[2].position) - b) < .0001f, "Lower segment length changed");
            Near(shoulder + axis * distance, arm[2].position);
        }
        public void Dispose() { Object.DestroyImmediate(root); }
    }
    private static Transform Child(Transform parent, string name) { var child = new GameObject(name).transform; child.SetParent(parent, false); return child; }
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static void Near(Vector3 expected, Vector3 actual) => Check(Vector3.Distance(expected, actual) < .0002f, "Pose mismatch: " + Vector3.Distance(expected, actual));
    private static object Invoke(object o, string name, params object[] args) => o.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(o, args);
    private static object Get(object o, string name) => o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(o);
}
