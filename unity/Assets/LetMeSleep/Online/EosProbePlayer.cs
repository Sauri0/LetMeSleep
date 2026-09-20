#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using System.Linq;
using System.Text;
using LetMeSleep.Core;
using UnityEngine;

namespace LetMeSleep.Online
{
    /// <summary>Development-build connectivity check. Never active in a normal player launch.</summary>
    public sealed class EosProbePlayer : MonoBehaviour
    {
        private EosConnection connection;
        private EosLobbySession lobby;
        private EosPeerTransport transport;
        private OnlineRoomCoordinator room;
        private EosProbeLifecycle lifecycle;
        private string role, invitePath, receiptPath, cachePath;
        private bool startedRoom, finished;
        private bool hostHelloReceived, guestAckReceived, hostConfirmationReceived;
        private bool ownerCloseRequested;
        private double startTime, lastSend, nextAction;
        private string networkType = "NotEstablished";
        private string closeReason = "";
        private string assignedRole = "";
        private int received;
        [Serializable] private sealed class Invitation { public string code, owner; }
        [Serializable] private sealed class RoundReceipt
        {
            public int round;
            public bool started, resultObserved, returnedToLobby;
            public bool remotePlayingAcknowledged, remoteResultsAcknowledged, remoteReturnAcknowledged;
        }
        [Serializable] private sealed class Receipt
        {
            public string schema = "lms-eos-unity-probe-3", role, result, networkType, closeReason, unityVersion;
            public int received, requiredRounds, completedRounds, remotelyConfirmedRounds;
            public string assignedRole;
            public bool roomHandshake, bothReady, roundStarted, resultObserved, returnedToLobby;
            public bool ownerCloseRequested, ownerCloseObserved;
            public bool wanVerified = false;
            public RoundReceipt[] rounds;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            var args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "--lms-eos-probe") < 0) return;
            var go = new GameObject("EOS connectivity check");
            DontDestroyOnLoad(go);
            go.AddComponent<EosProbePlayer>();
        }
        private static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, name);
            if (index < 0 || index + 1 >= args.Length) throw new ArgumentException("Missing probe argument " + name);
            return args[index + 1];
        }
        private void Start()
        {
            try
            {
                role = Arg("--role"); invitePath = Arg("--invitation"); receiptPath = Arg("--receipt"); cachePath = Arg("--cache");
                if (role != "host" && role != "guest") throw new ArgumentException("Probe role must be host or guest.");
                startTime = Time.realtimeSinceStartupAsDouble;
                connection = new EosConnection();
                connection.Initialize(EosConfiguration.Load(Arg("--configuration")), "Unity " + role, cachePath);
            }
            catch (Exception e) { Finish("Setup_" + e.GetType().Name); }
        }
        private void Update()
        {
            if (finished || connection == null) return;
            double now = Time.realtimeSinceStartupAsDouble;
            if (now - startTime > 120) { Finish("ProbeTimedOut"); return; }
            connection.Tick(now);
            if (connection.State == ConnectionState.Failed) { Finish(connection.FailureCode); return; }
            if (connection.State == ConnectionState.Ready && !startedRoom)
            {
                if (role != "host" && !File.Exists(invitePath)) return;
                startedRoom = true;
                lobby = new EosLobbySession(connection);
                transport = new EosPeerTransport(connection, lobby, true);
                room = new OnlineRoomCoordinator(connection, lobby, transport, "Unity " + role);
                lifecycle = new EosProbeLifecycle(role == "host");
                transport.PeerStateChanged += (_, state) =>
                {
                    if (state.StartsWith("Closed:", StringComparison.Ordinal)) closeReason = state.Substring("Closed:".Length);
                    else networkType = state;
                };
                transport.PacketReceived += Receive;
                if (role == "host") lobby.Create();
                else
                {
                    Invitation invitation;
                    try { invitation = JsonUtility.FromJson<Invitation>(File.ReadAllText(invitePath)); }
                    catch (IOException) { Finish("InvitationReadFailed"); return; }
                    if (invitation == null || string.IsNullOrWhiteSpace(invitation.code) || string.IsNullOrWhiteSpace(invitation.owner))
                    { Finish("InvalidInvitation"); return; }
                    if (invitation.owner == connection.LocalUserId.ToString()) { Finish("SameDeviceIdentity"); return; }
                    lobby.Join(invitation.code);
                }
            }
            lobby?.Tick(now);
            room?.Tick(now);
            transport?.Poll();
            if (lobby != null && lobby.State == LobbyState.Failed) { Finish(lobby.ErrorCode); return; }
            if (lobby != null && lobby.State == LobbyState.Connected)
            {
                if (role == "host" && !File.Exists(invitePath))
                {
                    try { File.WriteAllText(invitePath, JsonUtility.ToJson(new Invitation { code = lobby.Code, owner = lobby.OwnerId })); }
                    catch (IOException) { Finish("InvitationWriteFailed"); return; }
                }
            }
            TickRoomLifecycle(now);
        }

        private void TickRoomLifecycle(double now)
        {
            if (lifecycle == null || lobby == null || room == null) return;
            var view = room.Current;
            var local = view?.Members.FirstOrDefault(member => member.Id == connection.LocalUserId.ToString());
            bool rolesAssigned = view != null && view.Members.Count == 2 && view.Members.All(member => member.Connected && member.Role != PlayerRole.Unassigned);
            if (local != null && local.Role != PlayerRole.Unassigned) assignedRole = local.Role.ToString();
            if (role != "host" && now - lastSend >= .5)
            {
                lastSend = now;
                string progress = lifecycle.TryGetLatestLocalCheckpoint(out EosProbeRoundCheckpoint checkpoint, out int progressRound)
                    ? EosProbeLifecycle.EncodeCheckpoint(checkpoint, progressRound) : "UnityHello";
                transport.Send(lobby.OwnerId, 2, new ArraySegment<byte>(Encoding.UTF8.GetBytes(progress)), true);
            }
            // Do not ask the lifecycle for an action until the caller can execute it. Several
            // room callbacks can arrive inside this window; consuming their action here would
            // permanently lose it because the lifecycle suppresses duplicate requests.
            if (now < nextAction) return;
            var action = lifecycle.Advance(lobby.State, view?.Phase, view?.Round ?? 0, local?.Ready == true,
                view != null && view.Members.Count == 2 && view.Members.All(member => member.Connected && member.Ready),
                rolesAssigned, HandshakeComplete);
            if (action == EosProbeLifecycleAction.None) return;
            nextAction = now + .5;
            RoomError error = RoomError.None;
            switch (action)
            {
                case EosProbeLifecycleAction.SetReady: error = room.SetReady(true); break;
                case EosProbeLifecycleAction.StartRound: error = room.StartRound(); break;
                case EosProbeLifecycleAction.FinishRound: error = room.FinishRound(); break;
                case EosProbeLifecycleAction.ReturnToLobby: error = room.ReturnToLobby(); break;
                case EosProbeLifecycleAction.CloseLobby: ownerCloseRequested = true; lobby.Leave(); break;
                case EosProbeLifecycleAction.Complete: Finish("Success"); return;
            }
            if (error != RoomError.None) Finish("Room_" + error);
        }
        private void Receive(string peer, byte channel, ArraySegment<byte> data)
        {
            if (channel != 2 || data.Array == null || data.Count <= 0 || data.Count > 48 || !IsExpectedPeer(peer)) return;
            string text = Encoding.UTF8.GetString(data.Array, data.Offset, data.Count);
            if (role == "host" && text == "UnityHello")
            {
                if (!hostHelloReceived) { hostHelloReceived = true; received++; }
                transport.Send(peer, 2, new ArraySegment<byte>(Encoding.UTF8.GetBytes("UnityAck")), true);
            }
            else if (role != "host" && text == "UnityAck")
            {
                if (!guestAckReceived) { guestAckReceived = true; received++; }
                transport.Send(peer, 2, new ArraySegment<byte>(Encoding.UTF8.GetBytes("UnityConfirmed")), true);
            }
            else if (role == "host" && text == "UnityConfirmed")
            {
                if (!hostConfirmationReceived) { hostConfirmationReceived = true; received++; }
            }
            else if (role == "host" && EosProbeLifecycle.TryParseCheckpoint(text, out EosProbeRoundCheckpoint checkpoint, out int round) &&
                lifecycle != null && lifecycle.RecordRemote(checkpoint, round)) received++;
        }

        private bool HandshakeComplete => role == "host" ? hostConfirmationReceived : guestAckReceived;
        private bool IsExpectedPeer(string peer)
        {
            var view = room?.Current;
            string local = connection == null ? null : connection.LocalUserId.ToString();
            return view != null && view.Members.Count == 2 && !string.IsNullOrWhiteSpace(peer) && peer != local &&
                view.Members.Any(member => member.Connected && member.Id == peer);
        }
        private RoundReceipt[] RoundReceipts()
        {
            var rounds = new RoundReceipt[EosProbeLifecycle.RequiredRounds];
            for (int round = 1; round <= rounds.Length; round++)
                rounds[round - 1] = new RoundReceipt {
                    round = round,
                    started = lifecycle?.HasLocalCheckpoint(EosProbeRoundCheckpoint.Playing, round) == true,
                    resultObserved = lifecycle?.HasLocalCheckpoint(EosProbeRoundCheckpoint.Results, round) == true,
                    returnedToLobby = lifecycle?.HasLocalCheckpoint(EosProbeRoundCheckpoint.ReturnedLobby, round) == true,
                    remotePlayingAcknowledged = lifecycle?.HasRemoteCheckpoint(EosProbeRoundCheckpoint.Playing, round) == true,
                    remoteResultsAcknowledged = lifecycle?.HasRemoteCheckpoint(EosProbeRoundCheckpoint.Results, round) == true,
                    remoteReturnAcknowledged = lifecycle?.HasRemoteCheckpoint(EosProbeRoundCheckpoint.ReturnedLobby, round) == true
                };
            return rounds;
        }
        private void Finish(string result)
        {
            if (finished) return;
            finished = true;
            if (!string.IsNullOrWhiteSpace(receiptPath))
                File.WriteAllText(receiptPath, JsonUtility.ToJson(new Receipt {
                    role = role, result = result, networkType = networkType, closeReason = closeReason, received = received,
                    unityVersion = Application.unityVersion, assignedRole = assignedRole,
                    requiredRounds = EosProbeLifecycle.RequiredRounds,
                    completedRounds = lifecycle?.CompletedRounds ?? 0,
                    remotelyConfirmedRounds = lifecycle?.RemoteConfirmedRounds ?? 0,
                    roomHandshake = HandshakeComplete,
                    bothReady = lifecycle?.SawPlaying == true, roundStarted = lifecycle?.SawPlaying == true,
                    resultObserved = lifecycle?.SawResults == true, returnedToLobby = lifecycle?.SawReturnedLobby == true,
                    ownerCloseRequested = ownerCloseRequested,
                    ownerCloseObserved = role != "host" && result == "Success",
                    rounds = RoundReceipts()
                }, true));
            Debug.Log("LMS_EOS_PROBE " + role + " " + result);
            room?.Dispose(); transport?.Dispose(); lobby?.Dispose(); connection?.Dispose();
            Application.Quit(result == "Success" ? 0 : 2);
        }
        private void OnDestroy() { room?.Dispose(); transport?.Dispose(); lobby?.Dispose(); connection?.Dispose(); }
    }
}
#endif
