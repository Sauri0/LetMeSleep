using System;
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

        private void Fixture()
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
            pickup.PickupId = 1; pickup.ToolId = GameplayTools.Slipper; pickup.InteractionCollider = collider;
            world.BeginRound(new[] { new SpawnActor(1, "human", PlayerRole.Human, Float3.Zero) }, Array.Empty<DoorDefinition>());
            world.BeginTools(world.GetToolDefinitions());
            world.ApplyToolState(new ToolPickupSnapshot(1, GameplayTools.Slipper, new Float3(3, .1f, 3), Rotation.Identity, 1, 2));
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
    }
}
