#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using LetMeSleep.Content.Characters;
using LetMeSleep.Core;
using LetMeSleep.Gameplay;
using LetMeSleep.Gameplay.Unity;
using LetMeSleep.Presentation;
using LetMeSleep.Presentation.Gameplay;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Ids = LetMeSleep.Presentation.Gameplay.CharacterMotionIds;
using Object = UnityEngine.Object;

namespace LetMeSleep.Tests.PlayMode
{
    /// <summary>v0.3.0 animation pass: jump/air states, wingbeat clock, secondary motion, strike layer,
    /// elbow limit, moods and mosquito camera framing. Presentation only.</summary>
    public sealed class ExpressiveAnimationPlayModeTests
    {
        private const string HumanPrefab = "Assets/LetMeSleep/Content/Characters/Prefabs/LMS_Human.prefab";
        private const string MosquitoPrefab = "Assets/LetMeSleep/Content/Characters/Prefabs/LMS_Mosquito.prefab";
        private readonly List<Object> created = new List<Object>();

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            foreach (var item in created) if (item) Object.Destroy(item);
            created.Clear();
            yield return null;
        }

        // ------------------------------------------------------------------ pure policies

        [Test]
        public void MoodPolicyFollowsMosquitoStatesAndEvents()
        {
            var policy = new GameplayMoodPolicy();
            Assert.That(Mood(policy, PlayerRole.Mosquito, LifeState.Flying, 0, near: false), Is.EqualTo(FacialMood.Neutral));
            Assert.That(Mood(policy, PlayerRole.Mosquito, LifeState.Flying, 1, near: true), Is.EqualTo(FacialMood.Alert));
            Assert.That(Mood(policy, PlayerRole.Mosquito, LifeState.PreparingBite, 2), Is.EqualTo(FacialMood.Focused));
            var feeding = policy.Evaluate(Frame(PlayerRole.Mosquito, LifeState.Biting), 3);
            Assert.That(feeding.Mood, Is.EqualTo(FacialMood.Happy));
            var later = policy.Evaluate(Frame(PlayerRole.Mosquito, LifeState.Biting), 5);
            Assert.That(later.Weight, Is.GreaterThan(feeding.Weight), "The mosquito gets happier the longer it feeds.");
            policy.Notify(GameplayEventKind.BiteEnded, true, false, 6);
            Assert.That(Mood(policy, PlayerRole.Mosquito, LifeState.Flying, 6.5), Is.EqualTo(FacialMood.Excited));
            policy.Notify(GameplayEventKind.MosquitoKnockedDown, true, false, 8);
            Assert.That(Mood(policy, PlayerRole.Mosquito, LifeState.Flying, 8.1), Is.EqualTo(FacialMood.Surprised));
            Assert.That(Mood(policy, PlayerRole.Mosquito, LifeState.Falling, 8.3), Is.EqualTo(FacialMood.Surprised));
            Assert.That(Mood(policy, PlayerRole.Mosquito, LifeState.Stunned, 9), Is.EqualTo(FacialMood.Dizzy));
            Assert.That(Mood(policy, PlayerRole.Mosquito, LifeState.Recovering, 10), Is.EqualTo(FacialMood.Sleepy));
        }

        [Test]
        public void MoodPolicyHumanSleepsYawnsAndReactsToStrikesAndBites()
        {
            var policy = new GameplayMoodPolicy();
            var calm = Frame(PlayerRole.Human, LifeState.Active);
            Assert.That(policy.Evaluate(calm, 0).Mood, Is.EqualTo(FacialMood.Neutral));
            Assert.That(policy.Evaluate(calm, 3).Mood, Is.EqualTo(FacialMood.Neutral));
            Assert.That(policy.Evaluate(calm, GameplayMoodPolicy.IdleSleepySeconds + .1).Mood, Is.EqualTo(FacialMood.Sleepy));
            int yawns = 0;
            for (double t = 6.2; t < 30; t += .1)
                if (policy.Evaluate(calm, t).StartYawn) yawns++;
            Assert.That(yawns, Is.InRange(2, 3), "A long idle yawns about every ten seconds, once per yawn.");
            var walking = new GameplayMoodPolicy.Frame(PlayerRole.Human, LifeState.Active, 1.5f, true, 0, false, false);
            Assert.That(policy.Evaluate(walking, 31).Mood, Is.EqualTo(FacialMood.Neutral), "Moving wakes the human up.");
            Assert.That(policy.Evaluate(calm, 33).Mood, Is.EqualTo(FacialMood.Neutral), "The idle timer restarts after moving.");

            policy.Notify(GameplayEventKind.StrikeStarted, true, false, 40);
            var striking = new GameplayMoodPolicy.Frame(PlayerRole.Human, LifeState.Active, 0, true, 0, true, false);
            Assert.That(policy.Evaluate(striking, 40.1).Mood, Is.EqualTo(FacialMood.Angry));
            policy.Notify(GameplayEventKind.StrikeImpact, true, false, 40.2);
            Assert.That(policy.Evaluate(striking, 40.3).Mood, Is.EqualTo(FacialMood.Happy), "A successful swat is a happy one.");

            policy.Notify(GameplayEventKind.BiteStarted, false, true, 50);
            Assert.That(policy.Evaluate(calm, 50.2).Mood, Is.EqualTo(FacialMood.Surprised));
            Assert.That(policy.Evaluate(calm, 51).Mood, Is.EqualTo(FacialMood.Angry));
            Assert.That(policy.Evaluate(Frame(PlayerRole.Human, LifeState.Fainted), 60).Mood, Is.EqualTo(FacialMood.Unconscious));
            Assert.That(policy.Evaluate(calm, double.NaN).Mood, Is.EqualTo(FacialMood.Neutral));
        }

        [Test]
        public void WingbeatRateFollowsSpeedNotDistanceAndPhaseIsPerActor()
        {
            Assert.That(MosquitoWingbeat.FrequencyHz(0), Is.EqualTo(8f).Within(1e-4f));
            Assert.That(MosquitoWingbeat.FrequencyHz(MosquitoWingbeat.MaximumSpeed), Is.EqualTo(12f).Within(1e-4f));
            Assert.That(MosquitoWingbeat.FrequencyHz(100f), Is.EqualTo(12f).Within(1e-4f));
            Assert.That(MosquitoWingbeat.FrequencyHz(float.NaN), Is.EqualTo(8f).Within(1e-4f));
            // Authored Fly/Hover loops: 12 frames at 30 fps hold 3 wingbeats.
            float loop = 12f / 30f;
            for (float speed = 0; speed <= 3.8f; speed += .2f)
            {
                float hz = MosquitoWingbeat.Playback(speed, loop) * MosquitoWingbeat.WingbeatsPerLoop / loop;
                Assert.That(hz, Is.InRange(8f - 1e-3f, 12f + 1e-3f));
            }
            var phases = new HashSet<float>();
            for (uint id = 1; id <= 16; id++)
            {
                float phase = MosquitoWingbeat.InitialPhase(id);
                Assert.That(phase, Is.InRange(0f, .9999999f));
                Assert.That(MosquitoWingbeat.InitialPhase(id), Is.EqualTo(phase), "Deterministic per actor.");
                phases.Add(Mathf.Round(phase * 100));
            }
            Assert.That(phases.Count, Is.GreaterThan(12), "A swarm does not flap in lockstep.");
        }

        [Test]
        public void SecondaryMotionStaysFiniteAndConservesVolume()
        {
            var actor = Track(new GameObject("SecondaryActor"));
            var rig = new GameObject("Rig").transform; rig.SetParent(actor.transform, false);
            var root = new GameObject("Root").transform; root.SetParent(rig, false);
            var abdomen = new GameObject("Abdomen01").transform; abdomen.SetParent(root, false);
            var motion = new CharacterSecondaryMotion(PlayerRole.Mosquito, actor.transform, rig);
            Assert.That(motion.SupportsSquash, Is.True);
            motion.Kick(-.28f); motion.KickWobble(30f);
            foreach (float dt in new[] { 1f / 60, 0f, -1f, float.NaN, float.PositiveInfinity, 10f, 1f / 144, .05f })
            {
                motion.Apply(new CharacterSecondaryMotion.Input { DeltaSeconds = dt, Biting = true, BitingSeconds = 2 });
                var scale = root.localScale;
                Assert.That(Finite(scale) && Finite(root.localRotation.eulerAngles) && Finite(abdomen.localScale), Is.True, "dt " + dt);
                Assert.That(scale.x * scale.y * scale.z, Is.EqualTo(1f).Within(1e-4f), "Squash and stretch keeps volume.");
            }
            motion.Kick(float.NaN); motion.KickWobble(float.PositiveInfinity);
            for (int i = 0; i < 240; i++) motion.Apply(new CharacterSecondaryMotion.Input { DeltaSeconds = 1f / 60 });
            Assert.That(Mathf.Abs(motion.Squash), Is.LessThan(.01f), "The spring settles.");
            Assert.That(abdomen.localScale.x, Is.LessThan(1.2f));
            motion.Reset();
            Assert.That(root.localScale, Is.EqualTo(Vector3.one));
        }

        [Test]
        public void PoseCrossfadeBlendsFromTheLastCapturedPose()
        {
            var bone = Track(new GameObject("Bone")).transform;
            var fade = new PoseCrossfade(new[] { bone });
            fade.Begin(.2f);
            Assert.That(fade.Active, Is.False, "Nothing to blend from before the first capture.");
            bone.localPosition = Vector3.zero; fade.Apply(.016f);
            bone.localPosition = Vector3.right; fade.Begin(.2f); fade.Apply(.1f);
            Assert.That(bone.localPosition.x, Is.EqualTo(.5f).Within(1e-4f));
            bone.localPosition = Vector3.right; fade.Apply(.2f);
            Assert.That(bone.localPosition.x, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(fade.Active, Is.False);
        }

        [Test]
        public void TwoBoneSolverRespectsTheInnerAngleLimit()
        {
            var upper = Track(new GameObject("Upper")).transform;
            var lower = new GameObject("Lower").transform; lower.SetParent(upper, false); lower.localPosition = new Vector3(0, -.27f, 0);
            var end = new GameObject("End").transform; end.SetParent(lower, false); end.localPosition = new Vector3(0, -.23f, 0);
            lower.localRotation = Quaternion.Euler(10, 0, 0);
            TwoBoneSolver.Solve(upper, lower, end, new Vector3(0, 0, 2), Vector3.forward, 150);
            float angle = TwoBoneSolver.InnerAngle(upper.position, lower.position, end.position);
            Assert.That(angle, Is.LessThanOrEqualTo(150.05f));
            Assert.That(angle, Is.GreaterThan(140f));
            Assert.That(Vector3.Distance(upper.position, end.position),
                Is.EqualTo(TwoBoneSolver.MaximumReach(.27f, .23f, 150)).Within(1e-3f));
        }

        // ------------------------------------------------------------------ production prefabs

        [Test]
        public void ProductionControllersExposeTheAppendedExpressiveStates()
        {
            var human = Load(HumanPrefab).GetComponent<CharacterView>();
            var mosquito = Load(MosquitoPrefab).GetComponent<CharacterView>();
            AssertMotion(human, Ids.HumanJumpAir, "Base Layer.JumpAir", true);
            AssertMotion(human, Ids.HumanFallAir, "Base Layer.FallAir", true);
            AssertMotion(human, Ids.HumanCrouchWalk, "Base Layer.CrouchWalk", true);
            AssertMotion(human, Ids.HumanYawn, "Base Layer.Yawn", false);
            AssertMotion(human, Ids.HumanVictory, "Base Layer.Victory", true);
            AssertMotion(human, Ids.HumanFall, "Base Layer.Fall", false);
            AssertMotion(human, Ids.HumanTrot, "Base Layer.Trot", true);
            AssertMotion(mosquito, Ids.MosquitoStunnedLoop, "Base Layer.StunnedLoop", true);
            AssertMotion(mosquito, Ids.MosquitoBite, "Base Layer.Bite", false);
            var mask = HumanLocomotionSetup.UpperBodyMask(human.Animator.transform);
            Assert.That(mask, Is.Not.Null);
            int chest = -1, leg = -1, active = 0;
            for (int i = 0; i < mask.transformCount; i++)
            {
                string path = mask.GetTransformPath(i);
                if (mask.GetTransformActive(i)) active++;
                if (path.EndsWith("/Chest")) chest = i;
                if (path.EndsWith("/UpperLeg.L")) leg = i;
            }
            Assert.That(chest, Is.GreaterThanOrEqualTo(0)); Assert.That(leg, Is.GreaterThanOrEqualTo(0));
            Assert.That(mask.GetTransformActive(chest), Is.True); Assert.That(mask.GetTransformActive(leg), Is.False);
            Assert.That(active, Is.GreaterThan(20).And.LessThan(mask.transformCount));
            Object.DestroyImmediate(mask);
        }

        /// <summary>
        /// Between two keys every bone must interpolate like a rotation, not like three Euler angles: a
        /// gimbal-adjacent Euler pair (deep thigh flexion, raised arms) interpolated per axis swings the limb
        /// through an unrelated pose for one frame (the crouch-walk foot at hip height).
        /// </summary>
        [Test]
        public void AuthoredClipsInterpolateBetweenKeysWithoutFlips()
        {
            var worst = new List<string>();
            foreach (var path in new[] { HumanPrefab, MosquitoPrefab })
            {
                var instance = Track(Object.Instantiate(Load(path)));
                var animator = instance.GetComponentInChildren<Animator>();
                var bones = animator.GetComponentsInChildren<Transform>(true);
                var before = new Quaternion[bones.Length];
                var after = new Quaternion[bones.Length];
                foreach (var clip in animator.runtimeAnimatorController.animationClips)
                {
                    float rate = clip.frameRate > 0 ? clip.frameRate : 30f;
                    int frames = Mathf.RoundToInt(clip.length * rate);
                    float maximum = 0, step = 0; string where = "", stepWhere = "";
                    for (int k = 0; k < frames; k++)
                    {
                        clip.SampleAnimation(animator.gameObject, k / rate);
                        for (int i = 0; i < bones.Length; i++) before[i] = bones[i].localRotation;
                        clip.SampleAnimation(animator.gameObject, (k + 1) / rate);
                        for (int i = 0; i < bones.Length; i++) after[i] = bones[i].localRotation;
                        clip.SampleAnimation(animator.gameObject, (k + .5f) / rate);
                        for (int i = 0; i < bones.Length; i++)
                        {
                            float deviation = Quaternion.Angle(bones[i].localRotation, Quaternion.Slerp(before[i], after[i], .5f));
                            if (deviation > maximum) { maximum = deviation; where = $"{bones[i].name}@{k}"; }
                            // A bone never turns 75+ deg in one 1/30 s key step (the fastest wingbeat turns ~40).
                            float turn = Quaternion.Angle(before[i], after[i]);
                            if (turn > step) { step = turn; stepWhere = $"{bones[i].name}@{k}"; }
                        }
                    }
                    if (maximum > 6f) worst.Add($"{clip.name} mid-key {where} {maximum:F1} deg");
                    if (step > 75f) worst.Add($"{clip.name} key step {stepWhere} {step:F1} deg");
                }
            }
            Assert.That(worst, Is.Empty, "Mid-key rotations leave the rotation between their keys: " + string.Join(", ", worst));
        }

        [UnityTest]
        public IEnumerator HumanJumpUsesAirClipsAndLandsWithoutTheFaintClip()
        {
            var fixture = Human(false, out var proxy, out var view, out var binding);
            binding.ApplySnapshot(HumanState(Vector3.zero, Vector3.zero, true), 1);
            yield return null;
            binding.ApplySnapshot(HumanState(new Vector3(0, .2f, 0), new Vector3(0, 4.6f, 0), false), 2);
            Assert.That(binding.CurrentMotion, Is.EqualTo(Ids.HumanJumpAir));
            Assert.That(binding.Secondary.Squash, Is.GreaterThan(.05f), "Take-off stretches.");
            yield return null;
            yield return new WaitForSecondsRealtime(.3f);
            binding.ApplySnapshot(HumanState(new Vector3(0, .8f, 0), new Vector3(0, -3.5f, 0), false), 3);
            Assert.That(binding.CurrentMotion, Is.EqualTo(Ids.HumanFallAir));
            Assert.That(binding.CurrentMotion, Is.Not.EqualTo(Ids.HumanFall), "The faint clip is never a jump descent.");
            yield return new WaitForSecondsRealtime(.3f);
            var info = view.Animator.GetCurrentAnimatorStateInfo(0);
            Assert.That(info.IsName("Base Layer.Fall"), Is.False);
            binding.ApplySnapshot(HumanState(Vector3.zero, Vector3.zero, true), 4);
            Assert.That(binding.TemporaryMotion, Is.EqualTo(Ids.HumanLand), "Land plays on touchdown.");
            Assert.That(binding.Secondary.Squash, Is.LessThan(-.05f), "Touchdown squashes.");
            var rootBone = Find(view.Animator.transform, "Root");
            float until = Time.realtimeSinceStartup + 1f;
            while (Time.realtimeSinceStartup < until)
            {
                yield return null;
                var scale = rootBone.localScale;
                Assert.That(Finite(scale), Is.True);
                Assert.That(scale.x * scale.y * scale.z, Is.EqualTo(1f).Within(2e-3f));
            }
            Assert.That(binding.TemporaryMotion, Is.EqualTo(-1), "Land is short.");
            Object.Destroy(fixture);
        }

        [UnityTest]
        public IEnumerator StrikeWhileWalkingKeepsTheGaitAndNeverLocksTheElbow()
        {
            var fixture = Human(false, out var proxy, out var view, out var binding);
            Assert.That(HumanLocomotionSetup.TryConfigure(view, proxy.ActorId, out var gait), Is.True);
            Assert.That(gait.SupportsStrikeLayer, Is.True); Assert.That(gait.SupportsCrouchWalk, Is.True);
            binding.BindLocomotion(gait);
            uint tick = 10; float z = 0;
            var velocity = new Vector3(0, 0, 1.55f);
            for (int i = 0; i < 20; i++)
            {
                z += 1.55f / 30f;
                binding.ApplySnapshot(HumanState(new Vector3(0, 0, z), velocity, true), tick++);
                yield return new WaitForSecondsRealtime(1f / 30f);
            }
            Assert.That(binding.UsingLocomotion, Is.True, "Walking uses the gait graph.");
            Vector3 origin = new Vector3(.25f, 1.25f, z + .45f), target = new Vector3(-.05f, 1.35f, z + .95f);
            binding.ApplyEvent(new GameplayEvent(1, 1, 1, tick, GameplayEventKind.StrikeStarted, proxy.ActorId, 0, 1,
                target.ToFloat(), Float3.Up));
            Assert.That(binding.TemporaryMotion, Is.EqualTo(-1), "No full-body Swat while walking.");
            float maximumElbow = 0, minimumLayer = 1;
            for (int i = 0; i < 16; i++)
            {
                z += 1.55f / 30f;
                var strike = new StrikeState(42, GameplayTools.Hands, 1, i < 3 ? StrikePhase.Windup : StrikePhase.Active,
                    tick, origin.ToFloat(), target.ToFloat(), Float3.Up, Mathf.Clamp01(i / 18f));
                binding.ApplySnapshot(HumanState(new Vector3(0, 0, z), velocity, true, strike: strike), tick++);
                yield return new WaitForSecondsRealtime(1f / 30f);
                Assert.That(binding.UsingLocomotion, Is.True, "Legs keep walking while striking (frame " + i + ").");
                if (i > 4) minimumLayer = Mathf.Min(minimumLayer, gait.StrikeLayerWeight);
                if (binding.LastStrikeSolveFrame >= Time.frameCount - 2) maximumElbow = Mathf.Max(maximumElbow, binding.LastElbowInnerDegrees);
            }
            Assert.That(minimumLayer, Is.GreaterThan(.5f), "The Swat plays on the upper-body layer.");
            Assert.That(maximumElbow, Is.LessThanOrEqualTo(ActorVisualBinding.StrikeElbowInnerDegrees + .5f), "The striking elbow stays bent.");
            Assert.That(binding.LastArmReachResidualMeters, Is.GreaterThanOrEqualTo(0));
            Object.Destroy(fixture);
        }

        [UnityTest]
        public IEnumerator StunnedMosquitoLoopsDizzyAndFlightTilts()
        {
            var root = Track(new GameObject("MosquitoFixture"));
            var proxy = root.AddComponent<GameplayActorProxy>();
            proxy.Initialize(new SpawnActor(9, "mosquito", PlayerRole.Mosquito, Float3.Zero));
            var visual = Object.Instantiate(Load(MosquitoPrefab), root.transform);
            var view = visual.GetComponent<CharacterView>();
            var binding = visual.AddComponent<ActorVisualBinding>();
            binding.Initialize(proxy, null, view, false);
            uint tick = 1; float z = 0;
            for (int i = 0; i < 20; i++)
            {
                z += 3f / 30f;
                binding.ApplySnapshot(MosquitoState(LifeState.Flying, new Vector3(0, 1, z), new Vector3(0, 0, 3f)), tick++);
                yield return new WaitForSecondsRealtime(1f / 30f);
            }
            Assert.That(binding.CurrentMotion, Is.EqualTo(Ids.MosquitoFly));
            float wingHz = view.Animator.speed * MosquitoWingbeat.WingbeatsPerLoop / (12f / 30f);
            Assert.That(wingHz, Is.InRange(8f, 12f), "Wingbeat from speed, not distance.");
            Assert.That(Quaternion.Angle(visual.transform.rotation, Quaternion.identity), Is.GreaterThan(6f), "Flying forward pitches the body.");
            Assert.That(binding.FlightTilt.eulerAngles.x, Is.InRange(5f, 20f));
            binding.ApplyEvent(new GameplayEvent(1, 1, 2, tick, GameplayEventKind.MosquitoKnockedDown, proxy.ActorId, 0, 1, Float3.Zero, Float3.Up));
            Assert.That(binding.TemporaryMotion, Is.EqualTo(Ids.MosquitoFall), "The swatted mosquito tumbles at once (no Hit flinch hiding the fall).");
            binding.ApplySnapshot(MosquitoState(LifeState.Falling, new Vector3(0, .5f, z), new Vector3(0, -2, 0)), tick++);
            yield return new WaitForSecondsRealtime(.35f);
            binding.ApplySnapshot(MosquitoState(LifeState.Stunned, new Vector3(0, .06f, z), Vector3.zero), tick++);
            // The 0.5 s tumble (Fall) finishes before the dizzy loop takes over.
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(binding.CurrentMotion, Is.EqualTo(Ids.MosquitoStunnedLoop), "Stunned is the dizzy loop, never the Hit pose.");
            yield return new WaitForSecondsRealtime(.6f);
            Assert.That(view.Animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Hit"), Is.False);
            Assert.That(binding.FlightTilt.eulerAngles.x, Is.LessThan(3f).Or.GreaterThan(357f), "Tilt eases out when not flying.");
        }

        [Test]
        public void FacialMoodsMoveBrowsJawLidsAndPupils()
        {
            var human = Track(Object.Instantiate(Load(HumanPrefab)));
            Assert.That(VisualAttentionFactory.TryInstall(human, true, out var humanRig, out var reason), Is.True, reason);
            Assert.That(humanRig.SupportsBrows && humanRig.SupportsJaw, Is.True);
            var brow = Find(human.transform, "Brow.L"); var jaw = Find(human.transform, "Jaw");
            humanRig.SetMood(FacialMood.Neutral);
            Settle(humanRig);
            Vector3 browNeutral = brow.position; Quaternion jawNeutral = jaw.localRotation;
            humanRig.PrepareForAnimation();
            humanRig.SetMood(FacialMood.Surprised);
            Settle(humanRig);
            var eye = Find(human.transform, "Eye.L");
            Assert.That(humanRig.SupportsEyeScale, Is.True);
            var decal = eye.localScale;
            // Round 3: the decals are lifted off the faceted globe along the look axis while the lids are open and
            // never narrowed below 0.82 (smaller decals sank into the facets: bitten edges and white holes).
            Assert.That(Mathf.Max(decal.x, Mathf.Max(decal.y, decal.z)), Is.InRange(1.03f, 1.07f), "Open-eyed pupils are lifted off the globe.");
            Assert.That(Mathf.Min(decal.x, Mathf.Min(decal.y, decal.z)), Is.GreaterThanOrEqualTo(.8f), "Pupils are never narrowed into the facets.");
            Assert.That(humanRig.SupportsMouthShapes, Is.True, "The human head carries the Smile/MouthO/Frown morphs.");
            var head = human.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r => r.name == "HumanHead");
            Assert.That(head.GetBlendShapeWeight(head.sharedMesh.GetBlendShapeIndex("MouthO")), Is.GreaterThan(80f), "Surprised opens an O mouth.");
            // Local: the surprised head also tips back, which cancels the open jaw in world space.
            Assert.That(Quaternion.Angle(jaw.localRotation, jawNeutral), Is.GreaterThan(8f), "Surprised drops the jaw.");
            humanRig.PrepareForAnimation();
            Assert.That(head.GetBlendShapeWeight(head.sharedMesh.GetBlendShapeIndex("MouthO")), Is.LessThan(.01f), "The mouth morph is restored.");
            Assert.That(eye.localScale.x, Is.EqualTo(1f).Within(1e-4f)); Assert.That(eye.localScale.z, Is.EqualTo(1f).Within(1e-4f));
            humanRig.SetMood(FacialMood.Happy);
            Settle(humanRig);
            Assert.That(head.GetBlendShapeWeight(head.sharedMesh.GetBlendShapeIndex("Smile")), Is.GreaterThan(95f), "Happy is a real open smile.");
            humanRig.PrepareForAnimation();
            humanRig.SetMood(FacialMood.Yawning);
            Settle(humanRig);
            Assert.That(head.GetBlendShapeWeight(head.sharedMesh.GetBlendShapeIndex("MouthO")), Is.GreaterThan(95f), "The yawn opens the mouth.");
            Assert.That(Quaternion.Angle(jaw.localRotation, jawNeutral), Is.GreaterThan(18f), "The yawn drops the jaw wide.");
            Assert.That(humanRig.LastPupilLift, Is.LessThan(1.02f), "Nearly closed lids pull the pupils back under the lid shell.");
            humanRig.PrepareForAnimation();
            humanRig.PrepareForAnimation();
            Assert.That(Vector3.Distance(brow.position, browNeutral), Is.LessThan(.0005f), "The rig restores its own writes.");
            humanRig.SetMood(FacialMood.Angry);
            Settle(humanRig);
            Assert.That(Vector3.Dot(brow.position - browNeutral, human.transform.up), Is.LessThan(-.004f), "Angry lowers the brows.");
            humanRig.PrepareForAnimation();
            Assert.That(eye.localScale.x, Is.EqualTo(eye.localScale.y).Within(1e-4f), "The squint is restored.");

            var mosquito = Track(Object.Instantiate(Load(MosquitoPrefab)));
            Assert.That(VisualAttentionFactory.TryInstall(mosquito, true, out var mosquitoRig, out reason), Is.True, reason);
            Assert.That(mosquitoRig.SupportsLowerLids, Is.True);
            var lid = Find(mosquito.transform, "LidUpper.L"); var pupil = Find(mosquito.transform, "Pupil.L");
            mosquitoRig.SetMood(FacialMood.Neutral);
            Settle(mosquitoRig);
            Quaternion lidNeutral = lid.localRotation; Vector3 pupilNeutral = pupil.localScale;
            mosquitoRig.PrepareForAnimation();
            mosquitoRig.SetMood(FacialMood.Angry);
            Settle(mosquitoRig);
            Assert.That(Quaternion.Angle(lid.localRotation, lidNeutral), Is.GreaterThan(40f), "Angry lowers and tilts the upper lid into a V.");
            Assert.That(mosquitoRig.MoodShape.Tilt, Is.GreaterThan(35f));
            Assert.That(mosquitoRig.MoodShape.Upper, Is.GreaterThan(.5f));
            Assert.That(pupil.localScale.magnitude, Is.LessThan(pupilNeutral.magnitude * .97f), "Angry pupils shrink.");
            mosquitoRig.PrepareForAnimation();
            mosquitoRig.SetMood(FacialMood.Dizzy);
            Settle(mosquitoRig);
            Assert.That(mosquitoRig.MoodShape.Dizzy, Is.GreaterThan(.9f));
        }

        [UnityTest]
        public IEnumerator MosquitoCameraKeepsItsOwnBodyUnderTheReticle()
        {
            var cameraObject = Track(new GameObject("FramingCamera", typeof(Camera)));
            var camera = cameraObject.GetComponent<Camera>(); camera.enabled = false; camera.nearClipPlane = .01f;
            var follow = cameraObject.AddComponent<MosquitoFollowCamera>();
            var body = Track(GameObject.CreatePrimitive(PrimitiveType.Cube)); body.transform.localScale = new Vector3(.12f, .1f, .2f);
            body.transform.position = new Vector3(0, 1, 0);
            follow.SetCollisionFilter(_ => false);
            follow.BindAnchors(body.transform, body.transform);
            follow.SetView(Quaternion.Euler(10, 0, 0), .85f);
            yield return new WaitForSecondsRealtime(.5f);
            float lift = follow.FramingLift(.85f);
            Assert.That(lift, Is.InRange(.12f, .2f));
            var ray = new Ray(camera.transform.position, camera.transform.forward);
            Vector3 closest = ray.origin + ray.direction * Vector3.Dot(body.transform.position - ray.origin, ray.direction);
            float clearance = Vector3.Dot(closest - body.transform.position, camera.transform.up);
            Assert.That(clearance, Is.GreaterThan(lift * .7f), "The reticle ray passes above the pivot.");
            Assert.That(body.GetComponent<Collider>().bounds.IntersectRay(ray), Is.False, "The body does not cover the reticle.");
            // W flies along the view: the reticle crosses that flight line at the convergence distance.
            Assert.That(ReticleMiss(camera, follow, follow.ReticleConvergenceMeters), Is.LessThan(.01f));
            Assert.That(ReticleMiss(camera, follow, 3f), Is.LessThan(.05f));
            Assert.That(ReticleMiss(camera, follow, 5f), Is.LessThan(.05f));
            Assert.That(follow.ResolvedDistance, Is.GreaterThan(.75f));
            follow.SetView(Quaternion.identity, 0);
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(follow.FramingLift(0), Is.Zero, "First person has no framing offset.");
        }

        [UnityTest]
        public IEnumerator MosquitoReticleConvergesWithTheFlightLineWhileTurning()
        {
            var cameraObject = Track(new GameObject("ConvergenceCamera", typeof(Camera)));
            var camera = cameraObject.GetComponent<Camera>(); camera.enabled = false; camera.nearClipPlane = .01f;
            var follow = cameraObject.AddComponent<MosquitoFollowCamera>();
            var body = Track(new GameObject("Body")); body.transform.position = new Vector3(0, 1, 0);
            follow.SetCollisionFilter(_ => false);
            follow.BindAnchors(body.transform, body.transform);
            float worst = 0;
            for (int i = 0; i < 40; i++)
            {
                follow.SetView(Quaternion.Euler(-20 + i, i * 6, 0), 1.6f);
                yield return null;
                if (i > 20) worst = Mathf.Max(worst, ReticleMiss(camera, follow, follow.ReticleConvergenceMeters));
            }
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(ReticleMiss(camera, follow, follow.ReticleConvergenceMeters), Is.LessThan(.01f));
            Assert.That(worst, Is.LessThan(.25f), "Even mid-turn the reticle stays near the flight line.");
        }

        [UnityTest]
        public IEnumerator HumanLandingWithTheGaitBoundDipsTheHipsAndSquashes()
        {
            var fixture = Human(false, out var proxy, out var view, out var binding);
            Assert.That(HumanLocomotionSetup.TryConfigure(view, proxy.ActorId, out var gait), Is.True);
            binding.BindLocomotion(gait);
            var hips = Find(view.Animator.transform, "Hips"); var rootBone = Find(view.Animator.transform, "Root");
            uint tick = 1;
            binding.ApplySnapshot(HumanState(Vector3.zero, Vector3.zero, true), tick++);
            yield return new WaitForSecondsRealtime(.3f);
            float standing = hips.position.y - view.transform.position.y;
            float start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < .75f)
            {
                float air = Time.realtimeSinceStartup - start;
                binding.ApplySnapshot(HumanState(new Vector3(0, Mathf.Max(0, 4.6f * air - 6f * air * air), 0),
                    new Vector3(0, 4.6f - 12f * air, 0), false), tick++);
                yield return new WaitForSecondsRealtime(1f / 30f);
            }
            binding.ApplySnapshot(HumanState(Vector3.zero, Vector3.zero, true), tick++);
            float contact = Time.realtimeSinceStartup, lowest = float.MaxValue, squash = float.MaxValue;
            do
            {
                yield return null;
                lowest = Mathf.Min(lowest, hips.position.y - view.transform.position.y);
                squash = Mathf.Min(squash, rootBone.localScale.y);
            } while (Time.realtimeSinceStartup - contact < .1f);
            Assert.That(binding.TemporaryMotion, Is.EqualTo(Ids.HumanLand));
            Assert.That(lowest, Is.LessThan(standing - .04f), "The hips visibly dip within 100 ms of touchdown.");
            Assert.That(squash, Is.LessThan(.95f), "The body squashes within 100 ms of touchdown.");
            Object.Destroy(fixture);
        }

        [UnityTest]
        public IEnumerator CrouchWalkFeetStayLowAndKneesMoveContinuously()
        {
            var fixture = Human(false, out var proxy, out var view, out var binding);
            Assert.That(HumanLocomotionSetup.TryConfigure(view, proxy.ActorId, out var gait), Is.True);
            binding.BindLocomotion(gait);
            Transform F(string n) => Find(view.Animator.transform, n);
            var feet = new[] { F("Foot.L"), F("Foot.R") }; var knees = new[] { F("LowerLeg.L"), F("LowerLeg.R") };
            var thighs = new[] { F("UpperLeg.L"), F("UpperLeg.R") };
            uint tick = 1; float z = 0, highest = 0, jump = 0; var previous = new float[2]; bool crouched = false;
            float start = Time.realtimeSinceStartup, nextSend = start;
            while (Time.realtimeSinceStartup - start < 2.6f)
            {
                float t = Time.realtimeSinceStartup - start;
                if (Time.realtimeSinceStartup >= nextSend)
                {
                    z += 1.55f / 30f; nextSend += 1f / 30f;
                    binding.ApplySnapshot(HumanState(new Vector3(0, 0, z), new Vector3(0, 0, 1.55f), true, Mathf.Clamp01((t - .4f) / .3f)), tick++);
                }
                yield return null;
                bool settled = t > 1.0f && gait.CrouchWeight > .99f && binding.UsingLocomotion;
                for (int i = 0; i < 2; i++)
                {
                    float knee = TwoBoneSolver.InnerAngle(thighs[i].position, knees[i].position, feet[i].position);
                    if (settled)
                    {
                        highest = Mathf.Max(highest, feet[i].position.y - view.transform.position.y);
                        if (crouched) jump = Mathf.Max(jump, Mathf.Abs(knee - previous[i]));
                    }
                    previous[i] = knee;
                }
                crouched = settled;
            }
            Assert.That(binding.UsingLocomotion, Is.True, "The crouched body keeps walking on the gait graph.");
            // Ankle rest height .12 m + the larger of swing lift (.07) and heel peel (.09) + 2 cm.
            Assert.That(highest, Is.LessThan(.23f), "No crouch-walk frame lifts a foot toward the hip.");
            Assert.That(jump, Is.LessThan(25f), "The knee angle changes continuously frame to frame.");
            Object.Destroy(fixture);
        }

        [UnityTest]
        public IEnumerator StoppingBlendsThroughAnIntermediatePose()
        {
            var fixture = Human(false, out var proxy, out var view, out var binding);
            Assert.That(HumanLocomotionSetup.TryConfigure(view, proxy.ActorId, out var gait), Is.True);
            binding.BindLocomotion(gait);
            var bones = view.Animator.GetComponentsInChildren<Transform>(true);
            uint tick = 1; float z = 0;
            for (int i = 0; i < 30; i++)
            {
                z += 1.55f / 30f;
                binding.ApplySnapshot(HumanState(new Vector3(0, 0, z), new Vector3(0, 0, 1.55f), true), tick++);
                yield return new WaitForSecondsRealtime(1f / 30f);
            }
            Assert.That(binding.UsingLocomotion, Is.True);
            var walking = Capture(bones);
            binding.ApplySnapshot(HumanState(new Vector3(0, 0, z), Vector3.zero, true), tick++);
            float stop = Time.realtimeSinceStartup;
            Quaternion[] early = null;
            while (Time.realtimeSinceStartup - stop < .6f)
            {
                yield return null;
                if (early == null && Time.realtimeSinceStartup - stop >= .09f) early = Capture(bones);
            }
            Assert.That(binding.UsingLocomotion, Is.False, "Standing still hands the pose back to the controller.");
            var idle = Capture(bones);
            int widest = 0; float total = 0;
            for (int i = 0; i < bones.Length; i++)
            {
                float angle = Quaternion.Angle(walking[i], idle[i]);
                if (angle > total) { total = angle; widest = i; }
            }
            Assert.That(total, Is.GreaterThan(5f), "The stride differs from the idle pose.");
            float done = Quaternion.Angle(walking[widest], early[widest]), left = Quaternion.Angle(early[widest], idle[widest]);
            Assert.That(done, Is.GreaterThan(.1f * total), bones[widest].name + " has started to settle at 90 ms.");
            Assert.That(left, Is.GreaterThan(.1f * total), bones[widest].name + " is still between stride and idle at 90 ms (no pop).");
            Object.Destroy(fixture);
        }

        [UnityTest]
        public IEnumerator FlyswatterStrikeAtMaximumReachStillConnects()
        {
            var fixture = Human(false, out var proxy, out var view, out var binding);
            var tool = MountFlyswatter(view, binding);
            uint tick = 1;
            binding.ApplySnapshot(HumanState(Vector3.zero, Vector3.zero, true, tool: GameplayTools.Flyswatter), tick++);
            yield return new WaitForSecondsRealtime(.2f);
            // The authority's own plan (UnityGameplayWorld.TryPlanStrike): the contact starts at the shoulder, 8 cm
            // forward, and ends at the aim point clamped to the maximum flyswatter reach from that shoulder.
            float reach = GameplayTools.FlyswatterShoulderReach;
            Vector3 shoulder = new Vector3(.21f, 1.39f, 0), origin = shoulder + Vector3.forward * .08f;
            Vector3 target = shoulder + new Vector3(-.25f, -.20f, 1f).normalized * reach;
            float worst = 0, worstElbow = 0, atImpact = -1, start = Time.realtimeSinceStartup; string assists = "";
            while (true)
            {
                float s = Time.realtimeSinceStartup - start;
                if (s >= StrikeVisualTrajectory.Duration) break;
                binding.ApplySnapshot(HumanState(Vector3.zero, Vector3.zero, true, strike: Strike(9, GameplayTools.Flyswatter, s, origin, target, tick),
                    tool: GameplayTools.Flyswatter), tick++);
                yield return null;
                if (binding.LastStrikeSolveFrame >= Time.frameCount - 1) worstElbow = Mathf.Max(worstElbow, binding.LastElbowInnerDegrees);
                if (s >= StrikeVisualTrajectory.SweepStart && s <= StrikeSwingPath.ImpactSeconds && binding.LastArmReachResidualMeters >= worst)
                {
                    worst = binding.LastArmReachResidualMeters;
                    assists = $" (clavicle {binding.LastClavicleAssistDegrees:F1}, lean {binding.LastLeanAssistDegrees:F1}, twist {binding.LastTorsoAssistDegrees:F1} deg at s={s:F3})";
                }
                if (atImpact < 0 && s >= StrikeSwingPath.ImpactSeconds)
                    atImpact = Vector3.Distance(tool.GetComponent<ToolView>().Impact.position, target);
            }
            Assert.That(worstElbow, Is.LessThanOrEqualTo(ActorVisualBinding.StrikeElbowInnerDegrees + .5f), "The swatting elbow stays bent.");
            Assert.That(worst, Is.LessThan(.02f), "At the authority's maximum flyswatter reach the visual swat still connects" + assists);
            Assert.That(atImpact, Is.InRange(0f, .06f), "The flyswatter head arrives on the authoritative target when the sweep ends.");
            Object.Destroy(tool);
            Object.Destroy(fixture);
        }

        /// <summary>
        /// anim-r3 (1): the flyswatter can never leave the hand. Every frame of the whole swat (windup, sweep,
        /// follow-through, settle), after every writer of the frame, the tool's grip stays on the hand's grip
        /// socket (the bone the skinned hand closes around).
        /// </summary>
        [UnityTest]
        public IEnumerator FlyswatterStaysInTheHandThroughTheWholeSwat()
        {
            var fixture = Human(false, out var proxy, out var view, out var binding);
            var tool = MountFlyswatter(view, binding);
            var toolView = tool.GetComponent<ToolView>();
            var grip = Find(view.Animator.transform, "Socket.Grip.R");
            var probe = Track(new GameObject("GripProbe")).AddComponent<LateFrameProbe>();
            float worst = 0; int frames = 0;
            probe.Measure = () => { if (tool.activeInHierarchy) { worst = Mathf.Max(worst, Vector3.Distance(toolView.Grip.position, grip.position)); frames++; } };
            uint tick = 1;
            binding.ApplySnapshot(HumanState(Vector3.zero, Vector3.zero, true, tool: GameplayTools.Flyswatter), tick++);
            yield return new WaitForSecondsRealtime(.2f);
            Vector3 shoulder = new Vector3(.21f, 1.39f, 0), origin = shoulder + Vector3.forward * .08f;
            Vector3 target = shoulder + new Vector3(.2f, -.35f, 1f).normalized * .8f;
            binding.ApplyEvent(new GameplayEvent(1, 1, 1, tick, GameplayEventKind.StrikeStarted, proxy.ActorId, 0, 1, target.ToFloat(), Float3.Up));
            float start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < StrikeVisualTrajectory.Duration + .4f)
            {
                float s = Time.realtimeSinceStartup - start;
                var strike = s < StrikeVisualTrajectory.Duration ? Strike(10, GameplayTools.Flyswatter, s, origin, target, tick) : default;
                binding.ApplySnapshot(HumanState(Vector3.zero, Vector3.zero, true, strike: strike, tool: GameplayTools.Flyswatter), tick++);
                yield return null;
            }
            yield return null;
            Assert.That(frames, Is.GreaterThan(20));
            Assert.That(worst, Is.LessThan(.01f), "The flyswatter grip stays within 1 cm of the hand on every frame of the swat.");
            Object.Destroy(tool);
            Object.Destroy(fixture);
        }

        /// <summary>
        /// anim-r3 (2): a comic slap, not a straight-arm push. The hand cocks above the shoulder during the windup,
        /// the elbow never opens past 120 deg, the torso leans forward (never back) into the impact, and the
        /// palm arrives on the authoritative target when the sweep ends.
        /// </summary>
        [UnityTest]
        public IEnumerator BareHandSwatCocksWhipsForwardAndLandsOnTheTarget()
        {
            var fixture = Human(false, out var proxy, out var view, out var binding);
            uint tick = 1;
            binding.ApplySnapshot(HumanState(Vector3.zero, Vector3.zero, true), tick++);
            yield return new WaitForSecondsRealtime(.3f);
            var palm = Find(view.Animator.transform, "Socket.Grip.R"); var upperArm = Find(view.Animator.transform, "UpperArm.R");
            var chest = Find(view.Animator.transform, "Chest"); var spine = Find(view.Animator.transform, "Spine");
            Vector3 shoulder = new Vector3(.21f, 1.39f, 0), origin = shoulder + Vector3.forward * .08f;
            Vector3 target = new Vector3(.02f, 1.28f, .62f);
            binding.ApplyEvent(new GameplayEvent(1, 1, 1, tick, GameplayEventKind.StrikeStarted, proxy.ActorId, 0, 1, target.ToFloat(), Float3.Up));
            Assert.That(binding.TemporaryMotion, Is.EqualTo(Ids.HumanSwat));
            float worstElbow = 0, cocked = float.MinValue, lean = float.MinValue, leanBack = 0, atImpact = -1; string impactNote = "";
            float start = Time.realtimeSinceStartup;
            while (true)
            {
                float s = Time.realtimeSinceStartup - start;
                if (s >= StrikeVisualTrajectory.Duration) break;
                binding.ApplySnapshot(HumanState(Vector3.zero, Vector3.zero, true, strike: Strike(11, GameplayTools.Hands, s, origin, target, tick)), tick++);
                yield return null;
                if (binding.LastStrikeSolveFrame >= Time.frameCount - 1) worstElbow = Mathf.Max(worstElbow, binding.LastElbowInnerDegrees);
                if (s > .05f && s < StrikeVisualTrajectory.SweepStart + .02f) cocked = Mathf.Max(cocked, palm.position.y - upperArm.position.y);
                // Torso pitch: the chest top ahead of the waist (positive = leaning forward).
                float pitch = Mathf.Atan2(chest.position.z - spine.position.z + (Find(view.Animator.transform, "Neck").position.z - chest.position.z),
                    Find(view.Animator.transform, "Neck").position.y - spine.position.y) * Mathf.Rad2Deg;
                if (s > .15f && s < .35f) { lean = Mathf.Max(lean, pitch); leanBack = Mathf.Min(leanBack, pitch); }
                if (atImpact < 0 && s >= StrikeSwingPath.ImpactSeconds)
                {
                    atImpact = Vector3.Distance(palm.position, target);
                    impactNote = $" (s={s:F3}, residual {binding.LastArmReachResidualMeters:F3} m, clavicle {binding.LastClavicleAssistDegrees:F1}, lean {binding.LastLeanAssistDegrees:F1}, twist {binding.LastTorsoAssistDegrees:F1} deg, elbow {binding.LastElbowInnerDegrees:F1})";
                }
            }
            Assert.That(cocked, Is.GreaterThan(.15f), "Anticipation: the hand cocks well above the shoulder, behind the ear.");
            Assert.That(worstElbow, Is.LessThanOrEqualTo(ActorVisualBinding.StrikeElbowInnerDegrees + .5f), "The elbow stays bent (<=120 deg).");
            Assert.That(lean, Is.GreaterThan(12f), "The torso leans forward into the slap.");
            Assert.That(leanBack, Is.GreaterThan(-3f), "The torso never leans back while striking.");
            Assert.That(atImpact, Is.InRange(0f, .035f), "The palm lands on the authoritative target when the sweep ends" + impactNote);
            Object.Destroy(fixture);
        }

        /// <summary>One uneven frame (a render hitch or a late snapshot) must not blend a walking body into a
        /// one-frame trot or run stride: the gait blend follows a smoothed rendered speed.</summary>
        [Test]
        public void GaitBlendIgnoresASingleUnevenFrame()
        {
            var clock = new HumanLocomotionClock(new HumanLocomotionClock.Profile(1f, .8333f, 2f),
                new HumanLocomotionClock.Profile(1.55f, .96875f, 2f), new HumanLocomotionClock.Profile(3.1f, 1.55f, 2f),
                new HumanLocomotionClock.Profile(5f, 2.1739f, 2f));
            double z = 0, dt = 1.0 / 60;
            int frame = 0;
            for (; frame < 60; frame++) { z += 1.55 * dt; clock.Advance(0, z, dt, frame, true); }
            Assert.That(clock.Current.UpperProfile, Is.LessThanOrEqualTo(2));
            Assert.That(clock.Current.LowerProfile, Is.EqualTo(1), "Steady 1.55 m/s is the Walk profile.");
            // A hitch: this frame's rendered displacement is three frames' worth.
            z += 1.55 * dt * 3; var spike = clock.Advance(0, z, dt, frame++, true);
            Assert.That(spike.LowerProfile, Is.EqualTo(1), "A single uneven frame never jumps to the trot or run clip.");
            Assert.That(spike.Blend, Is.LessThan(.35), "The blend toward the trot stays small.");
            for (int i = 0; i < 30; i++) { z += 1.55 * dt; clock.Advance(0, z, dt, frame++, true); }
            Assert.That(clock.GaitSpeed, Is.EqualTo(1.55).Within(.05), "The smoothed speed settles back.");
        }

        [Test]
        public void CrouchedFootstepsAreSilent()
        {
            ActorSnapshot State(float crouch, bool grounded = true) => new ActorSnapshot(7, PlayerRole.Human, LifeState.Active, 1,
                Float3.Zero, new Float3(0, 0, 1.55f), Rotation.Identity, Float3.Forward, 0, 0, 1, 1, grounded, crouch, 0, null, null, default, 0);
            Assert.That(GameplayAudioPresenter.PlaysFootstep(State(0)), Is.True, "Walking steps are heard.");
            Assert.That(GameplayAudioPresenter.PlaysFootstep(State(.2f)), Is.True);
            Assert.That(GameplayAudioPresenter.PlaysFootstep(State(.3f)), Is.False, "A sneaking (crouched) human makes no footstep sound.");
            Assert.That(GameplayAudioPresenter.PlaysFootstep(State(1)), Is.False);
            Assert.That(GameplayAudioPresenter.PlaysFootstep(State(0, false)), Is.False);
            Assert.That(GameplayAudioPresenter.PlaysFootstep(null), Is.False);
        }

        /// <summary>anim-r3 (10): the crouched first-person eye goes back to ~0.88 m (the authority aims from
        /// 0.89 m), well inside the 1.0 m crouched capsule with the 0.04 m near clip, standing or sneaking.</summary>
        [UnityTest]
        public IEnumerator CrouchedEyeStaysWellInsideTheCrouchedCapsule()
        {
            var fixture = Human(false, out var proxy, out var view, out var binding);
            Assert.That(HumanLocomotionSetup.TryConfigure(view, proxy.ActorId, out var gait), Is.True);
            binding.BindLocomotion(gait);
            var eye = view.GetAnchor("CameraEye");
            Assert.That(eye, Is.Not.Null);
            uint tick = 1; float z = 0, highest = 0, lowest = float.MaxValue;
            float start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < 2.4f)
            {
                float t = Time.realtimeSinceStartup - start;
                bool moving = t > 1.2f;
                if (moving) z += 1.55f / 60f;
                binding.ApplySnapshot(HumanState(new Vector3(0, 0, z), moving ? new Vector3(0, 0, 1.55f) : Vector3.zero, true, 1), tick++);
                yield return null;
                view.RefreshAnchors();
                if ((t > .9f && t < 1.2f) || t > 1.7f)
                {
                    float height = eye.position.y - view.transform.position.y;
                    highest = Mathf.Max(highest, height); lowest = Mathf.Min(lowest, height);
                }
            }
            Assert.That(highest, Is.LessThanOrEqualTo(.92f), "The crouched eye stays >= 8 cm under the 1.0 m capsule top.");
            Assert.That(lowest, Is.GreaterThan(.78f), "The crouched eye stays near the authority's 0.89 m aim eye.");
            Object.Destroy(fixture);
        }

        /// <summary>anim-r3 (review risk): the local first-person human's moods never pitch or roll the head the
        /// camera rides on; a remote human's surprised head does tip back.</summary>
        [UnityTest]
        public IEnumerator LocalHumanMoodsNeverMoveTheCameraEye()
        {
            var applied = new float[2];
            for (int pass = 0; pass < 2; pass++)
            {
                bool local = pass == 0;
                var fixture = Human(local, out var proxy, out var view, out var binding);
                Assert.That(VisualAttentionFactory.TryInstall(view.gameObject, false, out var rig, out var reason), Is.True, reason);
                uint tick = 1;
                binding.ApplySnapshot(HumanState(Vector3.zero, Vector3.zero, true), tick++);
                for (int i = 0; i < 20; i++) yield return null;
                // Bitten: surprised (head tipped back 13 deg) for 0.6 s, then angry (chin down).
                binding.ApplyEvent(new GameplayEvent(1, 1, 1, tick, GameplayEventKind.BiteStarted, 99, proxy.ActorId, 1, Float3.Zero, Float3.Up));
                float worst = 0, until = Time.realtimeSinceStartup + .5f;
                while (Time.realtimeSinceStartup < until)
                {
                    binding.ApplySnapshot(HumanState(Vector3.zero, Vector3.zero, true), tick++);
                    yield return null;
                    worst = Mathf.Max(worst, rig.LastMoodHeadDegrees);
                }
                Assert.That(rig.Mood, Is.EqualTo(FacialMood.Surprised));
                Assert.That(rig.MoodHeadPoseEnabled, Is.EqualTo(!local));
                applied[pass] = worst;
                Object.Destroy(fixture);
                yield return null;
            }
            Assert.That(applied[0], Is.EqualTo(0f), "The local human's moods never turn the head its camera eye rides on.");
            Assert.That(applied[1], Is.GreaterThan(8f), "A remote human's surprised head does tip back.");
        }

        [UnityTest]
        public IEnumerator MosquitoKnockoutLiesBellyUpAndRecoversInTime()
        {
            var root = Track(new GameObject("MosquitoKnockout"));
            var proxy = root.AddComponent<GameplayActorProxy>();
            proxy.Initialize(new SpawnActor(9, "mosquito", PlayerRole.Mosquito, Float3.Zero));
            var visual = Object.Instantiate(Load(MosquitoPrefab), root.transform);
            var view = visual.GetComponent<CharacterView>();
            var binding = visual.AddComponent<ActorVisualBinding>();
            binding.Initialize(proxy, null, view, false);
            uint tick = 1;
            binding.ApplySnapshot(MosquitoState(LifeState.Stunned, new Vector3(0, .06f, 0), Vector3.zero), tick++);
            yield return new WaitForSecondsRealtime(.8f);
            Assert.That(binding.CurrentMotion, Is.EqualTo(Ids.MosquitoStunnedLoop));
            var thorax = Find(view.Animator.transform, "Thorax");
            float feet = 0; int count = 0;
            foreach (var bone in view.Animator.GetComponentsInChildren<Transform>(true))
                if (bone.name.StartsWith("Leg") && bone.name.Contains("03.")) { feet += bone.position.y; count++; }
            Assert.That(count, Is.EqualTo(6));
            Assert.That(feet / count, Is.GreaterThan(thorax.position.y), "Knocked out belly up: the six feet point at the sky.");
            var recovering = new ActorSnapshot(9, PlayerRole.Mosquito, LifeState.Recovering, 1, new Float3(0, .06f, 0), Float3.Zero,
                Rotation.Identity, Float3.Forward, 0, 0, 1, 1, true, 0, 0, null, null, default, tick + 12);
            binding.ApplySnapshot(recovering, tick);
            yield return null;
            Assert.That(binding.CurrentMotion, Is.EqualTo(Ids.MosquitoRecover));
            Assert.That(view.Animator.speed, Is.InRange(.9f, 1.3f), "The short Recover clip fits the 0.4 s window at normal speed.");
            float normalized = view.Animator.GetNextAnimatorStateInfo(0).normalizedTime;
            if (!view.Animator.IsInTransition(0)) normalized = view.Animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
            Assert.That(normalized, Is.LessThan(.25f), "Recover starts from its beginning (the belly-up roll), not its tail.");
        }

        [Test]
        public void EveryMoodDiffersFromEveryOtherAtAGlance()
        {
            var moods = new[] { FacialMood.Neutral, FacialMood.Happy, FacialMood.Angry, FacialMood.Alert, FacialMood.Sleepy,
                FacialMood.Surprised, FacialMood.Focused, FacialMood.Dizzy, FacialMood.Excited, FacialMood.Yawning };
            foreach (bool human in new[] { true, false })
                for (int a = 0; a < moods.Length; a++)
                    for (int b = a + 1; b < moods.Length; b++)
                        Assert.That(FacialMoodShape.Distance(FacialMoodShape.For(moods[a], !human), FacialMoodShape.For(moods[b], !human), human),
                            Is.GreaterThan(.6f), $"{(human ? "human" : "mosquito")} {moods[a]} vs {moods[b]}");
            // Round 3: the mosquito's attentive moods differ in their lids, not only in pupils or head pose.
            foreach (var (a, b) in new[] { (FacialMood.Alert, FacialMood.Neutral), (FacialMood.Focused, FacialMood.Angry),
                (FacialMood.Excited, FacialMood.Happy), (FacialMood.Alert, FacialMood.Focused), (FacialMood.Excited, FacialMood.Surprised) })
            {
                var x = FacialMoodShape.For(a, true); var y = FacialMoodShape.For(b, true);
                Assert.That(Mathf.Abs(x.Upper - y.Upper) + Mathf.Abs(x.Lower - y.Lower) + Mathf.Abs(x.Tilt - y.Tilt) / 30f,
                    Is.GreaterThan(.14f), $"mosquito lids {a} vs {b}");
            }
            // Human moods that the sketches read by the mouth.
            Assert.That(FacialMoodShape.For(FacialMood.Happy).Smile, Is.EqualTo(1f));
            Assert.That(FacialMoodShape.For(FacialMood.Excited).Smile, Is.GreaterThan(.8f));
            Assert.That(FacialMoodShape.For(FacialMood.Yawning).MouthOpen, Is.EqualTo(1f));
            Assert.That(FacialMoodShape.For(FacialMood.Angry).Frown, Is.EqualTo(1f));
        }

        [UnityTest]
        public IEnumerator UpperBodyMasksDoNotAccumulateOverRespawns()
        {
            int Masks() { int n = 0; foreach (var mask in Resources.FindObjectsOfTypeAll<AvatarMask>()) if (mask && mask.name == "LMS_HumanUpperBody") n++; return n; }
            AvatarMask shared = null;
            int first = -1;
            for (int i = 0; i < 4; i++)
            {
                var fixture = Human(false, out var proxy, out var view, out var binding);
                Assert.That(HumanLocomotionSetup.TryConfigure(view, proxy.ActorId, out var gait), Is.True);
                Assert.That(gait.UpperBodyMask, Is.Not.Null);
                if (shared == null) { shared = gait.UpperBodyMask; first = Masks(); }
                Assert.That(gait.UpperBodyMask, Is.SameAs(shared), "Every actor of the rig shares one upper-body mask.");
                Assert.That(Masks(), Is.EqualTo(first), "Respawns never allocate another mask.");
                Object.Destroy(fixture);
                yield return null;
                yield return null;
            }
            Assert.That(shared != null, Is.True, "Destroying an actor never destroys the shared mask.");
            Assert.That(Masks(), Is.EqualTo(first));
        }

        [UnityTest]
        public IEnumerator HumanLongIdleYawnsWithTheHandsAboveTheHead()
        {
            var fixture = Human(false, out var proxy, out var view, out var binding);
            var crown = Find(view.Animator.transform, "Socket.Head"); var knuckles = Find(view.Animator.transform, "Middle01.L");
            VisualAttentionFactory.TryInstall(view.gameObject, false, out _, out _);
            binding.ApplySnapshot(HumanState(Vector3.zero, Vector3.zero, true), 1);
            float start = Time.realtimeSinceStartup, highest = float.MinValue;
            bool yawned = false;
            while (Time.realtimeSinceStartup - start < GameplayMoodPolicy.IdleSleepySeconds + 3.5f)
            {
                yield return null;
                if (binding.TemporaryMotion == Ids.HumanYawn) yawned = true;
                if (yawned) highest = Mathf.Max(highest, knuckles.position.y - crown.position.y);
            }
            Assert.That(yawned, Is.True, "A long calm idle plays the Yawn body clip.");
            Assert.That(highest, Is.GreaterThan(.03f), "The stretch lifts the fists above the crown of the head.");
            Object.Destroy(fixture);
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>Calls Measure after every other writer of the frame (anchors, facial rig, cameras).</summary>
        [DefaultExecutionOrder(5000)]
        private sealed class LateFrameProbe : MonoBehaviour
        {
            public System.Action Measure;
            private void LateUpdate() => Measure?.Invoke();
        }

        /// <summary>Mounts LMS_Flyswatter on ToolSocket_R exactly like GameplayVisualPresenter.AttachTool.</summary>
        private GameObject MountFlyswatter(CharacterView view, ActorVisualBinding binding)
        {
            var tool = Track(Object.Instantiate(Load("Assets/LetMeSleep/Content/Characters/Prefabs/LMS_Flyswatter.prefab")));
            var socket = view.GetAnchor("ToolSocket_R");
            Assert.That(socket, Is.Not.Null);
            tool.transform.SetParent(socket, false);
            var toolView = tool.GetComponent<ToolView>();
            tool.transform.rotation = socket.rotation * Quaternion.Inverse(toolView.Grip.rotation) * tool.transform.rotation;
            tool.transform.position += socket.position - toolView.Grip.position;
            binding.BindTool(GameplayTools.Flyswatter, tool);
            return tool;
        }

        private static StrikeState Strike(uint id, string tool, float seconds, Vector3 origin, Vector3 target, uint tick)
        {
            var phase = seconds < StrikeVisualTrajectory.SweepStart ? StrikePhase.Windup :
                seconds < StrikeVisualTrajectory.SweepStart + StrikeVisualTrajectory.SweepDuration ? StrikePhase.Active : StrikePhase.Recovery;
            return new StrikeState(id, tool, 1, phase, tick, origin.ToFloat(), target.ToFloat(), Float3.Up,
                Mathf.Clamp01(seconds / StrikeVisualTrajectory.Duration));
        }

        private static float ReticleMiss(Camera camera, MosquitoFollowCamera follow, float distance)
        {
            Vector3 point = follow.FlightLineOrigin + follow.FlightLineDirection * distance;
            var ray = new Ray(camera.transform.position, camera.transform.forward);
            return Vector3.Cross(ray.direction, point - ray.origin).magnitude;
        }

        private static Quaternion[] Capture(Transform[] bones)
        {
            var result = new Quaternion[bones.Length];
            for (int i = 0; i < bones.Length; i++) result[i] = bones[i].localRotation;
            return result;
        }

        private GameObject Human(bool local, out GameplayActorProxy proxy, out CharacterView view, out ActorVisualBinding binding)
        {
            var root = Track(new GameObject("HumanFixture"));
            proxy = root.AddComponent<GameplayActorProxy>();
            proxy.Initialize(new SpawnActor(7, "human", PlayerRole.Human, Float3.Zero));
            var visual = Object.Instantiate(Load(HumanPrefab), root.transform);
            view = visual.GetComponent<CharacterView>();
            binding = visual.AddComponent<ActorVisualBinding>();
            binding.Initialize(proxy, null, view, local);
            return root;
        }

        private static ActorSnapshot HumanState(Vector3 position, Vector3 velocity, bool grounded, float crouch = 0,
            StrikeState strike = default, string tool = null)
        {
            return new ActorSnapshot(7, PlayerRole.Human, LifeState.Active, 1, position.ToFloat(),
                velocity.ToFloat(), Rotation.Identity, Float3.Forward, 0, 0, 1, 1, grounded, crouch, 0, null, null, strike, 0,
                tool ?? GameplayTools.Hands);
        }

        private static ActorSnapshot MosquitoState(LifeState state, Vector3 position, Vector3 velocity) => new ActorSnapshot(
            9, PlayerRole.Mosquito, state, 1, position.ToFloat(), velocity.ToFloat(), Rotation.Identity, Float3.Forward,
            0, 0, 1, 1, state == LifeState.Stunned, 0, 0, null, null, default, 0);

        private static GameplayMoodPolicy.Frame Frame(PlayerRole role, LifeState state, bool near = false) =>
            new GameplayMoodPolicy.Frame(role, state, 0, true, 0, false, near);

        private static FacialMood Mood(GameplayMoodPolicy policy, PlayerRole role, LifeState state, double now, bool near = false) =>
            policy.Evaluate(Frame(role, state, near), now).Mood;

        private static void Settle(VisualAttentionRig rig)
        {
            for (int i = 0; i < 12; i++) { rig.PrepareForAnimation(); rig.EvaluateAfterAnimation(.1f); }
        }

        private static void AssertMotion(CharacterView view, int id, string state, bool loop)
        {
            CharacterView.MotionBinding found = null;
            foreach (var motion in view.Motions) if (motion.Id == id) found = motion;
            Assert.That(found, Is.Not.Null, state);
            Assert.That(found.StateName, Is.EqualTo(state));
            Assert.That(found.Loop, Is.EqualTo(loop), state);
        }

        private static GameObject Load(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null, path);
            return prefab;
        }

        private GameObject Track(GameObject item) { created.Add(item); return item; }

        private static Transform Find(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++) { var found = Find(root.GetChild(i), name); if (found) return found; }
            return null;
        }

        private static bool Finite(Vector3 value) =>
            !float.IsNaN(value.x) && !float.IsInfinity(value.x) && !float.IsNaN(value.y) &&
            !float.IsInfinity(value.y) && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
    }
}
#endif
