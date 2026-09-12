using System.Collections;
using System.Linq;
using LetMeSleep.Bootstrap;
using LetMeSleep.Core;
using LetMeSleep.Gameplay;
using LetMeSleep.Gameplay.Unity;
using LetMeSleep.Presentation;
using LetMeSleep.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace LetMeSleep.Tests.PlayMode
{
    public sealed class TrainingBootstrapPlayModeTests
    {
        private const string BootScene = "LetMeSleepBoot";
        private AlfaApplication application;

        [UnitySetUp]
        public IEnumerator LoadBootScene()
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            AsyncOperation load = SceneManager.LoadSceneAsync(BootScene, LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null, $"{BootScene} must be present and enabled in Build Settings.");
            while (!load.isDone)
                yield return null;

            yield return null;
            application = Object.FindFirstObjectByType<AlfaApplication>();
            Assert.That(application, Is.Not.Null, "The boot scene must start AlfaApplication.");
        }

        [UnityTearDown]
        public IEnumerator RestoreApplicationState()
        {
            if (application != null)
                application.CancelTraining();
            yield return null;
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        [UnityTest]
        public IEnumerator HumanTrainingBuildsAndCleansACompleteRuntime()
        {
            return ExerciseTraining(AlfaRole.Human, PlayerRole.Human);
        }

        [UnityTest]
        public IEnumerator MosquitoTrainingBuildsAndCleansACompleteRuntime()
        {
            return ExerciseTraining(AlfaRole.Mosquito, PlayerRole.Mosquito);
        }

        private IEnumerator ExerciseTraining(AlfaRole selectedRole, PlayerRole expectedLocalRole)
        {
            application.StartTraining(selectedRole, AlfaUiController.BloodModeId, RoomRules.AlfaMap);
            yield return null;
            yield return null;

            GameplayRuntime runtime = Object.FindFirstObjectByType<GameplayRuntime>();
            AssertTrainingRuntime(runtime, expectedLocalRole);

            for (int frame = 0; frame < 5; frame++)
                yield return null;

            AssertTrainingRuntime(runtime, expectedLocalRole);

            application.CancelTraining();
            yield return null;
            yield return null;

            Assert.That(Object.FindFirstObjectByType<GameplayRuntime>(), Is.Null,
                "CancelTraining must destroy the active gameplay runtime.");
            Assert.That(Object.FindObjectsByType<GameplayActorProxy>(FindObjectsInactive.Exclude, FindObjectsSortMode.None), Is.Empty,
                "CancelTraining must remove every active gameplay actor.");
            Assert.That(Object.FindObjectsByType<GameplayToolPickup>(FindObjectsInactive.Exclude, FindObjectsSortMode.None), Is.Empty,
                "CancelTraining must remove the house pickup geometry.");
            Assert.That(application.MenuCamera.enabled, Is.True,
                "CancelTraining must restore the menu camera.");
            AssertSingleEnabledCamera(application.MenuCamera, "after leaving training");
        }

        private void AssertTrainingRuntime(GameplayRuntime runtime, PlayerRole expectedLocalRole)
        {
            Assert.That(runtime, Is.Not.Null, "StartTraining must create GameplayRuntime.");
            Assert.That(runtime.gameObject.activeInHierarchy, Is.True);
            Assert.That(runtime.IsHost, Is.True, "Training must run local authority.");
            Assert.That(runtime.AutomaticTick, Is.True, "Training must advance without an online host.");
            Assert.That(runtime.World, Is.Not.Null);
            Assert.That(runtime.World.MapRoot, Is.Not.Null);
            Assert.That(runtime.World.MapRoot.gameObject.activeInHierarchy, Is.True);
            Assert.That(runtime.LatestSnapshot, Is.Not.Null, "Training must begin a round immediately.");
            Assert.That(runtime.LatestSnapshot.SimulationPhase, Is.EqualTo(SimulationPhase.Running));
            Assert.That(runtime.LatestSnapshot.MapId, Is.EqualTo(RoomRules.AlfaMap));

            GameplayActorProxy[] actors = runtime.World.Actors.Values.OrderBy(actor => actor.ActorId).ToArray();
            Assert.That(actors, Has.Length.EqualTo(3), "Training must create one local actor and two bots.");
            Assert.That(actors.Select(actor => actor.ActorId).Distinct().Count(), Is.EqualTo(3));
            Assert.That(actors.Count(actor => actor.Role == PlayerRole.Human), Is.EqualTo(1));
            Assert.That(actors.Count(actor => actor.Role == PlayerRole.Mosquito), Is.EqualTo(2));

            GameplayActorProxy local = actors.Single(actor => actor.ActorId == runtime.LocalActorId);
            Assert.That(local.Role, Is.EqualTo(expectedLocalRole));
            foreach (GameplayActorProxy actor in actors)
            {
                Assert.That(actor.gameObject.activeInHierarchy, Is.True);
                Assert.That(actor.MotorCollider, Is.Not.Null, $"Actor {actor.ActorId} needs a physical motor collider.");
                Assert.That(actor.MotorCollider.enabled, Is.True);
                AssertFinite(actor.transform, $"actor {actor.ActorId}");
            }

            GameplayToolPickup[] pickups = Object.FindObjectsByType<GameplayToolPickup>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            Assert.That(pickups, Has.Length.EqualTo(7), "The alfa house must expose seven physical flyswatter pickups.");
            Assert.That(runtime.World.GetToolDefinitions().Count, Is.EqualTo(7));
            Assert.That(runtime.LatestSnapshot.ToolPickups.Count, Is.EqualTo(7));
            CollectionAssert.AreEquivalent(
                pickups.Select(pickup => pickup.PickupId),
                runtime.LatestSnapshot.ToolPickups.Select(pickup => pickup.PickupId));
            foreach (GameplayToolPickup pickup in pickups)
            {
                Assert.That(pickup.transform.IsChildOf(runtime.World.MapRoot), Is.True,
                    $"Pickup {pickup.PickupId} must belong to the active gameplay map.");
                Assert.That(pickup.InteractionCollider, Is.Not.Null,
                    $"Pickup {pickup.PickupId} needs an interaction collider.");
                ToolPickupSnapshot state = runtime.LatestSnapshot.ToolPickups.Single(item => item.PickupId == pickup.PickupId);
                Assert.That(pickup.OwnerActorId, Is.EqualTo(state.OwnerActorId));
                Assert.That(pickup.InteractionCollider.enabled, Is.EqualTo(state.OwnerActorId == 0),
                    $"Pickup {pickup.PickupId} physics must match replicated ownership.");
                Assert.That(pickup.InteractionCollider.isTrigger, Is.True);
                AssertFinite(pickup.transform, $"pickup {pickup.PickupId}");
            }

            foreach (ActorSnapshot actor in runtime.LatestSnapshot.Actors)
            {
                Assert.That(actor.Position.IsFinite, Is.True, $"Actor {actor.ActorId} position contains NaN/Infinity.");
                Assert.That(actor.Velocity.IsFinite, Is.True, $"Actor {actor.ActorId} velocity contains NaN/Infinity.");
                Assert.That(actor.ViewForward.IsFinite, Is.True, $"Actor {actor.ActorId} view contains NaN/Infinity.");
                Assert.That(MathEx.Finite(actor.ViewYawRadians), Is.True);
                Assert.That(MathEx.Finite(actor.ViewPitchRadians), Is.True);
                AssertRotationFinite(actor.BodyRotation, $"actor {actor.ActorId} body rotation");
            }
            foreach (ToolPickupSnapshot pickup in runtime.LatestSnapshot.ToolPickups)
            {
                Assert.That(pickup.Position.IsFinite, Is.True, $"Pickup {pickup.PickupId} position contains NaN/Infinity.");
                AssertRotationFinite(pickup.Rotation, $"pickup {pickup.PickupId} rotation");
            }
            Assert.That(MathEx.Finite(runtime.LatestSnapshot.BloodCollected), Is.True);
            Assert.That(MathEx.Finite(runtime.LatestSnapshot.BloodGoal), Is.True);

            Assert.That(application.MenuCamera, Is.Not.Null);
            Assert.That(application.MenuCamera.enabled, Is.False,
                "Gameplay must disable the menu camera.");
            Camera gameplayCamera = AssertSingleEnabledCamera(null, "during training");
            Assert.That(gameplayCamera.name, Is.EqualTo("PlayerCamera"));
            HumanViewCamera humanCamera = gameplayCamera.GetComponent<HumanViewCamera>();
            MosquitoFollowCamera mosquitoCamera = gameplayCamera.GetComponent<MosquitoFollowCamera>();
            Assert.That(humanCamera, Is.Not.Null);
            Assert.That(mosquitoCamera, Is.Not.Null);
            Assert.That(humanCamera.enabled, Is.EqualTo(expectedLocalRole == PlayerRole.Human));
            Assert.That(mosquitoCamera.enabled, Is.EqualTo(expectedLocalRole == PlayerRole.Mosquito));
            AssertFinite(gameplayCamera.transform, "gameplay camera");
        }

        private static Camera AssertSingleEnabledCamera(Camera expected, string context)
        {
            Camera[] enabled = Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(camera => camera.enabled && camera.gameObject.activeInHierarchy)
                .ToArray();
            Assert.That(enabled, Has.Length.EqualTo(1), $"Exactly one camera must render {context}.");
            if (expected != null)
                Assert.That(enabled[0], Is.SameAs(expected));
            return enabled[0];
        }

        private static void AssertFinite(Transform value, string label)
        {
            Assert.That(IsFinite(value.position), Is.True, $"{label} position contains NaN/Infinity.");
            Assert.That(IsFinite(value.rotation), Is.True, $"{label} rotation contains NaN/Infinity.");
            Assert.That(IsFinite(value.localScale), Is.True, $"{label} scale contains NaN/Infinity.");
        }

        private static void AssertRotationFinite(Rotation value, string label)
        {
            Assert.That(MathEx.Finite(value.X) && MathEx.Finite(value.Y) && MathEx.Finite(value.Z) && MathEx.Finite(value.W),
                Is.True, $"{label} contains NaN/Infinity.");
            float lengthSquared = value.X * value.X + value.Y * value.Y + value.Z * value.Z + value.W * value.W;
            Assert.That(lengthSquared, Is.GreaterThan(0.000001f), $"{label} must be non-zero.");
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(Quaternion value)
        {
            return IsFinite(value.x) && IsFinite(value.y) &&
                IsFinite(value.z) && IsFinite(value.w) &&
                value.x * value.x + value.y * value.y + value.z * value.z + value.w * value.w > 0.000001f;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
