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
        LobbyFull,
        UnsafeLobbyConfiguration
    }

    /// <summary>Pure pre-join validation for EOS search results.</summary>
    public static class LobbyJoinPolicy
    {
        public static LobbyCandidateRejection Validate(string localUserId, string ownerUserId, string bucketId,
            uint maxMembers, uint availableSlots, bool allowHostMigration, bool rtcRoomEnabled, bool allowJoinById)
        {
            if (string.IsNullOrWhiteSpace(localUserId) || string.IsNullOrWhiteSpace(ownerUserId))
                return LobbyCandidateRejection.InvalidDetails;
            if (string.Equals(localUserId, ownerUserId, StringComparison.Ordinal))
                return LobbyCandidateRejection.SameDeviceIdentity;
            if (!string.Equals(bucketId, RoomSession.Protocol, StringComparison.Ordinal))
                return LobbyCandidateRejection.IncompatibleVersion;
            if (maxMembers < 2 || maxMembers > RoomRules.Capacity)
                return LobbyCandidateRejection.UnsafeLobbyConfiguration;
            if (availableSlots == 0)
                return LobbyCandidateRejection.LobbyFull;
            if (allowHostMigration || rtcRoomEnabled || !allowJoinById)
                return LobbyCandidateRejection.UnsafeLobbyConfiguration;
            return LobbyCandidateRejection.None;
        }
    }
}
