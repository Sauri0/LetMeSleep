using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace LetMeSleep.Core
{
    public enum RoomPhase { Waiting, Playing, Results, Closed }
    public enum PlayerRole { Unassigned, Human, Mosquito }
    public enum RoomError { None, Closed, NotOwner, WrongPhase, UnknownMember, DuplicateMember, InvalidMember, Full, IncompatibleVersion, InvalidRules, NotEnoughPlayers, NotReady }
    public interface IRandomSource { int Next(int exclusiveMax); }
    public sealed class SeededRandom : IRandomSource
    {
        private readonly Random random;
        public SeededRandom(int seed) { random = new Random(seed); }
        public int Next(int exclusiveMax) { return random.Next(exclusiveMax); }
    }

    public sealed class RoomRules
    {
        public const int Capacity = 16;
        public const string AlfaMap = "house-patio-v1";
        public int? HumanCount { get; }
        public int RoundSeconds { get; }
        public float BloodQuota { get; }
        public string MapId { get; }
        public RoomRules(int? humanCount = null, int roundSeconds = 180, float bloodQuota = 20, string mapId = AlfaMap)
        {
            HumanCount = humanCount; RoundSeconds = roundSeconds; BloodQuota = bloodQuota; MapId = mapId;
        }
        public bool IsValid => (!HumanCount.HasValue || (HumanCount.Value >= 1 && HumanCount.Value <= 5))
            && RoundSeconds >= 30 && RoundSeconds <= 1800 && BloodQuota > 0 && BloodQuota <= 1000
            && !float.IsNaN(BloodQuota) && !float.IsInfinity(BloodQuota) && MapId == AlfaMap;
    }

    public sealed class MemberView
    {
        public string Id { get; }
        public string Name { get; }
        public bool Ready { get; }
        public PlayerRole Role { get; }
        public MemberView(string id, string name, bool ready, PlayerRole role)
        { Id = id; Name = name; Ready = ready; Role = role; }
    }

    public sealed class RoomView
    {
        public long Revision { get; }
        public int Round { get; }
        public string OwnerId { get; }
        public RoomPhase Phase { get; }
        public RoomRules Rules { get; }
        public IReadOnlyList<MemberView> Members { get; }
        internal RoomView(long revision, int round, string ownerId, RoomPhase phase, RoomRules rules, MemberView[] members)
        { Revision = revision; Round = round; OwnerId = ownerId; Phase = phase; Rules = rules; Members = Array.AsReadOnly(members); }
    }

    // Host-owned domain state. The transport must bind sender IDs to authenticated peers.
    // Neither UI visibility nor the caller-supplied display name grants authority.
    public sealed class RoomSession
    {
        public const string Protocol = "lms-unity-094-alfa-1";
        private sealed class Member
        {
            internal string Id, Name;
            internal bool Ready;
            internal PlayerRole Role;
        }
        private readonly List<Member> members = new List<Member>();
        private readonly IRandomSource random;
        private readonly string ownerId;
        private RoomRules rules = new RoomRules();
        private RoomPhase phase = RoomPhase.Waiting;
        private long revision;
        private int round;
        public RoomSession(string ownerId, string ownerName, IRandomSource random)
        {
            if (!ValidIdentity(ownerId, ownerName)) throw new ArgumentException("Invalid owner.");
            this.random = random ?? throw new ArgumentNullException(nameof(random));
            this.ownerId = ownerId;
            members.Add(new Member { Id = ownerId, Name = ownerName.Trim() });
        }
        public RoomView Snapshot() => new RoomView(revision, round, ownerId, phase, rules,
            members.Select(m => new MemberView(m.Id, m.Name, m.Ready, m.Role)).ToArray());
        public RoomError Join(string id, string name, string protocol)
        {
            if (phase == RoomPhase.Closed) return RoomError.Closed;
            if (phase != RoomPhase.Waiting) return RoomError.WrongPhase;
            if (protocol != Protocol) return RoomError.IncompatibleVersion;
            if (!ValidIdentity(id, name)) return RoomError.InvalidMember;
            if (members.Any(m => m.Id == id)) return RoomError.DuplicateMember;
            if (members.Count >= RoomRules.Capacity) return RoomError.Full;
            members.Add(new Member { Id = id, Name = name.Trim() });
            revision++;
            return RoomError.None;
        }
        public RoomError SetReady(string senderId, bool ready)
        {
            if (phase == RoomPhase.Closed) return RoomError.Closed;
            if (phase != RoomPhase.Waiting) return RoomError.WrongPhase;
            var member = members.Find(m => m.Id == senderId);
            if (member == null) return RoomError.UnknownMember;
            if (member.Ready != ready) { member.Ready = ready; revision++; }
            return RoomError.None;
        }
        public RoomError ChangeRules(string senderId, RoomRules next)
        {
            var error = OwnerAction(senderId, RoomPhase.Waiting);
            if (error != RoomError.None) return error;
            if (next == null || !next.IsValid) return RoomError.InvalidRules;
            rules = next;
            // A ready acknowledgement applies only to the rules the player saw.
            foreach (var member in members) member.Ready = false;
            revision++;
            return RoomError.None;
        }
        public RoomError StartRound(string senderId)
        {
            var error = OwnerAction(senderId, RoomPhase.Waiting);
            if (error != RoomError.None) return error;
            if (members.Count < 2) return RoomError.NotEnoughPlayers;
            if (rules.HumanCount.HasValue && rules.HumanCount.Value >= members.Count) return RoomError.InvalidRules;
            if (members.Any(m => !m.Ready)) return RoomError.NotReady;
            var order = members.ToArray();
            for (int i = order.Length - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                var temp = order[i]; order[i] = order[j]; order[j] = temp;
            }
            int humans = rules.HumanCount ?? (1 + random.Next(Math.Min(5, order.Length - 1)));
            for (int i = 0; i < order.Length; i++)
                order[i].Role = i < humans ? PlayerRole.Human : PlayerRole.Mosquito;
            phase = RoomPhase.Playing; round++; revision++;
            return RoomError.None;
        }
        public RoomError FinishRound(string senderId)
        {
            var error = OwnerAction(senderId, RoomPhase.Playing);
            if (error != RoomError.None) return error;
            phase = RoomPhase.Results; revision++;
            return RoomError.None;
        }
        public RoomError ReturnToWaiting(string senderId)
        {
            var error = OwnerAction(senderId, RoomPhase.Results);
            if (error != RoomError.None) return error;
            foreach (var m in members) { m.Ready = false; m.Role = PlayerRole.Unassigned; }
            phase = RoomPhase.Waiting; revision++;
            return RoomError.None;
        }
        public RoomError Leave(string senderId)
        {
            if (phase == RoomPhase.Closed) return RoomError.Closed;
            var member = members.Find(m => m.Id == senderId);
            if (member == null) return RoomError.UnknownMember;
            members.Remove(member);
            if (senderId == ownerId) phase = RoomPhase.Closed;
            // Gameplay observes roster changes and decides the mode's resulting outcome.
            revision++;
            return RoomError.None;
        }
        private RoomError OwnerAction(string senderId, RoomPhase required)
        {
            if (phase == RoomPhase.Closed) return RoomError.Closed;
            if (senderId != ownerId) return RoomError.NotOwner;
            return phase == required ? RoomError.None : RoomError.WrongPhase;
        }
        private static bool ValidIdentity(string id, string name) =>
            !string.IsNullOrWhiteSpace(id) && id.Length <= 128 && !id.Any(char.IsControl) &&
            !string.IsNullOrWhiteSpace(name) && name.Trim().Length <= 24 && !name.Any(char.IsControl);
    }
}

