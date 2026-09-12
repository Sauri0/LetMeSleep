using System.IO;
using LetMeSleep.Online;
using UnityEditor;
using UnityEngine;

namespace LetMeSleep.Editor
{
    public static class EosProbe
    {
        private static EosConnection connection;
        private static EosLobbySession room;
        public static string Status => connection == null ? "Stopped" : connection.State + ":" + connection.FailureCode;
        public static string RoomStatus => room == null ? "None" : room.State + ":" + room.ErrorCode;
        public static string RoomCode => room?.Code ?? "";
        public static void CreateRoom() { room?.Dispose(); room = new EosLobbySession(connection); room.Create(); }
        public static void LeaveRoom() => room?.Leave();
        public static void Start(string configPath)
        {
            Stop();
            connection = new EosConnection();
            connection.Initialize(EosConfiguration.Load(configPath), "UnityProbe", "N:/LetMeSleep/Private/EosProbeCache");
            EditorApplication.update += Tick;
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
            EditorApplication.quitting += Stop;
        }
        private static void Tick() { connection?.Tick(EditorApplication.timeSinceStartup); room?.Tick(EditorApplication.timeSinceStartup); }
        public static void Stop()
        {
            EditorApplication.update -= Tick;
            AssemblyReloadEvents.beforeAssemblyReload -= Stop;
            EditorApplication.quitting -= Stop;
            room?.Dispose(); room = null;
            connection?.Dispose(); connection = null;
        }
    }
}
