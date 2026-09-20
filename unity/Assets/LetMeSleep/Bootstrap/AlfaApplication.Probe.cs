using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LetMeSleep.Gameplay;
using LetMeSleep.Core;
using LetMeSleep.UI;
using UnityEngine;

namespace LetMeSleep.Bootstrap
{
    public sealed partial class AlfaApplication
    {
        // Explicit development-player opt-in. Ordinary launches never run or exit through this probe.
        private void StartBuildProbeIfRequested()
        {
            string output=RequestedProbeOutput();
            if(output!=null) StartCoroutine(BuildProbe(output));
        }
        private static string RequestedProbeOutput()
        {
            string[] args=Environment.GetCommandLineArgs();
            int option=Array.IndexOf(args,"--lms-probe-output");
            if(!Debug.isDebugBuild || Application.isEditor || option<0 || option+1>=args.Length) return null;
            return Path.GetFullPath(args[option+1]);
        }
        [Serializable] private sealed class PlayerProbeReceipt
        {
            public string version,unity,gpu,utc,modeId,mapId;
            public bool preferencesRecovered,preferencesWriteBlocked;
            public string loadedPlayerName,loadedSkin,loadedPajama,loadedMosquito;
            public bool humanRuntime,mosquitoRuntime,humanStationary,returnedToMenu,onlineRoomCreated,onlineRoomLeft;
            public bool humanGameplayVisible,mosquitoGameplayVisible;
            public int runtimeErrorCount;
            public bool humanSettled;
            public uint humanSettledTick;
            public float humanInitialDrop, humanInitialHorizontalDrift, humanMaxSettledDrift;
            public Vector3 humanSpawnPosition, humanSettledPosition, humanEndPosition;
            public int width,height,frames; public float medianFrameMs,p95FrameMs;
            public string failure="";
        }
        private IEnumerator BuildProbe(string output)
        {
            Directory.CreateDirectory(output);
            Screen.SetResolution(1920,1080,FullScreenMode.Windowed);
            var receipt=new PlayerProbeReceipt { version=Application.version,unity=Application.unityVersion,
                gpu=SystemInfo.graphicsDeviceName,utc=DateTime.UtcNow.ToString("O"),
                preferencesRecovered=preferenceStore?.RecoveredFromBackup==true,
                preferencesWriteBlocked=preferenceStore?.WriteBlocked==true,
                loadedPlayerName=playerName,loadedSkin=appearance.SkinColorId,
                loadedPajama=appearance.PajamaColorId,loadedMosquito=appearance.MosquitoColorId };
            int observedErrors=0;
            Application.LogCallback countErrors=(message,stack,type)=>
            {
                if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert) observedErrors++;
            };
            Application.logMessageReceived+=countErrors;
            string[] probeArgs=Environment.GetCommandLineArgs();
            int menuOption=Array.IndexOf(probeArgs,"--lms-probe-menu-seconds");
            float menuSeconds=2;
            if(menuOption>=0 && menuOption+1<probeArgs.Length && int.TryParse(probeArgs[menuOption+1],out int requestedMenuSeconds))
                menuSeconds=Mathf.Clamp(requestedMenuSeconds,2,30);
            yield return new WaitForSecondsRealtime(menuSeconds);
            ScreenCapture.CaptureScreenshot(Path.Combine(output,"menu.png"));
            yield return null; yield return null;
            string probeMode = Core.GameModes.Blood, probeMap = Core.RoomRules.AlfaMap;
            int modeOption = Array.IndexOf(probeArgs, "--lms-probe-mode"), mapOption = Array.IndexOf(probeArgs, "--lms-probe-map");
            if (modeOption >= 0 && modeOption + 1 < probeArgs.Length) probeMode = probeArgs[modeOption + 1];
            if (mapOption >= 0 && mapOption + 1 < probeArgs.Length) probeMap = probeArgs[mapOption + 1];
            receipt.modeId = probeMode; receipt.mapId = probeMap;
            if (!Core.GameModes.IsValid(probeMode) || !IsModeAvailable(probeMap, probeMode))
            {
                receipt.failure = "Requested mode/map is unavailable.";
                File.WriteAllText(Path.Combine(output, "player-probe.json"), JsonUtility.ToJson(receipt, true));
                Application.logMessageReceived -= countErrors; Application.Quit(1); yield break;
            }
            var timings=new List<float>();
            foreach(var role in new[]{AlfaRole.Human,AlfaRole.Mosquito})
            {
                StartTraining(role,probeMode,probeMap);
                if (game == null || game.LatestSnapshot == null) { receipt.failure = "Training failed to start."; break; }
                // The ordinary training button also switches the UI after StartTraining.
                ui.ShowGameplay(true);
                game.CaptureLocalInput=false; var start=game.LatestSnapshot.Actors.First(a=>a.ActorId==game.LocalActorId).Position;
                // Authoring spawn points may sit slightly above support. Measure idle drift
                // from the first real grounded tick, with a bounded initial drop and no
                // horizontal-motion exemption. Preserve the old 3.5 cm drift limit.
                Float3 settledPosition = start;
                bool settled = false, settlingValid = true;
                uint initialTick = game.LatestSnapshot.HostTick;
                float initialHorizontalDrift = 0, maxSettledDrift = 0;
                Action<GameSessionState> observeHuman = observedState =>
                {
                    if (role == AlfaRole.Human)
                    {
                        var observed = observedState.Actors.First(a => a.ActorId == game.LocalActorId);
                        settlingValid &= observed.Position.IsFinite;
                        if (!settled)
                        {
                            var delta = observed.Position - start;
                            initialHorizontalDrift = Mathf.Max(initialHorizontalDrift, Mathf.Sqrt(delta.X * delta.X + delta.Z * delta.Z));
                            settlingValid &= observed.Position.IsFinite && initialHorizontalDrift < .035f && delta.Y <= .035f && delta.Y >= -.10f;
                            if (observed.Grounded)
                            {
                                settled = true; settledPosition = observed.Position;
                                receipt.humanSettledTick = observedState.HostTick;
                                settlingValid &= receipt.humanSettledTick - initialTick <= 30;
                            }
                            else if (observedState.HostTick - initialTick > 30) settlingValid = false;
                        }
                        if (settled) maxSettledDrift = Mathf.Max(maxSettledDrift, (observed.Position - settledPosition).Length);
                    }
                };
                // Observe every simulation snapshot, including multiple ticks within one
                // rendered frame. Sampling only LatestSnapshot could miss transient drift.
                var observedRuntime = game;
                observeHuman(observedRuntime.LatestSnapshot);
                observedRuntime.SnapshotApplied += observeHuman;
                double until=Time.realtimeSinceStartupAsDouble+15;
                try
                {
                    while(Time.realtimeSinceStartupAsDouble<until)
                    {
                        yield return null;
                        if(until-Time.realtimeSinceStartupAsDouble<10) timings.Add(Time.unscaledDeltaTime*1000);
                    }
                }
                finally { observedRuntime.SnapshotApplied -= observeHuman; }
                var state=game.LatestSnapshot;
                bool valid=state.Actors.Count==3 && state.Actors.All(a=>a.Position.IsFinite);
                bool gameplayVisible=ui.CurrentScreen==AlfaUiScreen.Gameplay;
                if(role==AlfaRole.Human)
                {
                    var end = state.Actors.First(a=>a.ActorId==game.LocalActorId).Position;
                    receipt.humanRuntime=valid; receipt.humanGameplayVisible=gameplayVisible;
                    receipt.humanSettled=settled;
                    receipt.humanStationary=settled && settlingValid && maxSettledDrift < .035f;
                    receipt.humanSpawnPosition=new Vector3(start.X,start.Y,start.Z);
                    receipt.humanSettledPosition=new Vector3(settledPosition.X,settledPosition.Y,settledPosition.Z);
                    receipt.humanEndPosition=new Vector3(end.X,end.Y,end.Z);
                    receipt.humanInitialDrop=start.Y-settledPosition.Y;
                    receipt.humanInitialHorizontalDrift=initialHorizontalDrift;
                    receipt.humanMaxSettledDrift=maxSettledDrift;
                }
                else { receipt.mosquitoRuntime=valid; receipt.mosquitoGameplayVisible=gameplayVisible; }
                ScreenCapture.CaptureScreenshot(Path.Combine(output,role.ToString().ToLowerInvariant()+".png"));
                yield return null; yield return null;
                CancelTraining(); yield return null; yield return null;
            }
            receipt.returnedToMenu=game==null && ui.CurrentScreen==AlfaUiScreen.MainMenu;
            CreateRoom("Windows QA");
            double deadline=Time.realtimeSinceStartupAsDouble+40;
            while(Time.realtimeSinceStartupAsDouble<deadline && room?.Current==null
                && connection?.State!=Online.ConnectionState.Failed && lobby?.State!=Online.LobbyState.Failed)
                yield return null;
            receipt.onlineRoomCreated=room?.Current!=null && lobby?.State==Online.LobbyState.Connected && !string.IsNullOrEmpty(lobby.Code);
            LeaveRoom(); yield return new WaitForSecondsRealtime(1);
            receipt.onlineRoomLeft=ui.CurrentScreen==AlfaUiScreen.MainMenu && game==null && lobbyMovement==null;
            receipt.width=Screen.width; receipt.height=Screen.height; receipt.frames=timings.Count;
            timings.Sort(); if(timings.Count>0) { receipt.medianFrameMs=timings[timings.Count/2]; receipt.p95FrameMs=timings[Math.Min(timings.Count-1,(int)(timings.Count*.95))]; }
            Application.logMessageReceived-=countErrors;
            receipt.runtimeErrorCount=observedErrors;
            bool pass=receipt.humanRuntime && receipt.mosquitoRuntime && receipt.humanGameplayVisible && receipt.mosquitoGameplayVisible &&
                receipt.runtimeErrorCount==0 && receipt.humanStationary && receipt.returnedToMenu && receipt.onlineRoomCreated && receipt.onlineRoomLeft;
            if(!pass) receipt.failure="One or more explicit player checks failed; inspect booleans and player log.";
            File.WriteAllText(Path.Combine(output,"player-probe.json"),JsonUtility.ToJson(receipt,true));
            Application.Quit(pass?0:2);
        }
    }
}
