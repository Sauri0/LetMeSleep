using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using LetMeSleep.Core;
using LetMeSleep.Gameplay;
using LetMeSleep.Online;
using NUnit.Framework;
using static LetMeSleep.Tests.EditMode.RoomSessionTestSupport;

namespace LetMeSleep.Tests.EditMode
{
    public sealed class LateJoinNetworkTests
    {
        [Test]
        public void ResumeBeginAcceptsOriginalActorsWhileLateMemberWaitsWithoutActor()
        {
            var core = TwoPlayerSession(); ReadyEveryone(core); core.StartRound("owner-puid");
            var active = core.Snapshot();
            var actors = active.Members.Select((member, index) =>
                new SpawnActor((uint)index + 1, member.Id, member.Role, Float3.Zero)).ToArray();
            var config = new GameplayRoundConfig(81, (ulong)active.Round, active.Rules.MapId, "content",
                active.Rules.RoundSeconds, active.Rules.BloodQuota);
            Assert.That(core.Join("late-puid", "Late", RoomSession.Protocol), Is.EqualTo(RoomError.None));
            Assert.That(core.Snapshot().Members.Single(member => member.Id == "late-puid").Role, Is.EqualTo(PlayerRole.Unassigned));

#pragma warning disable SYSLIB0050
            var room = (OnlineRoomCoordinator)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(OnlineRoomCoordinator));
            var session = (OnlineGameplaySession)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(OnlineGameplaySession));
#pragma warning restore SYSLIB0050
            typeof(OnlineRoomCoordinator).GetField("<Current>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(room, core.Snapshot());
            foreach (var field in new Dictionary<string, object>
            {
                ["room"] = room, ["localId"] = "guest-puid", ["contentHash"] = config.ContentHash,
                ["doors"] = new Func<IReadOnlyList<DoorDefinition>>(() => Array.Empty<DoorDefinition>()),
                ["tools"] = new Func<IReadOnlyList<ToolPickupDefinition>>(() => Array.Empty<ToolPickupDefinition>()),
                ["objectives"] = new Func<IReadOnlyList<ObjectiveDefinition>>(() => Array.Empty<ObjectiveDefinition>())
            }) typeof(OnlineGameplaySession).GetField(field.Key, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(session, field.Value);
            var encode = typeof(OnlineGameplaySession).GetMethod("EncodeBegin", BindingFlags.Static | BindingFlags.NonPublic);
            var decode = typeof(OnlineGameplaySession).GetMethod("TryBegin", BindingFlags.Instance | BindingFlags.NonPublic);
            var packet = (byte[])encode.Invoke(null, new object[] { config, actors });

            Assert.That(decode.Invoke(session, new object[] { packet, null, null }), Is.True,
                "A late waiting member is room-authenticated but is not part of the active round roster.");
        }
    }
}
