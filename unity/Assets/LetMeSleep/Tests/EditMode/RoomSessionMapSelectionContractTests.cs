using System;
using System.Linq;
using LetMeSleep.Core;
using LetMeSleep.Gameplay;
using LetMeSleep.Online;
using NUnit.Framework;
using static LetMeSleep.Tests.EditMode.RoomSessionTestSupport;

namespace LetMeSleep.Tests.EditMode
{
    public sealed class MapSelectionContractTests
    {
        [Test]
        public void AlfaMapRemainsDefaultAndCatalogIsReadOnlyAndExact()
        {
            Assert.That(new RoomRules().MapId, Is.EqualTo(RoomRules.AlfaMap));
            Assert.That(RoomRules.SupportedMapIds, Is.EqualTo(new[]
            {
                RoomRules.AlfaMap,
                RoomRules.IslaDelLaguitoMap,
                RoomRules.CasaDelPatioMap,
                RoomRules.CampamentoPinarMap,
                RoomRules.YateALaDerivaMap,
                RoomRules.PuertoDelFaroMap
            }));
            Assert.That(RoomRules.SupportedMapIds.Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(6));
            Assert.That(RoomRules.SupportedMapIds, Is.InstanceOf<System.Collections.ObjectModel.ReadOnlyCollection<string>>());
        }

        [TestCase(RoomRules.AlfaMap)]
        [TestCase(RoomRules.IslaDelLaguitoMap)]
        [TestCase(RoomRules.CasaDelPatioMap)]
        [TestCase(RoomRules.CampamentoPinarMap)]
        [TestCase(RoomRules.YateALaDerivaMap)]
        [TestCase(RoomRules.PuertoDelFaroMap)]
        public void OwnerRuleChangeAndRoomSnapshotRoundTripPreserveSupportedMap(string mapId)
        {
            var session = TwoPlayerSession();
            ReadyEveryone(session);
            var before = session.Snapshot();
            var rules = new RoomRules(1, 240, 25, mapId);

            Assert.That(rules.IsValid, Is.True);
            Assert.That(session.ChangeRules("guest-puid", rules), Is.EqualTo(RoomError.NotOwner));
            Assert.That(session.Snapshot().Revision, Is.EqualTo(before.Revision));
            Assert.That(session.ChangeRules("owner-puid", rules), Is.EqualTo(RoomError.None));

            var selected = session.Snapshot();
            Assert.That(selected.Rules.MapId, Is.EqualTo(mapId));
            Assert.That(selected.Members.All(member => !member.Ready), Is.True);
            Assert.That(RoomWireCodec.TryDecode(RoomWireCodec.Encode(selected), "owner-puid", out var decoded), Is.True);
            Assert.That(decoded.Rules.MapId, Is.EqualTo(mapId));
        }

        [TestCase("hf-isla-del-laguito")]
        [TestCase("hf-isla-del-laguito-v1")]
        [TestCase("hf-casa-del-patio")]
        [TestCase("hf-campamento-pinar-v")]
        [TestCase("hf-yate-a-la-deriva-v2")]
        [TestCase("hf-puerto-del-faro-v1-preview")]
        [TestCase("HF-PUERTO-DEL-FARO-V1")]
        [TestCase(" hf-puerto-del-faro-v1")]
        [TestCase("future-map")]
        public void ArbitraryIncompleteOrWrongVersionMapIsRejected(string mapId)
        {
            Assert.That(RoomRules.IsSupportedMapId(mapId), Is.False);
            Assert.That(new RoomRules(mapId: mapId).IsValid, Is.False);

            var session = TwoPlayerSession();
            var before = session.Snapshot();
            Assert.That(session.ChangeRules("owner-puid", new RoomRules(mapId: mapId)), Is.EqualTo(RoomError.InvalidRules));
            Assert.That(session.Snapshot().Revision, Is.EqualTo(before.Revision));
            Assert.That(session.Snapshot().Rules.MapId, Is.EqualTo(RoomRules.AlfaMap));
        }

        [Test]
        public void NullAndEmptyMapIdsAreRejected()
        {
            Assert.That(RoomRules.IsSupportedMapId(null), Is.False);
            Assert.That(RoomRules.IsSupportedMapId(""), Is.False);
            Assert.That(new RoomRules(mapId: null).IsValid, Is.False);
            Assert.That(new RoomRules(mapId: "").IsValid, Is.False);
        }

        [TestCase(RoomRules.AlfaMap)]
        [TestCase(RoomRules.IslaDelLaguitoMap)]
        [TestCase(RoomRules.CasaDelPatioMap)]
        [TestCase(RoomRules.CampamentoPinarMap)]
        [TestCase(RoomRules.YateALaDerivaMap)]
        [TestCase(RoomRules.PuertoDelFaroMap)]
        public void GameplaySnapshotRoundTripPreservesSupportedMap(string mapId)
        {
            var config = new GameplayRoundConfig(7, 3, mapId, "content-hash", 30);
            var original = new GameSessionState(config, config.RoundDurationTicks, SimulationPhase.Ended, 0,
                RoundEndReason.TimeExpired, PlayerRole.Human, Array.Empty<ActorSnapshot>(), Array.Empty<DoorSnapshot>());

            var packet = GameplayWireCodec.Encode(original);
            Assert.That(GameplayWireCodec.TryDecode(packet, out GameSessionState decoded), Is.True);
            Assert.That(decoded.MapId, Is.EqualTo(mapId));
            Assert.That(decoded.SessionEpoch, Is.EqualTo(original.SessionEpoch));
            Assert.That(decoded.RoundId, Is.EqualTo(original.RoundId));
        }
    }
}
