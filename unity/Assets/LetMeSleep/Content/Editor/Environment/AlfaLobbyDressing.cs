using System;
using System.Linq;
using LetMeSleep.Content.Environment;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LetMeSleep.Content.Editor
{
    public static partial class AlfaMapBuilder
    {
        static readonly Vector3 MenuCameraPosition=new Vector3(0,1.6f,.4f);
        static readonly Vector3 MenuLookAtPosition=new Vector3(.8f,1.1f,4.85f);

        static void FurnishLobby(GameObject lobby)
        {
            var furnishings=Child(lobby.transform,"Furnishings");
            // Existing kit assets keep their authored dimensions. Only the architectural shell
            // expands, leaving the old 10x8 floor as circulation inside a decorative apron.
            foreach(float x in new[]{-6.25f,6.25f})foreach(float z in new[]{-2.3f,2.3f})
                Place("PatioBench","Lobby_Bench_"+Token(x)+"_"+Token(z),new Vector3(x,0,z),x<0?-90:90,furnishings);
            foreach(float x in new[]{-3.3f,3.3f})
                Place("PatioBench","Lobby_BackBench_"+Token(x),new Vector3(x,0,5.35f),0,furnishings);
            foreach(float x in new[]{-5.85f,5.85f})foreach(float z in new[]{-4.75f,4.75f})
                Place("Pine","Lobby_Pine_"+Token(x)+"_"+Token(z),new Vector3(x,0,z),0,furnishings);
            Place("Shelf","Lobby_BackShelf",new Vector3(0,0,5.7f),0,furnishings);

            var trim=Child(lobby.transform,"ArchitecturalTrim");
            LobbyPiece(trim,"Central_Textile_Inlay",new Vector3(0,.005f,0),new Vector3(6,.01f,4),"Textile_Blue",false);
            foreach(float x in new[]{-6.85f,6.85f}){
                LobbyPiece(trim,"Side_Skirting_"+Token(x),new Vector3(Mathf.Sign(x)*6.95f,.10f,0),new Vector3(.10f,.20f,11.8f),"Wood_Edge",true);
                LobbyPiece(trim,"Side_Rail_"+Token(x),new Vector3(x,1.05f,0),new Vector3(.12f,.12f,10.5f),"Wood_Honey",true);
                foreach(float z in new[]{-5.2f,0,5.2f})LobbyPiece(trim,"Rail_Post_"+Token(x)+"_"+Token(z),new Vector3(x,.525f,z),new Vector3(.12f,1.05f,.12f),"Wood_Edge",true);
            }
            foreach(float z in new[]{-5.95f,5.95f})LobbyPiece(trim,"End_Skirting_"+Token(z),new Vector3(0,.1f,z),new Vector3(13.9f,.2f,.1f),"Wood_Edge",true);
            foreach(float x in new[]{-4.7f,4.7f})LobbyPiece(trim,"Ceiling_Beam_"+Token(x),new Vector3(x,3.10f,0),new Vector3(.20f,.20f,11.8f),"Wood_Edge",true);
            LobbyPiece(trim,"Ceiling_Crossbeam",new Vector3(0,3.10f,4),new Vector3(13.8f,.20f,.20f),"Wood_Edge",true);

            var lanterns=Child(lobby.transform,"Lanterns");
            EnsureLanternMaterial();
            foreach(float x in new[]{-4.8f,0,4.8f}){
                var lantern=Child(lanterns,"Lantern_"+Token(x));lantern.localPosition=new Vector3(x,2.45f,5.74f);
                LobbyPiece(lantern,"Backplate",new Vector3(0,0,.21f),new Vector3(.30f,.54f,.14f),"Wood_Edge",true);
                LobbyPiece(lantern,"Warm_Core",Vector3.zero,new Vector3(.20f,.30f,.16f),"Lobby_LanternGlow",false);
                foreach(float y in new[]{-.20f,.20f})LobbyPiece(lantern,"Cap_"+Token(y),new Vector3(0,y,0),new Vector3(.32f,.06f,.28f),"Iron",true);
                foreach(float side in new[]{-.13f,.13f})LobbyPiece(lantern,"Frame_"+Token(side),new Vector3(side,0,-.10f),new Vector3(.035f,.40f,.035f),"Iron",true);
            }
        }

        static string Token(float value)=>value.ToString("0.##",System.Globalization.CultureInfo.InvariantCulture).Replace('-','m').Replace('.','p');

        static void EnsureLanternMaterial()
        {
            const string name="Lobby_LanternGlow";string path=Output+"/Materials/"+name+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name,enableInstancing=true};AssetDatabase.CreateAsset(material,path);}
            material.SetColor("_BaseColor",new Color(1,.70f,.30f));material.SetColor("_EmissionColor",new Color(1,.46f,.12f)*2);
            material.EnableKeyword("_EMISSION");material.SetFloat("_Smoothness",.25f);EditorUtility.SetDirty(material);materials[name]=material;
        }

        static void LobbyPiece(Transform parent,string name,Vector3 center,Vector3 size,string material,bool solid)
        {
            // Reuse the bevelled, UV2-bearing tabletop mesh rather than generating new art.
            var source=F(Kit["Kit_Table"],"Kit_Table_Top").GetComponent<MeshFilter>().sharedMesh;
            Bounds bounds=source.bounds;var t=Child(parent,name);
            t.localScale=new Vector3(size.x/bounds.size.x,size.y/bounds.size.y,size.z/bounds.size.z);
            t.localPosition=center-Vector3.Scale(bounds.center,t.localScale);
            t.gameObject.AddComponent<MeshFilter>().sharedMesh=source;
            var renderer=t.gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=materials[material];
            renderer.receiveGI=ReceiveGI.Lightmaps;renderer.lightProbeUsage=LightProbeUsage.Off;
            renderer.reflectionProbeUsage=ReflectionProbeUsage.BlendProbes;renderer.receiveShadows=true;
            renderer.shadowCastingMode=material=="Lobby_LanternGlow"?ShadowCastingMode.Off:ShadowCastingMode.On;
            GameObjectUtility.SetStaticEditorFlags(t.gameObject,StaticEditorFlags.ContributeGI|StaticEditorFlags.OccludeeStatic);
            if(solid){t.gameObject.layer=EnvironmentSampleBuilder.Layer("WorldStatic");var collider=t.gameObject.AddComponent<BoxCollider>();collider.center=bounds.center;collider.size=bounds.size;}
        }

        static void AddLobbyPresentationAnchors(EnvironmentMapDefinition data)
        {
            var camera=Anchor(data.PresentationAnchors,"MainMenuCamera",MenuCameraPosition);
            camera.LookAt(data.transform.TransformPoint(MenuLookAtPosition),data.transform.up);
            Anchor(data.PresentationAnchors,"MainMenuLookAt",MenuLookAtPosition);
            var human=Anchor(data.PresentationAnchors,"HumanMenuStage",new Vector3(1.65f,.02f,4.7f));
            human.localRotation=Quaternion.LookRotation(new Vector3(MenuCameraPosition.x-human.localPosition.x,0,MenuCameraPosition.z-human.localPosition.z));
            var mosquito=Anchor(data.PresentationAnchors,"MosquitoMenuStage",new Vector3(3.1f,1.55f,4.5f));
            mosquito.localRotation=Quaternion.LookRotation(MenuCameraPosition-mosquito.localPosition);
            foreach(float x in new[]{-4.8f,0,4.8f})Anchor(data.PresentationAnchors,"LightAnchor_Lobby_Lantern_"+Token(x),new Vector3(x,2.45f,5.55f));
        }

        static void CheckLobbyDressing(GameObject lobby,EnvironmentMapDefinition data)
        {
            Need(data.LobbySpawnPoints.Length==16,"Lobby must preserve sixteen spawns");
            // Center 6x4 plus a full 1.8m circulation ring. Floor and overhead beams are allowed;
            // no furniture, rail or solid wall may occupy the standing-height protected box.
            var protectedRoute=new Bounds(new Vector3(0,1.11f,0),new Vector3(9.6f,2.18f,7.6f));
            foreach(var collider in lobby.GetComponentsInChildren<Collider>().Where(c=>!c.isTrigger))
                Need(!ColliderBounds(collider).Intersects(protectedRoute),"Lobby circulation obstructed: "+Hierarchy(collider.transform));
            foreach(var renderer in F(lobby,"Furnishings").GetComponentsInChildren<Renderer>())
                Need(!renderer.bounds.Intersects(protectedRoute),"Lobby furnishing visually intrudes into circulation: "+Hierarchy(renderer.transform));
            foreach(string name in new[]{"MainMenuCamera","MainMenuLookAt","HumanMenuStage","MosquitoMenuStage"})
                Need(data.PresentationAnchors.Find(name)!=null,"Missing menu presentation anchor: "+name);
            var camera=data.PresentationAnchors.Find("MainMenuCamera");var target=data.PresentationAnchors.Find("MainMenuLookAt");
            Need(Vector3.Distance(camera.localPosition,MenuCameraPosition)<.0001f&&Vector3.Distance(target.localPosition,MenuLookAtPosition)<.0001f,"Menu camera anchors moved during serialization");
            Need(Vector3.Dot(camera.forward,(target.position-camera.position).normalized)>.9999f,"Menu camera orientation differs from look-at");
            CheckSpawns(lobby,new[]{camera},.05f,.1f,true);
            var human=data.PresentationAnchors.Find("HumanMenuStage");var mosquito=data.PresentationAnchors.Find("MosquitoMenuStage");
            CheckSpawns(lobby,new[]{human},.25f,1.72f);CheckSpawns(lobby,new[]{mosquito},.055f,.11f,true);
            foreach(var spawn in data.LobbySpawnPoints)
                Need(Vector2.Distance(new Vector2(spawn.localPosition.x,spawn.localPosition.z),new Vector2(human.localPosition.x,human.localPosition.z))>.8f,"Menu stage overlaps a lobby spawn");
        }
    }
}
