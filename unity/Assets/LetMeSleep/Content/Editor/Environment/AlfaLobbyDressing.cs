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
            // These three floating blue panels were placeholder decoration, not architecture.
            // Remove their unpacked instances; retain the closed, validated LobbyShell.
            foreach(var renderer in lobby.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name.StartsWith("Lobby_Wall_Panel",StringComparison.Ordinal)).ToArray())
                UnityEngine.Object.DestroyImmediate(renderer.gameObject);
            var furnishings=Child(lobby.transform,"Furnishings");
            // Existing kit assets keep their authored dimensions. Only the architectural shell
            // expands, leaving the old 10x8 floor as circulation inside a decorative apron.
            foreach(float x in new[]{-6.25f,6.25f})foreach(float z in new[]{-2.3f,2.3f})
                Place("PatioBench","Lobby_Bench_"+Token(x)+"_"+Token(z),new Vector3(x,0,z),x<0?-90:90,furnishings);
            foreach(float x in new[]{-3.3f,3.3f})
                Place("Sofa","Lobby_BackSofa_"+Token(x),new Vector3(x,0,5.35f),0,furnishings);
            foreach(float x in new[]{-5.85f,5.85f})foreach(float z in new[]{-4.75f,4.75f})
                Place("Pine","Lobby_Pine_"+Token(x)+"_"+Token(z),new Vector3(x,0,z),0,furnishings);
            Place("Shelf","Lobby_BackShelf",new Vector3(0,0,5.7f),0,furnishings);
            AddLobbyDomesticSet(furnishings);

            var trim=Child(lobby.transform,"ArchitecturalTrim");
            LobbyPiece(trim,"Central_Textile_Inlay",new Vector3(0,.005f,0),new Vector3(6,.01f,4),"Textile_Blue",false);
            foreach(float x in new[]{-6.85f,6.85f}){
                LobbyPiece(trim,"Side_Skirting_"+Token(x),new Vector3(Mathf.Sign(x)*6.95f,.10f,0),new Vector3(.10f,.20f,11.8f),"Wood_Edge",true);
                LobbyPiece(trim,"Side_Rail_"+Token(x),new Vector3(x,1.05f,0),new Vector3(.12f,.12f,10.5f),"Wood_Honey",true);
                foreach(float z in new[]{-5.2f,0,5.2f})LobbyPiece(trim,"Rail_Post_"+Token(x)+"_"+Token(z),new Vector3(x,.525f,z),new Vector3(.12f,1.05f,.12f),"Wood_Edge",true);
            }
            foreach(float z in new[]{-5.95f,5.95f})LobbyPiece(trim,"End_Skirting_"+Token(z),new Vector3(0,.1f,z),new Vector3(13.9f,.2f,.1f),"Wood_Edge",true);
            // Low wood lining makes the seating wall one domestic composition, rather
            // than three unrelated billboards. It stays behind all furniture and stages.
            LobbyPiece(trim,"Back_Wainscot",new Vector3(0,.57f,5.96f),new Vector3(13.7f,.82f,.06f),"Wood_Honey",true);
            LobbyPiece(trim,"Back_ChairRail",new Vector3(0,1.005f,5.93f),new Vector3(13.7f,.05f,.10f),"Wood_Edge",true);
            for(int i=-6;i<=6;i++)LobbyPiece(trim,"Back_Stile_"+i,new Vector3(i,.57f,5.915f),new Vector3(.045f,.82f,.03f),"Wood_Edge",true);
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

        static void AddLobbyDomesticSet(Transform furnishings)
        {
            var domestic=Child(furnishings,"Lobby_Domestic");
            var books=Child(domestic,"Shelf_Books");books.localPosition=new Vector3(0,0,5.7f);books.localRotation=Quaternion.Euler(0,180,0);
            for(int i=0;i<5;i++)AddBook(books,"Middle_"+i,new Vector3(-.35f+i*.072f,.61f,0),.055f,.24f+(i%3)*.035f,.23f,
                i%3==0?"Textile_Rust":i%3==1?"Textile_Blue":"Textile_Navy");
            AddBook(books,"Upper_0",new Vector3(.20f,1.11f,0),.07f,.31f,.23f,"Textile_Blue");
            AddBook(books,"Upper_1",new Vector3(.285f,1.11f,0),.06f,.27f,.23f,"Textile_Rust");
            foreach(float x in new[]{-.26f,.26f}){
                var box=Child(domestic,"Storage_Box_"+Token(x));box.localPosition=new Vector3(x,.11f,5.7f);
                LobbyPiece(box,"Linen_Box",new Vector3(0,.16f,0),new Vector3(.42f,.32f,.27f),"Linen",true);
                LobbyPiece(box,"Lid",new Vector3(0,.33f,0),new Vector3(.44f,.02f,.29f),"Wood_Honey",true);
                LobbyPiece(box,"Pull",new Vector3(0,.18f,-.143f),new Vector3(.10f,.025f,.016f),"Iron",true);
            }
            foreach(float x in new[]{-3.3f,3.3f}){
                LobbyPiece(domestic,"Sofa_Throw_"+Token(x),new Vector3(x-.30f,.581f,5.31f),new Vector3(.40f,.012f,.60f),"Linen",false);
                LobbyPiece(domestic,"Throw_Edge_"+Token(x),new Vector3(x-.30f,.588f,5.06f),new Vector3(.37f,.002f,.025f),"Textile_Navy",false);
                LobbyPiece(domestic,"Sofa_Cushion_"+Token(x),new Vector3(x+.55f,.725f,5.52f),new Vector3(.36f,.30f,.14f),"Textile_Blue",false);
            }
            var rack=Child(domestic,"Entry_CoatRack");rack.localPosition=new Vector3(-2,1.7f,5.92f);
            LobbyPiece(rack,"MountingBoard",Vector3.zero,new Vector3(.85f,.16f,.08f),"Wood_Honey",true);
            foreach(float x in new[]{-.26f,0,.26f}){
                LobbyPiece(rack,"Hook_Stem_"+Token(x),new Vector3(x,-.045f,-.09f),new Vector3(.025f,.025f,.10f),"Iron",true);
                LobbyPiece(rack,"Hook_Tip_"+Token(x),new Vector3(x,-.015f,-.13f),new Vector3(.025f,.075f,.025f),"Iron",true);
            }
        }

        static string Token(float value)=>value.ToString("0.##",System.Globalization.CultureInfo.InvariantCulture).Replace('-','m').Replace('.','p');

        static void EnsureLanternMaterial()
        {
            const string name="Lobby_LanternGlow";string path=Output+"/Materials/"+name+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name,enableInstancing=true};AssetDatabase.CreateAsset(material,path);}
            material.SetColor("_BaseColor",new Color(.90f,.78f,.57f));material.SetColor("_EmissionColor",new Color(.18f,.10f,.035f));
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
            var mosquito=Anchor(data.PresentationAnchors,"MosquitoMenuStage",new Vector3(.8f,1.85f,4.5f));
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
            CheckLobbyDomesticSupport(lobby);
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

        static void CheckLobbyDomesticSupport(GameObject lobby)
        {
            Need(!lobby.GetComponentsInChildren<MeshRenderer>().Any(r=>r.name.StartsWith("Lobby_Wall_Panel",StringComparison.Ordinal)),"Lobby placeholder wall panels remain");
            var domestic=F(lobby,"Lobby_Domestic");var shelf=F(lobby,"Lobby_BackShelf");
            var boards=shelf.GetComponentsInChildren<Collider>().Where(c=>c.name.Contains("Shelf_Board")).Select(ColliderBounds).ToArray();
            foreach(var book in F(domestic.gameObject,"Shelf_Books").GetComponentsInChildren<BoxCollider>())
                Need(boards.Any(board=>Mathf.Abs(ColliderBounds(book).min.y-board.max.y)<.0001f&&board.min.x<=ColliderBounds(book).min.x&&board.max.x>=ColliderBounds(book).max.x&&board.min.z<=ColliderBounds(book).min.z&&board.max.z>=ColliderBounds(book).max.z),"Lobby book not supported on a shelf");
            foreach(Transform box in domestic)if(box.name.StartsWith("Storage_Box_",StringComparison.Ordinal)){
                var body=box.Find("Linen_Box").GetComponent<Renderer>().bounds;var lid=box.Find("Lid").GetComponent<Renderer>().bounds;
                Need(boards.Any(board=>Mathf.Abs(body.min.y-board.max.y)<.0001f&&board.min.x<=body.min.x&&board.max.x>=body.max.x&&board.min.z<=body.min.z&&board.max.z>=body.max.z),"Lobby storage box not supported on a shelf");
                Need(Mathf.Abs(lid.min.y-body.max.y)<.0001f,"Lobby storage lid floats above its box");
            }
            foreach(float x in new[]{-3.3f,3.3f}){
                var sofa=F(lobby,"Lobby_BackSofa_"+Token(x));var seat=sofa.GetComponentsInChildren<Collider>().Single(c=>c.name.Contains("Sofa_Seat"));float top=ColliderBounds(seat).max.y;
                foreach(string prefix in new[]{"Sofa_Throw_","Sofa_Cushion_"})Need(Mathf.Abs(F(domestic.gameObject,prefix+Token(x)).GetComponent<Renderer>().bounds.min.y-top)<.0001f,"Lobby textile floats above sofa seat");
            }
        }

        static void CheckLobbyShellFacing(GameObject source)
        {
            var filter=F(source,"LobbyShell").GetComponent<MeshFilter>();var mesh=filter.sharedMesh;
            var vertices=mesh.vertices;var normals=mesh.normals;var triangles=mesh.triangles;
            var axes=new[]{1,1,0,0,2,2};var planes=new[]{0f,3.2f,-5f,5f,-4f,4f};
            var expected=new[]{Vector3.up,Vector3.down,Vector3.right,Vector3.left,Vector3.forward,Vector3.back};
            var normalMatrix=filter.transform.localToWorldMatrix.inverse.transpose;
            for(int plane=0;plane<planes.Length;plane++){
                int found=0;
                for(int i=0;i<triangles.Length;i+=3){
                    var a=filter.transform.TransformPoint(vertices[triangles[i]]);var b=filter.transform.TransformPoint(vertices[triangles[i+1]]);var c=filter.transform.TransformPoint(vertices[triangles[i+2]]);
                    if(Mathf.Abs(a[axes[plane]]-planes[plane])>.002f||Mathf.Abs(b[axes[plane]]-planes[plane])>.002f||Mathf.Abs(c[axes[plane]]-planes[plane])>.002f)continue;
                    var center=(a+b+c)/3;
                    if(center.x < -5.001f||center.x > 5.001f||center.y < -.001f||center.y > 3.201f||center.z < -4.001f||center.z > 4.001f)continue;
                    found++;
                    Need(Vector3.Dot(Vector3.Cross(b-a,c-a).normalized,expected[plane])>.999f,"Lobby cavity triangle faces away from interior plane "+plane);
                    for(int corner=0;corner<3;corner++)Need(Vector3.Dot(normalMatrix.MultiplyVector(normals[triangles[i+corner]]).normalized,expected[plane])>.999f,"Lobby cavity shading normal faces away from interior plane "+plane);
                }
                Need(found>=2,"Lobby interior plane is missing: "+plane);
            }
        }
    }
}
