using System;
using System.Linq;
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

        [Test]
        public void RouteBudgetIncludesPhysicalOffsetOutsideNearestRegion()
        {
            var method = navigation.GetType().GetMethod("RouteWithin", new[]
                { typeof(Float3), typeof(string), typeof(Float3), typeof(uint) });
            Assert.That(method, Is.Not.Null);
            var outside = new Float3(-4, 0, 0);

            Assert.That((bool)method.Invoke(navigation,
                new object[] { outside, objective.RouteRegionId, objective.ApproachPoint, 70u }), Is.False,
                "The nearest-region clamp must not erase two metres from the admission cost.");
            Assert.That((bool)method.Invoke(navigation,
                new object[] { outside, objective.RouteRegionId, objective.ApproachPoint, 78u }), Is.True);
        }

        [Test]
        public void RouteDiagnosticSeparatesAuthoredAndCurrentlyOpenDistance()
        {
            Object.DestroyImmediate(topology);
            topology = new TextAsset("{\"schema_version\":1,\"map_id\":\"test-map\",\"zones\":[" +
                "{\"id\":\"start\",\"min\":[-2,0,-2],\"max\":[2,3,2]}," +
                "{\"id\":\"finish\",\"min\":[2.1,0,-2],\"max\":[6,3,2]}],\"portals\":[" +
                "{\"id\":\"closed-door\",\"from\":\"start\",\"to\":\"finish\",\"center\":[2.05,1,0]," +
                "\"normal\":[1,0,0],\"width\":1,\"height\":2,\"door\":true}]}");
            navigation = ConfigureMap();
            var method = navigation.GetType().GetMethod("DiagnoseRoute",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);

            string diagnostic = (string)method.Invoke(navigation, new object[]
                { Float3.Zero, objective.RouteRegionId, objective.ApproachPoint, 100u });

            StringAssert.Contains("authoredDistance=4", diagnostic);
            StringAssert.Contains("authoredWithin=1", diagnostic);
            StringAssert.Contains("openDistance=Infinity", diagnostic);
            StringAssert.Contains("openWithin=0", diagnostic);
        }

        [Test]
        public void HumanGraphDoesNotCrossTheAuthoredMosquitoGraph()
        {
            navigation = ConfigureJson(SeparateGraphJson);
            var humanTarget = Objective("human_finish", new Float3(4, 0, 0));
            var airTarget = Objective("air_finish", new Float3(4, 0, 0));

            Assert.That(Direction(navigation, 1, 0, Float3.Zero, airTarget).Length, Is.Zero,
                "A human must not borrow an air-only passage.");
            Assert.That(Direction(navigation, 1, 0, Float3.Zero, humanTarget).X, Is.GreaterThan(0));
            Assert.That(KnowsRegion(navigation, "air_finish"), Is.False);
            Assert.That(KnowsRegion(navigation, "human_finish"), Is.True);
        }

        [Test]
        public void HumanGraphDoesNotChangeMosquitoExploration()
        {
            var legacy = ConfigureJson(LegacyAirGraphJson);
            var separated = ConfigureJson(SeparateGraphJson);

            var expected = Explore(legacy, 70, 12, Float3.Zero);
            var actual = Explore(separated, 70, 12, Float3.Zero);

            Assert.That(actual.X, Is.EqualTo(expected.X).Within(.000001f));
            Assert.That(actual.Y, Is.EqualTo(expected.Y).Within(.000001f));
            Assert.That(actual.Z, Is.EqualTo(expected.Z).Within(.000001f));
            Assert.That(actual.Length, Is.GreaterThan(0));
            Assert.That(Context(legacy, 70).ReadProgress().Value.PassageId, Is.EqualTo("air-passage"));
            Assert.That(Context(separated, 70).ReadProgress().Value.PassageId, Is.EqualTo("air-passage"),
                "The separated mosquito patrol must remain visible to the existing bot context.");
        }

        [Test]
        public void HumanRoutePreservesMeasuredPolylineAndTraversalCorridor()
        {
            navigation = ConfigureJson(SeparateGraphJson);
            var passages = (BotPassage[])navigation.GetType().GetField("passages",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(navigation);
            var route = passages.Single(p => p.Id == "human-route");

            Assert.That(route.From, Is.EqualTo("human_start"));
            Assert.That(route.To, Is.EqualTo("human_finish"));
            Assert.That(route.Points.Select(p => p.X), Is.EqualTo(new[] { 1.5f, 2.5f, 3.5f }));
            Assert.That(route.Points.All(p => p.Y == 1 && p.Z == 0), Is.True);
            Assert.That(route.TraversalRegions.Count, Is.EqualTo(1));
            Assert.That(route.TraversalRegions[0].Id, Is.EqualTo("human-route:corridor"));
            Assert.That(route.TraversalRegions[0].Min.X, Is.EqualTo(1.2f));
            Assert.That(route.TraversalRegions[0].Max.X, Is.EqualTo(3.8f));

            var mosquito = (BotPassage[])navigation.GetType().GetField("mosquitoPassages",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(navigation);
            Assert.That(mosquito.Select(p => p.Id), Is.EqualTo(new[] { "air-passage" }));
        }

        [Test]
        public void PartialOrInvalidHumanGraphsAreRejected()
        {
            string[] invalid =
            {
                LegacyAirGraphJson.TrimEnd('}') + ",\"human_zones\":[]}",
                SeparateGraphJson.Replace("\"id\":\"human_finish\"", "\"id\":\"human_start\""),
                SeparateGraphJson.Replace("\"from\":\"human_start\",\"to\":\"human_finish\"",
                    "\"from\":\"missing\",\"to\":\"human_finish\""),
                SeparateGraphJson.Replace("\"position\":[2.5,1,0]", "\"position\":[2.5,1]"),
                SeparateGraphJson.Replace("\"min\":[1.2,0.5,-0.5],\"max\":[3.8,1.5,0.5]",
                    "\"min\":[3.8,0.5,-0.5],\"max\":[1.2,1.5,0.5]")
            };

            foreach (var json in invalid)
            {
                var error = Assert.Throws<TargetInvocationException>(() => ConfigureJson(json));
                Assert.That(error.InnerException, Is.TypeOf<ArgumentException>(), json);
            }
        }

        [Test]
        public void CasaStairReplayKeepsTheAuthoredPassageAndPhysicalSampleAtTick44()
        {
            ConfigureStairMap(); EnterStair();
            Direction(navigation, 1, 41, new Float3(1.22179532f, 2.8707273f, 2.25073266f));
            string key = Context(navigation, 1).ReadProgress().Value.WaypointKey;
            var direction = Direction(navigation, 1, 44, StairFoot);
            Assert.That(Context(navigation, 1).ReadProgress().Value.PassageId, Is.EqualTo("straight_stair"));
            Assert.That(Context(navigation, 1).ReadProgress().Value.WaypointKey, Is.EqualTo(key));
            Assert.That(direction.X, Is.EqualTo(.00281057f).Within(.00001f));
            Assert.That(direction.Y, Is.EqualTo(-1.13479829f).Within(.00001f));
            Assert.That(direction.Z, Is.EqualTo(-1.310733f).Within(.00001f));
        }

        [Test]
        public void CasaStairContextSuspendsMovementUntilThePassageBlockExpires()
        {
            ConfigureStairMap(); EnterStair(); Direction(navigation, 1, 44, StairFoot);
            Context(navigation, 1).InvalidatePassage("straight_stair", 224);
            Assert.That(Direction(navigation, 1, 44, StairFoot).Length, Is.Zero);
            Assert.That(Direction(navigation, 1, 223, StairFoot).Length, Is.Zero);
            Assert.That(Direction(navigation, 1, 224, StairFoot).Z, Is.LessThan(-1));
        }

        [Test]
        public void CasaStairCannotBeAcquiredByClampingAnUnenteredShaftToAnotherRoom()
        {
            ConfigureStairMap();
            Assert.That(Direction(navigation, 1, 44, StairFoot).Length, Is.Zero);
            Assert.That(Context(navigation, 1).ReadProgress().HasValue, Is.False);
        }

        private static readonly Float3 StairFoot = new Float3(1.22218943f, 2.73779845f, 1.940733f);
        private const string LegacyAirGraphJson = @"{
            ""schema_version"":1,""map_id"":""test-map"",""zones"":[
            {""id"":""air_start"",""min"":[-2,0,-2],""max"":[2,3,2]},
            {""id"":""air_finish"",""min"":[2.1,0,-2],""max"":[6,3,2]}],""portals"":[
            {""id"":""air-passage"",""from"":""air_start"",""to"":""air_finish"",""center"":[2.05,1,0],
             ""normal"":[1,0,0],""width"":1,""height"":2,""door"":false}]}";
        private const string SeparateGraphJson = @"{
            ""schema_version"":1,""map_id"":""test-map"",""zones"":[
            {""id"":""air_start"",""min"":[-2,0,-2],""max"":[2,3,2]},
            {""id"":""air_finish"",""min"":[2.1,0,-2],""max"":[6,3,2]}],""portals"":[
            {""id"":""air-passage"",""from"":""air_start"",""to"":""air_finish"",""center"":[2.05,1,0],
             ""normal"":[1,0,0],""width"":1,""height"":2,""door"":false}],
            ""human_zones"":[
            {""id"":""human_start"",""min"":[-2,0,-2],""max"":[2,3,2]},
            {""id"":""human_finish"",""min"":[2.1,0,-2],""max"":[6,3,2]}],
            ""human_portals"":[],""human_routes"":[
            {""id"":""human-route"",""from"":""human_start"",""to"":""human_finish"",""points"":[
             {""position"":[1.5,1,0]},{""position"":[2.5,1,0]},{""position"":[3.5,1,0]}],
             ""traversal_regions"":[{""id"":""human-route:corridor"",""min"":[1.2,0.5,-0.5],""max"":[3.8,1.5,0.5]}]}]}";
        private void EnterStair()
        {
            Direction(navigation, 1, 2, new Float3(-.65f, 3.174f, 3.95f));
            Direction(navigation, 1, 20, new Float3(1.20407653f, 3.150159f, 3.80167365f));
            Direction(navigation, 1, 23, new Float3(1.21852863f, 3.150159f, 4.11133671f));
        }
        private void ConfigureStairMap()
        {
            Object.DestroyImmediate(topology);
            // Reduced authored Casa topology, preserving its real flight and region bounds.
            topology = new TextAsset(@"{
                ""schema_version"":1,""map_id"":""test-map"",""zones"":[
                {""id"":""lower"",""min"":[-1.25,0.4,-4.76],""max"":[2.05,2.65,-1.65]},
                {""id"":""upper"",""min"":[-1.25,3.3,3.15],""max"":[2.05,5.15,4.76]},
                {""id"":""neighbour"",""min"":[-1.25,3.3,-1.65],""max"":[0.3,5.15,3.15]},
                {""id"":""finish"",""min"":[-6.76,0.4,-4.76],""max"":[-1.55,2.65,4.76]}],
                ""portals"":[
                {""id"":""upper_neighbour"",""from"":""neighbour"",""to"":""upper"",""center"":[-0.55,4.4,3.15],""normal"":[0,0,1],""width"":1,""height"":2,""door"":false},
                {""id"":""bottom_exit"",""from"":""lower"",""to"":""finish"",""center"":[-1.5,1.3,-2],""normal"":[-1,0,0],""width"":1,""height"":2,""door"":false}],
                ""stair"": {""id"":""straight_stair"",""from"":""lower"",""to"":""upper"",
                ""lower_flight"": {""clear_x"":[0.65,1.8],""start_y"":0.55,""end_y"":1.753,""start_z"":-1.55,""end_z"":0.63},
                ""upper_flight"": {""clear_x"":[0.65,1.8],""start_y"":1.753,""end_y"":3.35,""start_z"":0.63,""end_z"":4.25},
                ""mid_landing"": {""id"":""landing"",""min"":[0.65,1.7,0.62],""max"":[1.8,1.8,0.64]}}}");
            objective = new ObjectiveDefinition("objective", ObjectiveKind.Clean, "task.test",
                "task.action.hold_clean", new Float3(-4, .3f, 1), new Float3(-4, .3f, 1), 1, 30, "finish");
            navigation = ConfigureMap();
        }

        private object ConfigureMap()
        {
            var method = typeof(UnityGameplayWorld).GetMethod("ConfigureModeMap", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return method.Invoke(world, new object[] { topology, "test-map", false });
        }
        private object ConfigureJson(string json)
        {
            if (topology) Object.DestroyImmediate(topology);
            topology = new TextAsset(json);
            return ConfigureMap();
        }
        private static BotNavigationContext Context(object adapter, uint actor)
            => (BotNavigationContext)adapter.GetType().GetMethod("ContextFor").Invoke(adapter, new object[] { actor });
        private Float3 Direction(object adapter, uint actor, uint tick, Float3 position = default)
            => Direction(adapter, actor, tick, position, objective);
        private static Float3 Direction(object adapter, uint actor, uint tick, Float3 position,
            ObjectiveDefinition target)
        {
            var snapshot = new ActorSnapshot(actor, PlayerRole.Human, LifeState.Active, 1, position,
                Float3.Zero, Rotation.Identity, Float3.Forward, 0, 0, 1, 1, true, 0, 0, null, null, default, 0);
            var method = adapter.GetType().GetMethod("DirectionTo", new[] { typeof(ActorSnapshot), typeof(ObjectiveDefinition), typeof(uint) });
            Assert.That(method, Is.Not.Null);
            return (Float3)method.Invoke(adapter, new object[] { snapshot, target, tick });
        }
        private static Float3 Explore(object adapter, uint actor, uint tick, Float3 position)
        {
            var snapshot = new ActorSnapshot(actor, PlayerRole.Mosquito, LifeState.Active, 1, position,
                Float3.Zero, Rotation.Identity, Float3.Forward, 0, 0, 1, 1, true, 0, 0, null, null, default, 0);
            return (Float3)adapter.GetType().GetMethod("Explore").Invoke(adapter, new object[] { snapshot, tick });
        }
        private static bool KnowsRegion(object adapter, string id)
            => (bool)adapter.GetType().GetMethod("KnowsRegion").Invoke(adapter, new object[] { id });
        private static ObjectiveDefinition Objective(string region, Float3 point)
            => new ObjectiveDefinition("objective-" + region, ObjectiveKind.Clean, "task.test",
                "task.action.hold_clean", point, point, 1, 30, region);
    }
}
