using System;
using System.Collections.Generic;
using System.Linq;
using LetMeSleep.Content.Environment;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

// Local visible+physical bevel of the measured second-tread front edge only.
public static class PuertoStairCandidate
{
    public const string Name="Lighthouse_Approach_StoneStairway";
    public sealed class Receipt
    {
        public string revision="puerto-exterior-stair-second-tread-bevel-01",objectName=Name,originalAssetPath,originalGuid,originalMeshSha256,candidateMeshSha256;
        public long originalMeshLocalId;
        public Vector3 edgeStart,edgeEnd,inward,originalMin,originalMax,candidateMin,candidateMax;
        public float bevel=.03f;
        public int originalTriangles,candidateTriangles,selectedTriangles,unchangedTriangles,capTriangles,originalTangents,candidateTangents;
        public bool renderAndColliderMatch,materialReferencesPreserved;
    }
    public static Receipt Apply(GameObject clone,List<Object> owned)
    {
        Require(clone.GetComponent<EnvironmentMapDefinition>().MapId=="hf-puerto-del-faro-v1","Final Puerto v1 only.");
        var t=clone.GetComponentsInChildren<Transform>(true).Single(x=>x.name==Name);var filter=t.GetComponent<MeshFilter>();var collider=t.GetComponent<MeshCollider>();var renderer=t.GetComponent<Renderer>();
        Require(filter && collider && renderer && filter.sharedMesh==collider.sharedMesh && !collider.convex,"Matching static visible/collision mesh required.");
        var source=filter.sharedMesh;var materials=renderer.sharedMaterials;
        Require(source.uv.Length==0 && source.colors.Length==0 && source.blendShapeCount==0,"Unexpected source vertex attributes: uv="+source.uv.Length+", colors="+source.colors.Length+", tangents="+source.tangents.Length+"; do not discard them.");
        var vertices=source.vertices;var normals=source.normals;var tangents=source.tangents;var world=vertices.Select(t.TransformPoint).ToArray();
        var receipt=new Receipt{originalAssetPath=AssetDatabase.GetAssetPath(source),originalMeshSha256=CampTechnicalCandidate.Hash(source),originalTriangles=source.triangles.Length/3,originalTangents=tangents.Length,
            edgeStart=new Vector3(17.46543884f,5.05693865f,15.18617439f),edgeEnd=new Vector3(19.08456039f,5.05693865f,13.28132629f),inward=new Vector3(.275f,0,.23375f).normalized};
        Require(receipt.originalAssetPath=="Assets/LetMeSleep/Content/Environment/HiggsfieldMaps/hf-puerto-del-faro-v1/Models/Environment.fbx","Original FBX required.");
        Require(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source,out receipt.originalGuid,out receipt.originalMeshLocalId),"Persisted source required.");
        receipt.originalMin=new Vector3(world.Min(x=>x.x),world.Min(x=>x.y),world.Min(x=>x.z));receipt.originalMax=new Vector3(world.Max(x=>x.x),world.Max(x=>x.y),world.Max(x=>x.z));
        var width=(receipt.edgeEnd-receipt.edgeStart).normalized;float span=Vector3.Distance(receipt.edgeEnd,receipt.edgeStart),run=new Vector2(.275f,.23375f).magnitude,top=receipt.edgeStart.y;
        float U(Vector3 x)=>Vector3.Dot(x-receipt.edgeStart,receipt.inward);
        float W(Vector3 x)=>Vector3.Dot(x-receipt.edgeStart,width);
        float D(Vector3 x)=>x.y-top-U(x)+receipt.bevel;
        bool Selected(Vector3 x)=>U(x)>=-.00003f && U(x)<=run+.00003f && W(x)>=-.00003f && W(x)<=span+.00003f && x.y>=top-.30003f && x.y<=top+.00003f;
        var output=new List<Vector3>();var outNormals=new List<Vector3>();var outTangents=new List<Vector4>();var indices=Enumerable.Range(0,source.subMeshCount).Select(_=>new List<int>()).ToArray();var cap=new List<Vector3>();int capMaterial=-1;
        void Triangle(Vector3 a,Vector3 b,Vector3 c,int material,Vector3? na=null,Vector3? nb=null,Vector3? nc=null)
        {if(Vector3.Cross(b-a,c-a).sqrMagnitude<1e-15f)return;var n=t.InverseTransformDirection(Vector3.Cross(b-a,c-a).normalized);int start=output.Count;output.Add(t.InverseTransformPoint(a));output.Add(t.InverseTransformPoint(b));output.Add(t.InverseTransformPoint(c));outNormals.Add(na??n);outNormals.Add(nb??n);outNormals.Add(nc??n);if(tangents.Length==vertices.Length){var basis=Vector3.Cross(n,Mathf.Abs(n.y)<.9f?Vector3.up:Vector3.right).normalized;for(int j=0;j<3;j++)outTangents.Add(new Vector4(basis.x,basis.y,basis.z,1));}indices[material].AddRange(new[]{start,start+1,start+2});}
        for(int sub=0;sub<source.subMeshCount;sub++)
        {
            var tris=source.GetTriangles(sub);
            for(int i=0;i<tris.Length;i+=3)
            {
                int ia=tris[i],ib=tris[i+1],ic=tris[i+2];var a=world[ia];var b=world[ib];var c=world[ic];
                if(!Selected(a)||!Selected(b)||!Selected(c)){int start=output.Count;foreach(int index in new[]{ia,ib,ic}){output.Add(vertices[index]);outNormals.Add(normals[index]);if(tangents.Length==vertices.Length)outTangents.Add(tangents[index]);}indices[sub].AddRange(new[]{start,start+1,start+2});receipt.unchangedTriangles++;continue;}
                receipt.selectedTriangles++;if(Vector3.Dot(Vector3.Cross(b-a,c-a).normalized,-receipt.inward)>.99f)capMaterial=sub;
                var polygon=new List<Vector3>{a,b,c};var clipped=new List<Vector3>();
                for(int j=0;j<polygon.Count;j++)
                {
                    var v=polygon[j];var next=polygon[(j+1)%polygon.Count];float dv=D(v),dn=D(next);bool vin=dv<=0,nin=dn<=0;
                    if(vin)clipped.Add(v);
                    if(vin!=nin){var intersection=Vector3.LerpUnclamped(v,next,dv/(dv-dn));clipped.Add(intersection);if(!cap.Any(q=>Vector3.Distance(q,intersection)<.00001f))cap.Add(intersection);}
                }
                for(int j=1;j+1<clipped.Count;j++)Triangle(clipped[0],clipped[j],clipped[j+1],sub);
            }
        }
        Require(receipt.selectedTriangles==12 && cap.Count>=4 && capMaterial>=0,"Expected exactly the second closed box and its cut edge.");
        var center=cap.Aggregate(Vector3.zero,(a,b)=>a+b)/cap.Count;var normal=(Vector3.up-receipt.inward).normalized;var axis=width;var second=Vector3.Cross(normal,axis);
        cap=cap.OrderBy(x=>Mathf.Atan2(Vector3.Dot(x-center,second),Vector3.Dot(x-center,axis))).ToList();
        // Keep collinear edge intersections so the clipped source triangles share boundaries exactly.
        for(int i=0;i<cap.Count;i++){Triangle(center,cap[i],cap[(i+1)%cap.Count],capMaterial);receipt.capTriangles++;}
        var mesh=new Mesh{name="PuertoSecondTreadBevel_TEMP"};owned.Add(mesh);mesh.vertices=output.ToArray();mesh.normals=outNormals.ToArray();if(outTangents.Count>0)mesh.tangents=outTangents.ToArray();receipt.candidateTangents=outTangents.Count;mesh.subMeshCount=source.subMeshCount;
        for(int i=0;i<indices.Length;i++)mesh.SetTriangles(indices[i],i);mesh.RecalculateBounds();
        receipt.candidateTriangles=mesh.triangles.Length/3;receipt.candidateMeshSha256=CampTechnicalCandidate.Hash(mesh);var result=mesh.vertices.Select(t.TransformPoint).ToArray();receipt.candidateMin=new Vector3(result.Min(x=>x.x),result.Min(x=>x.y),result.Min(x=>x.z));receipt.candidateMax=new Vector3(result.Max(x=>x.x),result.Max(x=>x.y),result.Max(x=>x.z));
        Require(Vector3.Distance(receipt.originalMin,receipt.candidateMin)<.00001f && Vector3.Distance(receipt.originalMax,receipt.candidateMax)<.00001f,"Unchanged stair outer bounds required.");
        filter.sharedMesh=mesh;collider.sharedMesh=mesh;receipt.renderAndColliderMatch=filter.sharedMesh==collider.sharedMesh;receipt.materialReferencesPreserved=renderer.sharedMaterials.SequenceEqual(materials);Physics.SyncTransforms();return receipt;
    }
    static void Require(bool value,string message){if(!value)throw new Exception(message);}
}
