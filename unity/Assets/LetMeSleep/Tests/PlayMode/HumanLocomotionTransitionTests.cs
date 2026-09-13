#if UNITY_EDITOR
using System;
using System.Reflection;
using LetMeSleep.Core;
using LetMeSleep.Gameplay;
using LetMeSleep.Gameplay.Unity;
using LetMeSleep.Content.Characters;
using LetMeSleep.Presentation;
using LetMeSleep.Presentation.Gameplay;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LetMeSleep.Tests.PlayMode
{
    public sealed class HumanLocomotionTransitionTests
    {
        private GameObject root;
        private Animator animator;
        private AnimatorController controller;
        private AnimationClip clip;
        private HumanLocomotionPresenter gait;

        [SetUp] public void SetUp()
        {
            root = new GameObject("Transition fixture");
            animator = root.AddComponent<Animator>();
            var left = new GameObject("Left").transform; left.SetParent(root.transform);
            var right = new GameObject("Right").transform; right.SetParent(root.transform);
            clip = new AnimationClip();
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("Left", typeof(Transform), "m_LocalPosition.x"),
                AnimationCurve.Linear(0, 0, 2, 0.1f));
            controller = new AnimatorController(); controller.AddLayer("Base Layer");
            controller.layers[0].stateMachine.AddState("Idle").motion = clip;
            animator.runtimeAnimatorController = controller;
            gait = root.AddComponent<HumanLocomotionPresenter>();
            gait.Configure(1, animator, new[] { new HumanLocomotionPresenter.GaitClip
                { Clip = clip, Speed = 3.1f, DistancePerCycle = 1.55f } }, left, right, "test-only");
        }

        [TearDown] public void TearDown()
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(controller);
            Object.DestroyImmediate(clip);
        }

        [Test] public void PauseCutAndInvalidSpeedReturnExplicitControllerOwnership()
        {
            Assert.That(gait.EvaluateRenderedPose(Vector3.zero, true, false, 1, .02f),
                Is.EqualTo(HumanLocomotionPresenter.PoseOwner.Locomotion));
            Assert.That(animator.runtimeAnimatorController, Is.Null);
            Assert.That(gait.EvaluateRenderedPose(Vector3.zero, true, false, 2, 0),
                Is.EqualTo(HumanLocomotionPresenter.PoseOwner.Controller));
            Assert.That(animator.runtimeAnimatorController, Is.SameAs(controller));
            Assert.That(gait.OwnsPose, Is.False);
            Assert.That(gait.EvaluateRenderedPose(Vector3.zero, true, false, 3, .02f),
                Is.EqualTo(HumanLocomotionPresenter.PoseOwner.Locomotion));
            Assert.That(gait.EvaluateRenderedPose(Vector3.one * 100, true, false, 4, .02f),
                Is.EqualTo(HumanLocomotionPresenter.PoseOwner.Controller));
            Assert.That(animator.runtimeAnimatorController, Is.SameAs(controller));
            gait.EvaluateRenderedPose(Vector3.zero, true, false, 5, .02f);
            Assert.That(gait.EvaluateRenderedPose(Vector3.zero, true, true, 6, .02f),
                Is.EqualTo(HumanLocomotionPresenter.PoseOwner.Controller));
        }

        [Test] public void StrikeAndCrouchCutLocalPoseAndRestoreController()
        {
            var proxy = root.AddComponent<GameplayActorProxy>();
            Set(proxy, "<Role>k__BackingField", PlayerRole.Human);
            Set(proxy, "<ActorId>k__BackingField", (uint)1);
            var view = root.AddComponent<CharacterView>(); view.Animator = animator;
            var binding = root.AddComponent<ActorVisualBinding>();
            binding.Initialize(proxy, null, view, true); binding.BindLocomotion(gait);
            binding.ApplySnapshot(State(0, 0, default), 1);
            gait.EvaluateRenderedPose(Vector3.zero, true, false, 1, .02f);
            var strike = new StrikeState(1, GameplayTools.Hands, default, StrikePhase.Recovery,
                1, default, default, default, .8f);
            binding.ApplySnapshot(State(1, 0, strike), 2);
            Assert.That(root.transform.position.x, Is.EqualTo(1));
            Assert.That(Get(binding, "usingLocomotion"), Is.False);
            Assert.That(animator.runtimeAnimatorController, Is.SameAs(controller));
            binding.ApplySnapshot(State(2, .5f, default), 3);
            Assert.That(root.transform.position.x, Is.EqualTo(2));
            Assert.That(Get(binding, "previous"), Is.SameAs(Get(binding, "current")));
            Assert.That(gait.OwnsPose, Is.False);
        }

        [Test] public void AudioReenableRetainsExactlyOneContactSubscription()
        {
            var audio = root.AddComponent<GameplayAudioPresenter>();
            audio.RegisterLocomotion(gait);
            for (int i = 0; i < 3; i++)
            {
                Assert.That(((Delegate)Get(gait, "ContactReady")).GetInvocationList().Length, Is.EqualTo(1));
                audio.enabled = false;
                Assert.That(Get(gait, "ContactReady"), Is.Null);
                audio.enabled = true;
            }
            Assert.That(((Delegate)Get(gait, "ContactReady")).GetInvocationList().Length, Is.EqualTo(1));
            audio.UnregisterLocomotion(gait);
            Assert.That(Get(gait, "ContactReady"), Is.Null);
        }

        [Test] public void CameraRefreshesFinalAnchorBeforeReadingIt()
        {
            var camera = root.AddComponent<HumanViewCamera>();
            Set(camera, "pitchPivot", root.transform);
            var anchor = new GameObject("Eye").transform; anchor.SetParent(root.transform);
            var expected = new Vector3(4, 2, 3);
            camera.BindEye(anchor, () => anchor.position = expected);
            typeof(HumanViewCamera).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(camera, null);
            Assert.That(root.transform.position, Is.EqualTo(expected));
            var order = (DefaultExecutionOrder)Attribute.GetCustomAttribute(typeof(HumanViewCamera), typeof(DefaultExecutionOrder));
            Assert.That(order.order, Is.GreaterThan(1200));
            camera.BindEye(null);
        }

        private static ActorSnapshot State(float x, float crouch, StrikeState strike) => new ActorSnapshot(
            1, PlayerRole.Human, LifeState.Active, 1, new Float3(x, 0, 0), new Float3(3.1f, 0, 0),
            Rotation.Yaw(0), new Float3(0, 0, 1), 0, 0, 1, 1, true, crouch, 0, null, null, strike, 0);
        private static object Get(object target, string field) => target.GetType()
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private static void Set(object target, string field, object value) => target.GetType()
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}
#endif
