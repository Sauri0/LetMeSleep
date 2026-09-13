using System;
using System.IO;
using System.Linq;
using LetMeSleep.Content.Environment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace LetMeSleep.Content.Editor.Higgsfield
{
    // Manual evidence collection on newly imported Higgsfield maps, never auto-run.
    public static class HiggsfieldNativeReview
    {
        [Serializable] public class Configuration
        {
            public string prefab, output;
            public float[] camera, target;
            public bool night;
            public float orthoSize;
            public float restoreShadowDistance = -1;
            public int restoreAntiAliasing = -1;
            public bool savePresentationScene;
            public LocalLight[] lights;
        }
        [Serializable] public class LocalLight { public string name; public float[] position, color; public float intensity, range; public bool shadows; }
        [Serializable] class Marker
        {
            public string name, ground;
            public Vector3 position, floor;
            public bool groundFound, standingCapsuleBlocked;
        }
        [Serializable] class MeshInfo
        {
            public string name;
            public Vector3 min, max;
            public int vertices, shapes;
        }
        [Serializable] class Report
        {
            public string unityVersion, mapId;
            public int renderers, colliders;
            public Marker[] humanSpawns;
            public MeshInfo[] meshes;
            public string[] pending;
        }
        public static void Run()
        {
            RenderPipelineAsset previousPipeline=QualitySettings.renderPipeline, temporaryPipeline=null;
            float previousShadowDistance=QualitySettings.shadowDistance;
            int previousAntiAliasing=QualitySettings.antiAliasing;
            bool failed=false;
            try
            {
                var args = System.Environment.GetCommandLineArgs();
                int at = Array.IndexOf(args, "-higgsfieldReview");
                if (at < 0 || at + 1 >= args.Length) throw new Exception("-higgsfieldReview config.json required");
                var config = JsonUtility.FromJson<Configuration>(File.ReadAllText(args[at+1]));
                if(config.restoreShadowDistance>=0) previousShadowDistance=config.restoreShadowDistance;
                if(config.restoreAntiAliasing>=0) previousAntiAliasing=config.restoreAntiAliasing;
                if (!config.prefab.StartsWith(HiggsfieldEnvironmentImporter.Output + "/hf-", StringComparison.Ordinal)) throw new Exception("Higgsfield map asset required");
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(config.prefab);
                if (prefab == null) throw new Exception("Map prefab missing: " + config.prefab);
                Directory.CreateDirectory(config.output);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                var map = root.GetComponent<EnvironmentMapDefinition>();
                Physics.SyncTransforms();
                int mask = root.GetComponentsInChildren<Collider>().Aggregate(0,(m,c)=>m | (1 << c.gameObject.layer));
                var markers = map.HumanSpawnPoints.Select(t => {
                    var m = new Marker { name=t.name, position=t.position };
                    if (Physics.Raycast(t.position + Vector3.up * .4f, Vector3.down, out RaycastHit hit, 5f, mask))
                    {
                        m.groundFound=true; m.ground=hit.collider.name; m.floor=hit.point;
                        m.standingCapsuleBlocked=Physics.CheckCapsule(hit.point+Vector3.up*.36f,hit.point+Vector3.up*1.45f,.3f,mask);
                    }
                    return m;
                }).ToArray();
                var report = new Report {
                    unityVersion=Application.unityVersion, mapId=map.MapId,
                    renderers=root.GetComponentsInChildren<Renderer>().Length,
                    colliders=root.GetComponentsInChildren<Collider>().Length,
                    humanSpawns=markers,
                    meshes=root.GetComponentsInChildren<Renderer>().Select(r=>{
                        var sk=r as SkinnedMeshRenderer; var mf=r.GetComponent<MeshFilter>();
                        var mesh=sk!=null?sk.sharedMesh:mf.sharedMesh;
                        return new MeshInfo{name=r.name,min=r.bounds.min,max=r.bounds.max,vertices=mesh.vertexCount,shapes=mesh.blendShapeCount};
                    }).ToArray(),
                    pending=new[]{"Full route traversal and role capacity", "Runtime water motion", "Gameplay integration", "Visual review of exported screenshot"}
                };
                File.WriteAllText(Path.Combine(config.output,"unity-native-review.json"),JsonUtility.ToJson(report,true));
                RenderSettings.ambientMode=AmbientMode.Trilight;
                RenderSettings.ambientSkyColor=config.night?new Color(.16f,.23f,.38f):new Color(.6f,.72f,.85f);
                RenderSettings.ambientEquatorColor=config.night?new Color(.12f,.16f,.23f):new Color(.45f,.5f,.55f);
                RenderSettings.ambientGroundColor=config.night?new Color(.06f,.08f,.13f):new Color(.28f,.3f,.26f);
                var light=new GameObject("Review_Key").AddComponent<Light>();
                light.type=LightType.Directional; light.intensity=config.night?.8f:1.05f;
                light.color=config.night?new Color(.5f,.65f,1):new Color(1,.94f,.82f);
                light.transform.rotation=Quaternion.Euler(48,-35,0);
                light.shadows=LightShadows.Soft;
                RenderSettings.sun=light;
                foreach(var source in config.lights??Array.Empty<LocalLight>())
                {
                    if(source.intensity<0 || source.range<=0 || source.position?.Length!=3 || source.color?.Length!=3)throw new Exception("Invalid explicit local light");
                    var local=new GameObject(source.name).AddComponent<Light>();
                    local.type=LightType.Point; local.transform.position=V(source.position);
                    local.color=new Color(source.color[0],source.color[1],source.color[2]);
                    local.intensity=source.intensity; local.range=source.range;
                    local.shadows=source.shadows?LightShadows.Soft:LightShadows.None;
                }
                // Capture-only pipeline copy. Never persist preview shadow/MSAA settings into the game's pipeline asset.
                var sourcePipeline=previousPipeline!=null?previousPipeline:GraphicsSettings.defaultRenderPipeline;
                if(sourcePipeline!=null)
                {
                    temporaryPipeline=UnityEngine.Object.Instantiate(sourcePipeline);
                    var serialized=new SerializedObject(temporaryPipeline);
                    var shadow=serialized.FindProperty("m_ShadowDistance"); if(shadow!=null)shadow.floatValue=250;
                    var msaa=serialized.FindProperty("m_MSAA"); if(msaa!=null)msaa.intValue=4;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    QualitySettings.renderPipeline=temporaryPipeline;
                }
                var cam=new GameObject("Review_Camera").AddComponent<Camera>();
                cam.transform.position=V(config.camera); cam.transform.LookAt(V(config.target));
                cam.nearClipPlane=.1f; cam.farClipPlane=1000; cam.fieldOfView=48;
                if(config.orthoSize>0){cam.orthographic=true;cam.orthographicSize=config.orthoSize;}
                cam.clearFlags=CameraClearFlags.SolidColor; cam.backgroundColor=new Color(.19f,.35f,.46f);
                var rt=new RenderTexture(1600,1000,24); rt.Create(); cam.targetTexture=rt;
                cam.Render();
                var previous=RenderTexture.active; RenderTexture.active=rt;
                var pixels=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
                pixels.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0); pixels.Apply();
                File.WriteAllBytes(Path.Combine(config.output,"unity-overview.png"),pixels.EncodeToPNG());
                RenderTexture.active=previous; cam.targetTexture=null;
                UnityEngine.Object.DestroyImmediate(pixels); rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
                if(config.savePresentationScene)
                {
                    string scenePath=Path.ChangeExtension(config.prefab.Replace("/Prefabs/","/Scenes/"),".unity");
                    if(!EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),scenePath))throw new Exception("Presentation scene save failed");
                }
                Debug.Log("HIGGSFIELD_NATIVE_REVIEW_DONE " + config.output);
            }
            catch(Exception e) { Debug.LogException(e); failed=true; }
            finally
            {
                QualitySettings.renderPipeline=previousPipeline;
                QualitySettings.shadowDistance=previousShadowDistance;
                QualitySettings.antiAliasing=previousAntiAliasing;
                if(temporaryPipeline!=null)UnityEngine.Object.DestroyImmediate(temporaryPipeline);
            }
            if(failed)EditorApplication.Exit(1);
        }
        static Vector3 V(float[] a) => new Vector3(a[0],a[1],a[2]);
    }
}
