using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LetMeSleep.Gameplay;
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
            public string version,unity,gpu,utc;
            public bool preferencesRecovered,preferencesWriteBlocked;
            public string loadedPlayerName,loadedSkin,loadedPajama,loadedMosquito;
            public bool humanRuntime,mosquitoRuntime,humanStationary,returnedToMenu,onlineRoomCreated,onlineRoomLeft;
            public bool humanGameplayVisible,mosquitoGameplayVisible;
            public int runtimeErrorCount;
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
            var timings=new List<float>();
            foreach(var role in new[]{AlfaRole.Human,AlfaRole.Mosquito})
            {
                StartTraining(role,AlfaUiController.BloodModeId,Core.RoomRules.AlfaMap);
                // The ordinary training button also switches the UI after StartTraining.
                ui.ShowGameplay(true);
                game.CaptureLocalInput=false; var start=game.LatestSnapshot.Actors.First(a=>a.ActorId==game.LocalActorId).Position;
                double until=Time.realtimeSinceStartupAsDouble+15;
                while(Time.realtimeSinceStartupAsDouble<until)
                {
                    yield return null;
                    if(until-Time.realtimeSinceStartupAsDouble<10) timings.Add(Time.unscaledDeltaTime*1000);
                }
                var state=game.LatestSnapshot;
                bool valid=state.Actors.Count==3 && state.Actors.All(a=>a.Position.IsFinite);
                bool gameplayVisible=ui.CurrentScreen==AlfaUiScreen.Gameplay;
                if(role==AlfaRole.Human) { receipt.humanRuntime=valid; receipt.humanGameplayVisible=gameplayVisible; receipt.humanStationary=(state.Actors.First(a=>a.ActorId==game.LocalActorId).Position-start).Length<.035f; }
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
