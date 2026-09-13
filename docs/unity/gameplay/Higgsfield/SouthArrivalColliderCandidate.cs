using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using LetMeSleep.Content.Environment;
using Object=UnityEngine.Object;

// Unapplied A/B candidate. Only mutates the fixture's disposable island clone.
public static class SouthArrivalColliderCandidate
{
    public sealed class Receipt
    {
        public string variant="local-convex-band", scope="TEMPORARY CLONE ONLY; native effectiveness unverified";
        public int sourceTriangles,removedTriangles,hullInputVertices;
        public float minZ,maxZ,thicknessDown=.04f,upperBoundTopRaise;
    }
    public static Receipt Apply(GameObject map,List<Object> owned)
    {
        if(map.GetComponent<EnvironmentMapDefinition>()?.MapId!="hf-isla-del-laguito-v2")throw new Exception("South arrival candidate is isla v2 only.");
        var matches=map.GetComponentsInChildren<MeshCollider>(true).Where(c=>c.name=="Path_South_Arrival_COLLIDABLE").ToArray();
        if(matches.Length!=1 || matches[0].convex)throw new Exception("Expected one concave south-arrival collider.");
        var original=matches[0];var source=original.sharedMesh;
        if(source==null || source.triangles.Length!=960 || original.GetComponent<MeshFilter>()?.sharedMesh!=source)throw new Exception("Unexpected source topology/binding; re-audit.");
        var vertices=source.vertices;var world=vertices.Select(v=>original.transform.TransformPoint(v)).ToArray();
        var triangles=source.triangles;var keep=new List<int>();var cut=new List<int>();
        for(int i=0;i<triangles.Length;i+=3)
        {
            float z=(world[triangles[i]].z+world[triangles[i+1]].z+world[triangles[i+2]].z)/3;
            var target=z> -25.3849f && z< -23.8701f?cut:keep;
            target.Add(triangles[i]);target.Add(triangles[i+1]);target.Add(triangles[i+2]);
        }
        if(cut.Count!=72)throw new Exception("Expected exactly 24 triangles in the three-row band.");
        var index=new Dictionary<Vector3Int,int>();var top=new List<Vector3>();var topWorld=new List<Vector3>();var topTriangles=new List<int>();
        foreach(var i in cut)
        {
            var w=world[i];var key=Vector3Int.RoundToInt(w*100000);
            if(!index.TryGetValue(key,out int n)){n=top.Count;index.Add(key,n);top.Add(vertices[i]);topWorld.Add(w);}topTriangles.Add(n);
        }
        if(top.Count!=20 || topWorld.Any(v=>v.z< -25.3851f || v.z> -23.8699f))throw new Exception("Source band coordinates differ from audited source.");
        float mx=topWorld.Average(v=>v.x),my=topWorld.Average(v=>v.y),mz=topWorld.Average(v=>v.z);
        float a=topWorld.Sum(v=>(v.x-mx)*(v.y-my))/topWorld.Sum(v=>(v.x-mx)*(v.x-mx));
        float b=topWorld.Sum(v=>(v.z-mz)*(v.y-my))/topWorld.Sum(v=>(v.z-mz)*(v.z-mz));
        var residual=topWorld.Select(v=>v.y-(my+a*(v.x-mx)+b*(v.z-mz))).ToArray();float bound=residual.Max()-residual.Min();
        if(bound>.01f)throw new Exception("Candidate top deviation bound exceeds 1cm; do not silently change geometry.");
        var receipt=new Receipt{sourceTriangles=triangles.Length/3,removedTriangles=cut.Count/3,hullInputVertices=top.Count*2,minZ=topWorld.Min(v=>v.z),maxZ=topWorld.Max(v=>v.z),upperBoundTopRaise=bound};
        var rest=new Mesh{name=source.name+"_SouthBandRemoved_TEMP"};owned.Add(rest);rest.vertices=vertices;rest.triangles=keep.ToArray();rest.RecalculateBounds();
        var hull=new Mesh{name="SouthArrival_ConvexBand_TEMP"};owned.Add(hull);
        var down=original.transform.InverseTransformVector(Vector3.down*.04f);hull.vertices=top.Concat(top.Select(v=>v+down)).ToArray();
        var faces=new List<int>(topTriangles);
        for(int i=0;i<topTriangles.Count;i+=3){faces.Add(topTriangles[i]+top.Count);faces.Add(topTriangles[i+2]+top.Count);faces.Add(topTriangles[i+1]+top.Count);}
        var edges=new Dictionary<(int,int),(int count,int from,int to)>();
        for(int i=0;i<topTriangles.Count;i+=3)for(int j=0;j<3;j++)
        {int x=topTriangles[i+j],y=topTriangles[i+(j+1)%3];var key=(Math.Min(x,y),Math.Max(x,y));edges.TryGetValue(key,out var value);edges[key]=(value.count+1,x,y);}
        foreach(var e in edges.Values.Where(e=>e.count==1)){int x=e.from,y=e.to;faces.AddRange(new[]{y,x,x+top.Count,y,x+top.Count,y+top.Count});}
        hull.triangles=faces.ToArray();hull.RecalculateBounds();
        var child=new GameObject("SouthArrival_ConvexBand_CANDIDATE"){layer=original.gameObject.layer};child.transform.SetParent(original.transform,false);
        var convex=child.AddComponent<MeshCollider>();convex.sharedMaterial=original.sharedMaterial;convex.contactOffset=original.contactOffset;convex.cookingOptions=original.cookingOptions;convex.convex=true;convex.sharedMesh=hull;
        original.sharedMesh=rest; // MeshFilter and source asset stay unchanged.
        Physics.SyncTransforms();return receipt;
    }
}
