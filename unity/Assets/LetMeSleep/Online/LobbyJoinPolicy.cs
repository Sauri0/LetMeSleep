using System;
using LetMeSleep.Core;

namespace LetMeSleep.Online
{
    public enum LobbyCandidateRejection
    {
        None,
        InvalidDetails,
        SameDeviceIdentity,
        IncompatibleVersion,
        UnsafeLobbyConfiguration
    }

    /// <summary>Pure validation of details copied immediately after a join-by-ID succeeds.</summary>
    public static class LobbyJoinPolicy
    {
        public static LobbyCandidateRejection ValidateJoined(string localUserId, string ownerUserId, string bucketId,
            uint maxMembers, bool allowHostMigration, bool rtcRoomEnabled, bool allowJoinById)
        {
            if (string.IsNullOrWhiteSpace(localUserId) || string.IsNullOrWhiteSpace(ownerUserId))
                return LobbyCandidateRejection.InvalidDetails;
            if (string.Equals(localUserId, ownerUserId, StringComparison.Ordinal))
                return LobbyCandidateRejection.SameDeviceIdentity;
            if (!string.Equals(bucketId, RoomSession.Protocol, StringComparison.Ordinal))
                return LobbyCandidateRejection.IncompatibleVersion;
            if (maxMembers < 2 || maxMembers > RoomRules.Capacity)
                return LobbyCandidateRejection.UnsafeLobbyConfiguration;
            if (allowHostMigration || rtcRoomEnabled || !allowJoinById)
                return LobbyCandidateRejection.UnsafeLobbyConfiguration;
            return LobbyCandidateRejection.None;
        }
    }
}
