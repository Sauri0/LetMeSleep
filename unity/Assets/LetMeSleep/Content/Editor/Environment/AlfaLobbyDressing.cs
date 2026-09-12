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
        // Seated clip space: floor origin, +Y up, +Z toward the front of the sofa.
        static readonly Vector3 MenuSeatedRootPosition=new Vector3(3.3f,0,5.35f);
        static readonly Vector3[] MenuMosquitoPathPositions={
            new Vector3(2.63f,1.40f,4.72f),new Vector3(2.15f,1.65f,4.45f),
            new Vector3(2.35f,1.90f,4.15f),new Vector3(3.20f,2.00f,4.20f),
            new Vector3(4.05f,1.85f,4.40f),new Vector3(4.50f,1.60f,4.90f),
            new Vector3(4.10f,1.85f,5.20f),new Vector3(2.90f,1.95f,5.00f)};

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
                float throwX=x-(x>0?.62f:.30f);
                LobbyPiece(domestic,"Sofa_Throw_"+Token(x),new Vector3(throwX,.581f,5.31f),new Vector3(.40f,.012f,.60f),"Linen",false);
                LobbyPiece(domestic,"Throw_Edge_"+Token(x),new Vector3(throwX,.588f,5.06f),new Vector3(.37f,.002f,.025f),"Textile_Navy",false);
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
            material.SetColor("_BaseColor",new Color(.90f,.78f,.57f));material.SetColor("_EmissionColor",new Color(.55f,.25f,.05f));material.SetFloat("_Metallic",0);material.SetShaderPassEnabled("ShadowCaster",false);
            PersistAuthoredEmission(material);material.SetFloat("_Smoothness",.25f);EditorUtility.SetDirty(material);materials[name]=material;
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
            AddLobbyLivingMenuAnchors(data);
        }

        static void AddLobbyLivingMenuAnchors(EnvironmentMapDefinition data)
        {
            var root=Anchor(data.PresentationAnchors,"HumanMenuSeatedRoot",MenuSeatedRootPosition);
            root.localRotation=Quaternion.Euler(0,180,0);
            // Flat siblings permit explicit reference binding through PresentationAnchors.Find.
            // These are surface targets, not skeleton pivots or actor spawn points.
            MenuSeatAnchor(data,root,"MenuSeatSurface",new Vector3(0,.575f,.345f));
            MenuSeatAnchor(data,root,"MenuSeatFrontEdge",new Vector3(0,.575f,.40f));
            MenuSeatAnchor(data,root,"MenuSeatBackSupport",new Vector3(0,.93f,-.265f));
            MenuSeatAnchor(data,root,"MenuSeatLeftFoot",new Vector3(-.16f,0,.70f));
            MenuSeatAnchor(data,root,"MenuSeatRightFoot",new Vector3(.16f,0,.70f));
            for(int i=0;i<MenuMosquitoPathPositions.Length;i++){
                var point=Anchor(data.PresentationAnchors,"MenuMosquitoPath_"+i.ToString("00"),MenuMosquitoPathPositions[i]);
                point.localRotation=Quaternion.LookRotation(MenuMosquitoPathPositions[(i+1)%MenuMosquitoPathPositions.Length]-MenuMosquitoPathPositions[i]);
            }
            Anchor(data.PresentationAnchors,"MenuWarmLight",new Vector3(4.60f,1.75f,4.90f));
            Anchor(data.PresentationAnchors,"MenuFillLight",new Vector3(2.10f,2.15f,4.20f));
        }

        static void MenuSeatAnchor(EnvironmentMapDefinition data,Transform root,string name,Vector3 actorLocal)
        {
            var point=Anchor(data.PresentationAnchors,name,data.PresentationAnchors.InverseTransformPoint(root.TransformPoint(actorLocal)));
            point.rotation=root.rotation;
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
            CheckLobbyLivingMenuAnchors(lobby,data);
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

        static void CheckLobbyLivingMenuAnchors(GameObject lobby,EnvironmentMapDefinition data)
        {
            var anchors=data.PresentationAnchors;
            foreach(string name in new[]{"HumanMenuSeatedRoot","MenuSeatSurface","MenuSeatFrontEdge","MenuSeatBackSupport","MenuSeatLeftFoot","MenuSeatRightFoot","MenuWarmLight","MenuFillLight"})
                Need(anchors.Find(name)!=null,"Missing living menu anchor: "+name);
            var root=anchors.Find("HumanMenuSeatedRoot");var contact=anchors.Find("MenuSeatSurface");
            Need(Vector3.Distance(root.localPosition,MenuSeatedRootPosition)<.0001f&&Quaternion.Angle(root.localRotation,Quaternion.Euler(0,180,0))<.01f,"Seated menu clip frame changed");
            var sofa=F(lobby,"Lobby_BackSofa_3p3");
            var seat=sofa.GetComponentsInChildren<MeshFilter>().Single(f=>f.name.Contains("Sofa_Seat"));
            var vertices=seat.sharedMesh.vertices.Select(seat.transform.TransformPoint).ToArray();var indices=seat.sharedMesh.triangles;
            // Sample the render mesh, not only the broad collision proxy. Stay inside bevels.
            foreach(float x in new[]{-.12f,0,.12f})foreach(float z in new[]{-.10f,-.05f,0}){
                var p=contact.TransformPoint(new Vector3(x,0,z));
                Need(Mathf.Abs(QualitySurfaceHeight(vertices,indices,p.x,p.z)-p.y)<.001f,"Menu pelvis contact is not on the actual seat surface");
            }
            var back=ColliderBounds(sofa.GetComponentsInChildren<Collider>().Single(c=>c.name.Contains("Sofa_Back")));
            Need(Mathf.Abs(anchors.Find("MenuSeatBackSupport").position.z-back.min.z)<.001f,"Menu back support differs from sofa front");
            var seatingArea=new Bounds(contact.position+Vector3.up*.13f,new Vector3(.60f,.25f,.40f));
            var domestic=F(lobby,"Lobby_Domestic");
            foreach(string prefix in new[]{"Sofa_Throw_","Sofa_Cushion_"})
                Need(!F(domestic.gameObject,prefix+"3p3").GetComponent<Renderer>().bounds.Intersects(seatingArea),"Menu seating contact obstructed by decorative textile");
            foreach(string name in new[]{"MenuSeatLeftFoot","MenuSeatRightFoot"}){
                var foot=anchors.Find(name);
                Need(Mathf.Abs(foot.localPosition.y)<.0001f&&foot.localPosition.z-.18f>3.8f,"Menu slipper support moved off floor or into circulation");
                var footprint=new Bounds(foot.position+Vector3.up*.045f,new Vector3(.24f,.08f,.36f));
                foreach(var c in lobby.GetComponentsInChildren<Collider>().Where(c=>!c.isTrigger))
                    Need(!ColliderBounds(c).Intersects(footprint),"Menu slipper footprint obstructed: "+Hierarchy(c.transform));
            }
            foreach(var spawn in data.LobbySpawnPoints)
                Need(Vector2.Distance(new Vector2(root.localPosition.x,root.localPosition.z),new Vector2(spawn.localPosition.x,spawn.localPosition.z))>.8f,"Seated menu anchor overlaps lobby spawn");
            for(int i=0;i<MenuMosquitoPathPositions.Length;i++){
                var point=anchors.Find("MenuMosquitoPath_"+i.ToString("00"));
                Need(point!=null&&Vector3.Distance(point.localPosition,MenuMosquitoPathPositions[i])<.0001f,"Menu mosquito waypoint missing or moved");
                // Presentation uses midpoint -> control point -> midpoint quadratic arcs.
                // The expanded triangle AABB conservatively contains each entire arc.
                var previous=anchors.Find("MenuMosquitoPath_"+((i+MenuMosquitoPathPositions.Length-1)%MenuMosquitoPathPositions.Length).ToString("00"));
                var next=anchors.Find("MenuMosquitoPath_"+((i+1)%MenuMosquitoPathPositions.Length).ToString("00"));
                Need(previous!=null&&next!=null,"Menu mosquito route is incomplete");
                var envelope=new Bounds(point.position,Vector3.zero);
                envelope.Encapsulate((previous.position+point.position)*.5f);
                envelope.Encapsulate((point.position+next.position)*.5f);
                envelope.Expand(.36f);
                foreach(var c in lobby.GetComponentsInChildren<Collider>().Where(c=>!c.isTrigger))
                    Need(!ColliderBounds(c).Intersects(envelope),"Menu mosquito waypoint intersects solid environment: "+Hierarchy(c.transform));
                foreach(var r in F(lobby,"Furnishings").GetComponentsInChildren<Renderer>())
                    Need(!r.bounds.Intersects(envelope),"Menu mosquito waypoint intersects visible environment: "+Hierarchy(r.transform));
            }
            // Static room clearance assumes mosquito envelope radius <= .18m and the
            // agreed quadratic curve. Actors, swatter sweep and framing need native review.
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
