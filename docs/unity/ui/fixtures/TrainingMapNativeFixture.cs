using System;
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
    public sealed class TrainingMapNativeFixture : MonoBehaviour
    {
        [Serializable] public class Receipt
        {
            public string state="running", failure, scope="Real uGUI/TMP, synthetic map IDs and recorded actions; no geometry load or physical input claim.";
            public int width,height; public List<string> checks=new List<string>();
            public List<string> emissions=new List<string>();
        }
        private static TrainingMapNativeFixture active;
        private AlfaUiController ui;
        private EventSystem ownedEvents;
        private Spy spy;
        private Receipt receipt;
        private string directory;
        private Camera renderCamera;
        private readonly TrainingMapOption[] maps=Enumerable.Range(1,5)
            .Select(i=>new TrainingMapOption("fixture-only-map-"+i,"Mapa de prueba explícito "+i)).ToArray();
        public static string Run(TMP_FontAsset heading,TMP_FontAsset body,int width,int height)
            => RunCore(heading,body,width,height,null);
        public static string RunRenderTexture(TMP_FontAsset heading,TMP_FontAsset body,Camera camera)
        {
            if(!camera || !camera.targetTexture)throw new ArgumentException("Camera with target RenderTexture required.");
            return RunCore(heading,body,camera.targetTexture.width,camera.targetTexture.height,camera);
        }
        private static string RunCore(TMP_FontAsset heading,TMP_FontAsset body,int width,int height,Camera camera)
        {
            if(!Application.isPlaying) throw new InvalidOperationException("Play Mode required.");
            if(active || FindObjectsByType<AlfaUiController>(FindObjectsSortMode.None).Length!=0)
                throw new InvalidOperationException("Use isolated empty Play scene; do not overlay or disable a live product UI.");
            if(!heading || !body) throw new ArgumentException("Pass actual project font assets.");
            if(!((width==1280&&height==720)||(width==1920&&height==1080)) || (!camera && (Screen.width!=width || Screen.height!=height)))
                throw new ArgumentException("Set exact 720p/1080p viewport on primary horizontal monitor first.");
            var f=new GameObject("TrainingMapNativeFixture").AddComponent<TrainingMapNativeFixture>(); active=f;
            f.receipt=new Receipt{width=width,height=height};
            f.renderCamera=camera;
            if(camera)f.receipt.scope="ScreenSpaceCamera RenderTexture UI; explicit 16:9 scale factor, synthetic IDs/actions. Not GameView/physical input/map loading.";
            f.directory="N:/LetMeSleep/Validation/UI-TrainingMaps-Native-20260913/run-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,8);
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
        public static string Status()=>active ? JsonUtility.ToJson(active.receipt,true) : "No fixture active.";
        public static string ShowMapForReview(int index)
        {
            if(!active || active.receipt.state!="checks-completed-awaiting-visual-review")
                throw new InvalidOperationException("Wait for completed checks first.");
            if(index<0 || index>=5)throw new ArgumentOutOfRangeException(nameof(index));
            active.ui.ShowTraining();
            for(int step=0;step<5 && active.MapLabel().text!=active.maps[index].DisplayName;step++) active.Click("TrainingMapNext");
            return "Fixture map selected through UI. Wait one frame, then capture actual GameView: "+active.maps[index].Id;
        }
        public static void DisposeFixture(){if(active) Destroy(active.gameObject);}
        private Button B(string name)=>ui.GetComponentsInChildren<Button>(true).Single(b=>b.name==name);
        private TextMeshProUGUI MapLabel()=>ui.GetComponentsInChildren<TextMeshProUGUI>(true).Single(t=>t.name=="TrainingMapValue");
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
        private IEnumerator Exercise()
        {
            yield return null;
            if(!Step(()=>{Click("MainTrainingButton");ui.SetTrainingMaps(maps);} ))yield break;
            yield return null;
            for(int i=0;i<5;i++)
            {
                int index=i;
                if(!Step(()=>{
                    Check(MapLabel().text==maps[index].DisplayName,"selected label "+index);Layout("map"+index);
                    int before=spy.starts;Click("TrainingStartButton");
                    Check(spy.starts==before+1&&spy.map==maps[index].Id,"emitted selected ID "+index);
                    Check(!B("TrainingMapNext").IsInteractable()&&!B("TrainingMapPrevious").IsInteractable(),"latch locks selector");
                    ui.PresentTraining(new TrainingUiState(AlfaRole.Human,true));
                    B("TrainingMapNext").onClick.Invoke(); B("TrainingStartButton").onClick.Invoke();
                    Check(MapLabel().text==maps[index].DisplayName&&spy.starts==before+1,"busy guard blocks forced callbacks");
                    ui.PresentResults(new ResultsUiState(MatchOutcome.Humans,true,false,0,20,3,"SYNTHETIC FIXTURE RESULT"));
                }))yield break;
                yield return null;
                if(!Step(()=>{
                    Layout("results"+index);int before=spy.starts;Click("ResultsPrimaryButton");
                    Check(spy.starts==before+1&&spy.map==maps[index].Id,"retry retained ID "+index);
                    Check(!B("ResultsPrimaryButton").IsInteractable(),"busy retry disabled");
                    B("ResultsPrimaryButton").onClick.Invoke();Check(spy.starts==before+1,"busy retry guards duplicate callback");
                    ui.PresentTraining(new TrainingUiState());ui.ShowTraining();
                    if(index<4)Click("TrainingMapNext");
                }))yield break;
                yield return null;
            }
            if(!Step(()=>{
                Click("TrainingMapNext");Check(MapLabel().text==maps[0].DisplayName,"forward wraps five");
                Click("TrainingMapPrevious");Check(MapLabel().text==maps[4].DisplayName,"backward wraps five");
                Click("TrainingBackButton");Check(ui.CurrentScreen==AlfaUiScreen.MainMenu,"back to menu");
                Click("MainTrainingButton");Check(MapLabel().text==maps[4].DisplayName,"menu return retains choice");
                int before=spy.starts;ui.SetTrainingMaps(Array.Empty<TrainingMapOption>());
                Check(!B("TrainingStartButton").IsInteractable(),"empty disables start");
                B("TrainingStartButton").onClick.Invoke();Check(spy.starts==before,"empty guarded dispatch");
                ui.SetTrainingMaps(maps);
            }))yield break;
            yield return null;
            if(!Step(()=>Layout("final")))yield break;
            receipt.state="checks-completed-awaiting-visual-review";Save();
            // Keep actual UI visible for Root's still/interaction review. Explicit DisposeFixture cleans up.
        }
        private void Fail(Exception e){receipt.state="failed";receipt.failure=e.ToString();Save();}
        private void Save()=>File.WriteAllText(Path.Combine(directory,"receipt.json"),JsonUtility.ToJson(receipt,true));
        private void OnDestroy()
        {
            if(receipt!=null){if(receipt.state=="running")receipt.state="interrupted";Save();}
            if(ui)Destroy(ui.gameObject);if(ownedEvents)Destroy(ownedEvents.gameObject);if(active==this)active=null;
        }
        private sealed class Spy : IMenuActions
        {
            private readonly Receipt receipt;public int starts;public string map;
            public Spy(Receipt receipt){this.receipt=receipt;}
            public void StartTraining(AlfaRole role,string modeId,string mapId){starts++;map=mapId;receipt.emissions.Add(role+"|"+modeId+"|"+mapId);}
            public void SetGameplayInputBlocked(bool value){}
            public void CancelTraining(){} public void CreateRoom(string name){} public void JoinRoom(string name,string code){}
            public void CancelOnline(){} public void CopyRoomCode(string code){} public void SetReady(bool ready){}
            public void SetHumanCount(int? count){} public void StartRound(){} public void LeaveRoom(){}
            public void PreviewCustomization(BasicCustomizationDraft draft){} public void SaveCustomization(BasicCustomizationDraft draft){}
            public void ApplySettings(AlfaSettingsDraft draft){} public void ResumeGame(){} public void ReturnToLobby(){}
            public void SetLobbyExploration(bool value){} public void QuitGame(){}
        }
    }
}
