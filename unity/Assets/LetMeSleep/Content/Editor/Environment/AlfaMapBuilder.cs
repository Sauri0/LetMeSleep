using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LetMeSleep.Content.Environment;
using LetMeSleep.Gameplay;
using LetMeSleep.Gameplay.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace LetMeSleep.Content.Editor
{
    public static partial class AlfaMapBuilder
    {
        const string Output="Assets/LetMeSleep/Content/Environment/AlfaMaps";
        const string Sample=EnvironmentSampleBuilder.Output;
        public const string HouseScene=Output+"/Scenes/HousePatio.unity";
        public const string LobbyScene=Output+"/Scenes/PrivateLobby.unity";
        [Serializable] public class Sources { public Model house_alfa_static,lobby_alfa_static,furniture_kit_alfa; }
        [Serializable] public class Model { public string root; public float[] min,max; public Mat[] materials; public Box[] box_colliders; public MeshCollision[] mesh_colliders; public Root[] roots; }
        [Serializable] public class Mat { public string name; public float[] color; }
        [Serializable] public class Box { public string node,source,surface; public float[] center,size; }
        [Serializable] public class MeshCollision { public string node,surface; }
        [Serializable] public class Root { public string name; public float[] position; }
        [Serializable] public class Plan { public Zone[] zones; public Portal[] portals; public Lobby lobby; }
        [Serializable] public class Zone { public string id,kind,floor; public float[] min,max; }
        [Serializable] public class Portal { public string id,from,to; public float[] center,normal; public float width,height,initial_degrees; public bool door; }
        [Serializable] public class Lobby { public Spawn[] spawns; public Zone bounds; public float[] source_shell_scale; }
        [Serializable] public class Spawn { public string id; public float[] position,forward; }
        [Serializable] public class Receipt { public string unityVersion,utc,houseScene,lobbyScene,houseContentHash,lobbyContentHash; public int houseMeshes,houseColliders,doors,toolPickups,lobbyMeshes,lobbyColliders; public string[] verified,pending; }
        static readonly Dictionary<string,GameObject> Kit=new Dictionary<string,GameObject>();
        static Dictionary<string,Material> materials;
        static Transform F(GameObject root,string name)=>EnvironmentSampleBuilder.Find(root,name);
        static Vector3 V(float[] p)=>new Vector3(p[0],p[1],p[2]);
        static void Need(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}

        [MenuItem("Let Me Sleep/Environment/Build Alfa Maps")]
        public static void BuildAlfaMaps()
        {
            Need(!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling,"Requires idle Edit mode.");
            for(int i=0;i<SceneManager.sceneCount;i++)
                Need(SceneManager.GetSceneAt(i).path!=HouseScene && SceneManager.GetSceneAt(i).path!=LobbyScene,"Close generated map scenes before rebuilding; changes preserved.");
            string repository=Path.GetFullPath(Path.Combine(Application.dataPath,"../.."));
            string sources=Path.Combine(repository,"art_source/unity/environments/alfa_maps");
            string planFile=Path.Combine(repository,"art_source/unity/environments/room_sample/house_layout_plan.json");
            var manifest=JsonUtility.FromJson<Sources>(File.ReadAllText(Path.Combine(sources,"source_manifest.json")));
            var plan=JsonUtility.FromJson<Plan>(File.ReadAllText(planFile));
            Need(manifest.house_alfa_static!=null && plan.zones.Length==14,"Unexpected alpha source/plan.");
            if(AssetDatabase.LoadAssetAtPath<GameObject>(Sample+"/Prefabs/RoomSample.prefab")==null)EnvironmentSampleBuilder.Build();
            foreach(string dir in new[]{"Models","Meshes","Materials","Prefabs","Scenes","Data"})EnvironmentSampleBuilder.EnsureFolder(Output+"/"+dir);
            Copy(planFile,Output+"/Data/house_layout_plan.json");
            Copy(Path.Combine(sources,"source_manifest.json"),Output+"/Data/source_manifest.json");
            materials=MakeMaterials(manifest.house_alfa_static.materials);
            Import(sources,"house_alfa_static",manifest.house_alfa_static);
            Import(sources,"lobby_alfa_static",manifest.lobby_alfa_static);
            Import(sources,"furniture_kit_alfa",manifest.furniture_kit_alfa);
            Scene previous=SceneManager.GetActiveScene();
            Scene scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                BuildKit(manifest.furniture_kit_alfa);
                var house=new GameObject("HousePatio");
                InstantiateStatic("house_alfa_static",manifest.house_alfa_static,house.transform);
                FurnishHouse(house);
                AddDoors(house,plan);
                var data=house.AddComponent<EnvironmentMapDefinition>();data.MapId="house-patio-v1";data.ContentHash=ContentHash(repository,data.MapId);
                string houseHash=data.ContentHash;
                data.SpatialData=AssetDatabase.LoadAssetAtPath<TextAsset>(Output+"/Data/house_layout_plan.json");
                data.PlayBounds=new Bounds(new Vector3(6.4f,5,8.7f),new Vector3(13.8f,10,21.4f));
                AddWorldBoundary(house,data.PlayBounds);
                AddHouseAnchors(house,data,plan);
                AddToolPickups(data);
                BindGameplay(house,10000,1);
                CheckSpawns(house,data.HumanSpawnPoints,.25f,1.72f);
                CheckSpawns(house,data.MosquitoSpawnPoints,.055f,.11f,true);
                int houseMeshes=house.GetComponentsInChildren<MeshRenderer>().Length;
                int houseColliders=house.GetComponentsInChildren<Collider>().Length;
                int doors=house.GetComponentsInChildren<GameplayDoor>().Length;
                Need(doors==9,"Expected nine real alpha doors.");
                SavePrefabAndInstantiate(ref house,Output+"/Prefabs/HousePatio.prefab",scene);
                AddReviewLights(plan,false);
                AddCamera(new Vector3(6.06f,1.53f,2),new Vector3(6.06f,1.53f,8),"Review_House_Human");
                Need(EditorSceneManager.SaveScene(scene,HouseScene),"House scene save failed.");
                EditorSceneManager.CloseScene(scene,true);
                scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(scene);
                var lobby=new GameObject("PrivateLobby");
                var lobbyShell=InstantiateStatic("lobby_alfa_static",manifest.lobby_alfa_static,lobby.transform);
                lobbyShell.transform.localScale=V(plan.lobby.source_shell_scale);
                FurnishLobby(lobby);
                var lobbyData=lobby.AddComponent<EnvironmentMapDefinition>();lobbyData.MapId="private-lobby-v1";lobbyData.ContentHash=ContentHash(repository,lobbyData.MapId);lobbyData.SpatialData=AssetDatabase.LoadAssetAtPath<TextAsset>(Output+"/Data/house_layout_plan.json");
                string lobbyHash=lobbyData.ContentHash;
                lobbyData.PlayBounds=new Bounds((V(plan.lobby.bounds.min)+V(plan.lobby.bounds.max))*.5f,V(plan.lobby.bounds.max)-V(plan.lobby.bounds.min));
                var spawnRoot=Child(lobby.transform,"Spawns");
                lobbyData.LobbySpawnPoints=plan.lobby.spawns.Select(p=>{
                    var t=Child(spawnRoot,p.id);t.localPosition=V(p.position)+Vector3.up*.02f;t.rotation=Quaternion.LookRotation(V(p.forward));return t;}).ToArray();
                lobbyData.HumanSpawnPoints=Array.Empty<Transform>();lobbyData.MosquitoSpawnPoints=Array.Empty<Transform>();
                lobbyData.ToolPickupPoints=Array.Empty<Transform>();
                lobbyData.PresentationAnchors=Child(lobby.transform,"PresentationAnchors");
                Anchor(lobbyData.PresentationAnchors,"AudioZone_Lobby",new Vector3(0,1.6f,0));
                Anchor(lobbyData.PresentationAnchors,"ReflectionVolume_Lobby",new Vector3(0,1.6f,0));
                Anchor(lobbyData.PresentationAnchors,"LightAnchor_Lobby",new Vector3(0,2.9f,0));
                AddLobbyPresentationAnchors(lobbyData);
                BindGameplay(lobby,20000,100);
                CheckSpawns(lobby,lobbyData.LobbySpawnPoints,.25f,1.72f);
                CheckLobbyDressing(lobby,lobbyData);
                int lobbyMeshes=lobby.GetComponentsInChildren<MeshRenderer>().Length,lobbyColliders=lobby.GetComponentsInChildren<Collider>().Length;
                SavePrefabAndInstantiate(ref lobby,Output+"/Prefabs/PrivateLobby.prefab",scene);
                CheckLobbyDressing(lobby,lobby.GetComponent<EnvironmentMapDefinition>());
                AddReviewLights(plan,true);AddCamera(new Vector3(0,1.8f,-3.3f),new Vector3(0,1.2f,1),"Review_Lobby");
                Need(EditorSceneManager.SaveScene(scene,LobbyScene),"Lobby scene save failed.");
                AssetDatabase.SaveAssets();
                var receipt=new Receipt{unityVersion=Application.unityVersion,utc=DateTime.UtcNow.ToString("o"),houseScene=HouseScene,lobbyScene=LobbyScene,houseContentHash=houseHash,lobbyContentHash=lobbyHash,
                    houseMeshes=houseMeshes,houseColliders=houseColliders,doors=doors,toolPickups=7,lobbyMeshes=lobbyMeshes,lobbyColliders=lobbyColliders,
                    verified=new[]{"Source bounds and explicit FBX Z conversion","Unique nonzero GameplaySurface IDs","Nine doors start open at100deg; closed reference and collider pose survive prefab reload","Seven unique flyswatter pickups with non-perchable interaction triggers","5 human / 16 mosquito / 16 lobby spawn clearances against geometry","Lobby dressing preserves central reserve and 1.8m circulation; menu camera/stages serialized","Separate house/patio and lobby scenes; no duplicated sample shell"},
                    pending=new[]{"Visual lighting and UV2 bake validation","Controller stair/door traversal and camera playtest","Runtime bots/pickups and online round integration","Performance measurement"}};
                File.WriteAllText(Path.Combine(repository,"docs/unity/environment/ALFA-MAPS-IMPORT-RECEIPT.json"),JsonUtility.ToJson(receipt,true)+"\n");
                Debug.Log("LMS_ALFA_MAPS_BUILT "+JsonUtility.ToJson(receipt));
            }
            finally{if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);if(scene.IsValid()&&scene.isLoaded)EditorSceneManager.CloseScene(scene,true);}
        }
        static void Copy(string source,string asset){File.Copy(source,Path.Combine(Application.dataPath,"..",asset),true);AssetDatabase.ImportAsset(asset,ImportAssetOptions.ForceSynchronousImport);}
        static Dictionary<string,Material> MakeMaterials(Mat[] specs)
        {
            Shader shader=Shader.Find("Universal Render Pipeline/Lit");Need(shader!=null,"URP/Lit required.");var result=new Dictionary<string,Material>();
            foreach(var spec in specs){var material=AssetDatabase.LoadAssetAtPath<Material>(Sample+"/Materials/"+spec.name+".mat");
                string path=Output+"/Materials/"+spec.name+".mat";if(material==null)material=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(material==null){material=new Material(shader){name=spec.name,enableInstancing=true};material.SetColor("_BaseColor",new Color(spec.color[0],spec.color[1],spec.color[2],spec.color[3]));material.SetFloat("_Smoothness",.22f);material.SetFloat("_Metallic",spec.name=="Iron"?.45f:0);AssetDatabase.CreateAsset(material,path);}result.Add(spec.name,material);}
            return result;
        }
        static void Import(string source,string file,Model spec)
        {
            string path=Output+"/Models/"+file+".fbx";Copy(Path.Combine(source,file+".fbx"),path);
            var imp=(ModelImporter)AssetImporter.GetAtPath(path);imp.globalScale=1;imp.useFileScale=true;imp.bakeAxisConversion=true;imp.preserveHierarchy=true;
            imp.importAnimation=false;imp.animationType=ModelImporterAnimationType.None;imp.importCameras=false;imp.importLights=false;imp.addCollider=false;imp.isReadable=true;
            imp.meshCompression=ModelImporterMeshCompression.Off;imp.importNormals=ModelImporterNormals.Import;imp.generateSecondaryUV=true;
            imp.secondaryUVMarginMethod=ModelImporterSecondaryUVMarginMethod.Calculate;imp.secondaryUVMinLightmapResolution=16;imp.secondaryUVMinObjectScale=1;
            imp.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;imp.materialLocation=ModelImporterMaterialLocation.InPrefab;
            foreach(var pair in materials)imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),pair.Key),pair.Value);imp.SaveAndReimport();
        }
        static GameObject InstantiateStatic(string file,Model spec,Transform parent)
        {
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(Output+"/Models/"+file+".fbx");Need(model!=null,"Missing imported "+file);
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(model,parent.gameObject.scene);PrefabUtility.UnpackPrefabInstance(instance,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            instance.name=file;instance.transform.SetParent(parent,false);
            Bounds bounds=RendererBounds(instance);Vector3 lo=V(spec.min),hi=V(spec.max);
            Need(Vector3.Distance(bounds.min,new Vector3(lo.x,lo.y,-hi.z))<.003f&&Vector3.Distance(bounds.max,new Vector3(hi.x,hi.y,-lo.z))<.003f,"Unexpected raw FBX axes: "+file);
            EnvironmentSampleBuilder.BakeModelFrame(instance,file,Output+"/Meshes");
            foreach(var r in instance.GetComponentsInChildren<MeshRenderer>()){
                r.sharedMaterials=r.sharedMaterials.Select(m=>materials[m.name]).ToArray();r.receiveGI=ReceiveGI.Lightmaps;r.lightProbeUsage=LightProbeUsage.Off;r.reflectionProbeUsage=ReflectionProbeUsage.BlendProbes;
                r.shadowCastingMode=r.sharedMaterials.Any(m=>m.name=="Glass_Blue_Opaque")?ShadowCastingMode.Off:ShadowCastingMode.On;r.receiveShadows=true;
                GameObjectUtility.SetStaticEditorFlags(r.gameObject,StaticEditorFlags.ContributeGI|StaticEditorFlags.OccludeeStatic);
                if(r.name=="HouseShell"||r.name=="LobbyShell")GameObjectUtility.SetStaticEditorFlags(r.gameObject,GameObjectUtility.GetStaticEditorFlags(r.gameObject)|StaticEditorFlags.OccluderStatic);
                var mesh=r.GetComponent<MeshFilter>().sharedMesh;Need(mesh.uv2.Length==mesh.vertexCount,"Missing UV2 "+r.name);
            }
            foreach(var box in spec.box_colliders){Transform owner=box.node==spec.root?instance.transform:F(instance,box.node);var child=Child(owner,"Collider_"+box.source);child.gameObject.layer=EnvironmentSampleBuilder.Layer("WorldStatic");var c=child.gameObject.AddComponent<BoxCollider>();c.center=V(box.center);c.size=V(box.size);}
            foreach(var mesh in spec.mesh_colliders){var visual=F(instance,mesh.node);var child=Child(visual,"Collider_"+mesh.node);child.gameObject.layer=EnvironmentSampleBuilder.Layer("WorldStatic");var c=child.gameObject.AddComponent<MeshCollider>();c.sharedMesh=visual.GetComponent<MeshFilter>().sharedMesh;c.convex=false;}
            Bounds final=RendererBounds(instance);Need(Vector3.Distance(final.min,lo)<.003f&&Vector3.Distance(final.max,hi)<.003f,"Corrected bounds mismatch "+file);
            return instance;
        }
        static Bounds RendererBounds(GameObject root){var renderers=root.GetComponentsInChildren<Renderer>();Need(renderers.Length>0,"Empty geometry");var b=renderers[0].bounds;foreach(var r in renderers)b.Encapsulate(r.bounds);return b;}
        static Transform Child(Transform parent,string name){var o=new GameObject(name);o.transform.SetParent(parent,false);return o.transform;}
        static Transform Anchor(Transform parent,string name,Vector3 p){var t=Child(parent,name);t.localPosition=p;return t;}
        static void BuildKit(Model spec)
        {
            Kit.Clear();var holder=new GameObject("KitBuildTemporary");
            try{var model=InstantiateStatic("furniture_kit_alfa",spec,holder.transform);
                foreach(var root in spec.roots){var piece=Object.Instantiate(F(model,root.name).gameObject);piece.name=root.name;piece.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
                    if(root.name=="Kit_Pine"||root.name=="Kit_Sofa"){var lod=piece.AddComponent<LODGroup>();lod.fadeMode=LODFadeMode.None;lod.SetLODs(new[]{new LOD(0,piece.GetComponentsInChildren<Renderer>())});lod.RecalculateBounds();}
                    string path=Output+"/Prefabs/"+root.name+".prefab";Need(PrefabUtility.SaveAsPrefabAsset(piece,path)!=null,"Kit save failed");Object.DestroyImmediate(piece);Kit.Add(root.name,AssetDatabase.LoadAssetAtPath<GameObject>(path));}}
            finally{Object.DestroyImmediate(holder);}
        }
        static GameObject Place(string kind,string name,Vector3 position,float yaw,Transform parent,bool sample=false)
        {
            GameObject source=sample?F(AssetDatabase.LoadAssetAtPath<GameObject>(Sample+"/Prefabs/RoomSample.prefab"),kind).gameObject:Kit["Kit_"+kind];
            var o=Object.Instantiate(source,parent);o.name=name;o.transform.localPosition=position;o.transform.localRotation=Quaternion.Euler(0,yaw,0);o.transform.localScale=Vector3.one;return o;
        }
        static void FurnishHouse(GameObject house)
        {
            var p=Child(house.transform,"Furnishings");
            Place("Sofa","Living_Sofa",new Vector3(.85f,0,2.35f),-90,p);Place("Table","Living_Table",new Vector3(2.4f,0,2.35f),90,p);Place("Shelf","Living_Shelf",new Vector3(1.9f,0,.45f),0,p);
            Place("Table","Dining_Table",new Vector3(10.8f,0,2.5f),0,p);
            Place("Furniture_Chair","Dining_Chair_S",new Vector3(10.8f,0,1.6f),0,p,true);Place("Furniture_Chair","Dining_Chair_N",new Vector3(10.8f,0,3.4f),180,p,true);Place("Furniture_Chair","Dining_Chair_W",new Vector3(9.6f,0,2.5f),90,p,true);
            Place("Counter","Kitchen_Counter_N",new Vector3(10.4f,0,10.79f),0,p);Place("Counter","Kitchen_Counter_E",new Vector3(12.25f,0,9.25f),90,p);Place("Stove","Kitchen_Stove",new Vector3(8.95f,0,10.81f),0,p);Place("Fridge","Kitchen_Fridge",new Vector3(7.55f,0,10.6f),0,p);
            var a=Child(p,"BedroomA");a.localPosition=new Vector3(4.98f,3,4.58f);a.localRotation=Quaternion.Euler(0,180,0);
            foreach(string name in new[]{"Furniture_Bed","Furniture_Nightstand","Furniture_Desk","Furniture_Chair"}){
                var src=F(AssetDatabase.LoadAssetAtPath<GameObject>(Sample+"/Prefabs/RoomSample.prefab"),name);Place(name,name,src.position,0,a,true);}
            Place("Furniture_Bed","BedroomB_Bed",new Vector3(11.4f,3,1.6f),180,p,true);Place("Furniture_Nightstand","BedroomB_Nightstand",new Vector3(10.05f,3,.85f),180,p,true);Place("Furniture_Desk","BedroomB_Desk",new Vector3(7.55f,3,1.4f),90,p,true);Place("Furniture_Chair","BedroomB_Chair",new Vector3(8.5f,3,1.4f),-90,p,true);
            Place("Vanity","Bathroom_Vanity",new Vector3(7.59f,3,8.35f),-90,p);Place("Toilet","Bathroom_Toilet",new Vector3(9.05f,3,10.72f),0,p);
            Place("Washer","Utility_Washer",new Vector3(12.2f,3,10.75f),0,p);Place("Counter","Utility_Counter",new Vector3(10.55f,3,10.79f),0,p);Place("Shelf","Utility_Shelf",new Vector3(12.36f,3,8.75f),90,p);
            Place("PatioBench","Patio_Bench",new Vector3(10,0,15.8f),0,p);Place("Pine","Patio_Pine_W",new Vector3(2,0,16.5f),0,p);Place("Pine","Patio_Pine_E",new Vector3(10.8f,0,18.3f),0,p);
        }
        static void AddDoors(GameObject house,Plan plan)
        {
            var parent=Child(house.transform,"Doors");var source=AssetDatabase.LoadAssetAtPath<GameObject>(Sample+"/Prefabs/Door_01.prefab");Need(source!=null,"Door sample missing");
            foreach(var portal in plan.portals.Where(p=>p.door)){
                var door=(GameObject)PrefabUtility.InstantiatePrefab(source,parent.gameObject.scene);door.name=portal.id;door.transform.SetParent(parent,false);
                Vector3 normal=V(portal.normal),center=V(portal.center)-Vector3.up*1.1f;
                door.transform.rotation=Quaternion.LookRotation(normal);door.transform.position=center+normal*.09f-door.transform.rotation*new Vector3(1.12f,0,0);
                var component=door.AddComponent<GameplayDoor>();component.Hinge=F(door,"Door_01_Hinge");component.Leaf=component.Hinge.GetComponentInChildren<BoxCollider>();component.Handle=F(door,"Socket_Door_Use");component.OpenSign=-1;component.OpenDegrees=100;component.InitialDegrees=portal.initial_degrees;
                Need(Mathf.Abs(component.InitialDegrees-100)<.001f,"Alpha map doors must start open");
                component.SetAuthoredClosedRotation(component.Hinge.localRotation);
                PrefabUtility.RecordPrefabInstancePropertyModifications(door.transform);PrefabUtility.RecordPrefabInstancePropertyModifications(door);
            }
        }
        static void AddHouseAnchors(GameObject house,EnvironmentMapDefinition data,Plan plan)
        {
            var spawn=Child(house.transform,"Spawns");
            var human=new[]{new Vector3(6.06f,.02f,1.2f),new Vector3(6.06f,.02f,3.2f),new Vector3(6.06f,.02f,7.7f),new Vector3(6.06f,.02f,9.9f),new Vector3(6.06f,3.02f,2.5f)};
            data.HumanSpawnPoints=human.Select((p,i)=>Anchor(spawn,"Human_"+i.ToString("00"),p)).ToArray();var mos=new List<Vector3>();
            foreach(float x in new[]{1f,4.3f,8f,12f})foreach(float z in new[]{1f,3.8f})mos.Add(new Vector3(x,2.4f,z));
            foreach(float x in new[]{8f,12f})foreach(float z in new[]{7.5f,10.5f})mos.Add(new Vector3(x,2.4f,z));
            mos.AddRange(new[]{new Vector3(2,5.4f,2.5f),new Vector3(10,5.4f,2.5f),new Vector3(2,1.8f,17),new Vector3(10,1.8f,17)});
            data.MosquitoSpawnPoints=mos.Select((p,i)=>Anchor(spawn,"Mosquito_"+i.ToString("00"),p)).ToArray(); // sphere centre, exactly as GameplayActorProxy expects
            data.LobbySpawnPoints=Array.Empty<Transform>();data.PresentationAnchors=Child(house.transform,"PresentationAnchors");
            foreach(var zone in plan.zones){Vector3 center=(V(zone.min)+V(zone.max))*.5f;Anchor(data.PresentationAnchors,"AudioZone_"+zone.id,center);Anchor(data.PresentationAnchors,"ReflectionVolume_"+zone.id,center);Anchor(data.PresentationAnchors,"LightAnchor_"+zone.id,new Vector3(center.x,V(zone.max).y-.3f,center.z));}
            Anchor(data.PresentationAnchors,"CameraCollision",Vector3.zero);
            var sockets=Child(house.transform,"InteractionSockets");
            data.ToolPickupPoints=new[]{
                Anchor(sockets,"Pickup_KitchenCounter_A",new Vector3(10.05f,.885f,10.66f)),Anchor(sockets,"Pickup_KitchenCounter_B",new Vector3(10.75f,.885f,10.66f)),
                Anchor(sockets,"Pickup_DiningTable_A",new Vector3(10.4f,.815f,2.45f)),Anchor(sockets,"Pickup_DiningTable_B",new Vector3(11.2f,.815f,2.45f)),
                Anchor(sockets,"Pickup_LivingTable",new Vector3(2.4f,.815f,2.35f)),Anchor(sockets,"Pickup_BedroomANightstand",new Vector3(2.755f,3.655f,.55f)),
                Anchor(sockets,"Pickup_UtilityCounter",new Vector3(10.55f,3.885f,10.66f))};
            Anchor(sockets,"TaskFuture_Utility",new Vector3(11.1f,3.9f,10.4f));
        }
        static void AddWorldBoundary(GameObject house,Bounds bounds)
        {
            var root=Child(house.transform,"WorldBoundary_NoPerch");Vector3 min=bounds.min,max=bounds.max,size=bounds.size;
            var definitions=new[]{new Box{center=new[]{min.x-.1f,bounds.center.y,bounds.center.z},size=new[]{.2f,size.y,size.z}},new Box{center=new[]{max.x+.1f,bounds.center.y,bounds.center.z},size=new[]{.2f,size.y,size.z}},new Box{center=new[]{bounds.center.x,bounds.center.y,min.z-.1f},size=new[]{size.x,size.y,.2f}},new Box{center=new[]{bounds.center.x,bounds.center.y,max.z+.1f},size=new[]{size.x,size.y,.2f}},new Box{center=new[]{bounds.center.x,max.y+.1f,bounds.center.z},size=new[]{size.x,.2f,size.z}}};
            for(int i=0;i<definitions.Length;i++){var t=Child(root,"Boundary_"+i);t.gameObject.layer=EnvironmentSampleBuilder.Layer("WorldStatic");var c=t.gameObject.AddComponent<BoxCollider>();c.center=V(definitions[i].center);c.size=V(definitions[i].size);}
        }
        static void AddToolPickups(EnvironmentMapDefinition map)
        {
            Need(map.ToolPickupPoints.Length==7,"Expected seven tool markers");
            for(int i=0;i<map.ToolPickupPoints.Length;i++){
                var marker=map.ToolPickupPoints[i];var pickup=marker.gameObject.AddComponent<GameplayToolPickup>();
                pickup.PickupId=(uint)(1001+i);pickup.ToolId=GameplayTools.Flyswatter;pickup.VisualRoot=null;
                marker.gameObject.layer=EnvironmentSampleBuilder.Layer("WorldDynamic");pickup.Initialize();
                Need(pickup.InteractionCollider!=null&&pickup.InteractionCollider.isTrigger,"Pickup interaction must be a trigger");
                Need(pickup.InteractionCollider.GetComponent<GameplaySurface>()==null,"Pickups must not be perch surfaces");
            }
            var pickups=map.GetComponentsInChildren<GameplayToolPickup>();
            Need(pickups.Length==7&&pickups.Select(p=>p.PickupId).Distinct().Count()==7,"Pickup identities must be unique");
            foreach(var pickup in pickups)CheckPickupDefinition(pickup);
        }
        static void CheckPickupDefinition(GameplayToolPickup pickup)
        {
            var definition=pickup.Definition;
            Need(definition.PickupId==pickup.PickupId&&definition.ToolId==GameplayTools.Flyswatter,"Pickup definition identity changed");
            Need(Vector3.Distance(definition.Position.ToUnity(),pickup.transform.position)<.0001f&&Quaternion.Angle(definition.Rotation.ToUnity(),pickup.transform.rotation)<.01f,"Pickup definition pose differs from grip marker");
            Need(Vector3.Distance(pickup.transform.up,Vector3.up)<.0001f&&Vector3.Distance(pickup.transform.forward,Vector3.forward)<.0001f,"Pickup grip axes must be +Y up and +Z along tool");
            Need(pickup.InteractionCollider.GetComponent<GameplaySurface>()==null,"Pickup trigger became a perch surface");
        }
        static string Hierarchy(Transform t){string p=t.name;while(t.parent!=null){t=t.parent;p=t.name+"/"+p;}return p;}
        static void BindGameplay(GameObject map,uint start,uint doorStart)
        {
            CanonicalizeColliderPaths(map);
            uint id=start;foreach(var collider in map.GetComponentsInChildren<Collider>(true).Where(c=>!c.isTrigger).OrderBy(c=>Hierarchy(c.transform),StringComparer.Ordinal)){
                var surface=collider.GetComponent<GameplaySurface>()??collider.gameObject.AddComponent<GameplaySurface>();surface.SurfaceId=id++;surface.Revision=1;surface.CanPerch=!Hierarchy(collider.transform).Contains("WorldBoundary_NoPerch");}
            foreach(var door in map.GetComponentsInChildren<GameplayDoor>().OrderBy(d=>d.name,StringComparer.Ordinal)){
                door.DoorId=doorStart++;Need(door.Hinge!=null&&door.Leaf!=null,"Door contract incomplete");door.SurfaceId=door.Leaf.GetComponent<GameplaySurface>().SurfaceId;
                var definition=door.Definition;Need(Mathf.Abs(definition.LeafCenterLocal.X-.535f)<.001f,"Gameplay door centre differs from real leaf");
                door.ApplyAngle(definition.InitialAngleRadians);
                PrefabUtility.RecordPrefabInstancePropertyModifications(door.Hinge);
                PrefabUtility.RecordPrefabInstancePropertyModifications(door);
                CheckDoorInitialPose(door);}
            var surfaces=map.GetComponentsInChildren<GameplaySurface>();Need(surfaces.All(s=>s.SurfaceId!=0)&&surfaces.Select(s=>s.SurfaceId).Distinct().Count()==surfaces.Length,"Duplicate/zero SurfaceId");
        }
        static void CanonicalizeColliderPaths(GameObject map)
        {
            // Legacy sample furniture named each leg collider identically. Give those
            // actual child objects deterministic names BEFORE assigning SurfaceIds.
            // Sorting by local box geometry makes the result independent of enumeration.
            var colliders=map.GetComponentsInChildren<Collider>(true);
            foreach(var c in colliders)Need(c.GetComponents<Collider>().Length==1,"Each collider needs its own child GameObject: "+Hierarchy(c.transform));
            foreach(var group in colliders.GroupBy(c=>Hierarchy(c.transform),StringComparer.Ordinal).Where(g=>g.Count()>1).ToArray())
            {
                var items=group.ToArray();Transform parent=items[0].transform.parent;
                Need(items.All(c=>c.transform.parent==parent),"Ambiguous duplicate ancestor names: "+group.Key);
                Need(items.All(c=>c is BoxCollider),"Duplicate non-box collider names require explicit source fix: "+group.Key);
                var ordered=items.OrderBy(BoxSignature,StringComparer.Ordinal).ToArray();
                Need(ordered.Select(BoxSignature).Distinct(StringComparer.Ordinal).Count()==ordered.Length,"Duplicate collider geometry: "+group.Key);
                string prefix=items[0].name+"__part_";
                for(int i=0;i<ordered.Length;i++){
                    string name=prefix+i.ToString("00",System.Globalization.CultureInfo.InvariantCulture);
                    Need(parent==null||parent.Find(name)==null,"Canonical collider name already exists: "+name);
                    ordered[i].gameObject.name=name;
                }
            }
            Need(colliders.Select(c=>Hierarchy(c.transform)).Distinct(StringComparer.Ordinal).Count()==colliders.Length,"Collider paths remain ambiguous after canonical naming");
        }
        static string BoxSignature(Collider collider)
        {
            var box=(BoxCollider)collider;Transform t=box.transform;
            float[] values={t.localPosition.x,t.localPosition.y,t.localPosition.z,t.localRotation.x,t.localRotation.y,t.localRotation.z,t.localRotation.w,t.localScale.x,t.localScale.y,t.localScale.z,box.center.x,box.center.y,box.center.z,box.size.x,box.size.y,box.size.z};
            return string.Join("|",values.Select(v=>v.ToString("R",System.Globalization.CultureInfo.InvariantCulture)));
        }
        static Bounds ColliderBounds(Collider c)
        {
            Bounds local;if(c is BoxCollider box)local=new Bounds(box.center,box.size);else if(c is MeshCollider mesh)local=mesh.sharedMesh.bounds;else throw new InvalidOperationException("Unsupported authored collider");
            var result=new Bounds(c.transform.TransformPoint(local.center),Vector3.zero);for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)result.Encapsulate(c.transform.TransformPoint(local.center+Vector3.Scale(local.extents,new Vector3(x,y,z))));return result;
        }
        static void CheckSpawns(GameObject map,Transform[] points,float radius,float height,bool sphereCenter=false)
        {
            // Conservative world AABB proof avoids querying unrelated open editor scenes.
            foreach(var point in points)foreach(var c in map.GetComponentsInChildren<Collider>().Where(c=>!c.isTrigger)){
                Bounds b=ColliderBounds(c);Vector3 p=point.position-(sphereCenter?Vector3.up*radius:Vector3.zero);float low=p.y+radius,high=p.y+height-radius;
                float dx=Mathf.Max(b.min.x-p.x,0,p.x-b.max.x),dz=Mathf.Max(b.min.z-p.z,0,p.z-b.max.z),dy=Mathf.Max(b.min.y-high,0,low-b.max.y);
                Need(dx*dx+dy*dy+dz*dz>=radius*radius-.000001f,"Spawn intersects geometry: "+point.name+" / "+Hierarchy(c.transform));}
        }
        static void CheckDoorInitialPose(GameplayDoor door)
        {
            var definition=door.Definition;
            Need(Mathf.Abs(definition.InitialAngleRadians-100*Mathf.Deg2Rad)<.0001f,"Door initial state is not fully open");
            var expected=definition.ClosedRotation.ToUnity()*Quaternion.AngleAxis(definition.InitialAngleRadians*Mathf.Rad2Deg*definition.OpenSign,Vector3.up);
            Need(Quaternion.Angle(door.Hinge.rotation,expected)<.01f,"Door geometry and initial state disagree: "+door.name);
            Need(Vector3.Distance(door.Leaf.transform.TransformPoint(door.Leaf.center),door.Hinge.TransformPoint(definition.LeafCenterLocal.ToUnity()))<.001f,"Open door collider differs from definition");
        }
        static void SavePrefabAndInstantiate(ref GameObject root,string path,Scene scene)
        {
            var before=root.GetComponentsInChildren<Collider>().Where(c=>!c.isTrigger).ToDictionary(c=>Hierarchy(c.transform),ColliderBounds);
            var ids=root.GetComponentsInChildren<Collider>().Where(c=>!c.isTrigger).ToDictionary(c=>Hierarchy(c.transform),c=>c.GetComponent<GameplaySurface>().SurfaceId);
            var triggers=root.GetComponentsInChildren<Collider>().Where(c=>c.isTrigger).ToDictionary(c=>Hierarchy(c.transform),ColliderBounds);
            var pickupIds=root.GetComponentsInChildren<GameplayToolPickup>().Select(p=>p.PickupId).OrderBy(id=>id).ToArray();
            var doorDefinitions=root.GetComponentsInChildren<GameplayDoor>().ToDictionary(d=>d.DoorId,d=>d.Definition);
            Need(PrefabUtility.SaveAsPrefabAsset(root,path)!=null,"Map prefab save failed");Object.DestroyImmediate(root);
            root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path),scene);
            var colliders=root.GetComponentsInChildren<Collider>().Where(c=>!c.isTrigger).ToArray();Need(colliders.Length==before.Count,"Prefab lost colliders");
            foreach(var collider in colliders){string key=Hierarchy(collider.transform);Need(before.ContainsKey(key),"Prefab changed hierarchy");Bounds b=ColliderBounds(collider);
                Need(Vector3.Distance(b.min,before[key].min)<.002f&&Vector3.Distance(b.max,before[key].max)<.002f,"Prefab lost collider placement: "+key);
                Need(collider.GetComponent<GameplaySurface>().SurfaceId==ids[key],"Prefab lost stable surface identity");}
            var savedTriggers=root.GetComponentsInChildren<Collider>().Where(c=>c.isTrigger).ToArray();Need(savedTriggers.Length==triggers.Count,"Prefab lost interaction triggers");
            foreach(var trigger in savedTriggers){string key=Hierarchy(trigger.transform);Need(triggers.ContainsKey(key),"Prefab changed trigger hierarchy");Bounds b=ColliderBounds(trigger);
                Need(Vector3.Distance(b.min,triggers[key].min)<.002f&&Vector3.Distance(b.max,triggers[key].max)<.002f,"Prefab moved interaction trigger: "+key);}
            var savedPickups=root.GetComponentsInChildren<GameplayToolPickup>();Need(pickupIds.SequenceEqual(savedPickups.Select(p=>p.PickupId).OrderBy(id=>id)),"Prefab lost pickup identities");
            foreach(var pickup in savedPickups)Need(pickup.ToolId==GameplayTools.Flyswatter&&pickup.InteractionCollider!=null&&pickup.InteractionCollider.isTrigger&&pickup.VisualRoot==null,"Prefab pickup contract changed");
            foreach(var pickup in savedPickups)CheckPickupDefinition(pickup);
            var savedDoors=root.GetComponentsInChildren<GameplayDoor>();Need(savedDoors.Length==doorDefinitions.Count,"Prefab lost doors");
            foreach(var door in savedDoors){Need(doorDefinitions.ContainsKey(door.DoorId),"Prefab changed DoorId");var beforeDoor=doorDefinitions[door.DoorId];var afterDoor=door.Definition;
                Need(Quaternion.Angle(beforeDoor.ClosedRotation.ToUnity(),afterDoor.ClosedRotation.ToUnity())<.01f&&Mathf.Abs(beforeDoor.InitialAngleRadians-afterDoor.InitialAngleRadians)<.0001f,"Prefab lost authored closed reference/initial door angle");
                CheckDoorInitialPose(door);}
        }
        static void AddReviewLights(Plan plan,bool lobby)
        {
            var root=new GameObject("ReviewOnly_LightingRoot");RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.17f,.20f,.27f);
            if(lobby){Point(root.transform,"Lobby",new Vector3(0,2.9f,0),4,12);foreach(float x in new[]{-4.8f,0,4.8f})Point(root.transform,"Lobby_Lantern_"+x,new Vector3(x,2.45f,5.55f),1.5f,5);return;}
            foreach(var zone in plan.zones.Where(z=>z.kind!="patio")){Vector3 center=(V(zone.min)+V(zone.max))*.5f;Point(root.transform,zone.id,new Vector3(center.x,V(zone.max).y-.25f,center.z),3,7);}
            var moon=Child(root.transform,"Moon_Review");moon.localRotation=Quaternion.Euler(50,-35,0);var light=moon.gameObject.AddComponent<Light>();light.type=LightType.Directional;light.color=new Color(.60f,.72f,1);light.intensity=.45f;light.shadows=LightShadows.Soft;
        }
        static void Point(Transform root,string name,Vector3 p,float intensity,float range){var t=Anchor(root,name,p);var light=t.gameObject.AddComponent<Light>();light.type=LightType.Point;light.color=new Color(1,.81f,.60f);light.intensity=intensity;light.range=range;light.shadows=LightShadows.Soft;light.lightmapBakeType=LightmapBakeType.Realtime;}
        static void AddCamera(Vector3 p,Vector3 target,string name){var t=new GameObject(name).transform;t.position=p;t.LookAt(target);var camera=t.gameObject.AddComponent<Camera>();camera.nearClipPlane=.03f;camera.farClipPlane=100;camera.fieldOfView=75;t.gameObject.tag="MainCamera";}
        internal static string ContentHash(string repository,string mapId)
        {
            string[] files={
                "art_source/unity/environments/alfa_maps/house_alfa_static.fbx","art_source/unity/environments/alfa_maps/lobby_alfa_static.fbx","art_source/unity/environments/alfa_maps/furniture_kit_alfa.fbx","art_source/unity/environments/alfa_maps/source_manifest.json",
                "art_source/unity/environments/room_sample/house_layout_plan.json","art_source/unity/environments/room_sample/room_furnished_without_door.fbx","art_source/unity/environments/room_sample/door_01.fbx","art_source/unity/environments/room_sample/room_contract.json","art_source/unity/environments/room_sample/presentation_manifest.json",
                "unity/Assets/LetMeSleep/Content/Editor/Environment/EnvironmentSampleBuilder.cs","unity/Assets/LetMeSleep/Content/Editor/Environment/AlfaMapBuilder.cs","unity/Assets/LetMeSleep/Content/Environment/EnvironmentMapDefinition.cs",
                "unity/Assets/LetMeSleep/Content/Editor/Environment/AlfaLobbyDressing.cs",
                "unity/Assets/LetMeSleep/Gameplay.Unity/GameplayToolPickup.cs","unity/Assets/LetMeSleep/Gameplay/ToolContracts.cs",
                "unity/Assets/LetMeSleep/Gameplay.Unity/GameplayDoor.cs","unity/Assets/LetMeSleep/Gameplay/Contracts.cs"};
            var payload=new System.Text.StringBuilder(mapId+"\n");
            using(var sha=System.Security.Cryptography.SHA256.Create()){
                foreach(string file in files){string path=Path.Combine(repository,file);byte[] bytes=file.EndsWith(".fbx",StringComparison.Ordinal)?File.ReadAllBytes(path):System.Text.Encoding.UTF8.GetBytes(File.ReadAllText(path).Replace("\r\n","\n").Replace("\r","\n"));
                    string hash=BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();payload.Append(file).Append('\t').Append(hash).Append('\n');}
                return "sha256-"+BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(payload.ToString()))).Replace("-","").ToLowerInvariant();}
        }
    }
}
