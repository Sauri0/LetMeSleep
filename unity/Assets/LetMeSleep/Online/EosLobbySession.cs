using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using Epic.OnlineServices;
using Epic.OnlineServices.Lobby;
using LetMeSleep.Core;

namespace LetMeSleep.Online
{
    public enum LobbyState { Idle, Creating, Joining, Connected, Leaving, Closed, Failed }

    /// <summary>EOS membership/discovery only. Gameplay rules remain in RoomSession.</summary>
    public sealed class EosLobbySession : IDisposable
    {
        private const string Prefix = "LMSU1-";
        private const string Alphabet = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ";
        private readonly EosConnection connection;
        private readonly LobbyInterface lobby;
        private readonly HashSet<string> members = new HashSet<string>(StringComparer.Ordinal);
        private readonly ulong memberNotification;
        private bool disposed;
        private bool membershipAcquired;
        private uint generation;
        private double deadline;
        private double clock;
        private bool clockStarted;
        public LobbyState State { get; private set; }
        public string Code { get; private set; } = "";
        public string LobbyId { get; private set; } = "";
        public string OwnerId { get; private set; } = "";
        public string ErrorCode { get; private set; } = "";
        public bool IsOwner => connection.LocalUserId != null && OwnerId == connection.LocalUserId.ToString();
        public event Action Changed;

        public EosLobbySession(EosConnection connection)
        {
            if (connection.State != ConnectionState.Ready) throw new InvalidOperationException("Sign in before opening a lobby.");
            this.connection = connection;
            lobby = connection.Platform.GetLobbyInterface();
            var notify = new AddNotifyLobbyMemberStatusReceivedOptions();
            memberNotification = lobby.AddNotifyLobbyMemberStatusReceived(ref notify, null, OnMemberChanged);
            connection.StateChanged += OnConnectionChanged;
        }

        public bool Contains(string authenticatedId) => members.Contains(authenticatedId);
        public string[] CaptureMembers() { var copy = new string[members.Count]; members.CopyTo(copy); Array.Sort(copy, StringComparer.Ordinal); return copy; }

        public void Create()
        {
            if (!CanBegin()) return;
            Code = GenerateCode();
            LobbyId = Prefix + Code;
            StartOperation(LobbyState.Creating);
            uint operation = generation;
            var options = new CreateLobbyOptions
            {
                LocalUserId = connection.LocalUserId, MaxLobbyMembers = RoomRules.Capacity,
                PermissionLevel = LobbyPermissionLevel.Joinviapresence,
                PresenceEnabled = false, AllowInvites = true, BucketId = RoomSession.Protocol,
                DisableHostMigration = true, EnableRTCRoom = false,
                LobbyId = LobbyId, EnableJoinById = true
            };
            lobby.CreateLobby(ref options, null, (ref CreateLobbyCallbackInfo info) =>
            {
                if (!IsCurrent(operation)) { if (info.ResultCode == Result.Success) CleanupLateLobby(info.LobbyId, true); return; }
                if (info.ResultCode != Result.Success) { Fail("Create_" + info.ResultCode); return; }
                LobbyId = info.LobbyId;
                membershipAcquired = true; OwnerId = connection.LocalUserId.ToString();
                if (!RefreshMembers()) { CleanupLateLobby(LobbyId, true); Fail("LobbyDetailsUnavailable"); return; }
                SetState(LobbyState.Connected);
            });
        }

        public void Join(string text)
        {
            if (!CanBegin()) return;
            if (!TryNormalizeCode(text, out var code)) { Fail("InvalidCode"); return; }
            Code = code; LobbyId = Prefix + code;
            StartOperation(LobbyState.Joining);
            uint operation = generation;
            var options = new JoinLobbyByIdOptions { LocalUserId = connection.LocalUserId, LobbyId = LobbyId, PresenceEnabled = false };
            lobby.JoinLobbyById(ref options, null, (ref JoinLobbyByIdCallbackInfo info) =>
            {
                if (info.ResultCode != Result.Success)
                {
                    if (IsCurrent(operation)) Fail("Join_" + info.ResultCode);
                    return;
                }
                if (IsCurrent(operation)) { CompleteJoinedLobby(info.LobbyId); return; }

                // A timed-out attempt may finish after the player retries the same code. Adopt that membership
                // and invalidate the newer duplicate request instead of leaving the lobby it is trying to join.
                if (!disposed && State == LobbyState.Joining && !membershipAcquired &&
                    string.Equals(LobbyId, info.LobbyId, StringComparison.Ordinal))
                { ++generation; CompleteJoinedLobby(info.LobbyId); return; }
                if (!disposed && State == LobbyState.Connected && membershipAcquired &&
                    string.Equals(LobbyId, info.LobbyId, StringComparison.Ordinal)) return;
                CleanupLateLobby(info.LobbyId, false);
            });
        }

        private void CompleteJoinedLobby(string joinedLobbyId)
        {
            LobbyId = joinedLobbyId;
            membershipAcquired = true;
            var rejection = ValidateJoinedLobby();
            if (rejection != LobbyCandidateRejection.None)
            {
                // A duplicate process using the owner's PUID must not leave/destroy the real owner's lobby.
                if (rejection != LobbyCandidateRejection.SameDeviceIdentity) CleanupLateLobby(LobbyId, false);
                Clear(); Fail(rejection.ToString()); return;
            }
            if (!RefreshMembers()) { CleanupLateLobby(LobbyId, false); Clear(); Fail("IncompatibleOrUnavailableLobby"); return; }
            SetState(LobbyState.Connected);
        }

        private LobbyCandidateRejection ValidateJoinedLobby()
        {
            var options = new CopyLobbyDetailsHandleOptions { LocalUserId = connection.LocalUserId, LobbyId = LobbyId };
            if (lobby.CopyLobbyDetailsHandle(ref options, out var details) != Result.Success || details == null)
                return LobbyCandidateRejection.InvalidDetails;
            try
            {
                var copy = new LobbyDetailsCopyInfoOptions();
                if (details.CopyInfo(ref copy, out var candidate) != Result.Success || !candidate.HasValue)
                    return LobbyCandidateRejection.InvalidDetails;
                var info = candidate.Value;
                if (!string.Equals(info.LobbyId?.ToString(), LobbyId, StringComparison.Ordinal))
                    return LobbyCandidateRejection.InvalidDetails;
                return LobbyJoinPolicy.ValidateJoined(connection.LocalUserId.ToString(), info.LobbyOwnerUserId?.ToString(),
                    info.BucketId?.ToString(), info.MaxMembers, info.AllowHostMigration, info.RTCRoomEnabled, info.AllowJoinById);
            }
            finally { details.Release(); }
        }

        public void Tick(double monotonicSeconds)
        {
            if (!clockStarted) { if (deadline > 0) deadline += monotonicSeconds; clockStarted = true; }
            clock = monotonicSeconds;
            if ((State == LobbyState.Joining || State == LobbyState.Creating || State == LobbyState.Leaving) && clock >= deadline)
            { ++generation; Fail("LobbyTimedOut"); }
        }

        public void Leave()
        {
            if (disposed) return;
            bool wasConnected = membershipAcquired;
            ++generation;
            if (!wasConnected) { Clear(); SetState(LobbyState.Idle); return; }
            StartOperation(LobbyState.Leaving);
            uint operation = generation;
            if (IsOwner)
            {
                var options = new DestroyLobbyOptions { LocalUserId = connection.LocalUserId, LobbyId = LobbyId };
                lobby.DestroyLobby(ref options, null, (ref DestroyLobbyCallbackInfo info) =>
                { if (IsCurrent(operation)) FinishLeave(info.ResultCode); });
            }
            else
            {
                var options = new LeaveLobbyOptions { LocalUserId = connection.LocalUserId, LobbyId = LobbyId };
                lobby.LeaveLobby(ref options, null, (ref LeaveLobbyCallbackInfo info) =>
                { if (IsCurrent(operation)) FinishLeave(info.ResultCode); });
            }
        }

        private void FinishLeave(Result result)
        {
            if (result == Result.Success || result == Result.NotFound) { Clear(); SetState(LobbyState.Idle); }
            else Fail("Leave_" + result);
        }

        private bool RefreshMembers()
        {
            var options = new CopyLobbyDetailsHandleOptions { LocalUserId = connection.LocalUserId, LobbyId = LobbyId };
            if (lobby.CopyLobbyDetailsHandle(ref options, out var details) != Result.Success) return false;
            try
            {
                var copy = new LobbyDetailsCopyInfoOptions();
                if (details.CopyInfo(ref copy, out var info) != Result.Success || !info.HasValue || info.Value.BucketId != RoomSession.Protocol) return false;
                if (info.Value.MaxMembers > RoomRules.Capacity || info.Value.AllowHostMigration || info.Value.RTCRoomEnabled || !info.Value.AllowJoinById) return false;
                OwnerId = info.Value.LobbyOwnerUserId.ToString();
                members.Clear();
                var countOptions = new LobbyDetailsGetMemberCountOptions();
                uint count = details.GetMemberCount(ref countOptions);
                if (count > RoomRules.Capacity) return false;
                for (uint i = 0; i < count && i < RoomRules.Capacity; i++)
                {
                    var member = new LobbyDetailsGetMemberByIndexOptions { MemberIndex = i };
                    var id = details.GetMemberByIndex(ref member);
                    if (id != null) members.Add(id.ToString());
                }
                return members.Contains(connection.LocalUserId.ToString()) && members.Contains(OwnerId);
            }
            finally { details.Release(); }
        }

        private void OnMemberChanged(ref LobbyMemberStatusReceivedCallbackInfo info)
        {
            if (disposed || info.LobbyId != LobbyId || State != LobbyState.Connected) return;
            if (info.CurrentStatus == LobbyMemberStatus.Closed ||
                (info.TargetUserId.ToString() == OwnerId && info.CurrentStatus != LobbyMemberStatus.Joined))
            { Clear(); SetState(LobbyState.Closed); return; }
            if (!RefreshMembers()) { Clear(); SetState(LobbyState.Closed); return; }
            Changed?.Invoke();
        }

        private void CleanupLateLobby(string id, bool owner)
        {
            if (connection.Platform == null) return;
            if (owner)
            {
                var options = new DestroyLobbyOptions { LocalUserId = connection.LocalUserId, LobbyId = id };
                lobby.DestroyLobby(ref options, null, (ref DestroyLobbyCallbackInfo _) => { });
            }
            else
            {
                var options = new LeaveLobbyOptions { LocalUserId = connection.LocalUserId, LobbyId = id };
                lobby.LeaveLobby(ref options, null, (ref LeaveLobbyCallbackInfo _) => { });
            }
        }

        private void OnConnectionChanged(ConnectionState state)
        {
            if (state == ConnectionState.Disposed) { Dispose(); return; }
            if (state == ConnectionState.Failed) { ++generation; Fail("AuthenticationLost"); }
        }
        private bool CanBegin() => !disposed && !membershipAcquired && connection.State == ConnectionState.Ready &&
            (State == LobbyState.Idle || State == LobbyState.Closed || State == LobbyState.Failed);
        private bool IsCurrent(uint operation) => !disposed && operation == generation;
        private void StartOperation(LobbyState state) { ++generation; deadline = clock + 25; ErrorCode = ""; SetState(state); }
        private void SetState(LobbyState state) { State = state; Changed?.Invoke(); }
        private void Fail(string error) { ErrorCode = error; SetState(LobbyState.Failed); }
        private void Clear() { membershipAcquired = false; members.Clear(); OwnerId = LobbyId = Code = ""; }

        public static bool TryNormalizeCode(string text, out string code)
        {
            code = (text ?? "").Trim().Replace(" ", "").Replace("-", "").ToUpperInvariant();
            if (code.Length != 10) return false;
            foreach (char character in code) if (Alphabet.IndexOf(character) < 0) return false;
            return true;
        }
        private static string GenerateCode()
        {
            var bytes = new byte[10];
            using (var random = RandomNumberGenerator.Create()) random.GetBytes(bytes);
            var chars = new char[10];
            for (int i = 0; i < chars.Length; i++) chars[i] = Alphabet[bytes[i] % Alphabet.Length];
            return new string(chars);
        }
        public void Dispose()
        {
            if (disposed) return;
            if (membershipAcquired) CleanupLateLobby(LobbyId, IsOwner);
            disposed = true; ++generation;
            connection.StateChanged -= OnConnectionChanged;
            lobby.RemoveNotifyLobbyMemberStatusReceived(memberNotification);
            Clear(); Changed = null;
        }
    }
}
