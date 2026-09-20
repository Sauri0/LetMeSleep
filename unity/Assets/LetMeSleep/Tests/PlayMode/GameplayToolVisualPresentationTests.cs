using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using LetMeSleep.Content.Characters;
using LetMeSleep.Core;
using LetMeSleep.Gameplay;
using LetMeSleep.Gameplay.Unity;
using LetMeSleep.Presentation.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace LetMeSleep.Tests.PlayMode
{
    public sealed class GameplayToolVisualPresentationTests
    {
        private readonly List<GameObject> roots = new List<GameObject>();

        [Test]
        public void ActorBindingShowsOnlyTheExactlyEquippedTool()
        {
            var actorRoot = Root("Actor");
            var proxy = actorRoot.AddComponent<GameplayActorProxy>();
            proxy.Initialize(new SpawnActor(1, "human", PlayerRole.Human, Float3.Zero));
            var view = actorRoot.AddComponent<CharacterView>();
            var binding = actorRoot.AddComponent<ActorVisualBinding>();
            binding.Initialize(proxy, null, view, false);
            var tools = new Dictionary<string, GameObject>();
            foreach (string id in ToolIds())
            {
                tools[id] = Tool(id);
                binding.BindTool(id, tools[id]);
            }

            binding.ApplySnapshot(Actor(GameplayTools.Slipper), 1);
            foreach (var pair in tools) Assert.That(pair.Value.activeSelf, Is.EqualTo(pair.Key == GameplayTools.Slipper), pair.Key);
            binding.ApplySnapshot(Actor(GameplayTools.ElectricRacket), 2);
            foreach (var pair in tools) Assert.That(pair.Value.activeSelf, Is.EqualTo(pair.Key == GameplayTools.ElectricRacket), pair.Key);
            binding.ApplySnapshot(Actor(GameplayTools.Hands), 3);
            foreach (var pair in tools) Assert.That(pair.Value.activeSelf, Is.False, pair.Key);
        }

        [Test]
        public void PresenterMountsEachConfiguredToolByItsOwnGrip()
        {
            var presenter = Root("Presenter").AddComponent<GameplayVisualPresenter>();
            var prefabs = Prefabs();
            presenter.SetPrefabs(null, null, null, prefabs[0], prefabs[1], prefabs[2], prefabs[3]);
            var character = Root("Character");
            var view = character.AddComponent<CharacterView>();
            var socket = new GameObject("ToolSocket_R").transform; socket.SetParent(character.transform);
            socket.SetPositionAndRotation(new Vector3(2, 3, 4), Quaternion.Euler(10, 25, 5));
            view.Anchors = new[] { new CharacterView.AnchorBinding { Name = "ToolSocket_R", Anchor = socket } };

            foreach (string id in ToolIds())
            {
                var instance = Invoke<GameObject>(presenter, "AttachTool", view, id);
                Assert.That(instance, Is.Not.Null, id);
                Assert.That(instance.transform.parent, Is.SameAs(socket));
                Assert.That(Vector3.Distance(instance.GetComponent<ToolView>().Grip.position, socket.position), Is.LessThan(.0001f));
                Assert.That(instance.GetComponentsInChildren<Collider>(true)[0].enabled, Is.False);
                Assert.That(instance.GetComponent<ToolView>().ToolId, Is.EqualTo(id));
            }
        }

        [UnityTest]
        public IEnumerator WorldVisualsFollowAuthoritativePoseAndPurgeMissingPickups()
        {
            var presenter = Root("Presenter").AddComponent<GameplayVisualPresenter>();
            var prefabs = Prefabs();
            presenter.SetPrefabs(null, null, null, prefabs[0], prefabs[1], prefabs[2], prefabs[3]);
            var first = new ToolPickupSnapshot(10, GameplayTools.Aerosol, new Float3(1, 2, 3), Rotation.Identity);
            var projectile = new ToolPickupSnapshot(11, GameplayTools.Slipper, new Float3(4, 5, 6), Rotation.Yaw(.5f),
                0, 2, ToolPickupPhase.Projectile, Float3.Forward, 1, 0, false);
            Invoke<object>(presenter, "ApplyToolPickups", new[] { first, projectile });

            var firstView = presenter.transform.Find("Pickup_10_aerosol").GetComponent<ToolView>();
            var projectileView = presenter.transform.Find("Pickup_11_slipper").GetComponent<ToolView>();
            Assert.That(Vector3.Distance(firstView.Grip.position, first.Position.ToUnity()), Is.LessThan(.0001f));
            Assert.That(Vector3.Distance(projectileView.Grip.position, projectile.Position.ToUnity()), Is.LessThan(.0001f));
            Assert.That(projectileView.gameObject.activeSelf, Is.True, "Ownerless projectile remains visible.");

            var held = new ToolPickupSnapshot(11, GameplayTools.Slipper, projectile.Position, projectile.Rotation, 1, 3);
            Invoke<object>(presenter, "ApplyToolPickups", new[] { held });
            yield return null;
            Assert.That(presenter.transform.Find("Pickup_10_aerosol"), Is.Null, "A pickup omitted from the authoritative ledger must be purged.");
            Assert.That(projectileView.gameObject.activeSelf, Is.False, "Held pickup is represented on the actor, not in the world.");
        }

        [Test]
        public void MissingNewToolPrefabIsDiagnosedWithoutFlyswatterFallback()
        {
            var presenter = Root("Presenter").AddComponent<GameplayVisualPresenter>();
            var flyswatter = Tool(GameplayTools.Flyswatter);
            presenter.SetPrefabs(null, null, null, flyswatter);
            var character = Root("Character");
            var view = character.AddComponent<CharacterView>();
            var socket = new GameObject("ToolSocket_R").transform; socket.SetParent(character.transform);
            view.Anchors = new[] { new CharacterView.AnchorBinding { Name = "ToolSocket_R", Anchor = socket } };

            LogAssert.Expect(LogType.Warning, "LMS_TOOL_PREFAB_MISSING tool=slipper");
            Assert.That(Invoke<GameObject>(presenter, "AttachTool", view, GameplayTools.Slipper), Is.Null);
            Assert.That(socket.childCount, Is.Zero);
        }

        [Test]
        public void UnknownToolIdIsRejectedWithoutCreatingAWorldVisual()
        {
            var presenter = Root("Presenter").AddComponent<GameplayVisualPresenter>();
            var prefabs = Prefabs();
            presenter.SetPrefabs(null, null, null, prefabs[0], prefabs[1], prefabs[2], prefabs[3]);
            LogAssert.Expect(LogType.Error, "LMS_TOOL_ID_UNSUPPORTED tool=not-a-tool");
            Invoke<object>(presenter, "ApplyToolPickups", new[]
            {
                new ToolPickupSnapshot(50, "not-a-tool", Float3.Zero, Rotation.Identity)
            });
            Assert.That(presenter.transform.childCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator ReusedPickupIdReplacesThePreviousToolType()
        {
            var presenter = Root("Presenter").AddComponent<GameplayVisualPresenter>();
            var prefabs = Prefabs();
            presenter.SetPrefabs(null, null, null, prefabs[0], prefabs[1], prefabs[2], prefabs[3]);
            Invoke<object>(presenter, "ApplyToolPickups", new[]
            {
                new ToolPickupSnapshot(60, GameplayTools.Slipper, Float3.Zero, Rotation.Identity)
            });
            Invoke<object>(presenter, "ApplyToolPickups", new[]
            {
                new ToolPickupSnapshot(60, GameplayTools.Aerosol, Float3.Forward, Rotation.Identity)
            });
            yield return null;

            Assert.That(presenter.transform.Find("Pickup_60_slipper"), Is.Null);
            var replacement = presenter.transform.Find("Pickup_60_aerosol");
            Assert.That(replacement, Is.Not.Null);
            Assert.That(replacement.GetComponent<ToolView>().ToolId, Is.EqualTo(GameplayTools.Aerosol));
        }

        [Test]
        public void ConfiguredPrefabMustMatchToolIdAndOwnNonDegenerateAnchors()
        {
            var presenter = Root("Presenter").AddComponent<GameplayVisualPresenter>();
            var flyswatter = Tool(GameplayTools.Flyswatter);
            var mismatched = Tool(GameplayTools.Flyswatter);
            presenter.SetPrefabs(null, null, null, flyswatter, mismatched, null, null);
            LogAssert.Expect(LogType.Error, "LMS_TOOL_PREFAB_INVALID tool=slipper prefab=Prefab_flyswatter");
            Assert.That(Invoke<GameObject>(presenter, "ToolPrefab", GameplayTools.Slipper, true), Is.Null);

            var degenerate = Tool(GameplayTools.Slipper);
            var tool = degenerate.GetComponent<ToolView>(); tool.Impact.position = tool.Grip.position;
            presenter.SetPrefabs(null, null, null, flyswatter, degenerate, null, null);
            LogAssert.Expect(LogType.Error, "LMS_TOOL_PREFAB_INVALID tool=slipper prefab=Prefab_slipper");
            Assert.That(Invoke<GameObject>(presenter, "ToolPrefab", GameplayTools.Slipper, true), Is.Null);
        }

        private GameObject[] Prefabs() => new[]
        {
            Tool(GameplayTools.Flyswatter), Tool(GameplayTools.Slipper),
            Tool(GameplayTools.ElectricRacket), Tool(GameplayTools.Aerosol)
        };

        private GameObject Tool(string id)
        {
            var root = Root("Prefab_" + id);
            var grip = new GameObject("Grip").transform; grip.SetParent(root.transform, false);
            grip.localPosition = new Vector3(.1f, .05f, 0);
            var impact = new GameObject("Impact").transform; impact.SetParent(root.transform, false);
            impact.localPosition = new Vector3(.1f, .05f, .4f);
            var view = root.AddComponent<ToolView>(); view.ToolId = id; view.Grip = grip; view.Impact = impact;
            root.AddComponent<BoxCollider>();
            return root;
        }

        private GameObject Root(string name)
        {
            var value = new GameObject(name); roots.Add(value); return value;
        }

        private static string[] ToolIds() => new[]
        {
            GameplayTools.Flyswatter, GameplayTools.Slipper,
            GameplayTools.ElectricRacket, GameplayTools.Aerosol
        };

        private static ActorSnapshot Actor(string equipped) => new ActorSnapshot(
            1, PlayerRole.Human, LifeState.Active, 1, Float3.Zero, Float3.Zero,
            Rotation.Identity, Float3.Forward, 0, 0, 1, 1, true, 0, 0, null, null, default, 0, equipped);

        private static T Invoke<T>(object target, string name, params object[] args)
        {
            var method = target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, name);
            return (T)method.Invoke(target, args);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            foreach (var item in roots) if (item) Object.Destroy(item);
            roots.Clear();
            yield return null;
        }
    }
}
