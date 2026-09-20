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
    public sealed class GameplayEquipmentWorldPlayModeTests
    {
        private GameObject root;
        private UnityGameplayWorld world;
        private GameplayToolPickup pickup;

        [TearDown] public void Cleanup() { if (root) Object.DestroyImmediate(root); }

        private void Fixture(string toolId = GameplayTools.Slipper, bool withMosquito = false, bool withBlockingPickup = false)
        {
            root = new GameObject("Equipment fixture");
            world = root.AddComponent<UnityGameplayWorld>();
            var map = new GameObject("Map"); map.transform.SetParent(root.transform);
            world.MapRoot = map.transform;
            Cube("Floor", new Vector3(0, -.1f, 0), new Vector3(10, .2f, 10));
            var item = new GameObject("Pickup"); item.transform.SetParent(map.transform);
            item.transform.position = new Vector3(3, .1f, 3);
            var collider = item.AddComponent<BoxCollider>();
            collider.center = new Vector3(0, .05f, .15f); collider.size = new Vector3(.2f, .1f, .3f);
            pickup = item.AddComponent<GameplayToolPickup>();
            pickup.PickupId = 1; pickup.ToolId = toolId; pickup.InteractionCollider = collider;
            if (withBlockingPickup)
            {
                var blocker = new GameObject("Blocking pickup"); blocker.transform.SetParent(map.transform);
                blocker.transform.position = new Vector3(1.5f, .8f, 1.5f);
                var blockerCollider = blocker.AddComponent<BoxCollider>(); blockerCollider.size = new Vector3(.3f, .5f, .3f);
                var blockerPickup = blocker.AddComponent<GameplayToolPickup>();
                blockerPickup.PickupId = 2; blockerPickup.ToolId = GameplayTools.Flyswatter; blockerPickup.InteractionCollider = blockerCollider;
            }
            var human = new SpawnActor(1, "human", PlayerRole.Human, Float3.Zero);
            var roster = withMosquito ? new[] { human, new SpawnActor(2, "mosquito", PlayerRole.Mosquito, new Float3(0, 1.53f, 1)) } : new[] { human };
            world.BeginRound(roster, Array.Empty<DoorDefinition>());
            world.BeginTools(world.GetToolDefinitions());
            world.ApplyToolState(new ToolPickupSnapshot(1, toolId, new Float3(3, .1f, 3), Rotation.Identity, 1, 2));
            Physics.SyncTransforms();
        }

        private BoxCollider Cube(string name, Vector3 position, Vector3 size)
        {
            var obj = new GameObject(name); obj.transform.SetParent(world.MapRoot);
            obj.transform.position = position;
            var collider = obj.AddComponent<BoxCollider>(); collider.size = size; return collider;
        }

        [Test] public void HiddenHeldPickupKeepsItsVolumeAndDropsWithinChosenDistance()
        {
            Fixture();
            Assert.That(pickup.InteractionCollider.enabled, Is.False);
            pickup.GetPlacementVolume(out var center, out var half);
            Assert.That(half.x, Is.EqualTo(.1f).Within(.001f));
            Assert.That(world.TryDropTool(1, 1, out var p, out var q), Is.True);
            var boxCenter = new Vector3(p.X, p.Y, p.Z) + new Quaternion(q.X, q.Y, q.Z, q.W) * center;
            Assert.That(boxCenter.z, Is.InRange(.599f, 1.001f));
            Assert.That(boxCenter.y - half.y, Is.GreaterThanOrEqualTo(.001f));
            Assert.That(pickup.OwnerActorId, Is.EqualTo(1), "Preparing a placement must not mutate ownership.");
        }

        [Test] public void DropDoesNotCrossAThinWallOrShrinkBelowMinimumDistance()
        {
            Fixture(); Cube("Thin wall", new Vector3(0, .8f, .4f), new Vector3(2, 2, .01f)); Physics.SyncTransforms();
            Assert.That(world.TryDropTool(1, 1, out _, out _), Is.False);
        }

        [Test] public void DropStopsAtInvalidFirstSupportInsteadOfUsingFloorBelow()
        {
            Fixture();
            // A steep, thin shelf covers every proposed deposit ray. A floor
            // remains below it but cannot be selected through the first hit.
            var shelf = Cube("Steep shelf", new Vector3(0, .6f, .8f), new Vector3(3, .02f, 3));
            shelf.transform.rotation = Quaternion.Euler(0, 0, 70); Physics.SyncTransforms();
            Assert.That(world.TryDropTool(1, 1, out _, out _), Is.False);
        }

        [Test] public void ThrowPreparationRejectsObstructedMuzzleWithoutMovingPickup()
        {
            Fixture(); Cube("Muzzle wall", new Vector3(0, 1.5f, .3f), new Vector3(2, 2, .01f)); Physics.SyncTransforms();
            Assert.That(world.TryPrepareThrow(1, 1, new Float3(0, 0, 1), 1, out _, out _, out _), Is.False);
            Assert.That(pickup.OwnerActorId, Is.EqualTo(1));
        }

        [Test] public void FastProjectileStopsBeforeThinWall()
        {
            Fixture(); Cube("Thin target", new Vector3(0, 1.5f, 2), new Vector3(2, 2, .01f)); Physics.SyncTransforms();
            var query = new ToolProjectileQuery(1, 1, new Float3(0, 1.4f, .5f), Rotation.Identity, new Float3(0, 0, 5));
            Assert.That(world.SweepProjectile(query, out var hit), Is.True);
            Assert.That(hit.ActorId, Is.Zero);
            Assert.That(hit.Fraction, Is.InRange(0, 1));
            Assert.That(hit.Point.Z + .3f, Is.LessThan(2));
        }

        [Test] public void ProjectileInitialOverlapIsAContactAtZeroFraction()
        {
            Fixture(); Cube("Overlapping wall", new Vector3(0, 1.5f, .7f), new Vector3(2, 2, .3f)); Physics.SyncTransforms();
            var query = new ToolProjectileQuery(1, 1, new Float3(0, 1.4f, .5f), Rotation.Identity, new Float3(0, 0, 5));
            Assert.That(world.SweepProjectile(query, out var hit), Is.True);
            Assert.That(hit.Fraction, Is.Zero);
            Assert.That(hit.StartedOverlapping, Is.True, "Authority must recover the pickup instead of depositing at this penetrating origin.");
            Assert.That(hit.Normal.IsFinite, Is.True);
        }

        [Test] public void EffectConeHitsVisibleMosquitoButStopsAtFirstWall()
        {
            Fixture(GameplayTools.Aerosol, true);
            Assert.That(world.TryToolEffectOrigin(1, 1, Float3.Forward, out var origin, out var direction), Is.True);
            var query = new ToolEffectQuery(ToolEffectKind.AerosolCloud, 1, 1, origin, direction, 2, 30);
            Assert.That(world.QueryToolEffect(query).Count, Is.EqualTo(1));
            Cube("Cover", new Vector3(0, 1.5f, .7f), new Vector3(2, 3, .01f)); Physics.SyncTransforms();
            Assert.That(world.QueryToolEffect(query).Count, Is.Zero);
        }

        [Test] public void EffectOriginCannotCrossThinWallOrUseAnotherActorsItem()
        {
            Fixture(GameplayTools.ElectricRacket, true);
            Assert.That(world.TryToolEffectOrigin(2, 1, Float3.Forward, out _, out _), Is.False);
            Cube("Muzzle cover", new Vector3(0, 1.5f, .2f), new Vector3(2, 3, .01f)); Physics.SyncTransforms();
            Assert.That(world.TryToolEffectOrigin(1, 1, Float3.Forward, out _, out _), Is.False);
        }

        [Test] public void EffectConeRejectsVisibleActorOutsideAngle()
        {
            Fixture(GameplayTools.Aerosol, true);
            var query = new ToolEffectQuery(ToolEffectKind.AerosolCloud, 1, 1, new Float3(0,1.53f,.35f), new Float3(1,0,0), 2, 30);
            Assert.That(world.QueryToolEffect(query).Count, Is.Zero);
        }
        [Test] public void MosquitoTouchingMuzzleRemainsAValidPulseTarget()
        {
            Fixture(GameplayTools.ElectricRacket, true);
            world.Actors[2].transform.position = new Vector3(0, 1.53f, .15f); Physics.SyncTransforms();
            Assert.That(world.TryToolEffectOrigin(1, 1, Float3.Forward, out var origin, out var direction), Is.True);
            Assert.That(world.QueryToolEffect(new ToolEffectQuery(ToolEffectKind.RacketPulse, 1, 1, origin, direction, 1.05f, 35)).Count, Is.EqualTo(1));
        }
        [Test] public void PersistentCloudDoesNotCastThroughDoorThatClosedOverItsOrigin()
        {
            Fixture(GameplayTools.Aerosol, true);
            var origin = new Float3(0,1.53f,.35f);
            Cube("Door over cloud", new Vector3(0,1.53f,.35f), new Vector3(2,3,.1f)); Physics.SyncTransforms();
            Assert.That(world.QueryToolEffect(new ToolEffectQuery(ToolEffectKind.AerosolCloud, 1, 1, origin, Float3.Forward, 2, 30)).Count, Is.Zero);
        }

        [Test] public void ObserveToolReturnsColliderContactWhenPickupRootIsOutsideItsVolume()
        {
            Fixture();
            var box = (BoxCollider)pickup.InteractionCollider;
            box.center = new Vector3(0, .05f, .6f); box.size = new Vector3(.2f, .1f, .2f);
            var state = WorldPickup(3); world.ApplyToolState(state); Physics.SyncTransforms();
            var origin = new Float3(3, 1.5f, 0);
            var expected = box.ClosestPoint(origin.ToUnity()).ToFloat();

            Assert.That(Observe(state, origin, out var point), Is.True);
            Assert.That((point - expected).Length, Is.LessThan(.001f));
            Assert.That((point - state.Position).Length, Is.GreaterThan(.1f),
                "Perception must target the visible collider surface, not the pickup root.");
        }

        [Test] public void ObserveToolRejectsPickupBehindFirstWall()
        {
            Fixture();
            var state = WorldPickup(3); world.ApplyToolState(state);
            Cube("Observation wall", new Vector3(1.5f, .8f, 1.5f), new Vector3(.4f, 2f, .4f)); Physics.SyncTransforms();
            Assert.That(Observe(state, new Float3(0, 1.5f, 0), out _), Is.False);
        }

        [Test] public void ObserveToolRejectsPickupBehindAnotherPickup()
        {
            Fixture(withBlockingPickup: true);
            var state = WorldPickup(3); world.ApplyToolState(state); Physics.SyncTransforms();
            Assert.That(Observe(state, new Float3(0, 1.5f, 0), out _), Is.False);
        }

        [Test] public void ObserveToolRejectsStaleRevisionAndHeldPhase()
        {
            Fixture();
            var current = WorldPickup(3); world.ApplyToolState(current); Physics.SyncTransforms();
            var origin = new Float3(0, 1.5f, 0);
            Assert.That(Observe(WorldPickup(2), origin, out _), Is.False, "A stale public ledger revision cannot identify the current pickup.");
            var held = new ToolPickupSnapshot(1, pickup.ToolId, current.Position, Rotation.Identity, 1, 3);
            Assert.That(Observe(held, origin, out _), Is.False, "Held pickups are private inventory, not world opportunities.");
        }

        private ToolPickupSnapshot WorldPickup(uint revision) =>
            new ToolPickupSnapshot(1, pickup.ToolId, new Float3(3, .1f, 3), Rotation.Identity, 0, revision);

        private bool Observe(in ToolPickupSnapshot state, Float3 origin, out Float3 visiblePoint)
        {
            var method = typeof(UnityGameplayWorld).GetMethod("TryObserveTool", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            object[] args = { 1u, state, origin, default(Float3) };
            bool result = (bool)method.Invoke(world, args);
            visiblePoint = (Float3)args[3];
            return result;
        }
    }
}
