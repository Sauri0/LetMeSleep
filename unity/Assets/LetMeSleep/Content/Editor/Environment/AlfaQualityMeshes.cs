using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LetMeSleep.Content.Editor
{
    public static partial class AlfaMapBuilder
    {
        // Authored metre-space geometry. These are actual rounded, lofted and folded
        // meshes, never nonuniform instances of the old tabletop mesh.
        sealed class QualityMesh
        {
            public readonly List<Vector3> vertices=new List<Vector3>();
            readonly List<Vector2> uv=new List<Vector2>();
            readonly List<int>[] triangles={new List<int>(),new List<int>(),new List<int>()};
            public void Triangle(Vector3 a,Vector3 b,Vector3 c,Vector3 outward,int material=0)
            {
                var normal=Vector3.Cross(b-a,c-a);if(normal.sqrMagnitude<1e-20f)return;
                if(Vector3.Dot(normal,outward)<0){var swap=b;b=c;c=swap;}
                int start=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);
                foreach(var p in new[]{a,b,c})uv.Add(Mathf.Abs(outward.y)>.7f?new Vector2(p.x,p.z):Mathf.Abs(outward.x)>.7f?new Vector2(p.z,p.y):new Vector2(p.x,p.y));
                triangles[material].AddRange(new[]{start,start+1,start+2});
            }
            public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector3 outward,int material=0)
            {Triangle(a,b,c,outward,material);Triangle(a,c,d,outward,material);}
            public Mesh Save(string name,int materialCount)
            {
                Need(vertices.Count>0,"Empty quality mesh: "+name);
                var mesh=new Mesh{name="Quality_"+name};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.subMeshCount=materialCount;
                for(int i=0;i<materialCount;i++)mesh.SetTriangles(triangles[i],i);
                mesh.RecalculateNormals();mesh.RecalculateBounds();Unwrapping.GenerateSecondaryUVSet(mesh);
                string path=Output+"/Meshes/Quality_"+name+".asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(saved==null){AssetDatabase.CreateAsset(mesh,path);saved=mesh;}else{EditorUtility.CopySerialized(mesh,saved);UnityEngine.Object.DestroyImmediate(mesh);EditorUtility.SetDirty(saved);}
                return saved;
            }
        }

        static Transform QualityPart(Transform parent,string name,Vector3 position,QualityMesh shape,params string[] palette)
        {
            var part=Child(parent,name);part.localPosition=position;
            string key=Hierarchy(part).Replace('/','_').Replace(' ','_');
            part.gameObject.AddComponent<MeshFilter>().sharedMesh=shape.Save(key,palette.Length);
            var renderer=part.gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterials=palette.Select(p=>materials[p]).ToArray();
            renderer.receiveGI=ReceiveGI.Lightmaps;renderer.reflectionProbeUsage=ReflectionProbeUsage.BlendProbes;
            GameObjectUtility.SetStaticEditorFlags(part.gameObject,StaticEditorFlags.ContributeGI|StaticEditorFlags.OccludeeStatic);
            return part;
        }

        static QualityMesh QualityRoundedBox(Vector3 size,float radius,float footScale=1)
        {
            var mesh=new QualityMesh();var half=size*.5f;radius=Mathf.Min(radius,Mathf.Min(half.x,Mathf.Min(half.y,half.z))*.9f);
            for(int axis=0;axis<3;axis++)foreach(float sign in new[]{-1f,1f}){
                int u=(axis+1)%3,v=(axis+2)%3;var outward=Vector3.zero;outward[axis]=sign;
                var gu=new[]{-half[u],-half[u]+radius,half[u]-radius,half[u]};var gv=new[]{-half[v],-half[v]+radius,half[v]-radius,half[v]};
                Func<int,int,Vector3> point=(i,j)=>{
                    var p=Vector3.zero;p[axis]=half[axis]*sign;p[u]=gu[i];p[v]=gv[j];
                    var core=new Vector3(Mathf.Clamp(p.x,-half.x+radius,half.x-radius),Mathf.Clamp(p.y,-half.y+radius,half.y-radius),Mathf.Clamp(p.z,-half.z+radius,half.z-radius));
                    p=core+(p-core).normalized*radius;float taper=Mathf.Lerp(footScale,1,(p.y+half.y)/size.y);p.x*=taper;p.z*=taper;return p;
                };
                for(int i=0;i<3;i++)for(int j=0;j<3;j++)mesh.Quad(point(i,j),point(i+1,j),point(i+1,j+1),point(i,j+1),outward);
            }
            return mesh;
        }

        static Transform QualityTimber(Transform parent,string name,Vector3 center,Vector3 size,string material="Quality_Wood",float bevel=.009f,float footScale=1)
        {return QualityPart(parent,name,center,QualityRoundedBox(size,bevel,footScale),material);}

        static QualityMesh QualityLathe(params Vector2[] profile)
        {
            double area=0;for(int i=0;i<profile.Length;i++){var p=profile[i];var q=profile[(i+1)%profile.Length];area+=p.x*q.y-q.x*p.y;}
            Need(area>0,"Lathed quality profile must travel outward at its bottom and upward on its outer wall");
            const int sides=12;var mesh=new QualityMesh();
            for(int ring=0;ring<profile.Length-1;ring++)for(int side=0;side<sides;side++){
                float a=side*Mathf.PI*2/sides,b=(side+1)*Mathf.PI*2/sides,m=(a+b)*.5f;var p=profile[ring];var q=profile[ring+1];
                var normal=new Vector3((q.y-p.y)*Mathf.Cos(m),p.x-q.x,(q.y-p.y)*Mathf.Sin(m));
                mesh.Quad(new Vector3(p.x*Mathf.Cos(a),p.y,p.x*Mathf.Sin(a)),new Vector3(p.x*Mathf.Cos(b),p.y,p.x*Mathf.Sin(b)),
                    new Vector3(q.x*Mathf.Cos(b),q.y,q.x*Mathf.Sin(b)),new Vector3(q.x*Mathf.Cos(a),q.y,q.x*Mathf.Sin(a)),normal);
            }
            return mesh;
        }

        static Vector2[] QualitySoftOutline(Vector2 half)
        {
            // A continuous superellipse gives bowed sides and rounded cloth corners,
            // instead of a rectangle with straight chamfer cuts.
            var points=new Vector2[20];for(int i=0;i<points.Length;i++){
                float angle=i*Mathf.PI*2/points.Length,c=Mathf.Cos(angle),s=Mathf.Sin(angle);
                points[i]=new Vector2(half.x*Mathf.Sign(c)*Mathf.Pow(Mathf.Abs(c),.55f),half.y*Mathf.Sign(s)*Mathf.Pow(Mathf.Abs(s),.55f));
            }return points;
        }

        static QualityMesh QualityPillow(Vector3 size,bool seat=false)
        {
            // Face is XY for a standing cushion and XZ for a seat pad. Nine rings
            // converge on convex front/back centres; there is no broad flat cap.
            var mesh=new QualityMesh();Vector3 dims=seat?new Vector3(size.x,size.z,size.y):size;
            var outline=QualitySoftOutline(new Vector2(dims.x,dims.y)*.5f);
            var depths=seat?new[]{-.5f,-.32f,0,.32f,.5f}:new[]{-.5f,-.47f,-.37f,-.20f,0,.20f,.37f,.47f,.5f};
            var scales=seat?new[]{.60f,.91f,1f,.91f,.60f}:new[]{0f,.35f,.70f,.93f,1f,.93f,.70f,.35f,0f};
            Func<Vector3,Vector3> orient=p=>seat?new Vector3(p.x,p.z,p.y):p;
            for(int ring=0;ring<depths.Length-1;ring++)for(int i=0;i<outline.Length;i++){
                int next=(i+1)%outline.Length;var p=outline[i];var q=outline[next];
                var a=new Vector3(p.x*scales[ring],p.y*scales[ring],depths[ring]*dims.z);var b=new Vector3(q.x*scales[ring],q.y*scales[ring],depths[ring]*dims.z);
                var c=new Vector3(q.x*scales[ring+1],q.y*scales[ring+1],depths[ring+1]*dims.z);var d=new Vector3(p.x*scales[ring+1],p.y*scales[ring+1],depths[ring+1]*dims.z);
                mesh.Quad(orient(a),orient(b),orient(c),orient(d),orient((a+b+c+d)*.25f).normalized);
            }
            // The weight-bearing seat retains a broad contact area; decorative and
            // back cushions use the fully convex profile above.
            if(seat)foreach(int end in new[]{0,depths.Length-1})for(int i=0;i<outline.Length;i++){
                var p=outline[i]*scales[end];var q=outline[(i+1)%outline.Length]*scales[end];
                mesh.Triangle(orient(new Vector3(0,0,depths[end]*dims.z)),orient(new Vector3(p.x,p.y,depths[end]*dims.z)),orient(new Vector3(q.x,q.y,depths[end]*dims.z)),orient(Vector3.forward*(end==0?-1:1)));
            }
            // A continuous narrow sewn band following the real loft, not a painted square.
            for(int i=0;i<outline.Length;i++){
                var a=outline[i]*.997f;var b=outline[(i+1)%outline.Length]*.997f;
                mesh.Quad(orient(new Vector3(a.x,a.y,-.002f)),orient(new Vector3(b.x,b.y,-.002f)),orient(new Vector3(b.x,b.y,.002f)),orient(new Vector3(a.x,a.y,.002f)),orient(new Vector3(a.x+b.x,a.y+b.y,0)),1);
            }
            return mesh;
        }

        static QualityMesh QualityRestingCushion(Vector3 size,float lean,float twist)
        {
            var mesh=QualityPillow(size);var rotation=Quaternion.Euler(lean,0,twist);
            for(int i=0;i<mesh.vertices.Count;i++)mesh.vertices[i]=rotation*mesh.vertices[i];
            float correction=-size.y*.5f-mesh.vertices.Min(v=>v.y);
            for(int i=0;i<mesh.vertices.Count;i++)mesh.vertices[i]+=Vector3.up*correction;
            return mesh;
        }

        static QualityMesh QualityThrow()
        {
            var mesh=new QualityMesh();var support=QualityPillow(new Vector3(.90f,.18f,.72f),true).vertices.ToArray();
            var path=new[]{new Vector2(.04f,0),new Vector2(-.08f,0),new Vector2(-.16f,0),new Vector2(-.23f,0),new Vector2(-.28f,0),new Vector2(-.33f,0),new Vector2(-.37f,0),new Vector2(-.39f,0),new Vector2(-.411f,.45f),new Vector2(-.425f,.38f),new Vector2(-.419f,.29f)};
            Func<int,int,float,Vector3> p=(i,j,side)=>{
                float x=-.19f+i*.0475f,z=path[j].x;float supportY=QualitySurfaceHeight(support,x,z+.04f);
                float y=float.IsNegativeInfinity(supportY)?path[j].y:supportY+.485f;
                float hanging=j>=8?(j-7)/3f:0;z+=.009f*Mathf.Sin(i*1.4f)*hanging;y+=.018f*Mathf.Sin(i*.83f)*hanging;
                return new Vector3(x,y+.003f+.002f*(1-Mathf.Cos(i*Mathf.PI*.5f))+side*.003f,z);
            };
            for(int i=0;i<8;i++)for(int j=0;j<path.Length-1;j++){
                int slot=i==0||i==7||j==path.Length-2?1:0;
                var normal=Vector3.Cross(p(i+1,j,1)-p(i,j,1),p(i,j+1,1)-p(i,j,1));
                mesh.Quad(p(i,j,1),p(i+1,j,1),p(i+1,j+1,1),p(i,j+1,1),normal,slot);
                mesh.Quad(p(i,j,-1),p(i,j+1,-1),p(i+1,j+1,-1),p(i+1,j,-1),-normal,slot);
            }
            foreach(int i in new[]{0,8})for(int j=0;j<path.Length-1;j++)mesh.Quad(p(i,j,1),p(i,j,-1),p(i,j+1,-1),p(i,j+1,1),i==0?Vector3.left:Vector3.right,1);
            foreach(int j in new[]{0,path.Length-1})for(int i=0;i<8;i++)mesh.Quad(p(i,j,1),p(i+1,j,1),p(i+1,j,-1),p(i,j,-1),j==0?Vector3.forward:Vector3.back,1);
            return mesh;
        }

        static float QualitySurfaceHeight(Vector3[] vertices,float x,float z)
        {
            float height=float.NegativeInfinity;
            for(int i=0;i<vertices.Length;i+=3){var a=vertices[i];var b=vertices[i+1];var c=vertices[i+2];
                float denominator=(b.z-c.z)*(a.x-c.x)+(c.x-b.x)*(a.z-c.z);if(Mathf.Abs(denominator)<1e-10f)continue;
                float u=((b.z-c.z)*(x-c.x)+(c.x-b.x)*(z-c.z))/denominator;
                float v=((c.z-a.z)*(x-c.x)+(a.x-c.x)*(z-c.z))/denominator,w=1-u-v;
                if(u>=-.00001f&&v>=-.00001f&&w>=-.00001f)height=Mathf.Max(height,u*a.y+v*b.y+w*c.y);
            }return height;
        }

        static void CheckQualityThrowSupport(Transform sofa,MeshFilter cloth)
        {
            var surfaces=sofa.GetComponentsInChildren<MeshFilter>().Where(f=>f.name.StartsWith("Seat_Pad_",StringComparison.Ordinal))
                .Select(f=>f.sharedMesh.vertices.Select(v=>f.transform.TransformPoint(v)).ToArray()).ToArray();
            Need(surfaces.Length==2,"Expected two actual sofa seat meshes");int contacts=0;
            foreach(var vertex in cloth.sharedMesh.vertices){var p=cloth.transform.TransformPoint(vertex);float support=surfaces.Max(s=>QualitySurfaceHeight(s,p.x,p.z));
                if(float.IsNegativeInfinity(support))continue;
                Need(p.y>=support-.001f&&p.y<=support+.012f,"Throw must follow the actual upholstered surface");
                if(Mathf.Abs(p.y-support)<.001f)contacts++;
            }
            Need(contacts>=8,"Draped throw must have real contact vertices on upholstery");
        }

        static QualityMesh QualityCurtain(float width,float height)
        {
            var mesh=new QualityMesh();const int across=12,down=5;
            Func<int,int,float,Vector3> p=(i,j,side)=>{
                float u=i/(float)across,t=j/(float)down;float depth=.035f*Mathf.Cos(u*6*Mathf.PI);
                return new Vector3((u-.5f)*width*(.85f+.15f*t),-t*height+.009f*Mathf.Sin(u*4*Mathf.PI)*t,depth+side*.0025f);
            };
            for(int i=0;i<across;i++)for(int j=0;j<down;j++){
                int slot=j==down-1?1:0;mesh.Quad(p(i,j,1),p(i+1,j,1),p(i+1,j+1,1),p(i,j+1,1),Vector3.forward,slot);
                mesh.Quad(p(i,j,-1),p(i,j+1,-1),p(i+1,j+1,-1),p(i+1,j,-1),Vector3.back,slot);
            }
            foreach(int i in new[]{0,across})for(int j=0;j<down;j++)mesh.Quad(p(i,j,1),p(i,j+1,1),p(i,j+1,-1),p(i,j,-1),i==0?Vector3.left:Vector3.right,1);
            foreach(int j in new[]{0,down})for(int i=0;i<across;i++)mesh.Quad(p(i,j,1),p(i,j,-1),p(i+1,j,-1),p(i+1,j,1),j==0?Vector3.up:Vector3.down,1);
            return mesh;
        }

        static void RemoveQualityReplacedVisuals(Transform root)
        {
            foreach(var filter in root.GetComponentsInChildren<MeshFilter>()){
                var renderer=filter.GetComponent<MeshRenderer>();if(renderer!=null)UnityEngine.Object.DestroyImmediate(renderer);
                UnityEngine.Object.DestroyImmediate(filter);
            }
            foreach(var lod in root.GetComponentsInChildren<LODGroup>())UnityEngine.Object.DestroyImmediate(lod);
        }

        static void MakeQualityMaterial(string name,Color color,float roughness=.7f,float metallic=0,Texture texture=null)
        {
            string path=Output+"/Materials/"+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name,enableInstancing=true};AssetDatabase.CreateAsset(material,path);}
            material.SetColor("_BaseColor",color);material.SetFloat("_Smoothness",1-roughness);material.SetFloat("_Metallic",metallic);
            material.SetTexture("_BaseMap",texture);material.SetTextureScale("_BaseMap",texture==null?Vector2.one:new Vector2(12,12));
            EditorUtility.SetDirty(material);materials[name]=material;
        }

        static void QualityMaterials()
        {
            // A repeatable over/under weave with quiet contrast. UVs are metres;
            // 12 repeats per metre give a fine 2.6 mm yarn pitch.
            const int size=128;var texture=new Texture2D(size,size,TextureFormat.RGBA32,true){name="Quality_LinenWeave",wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Trilinear};
            var pixels=new Color[size*size];for(int y=0;y<size;y++)for(int x=0;x<size;x++){
                bool warp=((x/4+y/4)&1)==0;float ridge=warp?Mathf.Sin((x%4+.5f)*Mathf.PI/4):Mathf.Sin((y%4+.5f)*Mathf.PI/4);
                float value=.94f+.06f*ridge;pixels[y*size+x]=new Color(value,value,value,1);
            }
            texture.SetPixels(pixels);texture.Apply(true,false);string path=Output+"/Materials/Quality_LinenWeave.asset";
            var saved=AssetDatabase.LoadAssetAtPath<Texture2D>(path);if(saved==null){AssetDatabase.CreateAsset(texture,path);saved=texture;}else{EditorUtility.CopySerialized(texture,saved);UnityEngine.Object.DestroyImmediate(texture);EditorUtility.SetDirty(saved);}
            MakeQualityMaterial("Quality_Wood",new Color(.48f,.29f,.14f),.84f);MakeQualityMaterial("Quality_WoodLight",new Color(.55f,.34f,.17f),.84f);MakeQualityMaterial("Quality_WoodEnd",new Color(.36f,.2175f,.105f),.84f);
            MakeQualityMaterial("Quality_Rust",new Color(.32f,.07f,.055f),.9f,0,saved);MakeQualityMaterial("Quality_Blue",new Color(.055f,.11f,.22f),.9f,0,saved);MakeQualityMaterial("Quality_Linen",new Color(.48f,.38f,.27f),.92f,0,saved);
            MakeQualityMaterial("Quality_Thread",new Color(.37f,.22f,.13f),.95f);MakeQualityMaterial("Quality_Iron",new Color(.09f,.115f,.12f),.42f,.35f);
            MakeQualityMaterial("Quality_Leaf",new Color(.24f,.43f,.095f),.8f);MakeQualityMaterial("Quality_LeafLight",new Color(.38f,.55f,.16f),.8f);
            MakeQualityMaterial("Quality_Terracotta",new Color(.55f,.25f,.12f),.88f);MakeQualityMaterial("Quality_Soil",new Color(.12f,.085f,.05f),.98f);
        }
    }
}
