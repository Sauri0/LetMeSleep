using System;
using System.Collections.Generic;
using System.Linq;
using LetMeSleep.Content.Environment;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

// Coordinator-authorized notch inside the existing swim stair opening only.
public static class YateBulkheadCandidate
{
    public const string Name="YATE_Lower_EndBulkhead_-11.6";
    public sealed class Receipt
    {
        public string revision="yate-swim-bulkhead-notch-01",objectName=Name,originalAssetPath,originalGuid,originalMeshSha256,candidateMeshSha256;
        public long originalMeshLocalId;
        public Vector3 originalMin,originalMax,candidateMin,candidateMax;
        public float openingMinX=1.625f,openingMaxX=3.075f,remainingTopY=2.70f,removedVolume;
        public int originalTriangles,candidateTriangles,inheritedUvVertices,newSurfaceUvVertices;
        public bool renderAndColliderMatch,materialReferencesPreserved;
    }
    public static Receipt Apply(GameObject clone,List<Object> owned)
    {
        Require(clone.GetComponent<EnvironmentMapDefinition>().MapId=="hf-yate-a-la-deriva-v3","Final Yate v3 only.");
        var transform=clone.GetComponentsInChildren<Transform>(true).Single(t=>t.name==Name);var filter=transform.GetComponent<MeshFilter>();var collider=transform.GetComponent<MeshCollider>();var renderer=transform.GetComponent<Renderer>();
        Require(filter && collider && renderer && filter.sharedMesh==collider.sharedMesh && !collider.convex,"Expected matching original visible/collision mesh.");
        var source=filter.sharedMesh;var materials=renderer.sharedMaterials;Require(source.subMeshCount==1,"Single material wall required.");
        var local=source.vertices;var world=local.Select(transform.TransformPoint).ToArray();var lo=new Vector3(world.Min(p=>p.x),world.Min(p=>p.y),world.Min(p=>p.z));var hi=new Vector3(world.Max(p=>p.x),world.Max(p=>p.y),world.Max(p=>p.z));
        Require(Vector3.Distance(lo,new Vector3(-3.2f,.8f,-11.67f))<.0001f && Vector3.Distance(hi,new Vector3(3.2f,3.18f,-11.53f))<.0001f,"Original wall bounds changed.");
        var receipt=new Receipt{originalAssetPath=AssetDatabase.GetAssetPath(source),originalMeshSha256=CampTechnicalCandidate.Hash(source),originalMin=lo,originalMax=hi,originalTriangles=source.triangles.Length/3};
        Require(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source,out receipt.originalGuid,out receipt.originalMeshLocalId),"Persisted original source required.");
        var polygon=new[]{new Vector2(lo.x,lo.y),new Vector2(hi.x,lo.y),new Vector2(hi.x,hi.y),new Vector2(receipt.openingMaxX,hi.y),new Vector2(receipt.openingMaxX,receipt.remainingTopY),new Vector2(receipt.openingMinX,receipt.remainingTopY),new Vector2(receipt.openingMinX,hi.y),new Vector2(lo.x,hi.y)};
        var remaining=Enumerable.Range(0,polygon.Length).ToList();var planar=new List<int>();
        float Cross(Vector2 a,Vector2 b,Vector2 c)=>(b.x-a.x)*(c.y-a.y)-(b.y-a.y)*(c.x-a.x);
        bool Inside(Vector2 p,Vector2 a,Vector2 b,Vector2 c)=>Cross(a,b,p)>=-1e-7f && Cross(b,c,p)>=-1e-7f && Cross(c,a,p)>=-1e-7f;
        while(remaining.Count>3)
        {
            bool clipped=false;
            for(int i=0;i<remaining.Count;i++)
            {
                int a=remaining[(i+remaining.Count-1)%remaining.Count],b=remaining[i],c=remaining[(i+1)%remaining.Count];
                if(Cross(polygon[a],polygon[b],polygon[c])<=1e-7f || remaining.Any(j=>j!=a && j!=b && j!=c && Inside(polygon[j],polygon[a],polygon[b],polygon[c])))continue;
                planar.AddRange(new[]{a,b,c});remaining.RemoveAt(i);clipped=true;break;
            }
            Require(clipped,"Notch triangulation failed.");
        }
        planar.AddRange(remaining);var vertices=new List<Vector3>();var uvs=new List<Vector2>();var sourceUv=source.uv;var sourceTriangles=source.triangles;
        Vector2 Uv(Vector3 worldPoint)
        {
            if(sourceUv.Length!=local.Length)return Vector2.zero;
            var p=transform.InverseTransformPoint(worldPoint);
            for(int i=0;i<sourceTriangles.Length;i+=3)
            {
                int ia=sourceTriangles[i],ib=sourceTriangles[i+1],ic=sourceTriangles[i+2];var a=local[ia];var ab=local[ib]-a;var ac=local[ic]-a;var ap=p-a;var normal=Vector3.Cross(ab,ac);
                if(Mathf.Abs(Vector3.Dot(ap,normal.normalized))>.00001f)continue;
                float aa=Vector3.Dot(ab,ab),bb=Vector3.Dot(ab,ac),cc=Vector3.Dot(ac,ac),pa=Vector3.Dot(ap,ab),pc=Vector3.Dot(ap,ac),det=aa*cc-bb*bb;
                if(Mathf.Abs(det)<1e-12f)continue;float v=(cc*pa-bb*pc)/det,w=(aa*pc-bb*pa)/det,u=1-v-w;
                if(u>=-.0001f && v>=-.0001f && w>=-.0001f){receipt.inheritedUvVertices++;return sourceUv[ia]*u+sourceUv[ib]*v+sourceUv[ic]*w;}
            }
            receipt.newSurfaceUvVertices++;return new Vector2(worldPoint.x,worldPoint.y);
        }
        void Triangle(Vector3 a,Vector3 b,Vector3 c){foreach(var p in new[]{a,b,c}){vertices.Add(transform.InverseTransformPoint(p));uvs.Add(Uv(p));}}
        Vector3 At(int i,float z)=>new Vector3(polygon[i].x,polygon[i].y,z);
        for(int i=0;i<planar.Count;i+=3){int a=planar[i],b=planar[i+1],c=planar[i+2];Triangle(At(a,hi.z),At(b,hi.z),At(c,hi.z));Triangle(At(a,lo.z),At(c,lo.z),At(b,lo.z));}
        for(int i=0;i<polygon.Length;i++){int j=(i+1)%polygon.Length;Triangle(At(i,lo.z),At(j,lo.z),At(j,hi.z));Triangle(At(i,lo.z),At(j,hi.z),At(i,hi.z));}
        var mesh=new Mesh{name="YateSwimBulkheadNotch_TEMP"};owned.Add(mesh);mesh.vertices=vertices.ToArray();mesh.triangles=Enumerable.Range(0,vertices.Count).ToArray();if(sourceUv.Length==local.Length)mesh.uv=uvs.ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();
        var result=mesh.vertices.Select(transform.TransformPoint).ToArray();receipt.candidateMin=new Vector3(result.Min(p=>p.x),result.Min(p=>p.y),result.Min(p=>p.z));receipt.candidateMax=new Vector3(result.Max(p=>p.x),result.Max(p=>p.y),result.Max(p=>p.z));
        Require(Vector3.Distance(receipt.candidateMin,lo)<.00001f && Vector3.Distance(receipt.candidateMax,hi)<.00001f,"Outer wall bounds must remain unchanged.");
        receipt.removedVolume=(receipt.openingMaxX-receipt.openingMinX)*(hi.y-receipt.remainingTopY)*(hi.z-lo.z);receipt.candidateTriangles=vertices.Count/3;receipt.candidateMeshSha256=CampTechnicalCandidate.Hash(mesh);
        filter.sharedMesh=mesh;collider.sharedMesh=mesh;receipt.renderAndColliderMatch=filter.sharedMesh==collider.sharedMesh;receipt.materialReferencesPreserved=renderer.sharedMaterials.SequenceEqual(materials);Physics.SyncTransforms();return receipt;
    }
    static void Require(bool value,string text){if(!value)throw new Exception(text);}
}
