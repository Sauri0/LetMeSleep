using System;
using System.IO;
using LetMeSleep.Validation;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Name/signature required by the coordinator's HiggsfieldExternalFixture loader.
public static class HiggsfieldMapChecks
{
    [Serializable] private class Config
    {
        public string headingFont="Assets/LetMeSleep/UI/Fonts/Bangers-Regular SDF.asset";
        public string bodyFont="Assets/LetMeSleep/UI/Fonts/AtkinsonHyperlegible-Regular SDF.asset";
    }
    [Serializable] private class Report
    {
        public string state="running",failure,fixture720,fixture1080;
        public string scope="URP RenderTexture 1280x720 and 1920x1080; real uGUI with explicit 16:9 scale; synthetic map IDs/actions. No GameView, physical input or geometry validation.";
        public bool checks720,checks1080;
    }
    private static Config config;private static Report report;private static string output;
    private static Camera camera;private static RenderTexture texture;
    private static int phase,index;private static double deadline,ready;
    private static bool oldEnabled;private static EnterPlayModeOptions oldOptions;
    public static string Run(string configPath,string outputPath)
    {
        if(!Application.isBatchMode)throw new InvalidOperationException("Use the dedicated batch editor with graphics.");
        if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)throw new InvalidOperationException("Graphics required; omit -nographics.");
        config=JsonUtility.FromJson<Config>(File.ReadAllText(configPath)) ?? new Config();
        output=Path.GetFullPath(outputPath);Directory.CreateDirectory(output);
        if(File.Exists(Path.Combine(output,"batch.json")))throw new InvalidOperationException("Use fresh output directory.");
        report=new Report();Save();oldEnabled=EditorSettings.enterPlayModeOptionsEnabled;oldOptions=EditorSettings.enterPlayModeOptions;
        try
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            EditorSettings.enterPlayModeOptionsEnabled=true;
            EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload|EnterPlayModeOptions.DisableSceneReload;
            phase=0;index=0;deadline=EditorApplication.timeSinceStartup+180;
            EditorApplication.update+=Tick;EditorApplication.isPlaying=true;
        }
        catch(Exception e){Fail(e);}
        return "Asynchronous UI RT fixture started: "+output+". Do not pass -quit.";
    }
    private static void Tick()
    {
        try
        {
            if(phase==90){if(!EditorApplication.isPlayingOrWillChangePlaymode)Finish();return;}
            if(EditorApplication.timeSinceStartup>deadline)throw new TimeoutException("UI RT batch timeout.");
            if(!EditorApplication.isPlaying)return;
            int width=index==0?1280:1920,height=index==0?720:1080;
            if(phase==0)
            {
                texture=new RenderTexture(width,height,24);if(!texture.Create())throw new Exception("RT creation failed.");
                camera=new GameObject("FixtureRTCamera",typeof(Camera)).GetComponent<Camera>();
                camera.targetTexture=texture;camera.aspect=(float)width/height;
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.025f,.05f,.09f);
                camera.nearClipPlane=.1f;camera.farClipPlane=100;
                var path=TrainingMapNativeFixture.RunRenderTexture(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(config.headingFont),
                    AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(config.bodyFont),camera);
                if(index==0)report.fixture720=path;else report.fixture1080=path;
                Save();phase=1;
            }
            else if(phase==1)
            {
                string status=TrainingMapNativeFixture.Status();
                bool ok=status.Contains("checks-completed-awaiting-visual-review");
                bool failed=status.Contains("\"failed\"");if(!ok&&!failed)return;
                if(index==0)report.checks720=ok;else report.checks1080=ok;
                if(ok)TrainingMapNativeFixture.ShowMapForReview(4);
                else report.failure=(report.failure??"")+"\nFixture "+height+": "+status;
                ready=EditorApplication.timeSinceStartup+.5;phase=2;
            }
            else if(phase==2 && EditorApplication.timeSinceStartup>=ready)
            {
                Canvas.ForceUpdateCanvases();
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=texture});
                Texture2D pixels=null;var previous=RenderTexture.active;
                try
                {
                    RenderTexture.active=texture;pixels=new Texture2D(width,height,TextureFormat.RGB24,false);
                    pixels.ReadPixels(new Rect(0,0,width,height),0,0);pixels.Apply();
                    var png=pixels.EncodeToPNG();
                    int w=(png[16]<<24)|(png[17]<<16)|(png[18]<<8)|png[19];
                    int h=(png[20]<<24)|(png[21]<<16)|(png[22]<<8)|png[23];
                    if(w!=width||h!=height)throw new Exception("PNG dimensions mismatch.");
                    File.WriteAllBytes(Path.Combine(output,"training-"+height+"-RT.png"),png);
                }
                finally{RenderTexture.active=previous;if(pixels)UnityEngine.Object.Destroy(pixels);}
                Release();ready=EditorApplication.timeSinceStartup+.5;phase=3;
            }
            else if(phase==3 && EditorApplication.timeSinceStartup>=ready)
            {
                if(index==0){index=1;phase=0;}
                else{report.state=report.checks720&&report.checks1080?"captured-awaiting-visual-review":"fixture-checks-failed";phase=90;EditorApplication.isPlaying=false;}
            }
        }
        catch(Exception e){Fail(e);}
    }
    private static void Release()
    {
        TrainingMapNativeFixture.DisposeFixture();
        if(camera){camera.targetTexture=null;UnityEngine.Object.Destroy(camera.gameObject);camera=null;}
        if(texture){texture.Release();UnityEngine.Object.Destroy(texture);texture=null;}
    }
    private static void Fail(Exception e)
    {
        report.state="failed";report.failure=(report.failure??"")+"\n"+e;Save();Release();phase=90;
        if(EditorApplication.isPlayingOrWillChangePlaymode)EditorApplication.isPlaying=false;else Finish();
    }
    private static void Finish()
    {
        EditorApplication.update-=Tick;
        EditorSettings.enterPlayModeOptions=oldOptions;EditorSettings.enterPlayModeOptionsEnabled=oldEnabled;
        Save();EditorApplication.Exit(report.state=="captured-awaiting-visual-review"?0:1);
    }
    private static void Save()=>File.WriteAllText(Path.Combine(output,"batch.json"),JsonUtility.ToJson(report,true));
}
