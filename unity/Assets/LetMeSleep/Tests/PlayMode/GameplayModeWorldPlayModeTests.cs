using System;
using System.Linq;
using System.Reflection;
using LetMeSleep.Content.Environment;
using LetMeSleep.Core;
using LetMeSleep.Gameplay;
using LetMeSleep.Gameplay.Unity;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LetMeSleep.Tests.PlayMode
{
    public sealed class GameplayModeWorldPlayModeTests
    {
        private GameObject owner;
        private Mesh generatedMesh;

        [TearDown]
        public void TearDown()
        {
            if (owner) Object.DestroyImmediate(owner);
            if (generatedMesh) Object.DestroyImmediate(generatedMesh);
            owner = null;
            generatedMesh = null;
        }

        [Test]
        public void EliminatedActorLeavesAllPhysicsQueriesAndRespawnRestoresThem()
        {
            owner = new GameObject("Mode actor fixture");
            var proxy = owner.AddComponent<GameplayActorProxy>();
            proxy.Initialize(new SpawnActor(2, "mosquito", PlayerRole.Mosquito, Float3.Zero));

            proxy.Apply(State(LifeState.Eliminated));
            Assert.That(proxy.MotorCollider.enabled, Is.False);
            Assert.That(proxy.GetComponentsInChildren<Collider>(true).All(c => !c.enabled), Is.True);

            proxy.Apply(State(LifeState.Recovering));
            Assert.That(proxy.MotorCollider.enabled, Is.True);
        }

        [Test]
        public void TasksAcceptAuthoredLowerFloorRouteAndExactTargetLineOfSight()
        {
            var fixture = CreateTaskMap(new Vector3(0, .002f, 0));
            var definitions = fixture.World.GetObjectiveDefinitions();
            var config = Config(definitions);

            Assert.DoesNotThrow(() => fixture.Runtime.BeginRound(config, Roster(new Float3(0, .002f, 0))));
            var objective = definitions.Single();
            var eye = new Float3(0, 1.532f, 0);
            var aim = (objective.Position - eye).Normalized;
            Assert.That(((IGameplayModeWorld)fixture.World).CanWorkObjective(1, objective,
                new Float3(0, .002f, 0), aim), Is.True);
        }

        [Test]
        public void TasksAcceptRayWitnessOnNonConvexMeshTarget()
        {
            var fixture = CreateTaskMap(new Vector3(0, .002f, 0), true);
            var definitions = fixture.World.GetObjectiveDefinitions();

            Assert.DoesNotThrow(() => fixture.Runtime.BeginRound(Config(definitions),
                Roster(new Float3(0, .002f, 0))));
            var objective = definitions.Single();
            var eye = new Float3(0, 1.532f, 0);
            Assert.That(((IGameplayModeWorld)fixture.World).CanWorkObjective(1, objective,
                new Float3(0, .002f, 0), (objective.Position - eye).Normalized), Is.True);
        }

        [Test]
        public void StackedFloorWithoutPassageIsNotFlattenedIntoAValidRoute()
        {
            var fixture = CreateTaskMap(new Vector3(0, 3.002f, 0));
            var config = Config(fixture.World.GetObjectiveDefinitions());

            Assert.Throws<ArgumentException>(() => fixture.Runtime.BeginRound(config,
                Roster(new Float3(0, 3.002f, 0))), "Upper and lower regions share XZ but have no authored passage.");
        }

        [Test]
        public void DistributedCatalogAcceptsLocalChoicesAndSelectionRejectsOutOfBudgetDestinations()
        {
            var fixture = CreateDistributedTaskMap();
            var definitions = fixture.World.GetObjectiveDefinitions();
            var config = Config(definitions);
            var roster = new[]
            {
                new SpawnActor(1, "west-human", PlayerRole.Human, new Float3(0, .002f, 0)),
                new SpawnActor(2, "east-human", PlayerRole.Human, new Float3(40, .002f, 0)),
                new SpawnActor(3, "mosquito", PlayerRole.Mosquito, new Float3(20, 1, 0))
            };

            Assert.DoesNotThrow(() => fixture.Runtime.BeginRound(config, roster),
                "Each spawn and objective has at least two local destinations inside 330 ticks.");
            var west = definitions.Single(objective => objective.ObjectiveId == "west.1");
            var east = definitions.Single(objective => objective.ObjectiveId == "east.1");
            var selection = (IGameplayTaskSelectionWorld)fixture.World;
            Assert.That(selection.CanAssignObjective(1, west), Is.True);
            Assert.That(selection.CanAssignObjective(1, east), Is.False,
                "A connected authored route beyond its budget must not be assigned.");
            Assert.That(selection.CanAssignObjective(2, east), Is.True);
            Assert.That(selection.CanAssignObjective(2, west), Is.False);
        }

        [Test]
        public void AssignmentBudgetDoesNotTurnAnActiveConnectedTaskUnavailableAfterMovingAway()
        {
            var fixture = CreateDistributedTaskMap();
            var definitions = fixture.World.GetObjectiveDefinitions();
            var config = Config(definitions);
            fixture.Runtime.BeginRound(config, new[]
            {
                new SpawnActor(1, "west-human", PlayerRole.Human, new Float3(0, .002f, 0)),
                new SpawnActor(2, "east-human", PlayerRole.Human, new Float3(40, .002f, 0)),
                new SpawnActor(3, "mosquito", PlayerRole.Mosquito, new Float3(20, 1, 0))
            });
            var target = definitions.Single(objective => objective.ObjectiveId == "east.1");
            var selection = (IGameplayTaskSelectionWorld)fixture.World;
            Assert.That(selection.CanAssignObjective(2, target), Is.True);

            fixture.World.Actors[2].Apply(new ActorSnapshot(2, PlayerRole.Human, LifeState.Active, 2,
                new Float3(0, .002f, 0), Float3.Zero, Rotation.Yaw(0), Float3.Forward, 0, 0,
                1, 2, true, 0, 0, null, null, default, 0));
            Physics.SyncTransforms();

            Assert.That(selection.CanAssignObjective(2, target), Is.False,
                "A new assignment remains constrained by its 330-tick route budget.");
            Assert.That(((IGameplayModeWorld)fixture.World).IsObjectiveAvailable(2, target), Is.True,
                "An already assigned task remains available while the authored open route still exists.");
        }

        [Test]
        public void ToolDetourUsesOpenAuthoredRouteAndRejectsPositionOutsideItsRegions()
        {
            var fixture = CreateDistributedTaskMap();
            var definitions = fixture.World.GetObjectiveDefinitions();
            fixture.Runtime.BeginRound(Config(definitions), new[]
            {
                new SpawnActor(1, "west-human", PlayerRole.Human, new Float3(0, .002f, 0)),
                new SpawnActor(2, "east-human", PlayerRole.Human, new Float3(40, .002f, 0)),
                new SpawnActor(3, "mosquito", PlayerRole.Mosquito, new Float3(20, 1, 0))
            });
            ActorSnapshot actor = fixture.Runtime.LatestSnapshot.Actors.Single(item => item.ActorId == 1);
            ObjectiveDefinition objective = definitions.Single(item => item.ObjectiveId == "east.1");
            const BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;
            object navigation = typeof(UnityGameplayWorld).GetField("modeNavigation", hidden)
                ?.GetValue(fixture.World);
            MethodInfo method = navigation?.GetType().GetMethod("TryToolDetour", hidden);
            Assert.That(method, Is.Not.Null);

            var routed = new object[] { actor, new Float3(40, .002f, 0), objective, 0f };
            Assert.That((bool)method.Invoke(navigation, routed), Is.True);
            Assert.That((float)routed[3], Is.GreaterThan(0));
            Assert.That((float)routed[3], Is.LessThan(3),
                "A pickup beside the objective should add only its authored-route deviation.");

            var outbound = new object[] { actor, new Float3(40, .002f, 0), null, 0f };
            Assert.That((bool)method.Invoke(navigation, outbound), Is.True);
            Assert.That((float)outbound[3], Is.GreaterThan(35),
                "Without an objective the opportunity cost is the authored outbound route.");

            var outside = new object[] { actor, new Float3(20, .002f, 0), objective, 0f };
            Assert.That((bool)method.Invoke(navigation, outside), Is.False,
                "Euclidean proximity cannot invent a route for a pickup outside authored regions.");
        }

        [Test]
        public void MosquitoRespawnSkipsOccupiedAuthoredSpawn()
        {
            owner = new GameObject("Mode respawn fixture");
            var world = owner.AddComponent<UnityGameplayWorld>();
            var map = new GameObject("Map"); map.transform.SetParent(owner.transform, false); world.MapRoot = map.transform;
            Floor(map.transform, Vector3.zero, new Vector3(1, .1f, 1));
            Floor(map.transform, new Vector3(2, 0, 0), new Vector3(1, .1f, 1));
            var volume = map.AddComponent<GameplayRecoveryVolume>();
            volume.SafetyBounds = new Bounds(new Vector3(1, 1, 0), new Vector3(8, 4, 4));
            volume.MosquitoSpawnPoints = new[] { new Vector3(0, 1, 0), new Vector3(2, 1, 0) };
            var roster = new[]
            {
                new SpawnActor(1, "human", PlayerRole.Human, new Float3(-3, .002f, 0)),
                new SpawnActor(2, "mosquito", PlayerRole.Mosquito, new Float3(4, 1, 0)),
                new SpawnActor(3, "occupied", PlayerRole.Mosquito, new Float3(0, .057f, 0))
            };
            world.BeginRound(roster, Array.Empty<DoorDefinition>());
            world.BeginBoundsRecovery(roster);
            Physics.SyncTransforms();

            Assert.That(((IGameplayModeWorld)world).TryMosquitoRespawn(2, out var point), Is.True);
            Assert.That(point.X, Is.EqualTo(2).Within(.003f));
            Assert.That(point.Y, Is.EqualTo(.059f).Within(.004f));
        }

        private TaskFixture CreateTaskMap(Vector3 humanSpawn, bool nonConvexTarget = false)
        {
            owner = new GameObject("Mode world fixture");
            var runtime = owner.AddComponent<GameplayRuntime>();
            runtime.IsHost = true; runtime.AutomaticTick = false; runtime.CaptureLocalInput = false;
            var map = new GameObject("Map"); map.transform.SetParent(owner.transform, false);
            var definition = map.AddComponent<EnvironmentMapDefinition>();
            definition.MapId = "mode-fixture"; definition.ContentHash = "fixture-content";
            var spawn = new GameObject("HumanSpawn").transform; spawn.SetParent(map.transform, false); spawn.localPosition = humanSpawn;
            definition.HumanSpawnPoints = new[] { spawn };
            var target = new GameObject("TaskFloor"); target.transform.SetParent(map.transform, false);
            if (nonConvexTarget)
            {
                generatedMesh = new Mesh { name = "NonConvexObjectiveFixture" };
                generatedMesh.vertices = new[] { new Vector3(-1.5f, 0, -1.5f), new Vector3(-1.5f, 0, 1.5f),
                    new Vector3(1.5f, 0, 1.5f), new Vector3(1.5f, 0, -1.5f) };
                generatedMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
                generatedMesh.RecalculateNormals(); generatedMesh.RecalculateBounds();
                var meshCollider = target.AddComponent<MeshCollider>(); meshCollider.sharedMesh = generatedMesh; meshCollider.convex = false;
            }
            else
            {
                var floor = target.AddComponent<BoxCollider>(); floor.center = new Vector3(0, -.05f, 0); floor.size = new Vector3(3, .1f, 3);
            }
            var catalog = map.AddComponent<GameplayObjectiveCatalog>();
            catalog.ConfigureForEditor("mode-fixture", new[]
            {
                new GameplayObjectiveCatalog.Entry
                {
                    ObjectiveId = "fixture.clean.01", Kind = GameplayObjectiveKind.Clean,
                    DisplayKey = "task.fixture.floor", ActionKey = "task.action.hold_clean",
                    LocalPosition = Vector3.zero, LocalApproachPoint = Vector3.up * .002f,
                    UseRadius = 1.25f, WorkTicks = 30, RouteRegionId = "lower",
                    RouteBudgetTicks = 330, TargetPath = "TaskFloor"
                }
            });
            runtime.World.MapRoot = map.transform;
            runtime.NavigationData = new TextAsset("{\"schema_version\":1,\"map_id\":\"mode-fixture\",\"zones\":[" +
                "{\"id\":\"lower\",\"min\":[-2,0,-2],\"max\":[2,2,2]}," +
                "{\"id\":\"upper\",\"min\":[-2,3,-2],\"max\":[2,5,2]}],\"portals\":[]}");
            Physics.SyncTransforms();
            return new TaskFixture(runtime);
        }

        private TaskFixture CreateDistributedTaskMap()
        {
            owner = new GameObject("Distributed task fixture");
            var runtime = owner.AddComponent<GameplayRuntime>();
            runtime.IsHost = true; runtime.AutomaticTick = false; runtime.CaptureLocalInput = false;
            var map = new GameObject("Map"); map.transform.SetParent(owner.transform, false);
            var definition = map.AddComponent<EnvironmentMapDefinition>();
            definition.MapId = "mode-fixture"; definition.ContentHash = "fixture-content";
            var westSpawn = new GameObject("WestSpawn").transform;
            westSpawn.SetParent(map.transform, false); westSpawn.localPosition = new Vector3(0, .002f, 0);
            var eastSpawn = new GameObject("EastSpawn").transform;
            eastSpawn.SetParent(map.transform, false); eastSpawn.localPosition = new Vector3(40, .002f, 0);
            definition.HumanSpawnPoints = new[] { westSpawn, eastSpawn };
            Floor(map.transform, new Vector3(20, 0, 0), new Vector3(45, .1f, 4));
            var entries = new System.Collections.Generic.List<GameplayObjectiveCatalog.Entry>();
            for (int side = 0; side < 2; side++)
            for (int index = 0; index < 3; index++)
            {
                float x = side == 0 ? index - 1 : 39 + index;
                string id = (side == 0 ? "west." : "east.") + (index + 1);
                var target = new GameObject(id); target.transform.SetParent(map.transform, false);
                var plate = target.AddComponent<BoxCollider>();
                plate.center = new Vector3(x, -.025f, 0); plate.size = new Vector3(.55f, .05f, .55f);
                entries.Add(new GameplayObjectiveCatalog.Entry
                {
                    ObjectiveId = id, Kind = GameplayObjectiveKind.Clean,
                    DisplayKey = "task." + id, ActionKey = "task.action.hold_clean",
                    LocalPosition = new Vector3(x, .002f, 0), LocalApproachPoint = new Vector3(x, .002f, 0),
                    UseRadius = 1.25f, WorkTicks = 30, RouteRegionId = side == 0 ? "west" : "east",
                    RouteBudgetTicks = 330, TargetPath = id
                });
            }
            var catalog = map.AddComponent<GameplayObjectiveCatalog>();
            catalog.ConfigureForEditor("mode-fixture", entries.ToArray());
            runtime.World.MapRoot = map.transform;
            runtime.NavigationData = new TextAsset("{\"schema_version\":1,\"map_id\":\"mode-fixture\",\"zones\":[" +
                "{\"id\":\"west\",\"min\":[-2,0,-2],\"max\":[3,2,2]}," +
                "{\"id\":\"east\",\"min\":[38,0,-2],\"max\":[43,2,2]}],\"portals\":[" +
                "{\"id\":\"long_link\",\"from\":\"west\",\"to\":\"east\",\"center\":[20,1,0]," +
                "\"normal\":[1,0,0],\"width\":1,\"height\":2,\"door\":false}]}");
            Physics.SyncTransforms();
            return new TaskFixture(runtime);
        }

        private static GameplayRoundConfig Config(System.Collections.Generic.IReadOnlyList<ObjectiveDefinition> objectives) =>
            new GameplayRoundConfig(1, 1, "mode-fixture", "fixture-content", 120, 0,
                modeId: GameModes.Tasks, modeRules: new ModeRuleProfile(GameModes.Tasks), objectives: objectives);
        private static SpawnActor[] Roster(Float3 human) => new[]
        {
            new SpawnActor(1, "human", PlayerRole.Human, human),
            new SpawnActor(2, "mosquito", PlayerRole.Mosquito, new Float3(4, 1, 0))
        };
        private static ActorSnapshot State(LifeState life) => new ActorSnapshot(2, PlayerRole.Mosquito, life, 1,
            Float3.Zero, Float3.Zero, Rotation.Yaw(0), Float3.Forward, 0, 0, 1, 1, false, 0, 0,
            null, null, default, 0, livesRemaining: life == LifeState.Eliminated ? 0 : 2);
        private static void Floor(Transform parent, Vector3 center, Vector3 size)
        {
            var go = new GameObject("Floor"); go.transform.SetParent(parent, false);
            var collider = go.AddComponent<BoxCollider>(); collider.center = center + Vector3.down * .05f; collider.size = size;
        }

        private sealed class TaskFixture
        {
            internal readonly GameplayRuntime Runtime;
            internal UnityGameplayWorld World => Runtime.World;
            internal TaskFixture(GameplayRuntime runtime) { Runtime = runtime; }
        }
    }
}
