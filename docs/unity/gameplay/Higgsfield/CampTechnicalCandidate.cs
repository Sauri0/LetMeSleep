using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using LetMeSleep.Content.Environment;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

// Explicit Camp v2 clone-only experiments. Never saves or changes visible meshes.
public static class CampTechnicalCandidate
{
    public sealed class Receipt
    {
        public string revision="camp-paths-exact-down-extrusion-01",originalMeshPath,originalMeshGuid,originalMeshSha256,candidateMeshSha256;
        public int originalVertices,originalTriangles,weldedVertices,components,boundaryEdges,candidateTriangles;
        public float topY,bottomY=-.035f,contactOffset;
        public int cookingOptions;
        public bool originalTopPreserved,visibleMeshPreserved,convex;
    }
    public sealed class SpawnReceipt
    {
        public string name,supportCollider,beforeBlocker,afterBlocker;
        public Vector3 before,after,supportPoint,supportNormal;
        public float beforePenetration,afterPenetration;
        public bool appliedToClone;
    }
    public static Receipt ApplyPaths(GameObject clone,List<Object> owned)
    {
        Require(clone.GetComponent<EnvironmentMapDefinition>().MapId=="hf-campamento-pinar-v2","Camp v2 only.");
        var colliders=clone.GetComponentsInChildren<MeshCollider>(true).Where(c=>c.name=="CAMP_Terrain_Paths").ToArray();
        Require(colliders.Length==1,"Exactly one authored Paths collider required.");
        var collider=colliders[0];var source=collider.sharedMesh;var filter=collider.GetComponent<MeshFilter>();var visible=filter?filter.sharedMesh:null;
        Require(source && !collider.convex && collider.enabled,"Original nonconvex enabled mesh required.");
        var vertices=source.vertices;var top=source.triangles;var world=vertices.Select(collider.transform.TransformPoint).ToArray();
        Require(world.Length<10000 && top.Length<30000 && world.Length>0,"Bounded mesh required.");
        Require(world.All(p=>Mathf.Abs(p.y-.04f)<.00001f),"Expected repaired v2 horizontal path sheet at Y=.04.");
        var receipt=new Receipt{originalMeshPath=AssetDatabase.GetAssetPath(source),originalVertices=vertices.Length,originalTriangles=top.Length/3,topY=world[0].y,convex=false,contactOffset=collider.contactOffset,cookingOptions=(int)collider.cookingOptions};
        receipt.originalMeshGuid=AssetDatabase.AssetPathToGUID(receipt.originalMeshPath);receipt.originalMeshSha256=Hash(source);
        var canonical=new Dictionary<Vector3,int>();var ids=new int[vertices.Length];
        for(int i=0;i<world.Length;i++){if(!canonical.TryGetValue(world[i],out int id)){id=canonical.Count;canonical.Add(world[i],id);}ids[i]=id;}
        receipt.weldedVertices=canonical.Count;
        var parent=Enumerable.Range(0,canonical.Count).ToArray();
        int Find(int x){while(parent[x]!=x){parent[x]=parent[parent[x]];x=parent[x];}return x;}
        void Join(int a,int b){parent[Find(a)]=Find(b);}
        var edges=new Dictionary<(int,int),List<(int,int)>>();
        void Edge(int a,int b){int x=ids[a],y=ids[b];Require(x!=y,"Degenerate edge.");var key=x<y?(x,y):(y,x);if(!edges.TryGetValue(key,out var list))edges.Add(key,list=new List<(int,int)>());list.Add((a,b));Join(x,y);}
        for(int i=0;i<top.Length;i+=3)
        {
            int a=top[i],b=top[i+1],c=top[i+2];
            Require(Vector3.Cross(world[b]-world[a],world[c]-world[a]).y>0,"Top must retain repaired upward winding.");
            Edge(a,b);Edge(b,c);Edge(c,a);
        }
        Require(edges.Values.All(e=>e.Count<=2),"Source has nonmanifold edges; do not extrude blindly.");
        receipt.components=Enumerable.Range(0,parent.Length).Select(Find).Distinct().Count();
        Require(receipt.components==11 && canonical.Count==320,"Expected exact 11 ribbons / 320 source positions.");
        var resultVertices=vertices.Concat(world.Select(p=>collider.transform.InverseTransformPoint(new Vector3(p.x,receipt.bottomY,p.z)))).ToArray();
        int count=vertices.Length;var triangles=new List<int>(top);
        for(int i=0;i<top.Length;i+=3){triangles.Add(top[i]+count);triangles.Add(top[i+2]+count);triangles.Add(top[i+1]+count);}
        foreach(var list in edges.Values.Where(e=>e.Count==1))
        {
            var (a,b)=list[0];triangles.AddRange(new[]{a,a+count,b+count,a,b+count,b});receipt.boundaryEdges++;
        }
        var mesh=new Mesh{name="CampPaths_ExactDownExtrusion_TEMP",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};owned.Add(mesh);
        mesh.vertices=resultVertices;mesh.triangles=triangles.ToArray();mesh.RecalculateBounds();
        receipt.candidateTriangles=triangles.Count/3;receipt.originalTopPreserved=mesh.vertices.Take(vertices.Length).SequenceEqual(vertices) && mesh.triangles.Take(top.Length).SequenceEqual(top);
        Require(receipt.originalTopPreserved,"Original top must be byte-value identical.");
        // Only the original collision component changes; no hull, merging, new bounds box or renderer mutation.
        collider.sharedMesh=mesh;Physics.SyncTransforms();
        receipt.visibleMeshPreserved=!filter || filter.sharedMesh==visible;
        receipt.candidateMeshSha256=Hash(mesh);return receipt;
    }
    public static SpawnReceipt SurveySpawn(EnvironmentMapDefinition map,bool applyToClone)
    {
        Require(map.MapId=="hf-campamento-pinar-v2" && map.HumanSpawnPoints.Length==5,"Camp v2 pool required.");
        var spawn=map.HumanSpawnPoints[4];var before=spawn.position;
        Require(Mathf.Abs(before.x-29.8f)<.0001f && Mathf.Abs(before.z-15.4f)<.0001f,"Expected authored Spawn05 XZ.");
        Vector3 origin=new Vector3(before.x,2,before.z);
        var hits=Physics.SphereCastAll(origin,.25f,Vector3.down,3,~0,QueryTriggerInteraction.Ignore).Where(h=>h.collider.transform.IsChildOf(map.transform) && h.normal.y>.55f && HiggsfieldCampPreparation.IsSupport(h.collider.name)).OrderBy(h=>h.distance).ToArray();
        Require(hits.Length>0,"No sphere support for Spawn05.");var hit=hits[0];
        var receipt=new SpawnReceipt{name=spawn.name,before=before,after=origin+Vector3.down*(hit.distance+.25f)+Vector3.up*.003f,supportCollider=hit.collider.name,supportPoint=hit.point,supportNormal=hit.normal};
        var probe=new GameObject("CampSpawnSphereProbe_TEMP"){hideFlags=HideFlags.HideAndDontSave};
        try
        {
            var capsule=probe.AddComponent<CapsuleCollider>();capsule.radius=.25f;capsule.height=1.72f;capsule.center=Vector3.up*.86f;
            receipt.beforePenetration=Depth(capsule,receipt.before,map.transform,out receipt.beforeBlocker);
            receipt.afterPenetration=Depth(capsule,receipt.after,map.transform,out receipt.afterBlocker);
            Require(receipt.afterPenetration<=.002f && Vector3.Distance(before,receipt.after)<.2f,"Spawn candidate must clear full capsule with bounded vertical displacement.");
            if(applyToClone){spawn.position=receipt.after;receipt.appliedToClone=true;Physics.SyncTransforms();}
        }
        finally{Object.DestroyImmediate(probe);Physics.SyncTransforms();}
        return receipt;
    }
    static float Depth(CapsuleCollider capsule,Vector3 position,Transform root,out string blocker)
    {
        float depth=0;blocker=null;
        foreach(var other in root.GetComponentsInChildren<Collider>(false).Where(c=>c.enabled && !c.isTrigger))
            if(Physics.ComputePenetration(capsule,position,Quaternion.identity,other,other.transform.position,other.transform.rotation,out _,out float d) && d>depth){depth=d;blocker=other.name;}
        return depth;
    }
    public static string Hash(Mesh mesh)
    {
        using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))using(var sha=SHA256.Create())
        {foreach(var p in mesh.vertices){writer.Write(p.x);writer.Write(p.y);writer.Write(p.z);}foreach(int t in mesh.triangles)writer.Write(t);writer.Flush();return BitConverter.ToString(sha.ComputeHash(stream.ToArray())).Replace("-","").ToLowerInvariant();}
    }
    static void Require(bool condition,string text){if(!condition)throw new Exception(text);}
}
