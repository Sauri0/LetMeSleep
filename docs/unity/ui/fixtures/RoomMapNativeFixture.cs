using System;
using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LetMeSleep.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LetMeSleep.Validation
{
    // External fixture, not a product map catalog. Compile/load only for isolated UI validation.
    public sealed class RoomMapNativeFixture : MonoBehaviour
    {
        [Serializable] public class Receipt
        {
            public string state="running", failure, scope="Real uGUI/TMP, catalog map IDs and recorded actions; no geometry load or physical input claim.";
            public int width,height; public List<string> checks=new List<string>();
            public List<string> emissions=new List<string>();
        }
        private static RoomMapNativeFixture active;
        private AlfaUiController ui;
        private EventSystem ownedEvents;
        private Spy spy;
        private Receipt receipt;
        private string directory;
        private Camera renderCamera;
        private TrainingMapOption[] maps;
        public static string RunRenderTexture(TMP_FontAsset heading,TMP_FontAsset body,Camera camera,TrainingMapOption[] maps)
        {
            if(!camera || !camera.targetTexture)throw new ArgumentException("Camera with target RenderTexture required.");
            if(maps==null || maps.Length<2)throw new ArgumentException("Real catalog with at least two maps required.");
            return RunCore(heading,body,camera.targetTexture.width,camera.targetTexture.height,camera,maps);
        }
        private static string RunCore(TMP_FontAsset heading,TMP_FontAsset body,int width,int height,Camera camera,TrainingMapOption[] maps)
        {
            if(!Application.isPlaying) throw new InvalidOperationException("Play Mode required.");
            if(active || FindObjectsByType<AlfaUiController>(FindObjectsSortMode.None).Length!=0)
                throw new InvalidOperationException("Use isolated empty Play scene; do not overlay or disable a live product UI.");
            if(!heading || !body) throw new ArgumentException("Pass actual project font assets.");
            if(!((width==1280&&height==720)||(width==1920&&height==1080)) || (!camera && (Screen.width!=width || Screen.height!=height)))
                throw new ArgumentException("Set exact 720p/1080p viewport on primary horizontal monitor first.");
            var f=new GameObject("RoomMapNativeFixture").AddComponent<RoomMapNativeFixture>(); active=f;
            f.receipt=new Receipt{width=width,height=height}; f.maps=maps.ToArray();
            f.renderCamera=camera;
            if(camera)f.receipt.scope="ScreenSpaceCamera RenderTexture UI; explicit 16:9 scale factor, catalog IDs, synthetic room/actions. Not GameView/physical input/map loading.";
            f.directory="N:/LetMeSleep/Validation/UI-RoomMaps-Native-20260913/run-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,8);
            Directory.CreateDirectory(f.directory);
            try
            {
                bool hadEvents=EventSystem.current!=null;
                f.spy=new Spy(f.receipt);
                f.ui=AlfaUiRuntime.Create(f.spy,new AlfaUiDependencies(heading,body,persistentAcrossScenes:false));
                if(camera)
                {
                    var canvas=f.ui.GetComponent<Canvas>();
                    canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=5;
                    // Isolate layout from batch desktop size; this is explicitly RT validation.
                    f.ui.GetComponent<CanvasScaler>().enabled=false;canvas.scaleFactor=width/1920f;
                }
                if(!hadEvents) f.ownedEvents=EventSystem.current;
                f.StartCoroutine(f.Exercise());
            }
            catch(Exception e){ f.Fail(e); }
            return f.directory;
        }
        public static string Status()=>active ? JsonConvert.SerializeObject(active.receipt,Formatting.Indented) : "No fixture active.";
        public static string ShowMapForReview(int index)
        {
            if(!active || active.receipt.state!="checks-completed-awaiting-visual-review")
                throw new InvalidOperationException("Wait for completed checks first.");
            if(index<0 || index>=active.maps.Length)throw new ArgumentOutOfRangeException(nameof(index));
            active.ui.PresentLobby(active.State(active.maps[index].Id));
            return "Authoritative synthetic host snapshot for review: "+active.maps[index].Id;
        }
        public static void DisposeFixture(){if(active) Destroy(active.gameObject);}
        private Button B(string name)=>ui.GetComponentsInChildren<Button>(true).Single(b=>b.name==name);
        private TextMeshProUGUI MapLabel()=>ui.GetComponentsInChildren<TextMeshProUGUI>(true).Single(t=>t.name=="RoomMapValue");
        private void Check(bool ok,string label){if(!ok)throw new InvalidOperationException(label);receipt.checks.Add(label);}
        private void Click(string name)
        {
            var b=B(name);Check(b.gameObject.activeInHierarchy&&b.IsInteractable(),"enabled "+name);
            ExecuteEvents.Execute(b.gameObject,new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler);
        }
        private void Layout(string label)
        {
            Canvas.ForceUpdateCanvases();
            var pixelRect=ui.GetComponent<Canvas>().pixelRect;
            Check(Mathf.Abs(pixelRect.width-receipt.width)<.5f&&Mathf.Abs(pixelRect.height-receipt.height)<.5f,"canvas pixelRect "+label);
            foreach(var text in ui.GetComponentsInChildren<TextMeshProUGUI>().Where(t=>t.gameObject.activeInHierarchy))
            {
                text.ForceMeshUpdate();
                if(text.isTextOverflowing)
                    receipt.checks.Add("Overflow detail: "+text.name+" parsed="+text.GetParsedText().Replace("\n"," | ")+
                        " rect="+text.rectTransform.rect+" margin="+text.margin+" fontSize="+text.fontSize+
                        " firstOverflowCharacterIndex="+text.firstOverflowCharacterIndex);
                Check(!text.isTextOverflowing,"TMP full text "+label+"/"+text.name);
            }
            foreach(var b in ui.GetComponentsInChildren<Button>().Where(b=>b.gameObject.activeInHierarchy))
            {
                var corners=new Vector3[4];((RectTransform)b.transform).GetWorldCorners(corners);
                Check(corners.All(c=>{var p=RectTransformUtility.WorldToScreenPoint(renderCamera,c);
                    return p.x>=-.5f&&p.y>=-.5f&&p.x<=receipt.width+.5f&&p.y<=receipt.height+.5f;}),"button bounds "+label+"/"+b.name);
            }
        }
        private bool Step(Action action)
        {try{action();return true;}catch(Exception e){Fail(e);return false;}}
        private LobbyUiState State(string mapId=AlfaUiController.HousePatioMapId,bool owner=true,bool waiting=true,
            bool ready=false,bool start=false,bool rules=false,int? humanCount=1)
            => new LobbyUiState(owner,"ABCDEF",new[]{new LobbyMemberUiState("host","Anfitrion",true),
                new LobbyMemberUiState("guest","Invitado",true)},true,ready,humanCount,true,"",mapId,
                maps.FirstOrDefault(m=>m.Id==mapId)?.DisplayName ?? "CASA CON PATIO",startPending:start,isWaiting:waiting,rulesPending:rules);
        private IEnumerator Exercise()
        {
            yield return null;
            if(!Step(()=>{ui.SetRoomMaps(maps);ui.PresentLobby(State());}))yield break;
            yield return null;
            if(!Step(()=>{Check(MapLabel().text=="CASA CON PATIO","default alpha authoritative");Layout("alpha");}))yield break;
            if(!Step(()=>{
                Check(B("HumanCount1").GetComponentInChildren<TextMeshProUGUI>().text=="[1]","selected count uses ASCII brackets");
                ui.PresentLobby(State(humanCount:null));
            }))yield break;
            yield return null;
            if(!Step(()=>{
                Check(B("HumanCount0").GetComponentInChildren<TextMeshProUGUI>().text=="[AUTO]","selected AUTO uses ASCII brackets");
                Check(B("HumanCount1").GetComponentInChildren<TextMeshProUGUI>().text=="1","unselected count has no marker");
                Layout("auto-count");ui.PresentLobby(State());
            }))yield break;
            yield return null;
            for(int i=0;i<maps.Length;i++)
            {
                int index=i;
                if(!Step(()=>{
                    string before=MapLabel().text;int calls=spy.starts;
                    Click("RoomMapNext");
                    Check(spy.starts==calls+1 && spy.map==maps[index].Id,"host emitted real catalog ID "+index);
                    Check(MapLabel().text==before,"no optimistic label "+index);
                    B("RoomMapNext").onClick.Invoke(); B("RoomMapPrevious").onClick.Invoke();
                    B("LobbyReadyButton").onClick.Invoke(); B("LobbyStartButton").onClick.Invoke();
                    Check(spy.starts==calls+1 && spy.readyCalls==0 && spy.roundCalls==0,"latch blocks duplicate and competing intent "+index);
                    ui.PresentLobby(State(index==0?AlfaUiController.HousePatioMapId:maps[index-1].Id,rules:true));
                    B("RoomMapNext").onClick.Invoke(); Check(spy.starts==calls+1,"authoritative pending guard "+index);
                    ui.PresentLobby(State(maps[index].Id));
                    Check(MapLabel().text==maps[index].DisplayName,"authoritative label "+index);
                }))yield break;
                yield return null;
                if(!Step(()=>Layout("catalog-map-"+index)))yield break;
            }
            if(!Step(()=>{
                foreach(var state in new[]{State(owner:false),State(waiting:false),State(ready:true),State(start:true),State(rules:true)})
                {
                    ui.PresentLobby(state);int before=spy.starts;
                    B("RoomMapNext").onClick.Invoke(); B("RoomMapPrevious").onClick.Invoke();
                    Check(!B("RoomMapNext").IsInteractable() && spy.starts==before,"guest/phase/pending blocks native callbacks");
                }
                ui.PresentLobby(State());Click("RoomMapNext");ui.PresentLobby(State());
                Check(MapLabel().text=="CASA CON PATIO" && B("RoomMapNext").IsInteractable(),"rejection snapshot releases latch");
                ui.SetRoomMaps(Array.Empty<TrainingMapOption>());int calls=spy.starts;
                B("RoomMapNext").onClick.Invoke();Check(!B("RoomMapNext").IsInteractable()&&spy.starts==calls,"empty catalog read-only");
                ui.SetRoomMaps(maps);ui.PresentLobby(State(maps[0].Id));
                Click("RoomMapPrevious");Check(spy.map==maps[maps.Length-1].Id,"previous wraps");
                ui.PresentLobby(State(maps[maps.Length-1].Id));Click("RoomMapNext");Check(spy.map==maps[0].Id,"next wraps");
                ui.PresentLobby(State(maps.OrderByDescending(m=>m.DisplayName.Length).First().Id));
            }))yield break;
            yield return null;
            if(!Step(()=>Layout("final-host")))yield break;
            receipt.state="checks-completed-awaiting-visual-review";Save();
        }
        private void Fail(Exception e){receipt.state="failed";receipt.failure=e.ToString();Save();}
        private void Save()=>File.WriteAllText(Path.Combine(directory,"receipt.json"),JsonConvert.SerializeObject(receipt,Formatting.Indented));
        private void OnDestroy()
        {
            if(receipt!=null){if(receipt.state=="running")receipt.state="interrupted";Save();}
            if(ui)Destroy(ui.gameObject);if(ownedEvents)Destroy(ownedEvents.gameObject);if(active==this)active=null;
        }
        private sealed class Spy : IMenuActions, IRoomMapActions
        {
            private readonly Receipt receipt;public int starts,readyCalls,roundCalls;public string map;
            public Spy(Receipt receipt){this.receipt=receipt;}
            public void StartTraining(AlfaRole role,string modeId,string mapId){}
            public void SetRoomMap(string mapId){starts++;map=mapId;receipt.emissions.Add("room|"+mapId);}
            public void SetGameplayInputBlocked(bool value){}
            public void CancelTraining(){} public void CreateRoom(string name){} public void JoinRoom(string name,string code){}
            public void CancelOnline(){} public void CopyRoomCode(string code){} public void SetReady(bool ready){readyCalls++;}
            public void SetHumanCount(int? count){} public void StartRound(){roundCalls++;} public void LeaveRoom(){}
            public void PreviewCustomization(BasicCustomizationDraft draft){} public void SaveCustomization(BasicCustomizationDraft draft){}
            public void ApplySettings(AlfaSettingsDraft draft){} public void ResumeGame(){} public void ReturnToLobby(){}
            public void SetLobbyExploration(bool value){} public void QuitGame(){}
        }
    }
}
