using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using LetMeSleep.Core;
using LetMeSleep.Gameplay;
using LetMeSleep.Online;
using NUnit.Framework;

namespace LetMeSleep.Tests.EditMode
{
    public sealed class ModesNetworkCodecTests
    {
        [TestCase(GameModes.Blood)]
        [TestCase(GameModes.Survival)]
        [TestCase(GameModes.Tasks)]
        public void RoomRoundTripPreservesModeAndProfileAndRejectsOldSchema(string mode)
        {
            var session = RoomSessionTestSupport.TwoPlayerSession();
            var rules = new RoomRules(1, 120, 20, RoomRules.AlfaMap, mode);
            Assert.That(session.ChangeRules("owner-puid", rules), Is.EqualTo(RoomError.None));

            byte[] packet = RoomWireCodec.Encode(session.Snapshot());
            Assert.That(packet[0], Is.EqualTo(RoomWireCodec.Version));
            Assert.That(RoomWireCodec.TryDecode(packet, "owner-puid", out var decoded), Is.True);
            Assert.That(decoded.Rules.ModeId, Is.EqualTo(mode));
            Assert.That(decoded.Rules.ModeRuleProfileId, Is.EqualTo(GameModes.ProfileId(mode)));
            Assert.That(decoded.Rules.BloodQuota, Is.EqualTo(mode == GameModes.Blood ? 20 : 0));

            packet[0] = 1;
            Assert.That(RoomWireCodec.TryDecode(packet, "owner-puid", out _), Is.False);
        }

        [Test]
        public void RoomCodecRejectsUnknownModeAndMismatchedProfile()
        {
            var session = RoomSessionTestSupport.TwoPlayerSession();
            Assert.That(session.ChangeRules("owner-puid", new RoomRules(1, 120, 0, RoomRules.AlfaMap, GameModes.Tasks)), Is.EqualTo(RoomError.None));
            byte[] packet = RoomWireCodec.Encode(session.Snapshot());

            byte[] unknownMode = (byte[])packet.Clone();
            ReplaceAscii(unknownMode, GameModes.Tasks, "xasks");
            Assert.That(RoomWireCodec.TryDecode(unknownMode, "owner-puid", out _), Is.False);

            byte[] wrongProfile = (byte[])packet.Clone();
            ReplaceAscii(wrongProfile, GameModes.ProfileId(GameModes.Tasks), "v020-tasks-2");
            Assert.That(RoomWireCodec.TryDecode(wrongProfile, "owner-puid", out _), Is.False);
        }

        [Test]
        public void TasksSnapshotRoundTripPreservesPublicScoreAndLivesWithoutPrivateObjective()
        {
            var config = TasksConfig();
            var original = new GameSessionState(config, 30, SimulationPhase.Running, 0, RoundEndReason.None,
                PlayerRole.Unassigned, Actors(GameModes.Tasks), Array.Empty<DoorSnapshot>(), tasksCompleted: 1, tasksGoal: 2, viableTaskOpportunities: 3);

            byte[] packet = GameplayWireCodec.Encode(original);
            Assert.That(BitConverter.ToUInt16(packet, 4), Is.EqualTo(GameplayWireCodec.Version));
            Assert.That(ContainsAscii(packet, config.Objectives[0].ObjectiveId), Is.False, "A public snapshot must not reveal a private assignment.");
            Assert.That(GameplayWireCodec.TryDecode(packet, out GameSessionState decoded), Is.True);
            Assert.That(decoded.ModeId, Is.EqualTo(GameModes.Tasks));
            Assert.That(decoded.TasksCompleted, Is.EqualTo(1));
            Assert.That(decoded.TasksGoal, Is.EqualTo(2));
            Assert.That(decoded.ViableTaskOpportunities, Is.EqualTo(3));
            Assert.That(decoded.Actors.Single(a => a.Role == PlayerRole.Mosquito).LivesRemaining, Is.EqualTo(3));
        }

        [Test]
        public void PrivateAssignmentRoundTripIsBoundedAndOldSchemaIsRejected()
        {
            var assignment = new TaskAssignment("task-sink", 30, 300, 90, 17, TaskAssignmentStatus.Active, 1);
            var original = new ActorPrivateState(1, 7, 4, CommandReject.None, InteractionHint.Task, 0, 0, 0, 0,
                true, DoorUseResult.Accepted, 81, 5, 60, assignment);

            byte[] packet = GameplayWireCodec.Encode(original);
            Assert.That(GameplayWireCodec.TryDecode(packet, out ActorPrivateState decoded), Is.True);
            Assert.That(decoded.ActorId, Is.EqualTo(1));
            Assert.That(decoded.TaskAssignment.ObjectiveId, Is.EqualTo("task-sink"));
            Assert.That(decoded.TaskAssignment.ProgressTicks, Is.EqualTo(17));
            Assert.That(decoded.TaskAssignment.PersonalFailures, Is.EqualTo(1));

            packet[4] = 2; packet[5] = 0;
            Assert.That(GameplayWireCodec.TryDecode(packet, out ActorPrivateState rejected), Is.False);
            Assert.That(rejected, Is.Null);
        }

        [Test]
        public void PublicEventChannelRejectsPrivateTaskEventsAndLocationLeaks()
        {
            foreach (var kind in new[] { GameplayEventKind.TaskAssigned, GameplayEventKind.TaskProgressed, GameplayEventKind.TaskMissed })
            {
                var item = new GameplayEvent(81, 5, 1, 30, kind, 1, 0, 1, Float3.Zero, Float3.Zero);
                Assert.Throws<InvalidDataException>(() => GameplayWireCodec.Encode(item));
            }

            var leakingCompletion = new GameplayEvent(81, 5, 1, 30, GameplayEventKind.TaskCompleted, 1, 0, 1,
                new Float3(1, 2, 3), Float3.Zero);
            Assert.Throws<InvalidDataException>(() => GameplayWireCodec.Encode(leakingCompletion));

            var safeCompletion = new GameplayEvent(81, 5, 1, 30, GameplayEventKind.TaskCompleted, 1, 0, 1, Float3.Zero, Float3.Zero);
            Assert.That(GameplayWireCodec.TryDecode(GameplayWireCodec.Encode(safeCompletion), out GameplayEvent decoded), Is.True);
            Assert.That(decoded.Position.LengthSquared, Is.Zero);
        }

        [Test]
        public void CrossModeScoreAndLifePayloadsCannotBeEncoded()
        {
            var survival = new GameplayRoundConfig(81, 5, RoomRules.AlfaMap, "content", 120, modeId: GameModes.Survival);
            Assert.Throws<ArgumentException>(() => new GameSessionState(survival, 30, SimulationPhase.Running, 0, RoundEndReason.None,
                PlayerRole.Unassigned, Actors(GameModes.Survival), Array.Empty<DoorSnapshot>(), tasksCompleted: 1, tasksGoal: 1, viableTaskOpportunities: 1));

            var tasks = TasksConfig();
            var invalidActors = Actors(GameModes.Tasks).ToArray();
            invalidActors[1] = Actor(2, PlayerRole.Mosquito, LifeState.Flying, 0);
            var invalidLives = new GameSessionState(tasks, 30, SimulationPhase.Running, 0, RoundEndReason.None,
                PlayerRole.Unassigned, invalidActors, Array.Empty<DoorSnapshot>(), tasksGoal: 2, viableTaskOpportunities: 3);
            Assert.Throws<InvalidDataException>(() => GameplayWireCodec.Encode(invalidLives));
        }

        [Test]
        public void BeginPacketCarriesModeProfileCatalogAndFullRoundIdentity()
        {
            var config = TasksConfig();
            var roster = new[]
            {
                new SpawnActor(1, "human", PlayerRole.Human, Float3.Zero),
                new SpawnActor(2, "mosquito", PlayerRole.Mosquito, new Float3(1, 1, 1))
            };
            var encode = typeof(OnlineGameplaySession).GetMethod("EncodeBegin", BindingFlags.NonPublic | BindingFlags.Static);
            var identity = typeof(OnlineGameplaySession).GetMethod("RoundIdentity", BindingFlags.NonPublic | BindingFlags.Static);

            var packet = (byte[])encode.Invoke(null, new object[] { config, roster });
            var identityBytes = (byte[])identity.Invoke(null, new object[] { config });
            Assert.That(packet[0], Is.EqualTo(3));
            Assert.That(packet.Length, Is.LessThanOrEqualTo(MessageFraming.MaximumMessageBytes));
            Assert.That(ContainsAscii(packet, config.ModeId), Is.True);
            Assert.That(ContainsAscii(packet, config.ModeRuleProfileId), Is.True);
            Assert.That(ContainsAscii(packet, config.ObjectiveCatalogHash), Is.True);
            Assert.That(ContainsAscii(packet, config.Objectives[0].ObjectiveId), Is.True);
            Assert.That(ContainsAscii(identityBytes, config.BalanceHash), Is.True);
        }

        [Test]
        public void ReleaseProtocolAndWireSchemasAreExplicitlyDecoupledFromAlpha()
        {
            Assert.That(RoomSession.Protocol, Is.EqualTo("lms-unity-020-1"));
            Assert.That(RoomWireCodec.Version, Is.EqualTo(2));
            Assert.That(GameplayWireCodec.Version, Is.EqualTo(3));
        }

        private static GameplayRoundConfig TasksConfig()
        {
            var objective = new ObjectiveDefinition("task-sink", ObjectiveKind.Clean, "task.sink", "action.clean",
                new Float3(2, 0, 2), new Float3(2, 0, 2), 1, 90, "kitchen", 120);
            return new GameplayRoundConfig(81, 5, RoomRules.AlfaMap, "content", 120, modeId: GameModes.Tasks,
                objectives: new[] { objective }, tasksGoal: 2);
        }

        private static IReadOnlyList<ActorSnapshot> Actors(string mode) => new[]
        {
            Actor(1, PlayerRole.Human, LifeState.Active, 0),
            Actor(2, PlayerRole.Mosquito, LifeState.Flying, mode == GameModes.Tasks ? 3 : mode == GameModes.Survival ? 1 : 0)
        };

        private static ActorSnapshot Actor(uint id, PlayerRole role, LifeState life, int lives) => new ActorSnapshot(
            id, role, life, 1, Float3.Zero, Float3.Zero, Rotation.Identity, Float3.Forward, 0, 0, 1, 0,
            role == PlayerRole.Human, 0, 0, null, null, default, 0, GameplayTools.Hands, lives);

        private static bool ContainsAscii(byte[] bytes, string text)
        {
            var needle = Encoding.UTF8.GetBytes(text);
            for (int i = 0; i <= bytes.Length - needle.Length; i++)
                if (needle.Select((value, offset) => bytes[i + offset] == value).All(match => match)) return true;
            return false;
        }

        private static void ReplaceAscii(byte[] bytes, string oldValue, string newValue)
        {
            Assert.That(newValue.Length, Is.EqualTo(oldValue.Length));
            var oldBytes = Encoding.UTF8.GetBytes(oldValue); var replacement = Encoding.UTF8.GetBytes(newValue);
            for (int i = 0; i <= bytes.Length - oldBytes.Length; i++)
            {
                if (!oldBytes.Select((value, offset) => bytes[i + offset] == value).All(match => match)) continue;
                Buffer.BlockCopy(replacement, 0, bytes, i, replacement.Length); return;
            }
            Assert.Fail("Wire fixture did not contain expected text: " + oldValue);
        }
    }
}
