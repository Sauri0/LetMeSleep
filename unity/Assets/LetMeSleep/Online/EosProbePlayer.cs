#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace LetMeSleep.Online
{
    /// <summary>Development-build connectivity check. Never active in a normal player launch.</summary>
    public sealed class EosProbePlayer : MonoBehaviour
    {
        private EosConnection connection;
        private EosLobbySession lobby;
        private EosPeerTransport transport;
        private string role, invitePath, receiptPath, cachePath;
        private bool startedRoom, finished;
        private double startTime, lastSend;
        private string networkType = "NotEstablished";
        private int received;
        [Serializable] private sealed class Invitation { public string code, owner; }
        [Serializable] private sealed class Receipt
        {
            public string schema = "lms-eos-unity-probe-1", role, result, networkType, unityVersion;
            public int received;
            public bool wanVerified = false;
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
            if (now - startTime > 75) { Finish("ProbeTimedOut"); return; }
            connection.Tick(now);
            if (connection.State == ConnectionState.Failed) { Finish(connection.FailureCode); return; }
            if (connection.State == ConnectionState.Ready && !startedRoom)
            {
                if (role != "host" && !File.Exists(invitePath)) return;
                startedRoom = true;
                lobby = new EosLobbySession(connection);
                transport = new EosPeerTransport(connection, lobby, true);
                transport.PeerStateChanged += (_, state) => networkType = state;
                transport.PacketReceived += Receive;
                if (role == "host") lobby.Create();
                else
                {
                    var invitation = JsonUtility.FromJson<Invitation>(File.ReadAllText(invitePath));
                    if (invitation.owner == connection.LocalUserId.ToString()) { Finish("SameDeviceIdentity"); return; }
                    lobby.Join(invitation.code);
                }
            }
            lobby?.Tick(now);
            transport?.Poll();
            if (lobby != null && lobby.State == LobbyState.Failed) { Finish(lobby.ErrorCode); return; }
            if (lobby != null && lobby.State == LobbyState.Connected)
            {
                if (role == "host" && !File.Exists(invitePath))
                {
                    File.WriteAllText(invitePath, JsonUtility.ToJson(new Invitation { code = lobby.Code, owner = lobby.OwnerId }));
                }
                if (role != "host" && now - lastSend >= .5)
                {
                    lastSend = now;
                    transport.Send(lobby.OwnerId, 0, new ArraySegment<byte>(Encoding.UTF8.GetBytes("UnityHello")), true);
                }
            }
        }
        private void Receive(string peer, byte channel, ArraySegment<byte> data)
        {
            string text = Encoding.UTF8.GetString(data.Array, data.Offset, data.Count);
            if (role == "host" && text == "UnityHello")
            {
                received++;
                transport.Send(peer, 0, new ArraySegment<byte>(Encoding.UTF8.GetBytes("UnityAck")), true);
                // Keep host alive until the client confirms its receipt.
            }
            else if (role != "host" && text == "UnityAck")
            {
                received++;
                transport.Send(peer, 0, new ArraySegment<byte>(Encoding.UTF8.GetBytes("UnityConfirmed")), true);
                Invoke(nameof(Succeed), 1);
            }
            else if (role == "host" && text == "UnityConfirmed") Invoke(nameof(Succeed), 1.5f);
        }
        private void Succeed() => Finish("Success");
        private void Finish(string result)
        {
            if (finished) return;
            finished = true;
            if (!string.IsNullOrWhiteSpace(receiptPath))
                File.WriteAllText(receiptPath, JsonUtility.ToJson(new Receipt { role = role, result = result, networkType = networkType, received = received, unityVersion = Application.unityVersion }, true));
            Debug.Log("LMS_EOS_PROBE " + role + " " + result);
            transport?.Dispose(); lobby?.Dispose(); connection?.Dispose();
            Application.Quit(result == "Success" ? 0 : 2);
        }
        private void OnDestroy() { transport?.Dispose(); lobby?.Dispose(); connection?.Dispose(); }
    }
}
#endif
