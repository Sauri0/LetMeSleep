using System;
using System.Collections.Generic;
using System.Linq;
using LetMeSleep.Content.Environment;
using UnityEngine;
using UnityEditor;
using Object=UnityEngine.Object;
public static class CampEdgeCandidate
{
    public sealed class MeshReceipt{public string kind,originalHash,candidateHash,originalGuid;public long originalLocalId;public int changedVertices,triangles;public bool topologyPreserved;public Vector3[] before,after;public float minimumUpwardNormalY;}
    public sealed class Receipt{public string revision="camp-path-north-edge-lower3mm-01",objectName="CAMP_Terrain_Paths";public float deltaY=-.003f;public List<MeshReceipt> meshes=new List<MeshReceipt>();public bool materialReferencesPreserved;}
    public static Receipt Apply(GameObject clone,List<Object> owned)
    {
        Require(clone.GetComponent<EnvironmentMapDefinition>().ContentHash=="fc9399387e8a9d4bcae5f4a70e3291233170f54d92383d63a9a8b5cb648ad168","Expected final Camp baseline");
        var t=clone.GetComponentsInChildren<Transform>(true).Single(x=>x.name=="CAMP_Terrain_Paths");var filter=t.GetComponent<MeshFilter>();var collider=t.GetComponent<MeshCollider>();var materials=t.GetComponent<Renderer>().sharedMaterials;var receipt=new Receipt();
        var targets=new[]{new Vector3(-11.2279024f,.04f,17.4469337f),new Vector3(-9.6029034f,.04f,17.9469337f)};
        Mesh Revise(Mesh source,string kind)
        {
            var mesh=Object.Instantiate(source);owned.Add(mesh);mesh.name="CampNorthEdge3mm_"+kind+"_TEMP";var vertices=source.vertices;var before=new List<Vector3>();var after=new List<Vector3>();
            for(int i=0;i<vertices.Length;i++){var world=t.TransformPoint(vertices[i]);if(!targets.Any(p=>Vector3.Distance(p,world)<.00002f))continue;before.Add(world);world.y+=receipt.deltaY;after.Add(world);vertices[i]=t.InverseTransformPoint(world);}
            Require(before.Count==2,"Expected exactly two shared edge vertices in "+kind);mesh.vertices=vertices;mesh.RecalculateBounds();if(kind=="visible")mesh.RecalculateNormals();
            var r=new MeshReceipt{kind=kind,originalHash=CampTechnicalCandidate.Hash(source),candidateHash=CampTechnicalCandidate.Hash(mesh),changedVertices=before.Count,triangles=mesh.triangles.Length/3,topologyPreserved=mesh.triangles.SequenceEqual(source.triangles),before=before.ToArray(),after=after.ToArray(),minimumUpwardNormalY=1};
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source,out r.originalGuid,out r.originalLocalId);var triangles=mesh.triangles;
            for(int i=0;i<triangles.Length;i+=3){var a=t.TransformPoint(vertices[triangles[i]]);var b=t.TransformPoint(vertices[triangles[i+1]]);var c=t.TransformPoint(vertices[triangles[i+2]]);if(a.y>.03f&&b.y>.03f&&c.y>.03f)r.minimumUpwardNormalY=Mathf.Min(r.minimumUpwardNormalY,Vector3.Cross(b-a,c-a).normalized.y);}
            Require(r.topologyPreserved&&r.minimumUpwardNormalY>.999f,"Topology/support normal changed unexpectedly");receipt.meshes.Add(r);return mesh;
        }
        filter.sharedMesh=Revise(filter.sharedMesh,"visible");collider.sharedMesh=Revise(collider.sharedMesh,"collision");receipt.materialReferencesPreserved=t.GetComponent<Renderer>().sharedMaterials.SequenceEqual(materials);Physics.SyncTransforms();return receipt;
    }
    static void Require(bool v,string s){if(!v)throw new Exception(s);}
}
