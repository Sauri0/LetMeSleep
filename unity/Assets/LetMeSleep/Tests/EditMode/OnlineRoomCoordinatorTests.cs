using System;
using System.Collections.Generic;
using System.Linq;
using LetMeSleep.Core;
using LetMeSleep.Online;
using NUnit.Framework;

namespace LetMeSleep.Tests.EditMode
{
    // Room coordinator over an in-memory link. EOS flushes reliable packets when a P2P link closes, so the
    // coordinator itself must resynchronize the room view once the link is back.
    public sealed class OnlineRoomCoordinatorTests
    {
        private const string HostId = "host-puid", GuestId = "guest-puid";

        private sealed class FakeLobby : IRoomLobby
        {
            public readonly HashSet<string> Members = new HashSet<string>(StringComparer.Ordinal);
            public LobbyState State { get; set; } = LobbyState.Connected;
            public bool IsOwner => OwnerId == LocalMemberId;
            public string OwnerId { get; set; }
            public string LocalMemberId { get; set; }
            public bool Contains(string memberId) => memberId != null && Members.Contains(memberId);
            public event Action Changed;
            public void Raise() => Changed?.Invoke();
        }

        private sealed class Network
        {
            public readonly Dictionary<string, Link> Links = new Dictionary<string, Link>(StringComparer.Ordinal);
            public readonly Queue<(string From, string To, byte Channel, byte[] Data)> InFlight = new Queue<(string, string, byte, byte[])>();
            public bool Open = true;
            public int DropNext, Dropped;
            public void Flush()
            {
                for (int guard = 0; guard < 1000 && InFlight.Count > 0; guard++)
                {
                    var packet = InFlight.Dequeue();
                    Links[packet.To].Receive(packet.From, packet.Channel, packet.Data);
                }
            }
        }

        private sealed class Link : IRoomLink
        {
            private readonly Network network;
            private readonly string owner;
            public int Sent;
            public Link(Network network, string owner) { this.network = network; this.owner = owner; network.Links.Add(owner, this); }
            public void SendPacket(string memberId, byte channel, ArraySegment<byte> data, bool reliable)
            {
                Sent++;
                // A closed link reports NoConnection and loses the packet, like EOS.
                if (!network.Open || network.DropNext > 0) { if (network.DropNext > 0) network.DropNext--; network.Dropped++; return; }
                network.InFlight.Enqueue((owner, memberId, channel, data.ToArray()));
            }
            public event Action<string, byte, ArraySegment<byte>> PacketReceived;
            public event Action<string, string> PeerStateChanged;
            public void Receive(string from, byte channel, byte[] data) => PacketReceived?.Invoke(from, channel, new ArraySegment<byte>(data));
            public void Route(string peer, string state) => PeerStateChanged?.Invoke(peer, state);
        }

        private Network network;
        private Link hostLink, guestLink;
        private FakeLobby hostLobby, guestLobby;
        private OnlineRoomCoordinator host, guest;
        private double now;

        [SetUp]
        public void SetUp()
        {
            network = new Network();
            hostLink = new Link(network, HostId); guestLink = new Link(network, GuestId);
            hostLobby = new FakeLobby { OwnerId = HostId, LocalMemberId = HostId };
            guestLobby = new FakeLobby { OwnerId = HostId, LocalMemberId = GuestId };
            foreach (var lobby in new[] { hostLobby, guestLobby }) { lobby.Members.Add(HostId); lobby.Members.Add(GuestId); }
            host = new OnlineRoomCoordinator(hostLobby, hostLink, "Anfitrión");
            guest = new OnlineRoomCoordinator(guestLobby, guestLink, "Invitada");
            hostLobby.Raise();
            now = 10;
            Step(.1);
            Assert.That(guest.Current, Is.Not.Null, "The Hello handshake completes over an open link.");
            Assert.That(host.Current.Members.Select(member => member.Id), Is.EquivalentTo(new[] { HostId, GuestId }));
        }

        [TearDown]
        public void TearDown() { host?.Dispose(); guest?.Dispose(); }

        private void Step(double seconds)
        {
            now += seconds;
            host.Tick(now); guest.Tick(now);
            network.Flush();
        }

        private void DropLink()
        {
            network.Open = false; network.InFlight.Clear();
            hostLink.Route(GuestId, "Closed:TimedOut"); guestLink.Route(HostId, "Closed:TimedOut");
        }

        private void StartRoundWhileTheLinkIsDown()
        {
            Assert.That(guest.SetReady(true), Is.EqualTo(RoomError.None));
            Step(.1);
            Assert.That(host.SetReady(true), Is.EqualTo(RoomError.None));
            network.Flush();
            DropLink();
            Assert.That(host.StartRound(), Is.EqualTo(RoomError.None));
            network.Open = true;
            Assert.That(host.Current.Phase, Is.EqualTo(RoomPhase.Playing));
            Assert.That(guest.Current.Phase, Is.EqualTo(RoomPhase.Waiting), "The Playing view was flushed with the closed link.");
        }

        [Test]
        public void HostResendsItsViewWhenTheLinkToAMemberIsReestablished()
        {
            StartRoundWhileTheLinkIsDown();
            hostLink.Route(GuestId, "DirectConnection");
            network.Flush();
            Assert.That(guest.Current.Phase, Is.EqualTo(RoomPhase.Playing), "The guest loads the round instead of timing out.");
            Assert.That(guest.Current.Revision, Is.EqualTo(host.Current.Revision));
        }

        [Test]
        public void GuestAsksForTheViewWhenItsLinkToTheOwnerIsReestablished()
        {
            StartRoundWhileTheLinkIsDown();
            guestLink.Route(HostId, "RelayedConnection");
            network.Flush();
            Assert.That(guest.Current.Phase, Is.EqualTo(RoomPhase.Playing));
            Assert.That(guest.Error, Is.Empty);
        }

        [Test]
        public void JoinedGuestRecoversAViewDroppedWithoutAnyLinkEvent()
        {
            network.DropNext = 1;
            Assert.That(host.SetReady(true), Is.EqualTo(RoomError.None));
            Assert.That(network.Dropped, Is.EqualTo(1));
            long published = host.Current.Revision;
            Step(1);
            Assert.That(guest.Current.Revision, Is.LessThan(published));
            Step(OnlineRoomCoordinator.ViewRefreshSeconds);
            Assert.That(guest.Current.Revision, Is.EqualTo(published), "A slow Hello resync returns the current view.");
            Assert.That(host.Current.Members.Count, Is.EqualTo(2), "A resync Hello from a known member changes nothing.");
        }

        [Test]
        public void ResyncOnlyAnswersTheAskingMemberAndDoesNotChangeTheRoom()
        {
            long revision = host.Current.Revision;
            int before = hostLink.Sent;
            guestLink.Route(HostId, "DirectConnection");
            network.Flush();
            Assert.That(host.Current.Revision, Is.EqualTo(revision));
            Assert.That(hostLink.Sent - before, Is.EqualTo(1), "One view back to the asking member, no room broadcast.");
        }

        [Test]
        public void ClosedLinksAndStrangersNeverTriggerTraffic()
        {
            int hostSent = hostLink.Sent, guestSent = guestLink.Sent;
            hostLink.Route(GuestId, "Closed:TimedOut");
            guestLink.Route(HostId, "Closed:TimedOut");
            hostLink.Route("stranger-puid", "DirectConnection");
            guestLink.Route("stranger-puid", "DirectConnection");
            Assert.That(hostLink.Sent, Is.EqualTo(hostSent));
            Assert.That(guestLink.Sent, Is.EqualTo(guestSent));
        }

        [Test]
        public void DisposedCoordinatorIgnoresLinkEvents()
        {
            host.Dispose(); guest.Dispose();
            int hostSent = hostLink.Sent, guestSent = guestLink.Sent;
            hostLink.Route(GuestId, "DirectConnection");
            guestLink.Route(HostId, "DirectConnection");
            Assert.That(hostLink.Sent, Is.EqualTo(hostSent));
            Assert.That(guestLink.Sent, Is.EqualTo(guestSent));
        }
    }
}
