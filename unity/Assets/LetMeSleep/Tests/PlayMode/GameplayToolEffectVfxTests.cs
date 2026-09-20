using System;
using System.Collections;
using System.Linq;
using LetMeSleep.Core;
using LetMeSleep.Gameplay;
using LetMeSleep.Presentation.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace LetMeSleep.Tests.PlayMode
{
    public sealed class GameplayToolEffectVfxTests
    {
        private GameObject root;
        private GameplayVfxPresenter presenter;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            root = new GameObject("ToolEffectVfxTest", typeof(GameplayVfxPresenter));
            presenter = root.GetComponent<GameplayVfxPresenter>();
            yield return null;
        }

        [UnityTest]
        public IEnumerator SnapshotCreatesUpdatesAndPurgesAuthoritativeVisualsWithoutAudio()
        {
            var pulse = Effect(1, 11, ToolEffectKind.RacketPulse, new Float3(1, 1, 1), Float3.Forward, 10,
                20 + HumanEquipmentProfile.RacketPulseHalfTicks);
            var cloud = Effect(2, 12, ToolEffectKind.AerosolCloud, new Float3(2, 1, 1), Float3.Forward, 10,
                20 + HumanEquipmentProfile.AerosolCloudHalfTicks);
            presenter.ApplySnapshot(State(1, 2, 10, pulse, cloud)); yield return null;

            var pulseRoot = Child("ToolEffect-RacketPulse-1"); var cloudRoot = Child("ToolEffect-AerosolCloud-2");
            Assert.That(pulseRoot.GetComponent<LineRenderer>(), Is.Not.Null);
            Assert.That(cloudRoot.GetComponent<ParticleSystem>(), Is.Not.Null);
            Assert.That(root.GetComponentsInChildren<AudioSource>(true), Is.Empty);

            pulse = Effect(1, 11, ToolEffectKind.RacketPulse, new Float3(3, 2, 1), Float3.Up, 10,
                20 + HumanEquipmentProfile.RacketPulseHalfTicks);
            presenter.ApplySnapshot(State(1, 2, 11, pulse)); yield return null;
            Assert.That(Child("ToolEffect-RacketPulse-1").transform.position, Is.EqualTo(new Vector3(3, 2, 1)));
            Assert.That(root.transform.Find("ToolEffect-AerosolCloud-2"), Is.Null, "Snapshot absence must purge the persistent cloud.");

            presenter.ApplySnapshot(State(1, 3, 1)); yield return null;
            Assert.That(root.transform.Cast<Transform>().Any(child => child.name.StartsWith("ToolEffect-", StringComparison.Ordinal)), Is.False,
                "Round identity change must purge all previous visuals.");
        }

        [UnityTest]
        public IEnumerator PulseExpiresAtEndHalfTickWithoutAnotherSnapshot()
        {
            uint end = 20 + HumanEquipmentProfile.RacketPulseHalfTicks;
            presenter.ApplySnapshot(State(1, 2, 10,
                Effect(7, 11, ToolEffectKind.RacketPulse, Float3.Zero, Float3.Forward, 10, end)));
            yield return null;
            Assert.That(Child("ToolEffect-RacketPulse-7"), Is.Not.Null);

            yield return new WaitForSecondsRealtime(HumanEquipmentProfile.RacketPulseHalfTicks / 60f + .08f);
            Assert.That(root.transform.Find("ToolEffect-RacketPulse-7"), Is.Null,
                "Presentation must not outlive the authoritative half-tick deadline.");

            presenter.ApplySnapshot(State(1, 2, 21,
                Effect(8, 11, ToolEffectKind.RacketPulse, Float3.Zero, Float3.Forward, 10, end)));
            yield return null;
            Assert.That(root.transform.Find("ToolEffect-RacketPulse-8"), Is.Null,
                "A delayed snapshot at the deadline cannot resurrect an expired effect.");
        }

        [UnityTest]
        public IEnumerator EffectsRemainUntilTheirAuthoritativeDurationsAndThenExpire()
        {
            presenter.ApplySnapshot(State(1, 2, 10,
                Effect(21, 11, ToolEffectKind.RacketPulse, Float3.Zero, Float3.Forward, 10,
                    20 + HumanEquipmentProfile.RacketPulseHalfTicks),
                Effect(22, 12, ToolEffectKind.AerosolCloud, Float3.Zero, Float3.Forward, 10,
                    20 + HumanEquipmentProfile.AerosolCloudHalfTicks)));
            yield return null;

            yield return new WaitForSecondsRealtime(HumanEquipmentProfile.RacketPulseHalfTicks / 60f - .1f);
            Assert.That(root.transform.Find("ToolEffect-RacketPulse-21"), Is.Not.Null,
                "The 0.35 s pulse must not be removed before its authoritative deadline.");
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(root.transform.Find("ToolEffect-RacketPulse-21"), Is.Null);

            yield return new WaitForSecondsRealtime(HumanEquipmentProfile.AerosolCloudHalfTicks / 60f - .65f);
            Assert.That(root.transform.Find("ToolEffect-AerosolCloud-22"), Is.Not.Null,
                "The 1.2 s cloud must remain visible until its authoritative deadline.");
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(root.transform.Find("ToolEffect-AerosolCloud-22"), Is.Null);
        }

        private Transform Child(string name)
        {
            var child = root.transform.Find(name);
            Assert.That(child, Is.Not.Null, name);
            return child;
        }

        private static ToolEffectSnapshot Effect(uint id, uint pickup, ToolEffectKind kind, Float3 origin,
            Float3 forward, uint start, uint endHalfTick) =>
            new ToolEffectSnapshot(id, pickup, 1, kind, origin, forward, start, endHalfTick);

        private static GameSessionState State(ulong epoch, ulong round, uint tick, params ToolEffectSnapshot[] effects)
        {
            var definitions = new[]
            {
                new ToolPickupDefinition(11, GameplayTools.ElectricRacket, Float3.Zero, Rotation.Identity),
                new ToolPickupDefinition(12, GameplayTools.Aerosol, Float3.Zero, Rotation.Identity)
            };
            var config = new GameplayRoundConfig(epoch, round, RoomRules.AlfaMap, "content", 180, tools: definitions);
            var actors = new[]
            {
                new ActorSnapshot(1, PlayerRole.Human, LifeState.Active, 1, Float3.Zero, Float3.Zero,
                    Rotation.Identity, Float3.Forward, 0, 0, 1, 1, true, 0, 0, null, null, default, 0, GameplayTools.ElectricRacket),
                new ActorSnapshot(2, PlayerRole.Mosquito, LifeState.Flying, 1, new Float3(0, 1, 1), Float3.Zero,
                    Rotation.Identity, Float3.Forward, 0, 0, 1, 1, false, 0, 0, null, null, default, 0)
            };
            var pickups = new[]
            {
                new ToolPickupSnapshot(11, GameplayTools.ElectricRacket, Float3.Zero, Rotation.Identity, 1),
                new ToolPickupSnapshot(12, GameplayTools.Aerosol, Float3.Zero, Rotation.Identity, 1)
            };
            return new GameSessionState(config, tick, SimulationPhase.Running, 0, RoundEndReason.None,
                PlayerRole.Unassigned, actors, Array.Empty<DoorSnapshot>(), pickups, 0, 0, 0, effects);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (root) Object.Destroy(root);
            yield return null;
        }
    }
}
