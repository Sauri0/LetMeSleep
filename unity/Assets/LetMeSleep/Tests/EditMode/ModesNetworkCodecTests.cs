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
        public void SnapshotPreservesAllTaskTimingRulesAndRejectsPreviousProtocol()
        {
            var rules = new ModeRuleProfile(GameModes.Tasks, taskSuccessRecoveryTicks: 120,
                taskInterruptionGraceTicks: 45, taskDecayBasisPointsPerSecond: 1250);
            var config = TasksConfig(rules);
            var original = new GameSessionState(config, 30, SimulationPhase.Running, 0, RoundEndReason.None,
                PlayerRole.Unassigned, Actors(GameModes.Tasks), Array.Empty<DoorSnapshot>(),
                tasksCompleted: 1, tasksGoal: 2, viableTaskOpportunities: 3);
            byte[] packet = GameplayWireCodec.Encode(original);
            Assert.That(GameplayWireCodec.TryDecode(packet, out GameSessionState decoded), Is.True);
            Assert.That(decoded.BalanceHash, Is.EqualTo(config.BalanceHash));
            Assert.That(decoded.BalanceHash, Does.Contain(":" + rules.Hash + ":"));
            packet[4] = 3; packet[5] = 0;
            Assert.That(GameplayWireCodec.TryDecode(packet, out GameSessionState rejected), Is.False);
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
            Assert.That(packet[0], Is.EqualTo(4));
            Assert.That(packet.Length, Is.LessThanOrEqualTo(MessageFraming.MaximumMessageBytes));
            Assert.That(ContainsAscii(packet, config.ModeId), Is.True);
            Assert.That(ContainsAscii(packet, config.ModeRuleProfileId), Is.True);
            Assert.That(ContainsAscii(packet, config.ObjectiveCatalogHash), Is.True);
            Assert.That(ContainsAscii(packet, config.Objectives[0].ObjectiveId), Is.True);
            Assert.That(ContainsAscii(identityBytes, config.BalanceHash), Is.True);

            using var reader = new BinaryReader(new MemoryStream(packet, false), Encoding.UTF8);
            reader.ReadByte(); reader.ReadUInt64(); reader.ReadUInt64();
            foreach (int limit in new[] { 64, 128, 16, 64, 64, 512 }) RoomWireCodec.ReadText(reader, limit);
            reader.ReadInt32(); reader.ReadSingle(); reader.ReadInt32();
            for (int i = 0; i < 5; i++) reader.ReadSingle();
            Assert.That(reader.ReadByte(), Is.EqualTo(config.ModeRules.MosquitoLives));
            var serializedRules = new uint[7];
            for (int i = 0; i < serializedRules.Length; i++) serializedRules[i] = reader.ReadUInt32();
            Assert.That(serializedRules, Is.EqualTo(new uint[] { 1200, 900, 450, 150, 90, 30, 1000 }));
            Assert.That(reader.ReadByte(), Is.EqualTo(config.Objectives.Count), "Rule fields must not shift the authored catalog.");
        }

        [Test]
        public void ReleaseProtocolAndWireSchemasAreExplicitlyDecoupledFromAlpha()
        {
            Assert.That(RoomSession.Protocol, Is.EqualTo("lms-unity-020-3"));
            Assert.That(RoomWireCodec.Version, Is.EqualTo(3));
            Assert.That(GameplayWireCodec.Version, Is.EqualTo(4));
        }

        [TestCase(GameModes.Blood)]
        [TestCase(GameModes.Survival)]
        public void PrivateStateWithoutTaskIsAcceptedOutsideTasks(string mode)
        {
            var config = new GameplayRoundConfig(81, 5, RoomRules.AlfaMap, "content", 120, modeId: mode);
            var session = PrivateSession(config);
            Assert.That(PrivateMatches(session, PrivateState()), Is.True);
            Assert.That(PrivateMatches(session, PrivateState(assignment: KnownAssignment())), Is.False);
        }

        [Test]
        public void TasksPrivateStateAllowsNoAssignmentAndKnownAssignmentButNotUnknownObjective()
        {
            var session = PrivateSession(TasksConfig());
            Assert.That(PrivateMatches(session, PrivateState()), Is.True);
            Assert.That(PrivateMatches(session, PrivateState(assignment: KnownAssignment())), Is.True);
            var unknown = new TaskAssignment("task-not-in-catalog", 30, 300, 90, 17, TaskAssignmentStatus.Active, 1);
            Assert.That(PrivateMatches(session, PrivateState(assignment: unknown)), Is.False);
        }

        [TestCase(82ul, 5ul, 1u)]
        [TestCase(81ul, 6ul, 1u)]
        [TestCase(81ul, 5ul, 99u)]
        public void PrivateStateRejectsWrongEpochRoundOrRosterActor(ulong epoch, ulong round, uint actor)
        {
            var session = PrivateSession(TasksConfig());
            Assert.That(PrivateMatches(session, PrivateState(actor, epoch, round, KnownAssignment())), Is.False);
        }

        [Test]
        public void PrivateStateRequiresRoundAndNonNullPayload()
        {
            Assert.That(PrivateMatches(PrivateSession(null), PrivateState()), Is.False);
            Assert.That(PrivateMatches(PrivateSession(TasksConfig()), null), Is.False);
        }

        private static TaskAssignment KnownAssignment() => new TaskAssignment(
            "task-sink", 30, 300, 90, 17, TaskAssignmentStatus.Active, 1);

        [Test]
        public void BeginDecoderValidatesFullPacketTruncationsVersionHashAndCanonicalRules()
        {
            var config = TasksConfig();
            var core = RoomSessionTestSupport.TwoPlayerSession();
            core.ChangeRules("owner-puid", new RoomRules(1, 120, 0, RoomRules.AlfaMap, GameModes.Tasks));
            for (int i = 0; i < 5; i++)
            {
                RoomSessionTestSupport.ReadyEveryone(core); core.StartRound("owner-puid");
                if (i < 4) { core.FinishRound("owner-puid"); core.ReturnToWaiting("owner-puid"); }
            }
            var actors = core.Snapshot().Members.Select((m, i) => new SpawnActor((uint)i + 1, m.Id, m.Role, Float3.Zero)).ToArray();
#pragma warning disable SYSLIB0050
            var room = (OnlineRoomCoordinator)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(OnlineRoomCoordinator));
#pragma warning restore SYSLIB0050
            typeof(OnlineRoomCoordinator).GetField("<Current>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(room, core.Snapshot());
            var session = PrivateSession(null);
            foreach (var field in new Dictionary<string, object>
            {
                ["room"] = room, ["localId"] = "guest-puid", ["contentHash"] = config.ContentHash,
                ["doors"] = new Func<IReadOnlyList<DoorDefinition>>(() => Array.Empty<DoorDefinition>()),
                ["tools"] = new Func<IReadOnlyList<ToolPickupDefinition>>(() => Array.Empty<ToolPickupDefinition>()),
                ["objectives"] = new Func<IReadOnlyList<ObjectiveDefinition>>(() => config.Objectives)
            }) typeof(OnlineGameplaySession).GetField(field.Key, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(session, field.Value);
            var encode = typeof(OnlineGameplaySession).GetMethod("EncodeBegin", BindingFlags.Static | BindingFlags.NonPublic);
            var decode = typeof(OnlineGameplaySession).GetMethod("TryBegin", BindingFlags.Instance | BindingFlags.NonPublic);
            bool Accept(byte[] bytes) => (bool)decode.Invoke(session, new object[] { bytes, null, null });
            var packet = (byte[])encode.Invoke(null, new object[] { config, actors });
            Assert.That(Accept(packet), Is.True);
            for (int length = 0; length < packet.Length; length++)
                Assert.That(Accept(packet.Take(length).ToArray()), Is.False, "truncated at " + length);
            var old = (byte[])packet.Clone(); old[0] = 3;
            Assert.That(Accept(old), Is.False);
            var changed = (byte[])packet.Clone();
            ReplaceAscii(changed, config.BalanceHash, "x" + config.BalanceHash.Substring(1));
            Assert.That(Accept(changed), Is.False);
            var custom = TasksConfig(new ModeRuleProfile(GameModes.Tasks, taskSuccessRecoveryTicks: 120));
            Assert.That(Accept((byte[])encode.Invoke(null, new object[] { custom, actors })), Is.False);
            // A reserved peer retains the same authenticated role and is part of Begin.
            core.Disconnect("guest-puid", 1);
            typeof(OnlineRoomCoordinator).GetField("<Current>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(room, core.Snapshot());
            Assert.That(Accept(packet), Is.True);
        }

        [Test]
        public void ResumeAcknowledgementRequiresLatestPeerChallengeAndCannotPrepareAnotherPeer()
        {
            var session = PrivateSession(TasksConfig());
            var type = typeof(OnlineGameplaySession);
            var waitingField = type.GetField("waiting", BindingFlags.Instance | BindingFlags.NonPublic);
            var resumingField = type.GetField("resuming", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(waitingField, Is.Not.Null);
            Assert.That(resumingField, Is.Not.Null);
            waitingField.SetValue(session, Activator.CreateInstance(waitingField.FieldType));
            resumingField.SetValue(session, Activator.CreateInstance(resumingField.FieldType));
            var waiting = (HashSet<string>)waitingField.GetValue(session);
            waiting.Add("human");

            var beginResume = type.GetMethod("BeginResumeAttempt", BindingFlags.Instance | BindingFlags.NonPublic);
            var accept = type.GetMethod("AcceptAcknowledgement", BindingFlags.Instance | BindingFlags.NonPublic);
            var prepared = type.GetMethod("HasPeerPrepared", BindingFlags.Instance | BindingFlags.NonPublic);
            var roundIdentity = type.GetMethod("RoundIdentity", BindingFlags.Static | BindingFlags.NonPublic);
            var resumeIdentity = type.GetMethod("ResumeIdentity", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(beginResume, Is.Not.Null); Assert.That(accept, Is.Not.Null); Assert.That(prepared, Is.Not.Null);
            Assert.That(roundIdentity, Is.Not.Null); Assert.That(resumeIdentity, Is.Not.Null);

            ulong first = (ulong)beginResume.Invoke(session, new object[] { "human" });
            byte[] initialAck = (byte[])roundIdentity.Invoke(null, new object[] { TasksConfig() });
            Assert.That(accept.Invoke(session, new object[] { "human", initialAck, false }), Is.False,
                "An initial-round ACK must not clear an active resume barrier.");
            Assert.That(prepared.Invoke(session, new object[] { "human" }), Is.False);
            Assert.That(prepared.Invoke(session, new object[] { "mosquito" }), Is.True,
                "A peer-specific resume must not pause another prepared member.");

            ulong second = (ulong)beginResume.Invoke(session, new object[] { "human" });
            Assert.That(second, Is.GreaterThan(first));
            byte[] staleResumeAck = (byte[])resumeIdentity.Invoke(session, new object[] { first });
            Assert.That(accept.Invoke(session, new object[] { "human", staleResumeAck, true }), Is.False,
                "A previous reconnect challenge must not satisfy a newer attempt.");
            Assert.That(prepared.Invoke(session, new object[] { "human" }), Is.False);

            byte[] currentResumeAck = (byte[])resumeIdentity.Invoke(session, new object[] { second });
            Assert.That(accept.Invoke(session, new object[] { "human", currentResumeAck, true }), Is.True);
            Assert.That(prepared.Invoke(session, new object[] { "human" }), Is.True);
            Assert.That(prepared.Invoke(session, new object[] { "mosquito" }), Is.True);
        }

        private static ActorPrivateState PrivateState(uint actor = 1, ulong epoch = 81, ulong round = 5,
            TaskAssignment assignment = null) => new ActorPrivateState(actor, 7, 4, CommandReject.None,
                InteractionHint.Task, 0, 0, 0, 0, true, DoorUseResult.Accepted, epoch, round, 60, assignment);

        private static OnlineGameplaySession PrivateSession(GameplayRoundConfig config)
        {
            // Exercise the real pure session predicate without starting native EOS.
#pragma warning disable SYSLIB0050
            var session = (OnlineGameplaySession)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(OnlineGameplaySession));
#pragma warning restore SYSLIB0050
            typeof(OnlineGameplaySession).GetField("config", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(session, config);
            typeof(OnlineGameplaySession).GetField("roster", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(session, new[]
            {
                new SpawnActor(1, "human", PlayerRole.Human, Float3.Zero),
                new SpawnActor(2, "mosquito", PlayerRole.Mosquito, Float3.Forward)
            });
            return session;
        }

        private static bool PrivateMatches(OnlineGameplaySession session, ActorPrivateState state)
        {
            var method = typeof(OnlineGameplaySession).GetMethod("PrivateMatchesRound", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, "The production send/receive predicate must exist.");
            return (bool)method.Invoke(session, new object[] { state });
        }

        private static GameplayRoundConfig TasksConfig(ModeRuleProfile rules = null)
        {
            var objective = new ObjectiveDefinition("task-sink", ObjectiveKind.Clean, "task.sink", "action.clean",
                new Float3(2, 0, 2), new Float3(2, 0, 2), 1, 90, "kitchen", 120);
            return new GameplayRoundConfig(81, 5, RoomRules.AlfaMap, "content", 120, modeId: GameModes.Tasks,
                objectives: new[] { objective }, tasksGoal: 2, modeRules: rules);
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
