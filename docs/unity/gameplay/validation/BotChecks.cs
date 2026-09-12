using System;
using System.Collections.Generic;
using System.Linq;
using LetMeSleep.Core;
using LetMeSleep.Gameplay;
using NUnit.Framework;

public sealed class BotChecks
{
    private static readonly BotRegion[] Regions = {
        new BotRegion("Living", new Float3(.18f, 0, .18f), new Float3(4.98f, 2.8f, 4.58f)),
        new BotRegion("West", new Float3(.18f, 0, 4.76f), new Float3(4.98f, 2.8f, 7.38f)),
        new BotRegion("Hall", new Float3(5.16f, 0, .18f), new Float3(6.96f, 2.8f, 11.22f)) };
    private static readonly BotPassage[] Passages = {
        new BotPassage("LivingDoor", "West", "Living", new[] { new Float3(3.86f,1.1f,5.22f), new Float3(3.86f,1.1f,4.67f), new Float3(3.86f,1.1f,4.12f) }),
        new BotPassage("HallWest0", "Hall", "West", new[] { new Float3(5.62f,1.1f,6.05f), new Float3(5.07f,1.1f,6.05f), new Float3(4.52f,1.1f,6.05f) }) };
    [Test] public void AuthoredLivingRouteDescendsAndReachesHallWithoutCrossingPartition()
    {
        foreach (var spawn in new[] { new Float3(1, 2.4f, 1), new Float3(4.92f, 2.4f, 2.57f), new Float3(.236f, 2.4f, .235f) })
        {
            var patrol = new BotPatrol(Regions, Passages, 2); var position = spawn; bool hall = false;
            Assert.That(patrol.Direction(position, 0, _ => true).Y, Is.LessThan(0));
            for (uint tick = 0; tick < 900; tick += 3)
            {
                var desired = patrol.Direction(position, tick, _ => true);
                position += Float3.ClampLength(desired, .19f);
                bool inRegion = Regions.Any(r => r.Contains(position));
                bool livingOpening = Math.Abs(position.Z - 4.67f) < .15f && Math.Abs(position.X - 3.86f) < .48f && position.Y < 2.1f;
                bool hallOpening = Math.Abs(position.X - 5.07f) < .15f && Math.Abs(position.Z - 6.05f) < .82f && position.Y < 2.1f;
                Assert.That(inRegion || livingOpening || hallOpening, Is.True, "Route left authored regions/doorway: " + position.X + "," + position.Y + "," + position.Z);
                if (Regions[2].Contains(position)) { hall = true; break; }
            }
            Assert.That(hall, Is.True, "Training mosquito did not explore from Living into Hall.");
        }
    }
    [Test] public void ClosedLivingDoorKeepsPatrolInsideLiving()
    {
        var patrol = new BotPatrol(Regions, Passages, 2); var position = new Float3(1, 2.4f, 1);
        for (uint tick = 0; tick < 1800; tick += 3)
        {
            position += Float3.ClampLength(patrol.Direction(position, tick, id => id != "LivingDoor"), .19f);
            Assert.That(Regions[0].Contains(position), Is.True);
        }
    }
    [Test] public void DoorClosingDuringApproachAbandonsCrossing()
    {
        var patrol = new BotPatrol(Regions, Passages, 2); var position = new Float3(3.86f, 1.1f, 4.3f);
        patrol.Direction(position, 0, _ => true);
        for (uint tick = 3; tick < 180; tick += 3)
        {
            position += Float3.ClampLength(patrol.Direction(position, tick, _ => false), .19f);
            Assert.That(Regions[0].Contains(position), Is.True);
        }
    }
    [Test] public void PersistentBlockedPassageReceivesCooldownAndInteriorEscape()
    {
        var patrol = new BotPatrol(Regions, Passages, 2); var position = new Float3(4.8f, 1.1f, 3);
        var before = patrol.Direction(position, 0, _ => true);
        patrol.Direction(position, 3, _ => true);
        var after = patrol.Direction(position, 160, _ => true);
        Assert.That((before - after).Length, Is.GreaterThan(.5f));
    }
    [Test] public void BotSteeringCanStopMovementWithoutIssuingActions()
    {
        var state = new ActorSnapshot(2, PlayerRole.Mosquito, LifeState.Flying, 1, new Float3(1,1,1), Float3.Zero, Rotation.Identity, Float3.Forward, 0, 0, 1, 1, false, 0, 0, null, null, default, 0);
        var bot = new BotController();
        var command = bot.Decide(new BotObservation(state, Array.Empty<BotTarget>(), Float3.Forward, false, _ => Float3.Zero), new BotTick(1,1,3));
        Assert.That(command.Input.MovePlanar.Y, Is.Zero); Assert.That(command.Action.HasValue, Is.False);
    }
}
