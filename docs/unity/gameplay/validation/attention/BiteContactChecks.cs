using System;
using System.Collections.Generic;
using System.Reflection;
using LetMeSleep.Core;
using LetMeSleep.Gameplay;
using LetMeSleep.Gameplay.Unity;
using LetMeSleep.Content.Characters;
using LetMeSleep.Presentation;
using LetMeSleep.Presentation.Gameplay;
using UnityEngine;
using Object = UnityEngine.Object;

// External native checks for Director's assigned slot. Never copy into Assets.
// Synthetic rig and synchronous callbacks: no imported animation/real-time claim.
public static class BiteContactChecks
{
    [Serializable] public sealed class Result { public int cases, failures; public string disableScope; public List<string> outcomes = new List<string>(); }
    public static string RunAll()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("RunAll requires Play Mode for automatic OnDisable. Use RunAllManualEditMode for explicitly manual cleanup checks.");
        return Run(false);
    }
    public static string RunAllManualEditMode()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Use RunAll in Play Mode.");
        return Run(true);
    }
    private static string Run(bool manualDisable)
    {
        var result = new Result { disableScope = manualDisable ? "Explicit OnDisable invocation; automatic lifecycle NOT tested" : "Automatic OnDisable in Play Mode" };
        Run(result, "yaw_after_attach_preserves_tip_and_authority", f => {
            foreach (float yaw in new[] { 0f, 90f, 180f, 270f })
            {
                f.Attach(Vector3.back, yaw, 1, Vector3.zero, Quaternion.identity);
                Vector3 position = f.Mosquito.transform.position;
                Quaternion rotation = f.Mosquito.transform.rotation;
                f.Evaluate();
                f.Contact(Vector3.back);
                Near(position, f.Mosquito.transform.position);
                Check(Quaternion.Angle(rotation, f.Mosquito.transform.rotation) < .001f, "Authoritative rotation changed");
                Check(Math.Abs(f.Mosquito.State.ViewYawRadians - yaw * Mathf.Deg2Rad) < .0001f, "View intent changed");
            }
        });
        Run(result, "normal_and_victim_pose_follow_without_stale_target", f => {
            uint revision = 1;
            foreach (var normal in new[] { Vector3.back, Vector3.right, Vector3.up, Vector3.down, new Vector3(1, 2, 3).normalized })
            {
                f.Attach(normal, 165, revision++, new Vector3(revision * .1f, .3f, -.2f), Quaternion.Euler(20, revision * 17, 0));
                f.Evaluate(); f.Contact(normal);
            }
        });
        Run(result, "head_and_neck_lock_after_alignment_eyes_and_blink_continue", f => {
            f.Attach(Vector3.back, 180, 1, Vector3.zero, Quaternion.identity);
            f.Rig.SetLookPoint(new Vector3(5, 4, 4));
            Set(f.Rig, "nextBlink", 0.0); // Deterministic closure in the facial clock.
            f.Evaluate(.08f);
            f.Contact(Vector3.back);
            Check(Quaternion.Angle(f.Head.localRotation, Quaternion.identity) < .001f, "Head moved while biting");
            Check(Quaternion.Angle(f.Neck.localRotation, Quaternion.identity) < .001f, "Neck moved while biting");
            Check(Quaternion.Angle(f.Eye.localRotation, Quaternion.identity) > .1f, "Pupil stopped tracking");
            Check(Quaternion.Angle(f.Lid.localRotation, Quaternion.identity) > 1f, "Blink stopped");
            // Negative control verifies this fixture detects the original late-head break.
            f.Rig.PrepareForAnimation(); f.Rig.SetHeadTrackingEnabled(true);
            f.Rig.EvaluateAfterAnimation(.2f); f.View.RefreshAnchors();
            Check(Vector3.Distance(f.Target, f.Mouth.position) > .001f, "Negative control failed to detect head-induced separation");
        });
        Run(result, "detach_restores_head_gradually_and_releases_visual_tilt", f => {
            f.Attach(Vector3.right, 0, 1, Vector3.zero, Quaternion.identity); f.Evaluate();
            Quaternion attached = f.Binding.transform.rotation;
            f.Rig.SetLookPoint(new Vector3(5, 4, 4));
            f.Rig.PrepareForAnimation();
            var free = State(2, PlayerRole.Mosquito, LifeState.Flying, f.Mosquito.transform.position, Quaternion.identity, 0, 2, null);
            f.Mosquito.Apply(free); f.Binding.ApplySnapshot(free, 2);
            Set(f.Binding, "lastVisualPoseTime", Time.unscaledTime - .016f);
            Call(f.Binding, "LateUpdate"); f.Rig.EvaluateAfterAnimation(.016f);
            float first = Quaternion.Angle(f.Head.localRotation, Quaternion.identity);
            Check(f.Rig.HeadTrackingEnabled && first > .01f && first < 10f, "Head release snapped or remained locked");
            Check(Quaternion.Angle(attached, f.Binding.transform.rotation) > .1f && Quaternion.Angle(f.Binding.transform.rotation, Quaternion.identity) > .1f, "Visual release must interpolate");
            f.Rig.PrepareForAnimation(); f.Rig.EvaluateAfterAnimation(.2f);
            Check(Quaternion.Angle(f.Head.localRotation, Quaternion.identity) > first, "Tracking did not recover");
            Check(f.Binding.LastBiteSampleFrame == -1, "Stale contact diagnostic after detach");
        });
        Run(result, "pose_revision_mismatch_releases_lock_and_sample", f => {
            f.Attach(Vector3.back, 0, 1, Vector3.zero, Quaternion.identity); f.Evaluate();
            f.Human.Apply(State(1, PlayerRole.Human, LifeState.Active, Vector3.zero, Quaternion.identity, 0, 2, null));
            f.Evaluate();
            Check(f.Rig.HeadTrackingEnabled && f.Binding.LastBiteSampleFrame == -1, "Stale pose retained contact");
        });
        Run(result, manualDisable ? "manual_disable_cleanup_releases_tracking_and_unsubscribes" : "disable_releases_tracking_and_unsubscribes", f => {
            f.Attach(Vector3.back, 0, 1, Vector3.zero, Quaternion.identity); f.Evaluate();
            f.Binding.enabled = false;
            if (manualDisable) Call(f.Binding, "OnDisable");
            Check(f.Rig.HeadTrackingEnabled && f.Binding.LastBiteSampleFrame == -1, "Disabled owner retained lock");
            f.Rig.PrepareForAnimation(); f.Rig.EvaluateAfterAnimation(.1f);
            Check(f.Binding.LastBiteSampleFrame == -1, "Disabled owner sampled facial event");
        });
        return JsonUtility.ToJson(result, true);
    }
    private static void Run(Result result, string name, Action<Fixture> test)
    {
        result.cases++;
        try { using (var f = new Fixture()) test(f); result.outcomes.Add("PASS " + name); }
        catch (Exception e) { result.failures++; result.outcomes.Add("FAIL " + name + ": " + e); }
    }
    private sealed class Fixture : IDisposable
    {
        private readonly GameObject root = new GameObject("BiteContactChecks-owned");
        public readonly GameplayActorProxy Human, Mosquito;
        public readonly ActorVisualBinding Binding;
        public readonly CharacterView View;
        public readonly VisualAttentionRig Rig;
        public readonly Transform Neck, Head, Mouth, Eye, Lid;
        public Vector3 Target;
        public Fixture()
        {
            var world = root.AddComponent<UnityGameplayWorld>();
            world.MapRoot = Child(root.transform, "Map");
            world.BeginRound(new[] { new SpawnActor(1, "human", PlayerRole.Human, Float3.Zero), new SpawnActor(2, "mosquito", PlayerRole.Mosquito, Float3.Zero) }, Array.Empty<DoorDefinition>());
            Human = world.Actors[1]; Mosquito = world.Actors[2];
            var visual = Child(Mosquito.transform, "SyntheticVisual");
            View = visual.gameObject.AddComponent<CharacterView>();
            Neck = Child(visual, "Neck"); Head = Child(Neck, "Head"); Head.localPosition = new Vector3(0, .05f, .015f);
            Mouth = Child(Head, "Socket.Mouth"); Mouth.localPosition = new Vector3(0, -.05f, .08f);
            Eye = Child(Head, "Pupil.L"); var right = Child(Head, "Pupil.R");
            Lid = Child(Head, "Lid.L"); var rightLid = Child(Head, "Lid.R");
            var anchor = Child(visual, "ProboscisTip");
            View.Anchors = new[] { new CharacterView.AnchorBinding { Name = "ProboscisTip", Anchor = anchor, SourceBone = Mouth } };
            Rig = visual.gameObject.AddComponent<VisualAttentionRig>();
            Check(Rig.Configure(new VisualAttentionRig.Bindings {
                Head = Head, Neck = Neck, LeftEye = Eye, RightEye = right,
                EyeForward = Vector3.forward, EyeUp = Vector3.up, HeadYawLimit = 25, HeadPitchLimit = 15,
                ManualEvaluation = true,
                LeftLids = new[] { new VisualAttentionRig.BlinkBone { Bone = Lid, LocalAxis = Vector3.right, ClosedAngleDegrees = 90 } },
                RightLids = new[] { new VisualAttentionRig.BlinkBone { Bone = rightLid, LocalAxis = Vector3.right, ClosedAngleDegrees = 90 } }
            }), "Rig configuration failed");
            Binding = visual.gameObject.AddComponent<ActorVisualBinding>(); Binding.Initialize(Mosquito, world, View, true);
        }
        public void Attach(Vector3 normal, float yaw, uint revision, Vector3 victimPosition, Quaternion victimRotation)
        {
            Human.Apply(State(1, PlayerRole.Human, LifeState.Active, victimPosition, victimRotation, 0, revision, null));
            var surface = Human.BodySurfaces[101];
            Vector3 point = new Vector3(0, 0, -.18f); Target = surface.transform.TransformPoint(point);
            var attachment = new BiteAttachment(1, 101, point.ToFloat(), surface.transform.InverseTransformDirection(normal).ToFloat(), revision);
            var state = State(2, PlayerRole.Mosquito, LifeState.Biting, Target + normal * .096f, Quaternion.Euler(0, yaw, 0), yaw, revision, attachment);
            Mosquito.Apply(state); Binding.ApplySnapshot(state, revision);
        }
        public void Evaluate(float dt = .016f) { Rig.PrepareForAnimation(); Call(Binding, "LateUpdate"); Rig.EvaluateAfterAnimation(dt); }
        public void Contact(Vector3 normal)
        {
            Near(Target, Mouth.position);
            Check(Vector3.Dot(Binding.transform.forward, -normal) > .9999f, "Visual forward ignores contact normal");
            Check(Binding.LastBitePreCorrectionMeters < .002f, "Unexpected pre-correction with neutral synthetic mouth");
            Check(Binding.LastBiteFinalResidualMeters < .0001f && Binding.LastBiteSampleFrame == Time.frameCount, "Missing/failing final sample");
            Check(!Rig.HeadTrackingEnabled, "Contact did not lock head");
        }
        public void Dispose() { Object.DestroyImmediate(root); }
    }
    private static ActorSnapshot State(uint id, PlayerRole role, LifeState life, Vector3 position, Quaternion rotation, float yaw, uint revision, BiteAttachment? bite) =>
        new ActorSnapshot(id, role, life, revision, position.ToFloat(), Float3.Zero, rotation.ToRotation(), Vector3.forward.ToFloat(), yaw * Mathf.Deg2Rad, 0, 0, revision, true, 0, 0, null, bite, default, 0);
    private static Transform Child(Transform parent, string name) { var child = new GameObject(name).transform; child.SetParent(parent, false); return child; }
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static void Near(Vector3 expected, Vector3 actual) { Check(Vector3.Distance(expected, actual) < .0001f, "Tip/authoritative position mismatch: " + Vector3.Distance(expected, actual)); }
    private static void Call(object value, string method) => value.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(value, null);
    private static void Set(object value, string field, object data) => value.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(value, data);
}
