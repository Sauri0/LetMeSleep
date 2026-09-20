using System;
using System.Linq;
using System.Reflection;
using LetMeSleep.Bootstrap;
using LetMeSleep.Core;
using LetMeSleep.Core.Customization;
using LetMeSleep.UI;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LetMeSleep.Tests.PlayMode
{
    public sealed class AppearancePeerStateTests
    {
        private const string Code = "SLEEP-24";
        private const string Peer = "peer";

        [Test]
        public void ModularPacketsRequireCurrentConnectedMembershipAndVisualAvailability()
        {
            var room = Room();
            var catalog = Catalog();
            var packet = ModularPacket(catalog, catalog.DefaultSelection());
            var state = State();
            Assert.That(state.TryReceive("outsider", 2, packet, 1, room.Snapshot(), Code, catalog, _ => true), Is.False);
            Assert.That(state.TryReceive(Peer, 3, packet, 1, room.Snapshot(), Code, catalog, _ => true), Is.False);
            Assert.That(state.TryReceive(Peer, 2, packet, 1, room.Snapshot(), Code, null, _ => true), Is.False);
            Assert.That(state.TryReceive(Peer, 2, packet, 1, room.Snapshot(), Code, catalog, _ => false), Is.False);
            Assert.That(state.TryReceive(Peer, 2, packet, 1, room.Snapshot(), Code, catalog, _ => true), Is.True);

            room.SetReady("owner", true); room.SetReady(Peer, true);
            Assert.That(room.StartRound("owner"), Is.EqualTo(RoomError.None));
            room.Disconnect(Peer, 2);
            Assert.That(room.Snapshot().Members.Single(member => member.Id == Peer).Connected, Is.False);
            Assert.That(state.TryReceive(Peer, 2, packet, 2, room.Snapshot(), Code, catalog, _ => true), Is.False);
            Assert.That(state.TryGetModular(Peer, catalog.NetworkFingerprint, out _), Is.True,
                "The reserved disconnected actor retains the last valid appearance without accepting new input.");
        }

        [Test]
        public void RejectedPacketDoesNotReplaceSelectionOrConsumeTheNextAcceptanceTime()
        {
            var room = Room().Snapshot();
            var catalog = Catalog();
            var original = catalog.DefaultSelection();
            var changed = original.Copy(); changed.Human.SetOption("human.base", "human-b");
            var state = State();
            Assert.That(state.TryReceive(Peer, 2, ModularPacket(catalog, original), 10, room, Code, catalog, _ => true), Is.True);
            Assert.That(state.TryReceive(Peer, 2, ModularPacket(catalog, changed), 10.1, room, Code, catalog, _ => true), Is.False);
            Assert.That(state.TryReceive(Peer, 2, ModularPacket(catalog, changed), 11, room, Code, catalog, _ => false), Is.False);
            Assert.That(state.TryGetModular(Peer, catalog.NetworkFingerprint, out var kept), Is.True);
            Assert.That(kept.CanonicalEquals(original), Is.True);
            Assert.That(state.TryReceive(Peer, 2, ModularPacket(catalog, changed), 11, room, Code, catalog, _ => true), Is.True);
            Assert.That(state.TryGetModular(Peer, catalog.NetworkFingerprint, out var accepted), Is.True);
            accepted.Human.SetOption("human.base", "human-a");
            state.TryGetModular(Peer, catalog.NetworkFingerprint, out var owned);
            Assert.That(owned.CanonicalEquals(changed), Is.True, "Consumers cannot mutate stored peer state.");
        }

        [Test]
        public void WrongRoomFingerprintMalformedAndLegacyDowngradePreserveAcceptedModularState()
        {
            var room = Room().Snapshot();
            var catalog = Catalog();
            var selection = catalog.DefaultSelection();
            var packet = ModularPacket(catalog, selection);
            var state = State();
            Assert.That(state.TryReceive(Peer, 2, packet, 1, room, Code, catalog, _ => true), Is.True);
            var wrongRoom = ModularPacket(catalog, selection, "OTHER-24");
            var wrongFingerprint = packet.ToArray(); wrongFingerprint[3 + Code.Length] ^= 0x80;
            var trailing = packet.Concat(new byte[] { 0 }).ToArray();
            var legacy = LegacyPacket();
            foreach (var rejected in new[] { wrongRoom, new ArraySegment<byte>(wrongFingerprint), new ArraySegment<byte>(trailing), legacy,
                default(ArraySegment<byte>), new ArraySegment<byte>(new byte[129]) })
                Assert.That(state.TryReceive(Peer, 2, rejected, 2, room, Code, catalog, _ => true), Is.False);
            Assert.That(state.TryReceive(Peer, 2, packet, double.NaN, room, Code, catalog, _ => true), Is.False);
            Assert.That(state.TryGetModular(Peer, catalog.NetworkFingerprint, out var kept), Is.True);
            Assert.That(kept.CanonicalEquals(selection), Is.True);
            Assert.That(state.TryGetLegacy(Peer, out _), Is.False);
        }

        [Test]
        public void LegacyCompatibilityChecksPayloadAndSegmentOffsetBeforeStoring()
        {
            var room = Room().Snapshot();
            var state = State();
            var packet = LegacyPacket().ToArray();
            var envelope = new byte[packet.Length + 8]; Array.Copy(packet, 0, envelope, 5, packet.Length);
            Assert.That(state.TryReceive(Peer, 2, new ArraySegment<byte>(envelope, 5, packet.Length), 1,
                room, Code, null, null), Is.True);
            Assert.That(state.TryReceive(Peer, 2, LegacyPacket("unknown"), 2, room, Code, null, null), Is.False);
            Assert.That(state.TryReceive(Peer, 2, new ArraySegment<byte>(packet.Concat(new byte[] { 0 }).ToArray()), 2,
                room, Code, null, null), Is.False);
            Assert.That(state.TryGetLegacy(Peer, out var kept), Is.True);
            Assert.That(kept.SkinColorId, Is.EqualTo("warm"));
            kept.SkinColorId = "mutated";
            state.TryGetLegacy(Peer, out var again);
            Assert.That(again.SkinColorId, Is.EqualTo("warm"));
        }

        [Test]
        public void RoomChangesDeparturesAndExplicitResetRemoveOldPeerState()
        {
            var room = Room();
            var state = State();
            Assert.That(state.TryReceive(Peer, 2, LegacyPacket(), 1, room.Snapshot(), Code, null, null), Is.True);
            room.Disconnect(Peer, 2);
            state.Synchronize(room.Snapshot(), Code);
            Assert.That(state.TryGetLegacy(Peer, out _), Is.False);
            room.Join(Peer, "Peer", RoomSession.Protocol);
            Assert.That(state.TryReceive(Peer, 2, LegacyPacket(), 3, room.Snapshot(), Code, null, null), Is.True);
            state.Synchronize(room.Snapshot(), "NEW-ROOM");
            Assert.That(state.TryGetLegacy(Peer, out _), Is.False);
            Assert.That(state.TryReceive(Peer, 2, LegacyPacket(), 4, room.Snapshot(), Code, null, null), Is.True);
            state.Clear();
            Assert.That(state.TryGetLegacy(Peer, out _), Is.False);
            Assert.That(state.TryReceive(Peer, 2, LegacyPacket(), 5, null, Code, null, null), Is.False);
        }

        [Test]
        public void ApplicationSendsPublishedLegacyValuesAndNeverFallbackDefaultsForRetainedModularData()
        {
            var root = new GameObject("Inactive appearance packet fixture");
            root.SetActive(false);
            try
            {
                var app = root.AddComponent<AlfaApplication>();
                Set(app, "appearance", new BasicCustomizationDraft(AlfaRole.Human, "warm", "blue", "red"));
                Set(app, "localAppearanceDraft", new BasicCustomizationDraft(AlfaRole.Mosquito, "dark", "purple", "green"));
                object[] args = { Code, null };
                var build = typeof(AlfaApplication).GetMethod("TryBuildAppearancePacket", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(build, Is.Not.Null);
                Assert.That((bool)build.Invoke(app, args), Is.True);
                var state = State();
                Assert.That(state.TryReceive(Peer, 2, new ArraySegment<byte>((byte[])args[1]), 1, Room().Snapshot(), Code, null, null), Is.True);
                state.TryGetLegacy(Peer, out var sent);
                Assert.That(sent.SkinColorId, Is.EqualTo("warm"));
                Assert.That(sent.PajamaColorId, Is.EqualTo("blue"));
                Assert.That(sent.MosquitoColorId, Is.EqualTo("red"));

                Set(app, "loadedPreferenceSchema", 2);
                Assert.That((bool)build.Invoke(app, args), Is.False);
                Assert.That(args[1], Is.Null);
                Set(app, "loadedPreferenceSchema", 1);
                Set(app, "preferenceWritesBlocked", true);
                Assert.That((bool)build.Invoke(app, args), Is.False);
                Assert.That(args[1], Is.Null);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        private static RoomSession Room()
        {
            var room = new RoomSession("owner", "Owner", new SeededRandom(1));
            Assert.That(room.Join(Peer, "Peer", RoomSession.Protocol), Is.EqualTo(RoomError.None));
            return room;
        }

        private static AppearancePeerState State() => new AppearancePeerState(value =>
            value != null && value.SkinColorId == "warm" && value.PajamaColorId == "blue" && value.MosquitoColorId == "red");

        private static ArraySegment<byte> LegacyPacket(string skin = "warm")
        {
            Assert.That(AppearancePeerState.TryEncodeLegacy(Code, new BasicCustomizationDraft(AlfaRole.Human, skin, "blue", "red"), out var data), Is.True);
            return new ArraySegment<byte>(data);
        }

        private static ArraySegment<byte> ModularPacket(CustomizationCatalogSnapshot catalog, AppearanceSelection selection, string room = Code)
        {
            Assert.That(AppearanceWireCodec.TryEncode(catalog, room, selection, out var data), Is.True);
            return new ArraySegment<byte>(data);
        }

        private static CustomizationCatalogSnapshot Catalog()
        {
            var slots = new[] {
                new CustomizationSlotRecord { Role=CustomizationRole.Human, SlotId="human.base", WireSlotId=1, Required=true, IsBaseSlot=true, DefaultOptionId="human-a" },
                new CustomizationSlotRecord { Role=CustomizationRole.Mosquito, SlotId="mosquito.base", WireSlotId=2, Required=true, IsBaseSlot=true, DefaultOptionId="mosquito-a" }
            };
            var options = new[] {
                Visual(CustomizationRole.Human, "human.base", "human-a", 1), Visual(CustomizationRole.Human, "human.base", "human-b", 2),
                Visual(CustomizationRole.Mosquito, "mosquito.base", "mosquito-a", 1)
            };
            Assert.That(CustomizationCatalogSnapshot.TryCreate("lms.peer.fixture", 1, slots, options, out var catalog, out var errors), Is.True,
                string.Join("; ", errors));
            return catalog;
        }

        private static CustomizationOptionRecord Visual(CustomizationRole role, string slot, string option, ushort wire) =>
            new CustomizationOptionRecord { Role=role, SlotId=slot, OptionId=option, WireOptionId=wire,
                Kind=CustomizationOptionKind.SkinnedPart, AssetId="synthetic-" + option, HasRuntimeAsset=true };
    }
}
