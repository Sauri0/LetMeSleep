using System;
using System.Reflection;
using LetMeSleep.Core;
using LetMeSleep.Gameplay;
using LetMeSleep.Gameplay.Unity;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LetMeSleep.Tests.PlayMode
{
    public sealed class GameplayBotNavigationContextPlayModeTests
    {
        private GameObject owner;
        private UnityGameplayWorld world;
        private TextAsset topology;
        private object navigation;
        private ObjectiveDefinition objective;

        [SetUp]
        public void SetUp()
        {
            owner = new GameObject("Bot navigation context fixture");
            world = owner.AddComponent<UnityGameplayWorld>();
            var map = new GameObject("Map"); map.transform.SetParent(owner.transform, false);
            world.MapRoot = map.transform;
            topology = new TextAsset("{\"schema_version\":1,\"map_id\":\"test-map\",\"zones\":[" +
                "{\"id\":\"start\",\"min\":[-2,0,-2],\"max\":[2,3,2]}," +
                "{\"id\":\"finish\",\"min\":[2.1,0,-2],\"max\":[6,3,2]}],\"portals\":[" +
                "{\"id\":\"passage\",\"from\":\"start\",\"to\":\"finish\",\"center\":[2.05,1,0]," +
                "\"normal\":[1,0,0],\"width\":1,\"height\":2,\"door\":false}]}");
            objective = new ObjectiveDefinition("objective", ObjectiveKind.Clean, "task.test",
                "task.action.hold_clean", new Float3(4, 0, 0), new Float3(4, 0, 0), 1, 30, "finish");
            navigation = ConfigureMap();
        }

        [TearDown]
        public void TearDown()
        {
            if (owner) Object.DestroyImmediate(owner);
            if (topology) Object.DestroyImmediate(topology);
            owner = null; world = null; topology = null; navigation = null;
        }

        [Test]
        public void ContextReadsTheRouteCreatedAfterTheContextItself()
        {
            var context = Context(navigation, 1);
            Assert.That(context.ReadProgress().HasValue, Is.False);
            Assert.That(Direction(navigation, 1, 0).X, Is.GreaterThan(0));
            Assert.That(context.ReadProgress().Value.PassageId, Is.EqualTo("passage"));
            Assert.That(context.ReadProgress().Value.RemainingDistance, Is.GreaterThan(0));
        }

        [Test]
        public void InvalidatingOneActorDoesNotChangeAnotherActorsRoute()
        {
            Direction(navigation, 1, 0); Direction(navigation, 2, 0);
            var first = Context(navigation, 1); var second = Context(navigation, 2);
            first.InvalidatePassage("passage", 330);
            Assert.That(Direction(navigation, 1, 150).Length, Is.Zero);
            Assert.That(Direction(navigation, 2, 150).X, Is.GreaterThan(0));
            Assert.That(first.ReadProgress().HasValue, Is.False);
            Assert.That(second.ReadProgress().Value.PassageId, Is.EqualTo("passage"));
        }

        [Test]
        public void AdapterPropagatesTheTickForExactPassageReopening()
        {
            Direction(navigation, 1, 0);
            Context(navigation, 1).InvalidatePassage("passage", 330);
            Assert.That(Direction(navigation, 1, 329).Length, Is.Zero);
            Assert.That(Direction(navigation, 1, 330).X, Is.GreaterThan(0));
        }

        [Test]
        public void ConfiguringANewMapInstanceDoesNotRetainPreviousRoundBlocks()
        {
            Direction(navigation, 1, 0);
            Context(navigation, 1).InvalidatePassage("passage", 330);
            Assert.That(Direction(navigation, 1, 150).Length, Is.Zero);
            var nextRoundNavigation = ConfigureMap();
            Assert.That(nextRoundNavigation, Is.Not.SameAs(navigation));
            Assert.That(Direction(nextRoundNavigation, 1, 150).X, Is.GreaterThan(0));
            Assert.That(Direction(navigation, 1, 150).Length, Is.Zero,
                "The new context must not borrow or mutate the old actor's patrol.");
        }

        private object ConfigureMap()
        {
            var method = typeof(UnityGameplayWorld).GetMethod("ConfigureModeMap", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return method.Invoke(world, new object[] { topology, "test-map", false });
        }
        private static BotNavigationContext Context(object adapter, uint actor)
            => (BotNavigationContext)adapter.GetType().GetMethod("ContextFor").Invoke(adapter, new object[] { actor });
        private Float3 Direction(object adapter, uint actor, uint tick)
        {
            var snapshot = new ActorSnapshot(actor, PlayerRole.Human, LifeState.Active, 1, Float3.Zero,
                Float3.Zero, Rotation.Identity, Float3.Forward, 0, 0, 1, 1, true, 0, 0, null, null, default, 0);
            var method = adapter.GetType().GetMethod("DirectionTo", new[] { typeof(ActorSnapshot), typeof(ObjectiveDefinition), typeof(uint) });
            Assert.That(method, Is.Not.Null);
            return (Float3)method.Invoke(adapter, new object[] { snapshot, objective, tick });
        }
    }
}
