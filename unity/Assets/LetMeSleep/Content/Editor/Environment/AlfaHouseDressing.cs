using System;
using System.Linq;
using LetMeSleep.Content.Environment;
using UnityEditor;
using UnityEngine;

namespace LetMeSleep.Content.Editor
{
    public static partial class AlfaMapBuilder
    {
        static void AddHouseDressing(GameObject house,EnvironmentMapDefinition data,Plan plan)
        {
            var root=Child(house.transform,"HouseDressing");
            var rug=Child(root,"Living_Rug");
            // Millimetre-height visual textile; the existing floor remains the collision plane.
            // Its boundary groups sofa/table without touching the east-side door route.
            LobbyPiece(rug,"Rust_Ground",new Vector3(1.90f,.003f,2.35f),new Vector3(2.45f,.006f,2.4f),"Textile_Rust",false);
            LobbyPiece(rug,"Linen_Border",new Vector3(1.90f,.0065f,2.35f),new Vector3(2.23f,.001f,2.18f),"Linen",false);
            LobbyPiece(rug,"Rust_Field",new Vector3(1.90f,.0075f,2.35f),new Vector3(2.13f,.001f,2.08f),"Textile_Rust",false);
            foreach(float z in new[]{1.51f,3.19f})LobbyPiece(rug,"Woven_Band_"+Token(z),new Vector3(1.90f,.0085f,z),new Vector3(1.94f,.001f,.045f),"Textile_Navy",false);

            var books=Child(root,"Living_Shelf_Books");
            // Shelf top surfaces are y=.11/.61/1.11; spine faces the room (+Z).
            for(int i=0;i<5;i++)AddBook(books,"Standing_"+i,new Vector3(1.53f+i*.068f,.61f,.47f),.05f,.24f+(i%3)*.035f,.23f,
                i%3==0?"Textile_Navy":i%3==1?"Textile_Rust":"Textile_Blue");
            AddBook(books,"Upper_0",new Vector3(2.15f,1.11f,.47f),.075f,.27f,.22f,"Textile_Rust");
            AddBook(books,"Upper_1",new Vector3(2.24f,1.11f,.47f),.055f,.30f,.22f,"Textile_Navy");
            AddRoomCarpentry(root,plan);
            AddDomesticSets(root);

            var fixtures=Child(root,"CeilingFixtures");EnsureHouseDiffuserMaterial();
            foreach(var zone in plan.zones.Where(z=>z.kind!="patio")){
                var lightAnchor=data.PresentationAnchors.Find("LightAnchor_"+zone.id);Need(lightAnchor!=null,"Missing light anchor for fixture");
                float ceiling=V(zone.max).y;
                var fixture=Child(fixtures,"CeilingFixture_"+zone.id);
                fixture.localPosition=new Vector3(lightAnchor.localPosition.x,ceiling,lightAnchor.localPosition.z);
                LobbyPiece(fixture,"Ceiling_Plate",new Vector3(0,-.025f,0),new Vector3(.58f,.05f,.58f),"Wood_Edge",true);
                foreach(float x in new[]{-.255f,.255f})LobbyPiece(fixture,"Frame_X_"+Token(x),new Vector3(x,-.105f,0),new Vector3(.07f,.11f,.58f),"Wood_Honey",true);
                foreach(float z in new[]{-.255f,.255f})LobbyPiece(fixture,"Frame_Z_"+Token(z),new Vector3(0,-.105f,z),new Vector3(.44f,.11f,.07f),"Wood_Honey",true);
                LobbyPiece(fixture,"Warm_Diffuser",new Vector3(0,-.13f,0),new Vector3(.44f,.04f,.44f),"House_Diffuser",true);
                // A light transform, not a Light component. W2 owns distribution/exposure.
                lightAnchor.localPosition=new Vector3(fixture.localPosition.x,ceiling-.38f,fixture.localPosition.z);
                lightAnchor.localRotation=Quaternion.LookRotation(Vector3.down,Vector3.forward);
            }
            CheckHouseDressing(house,data);
        }

        static void AddRoomCarpentry(Transform root,Plan plan)
        {
            var trim=Child(root,"RoomCarpentry");
            foreach(var zone in plan.zones.Where(z=>z.kind=="room")){
                Vector3 lo=V(zone.min),hi=V(zone.max);
                for(int axis=0;axis<=2;axis+=2)foreach(bool high in new[]{false,true}){
                    float edge=high?hi[axis]:lo[axis];int along=axis==0?2:0;
                    var intervals=new System.Collections.Generic.List<Vector2>{new Vector2(lo[along],hi[along])};
                    foreach(var portal in plan.portals){var c=V(portal.center);
                        if(Mathf.Abs(c[axis]-edge)>.101f||Mathf.Abs(c.y-(lo.y+1.1f))>.01f)continue;
                        float cutLow=c[along]-portal.width*.5f-.085f,cutHigh=c[along]+portal.width*.5f+.085f;
                        var next=new System.Collections.Generic.List<Vector2>();
                        foreach(var interval in intervals){if(cutHigh<=interval.x||cutLow>=interval.y){next.Add(interval);continue;}
                            if(cutLow>interval.x)next.Add(new Vector2(interval.x,cutLow));if(cutHigh<interval.y)next.Add(new Vector2(cutHigh,interval.y));}
                        intervals=next;
                    }
                    int part=0;foreach(var interval in intervals){
                        var center=new Vector3(0,lo.y+.055f,0);center[axis]=edge+(high?-.0125f:.0125f);center[along]=(interval.x+interval.y)*.5f;
                        var size=new Vector3(.025f,.11f,.025f);size[along]=interval.y-interval.x;
                        LobbyPiece(trim,zone.id+"_Skirting_"+axis+"_"+high+"_"+part++,center,size,"Wood_Edge",false);
                    }
                }
            }
            // Only the witness living room and BedroomA windows; no change to sealed panes.
            foreach(float floor in new[]{0f,3f}){
                string label=floor==0?"Living":"BedroomA";
                LobbyPiece(trim,label+"_WindowSill",new Vector3(1.38f,floor+1.16f,.205f),new Vector3(1.35f,.07f,.10f),"Wood_Honey",true);
                LobbyPiece(trim,label+"_CurtainRail",new Vector3(1.38f,floor+2.33f,.24f),new Vector3(1.65f,.045f,.045f),"Iron",true);
                foreach(float x in new[]{.65f,2.11f}){
                    for(int fold=0;fold<3;fold++)LobbyPiece(trim,label+"_Curtain_"+Token(x)+"_"+fold,
                        new Vector3(x+(fold-1)*.065f,floor+1.73f,.21f+(fold%2)*.025f),new Vector3(.072f,1.1f,.035f),
                        fold%2==0?"Textile_Rust":"Textile_Navy",true);
                }
            }
        }

        static void AddDomesticSets(Transform root)
        {
            var bedroom=Child(root,"BedroomA_Domestic");
            // Rug sits under the open side/foot of the bed, not in its door sweep.
            LobbyPiece(bedroom,"Bedside_Rug",new Vector3(2.2f,3.003f,1.85f),new Vector3(1.15f,.006f,2.35f),"Textile_Blue",false);
            foreach(float z in new[]{.83f,2.87f})LobbyPiece(bedroom,"Rug_Border_"+Token(z),new Vector3(2.2f,3.007f,z),new Vector3(1.02f,.002f,.055f),"Linen",false);
            // Desk small still-life remains well away from nightstand pickup1006.
            LobbyPiece(bedroom,"Desk_ClosedBook",new Vector3(4.42f,3.8125f,.55f),new Vector3(.24f,.035f,.18f),"Textile_Rust",false);
            var kitchen=Child(root,"Kitchen_Domestic");
            LobbyPiece(kitchen,"CuttingBoard",new Vector3(12.20f,.885f,9.18f),new Vector3(.34f,.01f,.48f),"Wood_Honey",false);
            LobbyPiece(kitchen,"FoldedTeaCloth",new Vector3(12.24f,.8925f,9.72f),new Vector3(.34f,.025f,.23f),"Linen",false);
            LobbyPiece(kitchen,"ClothBand",new Vector3(12.24f,.906f,9.72f),new Vector3(.05f,.002f,.21f),"Textile_Blue",false);
            // Plates are low-poly turned ceramics; real sidewalls and rim, no flat decal.
            AddPlate(kitchen,"Plate_Lower",new Vector3(12.20f,.88f,8.70f));
            AddPlate(kitchen,"Plate_Upper",new Vector3(12.20f,.896f,8.70f));
        }

        static void AddPlate(Transform parent,string name,Vector3 position)
        {
            const int sides=16;var vertices=new System.Collections.Generic.List<Vector3>();var triangles=new System.Collections.Generic.List<int>();
            // Closed lathed cross-section: underside disk -> foot -> outer lip -> inner well.
            var profile=new[]{new Vector2(0,0),new Vector2(.085f,0),new Vector2(.15f,.018f),new Vector2(.15f,.029f),new Vector2(.10f,.016f),new Vector2(0,.016f)};
            for(int ring=0;ring<profile.Length-1;ring++)for(int side=0;side<sides;side++){
                float a=side*Mathf.PI*2/sides,b=(side+1)*Mathf.PI*2/sides;int start=vertices.Count;
                var p=profile[ring];var q=profile[ring+1];
                vertices.Add(new Vector3(Mathf.Cos(a)*p.x,p.y,Mathf.Sin(a)*p.x));vertices.Add(new Vector3(Mathf.Cos(b)*p.x,p.y,Mathf.Sin(b)*p.x));
                vertices.Add(new Vector3(Mathf.Cos(b)*q.x,q.y,Mathf.Sin(b)*q.x));vertices.Add(new Vector3(Mathf.Cos(a)*q.x,q.y,Mathf.Sin(a)*q.x));
                if(p.x>0){triangles.Add(start);triangles.Add(start+2);triangles.Add(start+1);}
                if(q.x>0){triangles.Add(start);triangles.Add(start+3);triangles.Add(start+2);}
            }
            var mesh=new Mesh{name="House_Plate"};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();CheckPlateFacing(mesh);Unwrapping.GenerateSecondaryUVSet(mesh);
            string path=Output+"/Meshes/House_Plate.asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(saved==null){AssetDatabase.CreateAsset(mesh,path);saved=mesh;}else{EditorUtility.CopySerialized(mesh,saved);UnityEngine.Object.DestroyImmediate(mesh);EditorUtility.SetDirty(saved);}
            var t=Child(parent,name);t.localPosition=position;t.gameObject.AddComponent<MeshFilter>().sharedMesh=saved;
            var renderer=t.gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=materials["Porcelain"];
            renderer.receiveGI=ReceiveGI.Lightmaps;
            GameObjectUtility.SetStaticEditorFlags(t.gameObject,StaticEditorFlags.ContributeGI|StaticEditorFlags.OccludeeStatic);
        }

        static void CheckPlateFacing(Mesh mesh)
        {
            var vertices=mesh.vertices;var triangles=mesh.triangles;int underside=0,well=0;double volume=0;
            for(int i=0;i<triangles.Length;i+=3){
                var a=vertices[triangles[i]];var b=vertices[triangles[i+1]];var c=vertices[triangles[i+2]];
                var cross=Vector3.Cross(b-a,c-a);Need(cross.sqrMagnitude>1e-12f,"Plate has a degenerate triangle");
                volume+=Vector3.Dot(a,Vector3.Cross(b,c))/6.0;
                if(Mathf.Abs(a.y)<1e-6f&&Mathf.Abs(b.y)<1e-6f&&Mathf.Abs(c.y)<1e-6f){
                    underside++;Need(Vector3.Dot(cross.normalized,Vector3.down)>.999f,"Plate underside must face down");}
                if(Mathf.Abs(a.y-.016f)<1e-6f&&Mathf.Abs(b.y-.016f)<1e-6f&&Mathf.Abs(c.y-.016f)<1e-6f){
                    well++;Need(Vector3.Dot(cross.normalized,Vector3.up)>.999f,"Plate well must face up");}
            }
            Need(underside==16&&well==16&&volume>0,"Plate must preserve both disks and positive enclosed volume");
        }

        static void AddBook(Transform parent,string name,Vector3 bottom,float width,float height,float depth,string cover)
        {
            var book=Child(parent,name);book.localPosition=bottom;
            LobbyPiece(book,"Pages",new Vector3(0,height*.5f,-.008f),new Vector3(width-.01f,height-.018f,depth-.024f),"Linen",false);
            foreach(float x in new[]{-width*.5f+.0025f,width*.5f-.0025f})
                LobbyPiece(book,"Cover_"+Token(x),new Vector3(x,height*.5f,0),new Vector3(.005f,height,depth),cover,false);
            LobbyPiece(book,"Spine",new Vector3(0,height*.5f,depth*.5f-.004f),new Vector3(width,.98f*height,.008f),cover,false);
            LobbyPiece(book,"Spine_Band",new Vector3(0,height*.75f,depth*.5f+.0005f),new Vector3(width-.012f,.012f,.001f),"Linen",false);
            book.gameObject.layer=EnvironmentSampleBuilder.Layer("WorldStatic");
            var collider=book.gameObject.AddComponent<BoxCollider>();collider.center=new Vector3(0,height*.5f,0);collider.size=new Vector3(width,height,depth+.002f);
        }

        static void EnsureHouseDiffuserMaterial()
        {
            const string name="House_Diffuser";string path=Output+"/Materials/"+name+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name,enableInstancing=true};AssetDatabase.CreateAsset(material,path);}
            // Reapply authored values on every build. W2 adjustments belong in these inputs.
            material.SetColor("_BaseColor",new Color(.90f,.78f,.57f));material.SetColor("_EmissionColor",new Color(.18f,.10f,.035f));
            material.EnableKeyword("_EMISSION");material.SetFloat("_Smoothness",.1f);EditorUtility.SetDirty(material);
            materials[name]=material;
        }

        static void CheckHouseDressing(GameObject house,EnvironmentMapDefinition data)
        {
            var root=house.transform.Find("HouseDressing");Need(root!=null,"House dressing missing");
            var halls=new[]{new Bounds(new Vector3(6.06f,1.1f,5.7f),new Vector3(1.8f,2.2f,11.04f)),new Bounds(new Vector3(6.06f,4.1f,5.7f),new Vector3(1.8f,2.2f,11.04f))};
            foreach(var collider in root.GetComponentsInChildren<Collider>())foreach(var hall in halls)
                Need(!ColliderBounds(collider).Intersects(hall),"House dressing blocks a protected hall: "+Hierarchy(collider.transform));
            foreach(var trim in root.Find("RoomCarpentry").GetComponentsInChildren<Collider>())
                foreach(var furniture in house.transform.Find("Furnishings").GetComponentsInChildren<Collider>()){
                    Bounds a=ColliderBounds(trim),b=ColliderBounds(furniture);
                    Vector3 overlap=Vector3.Min(a.max,b.max)-Vector3.Max(a.min,b.min);
                    Need(overlap.x<=.0001f||overlap.y<=.0001f||overlap.z<=.0001f,"Carpentry intersects furniture: "+trim.name+" / "+Hierarchy(furniture.transform));
                }
            foreach(var plate in root.Find("Kitchen_Domestic").GetComponentsInChildren<MeshFilter>().Where(f=>f.name.StartsWith("Plate_",StringComparison.Ordinal)))CheckPlateFacing(plate.sharedMesh);
            var pickup=data.ToolPickupPoints.Single(p=>p.name=="Pickup_LivingTable");
            Need(Vector3.Distance(pickup.localPosition,new Vector3(2.4f,.815f,2.35f))<.0001f,"Living pickup anchor changed");
            foreach(var tool in data.ToolPickupPoints){var toolReserve=new Bounds(tool.position+new Vector3(0,.025f,.18f),new Vector3(.23f,.10f,.48f));
                foreach(var renderer in root.GetComponentsInChildren<Renderer>())Need(!renderer.bounds.Intersects(toolReserve),"House dressing obscures pickup "+tool.name+": "+renderer.name);}
            foreach(Transform fixture in root.Find("CeilingFixtures")){
                string zone=fixture.name.Substring("CeilingFixture_".Length);var anchor=data.PresentationAnchors.Find("LightAnchor_"+zone);
                Need(anchor!=null&&Vector3.Distance(anchor.localPosition,fixture.localPosition-Vector3.up*.38f)<.0001f,"Light anchor and fixture differ");
                Need(Vector3.Dot(anchor.forward,Vector3.down)>.9999f,"Fixture light direction must face down");
            }
            Need(root.GetComponentsInChildren<Light>().Length==0,"Environment source must not duplicate W2 runtime lights");
        }
    }
}
