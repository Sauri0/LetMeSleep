using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using LetMeSleep.Core;
using LetMeSleep.Gameplay;
using UnityEngine;

namespace LetMeSleep.Bootstrap
{
    public sealed partial class AlfaApplication
    {
        [Serializable] private sealed class PlaytestContext { public string runId="", saltBase64=""; }
        [Serializable] private sealed class PlaytestEvent
        {
            public string schema="lms-playtest-events-1", runId, utc, buildGuid, version, protocol;
            public string kind, status, localHash, lobbyHash, peerHash, epoch, role, mapHash;
            public int sequence, round, members;
            public uint tick;
            public float blood;
            // Events are evidence of observations, never automatic certification of WAN or process exit.
        }
        private StreamWriter playtestWriter;
        private byte[] playtestSalt;
        private string playtestRun="", playtestRoomKey, playtestResultKey;
        private int playtestSequence;

        private void StartPlaytestJournal()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var args=Environment.GetCommandLineArgs();
            int output=Array.IndexOf(args,"--lms-playtest-log"), context=Array.IndexOf(args,"--lms-playtest-context");
            if(output<0 && context<0) return;
            try
            {
                if(output<0 || output+1>=args.Length || context<0 || context+1>=args.Length)
                    throw new ArgumentException("Both playtest arguments are required.");
                var path=Path.GetFullPath(args[output+1]);
                var contextPath=Path.GetFullPath(args[context+1]);
                if(new FileInfo(contextPath).Length>4096) throw new InvalidDataException("Oversized context.");
                var setup=JsonUtility.FromJson<PlaytestContext>(File.ReadAllText(contextPath));
                if(setup==null || !Guid.TryParse(setup.runId,out var run)) throw new InvalidDataException("Invalid run ID.");
                playtestSalt=Convert.FromBase64String(setup.saltBase64 ?? "");
                if(playtestSalt.Length!=32) throw new InvalidDataException("Invalid salt.");
                playtestRun=run.ToString("D");
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                playtestWriter=new StreamWriter(new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.Read),new UTF8Encoding(false));
                RecordPlaytest("Started","ObservationsOnly");
            }
            catch(Exception exception) when(exception is IOException || exception is InvalidDataException || exception is UnauthorizedAccessException || exception is ArgumentException || exception is FormatException)
            { DisablePlaytestJournal(exception); }
#endif
        }

        private string PlaytestHash(string value)
        {
            if(string.IsNullOrEmpty(value) || playtestSalt==null) return "";
            using var hmac=new HMACSHA256(playtestSalt);
            return BitConverter.ToString(hmac.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-","").ToLowerInvariant();
        }
        private void RecordPlaytest(string kind,string status="",string peer=null,GameSessionState state=null,string role="")
        {
            if(playtestWriter==null) return;
            try
            {
                if(playtestSequence>=4095) { StopPlaytestJournal("EventLimitReached"); return; }
                var item=new PlaytestEvent
                {
                    runId=playtestRun, utc=DateTime.UtcNow.ToString("O"), buildGuid=Application.buildGUID,
                    version=Application.version, protocol=RoomSession.Protocol, sequence=++playtestSequence,
                    kind=kind, status=status, localHash=connection?.LocalUserId==null ? "" : PlaytestHash(LocalId),
                    lobbyHash=PlaytestHash(lobby?.Code), peerHash=PlaytestHash(peer),
                    round=state==null ? room?.Current?.Round ?? -1 : checked((int)state.RoundId),
                    members=room?.Current?.Members.Count ?? 0, epoch=state?.SessionEpoch.ToString() ?? "",
                    tick=state?.HostTick ?? 0, blood=state?.BloodCollected ?? 0, mapHash=state?.ContentHash ?? "", role=role
                };
                playtestWriter.WriteLine(JsonUtility.ToJson(item));
                playtestWriter.Flush(); // Small event records only: no per-frame sampling or GPU readback.
            }
            catch(Exception exception) when(exception is IOException || exception is UnauthorizedAccessException || exception is OverflowException)
            { DisablePlaytestJournal(exception); }
        }
        private void ObservePlaytestRoom(RoomView view)
        {
            if(playtestWriter==null || view==null) return;
            string key=PlaytestHash(lobby?.Code)+":"+view.Round+":"+view.Phase+":"+
                string.Join(",",view.Members.Select(m=>PlaytestHash(m.Id)).OrderBy(id=>id,StringComparer.Ordinal));
            if(key==playtestRoomKey) return;
            playtestRoomKey=key;
            RecordPlaytest("Room",view.Phase.ToString());
            foreach(var member in view.Members) RecordPlaytest("Member",member.Id==LocalId ? "Local" : "Remote",member.Id,role:member.Role.ToString());
        }
        private void ObservePlaytestPeer(string peer,string state) => RecordPlaytest("PeerRoute",state,peer);
        private void ObservePlaytestResult(GameSessionState state)
        {
            if(playtestWriter==null || training || state.SimulationPhase!=SimulationPhase.Ended) return;
            string key=state.SessionEpoch+":"+state.RoundId;
            if(key==playtestResultKey) return;
            playtestResultKey=key;
            RecordPlaytest("RoundEnded",state.Result.ToString(),state:state,role:state.Winner.ToString());
        }
        private void StopPlaytestJournal(string reason)
        {
            if(playtestWriter==null) return;
            var writer=playtestWriter; playtestWriter=null;
            try
            {
                writer.WriteLine(JsonUtility.ToJson(new PlaytestEvent { runId=playtestRun, utc=DateTime.UtcNow.ToString("O"),
                    kind="JournalStopped",status=reason,sequence=++playtestSequence }));
                writer.Dispose();
            }
            catch(IOException) { try { writer.Dispose(); } catch(IOException) { } }
            playtestSalt=null;
        }
        private void DisablePlaytestJournal(Exception exception)
        {
            StopPlaytestJournal("WriteFailed");
            // Do not include paths, account IDs or context contents in application logs.
            Debug.LogWarning("LMS_PLAYTEST_JOURNAL_DISABLED: "+exception.GetType().Name);
        }
    }
}
